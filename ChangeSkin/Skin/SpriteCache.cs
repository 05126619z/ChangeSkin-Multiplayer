using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChangeSkin.Skin;

/// <summary>
/// In-memory cache for loaded sprite dictionaries.
/// Keyed by skin identity (name + local/remote flag).
/// Prevents redundant disk I/O and sprite creation when the same skin
/// is requested by multiple players or reloaded.
/// </summary>
internal static class SpriteCache
{
    private sealed class CacheKey : IEquatable<CacheKey>
    {
        public readonly string SkinName;
        public readonly bool IsLocal;

        public CacheKey(string skinName, bool isLocal)
        {
            SkinName = skinName ?? string.Empty;
            IsLocal = isLocal;
        }

        public bool Equals(CacheKey other)
        {
            if (other is null) return false;
            return IsLocal == other.IsLocal
                && string.Equals(SkinName, other.SkinName, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj) => Equals(obj as CacheKey);

        public override int GetHashCode()
        {
            unchecked
            {
                return (StringComparer.OrdinalIgnoreCase.GetHashCode(SkinName) * 397)
                    ^ IsLocal.GetHashCode();
            }
        }
    }

    private static readonly Dictionary<CacheKey, Dictionary<string, Sprite>> _cache = new();
    private static readonly object _lock = new();

    /// <summary>
    /// Try to get cached sprites for the given skin.
    /// Returns true if a valid cache entry exists (even if empty).
    /// </summary>
    public static bool TryGet(string skinName, bool isLocal, out Dictionary<string, Sprite> sprites)
    {
        lock (_lock)
        {
            return _cache.TryGetValue(new CacheKey(skinName, isLocal), out sprites);
        }
    }

    /// <summary>
    /// Store sprites in cache. Overwrites any existing entry for the same key.
    /// </summary>
    public static void Set(string skinName, bool isLocal, Dictionary<string, Sprite> sprites)
    {
        if (string.IsNullOrEmpty(skinName))
            return;

        lock (_lock)
        {
            _cache[new CacheKey(skinName, isLocal)] = sprites;
        }
    }

    /// <summary>
    /// Remove a specific cache entry.
    /// </summary>
    public static void Remove(string skinName, bool isLocal)
    {
        lock (_lock)
        {
            _cache.Remove(new CacheKey(skinName, isLocal));
        }
    }

    /// <summary>
    /// Clear the entire cache, freeing sprite references for GC.
    /// </summary>
    public static void Clear()
    {
        lock (_lock)
        {
            _cache.Clear();
        }
    }

    /// <summary>
    /// Number of cached skins.
    /// </summary>
    public static int Count
    {
        get
        {
            lock (_lock)
            {
                return _cache.Count;
            }
        }
    }
}
