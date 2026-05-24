using System;
using System.Collections.Generic;
using System.Linq;
using KrokoshaCasualtiesMP;

namespace ChangeSkinMP;

public static class NetworkRegistry
{
    static HashSet<NetworkRegistryEntry> _players = new();
    public static IReadOnlyCollection<NetworkRegistryEntry> Players => _players;
    public static LocalSkinController LocalPlayerSkinController { get; private set; }

    public static void RegisterConnected(NetBody netBody)
    {
        if (Get(netBody) != null)
            return;
        ChangeBody changeBody = netBody.body.gameObject.AddComponent<ChangeBody>();
        changeBody.Init(netBody);
        RemoteSkinController skinController =
            netBody.body.gameObject.AddComponent<RemoteSkinController>();
        PlayerInfo playerInfo = new(netBody);
        NetworkRegistryEntry networkRegistryEntry = new(
            netBody,
            netBody.plr.clientId,
            playerInfo,
            skinController
        );
        _players.Add(networkRegistryEntry);

        // Локальный игрок получает контроллер
        if (netBody.netId == NetPlayer.LOCAL_PLAYER.clientId)
            LocalPlayerSkinController = netBody.body.gameObject.AddComponent<LocalSkinController>();
    }

    public static void RegisterDisconnected(NetBody netBody)
    {
        NetworkRegistryEntry networkRegistryEntry = Get(netBody);
        networkRegistryEntry?.SkinController.Disable();
        UnityEngine.Object.Destroy(networkRegistryEntry?.SkinController.CBody);
        UnityEngine.Object.Destroy(networkRegistryEntry?.SkinController);
    }

    public static NetworkRegistryEntry? Get(uint clientId) =>
        _players.FirstOrDefault(c => c.ClientID == clientId);

    public static NetworkRegistryEntry? Get(PlayerInfo playerInfo) =>
        _players.FirstOrDefault(c => c.PlayerInfo == playerInfo);

    public static NetworkRegistryEntry? Get(string nickname) =>
        _players.FirstOrDefault(e => e.PlayerInfo.Nickname == nickname);

    public static NetworkRegistryEntry? Get(NetBody netBody) =>
        _players.FirstOrDefault(e => e.NBody == netBody);

    public static void Clear()
    {
        LocalPlayerSkinController?.CBody.RepEnd();
        LocalPlayerSkinController = null;
        foreach (NetworkRegistryEntry entry in _players)
        {
            UnityEngine.Object.Destroy(entry.CBody);
            UnityEngine.Object.Destroy(entry.SkinController);
        }
        _players.Clear();
    }
}
