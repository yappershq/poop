using Sharp.Shared.Objects;
using Sharp.Shared.Types;

namespace Prefix.Poop.Messages;

// Internal MessagePipe bus. A single relay hooks the raw ModSharp game/client events and
// republishes them as these messages, so modules subscribe instead of each hooking events
// and reaching through a static singleton.

/// <summary>Round just (re)started.</summary>
public sealed record RoundStartMessage;

/// <summary>Round ended.</summary>
public sealed record RoundEndMessage;

/// <summary>A player died — carries the death position for victim detection.</summary>
public sealed record PlayerDeathMessage(IGameClient Victim, Vector Position);

/// <summary>A real client finished connecting (not a bot/HLTV).</summary>
public sealed record ClientPutInServerMessage(IGameClient Client);

/// <summary>A client disconnected.</summary>
public sealed record ClientDisconnectMessage(IGameClient Client);

/// <summary>Published after every poop spawn attempt; the shared-API module re-raises it.</summary>
public sealed record PoopSpawnedMessage(
    IGameClient? Player,
    Vector Position,
    float Size,
    IGameClient? Victim,
    bool Success);
