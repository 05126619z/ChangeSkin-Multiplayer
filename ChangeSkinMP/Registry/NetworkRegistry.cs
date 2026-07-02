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
    public static LocalSkinController LocalPlayerSkinController { get; internal set; }

    public static void RegisterConnected(NetBody netBody)
    {
        if (Get(netBody) != null)
            return;

        GameObject go = netBody.body.gameObject;

        // Idempotent: the SP fallback (TrySetupLocalPlayer) may have already
        // attached a ChangeBody + LocalSkinController to this same rig. Adding
        // duplicates would desync LocalSkinController.CBody (Awake grabs the
        // first ChangeBody) from the NetBody-backed one. Reuse existing
        // components; bind the NetBody onto an SP-created ChangeBody.
        ChangeBody changeBody = go.GetComponent<ChangeBody>();
        if (changeBody == null)
        {
            changeBody = go.AddComponent<ChangeBody>();
            changeBody.Init(netBody);
        }
        else if (changeBody.NBody == null)
        {
            changeBody.BindNetBody(netBody);
        }

        RemoteSkinController skinController = go.GetComponent<RemoteSkinController>();
        if (skinController == null)
            skinController = go.AddComponent<RemoteSkinController>();

        PlayerInfo playerInfo = new(netBody);
        NetworkRegistryEntry networkRegistryEntry = new(
            netBody,
            netBody.plr.clientId,
            playerInfo,
            skinController
        );
        _players.Add(networkRegistryEntry);

        if (netBody.netId == NetPlayer.LOCAL_PLAYER.clientId)
        {
            LocalSkinController localController = go.GetComponent<LocalSkinController>();
            if (localController == null)
                localController = go.AddComponent<LocalSkinController>();
            LocalPlayerSkinController = localController;
        }
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
            // SP fallback attaches a ChangeBody without a registry entry, so the
            // loop above never destroys it. Tear it down here too, otherwise it
            // lingers on the rig and RegisterConnected would create a duplicate
            // when MP re-registers the same GameObject.
            if (LocalPlayerSkinController.CBody != null)
            {
                LocalPlayerSkinController.CBody.ResetSkin();
                UnityEngine.Object.Destroy(LocalPlayerSkinController.CBody);
            }
            UnityEngine.Object.Destroy(LocalPlayerSkinController);
            LocalPlayerSkinController = null;
        }
        _players.Clear();
    }
}
