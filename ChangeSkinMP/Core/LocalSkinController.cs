using System;
using KrokoshaCasualtiesMP;
using LiteNetLib;
using LiteNetLib.Utils;
using UnityEngine;

namespace ChangeSkinMP;

// Вешается на того же GameObject что и ChangeBody, но только локальному игроку
public class LocalSkinController : MonoBehaviour
{
    public ChangeBody CBody { get; private set; }

    void Awake()
    {
        CBody = GetComponent<ChangeBody>();
    }

    // Смена скина — только локальный игрок может это вызвать
    public void SetSkin(string skinName)
    {
        SkinObject skin = SkinObject.LoadFromLocal(skinName);
        CBody.ApplySkin(skin);
        SendToOthers(skin);
    }

    public void SetSkin(Uri uri)
    {
        SkinObject skin = SkinObject.LoadFromUri(uri);
        CBody.ApplySkin(skin);
        SendToOthers(skin);
    }

    public void SetSkin(SkinObject skin)
    {
        CBody.ApplySkin(skin);
        SendToOthers(skin);
    }

    private void SendToOthers(SkinObject skin)
    {
        var writer = new NetDataWriter();
        writer.Put(CBody.OwnerID);
        skin.Serialize(writer);
        if (Net.is_server)
        {
            MessageSender.SendToAll(Messages.SendSkinMessage, writer);
        }
        else
        {
            MessageSender.SendToServer(Messages.SendSkinMessage, writer);
        }
    }
}
