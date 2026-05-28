using Sharp.Shared.GameEntities;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;

namespace Prefix.Poop.Modules.Lifecycle;

/// <summary>A tracked dead player: where they died + who they were.</summary>
internal sealed record DeadPlayerInfo(Vector Position, IGameClient? Player);

/// <summary>A tracked ragdoll entity + the player it belongs to.</summary>
internal sealed record RagdollInfo(IBaseEntity Ragdoll, IGameClient? Player);
