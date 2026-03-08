using ChatServer.Repository;
using ChatCommons;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace ChatServer;

public class ChatHub(ILogger<ChatHub> logger, IChatRepository repository) : Hub
{

    private static readonly ConcurrentDictionary<string, string> ConnectedUsers = new();

    public bool CheckUserExists(string username)
    {
        return repository.UserExists(username);
    }

    public bool Register(string username, string password)
    {
        if (repository.UserExists(username))
        {
            return false;
        }

        var success = repository.AddUser(new UserData { Username = username, Password = password });
        if (!success)
        {
            logger.LogWarning("Registrazione fallita: {User}", username);
            return false;
        }

        logger.LogInformation("Nuovo utente registrato nel DB: {User}", username);
        return true;
    }

    public LoginResult Login(string username, string password)
    {
        if (ConnectedUsers.ContainsKey(username))
        {
            return LoginResult.AlreadyConnected;
        }

        var isValid = repository.ValidateCredentials(username, password);

        if (isValid)
        {
            // Se le credenziali sono ok, aggiorniamo la mappa delle connessioni attive
            ConnectedUsers[username] = Context.ConnectionId;
            logger.LogInformation("Utente loggato: {User} ({ConnectionId})", username, Context.ConnectionId);

            // Avvisiamo gli altri che Tizio è online (opzionale)
            _ = Clients.All.SendAsync(ChatEvents.UserConnected, username);
            return LoginResult.Success;
        }

        return LoginResult.InvalidCredentials;
    }

    public async Task SendPublicMessageAsync(string user, string message)
    {
        repository.AddMessage(new ChatMessage
        {
            Sender = user,
            Receiver = null,
            Content = message,
            Type = MessageType.Public
        });

        logger.LogInformation("PUBBLICO [{User}]: {Message}", user, message);

        // Calcola gli ID connection da escludere dal broadcast
        var excludedConnections = new List<string>();

        // 1. Chi ha bloccato l'utente (lui non deve vedere i messaggi di 'user')
        var usersWhoBlockedMe = repository.GetUsersWhoBlockedMe(user);
        foreach (var blocker in usersWhoBlockedMe)
        {
            ConnectedUsers.TryGetValue(blocker, out string blockerId);
            if (blockerId != null)
                excludedConnections.Add(blockerId);
        }

        // 2. Chi è stato bloccato da 'user' (user non vede i messaggi di chi lo blocca... wait, no!)
        // In realtà: 'user' ha bloccato X significa che X non deve vedere i messaggi di 'user'
        // Quindi escludiamo anche gli utenti bloccati da 'user'
        var blockedByMe = repository.GetBlockedUsers(user);
        foreach (var blocked in blockedByMe)
        {
            ConnectedUsers.TryGetValue(blocked, out string blockedId);
            if (blockedId != null)
                excludedConnections.Add(blockedId);
        }

        // 3. Invia a tutti TRANNE i bloccanti e i bloccati
        if (excludedConnections.Count > 0)
        {
            await Clients.AllExcept(excludedConnections).SendAsync(ChatEvents.ReceivePublic, user, message);
        }
        else
        {
            await Clients.All.SendAsync(ChatEvents.ReceivePublic, user, message);
        }
    }

    public async Task SendPrivateMessageAsync(string sender, string receiver, string message)
    {
        if (repository.IsBlocked(receiver, sender))
        {
            await Clients.Caller.SendAsync(ChatEvents.ReceiveSystemNotification, $"Hai bloccato {receiver}. Sbloccalo prima di scrivergli.");
            return;
        }

        if (repository.IsBlocked(sender, receiver))
        {
            // SILENT DROP
            logger.LogInformation("BLOCCO ATTIVO: Messaggio da {Sender} a {Receiver} scartato silenziosamente.", sender, receiver);
            // Manda copia al mittente come se nulla fosse (così non sospetta nulla)
            await Clients.Caller.SendAsync(ChatEvents.ReceivePrivate, sender, message);
            return;
        }

        repository.AddMessage(new ChatMessage
        {
            Sender = sender,
            Receiver = receiver,
            Content = message,
            Type = MessageType.Private
        });

        logger.LogInformation("PRIVATO [{Sender} -> {Receiver}]: {Message}", sender, receiver, message);

        ConnectedUsers.TryGetValue(receiver, out string targetId);

        // Manda copia al mittente (per conferma visiva immediata)
        await Clients.Caller.SendAsync(ChatEvents.ReceivePrivate, sender, message);

        if (targetId != null)
        {
            await Clients.Client(targetId).SendAsync(ChatEvents.ReceivePrivate, sender, message);
        }
    }

