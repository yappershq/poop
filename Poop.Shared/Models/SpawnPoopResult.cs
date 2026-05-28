using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using Sharp.Shared.GameEntities;

namespace Prefix.Poop.Shared.Models;

/// <summary>
/// Result of spawning a poop entity.
/// </summary>
public sealed class SpawnPoopResult
{
    /// <summary>The spawned poop entity (null if spawn failed).</summary>
    public IBaseEntity? Entity { get; set; }

    /// <summary>The actual size of the spawned poop.</summary>
    public float? Size { get; set; }

    /// <summary>The position where the poop was spawned.</summary>
    public Vector? Position { get; set; }

    /// <summary>The victim (dead player) the poop was spawned on, if any.</summary>
    public IGameClient? Victim { get; set; }

    /// <summary>Whether the spawn was successful.</summary>
    public bool Success => Entity != null && Entity.IsValid();
}
