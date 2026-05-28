using System;
using System.Collections.Generic;
using System.Linq;
using MessagePipe;
using Microsoft.Extensions.Logging;
using Prefix.Poop.Config;
using Prefix.Poop.Messages;
using Sharp.Shared.Enums;
using Sharp.Shared.GameEntities;

namespace Prefix.Poop.Modules.Lifecycle;

/// <summary>
/// Tracks active poop entities, schedules lifetime-based removal, and enforces
/// the per-round spawn cap. Subscribes to round events via MessagePipe.
/// </summary>
internal sealed class PoopLifecycleManager : IModule
{
    private readonly InterfaceBridge                    _bridge;
    private readonly ILogger<PoopLifecycleManager>      _logger;
    private readonly PoopConfig                         _config;
    private readonly Dictionary<IBaseEntity, Guid>      _poopTimers = new();
    private System.IDisposable?                    _subscriptions;
    private int                                         _poopsThisRound;

    public PoopLifecycleManager(
        InterfaceBridge                  bridge,
        ILogger<PoopLifecycleManager>    logger,
        PoopConfig                       config,
        ISubscriber<RoundStartMessage>   roundStart,
        ISubscriber<RoundEndMessage>     roundEnd)
    {
        _bridge = bridge;
        _logger = logger;
        _config = config;

        var bag = DisposableBag.CreateBuilder();
        roundStart.Subscribe(_ => _poopsThisRound = 0).AddTo(bag);
        roundEnd.Subscribe(_ =>
        {
            if (_config.Gameplay.RemovePoopsOnRoundEnd)
                RemoveAllPoops();
        }).AddTo(bag);
        _subscriptions = bag.Build();
    }

    public bool Init() => true;

    public void Shutdown()
    {
        RemoveAllPoops();
        _subscriptions?.Dispose();
        _subscriptions = null;
    }

    /// <summary>
    /// Records the entity, increments the round counter, and starts a lifetime timer if configured.
    /// </summary>
    public void TrackPoop(IBaseEntity entity)
    {
        if (!entity.IsValid())
        {
            _logger.LogWarning("[Poop] Attempted to track invalid poop entity");
            return;
        }

        _poopsThisRound++;

        if (_config.Gameplay.PoopLifetimeSeconds > 0)
        {
            var timerId = _bridge.ModSharp.PushTimer(() =>
            {
                RemovePoopInternal(entity);
                return TimerAction.Stop;
            }, _config.Gameplay.PoopLifetimeSeconds, GameTimerFlags.StopOnRoundEnd);

            _poopTimers[entity] = timerId;
        }
        else
        {
            _poopTimers[entity] = Guid.Empty;
        }
    }

    /// <summary>Removes a single tracked poop and its timer.</summary>
    public void RemovePoop(IBaseEntity entity) => RemovePoopInternal(entity);

    /// <summary>Kills every tracked poop and clears state.</summary>
    public void RemoveAllPoops()
    {
        foreach (var (entity, timerId) in _poopTimers.ToList())
        {
            if (timerId != Guid.Empty && _bridge.ModSharp.IsValidTimer(timerId))
                _bridge.ModSharp.StopTimer(timerId);

            if (entity.IsValid())
            {
                try { entity.Kill(); }
                catch (Exception ex) { _logger.LogError(ex, "[Poop] Error killing poop entity"); }
            }
        }
        _poopTimers.Clear();
    }

    /// <summary>Returns true when the per-round limit has been reached (0 = unlimited).</summary>
    public bool HasReachedMaxPoopsPerRound()
    {
        if (_config.Gameplay.MaxPoopsPerRound <= 0)
            return false;

        return _poopsThisRound >= _config.Gameplay.MaxPoopsPerRound;
    }

    /// <summary>Number of currently tracked poop entities.</summary>
    public int TrackedPoopCount => _poopTimers.Count;

    // ── internals ────────────────────────────────────────────────────────────

    private void RemovePoopInternal(IBaseEntity? entity)
    {
        if (entity == null || !_poopTimers.TryGetValue(entity, out var timerId))
            return;

        if (timerId != Guid.Empty && _bridge.ModSharp.IsValidTimer(timerId))
            _bridge.ModSharp.StopTimer(timerId);

        if (entity.IsValid())
        {
            try { entity.Kill(); }
            catch (Exception ex) { _logger.LogError(ex, "[Poop] Error killing poop entity"); }
        }

        _poopTimers.Remove(entity);
    }
}
