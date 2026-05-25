using System;
using KrokoshaCasualtiesMP;
using LiteNetLib;
using LiteNetLib.Utils;
using UnityEngine;

namespace ChangeSkinMP;

public class LocalSkinController : MonoBehaviour
{
    public ChangeBody CBody { get; private set; }

    void Awake()
    {
        CBody = GetComponent<ChangeBody>();
    }

    public void SetSkin(string skinName)
    {
        SkinObject skin = SkinObject.LoadFromLocal(skinName);
        CBody.ApplySkin(skin);
        CBody.RepStart();
        SendToOthers(skin);
    }

    public void SetSkin(Uri uri)
    {
        SkinObject skin = SkinObject.LoadFromUri(uri);
        CBody.ApplySkin(skin);
        CBody.RepStart();
        SendToOthers(skin);
    }

    public void SetSkin(SkinObject skin)
    {
        CBody.ApplySkin(skin);
        CBody.RepStart();
        SendToOthers(skin);
    }

    private void SendToOthers(SkinObject skin)
    {
        NetDataWriter writer = Net.CreateWriter((ushort)Messages.SendSkinMessage);
        writer.Put(CBody.OwnerID);
        skin.Serialize(writer);
        if (Net.is_server)
            MessageSender.SendToAll(writer);
        else
            MessageSender.SendToServer(writer);
    }
}
