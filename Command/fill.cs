using CommandSystem;
using RemoteAdmin;

namespace ZombieOptOut.Command;

[CommandHandler(typeof(ClientCommandHandler))]
[CommandHandler(typeof(GameConsoleCommandHandler))]
public class fill : ICommand
{
    public string Command => "Fill";
    public string[] Aliases => ["fill"];
    public string Description => "Replace a disconnected SCP";
    public bool SanitizeResponse => true;

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {

        if (sender is not PlayerCommandSender)
        {
            response = "This command can only be ran by a player!";
            return false;
        }

        if (AFKReplacement.disconnectedRoleQueue.Count == 0 || !AFKReplacement.canReplace)
        {
            response = "There's no roles to fill in for currently!";
            return false;
        }

        Player player = Player.Get(sender);

        if (player.IsSCP)
        {
            response = "You can't fill as an SCP!";
            return false;
        }

        if (AFKReplacement.offendingPlayers.Contains(player.IpAddress))
        {
            response = "You're blacklisted from replacing SCP's this round.";
            return false;
        }

        if (SimpleCustomRoles.Helpers.CustomRoleHelpers.TryGetCustomRole(player, out _))
        {
            response = "You have a custom role, so cannot replace SCP's right now!";
            return false;
        }

        AFKReplacement.OnFilling(player);
        response = "You've filled a role, thankyou!";
        return true;
    }
}