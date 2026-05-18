using KrokoshaCasualtiesMP;

namespace ChangeSkinMP;

public class NetworkRegistryEntry
{
    public uint ClientID { get; private set; }
    public PlayerInfo PlayerInfo { get; private set; }
    public RemoteSkinController SkinController { get; private set; }
    public ChangeBody CBody { get; private set; }
    public NetBody NBody { get; private set; }

    public NetworkRegistryEntry(
        NetBody netBody,
        uint clientID,
        PlayerInfo playerInfo,
        RemoteSkinController skinController
    )
    {
        ClientID = clientID;
        PlayerInfo = playerInfo;
        SkinController = skinController;
        NBody = netBody;
        CBody = skinController.CBody;
    }
}
