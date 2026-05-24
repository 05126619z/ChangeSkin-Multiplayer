using System.Collections.Generic;
using KrokoshaCasualtiesMP;
using LiteNetLib;
using LiteNetLib.Utils;

namespace ChangeSkinMP;

public class SimpleMessage
{
    public SimpleMessage(IReadOnlyList<uint> _ClientIDs, uint _localClientID)
    {
        ClientIDs = _ClientIDs;
        LocalClientID = _localClientID;
    }

    IReadOnlyList<uint> ClientIDs;
    uint LocalClientID;

    public void Srv_SendMsgAll(uint msgID, NetDataWriter netDataWriter)
    {
        NetDataWriter n2 = new();
        n2.Put(msgID);
        n2.PutBytesWithLength(netDataWriter.Data);
        if (!Net.is_server)
        {
            Log.Err("Srv_SendMsgAll: Cant send msg to all, not server.");
            return;
        }
        foreach (uint clId in ClientIDs)
        {
            if (clId == LocalClientID)
                continue;
            Net.TRANSPORT.Server_SendTo(DeliveryMethod.ReliableOrdered, n2, clId);
        }
    }

    public void Srv_SendMsgOne(uint msgID, uint clID, NetDataWriter netDataWriter)
    {
        if (!Net.is_server)
        {
            Log.Err("Srv_SendMsgOne: Cant send msg to one, not server.");
            return;
        }
        if (LocalClientID == clID)
        {
            // TODO
            // блять да запакуй ты все данные по игрокам в один класс Net и не ебись.
            // откуда мне нахуй id клиента и сервера брать?
            // я и раньше то юзал unity netcode чтоб его получить потому что у тебя хуй найдёшь.
            Log.Warn("Srv_SendMsgOne: Not gonna send message to self.");
            return;
        }
        NetDataWriter n2 = new();
        n2.Put(msgID);
        n2.PutBytesWithLength(netDataWriter.Data);
        Net.TRANSPORT.Server_SendTo(DeliveryMethod.ReliableOrdered, n2, clID);
    }

    public void Cl_SendMsgSrv(uint msgID, NetDataWriter netDataWriter)
    {
        if (Net.SERVER_CLIENTID == LocalClientID)
        {
            Log.Warn("Cl_SendMsgSrv: Not sending to avoid infinite loopback");
            return;
        }
        NetDataWriter n2 = new();
        n2.Put(msgID);
        n2.PutBytesWithLength(netDataWriter.Data);
        Net.TRANSPORT.Client_Send(DeliveryMethod.ReliableOrdered, n2);
    }
}
