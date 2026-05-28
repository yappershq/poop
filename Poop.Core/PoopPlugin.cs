using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sharp.Shared;
using Sharp.Shared.Abstractions;

namespace Prefix.Poop;

/// <summary>
/// Poop plugin entry point. Drives the IModule lifecycle over a DI container.
/// Publishers (e.g. the shared-interface module) register in PostInit; consumers resolve
/// optional modules in OnAllModulesLoaded — ModSharp guarantees all PostInits run before any OAM.
/// </summary>
public sealed class PoopPlugin : IModSharpModule
{
    public string DisplayName   => "Poop";
    public string DisplayAuthor => "Prefix";

    private readonly ServiceProvider     _serviceProvider;
    private readonly ILogger<PoopPlugin> _logger;
    private readonly InterfaceBridge     _bridge;

    public PoopPlugin(
        ISharedSystem  sharedSystem,
        string         dllPath,
        string         sharpPath,
        Version        version,
        IConfiguration configuration,
        bool           hotReload)
    {
        var loggerFactory = sharedSystem.GetLoggerFactory();
        _logger = loggerFactory.CreateLogger<PoopPlugin>();

        var bridge = new InterfaceBridge(dllPath,
                                         sharpPath,
                                         version,
                                         sharedSystem,
                                         this,
                                         hotReload,
                                         sharedSystem.GetModSharp().HasCommandLine("-debug"));

        var services = new ServiceCollection();
        services.AddSingleton(bridge);
        services.AddSingleton(loggerFactory);
        services.AddSingleton(sharedSystem);
        services.AddLogging();
        services.AddModuleDi();

        _bridge          = bridge;
        _serviceProvider = services.BuildServiceProvider();
    }

    public bool Init()
    {
        foreach (var service in _serviceProvider.GetServices<IModule>())
        {
            try
            {
                if (service.Init())
                {
                    if (_bridge.Debug)
                        _logger.LogInformation("{Service} initialized", service.GetType().FullName);

                    continue;
                }

                _logger.LogError("Failed to init {Service}!", service.GetType().FullName);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to init {Service}!", service.GetType().FullName);
            }

            return false;
        }

        return true;
    }

    public void PostInit()
    {
        foreach (var service in _serviceProvider.GetServices<IModule>())
        {
            try
            {
                service.OnPostInit(_serviceProvider);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error in PostInit for {Service}", service.GetType().FullName);
            }
        }
    }

    public void OnAllModulesLoaded()
    {
        foreach (var service in _serviceProvider.GetServices<IModule>())
        {
            try
            {
                service.OnAllModulesLoaded(_serviceProvider);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error in OnAllModulesLoaded for {Service}", service.GetType().FullName);
            }
        }
    }

    public void Shutdown()
    {
        foreach (var service in _serviceProvider.GetServices<IModule>())
        {
            try
            {
                service.Shutdown();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error in Shutdown for {Service}", service.GetType().FullName);
            }
        }

        _serviceProvider.Dispose();
    }
}
