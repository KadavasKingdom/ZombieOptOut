using CommandSystem;
using RemoteAdmin;

namespace ZombieOptOut.Command;

[CommandHandler(typeof(ClientCommandHandler))]
[CommandHandler(typeof(GameConsoleCommandHandler))]
public class Commands : ICommand
{
    public string Command => "ZombieOptIn";
    public string[] Aliases => ["optin", "zombieoptin", "opt", "oi"];
    public string Description => "Replace an opted out zombie player";
    public bool SanitizeResponse => true;

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        response = null;

        if (sender is not PlayerCommandSender)
        {
            response = "This command can only be ran by a player!";
            return false;
        }

        if (OptOutSystem.waitingForCompensationFrom == 0)
        {
            response = "There's no zombie roles waiting to be filled! You can enable Auto-Fill in the settings to never miss out!";
            return false;
        }

        Player p = Player.Get(sender);

        if (sender is not PlayerCommandSender playerSender)
            return false;

        var player = Player.ReadyList.Where(x => x.UserId == playerSender.SenderId).FirstOrDefault();

        OptOutSystem.RoleFill(player);
        response = "You've filled in for a missing zombie!";
        return true;
    }
}