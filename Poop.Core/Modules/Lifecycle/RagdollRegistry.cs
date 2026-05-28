using System;
using System.Collections.Generic;
using MessagePipe;
using Microsoft.Extensions.Logging;
using Prefix.Poop.Messages;
using Sharp.Shared.GameEntities;
using Sharp.Shared.Listeners;
using Sharp.Shared.Objects;

namespace Prefix.Poop.Modules.Lifecycle;

/// <summary>
/// Tracks ragdoll entities spawned when players die, indexed by IGameClient.
/// Registers itself as an IEntityListener; round/disconnect events come from MessagePipe.
/// </summary>
internal sealed class RagdollRegistry : IModule, IEntityListener
{
    private readonly InterfaceBridge                          _bridge;
    private readonly ILogger<RagdollRegistry>                 _logger;
    private readonly Dictionary<IGameClient, RagdollInfo>     _ragdolls = new();
    private System.IDisposable?                          _subscriptions;

    public int ListenerPriority => 0;
    public int ListenerVersion  => IEntityListener.ApiVersion;

    public IReadOnlyDictionary<IGameClient, RagdollInfo> Ragdolls => _ragdolls;

    public RagdollRegistry(
        InterfaceBridge                      bridge,
        ILogger<RagdollRegistry>             logger,
        ISubscriber<RoundStartMessage>       roundStart,
        ISubscriber<ClientDisconnectMessage> disconnect)
    {
        _bridge = bridge;
        _logger = logger;

        var bag = DisposableBag.CreateBuilder();
        roundStart.Subscribe(_ => _ragdolls.Clear()).AddTo(bag);
        disconnect.Subscribe(m => RemoveByClient(m.Client)).AddTo(bag);
        _subscriptions = bag.Build();
    }

    public bool Init()
    {
        _bridge.EntityManager.InstallEntityListener(this);
        return true;
    }

    public void Shutdown()
    {
        _bridge.EntityManager.RemoveEntityListener(this);
        _subscriptions?.Dispose();
        _subscriptions = null;
        _ragdolls.Clear();
    }

    // ── IEntityListener ─────────────────────────────────────────────────────

    public void OnEntityCreated(IBaseEntity entity)
    {
        if (!entity.IsValid())
            return;

        try
        {
            var name = entity.Name;
            if (string.IsNullOrEmpty(name))
                return;

            if (!name.StartsWith("ragdoll_", StringComparison.OrdinalIgnoreCase))
                return;

            // Ragdoll names: ragdoll_[role]_[slot] e.g. "ragdoll_traitor_12"
            var parts = name.Split('_');
            if (parts.Length < 3)
            {
                _logger.LogDebug("[Poop] Ragdoll '{Name}' doesn't match expected pattern", name);
                return;
            }

            var slotStr = parts[^1];
            if (!int.TryParse(slotStr, out int slotNum))
            {
                _logger.LogDebug("[Poop] Failed to parse slot from ragdoll name '{Name}'", name);
                return;
            }

            // PlayerSlot has no int constructor; resolve by iterating connected clients.
            IGameClient? client = null;
            foreach (var c in _bridge.ClientManager.GetGameClients(inGame: false))
            {
                if (c.Slot.AsPrimitive() == slotNum)
                {
                    client = c;
                    break;
                }
            }

            if (client == null)
            {
                _logger.LogDebug("[Poop] Ragdoll '{Name}': no client at slot {Slot}", name, slotNum);
                return;
            }

            _ragdolls[client] = new RagdollInfo(entity, client);

            _logger.LogDebug("[Poop] Tracked ragdoll '{Name}' → {ClientName} (slot {Slot})",
                name, client.Name, slotNum);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Poop] Error tracking ragdoll entity");
        }
    }

    public void OnEntityDeleted(IBaseEntity entity)
    {
        if (!entity.IsValid())
            return;

        IGameClient? toRemove = null;
        foreach (var kvp in _ragdolls)
        {
            if (kvp.Value.Ragdoll == entity)
            {
                toRemove = kvp.Key;
                break;
            }
        }

        if (toRemove != null)
            _ragdolls.Remove(toRemove);
    }

    public void OnEntitySpawned(IBaseEntity entity) { }

    public void OnEntityFollowed(IBaseEntity entity, IBaseEntity? owner) { }

    // ── helpers ─────────────────────────────────────────────────────────────

    private void RemoveByClient(IGameClient client)
        => _ragdolls.Remove(client);
}
