using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ChangeSkin.Progress;
using UnityEngine;

namespace ChangeSkin.Skin;

/// <summary>
/// Legacy static facade kept for API compatibility.
/// Internally delegates work to an <see cref="ISkinLoader"/> implementation.
/// </summary>
internal static class SkinLoader
{
    private static ISkinLoader _impl;
    private static ISkinFileSystem _fs;
    private static ISkinPaths _paths;

    /// <summary>Override implementation (composition root).</summary>
    public static void Use(ISkinLoader impl)
    {
        _impl = impl;
    }

    /// <summary>Set filesystem and paths abstractions for direct access by other components.</summary>
    public static void UseFileSystem(ISkinFileSystem fs, ISkinPaths paths)
    {
        _fs = fs;
        _paths = paths;
    }

    /// <summary>Direct access to the filesystem abstraction (for ChangeBody file I/O).</summary>
    public static ISkinFileSystem FileSystem => _fs ?? EnsureDefaults().Item1;

    /// <summary>Direct access to the paths abstraction (for ChangeBody path resolution).</summary>
    public static ISkinPaths Paths => _paths ?? EnsureDefaults().Item2;

    private static (ISkinFileSystem, ISkinPaths) EnsureDefaults()
    {
        var fs = new SystemSkinFileSystem();
        var p = new DefaultSkinPaths();
        _fs = fs;
        _paths = p;
        return (fs, p);
    }

    private static ISkinLoader Impl
    {
        get
        {
            if (_impl != null)
                return _impl;

            // Safe default for callers that don't wire DI.
            _impl = new SkinLoaderService(
                new DefaultSkinPaths(),
                new SystemSkinFileSystem(),
                new SystemZipArchive(),
                new HttpClientDownloader(),
                new UtilsSpriteLoader(),
                new PluginSkinLog()
            );
            return _impl;
        }
    }

    public static void LoadSkin(
        string skinName,
        string[] filenames,
        bool isLocal,
        ref Dictionary<string, Sprite> dict
    )
    {
        if (dict == null)
            dict = new Dictionary<string, Sprite>(StringComparer.Ordinal);

        Dictionary<string, Sprite> loaded = Impl.LoadSkin(skinName, filenames, isLocal);
        dict.Clear();
        foreach (var kv in loaded)
            dict[kv.Key] = kv.Value;
    }

    public static Task<SkinLoadResult> LoadSkinAsync(
        string skinName,
        string[] filenames,
        bool isLocal
    )
    {
        return Impl.LoadSkinAsync(skinName, filenames, isLocal);
    }

    public static Task<SkinLoadResult> LoadSkinAsync(
        string skinName,
        string[] filenames,
        bool isLocal,
        ILoadProgress progress
    )
    {
        return Impl.LoadSkinAsync(skinName, filenames, isLocal, progress);
    }

    internal static void UpdateLocalSkin(string skinName) => Impl.UpdateLocalSkin(skinName);

    internal static string UploadLocal(string skinName, string uploadUrl) =>
        Impl.UploadLocalZip(skinName, uploadUrl);

    public static string DownloadRemote(string url) => Impl.DownloadRemoteZip(url);

    public static Task<string> DownloadRemoteAsync(string url) =>
        Impl.DownloadRemoteZipAsync(url);

    public static Task<string> DownloadRemoteAsync(string url, ILoadProgress progress) =>
        Impl.DownloadRemoteZipAsync(url, progress);

    public static void UnpackRemote(string archiveName) => Impl.UnpackRemoteZip(archiveName);

    public static void ClearCache() => Impl.ClearCache();
}
