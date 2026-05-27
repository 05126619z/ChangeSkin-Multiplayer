using KrokoshaCasualtiesMP;
using LiteNetLib.Utils;

namespace ChangeSkinMP;

public class PlayerInfo : INetSerializable
{
    public string Nickname { get; private set; }
    public ulong? SteamID { get; private set; }

    public PlayerInfo() { }

    public PlayerInfo(NetBody netBody)
    {
        Nickname = netBody.playername;
        SteamID = netBody.plr.steam_id;
    }

    public void Serialize(NetDataWriter dataWriter)
    {
        dataWriter.Put(Nickname ?? "");
        dataWriter.Put(SteamID ?? 0);
    }

    public void Deserialize(NetDataReader dataReader)
    {
        Nickname = dataReader.GetString();
        SteamID = dataReader.GetULong();
    }

    public override bool Equals(object obj) =>
        obj is PlayerInfo other && Nickname == other.Nickname && SteamID == other.SteamID;

    public override int GetHashCode() => (Nickname, SteamID).GetHashCode();
}
