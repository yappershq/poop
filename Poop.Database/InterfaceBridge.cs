namespace Prefix.Poop.Database;

using System.IO;
using Microsoft.Extensions.Logging;
using Sharp.Shared;
using Sharp.Shared.Managers;

internal sealed class InterfaceBridge
{
    public static InterfaceBridge Instance { get; private set; } = null!;

    public string               SharpPath          { get; }
    public string               DllPath            { get; }
    public string               DataPath           { get; }
    public string               ConfigPath         { get; }
    public IModSharp            ModSharp           { get; }
    public ILoggerFactory       LoggerFactory      { get; }
    public ISharpModuleManager  SharpModuleManager { get; }
    public string               ModuleIdentity     { get; }
    public PoopDatabasePlugin   Module             { get; }

    public InterfaceBridge(PoopDatabasePlugin module, ISharedSystem sharedSystem, string sharpPath)
    {
        Instance = this;

        Module     = module;
        SharpPath  = sharpPath;
        DllPath    = Path.GetFullPath(Path.Combine(sharpPath, "modules", "Poop.Database"));
        DataPath   = Path.GetFullPath(Path.Combine(sharpPath, "data"));
        ConfigPath = Path.GetFullPath(Path.Combine(sharpPath, "configs"));

        ModSharp           = sharedSystem.GetModSharp();
        LoggerFactory      = sharedSystem.GetLoggerFactory();
        SharpModuleManager = sharedSystem.GetSharpModuleManager();
        ModuleIdentity     = Path.GetFileNameWithoutExtension(DllPath);

        Directory.CreateDirectory(DataPath);
        Directory.CreateDirectory(ConfigPath);
    }
}
