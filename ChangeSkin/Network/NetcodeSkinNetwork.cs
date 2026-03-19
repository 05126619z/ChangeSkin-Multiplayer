using System;
using ChangeSkin.App;
using ChangeSkin.Core;
using ChangeSkin.Skin;
using KrokoshaCasualtiesMP;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace ChangeSkin.Network;

/// <summary>
/// Netcode-based skin network transport.
/// Handles registration, sending, relaying, and cleanup of all skin-sync messages.
/// Follows a server-authoritative relay pattern: clients send to server, server relays to all.
/// </summary>
internal sealed class NetcodeSkinNetwork : ISkinNetwork
{
    // ── Message names (kept stable for wire compatibility) ──────────────────
    private const string MsgSkinUpdate = "SkinUpdate";
    private const string MsgPlayerSkinRelay = "PlayerSkinRelay";
    private const string MsgSkinStateUpdate = "SkinStateUpdate";
    private const string MsgSkinStateRelay = "SkinStateUpdateRelay";

    // ── Dependencies ────────────────────────────────────────────────────────
    private readonly PlayerRegistry _registry;

    // ── State ───────────────────────────────────────────────────────────────
    private bool _serverRegistered;
    private bool _clientRegistered;
    private bool _disposed;

    public bool IsInitialized => !_disposed;

