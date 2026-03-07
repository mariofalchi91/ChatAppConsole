using ChatClientConsole.Services;

namespace ChatClientConsole.Commands;

internal class BlockCommand(ChatManager manager, UiService ui) : IClientCommand
{
    public string Usage => "#block";
    public string Description => "Blocca un utente (es. #block username)";

    public bool CanExecute(string input) => input.Trim() == Usage || input.Trim().StartsWith(Usage + ' ');

    public async Task ExecuteAsync(string input)
    {
        var parts = input.Split(' ');
        if (parts.Length < 2)
        {
            ui.Print("Sintassi errata!");
            return;
        }

        string target = parts[1];
        await manager.BlockUserAsync(target);
    }
}
