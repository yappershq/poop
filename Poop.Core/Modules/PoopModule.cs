using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Prefix.Poop.Config;
using Prefix.Poop.Database;
using Prefix.Poop.Modules.Lifecycle;
using Prefix.Poop.Shared.Models;
using Prefix.Poop.Utils;
using Sharp.Shared.Enums;
using Sharp.Shared.Listeners;
using Sharp.Shared.Managers;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;

namespace Prefix.Poop.Modules;

/// <summary>
/// Owns the poop chat commands (poop / poopcolor / toppoopers / toppoop), the poop_dryrun server
/// command, and resource precache. Spawning/stats/menus are delegated to the focused services.
/// </summary>
internal sealed class PoopModule : IModule, IGameListener
{
    private readonly InterfaceBridge        _bridge;
    private readonly PoopConfig             _config;
    private readonly PoopLocale             _locale;
    private readonly PoopSpawner            _spawner;
    private readonly SizeGenerator          _sizeGenerator;
    private readonly PoopLifecycleManager   _lifecycle;
    private readonly ColorMenu              _colorMenu;
    private readonly ColorStore             _colors;
    private readonly PoopStatsRepository    _stats;
    private readonly SharedInterfaceModule  _shared;
    private readonly ILogger<PoopModule>    _logger;
    private readonly CommandCooldownTracker _cooldowns;

    private readonly List<(string alias, IClientManager.DelegateClientCommand handler)> _registered = [];

    public PoopModule(
        InterfaceBridge       bridge,
        PoopConfig            config,
        PoopLocale            locale,
        PoopSpawner           spawner,
        SizeGenerator         sizeGenerator,
        PoopLifecycleManager  lifecycle,
        ColorMenu             colorMenu,
        ColorStore            colors,
        PoopStatsRepository   stats,
        SharedInterfaceModule shared,
        ILogger<PoopModule>   logger)
    {
        _bridge        = bridge;
        _config        = config;
        _locale        = locale;
        _spawner       = spawner;
        _sizeGenerator = sizeGenerator;
        _lifecycle     = lifecycle;
        _colorMenu     = colorMenu;
        _colors        = colors;
        _stats         = stats;
        _shared        = shared;
        _logger        = logger;

        _cooldowns = new CommandCooldownTracker(bridge, config.Commands.CommandCooldownSeconds);
        _cooldowns.SetCommandCooldown("poop",       config.Commands.Poop.CooldownSeconds);
        _cooldowns.SetCommandCooldown("poopcolor",  config.Commands.Color.CooldownSeconds);
        _cooldowns.SetCommandCooldown("toppoopers", config.Commands.TopPoopers.CooldownSeconds);
        _cooldowns.SetCommandCooldown("toppoop",    config.Commands.TopVictims.CooldownSeconds);
    }

    public int ListenerPriority => 0;
    public int ListenerVersion  => IGameListener.ApiVersion;

    public bool Init()
    {
        Format.InitializeChatPrefix(_config.Ui.ChatPrefix);

        _bridge.ModSharp.InstallGameListener(this);

        Register(_config.Commands.Poop,       OnPoop);
        Register(_config.Commands.Color,      OnColor);
        Register(_config.Commands.TopPoopers, OnTopPoopers);
        Register(_config.Commands.TopVictims, OnTopVictims);

        _bridge.ConVarManager.CreateServerCommand("poop_dryrun", OnDryRun);

        return true;
    }

    public void OnAllModulesLoaded(ServiceProvider provider)
    {
        // The database plugin published IDatabaseProvider in its PostInit (guaranteed before any OAM).
        _stats.Attach(_bridge.GetDatabaseProvider());
    }

    public void Shutdown()
    {
        _bridge.ModSharp.RemoveGameListener(this);

        foreach (var (alias, handler) in _registered)
            _bridge.ClientManager.RemoveCommandCallback(alias, handler);

        _registered.Clear();
        _bridge.ConVarManager.ReleaseCommand("poop_dryrun");
    }

