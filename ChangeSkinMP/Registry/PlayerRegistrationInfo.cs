using LiteNetLib.Utils;

namespace ChangeSkinMP;

public struct PlayerRegistrationInfo : INetSerializable
{
    public uint ClientId;
    public string Nickname;

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(ClientId);
        writer.Put(Nickname);
    }

    public void Deserialize(NetDataReader reader)
    {
        ClientId = reader.GetUInt();
        Nickname = reader.GetString();
    }
}
