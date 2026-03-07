using ChatClientConsole.Services;

namespace ChatClientConsole.Commands;

public class PrivateChatCommand(ChatManager manager) : IClientCommand
{
    public string Usage => "@username";
    public string Description => "Passa alla chat privata";

    public bool CanExecute(string input) 
    {
        var trimmed = input.Trim();
        return trimmed.StartsWith(Usage[0]) && trimmed.Length > 1;
    }

    public async Task ExecuteAsync(string input)
    {
        string target = input.Trim().Substring(1); // Rimuove la @
        if (!string.IsNullOrEmpty(target))
        {
            await manager.SwitchToPrivateAsync(target);
        }
    }
}