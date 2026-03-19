using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ChangeSkin.Core;
using ChangeSkin.Storage;
using ChangeSkin.Utils;
using Newtonsoft.Json;
using UnityEngine;

namespace ChangeSkin.Skin;

public class ChangeBody : MonoBehaviour, IDisposable
{
    private Body body;
    private readonly TextureStorage textureStorage = new();
    private readonly ChangeFacialExpression changeFacialExpression = new();

    public string skinName;
    private string loadedName;
    private bool loaded = false;
    private bool working = false;
    private bool isLocal = true;
    private bool isLoading = false; // prevents double-load

    public bool isLocalChangeBody = false;
    public bool isBanned = false;
    public string skinURL;

    private Coroutine _replacementLoop;
    private SpriteRenderer[] _cachedRenderers;

    private SpriteRenderer[] GetRenderers()
    {
        if (_cachedRenderers == null || _cachedRenderers.Length == 0)
            _cachedRenderers = gameObject.GetComponentsInChildren<SpriteRenderer>(true);
        return _cachedRenderers;
    }

    private void LateUpdate()
    {
        if (working && loaded)
            ApplyReplacementsImmediate();
    }

    private void ApplyReplacementsImmediate()
    {
        var renderers = GetRenderers();
        if (textureStorage.newBodySprites == null || textureStorage.newBodySprites.Count == 0)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null)
                continue;
            var cur = r.sprite;
            if (cur == null)
                continue;

