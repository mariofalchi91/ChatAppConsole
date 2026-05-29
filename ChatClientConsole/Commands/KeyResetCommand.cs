using ChatClientConsole.Services;

namespace ChatClientConsole.Commands;

public class KeyResetCommand(ChatManager manager, UiService ui) : IClientCommand
{
    public string Usage => "#keyreset";
    public string Description => "Rimuove la chiave E2E per un utente (es. #keyreset username)";

    public bool CanExecute(string input)
    {
        var trimmed = input.Trim();
        return trimmed == Usage || trimmed.StartsWith(Usage + ' ');
    }

    public Task ExecuteAsync(string input)
    {
        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            ui.Print("Sintassi errata! Usa: #keyreset username");
            return Task.CompletedTask;
        }

        var target = parts[1];
        if (!manager.ResetPrivateKey(target, out var error))
        {
            ui.PrintSystemMessage($"[E2E] {error}");
            return Task.CompletedTask;
        }

        ui.PrintSystemMessage($"[E2E] Chiave rimossa per {target}.");
        return Task.CompletedTask;
    }
}
