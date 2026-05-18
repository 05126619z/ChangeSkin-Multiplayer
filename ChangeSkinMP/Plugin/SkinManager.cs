using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using CommandLine;
using CommandLine.Text;
using KrokoshaCasualtiesMP;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace ChangeSkinMP;

public static class SkinManager
{
    public static SimpleMessage SimpleMessage { get; private set; }
    public static bool initialized = false;

    static SkinObject PendingSkin;

    public static void Init()
    {
        if (initialized)
            return;
        SimpleMessage = new(ServerMain.AllClientIdsExceptHost, NetPlayer.LOCAL_PLAYER.clientId);
        MessageSender.Init(SimpleMessage);
        SceneManager.sceneUnloaded += new UnityAction<Scene>(OnSceneUnloaded);
        WorldgenPatches.OnWorldgenFinish += AfterConnection;
        TryLoadLastSkin();

        initialized = true;
        ConsoleScript.instance.LogToConsole("ChangeSkin initialized");
    }

    internal static void AfterConnection()
    {
        foreach (NetBody netBody in NetBody.all_instances)
        {
            if (NetworkRegistry.Get(netBody) == null)
                NetworkRegistry.RegisterConnected(netBody);
        }
        if (PendingSkin != null)
        {
            NetworkRegistry.LocalPlayerSkinController.SetSkin(PendingSkin);
            PendingSkin = null;
        }
    }

    private static void TryLoadLastSkin()
    {
        string lastSkin = ModConfig.Instance.LastSelectedSkin;
        if (string.IsNullOrEmpty(lastSkin))
            return;

        try
        {
            SkinObject skin;

            if (ModConfig.Instance.LastSkinIsRemote)
            {
                // Сначала проверяем есть ли уже скачанный zip локально
                skin = SkinObject.LoadFromLocal(lastSkin);
                Plugin.Logger.LogInfo($"Loaded cached remote skin: {lastSkin}");
            }
            else
            {
                skin = SkinObject.LoadFromLocal(lastSkin);
                Plugin.Logger.LogInfo($"Loaded local skin: {lastSkin}");
            }

            SkinManager.PendingSkin = skin;
        }
        catch (FileNotFoundException)
        {
            // Локального кеша нет — качаем заново
            if (
                ModConfig.Instance.LastSkinIsRemote
                && !string.IsNullOrEmpty(ModConfig.Instance.LastURL)
            )
            {
                Plugin.Logger.LogInfo($"Re-downloading remote skin: {ModConfig.Instance.LastURL}");
                try
                {
                    SkinObject skin = SkinObject.LoadFromUri(new Uri(ModConfig.Instance.LastURL));
                    SkinManager.PendingSkin = skin;
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

    private static void OnSceneUnloaded(Scene scene)
    {
        Destructor();
    }

    public static void Destructor()
    {
        NetworkRegistry.Clear();
    }

    // ── Реализации ───────────────────────────────────────────────────────────────

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
        return "ChangeSkin enabled";
    }

    public static string DisableSkins()
    {
        foreach (NetworkRegistryEntry networkRegistryEntry in NetworkRegistry.Players)
        {
            networkRegistryEntry.SkinController.Disable();
        }
        return "ChangeSkin disabled";
    }
}
