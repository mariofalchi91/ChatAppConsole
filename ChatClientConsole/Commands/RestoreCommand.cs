using ChatClientConsole.Services;

namespace ChatClientConsole.Commands;

public class RestoreCommand(ChatManager manager) : IClientCommand
{
    public string Usage => "#restore";
    public string Description => "Ricarica la chat corrente";

    public bool CanExecute(string input) => input.Trim() == Usage;

    public async Task ExecuteAsync(string input)
    {
        await manager.RefreshCurrentViewAsync();
    }
}