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
        // Single-player: no MP local player, nothing to broadcast.
        // (4.0.1 inits Steamworks in SP too, so is_server/is_client_or_host
        //  are unreliable; LOCAL_PLAYER == null is the real no-lobby signal.)
        if (NetPlayer.LOCAL_PLAYER == null)
        {
            Log.Info($"Skin applied locally (single-player): {skin.Name}");
            return;
        }
        NetDataWriter writer = Net.CreateWriter((ushort)Messages.SendSkinMessage);
        writer.Put(CBody.OwnerID);
        writer.Put(true);
        skin.Serialize(writer);
        if (Net.is_server)
            MessageSender.SendToAll(writer);
        else
            MessageSender.SendToServer(writer);
        Log.Info($"Skin change sent (owner={CBody.OwnerID}, skin={skin.Name})");
        ConsoleScript.instance.LogToConsole($"[ChangeSkin] You have sent a skin change signal");
    }

    public void ResetSkin()
    {
        CBody.ResetSkin();
        // Single-player: no MP local player, nothing to broadcast.
        if (NetPlayer.LOCAL_PLAYER == null)
        {
            Log.Info("Skin reset locally (single-player)");
            return;
        }
        NetDataWriter writer = Net.CreateWriter((ushort)Messages.SendSkinMessage);
        writer.Put(CBody.OwnerID);
        writer.Put(false);
        if (Net.is_server)
            MessageSender.SendToAll(writer);
        else
            MessageSender.SendToServer(writer);
        Log.Info($"Skin change sent (owner={CBody.OwnerID}, skin=default)");
        ConsoleScript.instance.LogToConsole($"[ChangeSkin] You have sent a skin change signal");
    }
}
