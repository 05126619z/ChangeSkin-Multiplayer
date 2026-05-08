using KrokoshaCasualtiesMP;
using LiteNetLib.Utils;

namespace ChangeSkinMP;

public class PlayerInfo : INetSerializable
{
    public uint ClientId { get; private set; }
    public string Nickname { get; private set; }
    public string? SteamID { get; private set; }

    public PlayerInfo(NetBody netBody)
    {
        ClientId = netBody.netId;
        Nickname = netBody.plr.playername;
        SteamID = netBody.plr.steam_id.ToString();
    }

    public void Serialize(NetDataWriter dataWriter)
    {
        dataWriter.Put(ClientId);
        dataWriter.Put(Nickname);
        dataWriter.Put(SteamID ?? "empty");
    }

    public void Deserialize(NetDataReader dataReader)
    {
        ClientId = dataReader.GetUInt();
        Nickname = dataReader.GetString();
        SteamID = dataReader.GetString();
    }
}
