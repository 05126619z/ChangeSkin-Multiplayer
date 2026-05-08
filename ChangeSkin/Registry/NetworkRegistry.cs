using System;
using System.Collections.Generic;
using KrokoshaCasualtiesMP;

namespace ChangeSkinMP;

public static class NetworkRegistry
{
    static Dictionary<NetBody, ChangeBody> netBodies = new();
    static Dictionary<uint, ChangeBody> byId = new();
    static List<PlayerInfo> _players = new List<PlayerInfo>();

    public static IReadOnlyList<PlayerInfo> Players => _players.AsReadOnly();

    public static void RegisterConnected(NetBody netBody)
    {
        ChangeBody changeBody = netBody.body.gameObject.AddComponent<ChangeBody>();
        changeBody.Init(netBody);
        netBodies.Add(netBody, changeBody);
        byId.Add(netBody.netId, changeBody);

        // Локальный игрок получает контроллер
        if (netBody.netId == NetPlayer.LOCAL_PLAYER.clientId)
            netBody.body.gameObject.AddComponent<LocalSkinController>();
    }

    // Применить скин по id — вызывается из сетевого обработчика
    public static void ApplySkin(uint clientId, SkinObject skin)
    {
        if (BanList.Contains(clientId))
            return;

        ChangeBody? body = GetById(clientId);
        body?.ApplySkin(skin);
    }

    public static ChangeBody? GetById(uint id) => byId.TryGetValue(id, out var body) ? body : null;

    public static void RegisterDisconnected(NetBody netBody)
    {
        netBodies.Remove(netBody);
    }

    public static void Clear()
    {
        foreach (var body in netBodies.Values)
            UnityEngine.Object.Destroy(body);

        netBodies.Clear();
        byId.Clear();
    }
}
