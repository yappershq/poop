using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Prefix.Poop.Shared;
using Prefix.Poop.Shared.Events;
using Sharp.Shared;
using Sharp.Shared.Abstractions;
using Sharp.Shared.Enums;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;

namespace Prefix.Poop.Example;

/// <summary>
/// Example consumer of the <see cref="IPoopShared"/> API: resolves the interface in
/// OnAllModulesLoaded, subscribes to events, and registers a few demo commands.
/// </summary>
public sealed class PoopExample : IModSharpModule
{
    public string DisplayName   => "Poop Example";
    public string DisplayAuthor => "Prefix";

    private readonly ISharedSystem        _shared;
    private readonly ILogger<PoopExample> _logger;
    private IPoopShared?                  _poop;

    public PoopExample(
        ISharedSystem  sharedSystem,
        string         dllPath,
        string         sharpPath,
        Version        version,
        IConfiguration configuration,
        bool           hotReload)
    {
        _shared = sharedSystem;
        _logger = sharedSystem.GetLoggerFactory().CreateLogger<PoopExample>();
    }

    public bool Init() => true;

    public void PostInit()
    {
        _shared.GetClientManager().InstallCommandCallback("forcepoop", OnForcePoop);
        _shared.GetClientManager().InstallCommandCallback("poopstats", OnPoopStats);
        _shared.GetClientManager().InstallCommandCallback("massivepoop", OnMassivePoop);
    }

    public void OnAllModulesLoaded()
    {
        // Publishers register in PostInit; safe to resolve here.
        var iface = _shared.GetSharpModuleManager()
                           .GetRequiredSharpModuleInterface<IPoopShared>(IPoopShared.Identity);

        _poop = iface?.Instance;
        if (_poop is null)
        {
            _logger.LogError("[PoopExample] Poop API not found — is Poop.dll loaded?");
            return;
        }

        _poop.OnPoopSpawned += OnPoopSpawned;
        _poop.OnPoopCommand += OnPoopCommand;
        _logger.LogInformation("[PoopExample] Connected to Poop API");
    }

    public void Shutdown()
    {
        if (_poop is not null)
        {
            _poop.OnPoopSpawned -= OnPoopSpawned;
            _poop.OnPoopCommand -= OnPoopCommand;
        }

        var cm = _shared.GetClientManager();
        cm.RemoveCommandCallback("forcepoop", OnForcePoop);
        cm.RemoveCommandCallback("poopstats", OnPoopStats);
        cm.RemoveCommandCallback("massivepoop", OnMassivePoop);
    }

    // ── events ──

    private void OnPoopSpawned(PoopSpawnedEventArgs e)
        => _logger.LogInformation("[PoopExample] Poop spawned by {Player} size {Size:F2} (success={Success})",
            e.Player?.Name ?? "API", e.Size, e.Success);

    private void OnPoopCommand(PoopCommandEventArgs e)
    {
        // Example gate: uncomment to make !poop donator-only.
        // if (!e.Player.IsAdmin) e.Cancel = true;
        _logger.LogInformation("[PoopExample] {Player} ran poop command '{Cmd}'", e.Player.Name, e.CommandName);
    }

    // ── demo commands ──

    private ECommandAction OnForcePoop(IGameClient client, StringCommand command)
    {
        _poop?.ForcePlayerPoop(client);
        return ECommandAction.Handled;
    }

    private ECommandAction OnMassivePoop(IGameClient client, StringCommand command)
    {
        _poop?.ForcePlayerPoop(client, size: 2.5f);
        return ECommandAction.Handled;
    }

    private ECommandAction OnPoopStats(IGameClient client, StringCommand command)
    {
        var api = _poop;
        if (api is null)
            return ECommandAction.Handled;

        _ = _shared.GetModSharp().InvokeFrameActionAsync(async () =>
        {
            var stats = await api.GetPlayerStatsAsync(client.SteamId);
            var controller = client.GetPlayerController();
            if (stats is not null && controller is not null)
                controller.Print(HudPrintChannel.Chat,
                    $" Poops placed: {stats.PoopsPlaced}, times pooped on: {stats.TimesPoopedOn}");
        });

        return ECommandAction.Handled;
    }
}
