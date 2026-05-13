using LabApi.Loader;
using LabApi.Loader.Features.Plugins;
using LabApi.Loader.Features.Plugins.Enums;
using HarmonyLib;
using LabApi.Events.CustomHandlers;

namespace ZombieOptOut;

public class Main : Plugin<Config>
{
    #region Plugin Info
    public override string Author => "Kadava";
    public override string Name => "ZombieOptOut";
    public override Version Version => new(0, 2);
    public override string Description => "Allows players to opt-out of being SCP-049-2";
    public override Version RequiredApiVersion => LabApi.Features.LabApiProperties.CurrentVersion;
    public override LoadPriority Priority => LoadPriority.Lowest;
    #endregion
    
    public static Main Instance { get; private set; }
    private Harmony? _harmony;
    private const string HarmonyId = "com.kadava.zombieoptout";
    private EventHandler Events { get; } = new();
    
    public override void Enable()
    {
        Instance = this;
        
        _harmony = new Harmony(HarmonyId);
        _harmony.PatchAll();
        CustomHandlersManager.RegisterEventsHandler(Events);
        ServerSpecificSettings.Initialize();
    }
    public override void Disable()
    {
        ServerSpecificSettings.DeInitialize();
        CustomHandlersManager.UnregisterEventsHandler(Events);
        
        _harmony?.UnpatchAll(HarmonyId);
        Instance = null!;
    }
    
    public override void LoadConfigs()
    {
        if (!this.TryLoadConfig(ConfigFileName, out Config? config, true))
        {
            CL.Warn("Failed to load the configuration file, using default values.");
            config = new Config();
        }

        Config = config;
    }
}