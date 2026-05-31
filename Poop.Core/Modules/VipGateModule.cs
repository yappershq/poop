using Microsoft.Extensions.DependencyInjection;
using Prefix.Poop.Config;
using Prefix.Poop.Shared;
using Prefix.Poop.Shared.Events;

namespace Prefix.Poop.Modules;

/// <summary>
/// Optional VIP gate for the poop command. Only fires when VipOnly=true AND IVipShared resolved.
/// Subscribes to IPoopShared.OnPoopCommand and sets Cancel=true for non-VIPs.
/// </summary>
internal sealed class VipGateModule : IModule
{
    private readonly InterfaceBridge       _bridge;
    private readonly PoopConfig            _config;
    private readonly PoopLocale            _locale;
    private readonly SharedInterfaceModule _shared;

    public VipGateModule(
        InterfaceBridge       bridge,
        PoopConfig            config,
        PoopLocale            locale,
        SharedInterfaceModule shared)
    {
        _bridge = bridge;
        _config = config;
        _locale = locale;
        _shared = shared;
    }

    public bool Init() => true;

    public void OnAllModulesLoaded(ServiceProvider provider)
    {
        _bridge.ResolveVipShared();

        // Only wire the gate when the config asks for it and the VIP plugin is present.
        if (_config.Commands.VipOnly && _bridge.VipShared is not null)
            _shared.OnPoopCommand += OnPoopCommand;
    }

    public void Shutdown()
        => _shared.OnPoopCommand -= OnPoopCommand;

    private void OnPoopCommand(PoopCommandEventArgs e)
    {
        // Only gate the !poop spawn command — leaderboard queries remain open to all.
        if (e.CommandName != "poop")
            return;

        if (_bridge.VipShared!.IsVip((ulong) e.Player.SteamId))
            return;

        // Player is not a VIP — block and notify.
        e.Cancel = true;
        var controller = e.Player.GetPlayerController();
        if (controller is not null)
            _locale.PrintToClient(controller, "poop.vip_only");
    }
}
