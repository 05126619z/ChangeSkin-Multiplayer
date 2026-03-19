using System;
using System.IO;
using ChangeSkin.Core;
using ChangeSkin.Network;
using ChangeSkin.Skin;

namespace ChangeSkin.App;

/// <summary>
/// Application service / use-case layer for ChangeSkin.
/// Keeps higher-level rules (upload/download toggles, multiplayer sync) out of UI/console entrypoints.
/// </summary>
internal sealed class ChangeSkinService
{
    private readonly PlayerRegistry _registry;
    private readonly ISkinNetwork _network;

    public ChangeSkinService(PlayerRegistry registry, ISkinNetwork network)
    {
        _registry = registry;
        _network = network;
    }

    public void LoadLocalForSelf(string skinName)
    {
        if (Core.Plugin.ModConfig.Verbose)
            Core.Plugin.Logger.LogInfo($"[ChangeSkinService] LoadLocalForSelf: skinName={skinName}");

        ChangeSkinMain.localChangeBody.LoadSkinLocal(skinName);
        Core.Plugin.ModConfig.LastSelectedSkin = skinName;
        Core.Plugin.ModConfig.lastSelected = ModConfig.LastSelected.Local;

        _network?.SendLocalSkin(skinName);
    }

    public string LoadRemoteForSelf(string url)
    {
        if (!Core.Plugin.ModConfig.SkinDownloading)
            return "Skin downloading is disabled by the rules";

        if (Core.Plugin.ModConfig.Verbose)
            Core.Plugin.Logger.LogInfo($"[ChangeSkinService] LoadRemoteForSelf: url={url}");

        Core.Plugin.ModConfig.LastURL = url;
        Core.Plugin.ModConfig.lastSelected = ModConfig.LastSelected.Remote;

        // Load skin locally (async, non-blocking)
        ChangeSkinMain.localChangeBody.LoadSkinURL(url);

        // Immediately notify other clients about the remote skin URL.
        // The archive name is derived from the URL filename.
        string skinName = Path.GetFileNameWithoutExtension(url);
        _network?.SendRemoteSkin(skinName, url);

        return $"Remote skin {url} loading (async)…";
    }

    public void EnableAll()
    {
        if (Core.Plugin.ModConfig.Verbose)
            Core.Plugin.Logger.LogInfo("[ChangeSkinService] EnableAll");

        // If a local skin was loaded but never synced, send it now so other clients can apply it.
        SyncCurrentSkinIfNeeded();

        _network?.SendEnabled();
        foreach (ChangeBody changeBody in ChangeSkinMain.replacers.Values)
            changeBody.BeginReplacement();
    }

    /// <summary>
    /// Sends the current local skin data to the network if it hasn't been synced yet.
    /// This ensures that a plain "skin enable" after "skin load local" works correctly.
    /// </summary>
    private void SyncCurrentSkinIfNeeded()
    {
        var local = ChangeSkinMain.localChangeBody;
        if (local == null)
            return;

        // Nothing loaded — nothing to sync.
        if (local.skinName == null && local.skinURL == null)
            return;

        switch (Core.Plugin.ModConfig.lastSelected)
        {
            case ModConfig.LastSelected.Local when local.skinName != null:
                _network?.SendLocalSkin(local.skinName);
                if (Core.Plugin.ModConfig.Verbose)
                    Core.Plugin.Logger.LogInfo($"[ChangeSkinService] Synced local skin '{local.skinName}' before enable");
                break;

            case ModConfig.LastSelected.Remote when local.skinURL != null:
                // skinName may be null if async load hasn't finished yet — send URL only,
                // the archiveName will be resolved by the receiver.
                string name = local.skinName ?? Path.GetFileNameWithoutExtension(local.skinURL);
                _network?.SendRemoteSkin(name, local.skinURL);
                if (Core.Plugin.ModConfig.Verbose)
                    Core.Plugin.Logger.LogInfo($"[ChangeSkinService] Synced remote skin '{name}' url='{local.skinURL}' before enable");
                break;
        }
    }

    public void DisableAll()
    {
        if (Core.Plugin.ModConfig.Verbose)
            Core.Plugin.Logger.LogInfo("[ChangeSkinService] DisableAll");

        foreach (ChangeBody changeBody in ChangeSkinMain.replacers.Values)
            changeBody.StopReplacement();
        _network?.SendDisabled();
    }

    public void ReloadAll()
    {
        if (Core.Plugin.ModConfig.Verbose)
            Core.Plugin.Logger.LogInfo("[ChangeSkinService] ReloadAll");

        foreach (ChangeBody changeBody in ChangeSkinMain.replacers.Values)
            changeBody.Reload();
    }

    public void UnloadSelf()
    {
        ChangeSkinMain.localChangeBody.Unload();
    }

    public string BanByPlayerName(string playerName)
    {
        if (Core.Plugin.ModConfig.Verbose)
            Core.Plugin.Logger.LogInfo($"[ChangeSkinService] BanByPlayerName: {playerName}");

        if (!_registry.TryFindByPlayerName(playerName, out var netBody) || netBody == null)
            return $"{playerName} not found";

        var changeBody = netBody.body.gameObject.GetComponent<ChangeBody>();
        if (changeBody == null)
            return $"{playerName} not found";

        changeBody.isBanned = true;
        changeBody.Unload();
        return $"{netBody.name} is now skinbanned";
    }

    public string UnbanByPlayerName(string playerName)
    {
        if (Core.Plugin.ModConfig.Verbose)
            Core.Plugin.Logger.LogInfo($"[ChangeSkinService] UnbanByPlayerName: {playerName}");

        if (!_registry.TryFindByPlayerName(playerName, out var netBody) || netBody == null)
            return $"{playerName} not found";

        var changeBody = netBody.body.gameObject.GetComponent<ChangeBody>();
        if (changeBody == null)
            return $"{playerName} not found";

        changeBody.isBanned = false;
        return $"{netBody.name} is now skinpardoned";
    }

    public string SetRule(string key, string value)
    {
        try
        {
            bool parsed = bool.Parse(value);
            if (key == "skinuploading")
            {
                Core.Plugin.ModConfig.SkinUploading = parsed;
                return $"Skin uploading is now {Core.Plugin.ModConfig.SkinUploading}";
            }
            if (key == "skindownloading")
            {
                Core.Plugin.ModConfig.SkinDownloading = parsed;
                return $"Skin downloading is now {Core.Plugin.ModConfig.SkinDownloading}";
            }
            return "Unknown rule";
        }
        catch (Exception e)
        {
            return $"Error: {e}";
        }
    }

    public string GetRule(string key)
    {
        if (key == "skinuploading")
            return $"Skin uploading is set to {Core.Plugin.ModConfig.SkinUploading}";
        if (key == "skindownloading")
            return $"Skin downloading is set to {Core.Plugin.ModConfig.SkinDownloading}";
        return "Unknown rule";
    }

    public string SetVerbose(string value)
    {
        Core.Plugin.ModConfig.Verbose = bool.Parse(value);
        return $"Verbose logging is now {Core.Plugin.ModConfig.Verbose}";
    }

    public string ClearCache()
    {
        SkinLoader.ClearCache();
        return "Cache cleared";
    }
}
