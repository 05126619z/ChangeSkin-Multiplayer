using System;
using System.Collections.Generic;
using ChangeSkin.Core;
using ChangeSkin.Network;
using ChangeSkin.Skin;
using ChangeSkin.Storage;
using KrokoshaCasualtiesMP;
using Unity.Netcode;
using UnityEngine.SceneManagement;

namespace ChangeSkin.App;

public static class ChangeSkinMain
{
    public static NetBody localPlayerBody;
    public static Body localBody;
    public static ChangeBody localChangeBody;

    // Legacy static collections — still referenced by patches and external code.
    // The PlayerRegistry wraps these for OOP access.
    public static List<NetBody> playerBodies = [];
    public static Dictionary<ulong, ChangeBody> replacers = [];

    public static bool initialized = false;

    // Composition root
    internal static ChangeSkinApp App { get; private set; }

    private static bool _sceneHooked;

    public static void Init()
    {
        if (initialized)
            return;

        if (Core.Plugin.ModConfig.Verbose)
            Core.Plugin.Logger.LogInfo("[ChangeSkinMain] Init() called");

        // ── Build services ──────────────────────────────────────────────────
        var registry = new PlayerRegistry();
        ISkinNetwork network = null;

        if (KrokoshaScavMultiplayer.network_system_is_running)
        {
            network = new NetcodeSkinNetwork(registry);

            if (Core.Plugin.ModConfig.Verbose)
                Core.Plugin.Logger.LogInfo(
                    "[ChangeSkinMain] Multiplayer mode — NetcodeSkinNetwork created"
                );
        }

        // Skin loader service
        var skinFs = new SystemSkinFileSystem();
        var skinPaths = new DefaultSkinPaths();
        ISkinLoader loader = new SkinLoaderService(
            skinPaths,
            skinFs,
            new SystemZipArchive(),
            new HttpClientDownloader(),
            new UtilsSpriteLoader(),
            new PluginSkinLog()
        );
        SkinLoader.Use(loader);
        SkinLoader.UseFileSystem(skinFs, skinPaths);

        var service = new ChangeSkinService(registry, network);
        var commandHandler = new SkinCommandHandler(service);
        App = new ChangeSkinApp(registry, network, loader, service, commandHandler);

        // ── Single-player path ──────────────────────────────────────────────
        if (!KrokoshaScavMultiplayer.network_system_is_running)
        {
            localBody = PlayerCamera.main.body;
            localChangeBody = localBody.gameObject.AddComponent<ChangeBody>();
            replacers[0] = localChangeBody;
            localChangeBody.isLocalChangeBody = true;

            if (Core.Plugin.ModConfig.Verbose)
                Core.Plugin.Logger.LogInfo(
                    "[ChangeSkinMain] Single-player mode — local ChangeBody attached"
                );
        }
        // ── Multiplayer path ────────────────────────────────────────────────
        else
        {
            // Register network handlers every time (they are cleaned up in Destructor).
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                network.RegisterServerHandlers();
                network.RegisterClientHandlers();

                if (Core.Plugin.ModConfig.Verbose)
                    Core.Plugin.Logger.LogInfo(
                        "[ChangeSkinMain] Network handlers registered for current session"
                    );
            }

            if (Core.Plugin.ModConfig.Verbose)
                Core.Plugin.Logger.LogInfo(
                    $"[ChangeSkinMain] Registering {NetPlayer.ClientIdToPlayerDict.Count} existing players"
                );

            RegisterPlayer(NetPlayer.LOCAL_PLAYER);
        }

        // ── Scene lifecycle hook ────────────────────────────────────────────
        if (!_sceneHooked)
        {
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            _sceneHooked = true;
        }

        TextureStorage.SaveOGSprites();

        // Restore last selected skin locally (no auto-sync).
        switch (Core.Plugin.ModConfig.lastSelected)
        {
            case ModConfig.LastSelected.Local:
            {
                if (Core.Plugin.ModConfig.LastSelectedSkin != null)
                    SkinSelectLocal(localChangeBody, Core.Plugin.ModConfig.LastSelectedSkin);
                break;
            }
            case ModConfig.LastSelected.Remote:
            {
                if (Core.Plugin.ModConfig.LastURL != null)
                    SkinSelectRemote(localChangeBody, Core.Plugin.ModConfig.LastURL);
                break;
            }
        }

        initialized = true;

        if (Core.Plugin.ModConfig.Verbose)
            Core.Plugin.Logger.LogInfo("[ChangeSkinMain] Init complete — initialized=true");

