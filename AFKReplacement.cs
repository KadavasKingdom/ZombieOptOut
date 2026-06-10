using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Arguments.ServerEvents;
using LabApi.Features.Extensions;
using MEC;
using PlayerRoles;
using PlayerRoles.PlayableScps.Scp173;
using UnityEngine;

namespace ZombieOptOut;

public class AFKReplacement
{
    public static bool withinRoundStart = true;
    public static Dictionary<RoleTypeId, string> cachedCustomRole = new Dictionary<RoleTypeId, string>();
    public static Dictionary<RoleTypeId, float> disconnectedRoleQueue = new Dictionary<RoleTypeId, float>();
    public static bool canReplace = false;
    //Uses IP instead of player info directly, otherwise references would be lost on disconnect
    public static List<string> offendingPlayers = new();
    private static CoroutineHandle fillTimerCoroutine;

    //TODO: Queue disconnected players and roles, framework is already mostly in place

    public static void OnServerRoundStarted()
    {
        if (!Main.Instance.Config.AFKReplacement)
            return;

        withinRoundStart = true;
        canReplace = false;
        cachedCustomRole.Clear();
        offendingPlayers.Clear();
        disconnectedRoleQueue.Clear();

        Timing.CallDelayed(Main.Instance.Config.AFKReplacementValidTime, () => withinRoundStart = false);
    }

    internal static void OnDisconnected(Player player)
    {
        if (!withinRoundStart)
            return;
        if (player == null)
            return;
        if (player.IsDummy)
            return;

        if (player.Role.IsScp() && player.Role != RoleTypeId.Scp0492)
        {
            if (SimpleCustomRoles.Helpers.CustomRoleHelpers.TryGetCustomRole(player, out var savedCustomRole))
            {
                if (!cachedCustomRole.ContainsKey(player.Role))
                {
                    cachedCustomRole.Add(player.Role, savedCustomRole.Rolename);
                }
            }
            else cachedCustomRole[player.Role] = null;

            if (disconnectedRoleQueue.ContainsKey(player.Role))
                disconnectedRoleQueue.Remove(player.Role);

            if (!Main.Instance.Config.DisableXPLoss)
                XPSystem.BackEnd.XpSystemAPI.AddXP(player, -500, "<b>Disconnected as an SCP</b>", "red");

            /*if (!offendingPlayers.Contains(player.IpAddress))
                offendingPlayers.Add(player.IpAddress);*/

            disconnectedRoleQueue.Add(player.Role, CacheHealth(player));
            AllowReplacement();
        }
    }

    //Caches health if the player suicides off the map
    internal static void OnUpdatingEffects(PlayerEffectUpdatingEventArgs ev)
    {
        if (!Main.Instance.Config.AFKReplacement)
            return;
        if (ev.Player == null)
            return;
        if (!ev.Player.Role.IsScp())
            return;
        if (ev.Player.IsDummy)
            return;
        if (!withinRoundStart)
            return;
        if (ev.Player.Role == RoleTypeId.Scp0492)
            return;
        if (ev.Effect.name.ToLower() != "pitdeath")
            return;

        // Account for SCP173 falling into pits because they were looked at over the top of a pit.
        if (ev.Player.RoleBase is Scp173Role scp173)
            if (scp173.SubroutineModule.TryGetSubroutine(out Scp173BlinkTimer timer))
                if (timer.RemainingSustain > 0f)
                    return;

        CL.Info("2");
        // If there are enemy players in the same room (typical for when an SCP falls in a pit while chasing someone)
        // otherwise the SCP most likely jumped in a pit of their own accord instead of being "killed" via pit
        if (ev.Player.Room != null)
        {
            if (ev.Player.Room.Players.ToList().Any(other_player => other_player.Faction != ev.Player.Faction))
                return;
        }

        if (SimpleCustomRoles.Helpers.CustomRoleHelpers.TryGetCustomRole(ev.Player, out var savedCustomRole))
        {
            if (!cachedCustomRole.ContainsKey(ev.Player.Role))
                cachedCustomRole.Add(ev.Player.Role, savedCustomRole.Rolename);
            else if (cachedCustomRole.TryGetValue(ev.Player.Role, out var role) && role == null)
                cachedCustomRole.Add(ev.Player.Role, savedCustomRole.Rolename);
        }

        if (disconnectedRoleQueue.ContainsKey(ev.Player.Role))
            disconnectedRoleQueue.Remove(ev.Player.Role);

        disconnectedRoleQueue.Add(ev.Player.Role, CacheHealth(ev.Player));

        if (!Main.Instance.Config.DisableXPLoss)
            XPSystem.BackEnd.XpSystemAPI.AddXP(ev.Player, -500, "<b>Suicided as an SCP</b>", "red");

        if (!offendingPlayers.Contains(ev.Player.IpAddress))
            offendingPlayers.Add(ev.Player.IpAddress);


        AllowReplacement();

    }

