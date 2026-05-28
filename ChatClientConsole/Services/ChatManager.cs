using ChatCommons;

namespace ChatClientConsole.Services;

public class ChatManager(NetworkService network, UiService ui, PrivateChatKeyService keyService)
{
    public string MyUsername { get; set; }
    public MessageType CurrentChatType { get; private set; } = MessageType.Public;
    public string CurrentChatPartnerName { get; private set; }

    private HashSet<string> _blockedUsersCache = new(StringComparer.OrdinalIgnoreCase);

    public virtual async Task RefreshCurrentViewAsync()
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

    public virtual async Task SwitchToPublicAsync()
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

    public virtual async Task SwitchToPrivateAsync(string target)
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

        if (!EnsureKeyForPeer(target))
        {
            return;
        }

        var history = await network.GetPrivateHistory(MyUsername, target);
        var decryptedHistory = new List<ChatMessage>(history.Count);
        foreach (var msg in history)
        {
            if (!keyService.TryDecryptForPeer(target, msg.Content, out var plaintext, out var decryptError))
            {
                ui.PrintSystemMessage($"[E2E] {decryptError}");
                ui.PrintSystemMessage($"[E2E] Reimposta la chiave con #keyreset {target} e poi #keyset {target}.");
                return;
            }

            decryptedHistory.Add(new ChatMessage
            {
                Id = msg.Id,
                Sender = msg.Sender,
                Receiver = msg.Receiver,
                Type = msg.Type,
                Timestamp = msg.Timestamp,
                IsRead = msg.IsRead,
                Content = plaintext
            });
        }

        CurrentChatType = MessageType.Private;
        CurrentChatPartnerName = target;
        ui.Clear();
        ui.SetTitle($"{MyUsername} - Privata con {target}");
        ui.PrintSystemMessage($"═══ CHAT PRIVATA CON {target.ToUpper()} ═══", reprintPrompt: false);

        foreach (var msg in decryptedHistory)
        {
            ui.PrintMessage(msg.Sender, msg.Content, msg.Timestamp,
                                   isMe: (msg.Sender == MyUsername),
                                   isRead: msg.IsRead,
                                   type: MessageType.Private,
                                   reprintPrompt: false);
        }

        await network.MarkAsRead(target);
    }

    public virtual bool InitializeCryptoSession(string username, string password)
    {
        if (keyService.TryInitializeSession(username, password, out var error))
        {
            return true;
        }

        ui.PrintSystemMessage($"[E2E] {error}");
        ui.PrintSystemMessage("[E2E] Le chiavi locali non sono state caricate. Reinseriscile quando apri le chat private.");
        return false;
    }

    public virtual bool SetPrivateKey(string target, string sharedSecret, out string error)
    {
        return keyService.TrySetKeyForPeer(target, sharedSecret, out error);
    }

    public virtual bool ResetPrivateKey(string target, out string error)
    {
        return keyService.RemoveKeyForPeer(target, out error);
    }

    public virtual bool TryEncryptCurrentPrivateMessage(string plaintext, out string ciphertext, out string error)
    {
        ciphertext = string.Empty;
        error = string.Empty;

        if (CurrentChatType != MessageType.Private || string.IsNullOrWhiteSpace(CurrentChatPartnerName))
        {
            error = "Nessuna chat privata attiva.";
            return false;
        }

        return keyService.TryEncryptForPeer(CurrentChatPartnerName, plaintext, out ciphertext, out error);
    }

    public virtual bool TryDecryptPrivatePayload(string sender, string ciphertext, out string plaintext, out string error)
    {
        plaintext = string.Empty;
        error = string.Empty;

        var peer = sender.Equals(MyUsername, StringComparison.OrdinalIgnoreCase)
            ? CurrentChatPartnerName
            : sender;

        if (string.IsNullOrWhiteSpace(peer))
        {
            error = "Impossibile identificare la chat privata associata al messaggio.";
            return false;
        }

        return keyService.TryDecryptForPeer(peer, ciphertext, out plaintext, out error);
    }

    public virtual async Task InitializeAsync()
    {
        var list = await network.GetBlockedListAsync();
        _blockedUsersCache = new HashSet<string>(list, StringComparer.OrdinalIgnoreCase);
    }

    public virtual bool IsUserBlocked(string username)
    {
        return _blockedUsersCache.Contains(username);
    }

    public virtual async Task BlockUserAsync(string target)
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

    public virtual async Task UnblockUserAsync(string target)
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

    public virtual void PrintBlockedList()
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

    private bool EnsureKeyForPeer(string target)
    {
        if (keyService.HasKeyForPeer(target))
        {
            return true;
        }

        ui.PrintSystemMessage($"[E2E] Inserisci la chiave condivisa per {target} (obbligatoria):", reprintPrompt: false);
        var key = ui.ReadPassword();

        if (string.IsNullOrWhiteSpace(key))
        {
            ui.PrintSystemMessage("[E2E] Chat privata annullata: chiave non inserita.");
            return false;
        }

        if (!keyService.TrySetKeyForPeer(target, key, out var error))
        {
            ui.PrintSystemMessage($"[E2E] {error}");
            return false;
        }

        ui.PrintSystemMessage($"[E2E] Chiave privata configurata per {target}.");
        return true;
    }
}
