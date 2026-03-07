using ChatClientConsole.Services;

namespace ChatClientConsole.Commands;

internal class PasswordCommand(UiService ui, ChatManager manager, NetworkService network) : IClientCommand
{
    public string Usage => "#password";
    public string Description => "Cambia la password di accesso";

    public bool CanExecute(string input) => input.Trim() == Usage;

    public async Task ExecuteAsync(string input)
    {
        ui.PrintSystemMessage("[CAMBIO PASSWORD]");

        ui.Print("Vecchia Password: ", true);
        string oldPass = ui.ReadPassword();

        ui.Print("Nuova Password: ", true);
        string newPass = ui.ReadPassword();

        ui.Print("Conferma Nuova Password: ", true);
        string confirmPass = ui.ReadPassword();

        if (newPass != confirmPass)
        {
            ui.PrintSystemMessage("[ERRORE] Le nuove password non coincidono.");
            return;
        }

        if (string.IsNullOrWhiteSpace(newPass) || newPass.Length < 8)
        {
            ui.PrintSystemMessage("[ERRORE] La password è troppo corta.");
            return;
        }

        ui.PrintSystemMessage("[ELABORAZIONE...]");

        bool success = await network.ChangePasswordAsync(manager.MyUsername, oldPass, newPass);

        if (success)
        {
            ui.PrintSystemMessage("[SUCCESSO] Password aggiornata correttamente!");
        }
        else
        {
            ui.PrintSystemMessage("[ERRORE] La vecchia password non è corretta.");
        }
    }
}
