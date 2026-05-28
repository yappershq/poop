using System;
using System.Collections.Generic;
using System.Globalization;
using MessagePipe;
using Microsoft.Extensions.Logging;
using Prefix.Poop.Config;
using Prefix.Poop.Messages;
using Prefix.Poop.Shared.Models;
using Sharp.Modules.ClientPreferences.Shared;
using Sharp.Shared.Objects;
using Sharp.Shared.Units;

namespace Prefix.Poop.Modules;

/// <summary>
/// Stores each player's poop colour preference in a ClientPreferences cookie
/// (encoded "r,g,b,rainbow,random"). Falls back to an in-memory, session-only cache when the
/// ClientPreferences module is absent. Replaces the old MySQL poop_colors table.
/// </summary>
internal sealed class ColorStore : IDisposable
{
    private const string CookieKey = "poop_color";

    private readonly InterfaceBridge      _bridge;
    private readonly ILogger<ColorStore>  _logger;
    private readonly IDisposable          _subscriptions;
    private readonly Dictionary<SteamID, PoopColorPreference> _cache = new();
    private readonly (int R, int G, int B) _default;

    public ColorStore(
        InterfaceBridge                      bridge,
        PoopConfig                           config,
        ILogger<ColorStore>                  logger,
        ISubscriber<ClientPutInServerMessage> putInServer,
        ISubscriber<ClientDisconnectMessage>  disconnect)
    {
        _bridge  = bridge;
        _logger  = logger;
        _default = ParseRgb(config.Color.DefaultPoopColor, (139, 69, 19));

        var bag = DisposableBag.CreateBuilder();
        putInServer.Subscribe(m => Load(m.Client)).AddTo(bag);
        disconnect.Subscribe(m => _cache.Remove(m.Client.SteamId)).AddTo(bag);
        _subscriptions = bag.Build();
    }

    /// <summary>The configured default colour (a fresh instance each call).</summary>
    public PoopColorPreference Default => new(_default.R, _default.G, _default.B);

    /// <summary>Synchronous read used at poop time — cache, else the configured default.</summary>
    public PoopColorPreference GetColor(SteamID steamId)
        => _cache.TryGetValue(steamId, out var pref) ? pref : Default;

    /// <summary>Cached preference if known (null if not loaded).</summary>
    public PoopColorPreference? GetCached(SteamID steamId)
        => _cache.TryGetValue(steamId, out var pref) ? pref : null;

    /// <summary>Persist + cache a preference.</summary>
    public void Save(SteamID steamId, PoopColorPreference pref)
    {
        _cache[steamId] = pref;

        var prefs = _bridge.GetClientPreference();
        if (prefs is null)
            return;

        try
        {
            prefs.SetCookie(steamId, CookieKey, Encode(pref));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Poop] Failed to save colour cookie for {SteamId}", steamId);
        }
    }

    private void Load(IGameClient client)
    {
        var prefs = _bridge.GetClientPreference();
        if (prefs is null || !prefs.IsLoaded(client.SteamId))
            return;

        try
        {
            var cookie = prefs.GetCookie(client.SteamId, CookieKey);
            if (cookie is { Type: CookieValueType.String } && Decode(cookie.GetString()) is { } pref)
                _cache[client.SteamId] = pref;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Poop] Failed to load colour cookie for {SteamId}", client.SteamId);
        }
    }

    private static string Encode(PoopColorPreference p)
        => string.Create(CultureInfo.InvariantCulture, $"{p.Red},{p.Green},{p.Blue},{(p.IsRainbow ? 1 : 0)},{(p.IsRandom ? 1 : 0)}");

    private static PoopColorPreference? Decode(string raw)
    {
        var parts = raw.Split(',');
        if (parts.Length < 5)
            return null;

        if (int.TryParse(parts[0], out var r) &&
            int.TryParse(parts[1], out var g) &&
            int.TryParse(parts[2], out var b))
        {
            return new PoopColorPreference(r, g, b, parts[3] == "1", parts[4] == "1");
        }

        return null;
    }

    private static (int, int, int) ParseRgb(string raw, (int, int, int) fallback)
    {
        var parts = raw.Split(',');
        if (parts.Length == 3 &&
            int.TryParse(parts[0], out var r) &&
            int.TryParse(parts[1], out var g) &&
            int.TryParse(parts[2], out var b))
        {
            return (r, g, b);
        }

        return fallback;
    }

    public void Dispose()
        => _subscriptions.Dispose();
}
