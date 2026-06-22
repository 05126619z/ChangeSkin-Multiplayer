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
        IReadOnlyList<knetid> targets = ServerMain.AllClientIdsExceptHost;
        if (targets.Count == 0) return;
        Net.Server_SendToClients(DeliveryMethod.ReliableOrdered, in writer, targets);
    }

    public static void SendToOthers(NetDataWriter writer, uint excludeClientId)
    {
        if (!Net.is_server) return;
        List<knetid> targets = ServerMain.GetListOfClientIdsExceptThisAndHost((knetid)excludeClientId);
        if (targets.Count == 0) return;
        IEnumerable<knetid> targetsEnumerable = targets;
        Net.Server_SendToClients(DeliveryMethod.ReliableOrdered, in writer, in targetsEnumerable);
    }

    public static void SendToOne(NetDataWriter writer, uint clientId)
    {
        if (!Net.is_server) return;
        if (clientId == NetPlayer.LOCAL_PLAYER?.clientId) return;
        Net.Server_SendTo(DeliveryMethod.ReliableOrdered, in writer, (knetid)clientId);
    }

    public static void SendToServer(NetDataWriter writer)
    {
        Net.TRANSPORT.Client_Send(DeliveryMethod.ReliableOrdered, writer);
    }
}
