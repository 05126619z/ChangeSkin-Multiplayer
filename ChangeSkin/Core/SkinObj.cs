using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using BepInEx;
using LiteNetLib.Utils;
using Newtonsoft.Json;
using UnityEngine;

namespace ChangeSkinMP;

public sealed class SkinObject : INetSerializable
{
    public SkinMeta Meta { get; private set; }
    public string Name => Meta.Name;
    public IReadOnlyDictionary<string, Sprite> BodySprites { get; private set; }

    private byte[] _zipBytes;

    public SkinObject() { }

    private SkinObject(SkinMeta skinMeta, Dictionary<string, Sprite> sprites)
    {
        Meta = skinMeta;
        BodySprites = sprites;
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.PutBytesWithLength(_zipBytes);
    }

    public void Deserialize(NetDataReader reader)
    {
        _zipBytes = reader.GetBytesWithLength();

        // Переиспользуем уже готовую логику
        SkinObject loaded = FromZipBytes(null, _zipBytes);
        Meta = loaded.Meta;
        BodySprites = loaded.BodySprites;

        // hash utilization not implemented yet
    }

    // ─── Публичные загрузчики ────────────────────────────────────────────────

    public static SkinObject LoadFromUri(string name, Uri uri)
    {
        byte[] data = DownloadURL(uri);
        return FromZipBytes(name, data);
    }

    public static SkinObject LoadFromUri(Uri uri)
    {
        byte[] data = DownloadURL(uri);
        return FromZipBytes(null, data);
    }

    public static SkinObject LoadFromLocal(string name)
    {
        byte[] data =
            ReadLocalZip(name) // уже готовый .zip
            ?? PackLocalFolder(name); // или папка с PNG

        if (data == null)
            throw new FileNotFoundException($"Skin not found: {name}");

        return FromZipBytes(name, data);
    }

    private static SkinObject FromZipBytes(string? name, byte[] data)
    {
        using var zip = new ZipArchive(new MemoryStream(data), ZipArchiveMode.Read);

        SkinMeta meta = SkinMeta.TryParseMeta(zip) ?? SkinMeta.CreateLegacy(name ?? "unknown"); // легаси — генерируем дефолт

        Dictionary<string, Sprite> sprites = ParseSprites(zip.Entries);

        return new SkinObject(meta, sprites);
    }

    private static Dictionary<string, Sprite> ParseSprites(IEnumerable<ZipArchiveEntry> entries)
    {
        var sprites = new Dictionary<string, Sprite>();

        foreach (var entry in entries)
        {
            if (entry.Name.Length == 0 || !entry.Name.EndsWith(".png"))
                continue;

            using var ms = new MemoryStream();
            using (Stream s = entry.Open())
                s.CopyTo(ms);

            Sprite sprite = Utils.LoadSprite(ms.ToArray());
            if (sprite != null)
                sprites[sprite.name] = sprite;
        }

        return sprites;
    }

    private static string SkinRoot(string name) =>
        Path.Combine(Paths.PluginPath, "ChangeSkin", "resources", name);

    // Вариант 1: уже лежит готовый .zip
    private static byte[]? ReadLocalZip(string name)
    {
        string path = SkinRoot(name) + ".zip";
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    // Вариант 2: папка с PNG (и опционально meta.json)
    private static byte[]? PackLocalFolder(string name)
    {
        string folder = SkinRoot(name);
        if (!Directory.Exists(folder))
            return null;

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            // meta.json если есть
            string metaPath = Path.Combine(folder, "meta.json");
            if (File.Exists(metaPath))
                AddFileToZip(zip, metaPath, "meta.json");

            foreach (string png in Directory.EnumerateFiles(folder, "*.png"))
                AddFileToZip(zip, png, Path.GetFileName(png));
        }

        return ms.ToArray();
    }

    private static void AddFileToZip(ZipArchive zip, string filePath, string entryName)
    {
        ZipArchiveEntry entry = zip.CreateEntry(entryName);
        using Stream es = entry.Open();
        using FileStream fs = File.OpenRead(filePath);
        fs.CopyTo(es);
    }

    //lol
    public static byte[] DownloadURL(Uri uri)
    {
        using (var client = new HttpClient())
        {
            return client.GetByteArrayAsync(uri).GetAwaiter().GetResult();
        }
    }
}

public sealed class SkinMeta
{
    [JsonProperty("name")]
    public string Name { get; init; }

    [JsonProperty("displayName")]
    public string DisplayName { get; init; }

    [JsonProperty("version")]
    public string Version { get; init; }

    [JsonProperty("author")]
    public string Author { get; init; }

    [JsonProperty("description")]
    public string Description { get; init; }

    [JsonProperty("hash")]
    public string Hash { get; init; }

    [JsonProperty("gameVersion")]
    public string GameVersion { get; init; }

    [JsonProperty("tags")]
    public IReadOnlyList<string> Tags { get; init; } = [];

    [JsonProperty("preview")]
    public string PreviewFile { get; init; } = "preview.png";

    [JsonProperty("legacy")]
    public bool IsLegacy { get; init; }

    public static SkinMeta CreateLegacy(string name) =>
        new()
        {
            Name = name,
            DisplayName = name,
            Version = "0.0.0",
            IsLegacy = true, // флаг что мета ненастоящая
        };

    public static SkinMeta? TryParseMeta(ZipArchive zip)
    {
        ZipArchiveEntry? entry = zip.GetEntry("meta.json");
        if (entry == null)
            return null; // нет файла — возвращаем null, не падаем

        using Stream s = entry.Open();
        using StreamReader reader = new(s);
        JsonTextReader jsonReader = new(reader);
        return new JsonSerializer().Deserialize<SkinMeta>(jsonReader);
    }
}
