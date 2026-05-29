using ChatClientConsole.Services;

namespace ChatClientConsole.Commands;

public class KeySetCommand(ChatManager manager, UiService ui) : IClientCommand
{
    public string Usage => "#keyset";
    public string Description => "Imposta la chiave E2E per una chat privata (es. #keyset username)";

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
            ui.Print("Sintassi errata! Usa: #keyset username");
            return Task.CompletedTask;
        }

        var target = parts[1];
        if (target.Equals(manager.MyUsername, StringComparison.OrdinalIgnoreCase))
        {
            ui.PrintSystemMessage("[E2E] Non puoi impostare una chiave privata con te stesso.");
            return Task.CompletedTask;
        }

        ui.Print($"Inserisci la chiave condivisa per {target}: ", isInline: true);
        var key = ui.ReadPassword();

        if (!manager.SetPrivateKey(target, key, out var error))
        {
            ui.PrintSystemMessage($"[E2E] {error}");
            return Task.CompletedTask;
        }

        ui.PrintSystemMessage($"[E2E] Chiave impostata per {target}.");
        return Task.CompletedTask;
    }
}
