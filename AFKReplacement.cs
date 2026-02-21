using CustomPlayerEffects;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Features.Extensions;
using MEC;
using PlayerRoles;
using SimpleCustomRoles.RoleYaml;

namespace ZombieOptOut;

public class AFKReplacement
{
    private static bool withinRoundStart = true;
    public static float health = 0;
    public static Dictionary<RoleTypeId, CustomRoleBaseInfo> cachedCustomRole = new Dictionary<RoleTypeId, CustomRoleBaseInfo>();
    public static bool canReplace = false;
    private static PlayerChangingRoleEventArgs cachedArgs;
    private static CoroutineHandle fillTimerCoroutine;

    public static void OnServerRoundStarted()
    {
        if (!Main.Instance.Config.AFKReplacement)
            return;

        withinRoundStart = true;
        canReplace = false;
        cachedCustomRole.Clear();

        Timing.CallDelayed(Main.Instance.Config.AFKReplacementValidTime, () => withinRoundStart = false);
    }

    // ADD DUMMY DETECTION
    //Caching information before disconnect or when a main SCP spawns
    public static void OnRoleChanging(PlayerChangingRoleEventArgs ev)
    {
        cachedArgs = ev;
        CL.Info($"[OnRoleChanging] Player changing role event triggered for {cachedArgs.Player.Nickname} | New Role: {cachedArgs.NewRole} | Old Role: {cachedArgs.OldRole.RoleTypeId} | Health: {cachedArgs.Player.Health}");

        if (!withinRoundStart)
            return;

        //Caches custom role when it initially spawns (Anything -> SCP)
        if (cachedArgs.NewRole.IsScp() && cachedArgs.NewRole != RoleTypeId.Scp0492)
        {
            CustomRoleBaseInfo savedCustomRole = null;

            if (cachedCustomRole.ContainsKey(cachedArgs.NewRole))
                cachedCustomRole.Remove(cachedArgs.NewRole);

            if (SimpleCustomRoles.Helpers.CustomRoleHelpers.TryGetCustomRole(cachedArgs.Player, out savedCustomRole))
                cachedCustomRole.Add(cachedArgs.NewRole, savedCustomRole);
            else
                cachedCustomRole.Add(cachedArgs.NewRole, null);

            CL.Info($"Custom role cached: {savedCustomRole.Rolename}");
        }

        //Runs when a player disconnects or dies (SCP -> Spectator)
        if (cachedArgs.NewRole == RoleTypeId.Spectator && cachedArgs.OldRole.RoleTypeId.IsScp() && cachedArgs.OldRole.RoleTypeId != RoleTypeId.Scp0492 && !cachedArgs.Player.TryGetEffect<PitDeath>(out _))
        {
            health = cachedArgs.Player.Health;
            canReplace = true;
            CL.Info($"Health cached: {health}");
        }
    }

    //Caches health if the player suicides off the map
    internal static void OnUpdatingEffects(PlayerEffectUpdatingEventArgs ev)
    {
        if (!ev.Player.Role.IsScp())
            return;

        CL.Info($"Effect: {ev.Effect.name}");

        if (ev.Effect.name.ToLower() == "pitdeath")
        {
            health = ev.Player.Health;
            CL.Info($"[Effect] Health cached: {health}");
        }
    }


    // ADD DUMMY DETECTION
    public static void OnPlayerLeft(PlayerLeftEventArgs ev)
    {
        Timing.CallDelayed(0.2f, () =>
        {
            CL.Info($"[OnPlayerLeft] Player left event triggered for {ev.Player.Nickname} | Curr Role: {ev.Player.Role} | Prev Role: {ev.Player.ReferenceHub.roleManager.PreviouslySentRole.LastOrDefault().Value}");

            if (!Main.Instance.Config.AFKReplacement)
                return;
            if (cachedCustomRole.Count == 0)
                return;

            CL.Info($"Player {ev.Player.Nickname} left while being an SCP");

            if (!withinRoundStart)
                return;


            foreach (Player player in Player.ReadyList)
            {
                if (player.IsSCP)
                    continue;

                if (SimpleCustomRoles.Helpers.CustomRoleHelpers.TryGetCustomRole(player, out _))
                    continue;

                CL.Info($"Sending AFK Replacement message to {player.Nickname}");

                if (player.IsDummy)
                    continue;

                //TODO Detect if its a custom role and alter message
                CL.Info($"<size=40>[AFK Replacement] <b>{cachedCustomRole.LastOrDefault().Key} ({cachedCustomRole.LastOrDefault().Value.Rolename} | {health}hp)</b> has disconnected!\n</size><size=34>You can take their spot by typing <b>.fill</b> in your console (`)!</size>");
                player.ClearBroadcasts();
                player.SendBroadcast($"<size=40>[AFK Replacement] <b>{cachedCustomRole.LastOrDefault().Key} ({cachedCustomRole.LastOrDefault().Value.Rolename} | {health}hp)</b> has disconnected!\n</size><size=34>You can take their spot by typing <b>.fill</b> in your console (`)!</size>", 5);
            }

            if (fillTimerCoroutine != null || !fillTimerCoroutine.IsValid)
                Timing.KillCoroutines(fillTimerCoroutine);

            fillTimerCoroutine = Timing.RunCoroutine(fillTimeout());
        });
    }

    public static void OnFilling(Player fillingPlayer)
    {
        fillingPlayer.SetRole(cachedCustomRole.LastOrDefault().Key);

        if (cachedCustomRole.LastOrDefault().Value != null)
            Timing.CallDelayed(0.5f, () => Server.RunCommand($"/scr set {cachedCustomRole.LastOrDefault().Value.Rolename} {fillingPlayer.PlayerId}"));

        Server.ClearBroadcasts();
        Server.SendBroadcast($"[AFK Replacement] {cachedCustomRole.LastOrDefault().Key} has been replaced!", 5);

        Timing.CallDelayed(2.5f, () => fillingPlayer.Health = health);

        if (fillTimerCoroutine != null || !fillTimerCoroutine.IsValid)
            Timing.KillCoroutines(fillTimerCoroutine);

        cachedCustomRole.Clear();
        canReplace = false;
    }

    private static IEnumerator<float> fillTimeout()
    {
        yield return Timing.WaitForSeconds(15f);
        cachedCustomRole.Clear();
        canReplace = false;
    }


}