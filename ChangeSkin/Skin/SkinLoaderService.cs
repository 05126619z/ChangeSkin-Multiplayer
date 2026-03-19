using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using BepInEx;
using ChangeSkin.Core;
using ChangeSkin.Progress;
using ChangeSkin.Utils;
using Newtonsoft.Json;
using UnityEngine;

namespace ChangeSkin.Skin;

/// <summary>
/// OOP implementation of skin loading/packing/downloading.
/// Keeps filesystem/network/zip details out of gameplay logic.
/// </summary>
internal sealed class SkinLoaderService : ISkinLoader
{
    private readonly ISkinPaths _paths;
    private readonly ISkinFileSystem _fs;
    private readonly IZipArchive _zip;
    private readonly IHttpDownloader _http;
    private readonly ISpriteLoader _spriteLoader;
    private readonly ISkinLog _log;

    public SkinLoaderService(
        ISkinPaths paths,
        ISkinFileSystem fs,
        IZipArchive zip,
        IHttpDownloader http,
        ISpriteLoader spriteLoader,
        ISkinLog log
    )
    {
        _paths = paths;
        _fs = fs;
        _zip = zip;
        _http = http;
        _spriteLoader = spriteLoader;
        _log = log;
    }

    public Dictionary<string, Sprite> LoadSkin(
        string skinName,
        IReadOnlyList<string> filenames,
        bool isLocal
    )
    {
        if (filenames == null)
            throw new ArgumentNullException(nameof(filenames));

        // In-memory cache
        if (SpriteCache.TryGet(skinName, isLocal, out var cached))
        {
            _log.Info($"Cache hit for skin '{skinName}' (local={isLocal})");
            return new Dictionary<string, Sprite>(cached, StringComparer.Ordinal);
        }

        string workPath;
        string originalSkinDir = null;

        if (isLocal)
        {
            originalSkinDir = _paths.PluginResourceSkinDir(skinName);
            UpdateLocalSkin(skinName);
            workPath = _paths.LocalSkinDir(skinName);
        }
        else
        {
            workPath = ResolveRemoteExtractedDir(skinName);
        }

        var dict = new Dictionary<string, Sprite>(StringComparer.Ordinal);

        // Try JSON manifest first — check workPath (temp cache), then original skin dir
        string manifestPath = FindManifestInDirs(workPath, originalSkinDir);

        if (manifestPath != null)
        {
            string manifestDir = Path.GetDirectoryName(manifestPath);
            _log.Info($"Loading skin '{skinName}' from manifest: {manifestPath}");
            var manifestDict = _spriteLoader.LoadFromManifest(manifestPath, manifestDir, filenames);

            // Also load any legacy files listed in the manifest
            if (manifestDict != null)
            {
                foreach (var kv in manifestDict)
                    dict[kv.Key] = kv.Value;
            }

            // Load legacy files from the manifest's legacyFiles list (filtered by filenames)
            var manifest = JsonConvert.DeserializeObject<SpriteManifest>(
                File.ReadAllText(manifestPath)
            );
            if (manifest?.legacyFiles != null)
            {
                foreach (string filename in manifest.legacyFiles)
                {
                    if (ShouldLoadFile(filename, filenames))
                        LoadSingleSprite(filename, manifestDir, dict);
                }
            }

            SpriteCache.Set(skinName, isLocal, dict);
            return dict;
        }

        // No manifest.json found — auto-generate one from the filenames list
        _log.Info($"No manifest.json found for skin '{skinName}', generating one...");

        // Generate into workPath (temp cache)
        _spriteLoader.GenerateManifest(workPath, filenames);

        // Also generate into the original skin directory if it exists and is different
        if (
            originalSkinDir != null
            && _fs.DirectoryExists(originalSkinDir)
            && !string.Equals(
                Path.GetFullPath(workPath),
                Path.GetFullPath(originalSkinDir),
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            _spriteLoader.GenerateManifest(originalSkinDir, filenames);
            _log.Info($"Also wrote manifest.json to original skin dir: {originalSkinDir}");
        }

        // Now load using the freshly generated manifest from workPath
        string generatedPath = Path.Combine(workPath, "manifest.json");
        var generatedDict = _spriteLoader.LoadFromManifest(generatedPath, workPath);
        if (generatedDict != null)
        {
            foreach (var kv in generatedDict)
                dict[kv.Key] = kv.Value;
        }

        SpriteCache.Set(skinName, isLocal, dict);
        return dict;
    }

    public Task<SkinLoadResult> LoadSkinAsync(
        string skinName,
        IReadOnlyList<string> filenames,
        bool isLocal
    )
    {
        return LoadSkinAsync(skinName, filenames, isLocal, null);
    }

    public Task<SkinLoadResult> LoadSkinAsync(
        string skinName,
        IReadOnlyList<string> filenames,
        bool isLocal,
        ILoadProgress progress
    )
    {
        if (isLocal)
        {
            // Local skins are fast (filesystem only), just run synchronously.
            try
            {
                progress?.ReportPhase("Loading local skin…");
                progress?.ReportPercent(0.3f);

                var sprites = LoadSkin(skinName, filenames, isLocal);

                progress?.Report("Loaded!", 1f);
                progress?.Complete();
                return Task.FromResult(SkinLoadResult.Ok(sprites));
            }
            catch (Exception ex)
            {
                progress?.Fail(ex.Message);
                return Task.FromResult(SkinLoadResult.Fail(ex.Message));
            }
        }

        // Remote skins: download + unpack on thread pool, then load sprites.
        return Task.Run(() =>
        {
            try
            {
                progress?.ReportPhase("Downloading…");
                progress?.ReportPercent(0f);

                string archiveName = DownloadRemoteZipAsync(skinName, progress)
                    .GetAwaiter().GetResult();

                progress?.Report("Extracting…", 0.6f);
                UnpackRemoteZip(archiveName);

                progress?.Report("Loading sprites…", 0.8f);
                var sprites = LoadSkin(archiveName, filenames, false);

                progress?.Complete();
                return SkinLoadResult.Ok(sprites);
            }
            catch (Exception ex)
            {
                progress?.Fail(ex.Message);
                return SkinLoadResult.Fail(ex.Message);
            }
        });
    }

    /// <summary>
    /// Searches for manifest.json in the given directories (in order).
    /// Returns the first path found, or null if none exists.
    /// </summary>
    private string FindManifestInDirs(params string[] dirs)
    {
        foreach (string dir in dirs)
        {
            if (string.IsNullOrEmpty(dir))
                continue;
            if (_spriteLoader.TryFindManifest(dir, out string path))
                return path;
        }
        return null;
    }

    private void LoadSingleSprite(string filename, string workPath, Dictionary<string, Sprite> dict)
    {
        try
        {
            string spritePath = Path.Combine(
                workPath,
                filename.Replace('/', Path.DirectorySeparatorChar)
            );
            Sprite sprite = _spriteLoader.Load(spritePath);
            dict[Path.GetFileNameWithoutExtension(filename)] = sprite;
        }
        catch (Exception)
        {
            _log.Warn(
                "It seems that some files for the skin are either missing or located in wrong folder structure. Follow the robot template for correct skin loading"
            );
            throw;
        }
    }

    private static bool ShouldLoadFile(string filename, IReadOnlyList<string> allowed)
    {
        if (allowed == null || allowed.Count == 0)
            return true;

        for (int i = 0; i < allowed.Count; i++)
        {
            if (string.Equals(filename, allowed[i], StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public void UpdateLocalSkin(string skinName)
    {
        SpriteCache.Remove(skinName, true);
        UpdateFromLocalResources(skinName);
        PackLocalZip(skinName);
    }

    public string UploadLocalZip(string skinName, string uploadUrl)
    {
        string filePath = _paths.LocalZipPath(skinName);
        byte[] fileBytes = File.ReadAllBytes(filePath);

        using HttpClient client = new HttpClient();
        using var formData = new MultipartFormDataContent();

        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        formData.Add(fileContent, "file", Path.GetFileName(filePath));

        HttpResponseMessage response = client.PostAsync(uploadUrl, formData).Result;

        if (response.StatusCode == HttpStatusCode.Created)
        {
            string result = response.Headers.GetValues("Location").FirstOrDefault();
            _log.Info(result);
            return result;
        }

        _log.Warn("Bad response from server: " + response.Content.ReadAsStringAsync().Result);
        return null;
    }

    public string DownloadRemoteZip(string url)
    {
        string fileName = Path.GetFileName(url);
        if (string.IsNullOrEmpty(fileName))
            throw new Exception("ChangeSkin invalid url, no filename");

        string archiveName = Path.GetFileNameWithoutExtension(fileName);
        string targetZipPath = _paths.RemoteZipPath(archiveName);

        _fs.EnsureDirectoryExists(Path.GetDirectoryName(targetZipPath));
        _http.DownloadToFile(url, targetZipPath);

        return archiveName;
    }

    public async Task<string> DownloadRemoteZipAsync(string url)
    {
        return await DownloadRemoteZipAsync(url, null);
    }

    public async Task<string> DownloadRemoteZipAsync(string url, ILoadProgress progress)
    {
        string fileName = Path.GetFileName(url);
        if (string.IsNullOrEmpty(fileName))
            throw new Exception("ChangeSkin invalid url, no filename");

        string archiveName = Path.GetFileNameWithoutExtension(fileName);
        string targetZipPath = _paths.RemoteZipPath(archiveName);

        _fs.EnsureDirectoryExists(Path.GetDirectoryName(targetZipPath));

        using var client = new HttpClient();
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        long totalBytes = response.Content.Headers.ContentLength ?? -1;

        using var httpStream = await response.Content.ReadAsStreamAsync();
        using var fileStream = new FileStream(
            targetZipPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None
        );

        byte[] buffer = new byte[81920];
        long totalRead = 0;
        int bytesRead;

        while ((bytesRead = await httpStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            await fileStream.WriteAsync(buffer, 0, bytesRead);
            totalRead += bytesRead;

            if (totalBytes > 0)
            {
                float pct = (float)totalRead / totalBytes;
                progress?.Report($"Downloading… {FormatBytes(totalRead)} / {FormatBytes(totalBytes)}", pct * 0.6f);
            }
            else
            {
                progress?.Report($"Downloading… {FormatBytes(totalRead)}", -1f);
            }
        }

        progress?.Report("Download complete", 0.6f);
        return archiveName;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }

    public void UnpackRemoteZip(string archiveName)
    {
        string targetPath = _paths.RemoteExtractDir(archiveName);
        string zipPath = _paths.RemoteZipPath(archiveName);

        // Clear sprite cache before deleting — textures may still hold file handles
        SpriteCache.Clear();

        if (_fs.DirectoryExists(targetPath))
            _fs.DeleteDirectory(targetPath, true);

        _fs.EnsureDirectoryExists(targetPath);
        _zip.ExtractToDirectory(zipPath, targetPath);
    }

    public void ClearCache()
    {
        // Clear sprite cache FIRST so textures release file handles
        SpriteCache.Clear();

        string root = _paths.RootCacheDir();
        if (_fs.DirectoryExists(root))
            _fs.DeleteDirectory(root, true);
    }

    private void UpdateFromLocalResources(string skinName)
    {
        string localDir = _paths.LocalSkinDir(skinName);

        // Clear sprite cache so textures release file handles before we delete
        SpriteCache.Remove(skinName, true);

        if (_fs.DirectoryExists(localDir))
            _fs.DeleteDirectory(localDir, true);

        _fs.EnsureDirectoryExists(_paths.LocalSkinsRoot());

        // Copy from plugin resources into temp cache
        string src = _paths.PluginResourceSkinDir(skinName);
        CopyFolderRecursively(src, localDir);
    }

    private void PackLocalZip(string skinName)
    {
        string zipDir = _paths.LocalZipRoot();
        _fs.EnsureDirectoryExists(zipDir);

        string zipPath = _paths.LocalZipPath(skinName);
        if (_fs.FileExists(zipPath))
            _fs.DeleteFile(zipPath);

        _zip.CreateFromDirectory(
            _paths.LocalSkinDir(skinName),
            zipPath,
            System.IO.Compression.CompressionLevel.Fastest,
            true
        );
    }

    private string ResolveRemoteExtractedDir(string archiveName)
    {
        string root = _paths.RemoteExtractDir(archiveName);
        if (!_fs.DirectoryExists(root))
            return root;

        string[] dirs = _fs.GetDirectories(root);
        string[] files = _fs.GetFiles(root);

        if (Core.Plugin.ModConfig.Verbose)
        {
            foreach (string d in dirs)
                _log.Info(d);
        }

        if (dirs.Length == 1 && files.Length == 0)
            return dirs[0];

        return root;
    }

    private void CopyFolderRecursively(string sourceFolder, string destFolder)
    {
        _fs.EnsureDirectoryExists(destFolder);

        foreach (string file in _fs.GetFiles(sourceFolder))
        {
            string name = Path.GetFileName(file);
            string dest = Path.Combine(destFolder, name);
            _fs.CopyFile(file, dest, overwrite: true);
        }

        foreach (string folder in _fs.GetDirectories(sourceFolder))
        {
            string name = Path.GetFileName(folder);
            string dest = Path.Combine(destFolder, name);
            CopyFolderRecursively(folder, dest);
        }
    }
}

// ── Abstractions (kept in same file for convenience) ────────────────────────

internal interface ISkinPaths
{
    string RootCacheDir();
    string LocalSkinsRoot();
    string RemoteSkinsRoot();

    string LocalSkinDir(string skinName);
    string RemoteExtractDir(string archiveName);

    string LocalZipRoot();
    string RemoteZipRoot();

    string LocalZipPath(string skinName);
    string RemoteZipPath(string archiveName);

    string PluginResourceSkinDir(string skinName);
}

internal sealed class DefaultSkinPaths : ISkinPaths
{
    public string RootCacheDir() => Path.Combine(Path.GetTempPath(), "ChangeSkin");

    public string LocalSkinsRoot() => Path.Combine(RootCacheDir(), "local");

    public string RemoteSkinsRoot() => Path.Combine(RootCacheDir(), "remote");

    public string LocalSkinDir(string skinName) => Path.Combine(LocalSkinsRoot(), skinName);

    public string RemoteExtractDir(string archiveName) =>
        Path.Combine(RemoteSkinsRoot(), archiveName);

    public string LocalZipRoot() => Path.Combine(RootCacheDir(), "zips", "local");

    public string RemoteZipRoot() => Path.Combine(RootCacheDir(), "zips", "remote");

    public string LocalZipPath(string skinName) => Path.Combine(LocalZipRoot(), $"{skinName}.zip");

    public string RemoteZipPath(string archiveName) =>
        Path.Combine(RemoteZipRoot(), $"{archiveName}.zip");

    public string PluginResourceSkinDir(string skinName) =>
        Path.Combine(Paths.PluginPath, "ChangeSkin", "resources", skinName);
}

internal interface ISkinFileSystem
{
    void EnsureDirectoryExists(string path);
    bool DirectoryExists(string path);
    bool FileExists(string path);
    void DeleteDirectory(string path, bool recursive);
    void DeleteFile(string path);
    string[] GetFiles(string path);
    string[] GetDirectories(string path);
    void CopyFile(string source, string dest, bool overwrite);
}

internal sealed class SystemSkinFileSystem : ISkinFileSystem
{
    public void EnsureDirectoryExists(string path)
    {
        if (string.IsNullOrEmpty(path))
            return;
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public bool FileExists(string path) => File.Exists(path);

    public void DeleteDirectory(string path, bool recursive)
    {
        if (!Directory.Exists(path))
            return;

        if (recursive)
        {
            // Remove read-only attributes and retry on locked files
            ForceDeleteDirectory(path);
        }
        else
        {
            Directory.Delete(path, false);
        }
    }

    /// <summary>
    /// Recursively deletes a directory, clearing read-only attributes
    /// and skipping files that are locked by other processes.
    /// </summary>
    private static void ForceDeleteDirectory(string dir)
    {
        // First pass: clear read-only attributes on all files
        try
        {
            foreach (string file in Directory.GetFiles(dir))
            {
                try
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }
                catch
                {
                    // ignore — file may be locked
                }
            }
            foreach (string sub in Directory.GetDirectories(dir))
            {
                ForceClearAttributes(sub);
            }
        }
        catch
        {
            // ignore
        }

        // Second pass: delete files (skip locked ones)
        try
        {
            foreach (string file in Directory.GetFiles(dir))
            {
                try
                {
                    File.Delete(file);
                }
                catch (UnauthorizedAccessException)
                {
                    // File is locked — skip it
                }
                catch (IOException)
                {
                    // File is in use — skip it
                }
            }
        }
        catch
        {
            // ignore
        }

        // Third pass: delete subdirectories
        try
        {
            foreach (string sub in Directory.GetDirectories(dir))
            {
                ForceDeleteDirectory(sub);
            }
        }
        catch
        {
            // ignore
        }

        // Finally delete this directory
        try
        {
            Directory.Delete(dir, false);
        }
        catch
        {
            // If it still can't be deleted, leave it — some files are still locked
        }
    }

    private static void ForceClearAttributes(string dir)
    {
        try
        {
            foreach (string file in Directory.GetFiles(dir))
            {
                try { File.SetAttributes(file, FileAttributes.Normal); }
                catch { }
            }
            foreach (string sub in Directory.GetDirectories(dir))
            {
                ForceClearAttributes(sub);
            }
        }
        catch { }
    }

    public void DeleteFile(string path)
    {
        if (File.Exists(path))
        {
            try { File.SetAttributes(path, FileAttributes.Normal); }
            catch { }
            File.Delete(path);
        }
    }

    public string[] GetFiles(string path) =>
        Directory.Exists(path) ? Directory.GetFiles(path) : Array.Empty<string>();

    public string[] GetDirectories(string path) =>
        Directory.Exists(path) ? Directory.GetDirectories(path) : Array.Empty<string>();

    public void CopyFile(string source, string dest, bool overwrite) =>
        File.Copy(source, dest, overwrite);
}

internal interface IZipArchive
{
    void CreateFromDirectory(
        string sourceDirectoryName,
        string destinationArchiveFileName,
        System.IO.Compression.CompressionLevel compressionLevel,
        bool includeBaseDirectory
    );
    void ExtractToDirectory(string sourceArchiveFileName, string destinationDirectoryName);
}

internal sealed class SystemZipArchive : IZipArchive
{
    public void CreateFromDirectory(
        string sourceDirectoryName,
        string destinationArchiveFileName,
        System.IO.Compression.CompressionLevel compressionLevel,
        bool includeBaseDirectory
    ) =>
        ZipFile.CreateFromDirectory(
            sourceDirectoryName,
            destinationArchiveFileName,
            compressionLevel,
            includeBaseDirectory
        );

    public void ExtractToDirectory(string sourceArchiveFileName, string destinationDirectoryName) =>
        ZipFile.ExtractToDirectory(sourceArchiveFileName, destinationDirectoryName);
}

internal interface IHttpDownloader
{
    void DownloadToFile(string url, string filePath);
}

internal sealed class HttpClientDownloader : IHttpDownloader
{
    public void DownloadToFile(string url, string filePath)
    {
        using var client = new HttpClient();
        using var s = client.GetStreamAsync(url).GetAwaiter().GetResult();
        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        s.CopyTo(fs);
    }
}

internal interface ISpriteLoader
{
    Sprite Load(string path);
    Dictionary<string, Sprite> LoadFromManifest(
        string manifestPath,
        string skinDir,
        IReadOnlyList<string> allowedFilenames = null
    );
    bool TryFindManifest(string skinDir, out string manifestPath);

    void GenerateManifest(string skinDir, IReadOnlyList<string> filenames);
}

internal sealed class UtilsSpriteLoader : ISpriteLoader
{
    public Sprite Load(string path) => SpriteHelpers.LoadSprite(path);

    public Dictionary<string, Sprite> LoadFromManifest(
        string manifestPath,
        string skinDir,
        IReadOnlyList<string> allowedFilenames = null
    )
    {
        string json = File.ReadAllText(manifestPath);
        var manifest = Newtonsoft.Json.JsonConvert.DeserializeObject<SpriteManifest>(json);
        if (manifest == null || manifest.sprites == null || manifest.sprites.Count == 0)
            throw new Exception($"SpriteManifest at {manifestPath} has no sprite entries");

        var defaults = manifest.defaults ?? new SpriteDefaults();
        var dict = new Dictionary<string, Sprite>(StringComparer.Ordinal);

        foreach (var entry in manifest.sprites)
        {
            if (string.IsNullOrEmpty(entry.image))
            {
                Core.Plugin.Logger.LogWarning($"SpriteManifest entry has no 'image' path, skipping");
                continue;
            }

            // Filter: only load if the image path is in the allowed filenames list
            if (allowedFilenames != null && allowedFilenames.Count > 0)
            {
                bool allowed = false;
                for (int i = 0; i < allowedFilenames.Count; i++)
                {
                    if (
                        string.Equals(
                            entry.image,
                            allowedFilenames[i],
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        allowed = true;
                        break;
                    }
                }
                if (!allowed)
                    continue;
            }

            Sprite sprite = SpriteHelpers.LoadSpriteFromEntry(entry, defaults, skinDir);
            dict[sprite.name] = sprite;
        }

        return dict;
    }

    public bool TryFindManifest(string skinDir, out string manifestPath)
    {
        string candidate = Path.Combine(skinDir, "manifest.json");
        if (File.Exists(candidate))
        {
            manifestPath = candidate;
            return true;
        }
        manifestPath = null;
        return false;
    }

    public void GenerateManifest(string skinDir, IReadOnlyList<string> filenames)
    {
        var manifest = SpriteManifest.FromFilenames(filenames);
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(
            manifest,
            Newtonsoft.Json.Formatting.Indented
        );
        string manifestPath = Path.Combine(skinDir, "manifest.json");
        File.WriteAllText(manifestPath, json);
        Core.Plugin.Logger.LogInfo($"Auto-generated manifest.json for skin at: {skinDir}");
    }
}

internal interface ISkinLog
{
    void Info(string message);
    void Warn(string message);
}

internal sealed class PluginSkinLog : ISkinLog
{
    public void Info(string message)
    {
        if (!string.IsNullOrEmpty(message))
            Core.Plugin.Logger.LogInfo(message);
    }

    public void Warn(string message)
    {
        if (!string.IsNullOrEmpty(message))
            Core.Plugin.Logger.LogWarning(message);
    }
}
