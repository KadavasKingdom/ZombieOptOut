using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Arguments.Scp049Events;
using LabApi.Events.CustomHandlers;

namespace ZombieOptOut;

public class EventHandler : CustomEventsHandler
{
    public override void OnScp049ResurrectedBody(Scp049ResurrectedBodyEventArgs ev) => OptOutSystem.RevivedZombie(ev);
    public override void OnScp049ResurrectingBody(Scp049ResurrectingBodyEventArgs ev) => OptOutSystem.RevivingZombie(ev);

    public override void OnServerRoundStarted()
    {
        OptOutSystem.RoundStart();
        AFKReplacement.OnServerRoundStarted();
    }

    public override void OnPlayerUpdatingEffect(PlayerEffectUpdatingEventArgs ev) => AFKReplacement.OnUpdatingEffects(ev);
    public override void OnPlayerDying(PlayerDyingEventArgs ev) => OptOutSystem.CacheDeathPositions(ev);
}