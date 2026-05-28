using System;
using MessagePipe;
using Microsoft.Extensions.Logging;
using Prefix.Poop.Messages;
using Sharp.Shared.Enums;
using Sharp.Shared.GameEvents;
using Sharp.Shared.Listeners;
using Sharp.Shared.Objects;

namespace Prefix.Poop.Modules;

/// <summary>
/// Single point of contact with ModSharp game/client events.
/// Hooks raw events and republishes them as typed MessagePipe messages so
/// other modules subscribe instead of installing listeners themselves.
/// </summary>
internal sealed class GameEventRelay : IModule, IEventListener, IClientListener
{
    private readonly InterfaceBridge                       _bridge;
    private readonly ILogger<GameEventRelay>               _logger;
    private readonly IPublisher<RoundStartMessage>         _roundStart;
    private readonly IPublisher<RoundEndMessage>           _roundEnd;
    private readonly IPublisher<PlayerDeathMessage>        _playerDeath;
    private readonly IPublisher<ClientPutInServerMessage>  _putInServer;
    private readonly IPublisher<ClientDisconnectMessage>   _disconnect;

    public int ListenerPriority => 0;
    public int ListenerVersion  => IEventListener.ApiVersion;

    public GameEventRelay(
        InterfaceBridge                       bridge,
        ILogger<GameEventRelay>               logger,
        IPublisher<RoundStartMessage>         roundStart,
        IPublisher<RoundEndMessage>           roundEnd,
        IPublisher<PlayerDeathMessage>        playerDeath,
        IPublisher<ClientPutInServerMessage>  putInServer,
        IPublisher<ClientDisconnectMessage>   disconnect)
    {
        _bridge      = bridge;
        _logger      = logger;
        _roundStart  = roundStart;
        _roundEnd    = roundEnd;
        _playerDeath = playerDeath;
        _putInServer = putInServer;
        _disconnect  = disconnect;
    }

    public bool Init()
    {
        _bridge.EventManager.InstallEventListener(this);
        _bridge.ClientManager.InstallClientListener(this);
        return true;
    }

    public void Shutdown()
    {
        _bridge.EventManager.RemoveEventListener(this);
        _bridge.ClientManager.RemoveClientListener(this);
    }

    // ── IEventListener ──────────────────────────────────────────────────────

    public void FireGameEvent(IGameEvent @event)
    {
        switch (@event.Name)
        {
            case "round_start":
                _roundStart.Publish(new RoundStartMessage());
                break;

            case "round_end":
                _roundEnd.Publish(new RoundEndMessage());
                break;

            case "player_death":
                HandlePlayerDeath(@event);
                break;
        }
    }

    private void HandlePlayerDeath(IGameEvent @event)
    {
        if (@event is not IEventPlayerDeath e)
            return;

        var victimController = e.VictimController;
        if (victimController == null || !victimController.IsValid())
            return;

        var victimClient = victimController.GetGameClient();
        if (victimClient == null)
            return;

        try
        {
            var pawn = victimController.GetPlayerPawn();
            if (pawn == null || !pawn.IsValid())
                return;

            var position = pawn.GetAbsOrigin();
            _playerDeath.Publish(new PlayerDeathMessage(victimClient, position));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Poop] Error in player_death relay for {Name}", victimClient.Name);
        }
    }

    // ── IClientListener ─────────────────────────────────────────────────────

    public void OnClientPutInServer(IGameClient client)
    {
        if (client.IsFakeClient || client.IsHltv)
            return;

        _putInServer.Publish(new ClientPutInServerMessage(client));
    }

    public void OnClientDisconnected(IGameClient client, NetworkDisconnectionReason reason)
        => _disconnect.Publish(new ClientDisconnectMessage(client));

    // Unused IClientListener members — satisfy interface with no-ops
    public void OnAdminCacheReload()                                                          { }
    public void OnClientConnected(IGameClient client)                                         { }
    public void OnClientDisconnecting(IGameClient client, NetworkDisconnectionReason reason) { }
    public void OnClientPostAdminCheck(IGameClient client)                                    { }
    public void OnClientSettingChanged(IGameClient client)                                    { }
}
