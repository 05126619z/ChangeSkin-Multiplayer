using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using BepInEx;
using OneOf.Types;
using UnityEngine;
using UnityEngine.Networking;

namespace ChangeSkinMP;

internal static class SkinLoader
{
    // public static IEnumerator LoadSkinAsync(
    //     string skinName,
    //     string[] filenames,
    //     bool isLocal,
    //     Action<Dictionary<string, Sprite>> callback
    // )
    // {
    //     Dictionary<string, Sprite> dict = new();
    //     string workPath;
    //     if (isLocal)
    //     {
    //         UpdateFromLocal(skinName);
    //         workPath = Path.GetTempPath() + $"/ChangeSkin/local/{skinName}";
    //     }
    //     else
    //     {
    //         workPath = Path.GetTempPath() + $"/ChangeSkin/remote/{skinName}";
    //         string skinDir = Directory.GetDirectories(workPath)[0];
    //         workPath = workPath + "/" + skinDir;
    //     }
    //     foreach (string filename in filenames)
    //     {
    //         Sprite sprite = Utils.LoadSprite(workPath + "/" + filename);
    //         sprite.name = Path.GetFileNameWithoutExtension(filename);
    //         dict[Path.GetFileNameWithoutExtension(filename)] = sprite;
    //         yield return null;
    //     }
    //     callback?.Invoke(dict);
    // }

    public static void LoadSkin(
        string skinName,
        string[] filenames,
        bool isLocal,
        ref Dictionary<string, Sprite> dict
    )
    {
        string workPath;
        if (isLocal)
        {
            UpdateLocalSkin(skinName);
            workPath = Path.Combine(Path.GetTempPath(), "ChangeSkin", "local", skinName);
        }
        else
        {
            workPath = Path.Combine(Path.GetTempPath(), "ChangeSkin", "remote", skinName);
            string[] dirNames = Directory.GetDirectories(workPath);
            if (Plugin.ModConfig.Verbose)
            {
                foreach (string dirName in dirNames)
                {
                    Plugin.Logger.LogInfo(dirName);
                }
            }
            workPath = dirNames[0];
        }
        foreach (string filename in filenames)
        {
            Sprite sprite;
            try
            {
                sprite = Utils.LoadSprite(workPath + "/" + filename);
                dict[Path.GetFileNameWithoutExtension(filename)] = sprite;
            }
            catch
            {
                string msg =
                    "It seems that some files for the skin are either missing or located in wrong folder structure. Follow the robot template for correct skin loading";
                ConsoleScript.instance.LogToConsole(msg);
                Plugin.Logger.LogWarning(msg);
            }
        }
    }

    internal static void UpdateLocalSkin(string skinName)
    {
        UpdateFromLocal(skinName);
        PackLocal(skinName);
    }

    private static void UpdateFromLocal(string skinName)
    {
        string workPath = Path.GetTempPath() + "/ChangeSkin/local";
        if (!Directory.Exists(workPath))
            Directory.CreateDirectory(workPath);
        if (Directory.Exists($"{workPath}/{skinName}"))
            Directory.Delete($"{workPath}/{skinName}", true);
        CopyFolderRecuresively(
            Paths.PluginPath + $"/ChangeSkin/resources/{skinName}",
            $"{workPath}/{skinName}"
        );
    }

    private static void PackLocal(string skinName)
    {
        string workPath = Path.GetTempPath() + "/ChangeSkin/zips/local";
        if (!Directory.Exists(workPath))
            Directory.CreateDirectory(workPath);
        if (File.Exists($"{workPath}/{skinName}.zip"))
            File.Delete($"{workPath}/{skinName}.zip");
        ZipFile.CreateFromDirectory(
            Path.GetTempPath() + $"/ChangeSkin/local/{skinName}",
            $"{workPath}/{skinName}.zip",
            System.IO.Compression.CompressionLevel.Fastest,
            true
        );
    }

    public static string DownloadRemote(string url)
    {
        string workPath = Path.GetTempPath() + "/ChangeSkin/zips/remote";
        if (!Directory.Exists(workPath))
            Directory.CreateDirectory(workPath);
        string fileName = Path.GetFileName(url);
        if (string.IsNullOrEmpty(fileName))
        {
            throw (new Exception("ChangeSkin invalid url, no filename"));
        }

        string filePath = Path.Combine(workPath, fileName);

        // Download using WebClient (simplest approach)
        // simplest approach my ass deepseek
        using (var client = new HttpClient())
        {
            using (var s = client.GetStreamAsync(url).GetAwaiter().GetResult())
            {
                using (
                    var fs = new FileStream(
                        workPath + "/" + fileName + ".zip",
                        FileMode.OpenOrCreate
                    )
                )
                {
                    s.CopyTo(fs);
                }
            }
        }

        return Path.GetFileNameWithoutExtension(fileName);
    }

