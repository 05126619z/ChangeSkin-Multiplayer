using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using KrokoshaCasualtiesMP;
using LiteNetLib.Utils;
using Newtonsoft.Json;

namespace ChangeSkinMP;

public static class BanList
{
    // nickname → храним по нику, не по clientId (clientId меняется между сессиями)
    private static readonly HashSet<PlayerInfo> _banned = new();
    private static readonly string _savePath = Path.Combine(
        Paths.PluginPath,
        "ChangeSkinMP",
        "skinbans.json"
    );

    public static bool Contains(PlayerInfo player) => _banned.Contains(player);

    // Вызывается только на сервере
    public static void Ban(PlayerInfo player)
    {
        _banned.Add(player);
        Save();
        NetworkRegistryEntry networkRegistryEntry = NetworkRegistry.Get(player);
        networkRegistryEntry.SkinController.OnBanReceived(true);
        NetDataWriter data = new();
        data.Put(player);
        data.Put(true); // is banned?
        MessageSender.SendToAll(Messages.SkinBanMessage, data);
    }

    public static void Unban(PlayerInfo player)
    {
        _banned.Add(player);
        Save();
        NetworkRegistryEntry networkRegistryEntry = NetworkRegistry.Get(player);
        networkRegistryEntry.SkinController.OnBanReceived(false);
        NetDataWriter data = new();
        data.Put(player);
        data.Put(false); // is banned?
        MessageSender.SendToAll(Messages.SkinBanMessage, data);
    }

    private static void Save() =>
        File.WriteAllText(_savePath, Newtonsoft.Json.JsonConvert.SerializeObject(_banned));

    private static void Load()
    {
        if (!File.Exists(_savePath))
            return;
        var saved = Newtonsoft.Json.JsonConvert.DeserializeObject<HashSet<PlayerInfo>>(
            File.ReadAllText(_savePath)
        );
        if (saved != null)
            _banned.UnionWith(saved);
    }
}
