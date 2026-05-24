using System.Collections.Generic;
using KrokoshaCasualtiesMP;
using LiteNetLib;
using LiteNetLib.Utils;
using UnityEngine;

namespace ChangeSkinMP;

public static class SkinNetworkHandler
{
    // public static void RegisterRecievers()
    // {
    //     // server
    //     if (Net.is_server)
    //     {
    //         Net.RegisterServerReciever(
    //             (ushort)Messages.RegistrationMessage,
    //             Srv_Handler_Registration
    //         );
    //         Net.RegisterServerReciever((ushort)Messages.SendSkinMessage, Srv_Handler_SkinMessage);
    //     }

    //     // client
    //     if (Net.is_client)
    //     {
    //         Net.RegisterClientReciever(
    //             (ushort)Messages.RegistrySyncMessage,
    //             Cl_Handler_RegistrySync
    //         );
    //         Net.RegisterClientReciever((ushort)Messages.SendSkinMessage, Cl_Handler_SkinMessage);
    //     }
    // }

    [ServerReciever((ushort)Messages.RegistrationMessage)]
    private static void Srv_Handler_Registration(uint senderClientId, ref NetDataReader reader)
    {
        uint clientId = reader.GetUInt();
        string nickname = reader.GetString();
        Log.Info(
            $"RegistrationMessage received from {senderClientId} for client {clientId} ({nickname})"
        );

        // Регистрируем у себя если ещё нет
        if (NetworkRegistry.Get(clientId) == null)
        {
            NetBody netBody = NetPlayer.ClientIdToPlayerDict[clientId]?.GetComponent<NetBody>();
            if (netBody != null)
                NetworkRegistry.RegisterConnected(netBody);
        }

        // Синхронизируем весь регистр со всеми
        Srv_Sender_RegistrySync();
    }

    private static void Srv_Sender_RegistrySync()
    {
        var entries = NetworkRegistry.Players;
        Log.Info($"Sending RegistrySyncMessage with {entries.Count} entries to all");

        NetDataWriter writer = new();
        writer.Put(entries.Count);
        foreach (var entry in entries)
        {
            writer.Put(entry.ClientID);
            writer.Put(entry.PlayerInfo.Nickname);
        }

        MessageSender.SendToAll(Messages.RegistrySyncMessage, writer);
    }

    [ServerReciever((ushort)Messages.RegistrySyncMessage)]
    private static void Cl_Handler_RegistrySync(uint _, ref NetDataReader reader)
    {
        int count = reader.GetInt();
        Log.Info($"RegistrySyncMessage received with {count} entries");
        for (int i = 0; i < count; i++)
        {
            uint clientId = reader.GetUInt();
            string nickname = reader.GetString();

            // Свой — пропускаем, LocalController уже есть
            if (clientId == NetPlayer.LOCAL_PLAYER.clientId)
                continue;

            // Уже зарегистрирован — пропускаем
            if (NetworkRegistry.Get(clientId) != null)
                continue;

            // Регистрируем чужого
            NetBody netBody = NetPlayer.ClientIdToPlayerDict[clientId].GetComponent<NetBody>();
            if (netBody != null)
                NetworkRegistry.RegisterConnected(netBody);
        }
    }

    // Сервер получает от клиента → валидирует → ретранслирует всем

    [ServerReciever((ushort)Messages.SendSkinMessage)]
    private static void Srv_Handler_SkinMessage(uint senderClientId, ref NetDataReader reader)
    {
        uint ownerId = reader.GetUInt();
        var skin = new SkinObject();
        skin.Deserialize(reader);
        Log.Info($"SendSkinMessage received from {senderClientId} for owner {ownerId}");

        // Клиент может слать только за себя
        if (senderClientId != ownerId)
        {
            Log.Warn($"Client {senderClientId} tried to spoof skin for {ownerId}");
            return;
        }

        if (BanList.Contains(NetworkRegistry.Get(senderClientId).PlayerInfo))
            return;

        // Применяем на хосте
        NetworkRegistry.Get(ownerId).SkinController.SetSkin(skin);

        // Ретранслируем остальным клиентам
        NetDataWriter writer = new();
        writer.Put(ownerId);
        skin.Serialize(writer);
        MessageSender.SendToAll(Messages.SendSkinMessage, writer);
    }

    [ServerReciever((ushort)Messages.SendSkinMessage)]
    private static void Cl_Handler_SkinMessage(uint _, ref NetDataReader reader)
    {
        uint ownerId = reader.GetUInt();
        var skin = new SkinObject();
        skin.Deserialize(reader);
        Log.Info($"SendSkinMessage received for owner {ownerId}");
        if (NetPlayer.LOCAL_PLAYER.clientId == ownerId)
            return;
        NetworkRegistry.Get(ownerId)?.SkinController.SetSkin(skin);
    }
}
