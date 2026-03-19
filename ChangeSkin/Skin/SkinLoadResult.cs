using System.Collections.Generic;

namespace ChangeSkin.Skin;

/// <summary>
/// Represents the outcome of an asynchronous skin load operation.
/// </summary>
internal sealed class SkinLoadResult
{
    public bool Success { get; }
    public Dictionary<string, UnityEngine.Sprite> Sprites { get; }
    public string Error { get; }

    private SkinLoadResult(
        bool success,
        Dictionary<string, UnityEngine.Sprite> sprites,
        string error
    )
    {
        Success = success;
        Sprites = sprites;
        Error = error;
    }

    public static SkinLoadResult Ok(Dictionary<string, UnityEngine.Sprite> sprites) =>
        new(true, sprites, null);

    public static SkinLoadResult Fail(string error) =>
        new(false, null, error);
}
