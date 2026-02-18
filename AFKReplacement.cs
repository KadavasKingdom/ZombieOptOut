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
    private static PlayerChangingRoleEventArgs cahcedArgs;

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
    //Caching information before disconnect
    public static void OnRoleChanging(PlayerChangingRoleEventArgs ev)
    {
        cahcedArgs = ev;
        CL.Info($"[OnRoleChanging] Player changing role event triggered for {cahcedArgs.Player.Nickname} | New Role: {cahcedArgs.NewRole} | Old Role: {cahcedArgs.OldRole.RoleTypeId} | Health: {cahcedArgs.Player.Health}");

        if (!withinRoundStart)
            return;

        if (cahcedArgs.NewRole == RoleTypeId.Spectator && cahcedArgs.OldRole.RoleTypeId.IsScp() && cahcedArgs.OldRole.RoleTypeId != RoleTypeId.Scp0492)
        {
            health = cahcedArgs.Player.Health;

            //Custom role handling
            CustomRoleBaseInfo savedCustomRole = null;

            if (SimpleCustomRoles.Helpers.CustomRoleHelpers.TryGetCustomRole(cahcedArgs.Player, out savedCustomRole))
                cachedCustomRole.Add(cahcedArgs.OldRole.RoleTypeId, savedCustomRole);
            else
                cachedCustomRole.Add(cahcedArgs.OldRole.RoleTypeId, null);

            CL.Info($"Custom role: {savedCustomRole.Rolename}");
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

            canReplace = true;

            foreach (Player player in Player.ReadyList)
            {
                if(player.IsSCP)
                    continue;

                if (SimpleCustomRoles.Helpers.CustomRoleHelpers.TryGetCustomRole(player, out _))
                    continue;

                CL.Info($"Sending AFK Replacement message to {player.Nickname}");

                if (player.IsDummy)
                    continue;

                //TODO Detect if its a custom role and alter message
                player.SendBroadcast($"<size=40>[AFK Replacement] <b>{cachedCustomRole.LastOrDefault().Key} ({cachedCustomRole.LastOrDefault().Value} | {health}hp)</b> has disconnected!\n</size><size=34>You can take their spot by typing <b>.fill</b> in your console (`)!</size>", 5);
            }
        });
    }

    public static void OnFilling(Player fillingPlayer)
    {
        fillingPlayer.SetRole(cachedCustomRole.LastOrDefault().Key);

        if (cachedCustomRole.LastOrDefault().Value != null)
            Timing.CallDelayed(0.5f, () => Server.RunCommand($"/scr set {cachedCustomRole.LastOrDefault().Value.Rolename} {fillingPlayer.PlayerId}"));

        Timing.CallDelayed(2.5f, () => fillingPlayer.Health = health);

        cachedCustomRole.Clear();
        canReplace = false;
    }
}