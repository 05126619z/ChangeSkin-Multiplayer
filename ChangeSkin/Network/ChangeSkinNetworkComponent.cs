using ChangeSkin.App;
using ChangeSkin.Core;

namespace ChangeSkin.Network;

/// <summary>
/// Thin static facade that bridges external callers (patches, legacy code)
/// to the OOP network layer via <see cref="ChangeSkinMain.App"/>.
/// Kept for API compatibility — all logic lives in <see cref="NetcodeSkinNetwork"/>.
/// </summary>
public static class ChangeSkinNetworkComponent
{
    internal const string UploadApiUrl = "https://skin.cat-bot.de/ots";

    private static ISkinNetwork Net => ChangeSkinMain.App?.Network;

    // ── Send helpers (delegate to ISkinNetwork) ────────────────────────────

    public static void SendLocalSkinMessage(string skinName)
    {
        if (Core.Plugin.ModConfig.Verbose)
            Core.Plugin.Logger.LogInfo($"[NetworkComponent] SendLocalSkinMessage: skinName={skinName}");

        Net?.SendLocalSkin(skinName);
    }

    public static void SendRemoteSkinMessage(string url, string skinName)
    {
        if (Core.Plugin.ModConfig.Verbose)
            Core.Plugin.Logger.LogInfo($"[NetworkComponent] SendRemoteSkinMessage: skinName={skinName} url={url}");

        Net?.SendRemoteSkin(skinName, url);
    }

    public static void SendSkinEnabled()
    {
        if (Core.Plugin.ModConfig.Verbose)
            Core.Plugin.Logger.LogInfo("[NetworkComponent] SendSkinEnabled");

        Net?.SendEnabled();
    }

    public static void SendSkinDisabled()
    {
        if (Core.Plugin.ModConfig.Verbose)
            Core.Plugin.Logger.LogInfo("[NetworkComponent] SendSkinDisabled");

        Net?.SendDisabled();
    }

    // ── Registration (legacy spelling kept for compatibility) ───────────────

    public static void RegisterServerRecievers()
    {
        if (Core.Plugin.ModConfig.Verbose)
            Core.Plugin.Logger.LogInfo("[NetworkComponent] RegisterServerReceivers");

        Net?.RegisterServerHandlers();
    }

    public static void RegisterClientRecievers()
    {
        if (Core.Plugin.ModConfig.Verbose)
            Core.Plugin.Logger.LogInfo("[NetworkComponent] RegisterClientReceivers");

        Net?.RegisterClientHandlers();
    }
}
