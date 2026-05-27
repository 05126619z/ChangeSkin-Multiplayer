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
    private static readonly HashSet<PlayerInfo> _banned = new();
    private static readonly string _savePath = Path.Combine(
        Paths.PluginPath,
        "ChangeSkinMP",
        "skinbans.json"
    );

    public static bool Contains(PlayerInfo player) => _banned.Contains(player);

    static BanList() => Load();

    public static void Ban(PlayerInfo player)
    {
        _banned.Add(player);
        Save();
        NetworkRegistryEntry networkRegistryEntry = NetworkRegistry.Get(player);
        networkRegistryEntry.SkinController.OnBanReceived(true);
        NetDataWriter data = Net.CreateWriter((ushort)Messages.SkinBanMessage);
        data.Put(player);
        data.Put(true);
        MessageSender.SendToAll(data);
    }

    public static void Unban(PlayerInfo player)
    {
        _banned.Remove(player);
        Save();
        NetworkRegistryEntry networkRegistryEntry = NetworkRegistry.Get(player);
        networkRegistryEntry.SkinController.OnBanReceived(false);
        NetDataWriter data = Net.CreateWriter((ushort)Messages.SkinBanMessage);
        data.Put(player);
        data.Put(false);
        MessageSender.SendToAll(data);
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
