using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Arguments.Scp049Events;
using MEC;
using PlayerRoles;
using SimpleCustomRoles.Helpers;
using UnityEngine;

namespace ZombieOptOut;

public static class OptOutSystem
{
    public static bool NeedReplacement => _optedOutZombies.Count != 0;
    private static string? _latest049Player; // latest 049 player's userId
    private static Vector3? GetReadyPosition()
    {
        var latest049Player = Player.Get(_latest049Player);
        
        if (latest049Player is { IsAlive: true })
            return latest049Player.Position;
        
        _latest049Player = null;
        return null;
    }
    /// <key name="string">UserId</key>
    /// <value name="Vector3">Saved death position</value>
    private static HashSet<string> _optedOutZombies = [];
    private static Dictionary<string, Vector3> _positionsToReplace = []; // Cached positions to replace after 

    public static void RevivingZombie(Scp049ResurrectingBodyEventArgs ev)
    {
        if (_optedOutZombies.Contains(ev.Player.UserId))
            return;
        
        var playerAndRoles = CustomRoleHelpers.GetPlayerAndRoles();
        
        if ((Main.Instance.Config?.UseCustomRoles ?? Defaults.UseCustomRoles) 
            && playerAndRoles.TryGetValue(ev.Player, out var customRole) 
            && (Main.Instance.Config?.OptOutImmuneZombies.Any(role => customRole.Rolename.Contains(role)) ?? false)) 
            return;

        _latest049Player = ev.Player.UserId;
        var refHub = ev.Target.ReferenceHub;
        
        if (!ServerSpecificSettings.GetOptOutEnabled(refHub)) // item1 is optOut, item2 is autoFill
            return;
        
        _optedOutZombies.Add(ev.Target.UserId); // NOTE: You can use ev.Ragdoll.Position here, but offset it by a bit when you do.
        
        ev.Target.SendBroadcast($"<size=36>[ZombieOptOut] You've opted out of being revived as a zombie in your Settings!</size>", 5);
        ev.IsAllowed = false;
        
        var optedOutPlayer = ev.Target;

        foreach (var player in Player.ReadyList.ToArray())
        {
            if (player == ev.Target)
                continue;
            
            if (player is {IsAlive: true} or {IsDummy: true})
                continue;
            
            if (!ServerSpecificSettings.GetAutoFillEnabled(player.ReferenceHub))
                continue;
            
            ev.Player.SendBroadcast($"<size=36>[ZombieOptOut] <b>{ev.Target.DisplayName}</b> Opted out of being revived and has been replaced with <b>{player.DisplayName}</b></size>", 5);
            _optedOutZombies.Remove(ev.Target.UserId);
            
            if (!_positionsToReplace.TryGetValue(ev.Target.UserId, out var position))
                position = GetValidZombiePosition(ev.Ragdoll.Position);
            
            _positionsToReplace[player.UserId] = position;
            ev.Target = player;
            ev.IsAllowed = true;
            return;
        }

        CL.Info("No players had Auto-Fill enabled!");
        ev.Player.SendBroadcast($"<size=36>[ZombieOptOut] {ev.Target.DisplayName} has opted-out of being a zombie!</size>", 5);
        
        BroadcastUserOptedOut(optedOutPlayer);
        
        Timing.CallDelayed(Main.Instance.Config?.ZombieFillDuration ?? Defaults.ZombieFillDuration, () =>
        {
            if (!NeedReplacement) return;
            if (!Player.TryGet(_latest049Player, out var player) || player.Role != RoleTypeId.Scp049) return;
            
            float compensation = 0;

            for (var i = 0; i < _optedOutZombies.Count; i++)
                compensation += Main.Instance.Config?.HealthCompensation ?? Defaults.HealthCompensation;

            compensation = (Main.Instance.Config?.StackZombieCompensation ?? Defaults.StackZombieCompensation) ? compensation : Main.Instance.Config?.HealthCompensation ?? Defaults.HealthCompensation;
            player.Heal(compensation);
            player.SendBroadcast($"<size=36>[ZombieOptOut] You were compensated for a zombie opting out with <b>+{compensation} HP</b></size>", 5);
            _optedOutZombies.Clear();
        });
    }

    private static void BroadcastUserOptedOut(Player optedOutPlayer)
    {
        foreach (var player in Player.ReadyList.ToArray())
        {
            if (player == optedOutPlayer)
                continue;

            if (player.IsAlive)
                continue;

            if (player.IsDummy)
                continue;

            player.SendBroadcast($"<size=40>[ZombieOptOut] <b>{optedOutPlayer.DisplayName}</b> Has opted out of being a zombie, you can take their spot!\n" +
                                 $"</size><size=34>By typing <b>.optin</b> in your console (`)!</size>", 5);
        }
    }

    public static void RevivedZombie(Scp049ResurrectedBodyEventArgs ev)
    {
        if (!_positionsToReplace.TryGetValue(ev.Target.UserId, out var position))
            return;
        
        ev.Target.Position = position;
        _positionsToReplace.Remove(ev.Target.UserId);
    }

    public static void RoleFill(Player player, string? replacedId = null)
    {
        if (!NeedReplacement)
        {
            CL.Info("No zombie roles waiting to be filled!");
            return;
        }
        
        CL.Info($"Player {player.DisplayName} Auto-Filled!");
        player.SetRole(RoleTypeId.Scp0492, RoleChangeReason.Revived);
        player.Position = GetReadyPosition() ?? GetValidZombiePosition(defaultPosition:player.Position);
        
        replacedId ??= _optedOutZombies.FirstOrDefault();
        if (string.IsNullOrEmpty(replacedId))
        {
            CL.Info("No zombie roles waiting to be filled!");
            return;
        }
        
        var xpToGive = Main.Instance.Config?.OptInExp ?? Defaults.OptedInExp;
        var modifier = xpToGive > 0 ? "+" : "-";
        if (xpToGive != 0)
            XPSystem.BackEnd.XpSystemAPI.AddXP(player, xpToGive, $"Opted-in as a Zombie [{modifier}{xpToGive}]");
        
        _optedOutZombies.Remove(replacedId);
    }

    private static Vector3 GetValidZombiePosition(Vector3 defaultPosition)
    {
        Player? scp0492 = null;
        Player? randomScp = null;
        
        foreach (var player in Player.ReadyList.ToArray())
        {
            if (player.Role == RoleTypeId.Scp049)
                return player.Position;
            
            if (scp0492 == null && player.Role == RoleTypeId.Scp0492)
                scp0492 = player;
            if (randomScp == null && player.Team == Team.SCPs)
                randomScp = player;
        }
        return scp0492?.Position ?? randomScp?.Position ?? defaultPosition;
    }

    internal static void RoundStart()
    {
        _optedOutZombies.Clear();
        _latest049Player = null;
        _positionsToReplace.Clear();
    }

    public static void CacheDeathPositions(PlayerDyingEventArgs ev)
    {
        if (ev.Player.Role != RoleTypeId.Scp0492)
            return;
        
        _positionsToReplace[ev.Player.UserId] = ev.Player.Position;
    }
}