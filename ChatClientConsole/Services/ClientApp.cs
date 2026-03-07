using ChatClientConsole.Commands;
using ChatCommons;
using System.Text.RegularExpressions;

namespace ChatClientConsole.Services;

public class ClientApp(NetworkService network, ChatManager chatManager, IEnumerable<IClientCommand> commands, UiService ui)
{
    public async Task RunAsync()
    {
        SetupEvents();

        if (!await TryConnectAsync())
        {
            return;
        }

        await DoLoginFlowAsync();
        await chatManager.InitializeAsync();
        await chatManager.SwitchToPublicAsync();
        await ChatLoopAsync();
    }

    private async Task<bool> TryConnectAsync()
    {
        try
        {
            await network.ConnectAsync();
            return true;
        }
        catch (Exception ex)
        {
            ui.PrintSystemMessage($"[ERRORE DI CONNESSIONE] {ex.Message}");
            return false;
        }
    }

    private async Task ChatLoopAsync()
    {
        //ui.PrintPrompt();

        while (true)
        {
            string input = ui.ReadInput();

            if (string.IsNullOrWhiteSpace(input))
            {
                //ui.PrintPrompt();
                continue;
            }

            // A. Cerchiamo un comando che sappia gestire l'input
            var commandToExecute = commands.FirstOrDefault(c => c.CanExecute(input));

            if (commandToExecute != null)
            {
                // B. Trovato! Eseguiamo il comando
                await commandToExecute.ExecuteAsync(input);
            }
            else
            {
                // C. Nessun comando trovato -> È un messaggio di chat normale
                if (chatManager.CurrentChatType == MessageType.Public)
                {
                    await network.SendPublicMessage(chatManager.MyUsername, input);
                }
                else
                {
                    await network.SendPrivateMessage(chatManager.MyUsername, chatManager.CurrentChatPartnerName, input);
                }
            }
        }
    }

    private async Task DoLoginFlowAsync()
    {
        ui.Clear();
        ui.Print("===== CHAT CLIENT =====");

        while (true)
        {
            ui.Print("Username: ", true);
            string user = ui.ReadInput(false);

            if (!Regex.IsMatch(user ?? "", "^[a-z0-9]+$"))
            {
                ui.Print("Nome non valido (solo a-z, 0-9)."); 
                continue;
            }

            bool exists = await network.CheckUserExists(user);
            if (exists)
            {
                ui.Print($"Password per {user}: ", true);
                string pass = ui.ReadPassword();
                LoginResult result = await network.Login(user, pass);

                switch (result)
                {
                    case LoginResult.Success:
                        chatManager.MyUsername = user;
                        // Usciamo dal while e procediamo
                        return;

                    case LoginResult.InvalidCredentials:
                        ui.Print("Errore: Password non valida.");
                        break;

                    case LoginResult.AlreadyConnected:
                        ui.Print("Errore: Utente già connesso da un'altra postazione!");
                        break;

                    default:
                        ui.Print("Errore sconosciuto durante il login.");
                        break;
                }
            }
            else
            {
                ui.Print("Nuovo utente. Crea password: ", true);
                string pass = ui.ReadPassword();
                await network.Register(user, pass);
                await network.Login(user, pass);
                chatManager.MyUsername = user;
                break;
            }
        }
    }

    private void SetupEvents()
    {
        network.OnPublicMessageReceived += (sender, msg) =>
        {
            if (chatManager.IsUserBlocked(sender))
            {
                return; // Silent drop lato client
            }

            if (chatManager.CurrentChatType == MessageType.Public) // Siamo nella pubblica
            {
                bool isMe = sender == chatManager.MyUsername;
                ui.PrintMessage(sender, msg, DateTime.UtcNow, isMe, isRead: false, type: MessageType.Public, reprintPrompt:true);
            }
        };

        network.OnPrivateMessageReceived += async (sender, msg) =>
        {
            bool isChatActive = chatManager.CurrentChatType == MessageType.Private &&
                                (chatManager.CurrentChatPartnerName == sender || sender == chatManager.MyUsername);

            if (isChatActive)
            {
                bool isMe = sender == chatManager.MyUsername;
                ui.PrintMessage(sender, msg, DateTime.Now, isMe, isRead: false, type: MessageType.Private, reprintPrompt: true);

                if (!isMe)
                {
                    await network.MarkAsRead(sender);
                }
            }
            else
            {
                // CASO B: Sono "altrove" (Pubblica o altra Privata)
                if (sender == "SERVER")
                {
                    return;
                }

                ui.PrintSystemMessage($"[NOTIFICA] Nuovo messaggio privato da {sender}!", reprintPrompt: true);
            }
        };

        network.OnSystemNotificationReceived += (notification) =>
        {
            ui.PrintSystemMessage($"[SISTEMA] {notification}", reprintPrompt: true);
        };
    }
}