    public NetcodeSkinNetwork(PlayerRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    // ── Public API ──────────────────────────────────────────────────────────

    public void RegisterServerHandlers()
    {
        if (_disposed || _serverRegistered)
            return;

        var mgr = NetworkManager.Singleton;
        if (mgr == null || !mgr.IsListening)
        {
            Core.Plugin.Logger.LogWarning("[NetcodeSkinNetwork] RegisterServerHandlers: skipped — NetworkManager not available or not listening.");
            return;
        }

        var cm = mgr.CustomMessagingManager;

        cm.RegisterNamedMessageHandler(MsgSkinUpdate, OnSkinUpdateReceived);
        cm.RegisterNamedMessageHandler(MsgSkinStateUpdate, OnSkinStateReceived);

        _serverRegistered = true;
        if (Core.Plugin.ModConfig.Verbose)
            Core.Plugin.Logger.LogInfo("[NetcodeSkinNetwork] Server handlers registered.");
    }

    public void RegisterClientHandlers()
    {
        if (_disposed || _clientRegistered)
            return;

        var mgr = NetworkManager.Singleton;
        if (mgr == null || !mgr.IsListening)
        {
            if (Core.Plugin.ModConfig.Verbose)
                Core.Plugin.Logger.LogWarning("[NetcodeSkinNetwork] RegisterClientHandlers: skipped — NetworkManager not available or not listening.");
            return;
        }

        var cm = mgr.CustomMessagingManager;

        cm.RegisterNamedMessageHandler(MsgPlayerSkinRelay, OnSkinRelayReceived);
        cm.RegisterNamedMessageHandler(MsgSkinStateRelay, OnSkinStateRelayReceived);

        _clientRegistered = true;
        if (Core.Plugin.ModConfig.Verbose)
            Core.Plugin.Logger.LogInfo("[NetcodeSkinNetwork] Client handlers registered.");
    }

    public void UnregisterHandlers()
    {
        if (_disposed)
            return;

        var mgr = NetworkManager.Singleton;
        if (mgr?.CustomMessagingManager == null)
            return;

        var cm = mgr.CustomMessagingManager;

        if (_serverRegistered)
        {
            cm.UnregisterNamedMessageHandler(MsgSkinUpdate);
            cm.UnregisterNamedMessageHandler(MsgSkinStateUpdate);
            _serverRegistered = false;
        }

        if (_clientRegistered)
        {
            cm.UnregisterNamedMessageHandler(MsgPlayerSkinRelay);
            cm.UnregisterNamedMessageHandler(MsgSkinStateRelay);
            _clientRegistered = false;
        }
        if (Core.Plugin.ModConfig.Verbose)
            Core.Plugin.Logger.LogInfo("[NetcodeSkinNetwork] All handlers unregistered.");
    }

    public void SendLocalSkin(string skinName)
    {
        if (!CanSend())
            return;

        string localSkinUrl = ChangeBody.UploadLocalSkin(
            skinName,
            ChangeSkinNetworkComponent.UploadApiUrl
        );
        if (localSkinUrl == null)
        {
            if (Core.Plugin.ModConfig.Verbose)
                Core.Plugin.Logger.LogWarning($"[NetcodeSkinNetwork] SendLocalSkin: upload failed for skin '{skinName}'.");
            return;
        }
        if (Core.Plugin.ModConfig.Verbose)
            Core.Plugin.Logger.LogInfo($"[NetcodeSkinNetwork] SendLocalSkin: skinName={skinName} url={localSkinUrl}");
        SendSkinUpdateToServer(skinName, localSkinUrl);
    }

    public void SendRemoteSkin(string skinName, string url)
    {
        if (!CanSend())
            return;
        if (Core.Plugin.ModConfig.Verbose)
            Core.Plugin.Logger.LogInfo($"[NetcodeSkinNetwork] SendRemoteSkin: skinName={skinName} url={url}");
        SendSkinUpdateToServer(skinName, url);
    }

    public void SendEnabled()
    {
        if (!CanSend())
            return;
        if (Core.Plugin.ModConfig.Verbose)
            Core.Plugin.Logger.LogInfo("[NetcodeSkinNetwork] SendEnabled");
        SendSkinStateToServer(true);
    }

    public void SendDisabled()
    {
        if (!CanSend())
            return;
        if (Core.Plugin.ModConfig.Verbose)
            Core.Plugin.Logger.LogInfo("[NetcodeSkinNetwork] SendDisabled");
        SendSkinStateToServer(false);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        UnregisterHandlers();
        _disposed = true;
        if (Core.Plugin.ModConfig.Verbose)
            Core.Plugin.Logger.LogInfo("[NetcodeSkinNetwork] Disposed.");
    }

    // ── Server-side receivers ───────────────────────────────────────────────

    /// <summary>
    /// Client → Server: a player wants to sync their skin.
    /// Server applies it locally (for host visibility) and relays to all OTHER clients.
    /// </summary>
    private void OnSkinUpdateReceived(ulong senderClientId, FastBufferReader reader)
    {
        if (!EnsureAppReady())
        {
            if (Core.Plugin.ModConfig.Verbose)
                Core.Plugin.Logger.LogWarning("[NetcodeSkinNetwork] OnSkinUpdateReceived: app not ready.");
            return;
        }

        if (!TryGetValidReplacer(senderClientId, out var changeBody))
        {
            if (Core.Plugin.ModConfig.Verbose)
                Core.Plugin.Logger.LogWarning($"[NetcodeSkinNetwork] OnSkinUpdateReceived: no valid replacer for client {senderClientId}.");
            return;
        }

        reader.ReadValueSafe(out string skinName);
        reader.ReadValueSafe(out string url);

        // Apply on the host so the host sees this player's skin.
        if (senderClientId != NetworkManager.ServerClientId)
        {
            ApplySkinAsync(changeBody, url, senderClientId, "SkinUpdate");
        }

        // Relay to every client EXCEPT the sender (they already have it).
        RelaySkinUpdate(senderClientId, skinName, url);
        if (Core.Plugin.ModConfig.Verbose)
            Core.Plugin.Logger.LogInfo($"[SkinUpdate] skinName={skinName} url={url} from client {senderClientId}");
    }

    /// <summary>
    /// Client → Server: a player toggled their skin on/off.
    /// Server applies locally and relays to all OTHER clients.
    /// </summary>
    private void OnSkinStateReceived(ulong senderClientId, FastBufferReader reader)
    {
        if (!EnsureAppReady())
        {
            if (Core.Plugin.ModConfig.Verbose)
                Core.Plugin.Logger.LogWarning("[NetcodeSkinNetwork] OnSkinStateReceived: app not ready.");
            return;
        }

        if (!TryGetValidReplacer(senderClientId, out var changeBody))
        {
            if (Core.Plugin.ModConfig.Verbose)
                Core.Plugin.Logger.LogWarning($"[NetcodeSkinNetwork] OnSkinStateReceived: no valid replacer for client {senderClientId}.");
            return;
        }

        reader.ReadValueSafe(out bool enabled);

        if (senderClientId != NetworkManager.ServerClientId)
        {
            if (enabled)
                changeBody.BeginReplacement();
            else
                changeBody.StopReplacement();
        }

        RelaySkinState(senderClientId, enabled);
        if (Core.Plugin.ModConfig.Verbose)
            Core.Plugin.Logger.LogInfo($"[SkinState] enabled={enabled} from client {senderClientId}");
    }

    // ── Client-side receivers ───────────────────────────────────────────────

    /// <summary>
    /// Server → Client: relay of another player's skin.
    /// Only accepted from the server to prevent spoofing.
    /// </summary>
    private void OnSkinRelayReceived(ulong senderClientId, FastBufferReader reader)
    {
        // Security: only accept relays from the server.
        if (senderClientId != NetworkManager.ServerClientId)
        {
            if (Core.Plugin.ModConfig.Verbose)
                Core.Plugin.Logger.LogWarning($"[SkinRelay] Rejected relay from non-server client {senderClientId}.");
            return;
        }

        if (!EnsureAppReady())
        {
            if (Core.Plugin.ModConfig.Verbose)
                Core.Plugin.Logger.LogWarning("[NetcodeSkinNetwork] OnSkinRelayReceived: app not ready.");
            return;
        }

        reader.ReadValueSafe(out ulong targetClientId);
        reader.ReadValueSafe(out string skinName);
        reader.ReadValueSafe(out string url);

        // Don't apply our own skin via relay (we already have it).
        if (IsLocalClientId(targetClientId))
            return;

        if (!TryGetValidReplacer(targetClientId, out var changeBody))
        {
            if (Core.Plugin.ModConfig.Verbose)
                Core.Plugin.Logger.LogWarning($"[SkinRelay] No valid replacer for target client {targetClientId}.");
            return;
        }

        ApplySkinAsync(changeBody, url, targetClientId, "SkinRelay");
        if (Core.Plugin.ModConfig.Verbose)
            Core.Plugin.Logger.LogInfo($"[SkinRelay] skinName={skinName} url={url} for client {targetClientId}");
    }

    /// <summary>
    /// Server → Client: relay of another player's skin state toggle.
    /// Only accepted from the server to prevent spoofing.
    /// </summary>
    private void OnSkinStateRelayReceived(ulong senderClientId, FastBufferReader reader)
    {
        if (!EnsureAppReady())
        {
            if (Core.Plugin.ModConfig.Verbose)
                Core.Plugin.Logger.LogWarning("[NetcodeSkinNetwork] OnSkinStateRelayReceived: app not ready.");
            return;
        }

        reader.ReadValueSafe(out ulong targetClientId);
        reader.ReadValueSafe(out bool enabled);

        // Don't apply our own state via relay.
        if (IsLocalClientId(targetClientId))
            return;

        if (!TryGetValidReplacer(targetClientId, out var changeBody))
        {
            if (Core.Plugin.ModConfig.Verbose)
                Core.Plugin.Logger.LogWarning($"[SkinStateRelay] No valid replacer for target client {targetClientId}.");
            return;
        }

        if (enabled)
            changeBody.BeginReplacement();
        else
            changeBody.StopReplacement();
        if (Core.Plugin.ModConfig.Verbose)
            Core.Plugin.Logger.LogInfo($"[SkinStateRelay] targetClient={targetClientId} enabled={enabled}");
    }

    // ── Private helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Applies a skin from URL to a ChangeBody, using async download when available.
    /// Falls back to synchronous loading if async is not supported.
    /// </summary>
    private static void ApplySkinAsync(ChangeBody changeBody, string url, ulong clientId, string context)
    {
        if (changeBody == null || string.IsNullOrEmpty(url))
            return;

        _ = ApplySkinInternalAsync(changeBody, url, clientId, context);
    }

    private static async System.Threading.Tasks.Task ApplySkinInternalAsync(
        ChangeBody changeBody,
        string url,
        ulong clientId,
        string context
    )
    {
        try
        {
            bool ok = await changeBody.LoadSkinURLAsync(url);
            if (Core.Plugin.ModConfig.Verbose)
            {
                if (ok)
                    Core.Plugin.Logger.LogInfo($"[{context}] Async skin applied for client {clientId}: {url}");
                else
                    Core.Plugin.Logger.LogWarning($"[{context}] Async skin load returned false for client {clientId}: {url}");
            }
        }
        catch (Exception ex)
        {
            Core.Plugin.Logger.LogError($"[{context}] Async skin load failed for client {clientId}: {ex.Message}");
        }
    }

    private bool CanSend()
    {
        if (_disposed)
        {
            if (Core.Plugin.ModConfig.Verbose)
                Core.Plugin.Logger.LogInfo("[NetcodeSkinNetwork] CanSend: blocked — disposed");
            return false;
        }

        if (!KrokoshaScavMultiplayer.network_system_is_running)
        {
            if (Core.Plugin.ModConfig.Verbose)
                Core.Plugin.Logger.LogInfo(
                    "[NetcodeSkinNetwork] CanSend: blocked — network not running"
                );
            return false;
        }

        var mgr = NetworkManager.Singleton;
        if (mgr == null || !mgr.IsListening)
        {
            if (Core.Plugin.ModConfig.Verbose)
                Core.Plugin.Logger.LogInfo(
                    "[NetcodeSkinNetwork] CanSend: blocked — NetworkManager not listening"
                );
            return false;
        }

        return true;
    }

    private static bool EnsureAppReady()
    {
        return ChangeSkinMain.initialized && ChangeSkinMain.App != null;
    }

    private bool TryGetValidReplacer(ulong clientId, out ChangeBody changeBody)
    {
        changeBody = null;

        if (!_registry.TryGetReplacer(clientId, out changeBody) || changeBody == null)
            return false;

        if (changeBody.isBanned)
        {
            changeBody = null;
            return false;
        }

        return true;
    }

    private static bool IsLocalClientId(ulong clientId)
    {
        try
        {
            return ChangeSkinMain.localPlayerBody != null
                && clientId == ChangeSkinMain.localPlayerBody.player.clientId;
        }
        catch
        {
            return false;
        }
    }

    private void SendSkinUpdateToServer(string skinName, string url)
    {
        if (Core.Plugin.ModConfig.Verbose)
            Core.Plugin.Logger.LogInfo(
                $"[NetcodeSkinNetwork] SendSkinUpdateToServer: skinName={skinName} url={url}"
            );

        using var writer = new FastBufferWriter(256, Allocator.Temp, 1200);
        writer.WriteValueSafe(skinName);
        writer.WriteValueSafe(url);

        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(
            MsgSkinUpdate,
            NetworkManager.ServerClientId,
            writer,
            NetworkDelivery.Reliable
        );
    }

    private void SendSkinStateToServer(bool enabled)
    {
        if (Core.Plugin.ModConfig.Verbose)
            Core.Plugin.Logger.LogInfo($"[NetcodeSkinNetwork] SendSkinStateToServer: enabled={enabled}");

        using var writer = new FastBufferWriter(8, Allocator.Temp, 1200);
        writer.WriteValueSafe(enabled);

        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(
            MsgSkinStateUpdate,
            NetworkManager.ServerClientId,
            writer,
            NetworkDelivery.Reliable
        );
    }

    /// <summary>
    /// Relay a skin update from the server to all connected clients.
    /// The sender already has the skin, so we skip them.
    /// </summary>
    private void RelaySkinUpdate(ulong senderClientId, string skinName, string url)
    {
        var mgr = NetworkManager.Singleton;
        if (mgr == null)
            return;

        using var writer = new FastBufferWriter(256, Allocator.Temp, 1200);
        writer.WriteValueSafe(senderClientId);
        writer.WriteValueSafe(skinName);
        writer.WriteValueSafe(url);

        int relayed = 0;
        foreach (ulong clientId in mgr.ConnectedClientsIds)
        {
            if (clientId == senderClientId)
                continue;

            mgr.CustomMessagingManager.SendNamedMessage(
                MsgPlayerSkinRelay,
                clientId,
                writer,
                NetworkDelivery.Reliable
            );
            relayed++;
        }
        if (Core.Plugin.ModConfig.Verbose)
            Core.Plugin.Logger.LogInfo($"[RelaySkinUpdate] Relayed skin '{skinName}' from {senderClientId} to {relayed} client(s).");
    }

    /// <summary>
    /// Relay a skin state toggle from the server to all connected clients.
    /// </summary>
    private void RelaySkinState(ulong senderClientId, bool enabled)
    {
        var mgr = NetworkManager.Singleton;
        if (mgr == null)
            return;

        using var writer = new FastBufferWriter(32, Allocator.Temp, 1200);
        writer.WriteValueSafe(senderClientId);
        writer.WriteValueSafe(enabled);

        int relayed = 0;
        foreach (ulong clientId in mgr.ConnectedClientsIds)
        {
            if (clientId == senderClientId)
                continue;

            mgr.CustomMessagingManager.SendNamedMessage(
                MsgSkinStateRelay,
                clientId,
                writer,
                NetworkDelivery.Reliable
            );
            relayed++;
        }
        if (Core.Plugin.ModConfig.Verbose)
            Core.Plugin.Logger.LogInfo($"[RelaySkinState] Relayed state enabled={enabled} from {senderClientId} to {relayed} client(s).");
    }
}
