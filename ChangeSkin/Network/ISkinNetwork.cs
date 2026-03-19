using System;

namespace ChangeSkin.Network;

/// <summary>
/// Defines the public contract for the skin networking layer.
/// Implementations handle message registration, sending, and cleanup.
/// </summary>
internal interface ISkinNetwork : IDisposable
{
    /// <summary>Whether the network layer has been initialized and is ready to send/receive.</summary>
    bool IsInitialized { get; }

    /// <summary>Register all server-side message handlers. Must be called by the host.</summary>
    void RegisterServerHandlers();

    /// <summary>Register all client-side message handlers. Must be called by every client.</summary>
    void RegisterClientHandlers();

    /// <summary>Unregister all message handlers. Safe to call multiple times.</summary>
    void UnregisterHandlers();

    /// <summary>Send a local skin (uploads and broadcasts skin name + URL).</summary>
    void SendLocalSkin(string skinName);

    /// <summary>Send a remote skin (broadcasts skin name + URL without upload).</summary>
    void SendRemoteSkin(string skinName, string url);

    /// <summary>Broadcast that the local player has enabled their skin.</summary>
    void SendEnabled();

    /// <summary>Broadcast that the local player has disabled their skin.</summary>
    void SendDisabled();
}
