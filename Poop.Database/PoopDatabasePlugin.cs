namespace Prefix.Poop.Database;

using System;
using System.IO;
using LiteDB;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Prefix.Poop.Database.Extensions;
using Prefix.Poop.Database.Provider;
using Prefix.Poop.Database.Shared;
using Sharp.Shared;
using SqlSugar;

/// <summary>
/// Poop Database Plugin — centralized database infrastructure.
/// Supports LiteDB (default), MySQL, and PostgreSQL via the same <see cref="IDatabaseProvider"/>
/// interface, switchable through <c>configs/poop.database.json</c>.
///
/// <para>Registered on <see cref="PostInit"/> via <c>SharpModuleManager</c> so any module
/// can discover it with
/// <c>GetOptionalSharpModuleInterface&lt;IDatabaseProvider&gt;(IDatabaseProvider.Identity)</c>.</para>
/// </summary>
public sealed class PoopDatabasePlugin : IModSharpModule
{
    public string DisplayName   => "Poop.Database";
    public string DisplayAuthor => "Prefix";

    private readonly InterfaceBridge _bridge;
    private readonly ILogger<PoopDatabasePlugin> _logger;
    private readonly ServiceProvider _serviceProvider;

    public PoopDatabasePlugin(
        ISharedSystem   sharedSystem,
        string?         dllPath,
        string?         sharpPath,
        Version?        version,
        IConfiguration? coreConfiguration,
        bool            hotReload)
    {
        ArgumentNullException.ThrowIfNull(sharpPath);

        _bridge = new InterfaceBridge(this, sharedSystem, sharpPath);
        _logger = sharedSystem.GetLoggerFactory().CreateLogger<PoopDatabasePlugin>();

        var configuration = LoadConfiguration(sharpPath);

        var services = new ServiceCollection();
        services.AddSingleton(_bridge);
        services.AddSingleton(sharedSystem);
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(sharedSystem.GetLoggerFactory());

        var dbType = (configuration["Database:Type"] ?? "litedb").ToLowerInvariant();

        if (dbType == "litedb")
        {
            var database = configuration["Database:Database"] ?? "poop";
            var dataDir = Path.Combine(sharpPath, "data");
            Directory.CreateDirectory(dataDir);
            var dbPath = Path.Combine(dataDir, $"{database}.db");

            _logger.LogInformation("[Poop.Database] Using LiteDB: {DbPath}", dbPath);

            services.AddSingleton(new LiteDatabase(new ConnectionString
            {
                Filename   = dbPath,
                Connection = ConnectionType.Shared,
            }));
            services.AddSingleton<IDatabaseProvider, LiteDbDatabaseProvider>();
        }
        else
        {
            _logger.LogInformation("[Poop.Database] Using SqlSugar ({DbType})", dbType);

            services.AddSingleton<ISqlSugarClient>(_ =>
                new SqlSugarScope(SugarExtensions.BuildConnectionConfig(configuration, _bridge.SharpPath)));
            services.AddSingleton<IDatabaseProvider, DatabaseProvider>();
        }

        _serviceProvider = services.BuildServiceProvider();
    }

    public bool Init() => true;

    public void PostInit()
    {
        var provider = _serviceProvider.GetRequiredService<IDatabaseProvider>();
        _bridge.SharpModuleManager.RegisterSharpModuleInterface<IDatabaseProvider>(
            _bridge.Module, IDatabaseProvider.Identity, provider);

        _logger.LogInformation("[Poop.Database] Registered IDatabaseProvider ({Id})", IDatabaseProvider.Identity);
    }

    public void OnAllModulesLoaded() { }
    public void OnLibraryConnected(string name) { }
    public void OnLibraryDisconnect(string name) { }

    public void Shutdown() => _serviceProvider.Dispose();

    private const string DefaultConfig =
        """
        {
            "Database": {
                "Type": "litedb",
                "Host": "localhost",
                "Port": 3306,
                "Database": "poop",
                "User": "root",
                "Password": ""
            }
        }
        """;

    private static IConfigurationRoot LoadConfiguration(string sharpPath)
    {
        var configPath = Path.Combine(sharpPath, "configs", "poop.database.json");

        if (!File.Exists(configPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
            File.WriteAllText(configPath, DefaultConfig);
        }

        return new ConfigurationBuilder()
            .AddJsonFile(configPath, optional: false, reloadOnChange: false)
            .Build();
    }
}
