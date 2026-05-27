using System;
using System.Collections.Generic;
using System.Linq;
using KrokoshaCasualtiesMP;
using UnityEngine;

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

        if (netBody.netId == NetPlayer.LOCAL_PLAYER.clientId)
            LocalPlayerSkinController = netBody.body.gameObject.AddComponent<LocalSkinController>();
    }

    public static void RegisterDisconnected(NetBody netBody)
    {
        NetworkRegistryEntry networkRegistryEntry = Get(netBody);
        if (networkRegistryEntry == null) return;
        networkRegistryEntry.SkinController.Disable();
        if (networkRegistryEntry.SkinController.CBody != null)
            networkRegistryEntry.SkinController.CBody.ResetSkin();
        UnityEngine.Object.Destroy(networkRegistryEntry.SkinController.CBody);
        UnityEngine.Object.Destroy(networkRegistryEntry.SkinController);
        if (netBody.netId == NetPlayer.LOCAL_PLAYER.clientId && LocalPlayerSkinController != null)
        {
            UnityEngine.Object.Destroy(LocalPlayerSkinController);
            LocalPlayerSkinController = null;
        }
        _players.Remove(networkRegistryEntry);
    }

    public static void RemovePlayer(uint clientId)
    {
        NetworkRegistryEntry entry = Get(clientId);
        if (entry == null) return;
        entry.SkinController.Disable();
        if (entry.CBody != null)
            entry.CBody.ResetSkin();
        UnityEngine.Object.Destroy(entry.CBody);
        UnityEngine.Object.Destroy(entry.SkinController);
        if (clientId == NetPlayer.LOCAL_PLAYER.clientId && LocalPlayerSkinController != null)
        {
            UnityEngine.Object.Destroy(LocalPlayerSkinController);
            LocalPlayerSkinController = null;
        }
        _players.Remove(entry);
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
        foreach (NetworkRegistryEntry entry in _players)
        {
            if (entry.CBody != null)
                entry.CBody.ResetSkin();
            UnityEngine.Object.Destroy(entry.CBody);
            UnityEngine.Object.Destroy(entry.SkinController);
        }
        if (LocalPlayerSkinController != null)
        {
            UnityEngine.Object.Destroy(LocalPlayerSkinController);
            LocalPlayerSkinController = null;
        }
        _players.Clear();
    }
}
