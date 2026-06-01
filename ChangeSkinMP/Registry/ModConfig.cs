using System.Collections.Generic;
using System.IO;
using BepInEx;
using Newtonsoft.Json;

public sealed class ModConfig
{
    private static readonly string _savePath = Path.Combine(
        Paths.PluginPath,
        "ChangeSkinMP",
        "settings.json"
    );

    public static ModConfig Instance { get; private set; } = new();

    // ─── Поля ────────────────────────────────────────────────────────────────

    [JsonProperty("lastSkinIsRemote")]
    public bool LastSkinIsRemote { get; set; } = false;

    [JsonProperty("lastSelectedSkin")]
    public string LastSelectedSkin { get; set; }

    [JsonProperty("lastUrl")]
    public string LastURL { get; set; }

    [JsonProperty("verbose")]
    public bool Verbose { get; set; } = false;

    [JsonProperty("skinUploading")]
    public bool SkinUploading { get; set; } = true;

    [JsonProperty("skinDownloading")]
    public bool SkinDownloading { get; set; } = true;

    [JsonProperty("skinChangingEnabled")]
    public bool SkinChangingEnabled { get; set; } = false;

    [JsonProperty("recentSkins")]
    public List<string> RecentSkins { get; set; } = new();

    // ─── Персистентность ─────────────────────────────────────────────────────

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_savePath));
        File.WriteAllText(_savePath, JsonConvert.SerializeObject(this, Formatting.Indented));
    }

    public static void Load()
    {
        if (!File.Exists(_savePath))
        {
            Instance = new ModConfig();
            Instance.Save();
            return;
        }

        try
        {
            Instance =
                JsonConvert.DeserializeObject<ModConfig>(File.ReadAllText(_savePath))
                ?? new ModConfig();
        }
        catch (Exception e)
        {
            Log.Warn($"Failed to load config: {e.Message}, using defaults");
            Instance = new ModConfig();
            Instance.Save();
        }
    }

    // ─── Хелперы ─────────────────────────────────────────────────────────────

    public void AddRecentSkin(string name)
    {
        RecentSkins.Remove(name); // убираем дубликат если был
        RecentSkins.Insert(0, name); // добавляем в начало
        if (RecentSkins.Count > 10) // максимум 10 последних
            RecentSkins.RemoveAt(RecentSkins.Count - 1);
        Save();
    }
}