    public Task<List<ChatMessage>> GetPublicHistory()
    {
        string requestingUser = ConnectedUsers.FirstOrDefault(x => x.Value == Context.ConnectionId).Key;
        var cutoffDate = repository.GetLastLogout(requestingUser);
        var history = repository.GetPublicHistory(cutoffDate);

        // Filtra i messaggi degli utenti bloccati
        var filteredHistory = history
            .Where(msg =>
            {
                // Non mostro messaggi di chi ho bloccato
                if (repository.IsBlocked(msg.Sender, requestingUser))
                {
                    return false; // Sender ha bloccato me, non mostro
                }

                // Non mostro messaggi di chi mi ha bloccato
                // (verifico se requestingUser è nella blacklist di msg.Sender)
                var usersWhoBlockedMe = repository.GetUsersWhoBlockedMe(requestingUser);
                if (usersWhoBlockedMe.Contains(msg.Sender))
                {
                    return false; // Sender mi ha bloccato, non mostro
                }

                return true;
            })
            .ToList();

        return Task.FromResult(filteredHistory);
    }

    public Task<List<ChatMessage>> GetPrivateHistory(string myUser, string otherUser)
    {
        var dbMessages = repository.GetPrivateHistory(myUser, otherUser);
        // 1. SE USIAMO I FILE: Gli stati IsRead sono già calcolati perfettamente dal Watermark.
        // Non dobbiamo fare copie o modifiche.
        if (repository is FileChatRepository)
        {
            return Task.FromResult(dbMessages);
        }
        // 2. SE USIAMO LA RAM (InMemoryChatRepository): 
        // Eseguiamo la vecchia logica per aggiornare i riferimenti in memoria.
        var responseList = new List<ChatMessage>();

        foreach (var msg in dbMessages)
        {
            // Creiamo una copia da mandare al client
            var msgToSend = new ChatMessage
            {
                Sender = msg.Sender,
                Receiver = msg.Receiver,
                Content = msg.Content,
                Timestamp = msg.Timestamp,
                Type = msg.Type,
                IsRead = msg.IsRead // Copiamo lo stato attuale
            };

            // SE SONO IO IL DESTINATARIO e il messaggio non è letto:
            // 1. Al client lo mando come "Non Letto" (così lo vede colorato ADESSO)
            // 2. Nel DB lo segno come "Letto" (così la PROSSIMA volta sarà grigio)
            if (msg.Receiver == myUser && !msg.IsRead)
            {
                msgToSend.IsRead = false; // Te lo mostro colorato
                msg.IsRead = true;        // Ma segno che l'hai visto
            }

            responseList.Add(msgToSend);
        }

        return Task.FromResult(responseList);
    }

    public override async Task OnDisconnectedAsync(Exception exception)
    {
        var userPair = ConnectedUsers.FirstOrDefault(x => x.Value == Context.ConnectionId);
        if (userPair.Key != null)
        {
            string username = userPair.Key;
            var logoutDate = repository.UpdateUserLogout(username);
            ConnectedUsers.TryRemove(username, out _);
            logger.LogInformation("Utente {User} disconnesso alle {Time}", username, logoutDate);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public Task<List<string>> GetUnreadSenders(string username)
    {
        var senders = repository.GetUnreadSenders(username);
        return Task.FromResult(senders);
    }

    public Task MarkMessagesAsRead(string sender)
    {
        string myName = ConnectedUsers.FirstOrDefault(x => x.Value == Context.ConnectionId).Key;
        if (myName == null)
        {
            return Task.CompletedTask;
        }

        // Aggiorna il watermark dell'utente ricevente (implementazione polimorfica)
        repository.UpdateReadWatermark(myName, sender);

        return Task.CompletedTask;
    }

    public bool ChangePassword(string oldPasswordHash, string newPasswordHash)
    {
        var username = ConnectedUsers.FirstOrDefault(x => x.Value == Context.ConnectionId).Key;
        if (username == null)
        {
            return false;
        }

        return repository.ChangePassword(username, oldPasswordHash, newPasswordHash);
    }

    public bool BlockUser(string userToBlock)
    {
        var me = ConnectedUsers.FirstOrDefault(x => x.Value == Context.ConnectionId).Key;
        if (me == null || string.Equals(me, userToBlock, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!repository.UserExists(userToBlock))
        {
            return false;
        }

        repository.BlockUser(me, userToBlock);
        logger.LogInformation("{User} ha bloccato {Blocked}", me, userToBlock);
        return true;
    }

    public bool UnblockUser(string userToUnblock)
    {
        var me = ConnectedUsers.FirstOrDefault(x => x.Value == Context.ConnectionId).Key;
        if (me == null)
        {
            return false;
        }
        if (userToUnblock.Equals(me, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        if (!repository.UserExists(userToUnblock))
        {
            return false;
        }
        repository.UnblockUser(me, userToUnblock);
        logger.LogInformation("{User} ha sbloccato {Unblocked}", me, userToUnblock);
        return true;
    }

    public Task<List<string>> GetBlockedList()
    {
        var me = ConnectedUsers.FirstOrDefault(x => x.Value == Context.ConnectionId).Key;
        if (me == null)
        {
            return Task.FromResult(new List<string>());
        }
        else
        {
            return Task.FromResult(repository.GetBlockedUsers(me));
        }
    }
}