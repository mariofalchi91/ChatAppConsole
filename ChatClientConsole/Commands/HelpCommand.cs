using ChatClientConsole.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Text;

namespace ChatClientConsole.Commands;

public class HelpCommand(UiService ui, IServiceProvider serviceProvider) : IClientCommand
{
    public string Usage => "#help";
    public string Description => "Mostra questo elenco";

    public bool CanExecute(string input) => input.Trim() == Usage;

    public Task ExecuteAsync(string input)
    {
        var commands = serviceProvider.GetServices<IClientCommand>().OrderBy(c => c.Usage);
        var sb = new StringBuilder();
        sb.AppendLine("═════════════ COMANDI DISPONIBILI ══════════════");
        foreach (var cmd in commands)
        {            
            sb.AppendLine($" {cmd.Usage,-15} : {cmd.Description}");
        }
        sb.AppendLine("════════════════════════════════════════════════");

        ui.PrintSystemMessage(sb.ToString(), false);

        return Task.CompletedTask;
    }
}