// ReSharper disable UnusedMember.Global

using Sharp.Shared.Objects;
using Sharp.Shared.Types;

namespace Prefix.Poop.Shared.Events;

/// <summary>
/// Event arguments for when a player executes a poop command.
/// Fires BEFORE any validation (cooldown, alive check, etc.); set <see cref="Cancel"/> to block it.
/// </summary>
public sealed class PoopCommandEventArgs
{
    /// <summary>The player who executed the command.</summary>
    public IGameClient Player { get; set; } = null!;

    /// <summary>The command executed (e.g. "poop", "poopcolor", "toppoopers", "toppoop").</summary>
    public string CommandName { get; set; } = string.Empty;

    /// <summary>Set to true to block the command from executing.</summary>
    public bool Cancel { get; set; }
}

/// <summary>
/// Event arguments fired after a poop spawn attempt (success or failure).
/// </summary>
public sealed class PoopSpawnedEventArgs
{
    /// <summary>The player who triggered the spawn (null for pure API spawns).</summary>
    public IGameClient Player { get; set; } = null!;

    /// <summary>Position where the poop was spawned.</summary>
    public Vector Position { get; set; }

    /// <summary>Size of the spawned poop.</summary>
    public float Size { get; set; }

    /// <summary>The victim (dead player) the poop landed on, if any.</summary>
    public IGameClient Victim { get; set; } = null!;

    /// <summary>True if a command triggered the spawn; false if it was programmatic.</summary>
    public bool IsCommandTriggered { get; set; }

    /// <summary>Whether the spawn succeeded.</summary>
    public bool Success { get; set; }
}
