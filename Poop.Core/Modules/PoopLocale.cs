using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sharp.Modules.LocalizerManager.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.GameEntities;
using Sharp.Shared.Objects;

namespace Prefix.Poop.Modules;

/// <summary>
/// Thin wrapper over ModSharp's <see cref="ILocalizerManager"/> for the plugin's strings.
/// Formats against the server culture (en-US) — the old plugin used a single global locale, so
/// this preserves behaviour — then applies the chat prefix + colour-token processing.
/// </summary>
internal sealed class PoopLocale : IModule
{
    private const string Culture = "en-US";

    private readonly InterfaceBridge     _bridge;
    private readonly ILogger<PoopLocale> _logger;
    private ILocalizerManager?           _loc;

    public PoopLocale(InterfaceBridge bridge, ILogger<PoopLocale> logger)
    {
        _bridge = bridge;
        _logger = logger;
    }

    public bool Init() => true;

    public void OnAllModulesLoaded(ServiceProvider provider)
    {
        try
        {
            _loc = _bridge.GetLocalizerManager();
            _loc.LoadLocaleFile("poop", true);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Poop] LocalizerManager unavailable — falling back to raw keys");
        }
    }

    /// <summary>Localized + formatted string (no prefix, no colour processing).</summary>
    public string Raw(string key, params object[] args)
    {
        try
        {
            return _loc?.Format(Culture, key, args) ?? key;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Poop] Locale format failed for {Key}", key);
            return key;
        }
    }

    /// <summary>Localized string wrapped with the chat prefix + colour codes.</summary>
    public string Chat(string key, params object[] args)
        => Utils.Format.ChatMessage(Raw(key, args));

    /// <summary>Broadcast a localized chat line to everyone.</summary>
    public void PrintToAll(string key, params object[] args)
        => _bridge.ModSharp.PrintToChatAll(Chat(key, args));

    /// <summary>Send a localized chat line to one player.</summary>
    public void PrintToClient(IPlayerController controller, string key, params object[] args)
        => controller.Print(HudPrintChannel.Chat, Chat(key, args));
}
