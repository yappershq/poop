using System;
using System.Threading.Tasks;
using MessagePipe;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Prefix.Poop.Config;
using Prefix.Poop.Database;
using Prefix.Poop.Messages;
using Prefix.Poop.Shared;
using Prefix.Poop.Shared.Events;
using Prefix.Poop.Shared.Models;
using Prefix.Poop.Utils;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using Sharp.Shared.Units;

namespace Prefix.Poop.Modules;

/// <summary>
/// Publishes the public <see cref="IPoopShared"/> API for other plugins and bridges the internal
/// MessagePipe spawn message to the public <see cref="OnPoopSpawned"/> event.
/// </summary>
internal sealed class SharedInterfaceModule : IModule, IPoopShared
{
    private readonly InterfaceBridge               _bridge;
    private readonly PoopConfig                    _config;
    private readonly PoopSpawner                   _spawner;
    private readonly PoopStatsRepository           _stats;
    private readonly ColorStore                    _colors;
    private readonly ISubscriber<PoopSpawnedMessage> _spawnedSub;
    private readonly ILogger<SharedInterfaceModule> _logger;

    private IDisposable? _subscription;

    public SharedInterfaceModule(
        InterfaceBridge                 bridge,
        PoopConfig                      config,
        PoopSpawner                     spawner,
        PoopStatsRepository             stats,
        ColorStore                      colors,
        ISubscriber<PoopSpawnedMessage> spawnedSub,
        ILogger<SharedInterfaceModule>  logger)
    {
        _bridge     = bridge;
        _config     = config;
        _spawner    = spawner;
        _stats      = stats;
        _colors     = colors;
        _spawnedSub = spawnedSub;
        _logger     = logger;
    }

    public event Action<PoopCommandEventArgs>? OnPoopCommand;
    public event Action<PoopSpawnedEventArgs>? OnPoopSpawned;

    public bool Init() => true;

    public void OnPostInit(ServiceProvider provider)
    {
        // Publishers register in PostInit; consumers resolve in OnAllModulesLoaded.
        _bridge.SharpModule.RegisterSharpModuleInterface(_bridge.Plugin, IPoopShared.Identity, (IPoopShared) this);

        _subscription = _spawnedSub.Subscribe(OnSpawnedMessage);

        _logger.LogInformation("[Poop] IPoopShared API registered");
    }

    public void Shutdown()
        => _subscription?.Dispose();

    /// <summary>Called by the command module before validation; returns false to block.</summary>
    public bool FireCommand(IGameClient player, string commandName)
    {
        if (OnPoopCommand is null)
            return true;

        var args = new PoopCommandEventArgs { Player = player, CommandName = commandName };
        OnPoopCommand.Invoke(args);
        return !args.Cancel;
    }

    private void OnSpawnedMessage(PoopSpawnedMessage m)
        => OnPoopSpawned?.Invoke(new PoopSpawnedEventArgs
        {
            Player             = m.Player!,
            Position           = m.Position,
            Size               = m.Size,
            Victim             = m.Victim!,
            IsCommandTriggered = m.Player is not null,
            Success            = m.Success,
        });

    #region IPoopShared — spawn

    public SpawnPoopResult SpawnPoop(
        IGameClient? player,
        Vector position,
        float size = -1.0f,
        PoopColorPreference? color = null,
        IGameClient? victim = null,
        bool playSounds = true)
    {
        var effective = color ?? _colors.Default;
        if (effective.IsRandom)
        {
            var def = ColorUtils.GetRandomColor(_config.Color.AvailableColors);
            effective = new PoopColorPreference(def.Red, def.Green, def.Blue);
        }

        return _spawner.SpawnPoopWithFullLogic(player, size, effective, playSounds, showMessages: false);
    }

    public SpawnPoopResult? ForcePlayerPoop(
        IGameClient? player,
        float size = -1.0f,
        PoopColorPreference? color = null,
        bool playSounds = true)
    {
        if (player is null || !player.IsValid)
            return null;

        color ??= _colors.GetColor(player.SteamId);
        return _spawner.SpawnPoopWithFullLogic(player, size, color, playSounds, showMessages: true);
    }

    #endregion

    #region IPoopShared — statistics

    public async Task<PoopStats?> GetPlayerStatsAsync(SteamID steamId)
    {
        try
        {
            var placed = await _stats.GetPlayerPoopCountAsync(steamId.ToString());
            var pooped = await _stats.GetVictimPoopCountAsync(steamId.ToString());
            var name   = _bridge.ClientManager.GetGameClient(steamId)?.Name ?? "Unknown";

            return new PoopStats { SteamId = steamId, PlayerName = name, PoopsPlaced = placed, TimesPoopedOn = pooped };
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Poop] GetPlayerStatsAsync failed for {SteamId}", steamId);
            return null;
        }
    }

    public Task<PoopStats[]> GetTopPoopersAsync(int limit = 10) => _stats.GetTopPoopersAsync(limit);
    public Task<PoopStats[]> GetTopVictimsAsync(int limit = 10) => _stats.GetTopVictimsAsync(limit);
    public Task<int> GetTotalPoopsCountAsync() => _stats.GetTotalPoopsCountAsync();

    #endregion

    #region IPoopShared — colour preferences

    public Task<PoopColorPreference?> GetPlayerColorPreferenceAsync(SteamID steamId)
        => Task.FromResult(_colors.GetCached(steamId));

    public Task SetPlayerColorPreferenceAsync(SteamID steamId, PoopColorPreference color)
    {
        _colors.Save(steamId, color);
        return Task.CompletedTask;
    }

    #endregion
}
