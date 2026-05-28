using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Prefix.Poop.Database.Shared;
using Sharp.Modules.ClientPreferences.Shared;
using Sharp.Modules.LocalizerManager.Shared;
using Sharp.Modules.MenuManager.Shared;
using Sharp.Shared;
using Sharp.Shared.Abstractions;
using Sharp.Shared.Managers;
using Sharp.Shared.Objects;

namespace Prefix.Poop;

/// <summary>
/// A self-contained unit of plugin functionality. The host calls these in order:
/// Init (all) → OnPostInit (all) → OnAllModulesLoaded (all), and Shutdown on unload.
/// </summary>
internal interface IModule
{
    bool Init();

    void OnPostInit(ServiceProvider provider)
    {
    }

    void OnAllModulesLoaded(ServiceProvider provider)
    {
    }

    void Shutdown()
    {
    }
}

/// <summary>
/// Caches the ModSharp interfaces + optional modules so modules don't each re-resolve them.
/// </summary>
internal sealed class InterfaceBridge
{
    private readonly ISharedSystem _sharedSystem;

    internal static InterfaceBridge Instance { get; private set; } = null!;

    public InterfaceBridge(string          dllPath,
                           string          sharpPath,
                           Version         version,
                           ISharedSystem   sharedSystem,
                           IModSharpModule plugin,
                           bool            hotReload,
                           bool            debug)
    {
        DllPath       = dllPath;
        SharpPath     = sharpPath;
        Version       = version;
        _sharedSystem = sharedSystem;
        Plugin        = plugin;
        HotReload     = hotReload;
        Debug         = debug;

        ModSharp       = sharedSystem.GetModSharp();
        ConVarManager  = sharedSystem.GetConVarManager();
        EventManager   = sharedSystem.GetEventManager();
        ClientManager  = sharedSystem.GetClientManager();
        EntityManager  = sharedSystem.GetEntityManager();
        SoundManager   = sharedSystem.GetSoundManager();
        HookManager    = sharedSystem.GetHookManager();
        SchemaManager  = sharedSystem.GetSchemaManager();
        SharpModule    = sharedSystem.GetSharpModuleManager();

        Instance = this;
    }

    public string          DllPath   { get; }
    public string          SharpPath { get; }
    public Version         Version   { get; }
    public IModSharpModule Plugin    { get; }
    public bool            HotReload { get; }
    public bool            Debug     { get; }

    public IModSharp      ModSharp      { get; }
    public IConVarManager ConVarManager { get; }
    public IEventManager  EventManager  { get; }
    public IClientManager ClientManager { get; }
    public IEntityManager EntityManager { get; }
    public ISoundManager  SoundManager  { get; }
    public IHookManager   HookManager   { get; }
    public ISchemaManager SchemaManager { get; }

    public ISharpModuleManager SharpModule { get; }

    public IGameRules  GameRules  => ModSharp.GetGameRules();
    public IGlobalVars GlobalVars => ModSharp.GetGlobals();

    public ILoggerFactory LoggerFactory => _sharedSystem.GetLoggerFactory();

    private ILocalizerManager? _cachedLocalizerManager;
    private IClientPreference?  _cachedClientPreference;
    private IMenuManager?       _cachedMenuManager;
    private IDatabaseProvider?  _cachedDatabaseProvider;
    private bool                _clientPreferenceResolved;
    private bool                _menuManagerResolved;
    private bool                _databaseProviderResolved;

    /// <summary>Resolve the generic database provider (optional — stats degrade if absent).</summary>
    public IDatabaseProvider? GetDatabaseProvider()
    {
        if (_databaseProviderResolved)
            return _cachedDatabaseProvider;

        var iface = SharpModule.GetOptionalSharpModuleInterface<IDatabaseProvider>(IDatabaseProvider.Identity);

        if (iface is { IsAvailable: true, Instance: { } instance })
            _cachedDatabaseProvider = instance;

        _databaseProviderResolved = true;
        return _cachedDatabaseProvider;
    }

    /// <summary>Resolve the LocalizerManager (required). Call from OnAllModulesLoaded.</summary>
    public ILocalizerManager GetLocalizerManager()
    {
        if (_cachedLocalizerManager is not null)
            return _cachedLocalizerManager;

        var iface = SharpModule.GetRequiredSharpModuleInterface<ILocalizerManager>(ILocalizerManager.Identity);

        if (iface is { IsAvailable: true, Instance: { } instance })
        {
            _cachedLocalizerManager = instance;
            return instance;
        }

        throw new InvalidOperationException($"Required module '{ILocalizerManager.Identity}' is unavailable.");
    }

    /// <summary>Resolve ClientPreferences (optional — color prefs degrade to session-only if absent).</summary>
    public IClientPreference? GetClientPreference()
    {
        if (_clientPreferenceResolved)
            return _cachedClientPreference;

        var iface = SharpModule.GetOptionalSharpModuleInterface<IClientPreference>(IClientPreference.Identity);

        if (iface is { IsAvailable: true, Instance: { } instance })
            _cachedClientPreference = instance;

        _clientPreferenceResolved = true;
        return _cachedClientPreference;
    }

    /// <summary>Resolve the MenuManager (optional — color menu disabled if absent).</summary>
    public IMenuManager? GetMenuManager()
    {
        if (_menuManagerResolved)
            return _cachedMenuManager;

        var iface = SharpModule.GetOptionalSharpModuleInterface<IMenuManager>(IMenuManager.Identity);

        if (iface is { IsAvailable: true, Instance: { } instance })
            _cachedMenuManager = instance;

        _menuManagerResolved = true;
        return _cachedMenuManager;
    }
}