    private static void AllowReplacement()
    {
        if (!withinRoundStart)
            return;
        if (Warhead.IsDetonated)
            return;
        if (disconnectedRoleQueue.Count <= 0)
            return;

        canReplace = true;

        foreach (Player player in Player.ReadyList.ToList())
        {
            if (player == null)
                continue;
            if (player.IsSCP)
                continue;
            if (SimpleCustomRoles.Helpers.CustomRoleHelpers.TryGetCustomRole(player, out _))
                continue;
            if (player.IsDummy)
                continue;

            player.ClearBroadcasts();
            player.SendBroadcast(MakeBroadcast(), 10);
        }

        if (fillTimerCoroutine != null || !fillTimerCoroutine.IsValid)
            Timing.KillCoroutines(fillTimerCoroutine);

        fillTimerCoroutine = Timing.RunCoroutine(FillTimeout());
    }

    private static float CacheHealth(Player player)
    {
        //Health is 0 when they die and 200 when they disconnect, setting it to -1 here so we don't bother changing health in the future if the role is filled
        if ((int)player.Health == 0 || (int)player.Health == 200)
            return -1f;
        else
            return player.Health;
    }

    private static string MakeBroadcast()
    {
        string broadcast = $"<size=40>[AFK Replacement] <b>{disconnectedRoleQueue.FirstOrDefault().Key}</b>";

        if (!cachedCustomRole.ContainsKey(disconnectedRoleQueue.FirstOrDefault().Key))
        {
            if (disconnectedRoleQueue.FirstOrDefault().Value != -1f)
                broadcast += $" ({disconnectedRoleQueue.FirstOrDefault().Value}hp) ";
        }
        else
        {
            if (disconnectedRoleQueue.FirstOrDefault().Value != -1f)
                broadcast += $" ({cachedCustomRole[disconnectedRoleQueue.FirstOrDefault().Key]} | {disconnectedRoleQueue.FirstOrDefault().Value}hp) ";
            else
                broadcast += $" ({cachedCustomRole[disconnectedRoleQueue.FirstOrDefault().Key]}) ";
        }


        return (broadcast + "has disconnected!\n </size><size=34> You can take their spot by typing <b>.fill</b> in your console (`)!</size>");
    }

    public static void OnFilling(Player fillingPlayer)
    {
        if (fillingPlayer == null)
            return;
        if (disconnectedRoleQueue.Count <= 0)
            return;

        canReplace = false;

        if (cachedCustomRole.Count > 0)
        {
            if (cachedCustomRole.TryGetValue(disconnectedRoleQueue.FirstOrDefault().Key, out string customRoleName))
                Server.RunCommand($"/scr set {customRoleName} {fillingPlayer.PlayerId}");
        }
        else
        {
            fillingPlayer.SetRole(disconnectedRoleQueue.FirstOrDefault().Key);
        }

        Server.ClearBroadcasts();
        Server.SendBroadcast($"[AFK Replacement] {disconnectedRoleQueue.FirstOrDefault().Key} has been replaced!", 5);

        if (!Main.Instance.Config.DisableXPLoss)
            XPSystem.BackEnd.XpSystemAPI.AddXP(fillingPlayer, 150, "Filled for an SCP [+150]");

        Timing.CallDelayed(3f, () =>
        {
            if (disconnectedRoleQueue.FirstOrDefault().Value != -1f)
                fillingPlayer.Health = Mathf.Clamp(disconnectedRoleQueue.FirstOrDefault().Value, 1f, fillingPlayer.MaxHealth);

            //Doesn't actually queue lul - can be improved in the future to actually queue multiple dc's
            disconnectedRoleQueue.Clear();
            cachedCustomRole.Clear();
        });

        if (fillTimerCoroutine != null || fillTimerCoroutine.IsValid)
            Timing.KillCoroutines(fillTimerCoroutine);
    }

    private static IEnumerator<float> FillTimeout()
    {
        yield return Timing.WaitForSeconds(Main.Instance.Config.SCPFillDuration);
        disconnectedRoleQueue.Clear();
        cachedCustomRole.Clear();
        canReplace = false;
    }

    public static void OnServerRoundEnded(RoundEndedEventArgs ev)
    {
        cachedCustomRole.Clear();
        offendingPlayers.Clear();
        disconnectedRoleQueue.Clear();
    }
}