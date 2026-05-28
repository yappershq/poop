using System;
using Microsoft.Extensions.Logging;
using Prefix.Poop.Config;
using Prefix.Poop.Shared.Models;
using Sharp.Modules.MenuManager.Shared;
using Sharp.Shared.Objects;

namespace Prefix.Poop.Modules;

/// <summary>
/// Poop colour selection menu, built on ModSharp's <see cref="IMenuManager"/>. Selecting a colour
/// persists it via <see cref="ColorStore"/> (ClientPreferences cookie).
/// </summary>
internal sealed class ColorMenu
{
    private readonly InterfaceBridge   _bridge;
    private readonly PoopConfig        _config;
    private readonly PoopLocale        _locale;
    private readonly ColorStore        _colors;
    private readonly ILogger<ColorMenu> _logger;

    public ColorMenu(
        InterfaceBridge    bridge,
        PoopConfig         config,
        PoopLocale         locale,
        ColorStore         colors,
        ILogger<ColorMenu> logger)
    {
        _bridge = bridge;
        _config = config;
        _locale = locale;
        _colors = colors;
        _logger = logger;
    }

    public void Open(IGameClient client)
    {
        var controller = client.GetPlayerController();

        if (!_config.Color.EnableColorPreferences)
        {
            if (controller is not null)
                _locale.PrintToClient(controller, "color.disabled");
            return;
        }

        var menuManager = _bridge.GetMenuManager();
        if (menuManager is null)
        {
            _logger.LogWarning("[Poop] MenuManager unavailable — cannot open colour menu");
            return;
        }

        var current = _colors.GetColor(client.SteamId);
        var builder = Menu.Create().Title(_locale.Raw("color.menu_title"));

        foreach (var def in _config.Color.AvailableColors)
        {
            if (def.IsRainbow && !_config.Color.EnableRainbowPoops)
                continue;

            var name = _locale.Raw(def.LocaleKey);

            if (IsSelected(current, def))
            {
                builder.DisabledItem($"{name} ✓");
            }
            else
            {
                var captured = def;
                builder.Item(name, _ => OnSelect(client, captured));
            }
        }

        builder.ExitItem();
        menuManager.DisplayMenu(client, builder.Build());
    }

    private void OnSelect(IGameClient client, ColorDefinition def)
    {
        var pref = new PoopColorPreference(def.Red, def.Green, def.Blue, def.IsRainbow, def.IsRandom);
        _colors.Save(client.SteamId, pref);

        var controller = client.GetPlayerController();
        if (controller is null)
            return;

        if (def.IsRainbow)
        {
            _locale.PrintToClient(controller, "color.set_rainbow");
            _locale.PrintToClient(controller, "color.set_rainbow_info");
        }
        else if (def.IsRandom)
        {
            _locale.PrintToClient(controller, "color.set_random");
            _locale.PrintToClient(controller, "color.set_random_info");
        }
        else
        {
            _locale.PrintToClient(controller, "color.set_normal", _locale.Raw(def.LocaleKey));
        }
    }

    private static bool IsSelected(PoopColorPreference? current, ColorDefinition def)
    {
        if (current is null)
            return false;

        if (def.IsRainbow)
            return current.IsRainbow;

        if (def.IsRandom)
            return current.IsRandom;

        return current is { IsRainbow: false, IsRandom: false }
            && current.Red == def.Red
            && current.Green == def.Green
            && current.Blue == def.Blue;
    }
}
