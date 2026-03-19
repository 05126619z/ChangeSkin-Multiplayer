using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Random = System.Random;

namespace ChangeSkin.Utils;

/// <summary>
/// Sprite and texture loading helpers.
/// </summary>
internal static class SpriteHelpers
{
    public static Texture2D LoadTexture(string path)
    {
        Texture2D newTexture = new Texture2D(2, 2);
        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            newTexture.LoadImage(bytes);
            newTexture.filterMode = FilterMode.Point;
            newTexture.name = Path.GetFileNameWithoutExtension(path);
        }
        catch
        {
            Core.Plugin.Logger.LogWarning($"Failed to load texture at: {path}");
            throw;
        }
        return newTexture;
    }

    public static Sprite LoadSprite(string path)
    {
        Sprite outSprite = new();
        try
        {
            Texture2D tex = LoadTexture(path);
            outSprite = Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                8,
                0,
                SpriteMeshType.Tight
            );
            outSprite.name = Path.GetFileNameWithoutExtension(path);
        }
        catch
        {
            Core.Plugin.Logger.LogWarning($"Failed to load sprite at: {path}");
            throw;
        }
        return outSprite;
    }

    public static Texture2D LoadTexture(
        string path,
        string filterMode = null,
        string wrapMode = null,
        int maxTextureSize = 0,
        bool? mipmaps = null,
        string compressionQuality = null
    )
    {
        Texture2D tex = new Texture2D(2, 2);
        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            tex.LoadImage(bytes);
            tex.filterMode = ParseFilterMode(filterMode ?? "Point");
            tex.wrapMode = ParseWrapMode(wrapMode ?? "Clamp");
            tex.name = Path.GetFileNameWithoutExtension(path);
        }
        catch
        {
            Core.Plugin.Logger.LogWarning($"Failed to load texture at: {path}");
            throw;
        }
        return tex;
    }

    public static Sprite CreateSprite(
        Texture2D tex,
        string spriteName,
        float rectX = 0f,
        float rectY = 0f,
        float rectWidth = 0f,
        float rectHeight = 0f,
        float pivotX = 0.5f,
        float pivotY = 0.5f,
        float pixelsPerUnit = 8f,
        uint extrudeEdges = 0,
        SpriteMeshType meshType = SpriteMeshType.Tight,
        Vector4 border = default,
        bool generatePhysicsShape = false
    )
    {
        float w = rectWidth > 0 ? rectWidth : tex.width;
        float h = rectHeight > 0 ? rectHeight : tex.height;

        Sprite sprite = Sprite.Create(
            tex,
            new Rect(rectX, rectY, w, h),
            new Vector2(pivotX, pivotY),
            pixelsPerUnit,
            extrudeEdges,
            meshType,
            border,
            generatePhysicsShape
        );
        sprite.name = spriteName;
        return sprite;
    }

    public static Sprite LoadSpriteFromEntry(
        Skin.SpriteEntry entry,
        Skin.SpriteDefaults defaults,
        string skinDir
    )
    {
        if (entry == null)
            throw new ArgumentNullException(nameof(entry));

        string imagePath = Path.Combine(
            skinDir,
            entry.image.Replace('/', Path.DirectorySeparatorChar)
        );
        string spriteName = !string.IsNullOrEmpty(entry.name)
            ? entry.name
            : Path.GetFileNameWithoutExtension(entry.image);

        string filterMode = entry.filterMode ?? defaults?.filterMode ?? "Point";
        string wrapMode = entry.wrapMode ?? defaults?.wrapMode ?? "Clamp";
        bool useMipmaps = entry.mipmaps ?? defaults?.mipmaps ?? false;

        Texture2D tex = LoadTexture(imagePath, filterMode, wrapMode, mipmaps: useMipmaps);

        float ppu =
            entry.pixelsPerUnit > 0 ? entry.pixelsPerUnit : (defaults?.pixelsPerUnit ?? 8f);
        float px = entry.pivotX ?? defaults?.pivotX ?? 0.5f;
        float py = entry.pivotY ?? defaults?.pivotY ?? 0.5f;
        uint extrude = entry.extrudeEdges ?? defaults?.extrudeEdges ?? 0;
        bool physics = entry.generatePhysicsShape ?? defaults?.generatePhysicsShape ?? false;
        SpriteMeshType mesh = ParseMeshType(entry.meshType ?? defaults?.meshType ?? "Tight");

        float bx = entry.borderX != 0 ? entry.borderX : (defaults?.borderX ?? 0f);
        float by = entry.borderY != 0 ? entry.borderY : (defaults?.borderY ?? 0f);
        float bz = entry.borderZ != 0 ? entry.borderZ : (defaults?.borderZ ?? 0f);
        float bw = entry.borderW != 0 ? entry.borderW : (defaults?.borderW ?? 0f);
        Vector4 border = new Vector4(bx, by, bz, bw);

        Sprite sprite = CreateSprite(
            tex,
            spriteName,
            entry.rectX,
            entry.rectY,
            entry.rectWidth,
            entry.rectHeight,
            px,
            py,
            ppu,
            extrude,
            mesh,
            border,
            physics
        );

        return sprite;
    }

    public static FilterMode ParseFilterMode(string value)
    {
        if (string.IsNullOrEmpty(value))
            return FilterMode.Point;
        switch (value.Trim().ToLowerInvariant())
        {
            case "bilinear":
                return FilterMode.Bilinear;
            case "trilinear":
                return FilterMode.Trilinear;
            case "point":
            default:
                return FilterMode.Point;
        }
    }

    public static TextureWrapMode ParseWrapMode(string value)
    {
        if (string.IsNullOrEmpty(value))
            return TextureWrapMode.Clamp;
        switch (value.Trim().ToLowerInvariant())
        {
            case "repeat":
                return TextureWrapMode.Repeat;
            case "mirror":
                return TextureWrapMode.Mirror;
            case "mirroronce":
                return TextureWrapMode.MirrorOnce;
            case "clamp":
            default:
                return TextureWrapMode.Clamp;
        }
    }

    public static SpriteMeshType ParseMeshType(string value)
    {
        if (string.IsNullOrEmpty(value))
            return SpriteMeshType.Tight;
        switch (value.Trim().ToLowerInvariant())
        {
            case "fullrect":
                return SpriteMeshType.FullRect;
            case "tight":
            default:
                return SpriteMeshType.Tight;
        }
    }

    public static string GenerateRandomString(int length = 10)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var random = new Random();
        return new string(
            Enumerable.Repeat(chars, length).Select(s => s[random.Next(s.Length)]).ToArray()
        );
    }
}
