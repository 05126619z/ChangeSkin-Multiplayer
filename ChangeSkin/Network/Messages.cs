using KrokoshaCasualtiesMP;
using LiteNetLib;
using LiteNetLib.Utils;
using Unity.Collections;
using static ChangeSkinMP.ChangeBody;

namespace ChangeSkinMP;

public enum Messages : ushort
{
    SendSkinMessage = 17000,
    RequestSkinMessage,
    SkinBanMessage,
}
