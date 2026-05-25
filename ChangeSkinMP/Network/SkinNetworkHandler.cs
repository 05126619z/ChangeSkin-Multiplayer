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
            Net.RegisterServerReciever(
                (ushort)Messages.RegistrationMessage,
                Srv_Handler_Registration
            );
            Net.RegisterServerReciever(
                (ushort)Messages.SendSkinMessage,
                Srv_Handler_SkinMessage
            );
        }

        if (Net.is_client_or_host)
        {
            Net.RegisterClientReciever(
                (ushort)Messages.RegistrySyncMessage,
                Cl_Handler_RegistrySync
            );
            Net.RegisterClientReciever(
                (ushort)Messages.SendSkinMessage,
                Cl_Handler_SkinMessage
            );
            Net.RegisterClientReciever(
                (ushort)Messages.SkinBanMessage,
                Cl_Handler_SkinBanMessage
            );
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
            Log.Info($"SkinSync: sending {(hasSkin ? "skin" : "default")} of {entry.ClientID} to new client {targetClientId}");
            MessageSender.SendToOne(writer, targetClientId);
        }
    }

    private static void Srv_Handler_SkinMessage(uint senderClientId, ref NetDataReader reader)
    {
        try
        {
            uint ownerId = reader.GetUInt();
            bool hasSkin = reader.GetBool();
            Log.Info($"SendSkinMessage from {senderClientId} for owner {ownerId}, hasSkin={hasSkin}");

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
            }
            else
            {
                entry.SkinController.CBody.ResetSkin();
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
            Log.Info($"SendSkinMessage received for owner {ownerId}, hasSkin={hasSkin}");
            if (NetPlayer.LOCAL_PLAYER.clientId == ownerId)
                return;
            var entry = NetworkRegistry.Get(ownerId);
            if (entry == null) return;
            if (hasSkin)
            {
                var skin = new SkinObject();
                skin.Deserialize(reader);
                entry.SkinController.SetSkin(skin);
            }
            else
            {
                entry.SkinController.CBody.ResetSkin();
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
}
