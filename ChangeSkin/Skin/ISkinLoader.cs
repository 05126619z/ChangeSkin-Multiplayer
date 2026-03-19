using System.Collections.Generic;
using System.Threading.Tasks;
using ChangeSkin.Progress;
using UnityEngine;

namespace ChangeSkin.Skin;

internal interface ISkinLoader
{
    Dictionary<string, Sprite> LoadSkin(
        string skinName,
        IReadOnlyList<string> filenames,
        bool isLocal
    );

    /// <summary>Asynchronous version of LoadSkin. Runs download/unpack on a background thread.</summary>
    Task<SkinLoadResult> LoadSkinAsync(
        string skinName,
        IReadOnlyList<string> filenames,
        bool isLocal
    );

    /// <summary>Asynchronous load with progress reporting.</summary>
    Task<SkinLoadResult> LoadSkinAsync(
        string skinName,
        IReadOnlyList<string> filenames,
        bool isLocal,
        ILoadProgress progress
    );

    void UpdateLocalSkin(string skinName);

    /// <summary>Uploads a prepared local zip (UpdateLocalSkin should already have been called).</summary>
    string UploadLocalZip(string skinName, string uploadUrl);

    /// <summary>Downloads a remote zip and returns archive name (without .zip extension).</summary>
    string DownloadRemoteZip(string url);

    /// <summary>Asynchronously downloads a remote zip and returns archive name.</summary>
    Task<string> DownloadRemoteZipAsync(string url);

    /// <summary>Asynchronously downloads a remote zip with progress reporting.</summary>
    Task<string> DownloadRemoteZipAsync(string url, ILoadProgress progress);

    void UnpackRemoteZip(string archiveName);

    void ClearCache();
}
