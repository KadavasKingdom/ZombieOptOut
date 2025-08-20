using LabApi.Events.Arguments.Scp049Events;
using LabApi.Events.Handlers;
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
        ServerSpecificSettings.Initialize();
    }
    public override void Disable()
    {
        Instance = null;

        LabApi.Events.Handlers.Scp049Events.ResurrectedBody -= OptOutSystem.RevivedZombie;
        ServerSpecificSettings.DeInitialize();
    }
}