            if (
                textureStorage.newBodySprites.TryGetValue(cur.name, out var replacement)
                && replacement != null
            )
                r.sprite = replacement;
        }
    }

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>
    /// Loads a local skin asynchronously — file I/O on background thread,
    /// sprite creation on main thread via coroutine.
    /// </summary>
    public void LoadSkinLocal(string skinName)
    {
        if (isLoading)
        {
            Core.Plugin.Logger.LogWarning("[ChangeBody] LoadSkinLocal: already loading, skipping");
            return;
        }

        isLoading = true;
        bool wasReplacing = working;

        if (working)
            StopReplacement();
        if (loaded)
            Unload();

        loaded = false;
        this.skinName = skinName;
        isLocal = true;
        textureStorage.newBodySprites = [];

        StartCoroutine(LoadSkinLocalCoroutine(skinName, wasReplacing));
    }

    /// <summary>
    /// Loads a remote skin — download + file I/O on background thread,
    /// sprite creation on main thread via coroutine.
    /// </summary>
    public void LoadSkinURL(string url)
    {
        if (isLoading)
        {
            Core.Plugin.Logger.LogWarning("[ChangeBody] LoadSkinURL: already loading, skipping");
            return;
        }

        isLoading = true;
        bool wasReplacing = working;

        if (working)
            StopReplacement();
        if (loaded)
            Unload();

        skinURL = url;
        loaded = false;
        isLocal = false;

        if (!Core.Plugin.ModConfig.SkinDownloading)
        {
            Core.Plugin.Logger.LogWarning("Skin downloading is disabled by the rules");
            isLoading = false;
            return;
        }

        StartCoroutine(LoadSkinURLCoroutine(url, wasReplacing));
    }

    /// <summary>
    /// Async version for network callers — returns true on success.
    /// Uses coroutine internally to avoid blocking the main thread.
    /// </summary>
    public Task<bool> LoadSkinURLAsync(string url)
    {
        var tcs = new TaskCompletionSource<bool>();
        StartCoroutine(LoadSkinURLAsyncCoroutine(url, tcs));
        return tcs.Task;
    }

    public void Reload()
    {
        if (isLoading)
        {
            Core.Plugin.Logger.LogWarning("[ChangeBody] Reload: already loading, skipping");
            return;
        }

        StopReplacement();
        Unload();

        if (isLocal)
            LoadSkinLocal(skinName);
        else
            LoadSkinURL(skinURL);
    }

    public void Unload()
    {
        textureStorage.newBodySprites.Clear();
        loadedName = null;
        loaded = false;
    }

    public void BeginReplacement()
    {
        if (isBanned)
            return;
        if (skinName == null && skinURL == null)
            return;
        if (body == null)
            body = gameObject.GetComponent<Body>();

        // If sprites aren't loaded yet, trigger load and return.
        // Replacement will start automatically when loading completes.
        if (textureStorage.newBodySprites == null || textureStorage.newBodySprites.Count == 0)
        {
            if (isLoading)
                return; // already loading, will start replacement when done

            if (isLocal)
                LoadSkinLocal(skinName);
            else
                LoadSkinURL(skinURL);
            return;
        }

        changeFacialExpression.SwapFacialExpression(
            body.GetComponentInChildren<FacialExpression>(),
            textureStorage.newBodySprites
        );

        working = true;

        if (_replacementLoop == null)
            _replacementLoop = StartCoroutine(ReplacementLoop());
    }

    public void StopReplacement()
    {
        if (!working)
            return;
        working = false;

        if (_replacementLoop != null)
        {
            StopCoroutine(_replacementLoop);
            _replacementLoop = null;
        }

        ReturnSprites();

        if (body != null)
        {
            changeFacialExpression.UnSwapFacialExpression(
                body.GetComponentInChildren<FacialExpression>()
            );
        }
    }

    // ── Coroutines (non-blocking load) ─────────────────────────────────────

    private IEnumerator LoadSkinLocalCoroutine(string skinName, bool wasReplacing)
    {
        // ── In-memory cache check ──────────────────────────────────────────
        if (SpriteCache.TryGet(skinName, true, out var cached))
        {
            Core.Plugin.Logger.LogInfo(
                $"[ChangeBody] Cache hit for local skin '{skinName}' ({cached.Count} sprites)"
            );
            textureStorage.newBodySprites = new Dictionary<string, Sprite>(cached);
            loadedName = skinName;
            loaded = true;
            isLoading = false;
            if (wasReplacing)
                BeginReplacement();
            yield break;
        }

        var fs = SkinLoader.FileSystem;
        var paths = SkinLoader.Paths;
        SpriteManifest manifest = null;
        string manifestPath = null;
        var bg = new BgResult();

        // Phase 1: file I/O on background thread, using ISkinFileSystem abstraction
        yield return RunInBackground(
            () =>
            {
                string originalSkinDir = paths.PluginResourceSkinDir(skinName);
                string workPath = paths.LocalSkinDir(skinName);

                if (fs.DirectoryExists(workPath))
                    fs.DeleteDirectory(workPath, true);
                fs.EnsureDirectoryExists(paths.LocalSkinsRoot());
                CopyFolderViaFs(originalSkinDir, workPath, fs);

                manifestPath = FindManifestViaFs(workPath, originalSkinDir, fs);
                if (manifestPath == null)
                {
                    GenerateManifestViaFs(workPath, bodyfilenames, fs);
                    manifestPath = System.IO.Path.Combine(workPath, "manifest.json");
                }

                string json = System.IO.File.ReadAllText(manifestPath);
                manifest = Newtonsoft.Json.JsonConvert.DeserializeObject<SpriteManifest>(json);
            },
            bg
        );

        if (bg.Error != null)
        {
            Core.Plugin.Logger.LogError(
                $"[ChangeBody] LoadSkinLocal background failed: {bg.Error.Message}"
            );
            isLoading = false;
            yield break;
        }

        // Phase 2: create sprites on main thread (Unity API requires main thread)
        CreateSpritesFromManifest(manifest, manifestPath);

        loadedName = skinName;
        loaded = true;
        isLoading = false;

        if (wasReplacing)
            BeginReplacement();
    }

    private IEnumerator LoadSkinURLCoroutine(string url, bool wasReplacing)
    {
        // ── In-memory cache check (keyed by URL) ──────────────────────────
        if (SpriteCache.TryGet(url, false, out var cached))
        {
            Core.Plugin.Logger.LogInfo(
                $"[ChangeBody] Cache hit for remote skin '{url}' ({cached.Count} sprites)"
            );
            textureStorage.newBodySprites = new Dictionary<string, Sprite>(cached);
            skinName = url;
            loadedName = url;
            loaded = true;
            isLoading = false;
            if (wasReplacing)
                BeginReplacement();
            yield break;
        }

        var fs = SkinLoader.FileSystem;
        var paths = SkinLoader.Paths;
        string archiveName = null;
        string manifestPath = null;
        SpriteManifest manifest = null;
        var bg = new BgResult();

        // Phase 1: download (async, yields per-frame)
        var downloadTask = SkinLoader.DownloadRemoteAsync(url);
        while (!downloadTask.IsCompleted)
            yield return null;

        if (downloadTask.IsFaulted)
        {
            Core.Plugin.Logger.LogError(
                $"[ChangeBody] Download failed: {downloadTask.Exception?.Message}"
            );
            isLoading = false;
            yield break;
        }
        archiveName = downloadTask.Result;

        // Phase 2: unpack + find manifest on background thread, using ISkinFileSystem abstraction
        yield return RunInBackground(
            () =>
            {
                SkinLoader.UnpackRemote(archiveName);

                string workPath = paths.RemoteExtractDir(archiveName);

                if (fs.DirectoryExists(workPath))
                {
                    string[] dirs = fs.GetDirectories(workPath);
                    string[] files = fs.GetFiles(workPath);
                    if (dirs.Length == 1 && files.Length == 0)
                        workPath = dirs[0];
                }

                manifestPath = FindManifestViaFs(workPath, null, fs);
                if (manifestPath == null)
                {
                    GenerateManifestViaFs(workPath, bodyfilenames, fs);
                    manifestPath = System.IO.Path.Combine(workPath, "manifest.json");
                }

                string json = System.IO.File.ReadAllText(manifestPath);
                manifest = Newtonsoft.Json.JsonConvert.DeserializeObject<SpriteManifest>(json);
            },
            bg
        );

        if (bg.Error != null)
        {
            Core.Plugin.Logger.LogError(
                $"[ChangeBody] LoadSkinURL background failed: {bg.Error.Message}"
            );
            isLoading = false;
            yield break;
        }

        skinName = archiveName;

        // Phase 3: create sprites on main thread
        CreateSpritesFromManifest(manifest, manifestPath);

        loadedName = archiveName;
        loaded = true;
        isLoading = false;

        if (wasReplacing)
            BeginReplacement();
    }

    private IEnumerator LoadSkinURLAsyncCoroutine(string url, TaskCompletionSource<bool> tcs)
    {
        if (isLoading)
        {
            Core.Plugin.Logger.LogWarning(
                "[ChangeBody] LoadSkinURLAsync: already loading, skipping"
            );
            tcs.SetResult(false);
            yield break;
        }

        isLoading = true;
        bool wasReplacing = working;

        if (working)
            StopReplacement();
        if (loaded)
            Unload();

        skinURL = url;
        loaded = false;
        isLocal = false;

        if (!Core.Plugin.ModConfig.SkinDownloading)
        {
            Core.Plugin.Logger.LogWarning("Skin downloading is disabled by the rules");
            isLoading = false;
            tcs.SetResult(false);
            yield break;
        }

        // ── In-memory cache check (keyed by URL) ──────────────────────────
        if (SpriteCache.TryGet(url, false, out var cached))
        {
            Core.Plugin.Logger.LogInfo(
                $"[ChangeBody] Cache hit for remote skin '{url}' ({cached.Count} sprites)"
            );
            textureStorage.newBodySprites = new Dictionary<string, Sprite>(cached);
            skinName = url;
            loadedName = url;
            loaded = true;
            isLoading = false;
            if (wasReplacing)
                BeginReplacement();
            tcs.SetResult(true);
            yield break;
        }

        var fs = SkinLoader.FileSystem;
        var paths = SkinLoader.Paths;
        string archiveName = null;
        string manifestPath = null;
        SpriteManifest manifest = null;
        var bg = new BgResult();

        // Download
        var downloadTask = SkinLoader.DownloadRemoteAsync(url);
        while (!downloadTask.IsCompleted)
            yield return null;

        if (downloadTask.IsFaulted)
        {
            Core.Plugin.Logger.LogError(
                $"[ChangeBody] Download failed: {downloadTask.Exception?.Message}"
            );
            isLoading = false;
            tcs.SetResult(false);
            yield break;
        }
        archiveName = downloadTask.Result;

        // Unpack + find manifest on background thread, using ISkinFileSystem abstraction
        yield return RunInBackground(
            () =>
            {
                SkinLoader.UnpackRemote(archiveName);

                string workPath = paths.RemoteExtractDir(archiveName);

                if (fs.DirectoryExists(workPath))
                {
                    string[] dirs = fs.GetDirectories(workPath);
                    string[] files = fs.GetFiles(workPath);
                    if (dirs.Length == 1 && files.Length == 0)
                        workPath = dirs[0];
                }

                manifestPath = FindManifestViaFs(workPath, null, fs);
                if (manifestPath == null)
                {
                    GenerateManifestViaFs(workPath, bodyfilenames, fs);
                    manifestPath = System.IO.Path.Combine(workPath, "manifest.json");
                }

                string json = System.IO.File.ReadAllText(manifestPath);
                manifest = Newtonsoft.Json.JsonConvert.DeserializeObject<SpriteManifest>(json);
            },
            bg
        );

        if (bg.Error != null)
        {
            Core.Plugin.Logger.LogError(
                $"[ChangeBody] LoadSkinURLAsync background failed: {bg.Error.Message}"
            );
            isLoading = false;
            tcs.SetResult(false);
            yield break;
        }

        skinName = archiveName;

        // Create sprites on main thread
        CreateSpritesFromManifest(manifest, manifestPath);

        loadedName = archiveName;
        loaded = true;
        isLoading = false;

        if (wasReplacing)
            BeginReplacement();

        tcs.SetResult(true);
    }

    // ── Sprite creation (main thread only) ─────────────────────────────────

    private void CreateSpritesFromManifest(SpriteManifest manifest, string manifestPath)
    {
        if (manifest == null || manifest.sprites == null)
            return;

        string manifestDir = Path.GetDirectoryName(manifestPath);
        var defaults = manifest.defaults ?? new SpriteDefaults();
        var dict = new Dictionary<string, Sprite>(StringComparer.Ordinal);

        foreach (var entry in manifest.sprites)
        {
            if (string.IsNullOrEmpty(entry.image))
                continue;
            if (!ShouldLoadFile(entry.image, bodyfilenames))
                continue;

            try
            {
                Sprite sprite = SpriteHelpers.LoadSpriteFromEntry(entry, defaults, manifestDir);
                dict[sprite.name] = sprite;
            }
            catch (Exception ex)
            {
                Core.Plugin.Logger.LogWarning(
                    $"Failed to load sprite entry '{entry.image}': {ex.Message}"
                );
            }
        }

        // Also load legacy files
        if (manifest.legacyFiles != null)
        {
            foreach (string filename in manifest.legacyFiles)
            {
                if (!ShouldLoadFile(filename, bodyfilenames))
                    continue;
                try
                {
                    string spritePath = Path.Combine(
                        manifestDir,
                        filename.Replace('/', Path.DirectorySeparatorChar)
                    );
                    Sprite sprite = SpriteHelpers.LoadSprite(spritePath);
                    dict[Path.GetFileNameWithoutExtension(filename)] = sprite;
                }
                catch (Exception ex)
                {
                    Core.Plugin.Logger.LogWarning(
                        $"Failed to load legacy sprite '{filename}': {ex.Message}"
                    );
                }
            }
        }

        textureStorage.newBodySprites = dict;

        // ── Store in in-memory cache ──────────────────────────────────────
        string cacheKey = skinName ?? loadedName;
        if (!string.IsNullOrEmpty(cacheKey))
        {
            SpriteCache.Set(cacheKey, isLocal, dict);
            Core.Plugin.Logger.LogInfo(
                $"[ChangeBody] Cached {dict.Count} sprites for '{cacheKey}' (local={isLocal})"
            );
        }
    }

    // ── Background thread helper ───────────────────────────────────────────

    private sealed class BgResult
    {
        public Exception Error;
    }

    private static IEnumerator RunInBackground(Action action, BgResult result)
    {
        result.Error = null;
        var task = Task.Run(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                result.Error = ex;
            }
        });

        while (!task.IsCompleted)
            yield return null;
    }

    // ── File I/O helpers via ISkinFileSystem abstraction (thread-safe, no Unity API) ──

    private static string FindManifestViaFs(string dir1, string dir2, ISkinFileSystem fs)
    {
        foreach (string dir in new[] { dir1, dir2 })
        {
            if (string.IsNullOrEmpty(dir))
                continue;
            string candidate = System.IO.Path.Combine(dir, "manifest.json");
            if (fs.FileExists(candidate))
                return candidate;
        }
        return null;
    }

    private static void GenerateManifestViaFs(
        string skinDir,
        string[] filenames,
        ISkinFileSystem fs
    )
    {
        var manifest = SpriteManifest.FromFilenames(filenames);
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(
            manifest,
            Newtonsoft.Json.Formatting.Indented
        );
        string path = System.IO.Path.Combine(skinDir, "manifest.json");
        // File.WriteAllText is fine here — ISkinFileSystem doesn't have a WriteFile method,
        // and we're on a background thread anyway.
        System.IO.File.WriteAllText(path, json);
    }

    private static void CopyFolderViaFs(string src, string dst, ISkinFileSystem fs)
    {
        fs.EnsureDirectoryExists(dst);
        foreach (string file in fs.GetFiles(src))
        {
            string name = System.IO.Path.GetFileName(file);
            string dest = System.IO.Path.Combine(dst, name);
            fs.CopyFile(file, dest, overwrite: true);
        }
        foreach (string dir in fs.GetDirectories(src))
        {
            string name = System.IO.Path.GetFileName(dir);
            string dest = System.IO.Path.Combine(dst, name);
            CopyFolderViaFs(dir, dest, fs);
        }
    }

    private static bool ShouldLoadFile(string filename, string[] allowed)
    {
        if (allowed == null || allowed.Length == 0)
            return true;
        for (int i = 0; i < allowed.Length; i++)
        {
            if (string.Equals(filename, allowed[i], StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    // ── Unity lifecycle ────────────────────────────────────────────────────

    private void Start()
    {
        body = gameObject.GetComponent<Body>();
    }

    public void Dispose()
    {
        StopReplacement();
        Unload();
    }

    // ── Sprite replacement loop ────────────────────────────────────────────

    private IEnumerator ReplacementLoop()
    {
        while (working)
        {
            if (!loaded)
            {
                yield return null;
                continue;
            }

            yield return null;
            yield return ReplaceSpritesOnce();
        }
    }

    private IEnumerator ReplaceSpritesOnce()
    {
        var renderers = GetRenderers();

        foreach (Sprite sprite in textureStorage.newBodySprites.Values)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer spriteRenderer = renderers[i];
                if (spriteRenderer == null || spriteRenderer.sprite == null)
                    continue;

                if (spriteRenderer.sprite.name == sprite.name)
                    spriteRenderer.sprite = sprite;
            }

            yield return null;
        }
    }

    internal void ReturnSprites()
    {
        if (TextureStorage.OgSprites == null)
            return;

        var renderers = GetRenderers();

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = renderers[i];
            if (spriteRenderer == null || spriteRenderer.sprite == null)
                continue;

            if (
                TextureStorage.OgSprites.TryGetValue(
                    spriteRenderer.sprite.name,
                    out Sprite originalSprite
                )
            )
            {
                spriteRenderer.sprite = originalSprite;
            }
        }
    }

    // ── Static helpers ─────────────────────────────────────────────────────

    public static string UploadLocalSkin(string skinName, string uploadUrl)
    {
        if (!Core.Plugin.ModConfig.SkinUploading)
        {
            Core.Plugin.Logger.LogWarning("Skin uploading is disabled by the rules");
            return null;
        }
        SkinLoader.UpdateLocalSkin(skinName);
        return SkinLoader.UploadLocal(skinName, uploadUrl);
    }

    internal static readonly string[] bodyfilenames =
    {
        "Body/experimentTail.png",
        "Body/experimentFoot.png",
        "Body/experimentUpTorso.png",
        "Body/experimentUpArm.png",
        "Body/experimentThigh.png",
        "Body/experimentDownTorso.png",
        "Body/experimentDownArm.png",
        "Body/experimentCrus.png",
        "Body/experimentEyeGoneHealed.png",
        "Body/experimentEyeGone.png",
        "Body/experimentEyeClosed.png",
        "Body/experimentEyeScaredBack.png",
        "Body/experimentEyeScared.png",
        "Body/experimentEyeSadBack.png",
        "Body/experimentEyeSad.png",
        "Body/experimentHead.png",
        "Body/experimentEyePanic.png",
        "Body/experimentEyeOpen.png",
        "Body/experimentEyeLookBack.png",
        "Body/experimentEyeHalfClosedBack.png",
        "Body/experimentEyeHalfClosed.png",
        "Body/experimentHeadDisfigured3Healed.png",
        "Body/experimentHeadDisfigured3.png",
        "Body/experimentHeadDisfigured2Healed.png",
        "Body/experimentHeadDisfigured2.png",
        "Body/experimentHeadDisfigured1Healed.png",
        "Body/experimentHeadDisfigured1.png",
        "Body/experimentHeadBackMouth.png",
        "Body/experimentHeadBackMouthMini.png",
        "Body/experimentHeadBack.png",
        "Body/experimentHandB.png",
        "Body/experimentHandF.png",
        "Body/experimentNosebleed.png",
        "Body/experimentEyeHappy.png",
    };
}
