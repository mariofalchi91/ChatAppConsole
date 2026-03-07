using ChatClientConsole.Services;
using ChatCommons;

namespace ChatClientConsole.Commands;

public class ExitCommand(UiService ui, ChatManager manager) : IClientCommand
{
    public string Usage => "#exit";
    public string Description => "Esce dalla chat privata";

    public bool CanExecute(string input) => input.Trim() == Usage;

    public async Task ExecuteAsync(string input)
    {
        if (manager.CurrentChatType == MessageType.Private)
        {
            await manager.SwitchToPublicAsync();
        }
        else if (manager.CurrentChatType == MessageType.Public)
        {
            ui.PrintSystemMessage("[INFO] Sei già nella pubblica.");
        }
        else
        {
            ui.PrintSystemMessage("[ERRORE] ExitCommand error");
        }
    }
}