    void IGameListener.OnResourcePrecache()
    {
        try
        {
            if (!string.IsNullOrEmpty(_config.Assets.PoopModel))
                _bridge.ModSharp.PrecacheResource(_config.Assets.PoopModel);

            if (_config.Sound.EnableSounds && !string.IsNullOrEmpty(_config.Assets.SoundEventsFile))
                _bridge.ModSharp.PrecacheResource(_config.Assets.SoundEventsFile);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Poop] Failed to precache resources");
        }
    }

    private void Register(CommandConfig cmd, IClientManager.DelegateClientCommand handler)
    {
        if (!cmd.Enabled)
            return;

        foreach (var alias in cmd.Aliases)
        {
            _bridge.ClientManager.InstallCommandCallback(alias, handler);
            _registered.Add((alias, handler));
        }
    }

    // -------------------------------------------------------------------------
    // Command handlers
    // -------------------------------------------------------------------------

    private ECommandAction OnPoop(IGameClient client, StringCommand command)
    {
        if (!_shared.FireCommand(client, "poop"))
            return ECommandAction.Handled;

        var controller = client.GetPlayerController();
        if (controller is null)
            return ECommandAction.Handled;

        var pawn = controller.GetPlayerPawn();
        if (pawn is null || !pawn.IsValid())
        {
            _locale.PrintToClient(controller, "poop.must_be_alive");
            return ECommandAction.Handled;
        }

        if (!_cooldowns.CanExecute("poop", client))
        {
            _locale.PrintToClient(controller, "common.cooldown", _cooldowns.GetRemainingCooldown("poop", client));
            return ECommandAction.Handled;
        }

        if (_lifecycle.HasReachedMaxPoopsPerRound())
        {
            _locale.PrintToClient(controller, "poop.max_per_round");
            return ECommandAction.Handled;
        }

        if (!pawn.Flags.HasFlag(EntityFlags.OnGround))
        {
            _locale.PrintToClient(controller, "poop.must_be_on_ground");
            return ECommandAction.Handled;
        }

        var color = ResolvePoopColor(client.SteamId);
        _spawner.SpawnPoopWithFullLogic(client, size: -1.0f, color, playSounds: true, showMessages: true);
        return ECommandAction.Handled;
    }

    private ECommandAction OnColor(IGameClient client, StringCommand command)
    {
        if (!_shared.FireCommand(client, "poopcolor"))
            return ECommandAction.Handled;

        if (!_cooldowns.CanExecute("poopcolor", client))
        {
            var controller = client.GetPlayerController();
            if (controller is not null)
                _locale.PrintToClient(controller, "common.cooldown", _cooldowns.GetRemainingCooldown("poopcolor", client));
            return ECommandAction.Handled;
        }

        _colorMenu.Open(client);
        return ECommandAction.Handled;
    }

    private ECommandAction OnTopPoopers(IGameClient client, StringCommand command)
    {
        if (!_shared.FireCommand(client, "toppoopers"))
            return ECommandAction.Handled;

        if (!CheckLeaderboardCooldown(client, "toppoopers"))
            return ECommandAction.Handled;

        _ = _bridge.ModSharp.InvokeFrameActionAsync(async () =>
        {
            var top = await _stats.GetTopPoopersAsync(_config.Commands.TopRecordsLimit);
            var controller = client.GetPlayerController();
            if (controller is null)
                return;

            if (top.Length == 0)
            {
                _locale.PrintToClient(controller, "leaderboard.no_poopers");
                return;
            }

            _locale.PrintToClient(controller, "leaderboard.top_poopers_title", top.Length);
            for (var i = 0; i < top.Length; i++)
                _locale.PrintToClient(controller, "leaderboard.top_poopers_entry", i + 1, top[i].PlayerName, top[i].PoopsPlaced);
        });

        return ECommandAction.Handled;
    }

    private ECommandAction OnTopVictims(IGameClient client, StringCommand command)
    {
        if (!_shared.FireCommand(client, "toppoop"))
            return ECommandAction.Handled;

        if (!CheckLeaderboardCooldown(client, "toppoop"))
            return ECommandAction.Handled;

        _ = _bridge.ModSharp.InvokeFrameActionAsync(async () =>
        {
            var top = await _stats.GetTopVictimsAsync(_config.Commands.TopRecordsLimit);
            var controller = client.GetPlayerController();
            if (controller is null)
                return;

            if (top.Length == 0)
            {
                _locale.PrintToClient(controller, "leaderboard.no_victims");
                return;
            }

            _locale.PrintToClient(controller, "leaderboard.top_victims_title", top.Length);
            for (var i = 0; i < top.Length; i++)
                _locale.PrintToClient(controller, "leaderboard.top_victims_entry", i + 1, top[i].PlayerName, top[i].TimesPoopedOn);
        });

        return ECommandAction.Handled;
    }

    private ECommandAction OnDryRun(StringCommand command)
    {
        var count = 1000;
        if (!string.IsNullOrWhiteSpace(command.ArgString) && int.TryParse(command.ArgString.Trim(), out var parsed) && parsed > 0)
            count = Math.Min(parsed, 100000);

        _logger.LogInformation("[Poop] poop_dryrun ({Count} samples):\n{Report}", count, _sizeGenerator.RunDistributionReport(count));
        return ECommandAction.Handled;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private bool CheckLeaderboardCooldown(IGameClient client, string command)
    {
        if (_cooldowns.CanExecute(command, client))
            return true;

        var controller = client.GetPlayerController();
        if (controller is not null)
            _locale.PrintToClient(controller, "common.cooldown", _cooldowns.GetRemainingCooldown(command, client));
        return false;
    }

    private PoopColorPreference ResolvePoopColor(Sharp.Shared.Units.SteamID steamId)
    {
        if (!_config.Color.EnableColorPreferences)
            return _colors.Default;

        var pref = _colors.GetColor(steamId);
        if (!pref.IsRandom)
            return pref;

        var def = ColorUtils.GetRandomColor(_config.Color.AvailableColors);
        return new PoopColorPreference(def.Red, def.Green, def.Blue);
    }
}
