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
        // No multiplayer lobby -> KrokoshaMP NetBody/NetPlayer infrastructure is absent.
        // Use the single-player fallback instead.
        //
        // NOTE: The MP path is deliberately in a separate method (AfterConnectionMP).
        // Mono JIT-resolves ALL method references when compiling a method, even those
        // behind runtime branches that never execute. If AfterConnection contained
        // references to KrokoshaMP types (NetBody.NetIdToNetBody, NetPlayer.LOCAL_PLAYER)
        // that don't match the installed Krokosha version, the JIT would throw
        // MissingMethodException for the ENTIRE method — including the SP guard.
        // Extracting the MP path keeps AfterConnection free of KrokoshaMP-specific
        // references, so it always JIT-compiles. AfterConnectionMP is only compiled
        // when actually called (i.e. in a real MP session).
        Plugin.Logger.LogInfo($"AfterConnection: is_server={Net.is_server} is_client_or_host={Net.is_client_or_host}");
        if (!Net.is_server && !Net.is_client_or_host)
        {
            TrySetupLocalPlayer();
            return;
        }

        AfterConnectionMP();
    }

    private static void AfterConnectionMP()
    {
        // 4.0.1 treats single-player as "host" (Steamworks inits regardless of
        // lobby), so Net.is_server/is_client_or_host are true even with no lobby.
        // The reliable no-MP signal is a null LOCAL_PLAYER: in a real session
        // Krokosha assigns it before OnWorldgenFinish fires.
        bool localPlayerSet = NetPlayer.LOCAL_PLAYER != null;
        Plugin.Logger.LogInfo($"AfterConnectionMP: LOCAL_PLAYER={(localPlayerSet ? "set" : "null")}");
        if (!localPlayerSet)
        {
            TrySetupLocalPlayer();
            return;
        }

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
        // PendingSkin is intentionally preserved: the SP watchdog and MP
        // AfterConnection re-apply it once the next player body exists.
        // (Previously nulled here, which dropped the auto-load last-skin
        //  feature across every scene transition.)
    }

    internal static void OnPlayerLeft(NetPlayer plr)
    {
        NetworkRegistry.RemovePlayer(plr.clientId);
    }

    // ─── Single-player fallback ──────────────────────────────────────────────
    // Without a KrokoshaMP lobby there is no NetBody / NetPlayer.LOCAL_PLAYER,
    // so the MP registration path (AfterConnection + NetBody.Start patch) never
    // builds a LocalPlayerSkinController. The watchdog scans for the player rig
    // by its "experiment*" sprite names and wires up a local-only controller.
    private const int MinPlayerParts = 10; // filters out partial menu displays

    /// <summary>
    /// Persistent coroutine started once from Plugin.Awake. In single-player it
    /// re-binds a LocalPlayerSkinController as soon as the player body exists;
    /// in multiplayer it stays idle (the MP path owns setup).
    /// </summary>
    public static IEnumerator SpWatchdog()
    {
        var wait = new WaitForSeconds(1f);
        while (true)
        {
            // 4.0.1: Steamworks inits in SP too, so is_server/is_client_or_host
            // are true even without a lobby. LOCAL_PLAYER == null is the real
            // "no multiplayer session" signal.
            if (NetPlayer.LOCAL_PLAYER == null)
                TrySetupLocalPlayer();
            yield return wait;
        }
    }

    /// <summary>
    /// Find the local player body and attach a ChangeBody + LocalSkinController.
    /// Idempotent and MP-safe (no-ops when a lobby is active or already set up).
    /// </summary>
    public static void TrySetupLocalPlayer()
    {
        if (NetPlayer.LOCAL_PLAYER != null)
            return; // multiplayer owns player setup
        if (NetworkRegistry.LocalPlayerSkinController != null)
            return; // already wired this scene

        GameObject body = FindLocalPlayerBody();
        if (body == null)
            return;

        ChangeBody changeBody = body.GetComponent<ChangeBody>();
        if (changeBody == null)
        {
            changeBody = body.AddComponent<ChangeBody>();
            changeBody.Init(body);
        }

        LocalSkinController controller = body.GetComponent<LocalSkinController>();
        if (controller == null)
            controller = body.AddComponent<LocalSkinController>();
        NetworkRegistry.LocalPlayerSkinController = controller;

        Log.Info($"Single-player skin controller bound to '{body.name}' (PendingSkin={(PendingSkin != null ? PendingSkin.Name : "null")})");

        if (PendingSkin != null)
            controller.SetSkin(PendingSkin);
    }

    /// <summary>
    /// Locate the player rig by its "experiment*" SpriteRenderers. Returns null
    /// until the in-game player has spawned (so menu/partial displays are
    /// rejected via MinPlayerParts).
    /// </summary>
    static GameObject FindLocalPlayerBody()
    {
        var renderers = UnityEngine.Object.FindObjectsOfType<SpriteRenderer>();
        var matching = new List<SpriteRenderer>();
        foreach (var sr in renderers)
        {
            if (sr.sprite == null) continue;
            if (Array.IndexOf(ChangeBody.names, sr.sprite.name) >= 0)
                matching.Add(sr);
        }
        if (matching.Count < MinPlayerParts)
        {
            if (matching.Count > 0)
                Plugin.Logger.LogInfo($"FindLocalPlayerBody: only {matching.Count} matching sprites (need {MinPlayerParts}), waiting");
            return null;
        }

        // Group by hierarchy root; the player rig is the root holding the most
        // experiment parts (handles a shared container as well as a bare root).
        var groups = new Dictionary<Transform, int>();
        foreach (var sr in matching)
        {
            Transform root = sr.transform.root;
            groups.TryGetValue(root, out int c);
            groups[root] = c + 1;
        }
        Transform best = null;
        int bestCount = 0;
        foreach (var kv in groups)
        {
            if (kv.Value > bestCount)
            {
                best = kv.Key;
                bestCount = kv.Value;
            }
        }
        return best != null ? best.gameObject : null;
    }

    /// <summary>
    /// Ensure a LocalPlayerSkinController exists before applying a skin. Tries
    /// a lazy SP setup when none is present (e.g. command issued mid-load).
    /// </summary>
    static bool EnsureLocalController()
    {
        if (NetworkRegistry.LocalPlayerSkinController != null)
            return true;
        TrySetupLocalPlayer();
        return NetworkRegistry.LocalPlayerSkinController != null;
    }

    public static string LoadLocal(string skinName)
    {
        SkinObject skin = SkinObject.LoadFromLocal(skinName);
        if (skin == null)
            return $"Local skin '{skinName}' not found";
        PendingSkin = skin;
        if (!EnsureLocalController())
            return "No player body found - start or load a game first.";
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
        PendingSkin = skin;
        if (!EnsureLocalController())
            return "No player body found - start or load a game first.";
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
        PendingSkin = skin;
        if (!EnsureLocalController())
            return "No player body found - start or load a game first.";
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
        NetworkRegistryEntry? entry = NetworkRegistry.Get(playerName);
        if (entry == null)
            return $"Player '{playerName}' not found";
        BanList.Ban(entry.PlayerInfo);
        return $"{playerName} banned";
    }

    public static string BanPlayer(uint id)
    {
        NetworkRegistryEntry? entry = NetworkRegistry.Get(id);
        if (entry == null)
            return $"Player id {id} not found";
        PlayerInfo playerInfo = entry.PlayerInfo;
        BanList.Ban(playerInfo);
        return $"{playerInfo.Nickname} banned";
    }

    public static string UnbanPlayer(string playerName)
    {
        NetworkRegistryEntry? entry = NetworkRegistry.Get(playerName);
        if (entry == null)
            return $"Player '{playerName}' not found";
        BanList.Unban(entry.PlayerInfo);
        return $"{entry.PlayerInfo.Nickname} unbanned";
    }

    public static string UnbanPlayer(uint id)
    {
        NetworkRegistryEntry? entry = NetworkRegistry.Get(id);
        if (entry == null)
            return $"Player id {id} not found";
        PlayerInfo playerInfo = entry.PlayerInfo;
        BanList.Unban(playerInfo);
        return $"{playerInfo.Nickname} unbanned";
    }

    public static string EnableSkins()
    {
        ModConfig.Instance.SkinChangingEnabled = true;
        ModConfig.Instance.Save();

        foreach (NetworkRegistryEntry networkRegistryEntry in NetworkRegistry.Players)
        {
            if (networkRegistryEntry.CBody?.Skin != null && !networkRegistryEntry.SkinController.Banned)
                networkRegistryEntry.SkinController.Enable();
        }
        if (NetworkRegistry.LocalPlayerSkinController?.CBody != null
            && NetworkRegistry.LocalPlayerSkinController.CBody.Skin != null)
            NetworkRegistry.LocalPlayerSkinController.CBody.RepStart();

        SkinNetworkHandler.BroadcastSkinAnnouncement(true, "Changing your skin has been enabled!");
        ConsoleScript.instance.LogToConsole("[ChangeSkin] Changing your skin has been enabled!");
        return "ChangeSkin enabled";
    }

    public static string DisableSkins()
    {
        ModConfig.Instance.SkinChangingEnabled = false;
        ModConfig.Instance.Save();

        SkinNetworkHandler.BroadcastSkinAnnouncement(false, "Skin changing has now been disabled.");
        ConsoleScript.instance.LogToConsole("[ChangeSkin] Skin changing has now been disabled.");
        return "ChangeSkin disabled";
    }
}
