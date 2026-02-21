using CustomPlayerEffects;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Features.Extensions;
using LabApi.Features.Wrappers;
using MEC;
using PlayerRoles;
using PlayerStatsSystem;
using SimpleCustomRoles.RoleYaml;

namespace ZombieOptOut;

public class AFKReplacement
{
    private static bool withinRoundStart = true;
    public static float health = 0;
    public static Dictionary<RoleTypeId, CustomRoleBaseInfo> cachedCustomRole = new Dictionary<RoleTypeId, CustomRoleBaseInfo>();
    public static Dictionary<RoleTypeId, float> disconnectedRoleQueue = new Dictionary<RoleTypeId, float>();
    public static bool canReplace = false;
    private static PlayerChangingRoleEventArgs cachedArgs;
    private static CoroutineHandle fillTimerCoroutine;

    //TODO: Queue disconnected players and roles, framework is already mostly in place
    //TODO: Prevent disconnected player from replacing their own role

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
        if (!Main.Instance.Config.AFKReplacement)
            return;
        if (!withinRoundStart)
            return;

        cachedArgs = ev;

        //Caches custom role when it initially spawns (Anything -> SCP), needed to save custom role info
        if (cachedArgs.NewRole.IsScp() && cachedArgs.NewRole != RoleTypeId.Scp0492)
        {
            CustomRoleBaseInfo savedCustomRole = null;

            if (cachedCustomRole.ContainsKey(cachedArgs.NewRole))
                cachedCustomRole.Remove(cachedArgs.NewRole);

            if (SimpleCustomRoles.Helpers.CustomRoleHelpers.TryGetCustomRole(cachedArgs.Player, out savedCustomRole))
                cachedCustomRole.Add(cachedArgs.NewRole, savedCustomRole);
            else
                cachedCustomRole.Add(cachedArgs.NewRole, null);
        }

        //Runs when a player disconnects or dies (SCP -> Spectator) and caches health if they die by pit death
        
    }

    internal static void OnPlayerDying(PlayerDyingEventArgs ev)
    {
        CL.Info("ded");
        if (ev.Player.Role.IsScp() && ev.Player.Role != RoleTypeId.Scp0492)
        {
            CL.Info($"PLAYER HAS DIED | Damage Handler: {ev.DamageHandler} | Attacker: {ev.Attacker} | Role {ev.Player.Role}");

            if (ev.Attacker != null)
                return;

            CL.Info("No attacker");

            //Handled by OnUpdatingEffects to gather health before the effect is applied
            if (ev.Player.HasEffect<PitDeath>())
                return;

            if (disconnectedRoleQueue.ContainsKey(ev.Player.Role))
                disconnectedRoleQueue.Remove(ev.Player.Role);

            disconnectedRoleQueue.Add(ev.Player.Role, CacheHealth(ev.Player));
            AllowReplacement();
        }
    }

    //Caches health if the player suicides off the map
    internal static void OnUpdatingEffects(PlayerEffectUpdatingEventArgs ev)
    {
        if (!Main.Instance.Config.AFKReplacement)
            return;
        if (!ev.Player.Role.IsScp())
            return;

        CL.Info($"Effect: {ev.Effect.name}");

        if (ev.Effect.name.ToLower() == "pitdeath")
        {
            disconnectedRoleQueue.Add(ev.Player.Role, CacheHealth(ev.Player));
            AllowReplacement();
        }
    }

    private static void AllowReplacement()
    {
        if (!withinRoundStart)
            return;
        if (Warhead.IsDetonated)
            return;

        canReplace = true;

        foreach (Player player in Player.ReadyList)
        {
            if (player.IsSCP)
                continue;

            if (SimpleCustomRoles.Helpers.CustomRoleHelpers.TryGetCustomRole(player, out _))
                continue;

            CL.Info($"Sending AFK Replacement message to {player.Nickname}");

            /*if (player.IsDummy)
                continue;*/

            CL.Info(MakeBroadcast());
            player.ClearBroadcasts();
            player.SendBroadcast(MakeBroadcast(), 5);
        }

        if (fillTimerCoroutine != null || !fillTimerCoroutine.IsValid)
            Timing.KillCoroutines(fillTimerCoroutine);

        fillTimerCoroutine = Timing.RunCoroutine(fillTimeout());
    }

    private static float CacheHealth(Player player)
    {
        //Health is 0 when they die and 200 when they disconnect, setting it to -1 here so we don't bother changing health in the future if the role is filled
        if ((int)player.Health == 0 || (int)player.Health == 200)
            health = -1f;
        else
            health = player.Health;

        CL.Info($"Health cached: {health}");
        return health;
    }

    private static string MakeBroadcast()
    {
        string broadcast = $"<size=40>[AFK Replacement] <b>{cachedCustomRole.LastOrDefault().Key}</b>";

        if (cachedCustomRole.LastOrDefault().Value == null)
        {
            if (health != -1f)
                broadcast += $" ({health}hp) ";
        }
        else
        {
            if (health != -1f)
                broadcast += $" ({cachedCustomRole.LastOrDefault().Value.Rolename} | {health}hp) ";
            else
                broadcast += $" ({cachedCustomRole.LastOrDefault().Value.Rolename}) ";
        }


        return (broadcast + "has disconnected!\n </size><size=34> You can take their spot by typing<b>.fill</b> in your console(`)!</size>");
    }

    public static void OnFilling(Player fillingPlayer)
    {
        fillingPlayer.SetRole(cachedCustomRole.LastOrDefault().Key);

        if (cachedCustomRole.LastOrDefault().Value != null)
            Timing.CallDelayed(0.5f, () => Server.RunCommand($"/scr set {cachedCustomRole.LastOrDefault().Value.Rolename} {fillingPlayer.PlayerId}"));

        Server.ClearBroadcasts();
        Server.SendBroadcast($"[AFK Replacement] {cachedCustomRole.LastOrDefault().Key} has been replaced!", 5);

        Timing.CallDelayed(2.5f, () =>
        {
            if (health != -1f)
                fillingPlayer.Health = health;
        });

        if (fillTimerCoroutine != null || fillTimerCoroutine.IsValid)
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