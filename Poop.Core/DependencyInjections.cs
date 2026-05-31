using MessagePipe;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Prefix.Poop.Config;
using Prefix.Poop.Database;
using Prefix.Poop.Modules;
using Prefix.Poop.Modules.Lifecycle;

namespace Prefix.Poop;

internal static class DependencyInjections
{
    public static IServiceCollection AddModuleDi(this IServiceCollection services)
    {
        // In-process event bus.
        services.AddMessagePipe();

        // Config loaded from sharp/configs/poop.json.
        services.AddSingleton(sp =>
        {
            var bridge = sp.GetRequiredService<InterfaceBridge>();
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Poop.Config");
            return PoopConfig.Load(bridge.SharpPath, logger);
        });

        // Plain services (singletons, injected by concrete type).
        services.AddSingleton<SizeGenerator>();
        services.AddSingleton<PoopStatsRepository>();
        services.AddSingleton<ColorStore>();
        services.AddSingleton<ColorMenu>();

        // Modules (lifecycle + concrete injection). Order = lifecycle iteration order.
        AddModule<PoopLocale>(services);
        AddModule<GameEventRelay>(services);
        AddModule<DeadPlayerRegistry>(services);
        AddModule<RagdollRegistry>(services);
        AddModule<PoopLifecycleManager>(services);
        AddModule<RainbowController>(services);
        AddModule<PoopSpawner>(services);
        AddModule<SharedInterfaceModule>(services);
        AddModule<VipGateModule>(services);
        AddModule<PoopModule>(services);

        return services;
    }

    /// <summary>Register a type as both its concrete self and an <see cref="IModule"/> (same instance).</summary>
    private static void AddModule<T>(IServiceCollection services) where T : class, IModule
    {
        services.AddSingleton<T>();
        services.AddSingleton<IModule>(sp => sp.GetRequiredService<T>());
    }
}