        ConsoleScript.instance.LogToConsole("ChangeSkin initialized");
    }

    /// <summary>
    /// Register a single player: add to bodies list, create/attach ChangeBody, track in registry.
    /// </summary>
    internal static void RegisterPlayer(NetPlayer scavClientInstance)
    {
        if (scavClientInstance == null)
            return;

        ulong clientId = scavClientInstance.clientId;

        // Already registered?
        if (replacers.ContainsKey(clientId))
        {
            if (Core.Plugin.ModConfig.Verbose)
                Core.Plugin.Logger.LogInfo(
                    $"[ChangeSkinMain] RegisterPlayer: clientId={clientId} already registered, skipping"
                );
            return;
        }

        // Guard: body may not be initialized yet (race condition during spawn).
        if (scavClientInstance.body == null)
        {
            if (Core.Plugin.ModConfig.Verbose)
                Core.Plugin.Logger.LogWarning(
                    $"[ChangeSkinMain] RegisterPlayer: clientId={clientId} body is null, skipping"
                );
            return;
        }

        var netBody = scavClientInstance.playerbody;
        if (netBody != null)
            playerBodies.Add(netBody);

        var changeBody = scavClientInstance.body.gameObject.GetComponent<ChangeBody>();
        if (changeBody == null)
            changeBody = scavClientInstance.body.gameObject.AddComponent<ChangeBody>();

        replacers[clientId] = changeBody;

        // Also register in the PlayerRegistry so the network layer can find players.
        App?.Registry?.SetReplacer(clientId, changeBody);
        if (netBody != null)
            App?.Registry?.AddPlayerBody(netBody);

        if (scavClientInstance == NetPlayer.LOCAL_PLAYER)
        {
            changeBody.isLocalChangeBody = true;
            localChangeBody = changeBody;
            localPlayerBody = netBody;
            localBody = scavClientInstance.body;

            if (Core.Plugin.ModConfig.Verbose)
                Core.Plugin.Logger.LogInfo(
                    $"[ChangeSkinMain] RegisterPlayer: clientId={clientId} is LOCAL PLAYER"
                );
        }
        else if (Core.Plugin.ModConfig.Verbose)
        {
            Core.Plugin.Logger.LogInfo(
                $"[ChangeSkinMain] RegisterPlayer: clientId={clientId} registered as remote player"
            );
        }
    }

    /// <summary>
    /// Unregister a player: remove from bodies list and registry, stop replacement.
    /// </summary>
    internal static void UnregisterPlayer(ulong clientId)
    {
        if (Core.Plugin.ModConfig.Verbose)
            Core.Plugin.Logger.LogInfo($"[ChangeSkinMain] UnregisterPlayer: clientId={clientId}");

        if (replacers.TryGetValue(clientId, out var changeBody))
        {
            if (changeBody != null)
                changeBody.StopReplacement();
            replacers.Remove(clientId);
        }

        // Also remove from the PlayerRegistry so the network layer stays in sync.
        App?.Registry?.RemoveByClientId(clientId);

        // Find and remove the matching NetBody
        for (int i = playerBodies.Count - 1; i >= 0; i--)
        {
            if (playerBodies[i] != null && playerBodies[i].player.clientId == clientId)
            {
                App?.Registry?.RemovePlayerBody(playerBodies[i]);
                playerBodies.RemoveAt(i);
                break;
            }
        }
    }

    private static void OnSceneUnloaded(Scene scene)
    {
        if (Core.Plugin.ModConfig.Verbose)
            Core.Plugin.Logger.LogInfo(
                $"[ChangeSkinMain] Scene unloaded: {scene.name} — calling Destructor()"
            );

        Destructor();
    }

    public static void Destructor()
    {
        if (Core.Plugin.ModConfig.Verbose)
            Core.Plugin.Logger.LogInfo("[ChangeSkinMain] Destructor() called");

        initialized = false;

        if (_sceneHooked)
        {
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            _sceneHooked = false;
        }

        // Stop all active replacements
        try
        {
            foreach (ChangeBody changeBody in replacers.Values)
            {
                if (changeBody != null)
                    changeBody.StopReplacement();
            }
        }
        catch { }

        // Dispose network (unregisters handlers)
        try
        {
            App?.Network?.Dispose();
        }
        catch { }

        App = null;

        replacers.Clear();
        playerBodies.Clear();
        localPlayerBody = null;
        localBody = null;
        localChangeBody = null;
        TextureStorage.OgSprites = null;
    }

    public static void SkinSelectLocal(ChangeBody changeBody, string skinName)
    {
        changeBody.LoadSkinLocal(skinName);
    }

    public static void SkinSelectRemote(ChangeBody changeBody, string url)
    {
        changeBody.LoadSkinURL(url);
    }

    public static string ToggleReplacement(string[] args)
    {
        if (!initialized)
            Init();

        string output = App.CommandHandler.Execute(args);
        Core.Plugin.Logger.LogInfo(output);
        return output;
    }
}
