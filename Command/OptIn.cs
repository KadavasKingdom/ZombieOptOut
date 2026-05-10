using CommandSystem;
using RemoteAdmin;

namespace ZombieOptOut.Command;

[CommandHandler(typeof(ClientCommandHandler))]
public class OptIn : ICommand
{
    public string Command => "ZombieOptIn";
    public string[] Aliases => ["optin", "zombieoptin", "opt", "oi"];
    public string Description => "Replace an opted out zombie player";

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        var player = Player.Get(sender);
        if (sender is not PlayerCommandSender || player == null)
        {
            response = "This command can only be ran by a player!";
            return false;
        }

        if (!OptOutSystem.NeedReplacement)
        {
            response = "There's no zombie roles waiting to be filled! You can enable Auto-Fill in the settings to never miss out!";
            return false;
        }


        OptOutSystem.RoleFill(player);
        response = "You've filled in for a missing zombie!";
        return true;
    }
}