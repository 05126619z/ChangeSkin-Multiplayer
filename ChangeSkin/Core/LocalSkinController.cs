using System;
using KrokoshaCasualtiesMP;
using LiteNetLib;
using LiteNetLib.Utils;
using UnityEngine;

namespace ChangeSkinMP;

// Вешается на того же GameObject что и ChangeBody, но только локальному игроку
public class LocalSkinController : MonoBehaviour
{
    ChangeBody _body;
    Messages messages; // или что у тебя

    private void Awake()
    {
        _body = GetComponent<ChangeBody>();
    }

    // Смена скина — только локальный игрок может это вызвать
    public void SetSkin(string skinName)
    {
        SkinObject skin = SkinObject.LoadFromLocal(skinName);
        _body.ApplySkin(skin);
        SendToOthers(skin);
    }

    public void SetSkin(Uri uri)
    {
        SkinObject skin = SkinObject.LoadFromUri(uri);
        _body.ApplySkin(skin);
        SendToOthers(skin);
    }

    private void SendToOthers(SkinObject skin)
    {
        var writer = new NetDataWriter();
        writer.Put(_body.OwnerID);
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
