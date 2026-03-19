using System;
using System.Collections.Generic;
using KrokoshaCasualtiesMP;

namespace ChangeSkin.Network;

/// <summary>
/// Tracks ChangeBody components per player/client.
/// Owns the collections (no more shared static state confusion).
/// </summary>
internal sealed class PlayerRegistry
{
    private readonly List<NetBody> _playerBodies = new();
    private readonly Dictionary<ulong, Skin.ChangeBody> _replacers = new();

    public IReadOnlyList<NetBody> PlayerBodies => _playerBodies;
    public IReadOnlyDictionary<ulong, Skin.ChangeBody> Replacers => _replacers;

    // ── Replacer CRUD ───────────────────────────────────────────────────────

    public bool TryGetReplacer(ulong clientId, out Skin.ChangeBody changeBody)
    {
        bool found = _replacers.TryGetValue(clientId, out changeBody);

        if (Core.Plugin.ModConfig.Verbose)
            Core.Plugin.Logger.LogInfo(
                $"[PlayerRegistry] TryGetReplacer clientId={clientId} found={found}"
            );

        return found;
    }

    public void SetReplacer(ulong clientId, Skin.ChangeBody changeBody)
    {
        if (changeBody == null)
            throw new ArgumentNullException(nameof(changeBody));
        _replacers[clientId] = changeBody;

        if (Core.Plugin.ModConfig.Verbose)
            Core.Plugin.Logger.LogInfo($"[PlayerRegistry] SetReplacer clientId={clientId}");
    }

    public bool RemoveByClientId(ulong clientId)
    {
        bool removed = _replacers.Remove(clientId);

        if (Core.Plugin.ModConfig.Verbose)
            Core.Plugin.Logger.LogInfo(
                $"[PlayerRegistry] RemoveByClientId clientId={clientId} removed={removed}"
            );

        return removed;
    }

    // ── Player body CRUD ────────────────────────────────────────────────────

    public void AddPlayerBody(NetBody netBody)
    {
        if (netBody == null)
            return;
        if (!_playerBodies.Contains(netBody))
            _playerBodies.Add(netBody);

        if (Core.Plugin.ModConfig.Verbose)
            Core.Plugin.Logger.LogInfo(
                $"[PlayerRegistry] AddPlayerBody name={netBody.name} totalBodies={_playerBodies.Count}"
            );
    }

    public bool RemovePlayerBody(NetBody netBody)
    {
        if (netBody == null)
            return false;
        bool removed = _playerBodies.Remove(netBody);

        if (Core.Plugin.ModConfig.Verbose)
            Core.Plugin.Logger.LogInfo(
                $"[PlayerRegistry] RemovePlayerBody name={netBody?.name} removed={removed} totalBodies={_playerBodies.Count}"
            );

        return removed;
    }

    // ── Lookups ─────────────────────────────────────────────────────────────

    public bool TryFindByPlayerName(string playerName, out NetBody netBody)
    {
        for (int i = 0; i < _playerBodies.Count; i++)
        {
            if (_playerBodies[i] != null && _playerBodies[i].name == playerName)
            {
                netBody = _playerBodies[i];
                return true;
            }
        }
        netBody = null;
        return false;
    }

    // ── Lifecycle ───────────────────────────────────────────────────────────

    public void Clear()
    {
        if (Core.Plugin.ModConfig.Verbose)
            Core.Plugin.Logger.LogInfo("[PlayerRegistry] Clear: resetting all collections");

        _playerBodies.Clear();
        _replacers.Clear();
    }
}
