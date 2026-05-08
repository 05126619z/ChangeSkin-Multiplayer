using System.Collections.Generic;
using KrokoshaCasualtiesMP;
using LiteNetLib;
using LiteNetLib.Utils;
using UnityEngine;

namespace ChangeSkinMP;

public class SkinNetworkHandler : MonoBehaviour
{
    // clientId → контроллер удалённого игрока
    private readonly Dictionary<uint, RemoteSkinController> _remotes = new();

    private void Start()
    {
        RegisterRecievers();
    }

    public void RegisterRecievers()
    {
        if (Net.is_server)
        {
            Net.RegisterServerReciever((ushort)Messages.SendSkinMessage, Server_HandleSkinMessage);
            Net.RegisterServerReciever(
                (ushort)Messages.RequestSkinMessage,
                Server_HandleSkinRequest
            );
        }

        if (Net.is_client_or_host)
        {
            Net.RegisterClientReciever((ushort)Messages.SendSkinMessage, Client_HandleSkinMessage);
            Net.RegisterClientReciever(
                (ushort)Messages.RequestSkinMessage,
                Client_HandleSkinRequest
            );
            Net.RegisterClientReciever((ushort)Messages.SkinBanMessage, Client_HandleBanMessage);
        }
    }

    // private void OnMessageReceived(uint senderClientId, NetDataReader reader)
    // {
    //     uint msgId = reader.GetUInt();

    //     switch ((Messages)msgId)
    //     {
    //         case Messages.SendSkinMessage:
    //             HandleSkinMessage(senderClientId, reader);
    //             break;
    //         case Messages.RequestSkinMessage:
    //             HandleSkinRequest(senderClientId, reader);
    //             break;
    //         case Messages.SkinBanMessage:
    //             HandleBanMessage(senderClientId, reader);
    //             break;
    //     }
    // }

    // Сервер получает от клиента → валидирует → ретранслирует всем
    private void Server_HandleSkinMessage(uint senderClientId, ref NetDataReader reader)
    {
        uint ownerId = reader.GetUInt();
        var skin = new SkinObject();
        skin.Deserialize(reader);

        // Клиент может слать только за себя
        if (senderClientId != ownerId)
        {
            Log.Warn($"Client {senderClientId} tried to spoof skin for {ownerId}");
            return;
        }

        if (BanList.Contains(senderClientId))
            return;

        // Применяем на хосте
        NetworkRegistry.GetById(ownerId)?.GetComponent<RemoteSkinController>()?.SetSkin(skin);

        // Ретранслируем остальным клиентам
        NetDataWriter writer = new();
        writer.Put(ownerId);
        skin.Serialize(writer);
        MessageSender.SendToAll(Messages.SendSkinMessage, writer);
    }

    // Клиент получает от сервера → просто применяет, без валидации (сервер уже проверил)
    private void Client_HandleSkinMessage(uint _, ref NetDataReader reader)
    {
        uint ownerId = reader.GetUInt();
        var skin = new SkinObject();
        skin.Deserialize(reader);

        NetworkRegistry.GetById(ownerId)?.GetComponent<RemoteSkinController>()?.SetSkin(skin);
    }

    private void Server_HandleSkinRequest(uint senderClientId, ref NetDataReader reader) { }

    private void Client_HandleSkinRequest(uint _, ref NetDataReader reader) { }

    private void Client_HandleBanMessage(uint _, ref NetDataReader reader)
    {
        uint clientId = reader.GetUInt();
        bool banned = reader.GetBool();

        NetworkRegistry
            .GetById(clientId)
            ?.GetComponent<RemoteSkinController>()
            ?.OnBanReceived(banned);
    }

    // private void HandleSkinMessage(uint senderClientId, NetDataReader reader)
    // {
    //     // create writer for in case you're server
    //     NetDataWriter writer = new NetDataWriter();
    //     writer.Put(reader.RawData, reader.RawDataSize, reader.AvailableBytes);

    //     // unpack
    //     uint ChangeBodyOwnerId;
    //     SkinObject skin = new();
    //     ChangeBodyOwnerId = reader.GetUInt();
    //     skin.Deserialize(reader);

    //     // fag check
    //     if (senderClientId != ChangeBodyOwnerId && senderClientId != Net.SERVER_CLIENTID)
    //         return;
    //     if (BanList.Contains(senderClientId) || BanList.Contains(ChangeBodyOwnerId))
    //         return;

    //     // set skin
    //     ChangeBody? body = NetworkRegistry.GetById(ChangeBodyOwnerId);
    //     body?.GetComponent<RemoteSkinController>()?.SetSkin(skin);

    //     // send to others.
    //     if (Net.is_server)
    //     {
    //         MessageSender.SendToAll(Messages.SendSkinMessage, writer);
    //     }
    // }

    // private void HandleSkinRequest(uint senderClientId, NetDataReader reader) { }

    // private void HandleBanMessage(uint senderClientId, NetDataReader reader) { }
}
