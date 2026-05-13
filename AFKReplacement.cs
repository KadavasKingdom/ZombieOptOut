using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Features.Extensions;
using MEC;
using PlayerRoles;
using PlayerRoles.PlayableScps.Scp173;
using SimpleCustomRoles.RoleYaml;
using UnityEngine;

namespace ZombieOptOut;

public static class AFKReplacement
{
    private static bool _withinRoundStart = true;
    private static Dictionary<RoleTypeId, CustomRoleBaseInfo> _cachedCustomRole = [];
    public static Dictionary<RoleTypeId, float> DisconnectedRoleQueue = []; // RoleTypeId, Health
    public static bool CanReplace;
    //Uses UserId instead of player info directly, otherwise references would be lost on disconnect
    public static HashSet<string> OffendingPlayers = [];
    private static CoroutineHandle _fillTimerCoroutine;

    //TODO: Queue disconnected players and roles, framework is already mostly in place

    public static void OnServerRoundStarted()
    {
        if (!Main.Instance.Config?.AFKReplacement ?? Defaults.AFKReplacement)
            return;

        _withinRoundStart = true;
        CanReplace = false;
        _cachedCustomRole.Clear();
        OffendingPlayers.Clear();
        DisconnectedRoleQueue.Clear();

        Timing.CallDelayed(Main.Instance.Config?.AFKReplacementValidTime ?? Defaults.AFKReplacementValidTime, () => _withinRoundStart = false);
    }

    internal static void OnDisconnected(Player? player)
    {
        if (player == null)
            return;
        
        if (!_withinRoundStart)
            return;

        if (player.Role.IsScp() && player.Role != RoleTypeId.Scp0492)
        {
            if (player.IsDummy)
                return;

            if ((Main.Instance.Config?.UseCustomRoles ?? Defaults.UseCustomRoles) &&
                SimpleCustomRoles.Helpers.CustomRoleHelpers.TryGetCustomRole(player, out var savedCustomRole) &&
                (!_cachedCustomRole.TryGetValue(player.Role, out var role) || role == null)) 
                _cachedCustomRole[player.Role] = savedCustomRole;

            DisconnectedRoleQueue.Remove(player.Role);

            if (!Main.Instance.Config?.DisableXPLoss ?? Defaults.DisableXPLoss)
                XPSystem.BackEnd.XpSystemAPI.AddXP(player, -500, "<b>Disconnected as an SCP</b>", "red");
            
            OffendingPlayers.Add(player.UserId);
            
            var cacheHealth = CacheHealth(player);
            var plrRole = player.Role;
            
            DisconnectedRoleQueue[plrRole] = cacheHealth;
            AllowReplacement(plrRole, cacheHealth);
        }
    }

    //Caches health if the player suicides off the map
    internal static void OnUpdatingEffects(PlayerEffectUpdatingEventArgs ev)
    {
        if (!Main.Instance.Config?.AFKReplacement ?? Defaults.AFKReplacement)
            return;
        
        var regularRole = ev.Player.Role;
        
        if (!regularRole.IsScp())
            return;
        if (ev.Player.IsDummy)
            return;
        if (!_withinRoundStart)
            return;
        if (regularRole == RoleTypeId.Scp0492)
            return;

        // Account for SCP173 falling into pits because they were looked at over the top of a pit.
        if (ev.Player.RoleBase is Scp173Role scp173 && scp173.SubroutineModule.TryGetSubroutine(out Scp173BlinkTimer timer) && timer.RemainingSustain > 0f)
            return;
        
        // If there are enemy players in the same room (typical for when an SCP falls in a pit while chasing someone)
        if (ev.Player.Room != null && ev.Player.Room.Players.Any(otherPlayer => otherPlayer.Faction != ev.Player.Faction))
            return;
        
        // otherwise the SCP most likely jumped in a pit of their own accord instead of being "killed" via pit
        
        if (!string.Equals(ev.Effect.name, "pitdeath", StringComparison.InvariantCultureIgnoreCase)) 
            return;
        
        if ((Main.Instance.Config?.UseCustomRoles ?? Defaults.UseCustomRoles) && SimpleCustomRoles.Helpers.CustomRoleHelpers.TryGetCustomRole(ev.Player, out var savedCustomRole) && (!_cachedCustomRole.TryGetValue(regularRole, out var role) || role == null))
            _cachedCustomRole[regularRole] = savedCustomRole;
        
        DisconnectedRoleQueue.Remove(regularRole);
        var cachedHealth = CacheHealth(ev.Player);
        DisconnectedRoleQueue[regularRole] = cachedHealth;
        
        if (!Main.Instance.Config?.DisableXPLoss ?? Defaults.DisableXPLoss)
            XPSystem.BackEnd.XpSystemAPI.AddXP(ev.Player, -500, "<b>Suicided as an SCP</b>", "red");
        
        OffendingPlayers.Add(ev.Player.UserId);
        
        AllowReplacement(regularRole, cachedHealth);
    }

