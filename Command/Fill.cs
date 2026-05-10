using CommandSystem;
using RemoteAdmin;

namespace ZombieOptOut.Command;

[CommandHandler(typeof(ClientCommandHandler))]
public class Fill : ICommand
{
    public string Command => "Fill";
    public string[] Aliases => []; // commands are case-insensitive
    public string Description => "Replace a disconnected SCP";

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        var player = Player.Get(sender);
        if (sender is not PlayerCommandSender || player == null)
        {
            response = "This command can only be ran by a player!";
            return false;
        }

        if (AFKReplacement.DisconnectedRoleQueue.Count == 0 || !AFKReplacement.CanReplace)
        {
            response = "There's no roles to fill in for currently!";
            return false;
        }

        if (player.IsSCP)
        {
            response = "You can't fill as an SCP!";
            return false;
        }

        if (AFKReplacement.OffendingPlayers.Contains(player.UserId))
        {
            response = "You're blacklisted from replacing SCP's this round.";
            return false;
        }

        if ((Main.Instance.Config?.UseCustomRoles ?? Defaults.UseCustomRoles) && SimpleCustomRoles.Helpers.CustomRoleHelpers.TryGetCustomRole(player, out _))
        {
            response = "You have a custom role, so cannot replace SCP's right now!";
            return false;
        }

        AFKReplacement.OnFilling(player);
        response = "You've filled a role, thank you!";
        return true;
    }
}