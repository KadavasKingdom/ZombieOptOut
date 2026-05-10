using LabApi.Loader;
using LabApi.Loader.Features.Plugins;
using LabApi.Loader.Features.Plugins.Enums;
using HarmonyLib;
using LabApi.Events.Handlers;

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
    
    private static Main? _instance;
    public static Main Instance => _instance ?? throw new InvalidOperationException("ZombieOptOut is not initialized!");
    private Harmony? _harmony;
    private const string HarmonyId = "com.kadava.zombieoptout";

    public override void Enable()
    {
        _instance = this;
        
        _harmony = new Harmony(HarmonyId);
        _harmony.PatchAll();

        ManageListeners();
        ServerSpecificSettings.Initialize();
    }
    public override void Disable()
    {
        ManageListeners(pluginEnabled:false);
        
        _harmony?.UnpatchAll(HarmonyId);
        _instance = null;
        ServerSpecificSettings.DeInitialize();
    }

    private static void ManageListeners(bool pluginEnabled = true)
    {
        if (pluginEnabled)
        {
            Scp049Events.ResurrectedBody += OptOutSystem.RevivedZombie;
            Scp049Events.ResurrectingBody += OptOutSystem.RevivingZombie;
            ServerEvents.RoundStarted += OptOutSystem.RoundStart;
            ServerEvents.RoundStarted += AFKReplacement.OnServerRoundStarted;
            PlayerEvents.UpdatingEffect += AFKReplacement.OnUpdatingEffects;
            PlayerEvents.Dying += OptOutSystem.CacheDeathPositions;
            return;
        }
        
        Scp049Events.ResurrectedBody -= OptOutSystem.RevivedZombie;
        Scp049Events.ResurrectingBody -= OptOutSystem.RevivingZombie;
        ServerEvents.RoundStarted -= OptOutSystem.RoundStart;
        ServerEvents.RoundStarted -= AFKReplacement.OnServerRoundStarted;
        PlayerEvents.UpdatingEffect -= AFKReplacement.OnUpdatingEffects;
        PlayerEvents.Dying -= OptOutSystem.CacheDeathPositions;
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