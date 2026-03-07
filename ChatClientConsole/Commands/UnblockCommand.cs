using ChatClientConsole.Services;

namespace ChatClientConsole.Commands;

public class UnblockCommand(ChatManager manager, UiService ui) : IClientCommand
{
    public string Usage => "#unblock";
    public string Description => "Sblocca un utente precedentemente bloccato (es. #unblock username)";

    public bool CanExecute(string input) => input.Trim().StartsWith(Usage);

    public async Task ExecuteAsync(string input)
    {
        var parts = input.Split(' ');
        if (parts.Length < 2)
        {
            ui.Print("Sintassi errata!");
            return;
        }

        string target = parts[1];
        await manager.UnblockUserAsync(target);
    }
}