    private static void AllowReplacement(RoleTypeId regularRole, float cachedHealth)
    {
        if (!_withinRoundStart)
            return;
        if (Warhead.IsDetonated)
            return;

        CanReplace = true;
        
        foreach (var player in Player.ReadyList)
        {
            if (player.IsSCP)
                continue;

            if ((Main.Instance.Config?.UseCustomRoles ?? Defaults.UseCustomRoles)  && SimpleCustomRoles.Helpers.CustomRoleHelpers.TryGetCustomRole(player, out _))
                continue;

            if (player.IsDummy)
                continue;

            player.ClearBroadcasts();
            player.SendBroadcast(MakeBroadcast(regularRole, cachedHealth), 10);
        }

        if (_fillTimerCoroutine.IsValid)
            Timing.KillCoroutines(_fillTimerCoroutine);

        _fillTimerCoroutine = Timing.RunCoroutine(FillTimeout());
    }

    private static float CacheHealth(Player player)
    {
        //Health is 0 when they die and 200 when they disconnect, setting it to -1 here so we don't bother changing health in the future if the role is filled
        if ((int)player.Health == 0 || (int)player.Health == 200)
            return -1f;
        
        return player.Health;
    }

    private static string MakeBroadcast(RoleTypeId roleId, float health = -1f)
    {
        var broadcast = $"<size=40>[AFK Replacement] <b>{roleId}</b>";

        if (!_cachedCustomRole.TryGetValue(roleId, out var disconnectedPlayer) || disconnectedPlayer == null) 
        {
            if (!Mathf.Approximately(health, -1f))
                broadcast += $" ({health} HP) ";
        }
        else
        {
            if (!Mathf.Approximately(health, -1f))
                broadcast += $" ({disconnectedPlayer.Rolename} | {health} HP) ";
            else
                broadcast += $" ({disconnectedPlayer.Rolename}) ";
        }


        return (broadcast + "has disconnected!\n </size><size=34> You can take their spot by typing <b>.fill</b> in your console (`)!</size>");
    }

    public static void OnFilling(Player fillingPlayer)
    {
        if (DisconnectedRoleQueue.Count == 0)
            return;
        CanReplace = false;
        var replacement = DisconnectedRoleQueue.FirstOrDefault();
        if (replacement.Key == RoleTypeId.None)
            return;

        if (_cachedCustomRole.TryGetValue(replacement.Key, out var customRole) && !string.IsNullOrEmpty(customRole.Rolename))
        {
            Server.RunCommand($"/scr set {customRole.Rolename} {fillingPlayer.PlayerId}");
        }
        else
        {
            fillingPlayer.SetRole(replacement.Key);
        }

        Server.ClearBroadcasts();
        Server.SendBroadcast($"[AFK Replacement] {replacement.Key} has been replaced!", 5);

        if (!Main.Instance.Config?.DisableXPLoss ?? Defaults.DisableXPLoss)
            XPSystem.BackEnd.XpSystemAPI.AddXP(fillingPlayer, 150, "Filled for an SCP [+150]");

        Timing.CallDelayed(3f, () =>
        {
            if (!Mathf.Approximately(replacement.Value, -1f))
                fillingPlayer.Health = replacement.Value;

            DisconnectedRoleQueue.Clear();
        });

        if (_fillTimerCoroutine.IsValid)
            Timing.KillCoroutines(_fillTimerCoroutine);
    }

    private static IEnumerator<float> FillTimeout()
    {
        yield return Timing.WaitForSeconds(Main.Instance.Config?.SCPFillDuration ?? Defaults.SCPFillDuration);
        DisconnectedRoleQueue.Clear();
        CanReplace = false;
    }
}