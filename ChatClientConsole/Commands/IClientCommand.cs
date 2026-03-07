namespace ChatClientConsole.Commands;

public interface IClientCommand
{
    // Metadati per l'Help
    string Usage { get; }       // Es: "#password"
    string Description { get; } // Es: "Cambia la tua password"
    // logica
    bool CanExecute(string input);
    Task ExecuteAsync(string input);
}
