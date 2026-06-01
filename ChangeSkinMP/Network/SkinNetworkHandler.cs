using System;
using KrokoshaCasualtiesMP;
using LiteNetLib.Utils;

namespace ChangeSkinMP;

public static class SkinNetworkHandler
{
    private static bool _registered;

    public static void Reset() => _registered = false;

    public static void RegisterRecievers()
    {
        if (_registered)
            return;
        _registered = true;

        if (Net.is_server)
        {
            try { Net.RegisterServerReciever((ushort)Messages.RegistrationMessage, Srv_Handler_Registration); }
            catch (ArgumentException) { Log.Info($"Server receiver {Messages.RegistrationMessage} already registered"); }
            try { Net.RegisterServerReciever((ushort)Messages.SendSkinMessage, Srv_Handler_SkinMessage); }
            catch (ArgumentException) { Log.Info($"Server receiver {Messages.SendSkinMessage} already registered"); }
        }

        if (Net.is_client_or_host)
        {
            try { Net.RegisterClientReciever((ushort)Messages.RegistrySyncMessage, Cl_Handler_RegistrySync); }
            catch (ArgumentException) { Log.Info($"Client receiver {Messages.RegistrySyncMessage} already registered"); }
            try { Net.RegisterClientReciever((ushort)Messages.SendSkinMessage, Cl_Handler_SkinMessage); }
            catch (ArgumentException) { Log.Info($"Client receiver {Messages.SendSkinMessage} already registered"); }
            try { Net.RegisterClientReciever((ushort)Messages.SkinBanMessage, Cl_Handler_SkinBanMessage); }
            catch (ArgumentException) { Log.Info($"Client receiver {Messages.SkinBanMessage} already registered"); }
            try { Net.RegisterClientReciever((ushort)Messages.SkinAnnouncementMessage, Cl_Handler_SkinAnnouncement); }
            catch (ArgumentException) { Log.Info($"Client receiver {Messages.SkinAnnouncementMessage} already registered"); }
        }
    }

    private static void Srv_Handler_Registration(uint senderClientId, ref NetDataReader reader)
    {
        try
        {
            uint clientId = reader.GetUInt();
            string nickname = reader.GetString();
            Log.Info($"RegistrationMessage from {senderClientId} for {clientId} ({nickname})");

            if (NetworkRegistry.Get(clientId) == null)
            {
                if (NetBody.NetIdToNetBody.TryGetValue(clientId, out NetBody netBody))
                    NetworkRegistry.RegisterConnected(netBody);
            }

            Srv_Sender_RegistrySync();
            Srv_Sender_SkinSync(senderClientId);
        }
        catch (Exception e)
        {
            Log.Err($"Srv_Handler_Registration error: {e}");
        }
    }

    private static void Srv_Sender_RegistrySync()
    {
        var entries = NetworkRegistry.Players;
        Log.Info($"Sending RegistrySyncMessage with {entries.Count} entries");

        NetDataWriter writer = Net.CreateWriter((ushort)Messages.RegistrySyncMessage);
        writer.Put(entries.Count);
        foreach (var entry in entries)
        {
            writer.Put(entry.ClientID);
            writer.Put(entry.PlayerInfo.Nickname);
        }
        MessageSender.SendToAll(writer);
    }

    private static void Srv_Sender_SkinSync(uint targetClientId)
    {
        foreach (var entry in NetworkRegistry.Players)
        {
            if (entry.ClientID == targetClientId) continue;

            bool hasSkin = entry.CBody.Skin != null;
            NetDataWriter writer = Net.CreateWriter((ushort)Messages.SendSkinMessage);
            writer.Put(entry.ClientID);
            writer.Put(hasSkin);
            if (hasSkin)
                entry.CBody.Skin.Serialize(writer);
            Log.Info($"SkinSync: sending {(hasSkin ? $"skin ({entry.CBody.Skin.Name})" : "default")} of \"{entry.PlayerInfo.Nickname}\" to new client {targetClientId}");
            MessageSender.SendToOne(writer, targetClientId);
        }
    }

    private static void VerifySkinApplied(NetworkRegistryEntry entry, string expectedSkinName)
    {
        if (entry.CBody.Skin == null || entry.CBody.Skin.Name != expectedSkinName)
            Log.Err($"Skin verify FAILED for \"{entry.PlayerInfo.Nickname}\": expected \"{expectedSkinName}\" but CBody.Skin={entry.CBody.Skin?.Name ?? "null"}");
        if (!entry.CBody.Working)
            Log.Err($"Skin verify FAILED for \"{entry.PlayerInfo.Nickname}\": RepStart not active (Working=false)");
    }

