using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace ChangeSkin.Skin;

/// <summary>
/// JSON manifest that describes all sprites for a skin in a single file.
/// Each entry specifies the texture source and all Sprite.Create parameters.
/// </summary>
[Serializable]
internal class SpriteManifest
{
    /// <summary>
    /// Global defaults applied to every sprite entry unless overridden.
    /// Omitted when null (not serialized).
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public SpriteDefaults defaults;

    /// <summary>
    /// Ordered list of sprite definitions. Order determines load priority.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public List<SpriteEntry> sprites;

    /// <summary>
    /// Optional: list of filenames to load using legacy (image-only) mode.
    /// These are loaded with the default parameters as before.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public List<string> legacyFiles;

    /// <summary>
    /// Creates a minimal manifest from a list of relative image filenames.
    /// Only required fields (name, image) are populated; all optional fields are omitted.
    /// </summary>
    public static SpriteManifest FromFilenames(IReadOnlyList<string> filenames)
    {
        var sprites = new List<SpriteEntry>(filenames.Count);
        foreach (string filename in filenames)
        {
            sprites.Add(
                new SpriteEntry
                {
                    name = Path.GetFileNameWithoutExtension(filename),
                    image = filename,
                }
            );
        }
        return new SpriteManifest { sprites = sprites };
    }
}

[Serializable]
internal class SpriteDefaults
{
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public float pixelsPerUnit = 8f;

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public float pivotX = 0.5f;

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public float pivotY = 0.5f;

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public string filterMode = "Point";

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public string meshType = "Tight";

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public uint extrudeEdges = 0;

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool generatePhysicsShape = false;

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public string secondaryTexturePath;

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public string wrapMode = "Clamp";

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public string compressionQuality = "None";

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public int maxTextureSize = 2048;

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool mipmaps = false;

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public float borderX;

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public float borderY;

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public float borderZ;

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public float borderW;
}

[Serializable]
internal class SpriteEntry
{
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string name;

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string image;

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public string filterMode;

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public string wrapMode;

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public int maxTextureSize;

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool? mipmaps;

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public string compressionQuality;

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public float rectX;

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public float rectY;

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public float rectWidth;

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public float rectHeight;

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public float? pivotX;

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public float? pivotY;

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public float pixelsPerUnit;

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public uint? extrudeEdges;

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public string meshType;

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool? generatePhysicsShape;

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public float borderX;

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public float borderY;

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public float borderZ;

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public float borderW;

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public string secondaryTexturePath;
}
