using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using CommandLine;
using CommandLine.Text;
using KrokoshaCasualtiesMP;
using LiteNetLib.Utils;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace ChangeSkinMP;

public static class SkinManager
{
    public static bool initialized = false;
    static SkinObject PendingSkin;

    internal static void AfterConnection()
    {
        SkinNetworkHandler.RegisterRecievers();
        NetworkRegistry.RegisterConnected(NetBody.NetIdToNetBody[NetPlayer.LOCAL_PLAYER.clientId]);

        if (!Net.is_server)
            Cl_SendRegistration();

        if (PendingSkin != null)
        {
            NetworkRegistry.LocalPlayerSkinController.SetSkin(PendingSkin);
            PendingSkin = null;
        }
    }

    private static void Cl_SendRegistration()
    {
        try
        {
            NetDataWriter writer = Net.CreateWriter((ushort)Messages.RegistrationMessage);
            writer.Put(NetPlayer.LOCAL_PLAYER.clientId);
            writer.Put(NetPlayer.LOCAL_PLAYER.playername);
            MessageSender.SendToServer(writer);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogWarning($"Failed to send registration: {e.Message}");
        }
    }

    public static void TryLoadLastSkin()
    {
        string lastSkin = ModConfig.Instance.LastSelectedSkin;
        if (string.IsNullOrEmpty(lastSkin))
            return;

        try
        {
            SkinObject skin = SkinObject.LoadFromLocal(lastSkin);
            PendingSkin = skin;
            Plugin.Logger.LogInfo($"Loaded last skin: {lastSkin}");
        }
        catch (FileNotFoundException)
        {
            if (
                ModConfig.Instance.LastSkinIsRemote
                && !string.IsNullOrEmpty(ModConfig.Instance.LastURL)
            )
            {
                Plugin.Logger.LogInfo($"Re-downloading remote skin: {ModConfig.Instance.LastURL}");
                try
                {
                    SkinObject skin = SkinObject.LoadFromUri(new Uri(ModConfig.Instance.LastURL));
                    PendingSkin = skin;
                }
                catch (Exception e)
                {
                    Plugin.Logger.LogWarning($"Failed to re-download skin: {e.Message}");
                    ModConfig.Instance.LastSelectedSkin = null;
                    ModConfig.Instance.LastURL = null;
                    ModConfig.Instance.Save();
                }
            }
            else
            {
                Plugin.Logger.LogWarning($"Last skin not found: {lastSkin}, clearing");
                ModConfig.Instance.LastSelectedSkin = null;
                ModConfig.Instance.Save();
            }
        }
    }

    public static void OnSceneUnloaded(Scene scene)
    {
        NetworkRegistry.Clear();
        SkinNetworkHandler.Reset();
        PendingSkin = null;
    }

    internal static void OnPlayerLeft(NetPlayer plr)
    {
        NetworkRegistry.RemovePlayer(plr.clientId);
    }

    public static string LoadLocal(string skinName)
    {
        SkinObject skin = SkinObject.LoadFromLocal(skinName);
        NetworkRegistry.LocalPlayerSkinController.SetSkin(skin);
        ModConfig.Instance.LastSelectedSkin = skin.Name;
        ModConfig.Instance.LastSkinIsRemote = false;
        ModConfig.Instance.AddRecentSkin(skin.Name);
        ModConfig.Instance.Save();
        return $"Local skin '{skin.Name}' loaded";
    }

    public static string LoadRemote(Uri uri)
    {
        SkinObject skin = SkinObject.LoadFromUri(uri);
        NetworkRegistry.LocalPlayerSkinController.SetSkin(skin);
        ModConfig.Instance.LastSelectedSkin = skin.Name;
        ModConfig.Instance.LastURL = uri.ToString();
        ModConfig.Instance.LastSkinIsRemote = true;
        ModConfig.Instance.AddRecentSkin(skin.Name);
        ModConfig.Instance.Save();
        return $"Remote skin '{uri}' loaded";
    }

    public static string LoadRemote(string url)
    {
        Uri uri = new(url);
        SkinObject skin = SkinObject.LoadFromUri(uri);
        NetworkRegistry.LocalPlayerSkinController.SetSkin(skin);
        ModConfig.Instance.LastSelectedSkin = skin.Name;
        ModConfig.Instance.LastURL = url;
        ModConfig.Instance.LastSkinIsRemote = true;
        ModConfig.Instance.AddRecentSkin(skin.Name);
        ModConfig.Instance.Save();
        return $"Remote skin '{url}' loaded";
    }

    public static string RuleSet(RuleName rule, bool value)
    {
        switch (rule)
        {
            case RuleName.SkinUploading:
                ModConfig.Instance.SkinUploading = value;
                return $"Skin uploading is now {value}";
            case RuleName.SkinDownloading:
                ModConfig.Instance.SkinDownloading = value;
                return $"Skin downloading is now {value}";
            default:
                return "Unknown rule";
        }
    }

    public static string RuleGet(RuleName rule) =>
        rule switch
        {
            RuleName.SkinUploading => $"Skin uploading: {ModConfig.Instance.SkinUploading}",
            RuleName.SkinDownloading => $"Skin downloading: {ModConfig.Instance.SkinDownloading}",
            _ => "Unknown rule",
        };

    public static string BanPlayer(string playerName)
    {
        PlayerInfo playerInfo = NetworkRegistry.Get(playerName).PlayerInfo;
        BanList.Ban(playerInfo);
        return $"{playerName} banned";
    }

    public static string BanPlayer(uint id)
    {
        PlayerInfo playerInfo = NetworkRegistry.Get(id).PlayerInfo;
        BanList.Ban(playerInfo);
        return $"{playerInfo.Nickname} banned";
    }

    public static string UnbanPlayer(string playerName)
    {
        PlayerInfo playerInfo = NetworkRegistry.Get(playerName).PlayerInfo;
        BanList.Unban(playerInfo);
        return $"{playerInfo.Nickname} unbanned";
    }

    public static string UnbanPlayer(uint id)
    {
        PlayerInfo playerInfo = NetworkRegistry.Get(id).PlayerInfo;
        BanList.Unban(playerInfo);
        return $"{playerInfo.Nickname} unbanned";
    }

    public static string EnableSkins()
    {
        foreach (NetworkRegistryEntry networkRegistryEntry in NetworkRegistry.Players)
        {
            networkRegistryEntry.SkinController.Enable();
        }
        if (NetworkRegistry.LocalPlayerSkinController?.CBody != null
            && NetworkRegistry.LocalPlayerSkinController.CBody.Skin != null)
            NetworkRegistry.LocalPlayerSkinController.CBody.RepStart();
        return "ChangeSkin enabled";
    }

    public static string DisableSkins()
    {
        foreach (NetworkRegistryEntry networkRegistryEntry in NetworkRegistry.Players)
        {
            networkRegistryEntry.SkinController.Disable();
        }
        if (NetworkRegistry.LocalPlayerSkinController?.CBody != null)
            NetworkRegistry.LocalPlayerSkinController.CBody.RepEnd();
        return "ChangeSkin disabled";
    }
}
