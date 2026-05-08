using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using Clap.Net.Models;
using KrokoshaCasualtiesMP;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace ChangeSkinMP;

public static class SkinManager
{
    public static SimpleMessage SimpleMessage { get; private set; }
    public static bool initialized = false;

    public static void Init()
    {
        if (initialized)
            return;
        SimpleMessage = new(ServerMain.AllClientIdsExceptHost, NetPlayer.LOCAL_PLAYER.clientId);
        MessageSender.Init(SimpleMessage);
        foreach (NetBody netBody in NetBody.all_instances)
        {
            NetworkRegistry.RegisterConnected(netBody);
        }

        SceneManager.sceneUnloaded += new UnityAction<Scene>(OnSceneUnloaded);
        TextureStorage.SaveOGSprites();

        switch (Plugin.ModConfig.lastSelected)
        {
            case ModConfig.LastSelected.Local:
            {
                if (Plugin.ModConfig.LastSelectedSkin != null)
                    SkinSelectLocal(localChangeBody, Plugin.ModConfig.LastSelectedSkin);
                break;
            }
            case ModConfig.LastSelected.Remote:
            {
                if (Plugin.ModConfig.LastURL != null)
                    SkinSelectRemote(localChangeBody, Plugin.ModConfig.LastURL);
                break;
            }
            default:
            {
                break;
            }
        }
        initialized = true;
        ConsoleScript.instance.LogToConsole("ChangeSkin initialized");
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "SampleScene") { }
    }

    private static void OnSceneUnloaded(Scene scene)
    {
        Destructor();
    }

    public static void Destructor()
    {
        NetworkRegistry.Clear();
    }

    public static void SkinSelectLocal(ChangeBody changeBody, string skinName)
    {
        changeBody.UseLocalSkin(skinName);
    }

    public static void SkinSelectRemote(ChangeBody changeBody, string url)
    {
        changeBody.UseURLSkin(url);
    }

    public static string Execute(
        string[] args,
        ChangeBody localChangeBody,
        IEnumerable<NetBody> playerBodies,
        Dictionary<object, ChangeBody> replacers
    )
    {
        ParseResult<SkinCommand> result = SkinCommand.TryParse(new ReadOnlySpan<string>(args));

        // Clap.Net вернул Help или ошибку — просто выводим сообщение
        if (result.IsT1)
            return result.AsT1.Message; // --help
        if (result.IsT2)
            return result.AsT2.Message; // --version
        if (result.IsT3)
            return result.AsT3.Message; // parse error

        var message = result.AsT0.Command switch
        {
            // skin load local <name>
            SkinSubCommands.Load { Command: LoadSubCommands.Local local } => LoadLocal(
                local.SkinName,
                localChangeBody
            ),

            // skin load remote <url>
            SkinSubCommands.Load { Command: LoadSubCommands.Remote remote } => LoadRemote(
                remote.Url,
                localChangeBody
            ),

            // skin rule set <rule> <value>
            SkinSubCommands.Rule { Command: RuleSubCommands.Set set } => RuleSet(
                set.Rule,
                set.Value
            ),

            // skin rule get <rule>
            SkinSubCommands.Rule { Command: RuleSubCommands.Get get } => RuleGet(get.Rule),

            SkinSubCommands.Ban ban => BanPlayer(ban.Player, playerBodies),
            SkinSubCommands.Unban unban => UnbanPlayer(unban.Player, playerBodies),
            SkinSubCommands.Enable => EnableSkins(localChangeBody, replacers),
            SkinSubCommands.Disable => DisableSkins(replacers),
            SkinSubCommands.Reload => ReloadSkins(replacers),
            SkinSubCommands.Unload => UnloadSelf(localChangeBody),
            SkinSubCommands.ClearCache => ClearCache(),
            SkinSubCommands.Verbose v => SetVerbose(v.Enabled),

            _ => "Unknown command. Try: skin --help",
        };

        Plugin.Instance.SaveConfig();
        Plugin.Logger.LogInfo(message);
        return message;
    }

    // ── Реализации ───────────────────────────────────────────────────────────────

    private static string LoadLocal(string skinName, ChangeBody changeBody)
    {
        SkinSelectLocal(changeBody, skinName);
        Plugin.ModConfig.LastSelectedSkin = skinName;
        localChangeBody.UseLocalSkin(skinName);
        return $"Local skin '{skinName}' loaded";
    }

    private static string LoadRemote(string url, ChangeBody changeBody)
    {
        if (!Plugin.ModConfig.SkinDownloading)
            return "Skin downloading is disabled by server rules";

        SkinSelectRemote(changeBody, url);
        Plugin.ModConfig.LastURL = url;
        ChangeSkinNetworkComponent.SendRemoteSkinMessage(url, changeBody.skinName);
        return $"Remote skin '{url}' loaded";
    }

    private static string RuleSet(RuleName rule, bool value)
    {
        switch (rule)
        {
            case RuleName.SkinUploading:
                Plugin.ModConfig.SkinUploading = value;
                return $"Skin uploading is now {value}";
            case RuleName.SkinDownloading:
                Plugin.ModConfig.SkinDownloading = value;
                return $"Skin downloading is now {value}";
            default:
                return "Unknown rule";
        }
    }

    private static string RuleGet(RuleName rule) =>
        rule switch
        {
            RuleName.SkinUploading => $"Skin uploading: {Plugin.ModConfig.SkinUploading}",
            RuleName.SkinDownloading => $"Skin downloading: {Plugin.ModConfig.SkinDownloading}",
            _ => "Unknown rule",
        };

    private static string BanPlayer(string playerName, IEnumerable<NetBody> players)
    {
        foreach (var netBody in players)
        {
            if (netBody.name != playerName)
                continue;
            var body = netBody.body.gameObject.GetComponent<ChangeBody>();
            body.isBanned = true;
            body.Unload();
            return $"{playerName} is now skinbanned";
        }
        return $"Player '{playerName}' not found";
    }

    private static string UnbanPlayer(string playerName, IEnumerable<NetBody> players)
    {
        foreach (var netBody in players)
        {
            if (netBody.name != playerName)
                continue;
            netBody.body.gameObject.GetComponent<ChangeBody>().isBanned = false;
            return $"{playerName} is now skinpardoned";
        }
        return $"Player '{playerName}' not found";
    }

    private static string EnableSkins(
        ChangeBody localBody,
        Dictionary<object, ChangeBody> replacers
    )
    {
        ChangeSkinNetworkComponent.SendSkinEnabled();
        foreach (var cb in replacers.Values)
            cb.BeginReplacement();

        return localBody.skinName == null
            ? "ChangeSkin enabled (warning: skin for self not selected)"
            : "ChangeSkin enabled";
    }

    private static string DisableSkins(Dictionary<object, ChangeBody> replacers)
    {
        foreach (var cb in replacers.Values)
            cb.StopReplacement();
        ChangeSkinNetworkComponent.SendSkinDisabled();
        return "ChangeSkin disabled";
    }

    private static string ReloadSkins(Dictionary<object, ChangeBody> replacers)
    {
        foreach (var cb in replacers.Values)
            cb.Reload();
        return "ChangeSkin reloaded";
    }

    private static string UnloadSelf(ChangeBody body)
    {
        body.Unload();
        return "Self skin unloaded";
    }

    private static string ClearCache()
    {
        SkinLoader.ClearCache();
        return "Cache cleared";
    }

    private static string SetVerbose(bool enabled)
    {
        Plugin.ModConfig.Verbose = enabled;
        return $"Verbose logging is now {enabled}";
    }

    private static string ExecuteWithConfig(Action action, string successMessage)
    {
        action();
        return successMessage;
    }
}
