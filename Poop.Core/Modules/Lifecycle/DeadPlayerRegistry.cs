using System.Collections.Generic;
using MessagePipe;
using Microsoft.Extensions.Logging;
using Prefix.Poop.Messages;
using Sharp.Shared.Objects;

namespace Prefix.Poop.Modules.Lifecycle;

/// <summary>
/// Maintains a dictionary of dead player positions updated through MessagePipe subscriptions.
/// Replaces DeadPlayerTracker; no direct event/client listener hooks — those flow via GameEventRelay.
/// </summary>
internal sealed class DeadPlayerRegistry : IModule
{
    private readonly ILogger<DeadPlayerRegistry>                _logger;
    private readonly Dictionary<IGameClient, DeadPlayerInfo>    _deadPlayers = new();
    private System.IDisposable?                            _subscriptions;

    public IReadOnlyDictionary<IGameClient, DeadPlayerInfo> DeadPlayers => _deadPlayers;

    public DeadPlayerRegistry(
        ILogger<DeadPlayerRegistry>           logger,
        ISubscriber<PlayerDeathMessage>       playerDeath,
        ISubscriber<RoundStartMessage>        roundStart,
        ISubscriber<ClientDisconnectMessage>  disconnect)
    {
        _logger = logger;

        var bag = DisposableBag.CreateBuilder();

        playerDeath.Subscribe(m =>
        {
            _deadPlayers[m.Victim] = new DeadPlayerInfo(m.Position, m.Victim);
        }).AddTo(bag);

        roundStart.Subscribe(_ => _deadPlayers.Clear()).AddTo(bag);

        disconnect.Subscribe(m => _deadPlayers.Remove(m.Client)).AddTo(bag);

        _subscriptions = bag.Build();
    }

    public bool Init() => true;

    public void Shutdown()
    {
        _subscriptions?.Dispose();
        _subscriptions = null;
        _deadPlayers.Clear();
    }
}
