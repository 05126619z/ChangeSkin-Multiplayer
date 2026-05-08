using LiteNetLib.Utils;

namespace ChangeSkinMP;

public static class MessageSender
{
    private static SimpleMessage _simple;

    // Инициализируется один раз при старте
    public static void Init(SimpleMessage simple) => _simple = simple;

    public static void SendToAll(this Messages msg, NetDataWriter payload)
    {
        _simple.Srv_SendMsgAll((uint)msg, payload);
    }

    public static void SendToOne(this Messages msg, uint clientId, NetDataWriter payload)
    {
        _simple.Srv_SendMsgOne((uint)msg, clientId, payload);
    }

    public static void SendToServer(this Messages msg, NetDataWriter payload)
    {
        _simple.Cl_SendMsgSrv((uint)msg, payload);
    }
}