    private static void Srv_Handler_SkinMessage(uint senderClientId, ref NetDataReader reader)
    {
        try
        {
            uint ownerId = reader.GetUInt();
            bool hasSkin = reader.GetBool();

            if (senderClientId != ownerId)
            {
                Log.Warn($"Client {senderClientId} tried to spoof skin for {ownerId}");
                return;
            }

            var entry = NetworkRegistry.Get(senderClientId);
            if (entry == null) return;
            if (BanList.Contains(entry.PlayerInfo)) return;

            if (hasSkin)
            {
                var skin = new SkinObject();
                skin.Deserialize(reader);
                entry.SkinController.SetSkin(skin);
                Log.Info($"Skin change received from \"{entry.PlayerInfo.Nickname}\" (skin={skin.Name})");
                ConsoleScript.instance.LogToConsole($"[ChangeSkin] {entry.PlayerInfo.Nickname} has changed their skin");
                VerifySkinApplied(entry, skin.Name);
            }
            else
            {
                entry.SkinController.CBody.ResetSkin();
                Log.Info($"Skin change received from \"{entry.PlayerInfo.Nickname}\" (skin=default)");
                ConsoleScript.instance.LogToConsole($"[ChangeSkin] {entry.PlayerInfo.Nickname} has changed their skin");
            }

            NetDataWriter writer = Net.CreateWriter((ushort)Messages.SendSkinMessage);
            writer.Put(ownerId);
            writer.Put(hasSkin);
            if (hasSkin)
            {
                SkinObject skin = entry.CBody.Skin;
                skin.Serialize(writer);
            }
            MessageSender.SendToOthers(writer, senderClientId);
        }
        catch (Exception e)
        {
            Log.Err($"Srv_Handler_SkinMessage error: {e}");
        }
    }

    private static void Cl_Handler_RegistrySync(uint _, ref NetDataReader reader)
    {
        try
        {
            int count = reader.GetInt();
            Log.Info($"RegistrySyncMessage received with {count} entries");
            for (int i = 0; i < count; i++)
            {
                uint clientId = reader.GetUInt();
                string nickname = reader.GetString();

                if (clientId == NetPlayer.LOCAL_PLAYER.clientId)
                    continue;
                if (NetworkRegistry.Get(clientId) != null)
                    continue;

                if (NetBody.NetIdToNetBody.TryGetValue(clientId, out NetBody netBody))
                    NetworkRegistry.RegisterConnected(netBody);
            }
        }
        catch (Exception e)
        {
            Log.Err($"Cl_Handler_RegistrySync error: {e}");
        }
    }

    private static void Cl_Handler_SkinMessage(uint _, ref NetDataReader reader)
    {
        try
        {
            uint ownerId = reader.GetUInt();
            bool hasSkin = reader.GetBool();
            if (NetPlayer.LOCAL_PLAYER.clientId == ownerId)
                return;
            var entry = NetworkRegistry.Get(ownerId);
            if (entry == null) return;
            string nickname = entry.PlayerInfo.Nickname;
            if (hasSkin)
            {
                var skin = new SkinObject();
                skin.Deserialize(reader);
                entry.SkinController.SetSkin(skin);
                Log.Info($"Skin change received from \"{nickname}\" (skin={skin.Name})");
                ConsoleScript.instance.LogToConsole($"[ChangeSkin] {nickname} has changed their skin");
                VerifySkinApplied(entry, skin.Name);
            }
            else
            {
                entry.SkinController.CBody.ResetSkin();
                Log.Info($"Skin change received from \"{nickname}\" (skin=default)");
                ConsoleScript.instance.LogToConsole($"[ChangeSkin] {nickname} has changed their skin");
            }
        }
        catch (Exception e)
        {
            Log.Err($"Cl_Handler_SkinMessage error: {e}");
        }
    }

    private static void Cl_Handler_SkinBanMessage(uint _, ref NetDataReader reader)
    {
        try
        {
            var playerInfo = new PlayerInfo();
            playerInfo.Deserialize(reader);
            bool banned = reader.GetBool();
            Log.Info($"SkinBanMessage received for {playerInfo.Nickname}: {banned}");

            if (playerInfo.Nickname == NetPlayer.LOCAL_PLAYER.playername)
            {
                if (banned)
                    NetworkRegistry.LocalPlayerSkinController?.ResetSkin();
                return;
            }

            var entry = NetworkRegistry.Get(playerInfo);
            if (entry != null)
                entry.SkinController.OnBanReceived(banned);
        }
        catch (Exception e)
        {
            Log.Err($"Cl_Handler_SkinBanMessage error: {e}");
        }
    }

    private static void Cl_Handler_SkinAnnouncement(uint _, ref NetDataReader reader)
    {
        try
        {
            bool enabled = reader.GetBool();
            string message = reader.GetString();
            ModConfig.Instance.SkinChangingEnabled = enabled;
            ModConfig.Instance.Save();
            ConsoleScript.instance.LogToConsole($"[ChangeSkin] {message}");
            Log.Info($"SkinAnnouncement received: skins {(enabled ? "enabled" : "disabled")}");
        }
        catch (Exception e)
        {
            Log.Err($"Cl_Handler_SkinAnnouncement error: {e}");
        }
    }

    public static void BroadcastSkinAnnouncement(bool enabled, string message)
    {
        if (!Net.is_server) return;
        NetDataWriter writer = Net.CreateWriter((ushort)Messages.SkinAnnouncementMessage);
        writer.Put(enabled);
        writer.Put(message);
        MessageSender.SendToAll(writer);
        Log.Info($"SkinAnnouncement broadcast: {message}");
    }
}
