using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Features.Extensions;
using MEC;
using PlayerRoles;
using SimpleCustomRoles.RoleYaml;

namespace ZombieOptOut;

public class AFKReplacement
{
    private static bool canReplace = true;
    public static float health = 0;
    public static Dictionary<RoleTypeId, CustomRoleBaseInfo> cachedCustomRole = new Dictionary<RoleTypeId, CustomRoleBaseInfo>();

    public static void OnServerRoundStarted()
    {
        if (!Main.Instance.Config.AFKReplacement)
            return;

        canReplace = true;
        cachedCustomRole.Clear();

        Timing.CallDelayed(Main.Instance.Config.AFKReplacementValidTime, () => canReplace = false);
    }

    // ADD DUMMY DETECTION
    //Caching information before disconnect
    public static void OnRoleChanging(PlayerChangingRoleEventArgs ev)
    {
        if (ev.NewRole == RoleTypeId.Destroyed && ev.OldRole.RoleTypeId.IsScp())
        {
            health = ev.Player.Health;

            //Custom role handling
            CustomRoleBaseInfo savedCustomRole = null;

            if (SimpleCustomRoles.Helpers.CustomRoleHelpers.TryGetCustomRole(ev.Player, out savedCustomRole))
                cachedCustomRole.Add(ev.OldRole.RoleTypeId, savedCustomRole);
            else
                cachedCustomRole.Add(ev.OldRole.RoleTypeId, null);
        }
    }
    // ADD DUMMY DETECTION
    public static void OnPlayerLeft(PlayerLeftEventArgs ev)
    {
        CL.Info($"Player left event triggered for {ev.Player.Nickname} | Curr Role: {ev.Player.Role} | Prev Role: {ev.Player.ReferenceHub.roleManager.PreviouslySentRole.LastOrDefault().Value}");

        if (!Main.Instance.Config.AFKReplacement)
            return;
        if (!ev.Player.ReferenceHub.roleManager.PreviouslySentRole.LastOrDefault().Value.IsScp())
            return;

        CL.Info($"Player {ev.Player.Nickname} left while being an SCP");

        if (!canReplace)
            return;

        foreach (Player player in Player.ReadyList)
        {
            if (!SimpleCustomRoles.Helpers.CustomRoleHelpers.TryGetCustomRole(player, out _) || player.Role == RoleTypeId.Spectator)
                continue;
            if (player.IsDummy)
                continue;

            //TODO Detect if its a custom role and alter message
            player.SendBroadcast($"<size=40>[AFK Replacement] <b>{cachedCustomRole.LastOrDefault().Key} ({cachedCustomRole.LastOrDefault().Value} | {health}hp)</b> has disconnected!\n</size><size=34>You can take their spot by typing <b>.fill</b> in your console (`)!</size>", 5);
        }
    }

    public static void OnFilling(Player fillingPlayer)
    {
        fillingPlayer.SetRole(cachedCustomRole.LastOrDefault().Key);

        if (cachedCustomRole.LastOrDefault().Value != null)
            Timing.CallDelayed(0.5f, () => Server.RunCommand($"/scr set {cachedCustomRole.LastOrDefault().Value.Rolename} {fillingPlayer.PlayerId}"));

        Timing.CallDelayed(2.5f, () => fillingPlayer.Health = health);

        cachedCustomRole.Clear();
    }
}