using System.Collections.Generic;
using KrokoshaCasualtiesMP;
using LiteNetLib;
using LiteNetLib.Utils;

namespace ChangeSkinMP;

public static class MessageSender
{
    public static void SendToAll(NetDataWriter writer)
    {
        if (!Net.is_server) return;
        IReadOnlyList<uint> targets = ServerMain.AllClientIdsExceptHost;
        if (targets.Count == 0) return;
        Net.Server_SendToClients(DeliveryMethod.ReliableOrdered, in writer, targets);
    }

    public static void SendToOthers(NetDataWriter writer, uint excludeClientId)
    {
        if (!Net.is_server) return;
        List<uint> targets = ServerMain.GetListOfClientIdsExceptThisAndHost(excludeClientId);
        if (targets.Count == 0) return;
        IReadOnlyList<uint> roTargets = targets;
        Net.Server_SendToClients(DeliveryMethod.ReliableOrdered, in writer, in roTargets);
    }

    public static void SendToOne(NetDataWriter writer, uint clientId)
    {
        if (!Net.is_server) return;
        if (clientId == NetPlayer.LOCAL_PLAYER?.clientId) return;
        Net.Server_SendTo(DeliveryMethod.ReliableOrdered, in writer, clientId);
    }

    public static void SendToServer(NetDataWriter writer)
    {
        Net.TRANSPORT.Client_Send(DeliveryMethod.ReliableOrdered, writer);
    }
}
