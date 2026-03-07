using ChatClientConsole.Services;

namespace ChatClientConsole.Commands;

public class BlockedListCommand(ChatManager manager) : IClientCommand
{
    public string Usage => "#blocked";
    public string Description => "Mostra la lista degli utenti bloccati";

    public bool CanExecute(string input) => input.Trim() == Usage;

    public Task ExecuteAsync(string input)
    {
        manager.PrintBlockedList();
        return Task.CompletedTask;
    }
}