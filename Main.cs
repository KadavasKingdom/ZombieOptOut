using LabApi.Loader.Features.Plugins;
using LabApi.Loader.Features.Plugins.Enums;

namespace ZombieOptOut;

public class Main : Plugin<Config>
{
    public static Main Instance { get; private set; }
    #region Plugin Info
    public override string Author => "Kadava";
    public override string Name => "ZombieOptOut";
    public override Version Version => new Version(0, 1);
    public override string Description => "Allows players to opt-out of being SCP-049-2";
    public override Version RequiredApiVersion => LabApi.Features.LabApiProperties.CurrentVersion;
    public override LoadPriority Priority => LoadPriority.Lowest;
    #endregion
    public override void Enable()
    {
        Instance = this;

        LabApi.Events.Handlers.Scp049Events.ResurrectedBody += OptOutSystem.RevivedZombie;
        LabApi.Events.Handlers.ServerEvents.RoundStarted += OptOutSystem.RoundStart;
        LabApi.Events.Handlers.PlayerEvents.Left += AFKReplacement.OnPlayerLeft;
        LabApi.Events.Handlers.ServerEvents.RoundStarted += AFKReplacement.OnServerRoundStarted;
        LabApi.Events.Handlers.PlayerEvents.ChangingRole += AFKReplacement.OnRoleChanging;
        LabApi.Events.Handlers.PlayerEvents.UpdatingEffect += AFKReplacement.OnUpdatingEffects;
        ServerSpecificSettings.Initialize();
    }
    public override void Disable()
    {
        Instance = null;

        LabApi.Events.Handlers.Scp049Events.ResurrectedBody -= OptOutSystem.RevivedZombie;
        LabApi.Events.Handlers.ServerEvents.RoundStarted -= OptOutSystem.RoundStart;
        LabApi.Events.Handlers.PlayerEvents.Left -= AFKReplacement.OnPlayerLeft;
        LabApi.Events.Handlers.ServerEvents.RoundStarted -= AFKReplacement.OnServerRoundStarted;
        LabApi.Events.Handlers.PlayerEvents.ChangingRole -= AFKReplacement.OnRoleChanging;
        LabApi.Events.Handlers.PlayerEvents.UpdatingEffect -= AFKReplacement.OnUpdatingEffects;
        ServerSpecificSettings.DeInitialize();
    }
}