using System;
using System.Collections.Generic;
using Sharp.Shared.Objects;
using Sharp.Shared.Units;

namespace Prefix.Poop.Utils;

/// <summary>
/// Per-player, per-command cooldown gate (in-memory, not persisted). Mirrors the old behaviour:
/// the cooldown is consumed by <see cref="CanExecute"/> when it returns true.
/// </summary>
internal sealed class CommandCooldownTracker(InterfaceBridge bridge, int defaultSeconds)
{
    private readonly Dictionary<string, int>             _perCommand = new();
    private readonly Dictionary<(string, SteamID), double> _last     = new();

    public void SetCommandCooldown(string command, int seconds)
        => _perCommand[command] = seconds;

    private int CooldownFor(string command)
        => _perCommand.TryGetValue(command, out var s) ? s : defaultSeconds;

    /// <summary>True (and records the time) if the command is off cooldown for this player.</summary>
    public bool CanExecute(string command, IGameClient client)
    {
        var now = bridge.ModSharp.EngineTime();
        var key = (command, client.SteamId);
        var cd  = CooldownFor(command);

        if (cd > 0 && _last.TryGetValue(key, out var last) && now - last < cd)
            return false;

        _last[key] = now;
        return true;
    }

    /// <summary>Whole seconds remaining on the player's cooldown for this command.</summary>
    public int GetRemainingCooldown(string command, IGameClient client)
    {
        var now = bridge.ModSharp.EngineTime();
        if (!_last.TryGetValue((command, client.SteamId), out var last))
            return 0;

        var remaining = CooldownFor(command) - (now - last);
        return remaining > 0 ? (int) Math.Ceiling(remaining) : 0;
    }
}
