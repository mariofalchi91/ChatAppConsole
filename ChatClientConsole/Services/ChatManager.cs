using ChatCommons;

namespace ChatClientConsole.Services;

public class ChatManager(NetworkService network, UiService ui)
{
    public string MyUsername { get; set; }
    public MessageType CurrentChatType { get; private set; } = MessageType.Public;
    public string CurrentChatPartnerName { get; private set; }

    private HashSet<string> _blockedUsersCache = new(StringComparer.OrdinalIgnoreCase);

    public async Task RefreshCurrentViewAsync()
    {
        if (CurrentChatType == MessageType.Public)
        {
            await SwitchToPublicAsync();
        }
        else
        {
            await SwitchToPrivateAsync(CurrentChatPartnerName);
        }
    }

    public async Task SwitchToPublicAsync()
    {
        CurrentChatType = MessageType.Public;
        CurrentChatPartnerName = string.Empty;
        ui.Clear();
        ui.SetTitle($"{MyUsername} - Pubblica");
        ui.PrintSystemMessage("═══ CHAT PUBBLICA ═══", reprintPrompt: false);

        // 1. Carica e stampa storico Pubblico
        var history = await network.GetPublicHistory();
        foreach (var msg in history)
        {
            ui.PrintMessage(msg.Sender, msg.Content, msg.Timestamp,
                                   isMe: (msg.Sender == MyUsername),
                                   isRead: msg.IsRead,
                                   type: MessageType.Public,
                                   reprintPrompt: false);
        }

        // --- 3. NUOVO: CONTROLLO NOTIFICHE PRIVATE ---
        // Chiediamo al server: "Chi mi ha cercato?"
        var unreadSenders = await network.GetUnreadSenders(MyUsername);

        if (unreadSenders.Count > 0)
        {
            // Usiamo un piccolo ritardo estetico o stampiamo subito
            foreach (var sender in unreadSenders)
            {
                // Usiamo PrintSystemMessage che gestisce già il cursore e i colori
                // Nota: reprintPrompt: true è fondamentale qui per rimettere il cursore dopo l'avviso
                ui.PrintSystemMessage($"[AVVISO] Hai messaggi NON LETTI da: {sender.ToUpper()} (@{sender})", reprintPrompt: false);
            }
        }
    }

    public async Task SwitchToPrivateAsync(string target)
    {
        bool exists = await network.CheckUserExists(target);
        if (!exists)
        {
            ui.PrintSystemMessage($"[ERRORE] Utente {target} non trovato.");
            return;
        }
        if (target == MyUsername)
        {
            ui.PrintSystemMessage($"[INFO] Non puoi parlare con te stesso...");
            return;
        }

        CurrentChatType = MessageType.Private;
        CurrentChatPartnerName = target;
        ui.Clear();
        ui.SetTitle($"{MyUsername} - Privata con {target}");
        ui.PrintSystemMessage($"═══ CHAT PRIVATA CON {target.ToUpper()} ═══", reprintPrompt: false);

        var history = await network.GetPrivateHistory(MyUsername, target);
        foreach (var msg in history)
        {
            // MODIFICA QUI: isRead: msg.IsRead
            ui.PrintMessage(msg.Sender, msg.Content, msg.Timestamp,
                                   isMe: (msg.Sender == MyUsername),
                                   isRead: msg.IsRead, // <--- Usiamo il flag del server
                                   type: MessageType.Private,
                                   reprintPrompt: false);
        }
        // Se ho appena aperto la chat e scaricato lo storico, 
        // dico al server che ho letto tutto quello che c'era in sospeso.
        await network.MarkAsRead(target);
        //ui.PrintPrompt();
    }

    public async Task InitializeAsync()
    {
        var list = await network.GetBlockedListAsync();
        _blockedUsersCache = new HashSet<string>(list, StringComparer.OrdinalIgnoreCase);
    }

    public bool IsUserBlocked(string username)
    {
        return _blockedUsersCache.Contains(username);
    }

    public async Task BlockUserAsync(string target)
    {
        bool success = await network.BlockUserAsync(target);
        if (success)
        {
            _blockedUsersCache.Add(target);
            ui.PrintSystemMessage($"[BLOCCATO] {target} non potrà più scriverti.");
        }
        else
        {
            ui.PrintSystemMessage($"[ERRORE] Impossibile bloccare {target} (Utente non trovato o sei tu).");
        }
    }

    public async Task UnblockUserAsync(string target)
    {
        bool success = await network.UnblockUserAsync(target);
        if (success)
        {
            _blockedUsersCache.Remove(target);
            ui.PrintSystemMessage($"[SBLOCCATO] Hai sbloccato {target}.");
        }
        else
        {
            ui.PrintSystemMessage($"[ERRORE] Impossibile sbloccare {target}.");
        }
    }

    public void PrintBlockedList()
    {
        if (_blockedUsersCache.Count == 0)
        {
            ui.PrintSystemMessage("[INFO] Non hai bloccato nessuno.");
            return;
        }

        ui.PrintSystemMessage("════ UTENTI BLOCCATI ════");
        foreach (var user in _blockedUsersCache)
        {
            ui.PrintSystemMessage($" - {user}", reprintPrompt: false);
        }
        ui.PrintPrompt();
    }
}
