using LabApi.Events.Arguments.Scp049Events;
using LabApi.Features.Wrappers;
using MEC;
using PlayerRoles;
using System.Net.NetworkInformation;
using UnityEngine;


namespace ZombieOptOut
{
    public class OptOutSystem
    {
        public static uint waitingForCompensationFrom = 0;
        private static Player optedOutPlayer = null;
        private static Player main049Player = null;

        public static void RevivedZombie(Scp049ResurrectedBodyEventArgs ev)
        {
            main049Player = ev.Player;
            ReferenceHub refHub = ev.Target.ReferenceHub;

            if (ServerSpecificSettings.savedSettings[refHub].Item1 == false)
            {
                ev.Target.SetRole(RoleTypeId.Spectator);
                ev.Target.SendBroadcast($"<size=36>[ZombieOptOut] You've opted out of being revived as a zombie in your Settings!</size>", 5);
                optedOutPlayer = ev.Target;

                foreach (Player player in Player.ReadyList)
                {
                    if (player.IsAlive)
                        continue;

                    if (player == null)
                        continue;

                    ReferenceHub listHub = player.ReferenceHub;

                    if (ServerSpecificSettings.savedSettings[listHub].Item2)
                        continue;

                    ev.Player.SendBroadcast($"<size=36>[ZombieOptOut] <b>{ev.Target.DisplayName}</b> Opted out of being revived and has been replaced with <b>{player.DisplayName}</b></size>", 5);
                    RoleFill(player);
                    return;
                }

                CL.Info("No players had Auto-Fill enabled!");
                ClampedCompensation();

                foreach (Player player in Player.ReadyList)
                {
                    if(player == optedOutPlayer)
                        continue;

                    if(player.IsAlive)
                        continue;

                    player.SendBroadcast($"<size=40>[ZombieOptOut] <b>{ev.Target.DisplayName}</b> Has opted out of being a zombie, you can take their spot!\n</size><size=34>By typing <b>.optin</b> in your console (`)!</size>", 5);
                }

                Timing.CallDelayed(ZombieOptOut.Main.Instance.Config.FillDuration, () => 
                {
                    if(waitingForCompensationFrom != 0)
                    {
                        foreach(Player player in Player.ReadyList)
                        {
                            if(player.Role != RoleTypeId.Scp049)
                                continue;

                            player.Heal(ZombieOptOut.Main.Instance.Config.HealthCompensation);
                            player.SendBroadcast($"<size=36>[ZombieOptOut] You were compenstaed for a zombie opting out with <b>+{ZombieOptOut.Main.Instance.Config.HealthCompensation}HP</b></size>", 5);
                        }

                        ClampedCompensation(-1);
                    }
                });

            }
        }

        public static void RoleFill(Player player)
        {
            CL.Info($"Player {player.DisplayName} Auto-Filled!");
            player.SetRole(RoleTypeId.Scp0492, RoleChangeReason.Revived);
            Timing.CallDelayed(0.5f, () => player.Position = main049Player.Position);
            ClampedCompensation(-1);
        }

        private static void ClampedCompensation(int value = 1)
        {
            waitingForCompensationFrom = (uint)(Mathf.Clamp(waitingForCompensationFrom + value, 0, 50));
        }
    }
}