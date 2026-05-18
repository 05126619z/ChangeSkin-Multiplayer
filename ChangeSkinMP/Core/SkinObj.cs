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

    private SkinObject(SkinMeta skinMeta, Dictionary<string, Sprite> sprites, byte[] zipBytes)
    {
        Meta = skinMeta;
        BodySprites = sprites;
        _zipBytes = zipBytes;
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

    public static SkinObject? LoadFromLocal(string name)
    {
        string folder = SkinRoot(name);

        byte[]? data = ReadLocalZip(name) ?? PackLocalFolder(name);

        if (data == null)
            throw new FileNotFoundException($"Skin not found: {name}");

        return FromZipBytes(name, data, saveMetaBack: true, folderPath: folder);
    }

    private static SkinObject FromZipBytes(
        string? name,
        byte[] data,
        bool saveMetaBack = false,
        string? folderPath = null
    )
    {
        using var zip = new ZipArchive(new MemoryStream(data), ZipArchiveMode.Read);

        bool metaWasGenerated = false;
        SkinMeta meta = SkinMeta.TryParseMeta(zip);
        if (meta == null)
        {
            meta = SkinMeta.CreateLegacy(name ?? "unknown");
            metaWasGenerated = true;
        }

        Dictionary<string, Sprite> sprites = ParseSprites(zip.Entries);

        // Сохраняем meta.json на диск если его не было
        if (saveMetaBack && metaWasGenerated && folderPath != null)
        {
            string metaPath = Path.Combine(folderPath, "meta.json");
            File.WriteAllText(metaPath, JsonConvert.SerializeObject(meta, Formatting.Indented));
            Plugin.Logger.LogInfo($"Saved meta.json to {metaPath}");
        }

        return new SkinObject(meta, sprites, data);
    }

    private static Dictionary<string, Sprite> ParseSprites(IEnumerable<ZipArchiveEntry> entries)
    {
        var sprites = new Dictionary<string, Sprite>();

        foreach (var entry in entries)
        {
            Plugin.Logger.LogInfo($"Entry: {entry.Name}");
            if (entry.Name.Length == 0 || !entry.Name.EndsWith(".png"))
            {
                Plugin.Logger.LogInfo($"Skipped: {entry.Name}");
                continue;
            }

            using var ms = new MemoryStream();
            using (Stream s = entry.Open())
                s.CopyTo(ms);

            string spriteName = Path.GetFileNameWithoutExtension(entry.Name);
            Sprite sprite = Utils.LoadSprite(ms.ToArray());
            if (sprite != null)
                sprites[spriteName] = sprite;
        }
        Plugin.Logger.LogInfo($"Parsed {sprites.Count} sprites");
        return sprites;
    }

    private static string SkinRoot(string name) =>
        Path.Combine(Paths.PluginPath, "ChangeSkinMP", "resources", name);

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
        Plugin.Logger.LogInfo($"Skin folder: {folder}");
        Plugin.Logger.LogInfo($"Exists: {Directory.Exists(folder)}");

        if (!Directory.Exists(folder))
            return null;

        var pngs = Directory.EnumerateFiles(folder, "*.png", SearchOption.AllDirectories).ToList();
        Plugin.Logger.LogInfo($"PNGs found: {pngs.Count}");
        foreach (var png in pngs)
            Plugin.Logger.LogInfo($"  {png}");

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            string metaPath = Path.Combine(folder, "meta.json");
            if (File.Exists(metaPath))
                AddFileToZip(zip, metaPath, "meta.json");

            foreach (string png in pngs)
                AddFileToZip(zip, png, Path.GetFileName(png));
        }

        byte[] result = ms.ToArray();
        Plugin.Logger.LogInfo($"Zip size: {result.Length} bytes");
        return result;
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