    public static byte[] DownloadURLBytes(Uri uri)
    {
        using (var client = new HttpClient())
        {
            return client.GetByteArrayAsync(uri).GetAwaiter().GetResult();
        }
    }

    // Для URI — скачали zip, распаковали
    public static Dictionary<string, Sprite> ParseSpriteSheet(byte[] zipData)
    {
        using var zip = new ZipArchive(new MemoryStream(zipData), ZipArchiveMode.Read);
        return ParseEntries(zip.Entries);
    }

    // Для локальных файлов — читаем папку напрямую, без промежуточного zip
    public static byte[] LoadLocalBytes(string name)
    {
        string folder = Path.Combine(Paths.PluginPath, "ChangeSkin", "resources", name);

        if (!Directory.Exists(folder))
            throw new DirectoryNotFoundException($"Skin folder not found: {folder}");

        // Пакуем в zip в памяти — формат единый для всех путей
        using var ms = new MemoryStream();

        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (string file in Directory.EnumerateFiles(folder, "*.png"))
            {
                ZipArchiveEntry entry = zip.CreateEntry(Path.GetFileName(file));
                using Stream entryStream = entry.Open();
                using FileStream fs = File.OpenRead(file);
                fs.CopyTo(entryStream);
            }
        } // zip.Dispose() здесь — финализирует запись в ms

        return ms.ToArray();
    }

    // Общая внутренняя логика разбора
    private static Dictionary<string, Sprite> ParseEntries(IEnumerable<ZipArchiveEntry> entries)
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

    public static void UnpackRemote(string archiveName)
    {
        string targetPath = Path.Combine(Path.GetTempPath(), "ChangeSkin", "remote", archiveName);
        string zipPath = Path.Combine(
            Path.GetTempPath(),
            "ChangeSkin",
            "zips",
            "remote",
            $"{archiveName}.zip"
        );

        if (Directory.Exists(targetPath))
            Directory.Delete(targetPath, true);

        Directory.CreateDirectory(targetPath);

        ZipFile.ExtractToDirectory(zipPath, targetPath);
    }

    private static void CopyFolderRecuresively(string sourceFolder, string destFolder)
    {
        if (!Directory.Exists(destFolder))
            Directory.CreateDirectory(destFolder);
        string[] files = Directory.GetFiles(sourceFolder);
        foreach (string file in files)
        {
            string name = Path.GetFileName(file);
            string dest = Path.Combine(destFolder, name);
            File.Copy(file, dest);
        }
        string[] folders = Directory.GetDirectories(sourceFolder);
        foreach (string folder in folders)
        {
            string name = Path.GetFileName(folder);
            string dest = Path.Combine(destFolder, name);
            CopyFolderRecuresively(folder, dest);
        }
    }

    private static void UnpackZip(string skinName)
    {
        if (!File.Exists(Path.GetTempPath() + $"/ChangeSkin/{skinName}.zip"))
            return;
        if (Directory.Exists(Path.GetTempPath() + $"/ChangeSkin/{skinName}"))
            Directory.Delete(Path.GetTempPath() + $"/ChangeSkin/{skinName}", true);
        ZipFile.ExtractToDirectory(
            Path.GetTempPath() + $"/ChangeSkin/{skinName}.zip",
            Path.GetTempPath() + $"/ChangeSkin/{skinName}"
        );
    }

    // private static void CreateZip(string path, string skinName)
    // {
    //     string outPath;
    //     outPath = Path.GetTempPath() + $"/ChangeSkin/{skinName}.zip";
    //     if (!Directory.Exists(Path.GetTempPath() + "/ChangeSkin"))
    //         Directory.CreateDirectory(Path.GetTempPath() + "/ChangeSkin");
    //     if (File.Exists(outPath))
    //         File.Delete(outPath);
    //     ZipFile.CreateFromDirectory(path, outPath);
    // }

    public static void ClearCache()
    {
        if (Directory.Exists(Path.GetTempPath() + "/ChangeSkin"))
            Directory.Delete(Path.GetTempPath() + "/ChangeSkin", true);
    }
}
