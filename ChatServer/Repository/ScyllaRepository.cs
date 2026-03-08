using Cassandra;
using ChatCommons;
using ChatServer.Configs;
using Microsoft.Extensions.Options;

namespace ChatServer.Repository;

public class ScyllaRepository : IChatRepository, IDisposable
{
    private readonly ILogger<ScyllaRepository> _logger;
    private readonly Cassandra.ISession _session;
    // Prepared statements
    private readonly PreparedStatement _psInsertUser;
    private readonly PreparedStatement _psSelectUser;
    private readonly PreparedStatement _psInsertPrivateMessage;
    private readonly PreparedStatement _psGetPrivateHistory;
    private readonly PreparedStatement _psGetUserAuth;
    private readonly PreparedStatement _psGetBlockedUsers;
    private readonly PreparedStatement _psUpdateLogout;
    private readonly PreparedStatement _psUpdateReadWatermarks;
    // Public chat
    private readonly PreparedStatement _psInsertPublicMessage;
    private readonly PreparedStatement _psGetPublicHistory;
    // Block operations
    private readonly PreparedStatement _psInsertBlock;
    private readonly PreparedStatement _psDeleteBlock;
    private readonly PreparedStatement _psGetBlockedBy;
    private readonly PreparedStatement _psDeleteBlockedBy;
    private readonly PreparedStatement _psSelectBlockStatus;
    private readonly PreparedStatement _psInsertBlockedBy;
    // User operations
    private readonly PreparedStatement _psGetUserPassword;
    private readonly PreparedStatement _psUpdatePassword;
    private readonly PreparedStatement _psGetLastLogout;
    // unread notifications
    private readonly PreparedStatement _psAddUnread;
    private readonly PreparedStatement _psRemoveUnread;
    private readonly PreparedStatement _psGetUnread;
    private bool disposedValue;

    public ScyllaRepository(IOptions<ScyllaSettings> options, ILogger<ScyllaRepository> logger)
    {
        _logger = logger;

        // Build cluster and session
        var cluster = Cluster.Builder()
            .AddContactPoints(options.Value.Nodes)
            .WithPort(options.Value.Port)
            .WithCredentials(options.Value.Username, options.Value.Password)
            .Build();
        _session = cluster.Connect(options.Value.Keyspace);

        // Prepare statements
        _psInsertUser = _session.Prepare("INSERT INTO users (username, password, last_logout, read_watermarks) VALUES (?, ?, ?, ?)");
        _psSelectUser = _session.Prepare("SELECT username FROM users WHERE username = ?");
        _psInsertPrivateMessage = _session.Prepare("INSERT INTO private_messages (room_id, timestamp, id, sender, receiver, content, msg_type, is_read) VALUES (?, ?, ?, ?, ?, ?, ?, ?)");
        _psGetPrivateHistory = _session.Prepare("SELECT room_id, timestamp, id, sender, receiver, content, msg_type, is_read FROM private_messages WHERE room_id = ?");
        _psGetUserAuth = _session.Prepare("SELECT password FROM users WHERE username = ?");
        _psGetBlockedUsers = _session.Prepare("SELECT blocked FROM user_blocks WHERE blocker = ?");
        _psUpdateLogout = _session.Prepare("UPDATE users SET last_logout = ? WHERE username = ?");
        _psUpdateReadWatermarks = _session.Prepare("UPDATE users SET read_watermarks[?] = ? WHERE username = ?");
        // Public chat
        _psInsertPublicMessage = _session.Prepare("INSERT INTO public_messages (channel_name, timestamp, id, sender, content, msg_type) VALUES (?, ?, ?, ?, ?, ?)");
        _psGetPublicHistory = _session.Prepare("SELECT channel_name, timestamp, id, sender, content, msg_type FROM public_messages WHERE channel_name = ?");
        // Block operations
        _psInsertBlock = _session.Prepare("INSERT INTO user_blocks (blocker, blocked, created_at) VALUES (?, ?, ?)");
        _psDeleteBlock = _session.Prepare("DELETE FROM user_blocks WHERE blocker = ? AND blocked = ?");
        _psGetBlockedBy = _session.Prepare("SELECT blocker FROM blocked_by_users WHERE blocked = ?");
        _psDeleteBlockedBy = _session.Prepare("DELETE FROM blocked_by_users WHERE blocked = ? AND blocker = ?");
        _psSelectBlockStatus = _session.Prepare("SELECT blocker FROM user_blocks WHERE blocker = ? AND blocked = ?");
        _psInsertBlockedBy = _session.Prepare("INSERT INTO blocked_by_users (blocked, blocker, created_at) VALUES (?, ?, ?)");
        // User operations
        _psGetUserPassword = _session.Prepare("SELECT password FROM users WHERE username = ?");
        _psUpdatePassword = _session.Prepare("UPDATE users SET password = ? WHERE username = ?");
        _psGetLastLogout = _session.Prepare("SELECT last_logout FROM users WHERE username = ?");
        // Unread notifications
        _psAddUnread = _session.Prepare("INSERT INTO unread_notifications (receiver, sender) VALUES (?, ?)");
        _psRemoveUnread = _session.Prepare("DELETE FROM unread_notifications WHERE receiver = ? AND sender = ?");
        _psGetUnread = _session.Prepare("SELECT sender FROM unread_notifications WHERE receiver = ?");
    }

    private static string GetRoomId(string user1, string user2)
    {
        var arr = new[] { user1, user2 };
        Array.Sort(arr, StringComparer.OrdinalIgnoreCase);
        return $"{arr[0]}_{arr[1]}";
    }

    public void AddMessage(ChatMessage message)
    {
        if (message.Type == MessageType.Public)
        {
            var now = message.Timestamp == default ? DateTime.UtcNow : message.Timestamp;
            var id = Guid.NewGuid();
            var bound = _psInsertPublicMessage.Bind(
                "global",
                now,
                id,
                message.Sender,
                message.Content,
                (int)message.Type
            );
            _session.Execute(bound);
            _logger.LogInformation("Messaggio pubblico inserito da {Sender} alle {Time}", message.Sender, now);
        }
        else if (message.Type == MessageType.Private)
        {
            var now = message.Timestamp == default ? DateTime.UtcNow : message.Timestamp;
            var id = Guid.NewGuid();
            var roomId = GetRoomId(message.Sender, message.Receiver);
            var bound = _psInsertPrivateMessage.Bind(
                roomId,
                now,
                id,
                message.Sender,
                message.Receiver,
                message.Content,
                (int)message.Type,
                message.IsRead
            );
            _session.Execute(bound);
            // Aggiunge la notifica di "non letto" per il destinatario
            var boundUnread = _psAddUnread.Bind(message.Receiver, message.Sender);
            _session.Execute(boundUnread);

            _logger.LogInformation("Messaggio privato inserito da {Sender} a {Receiver} alle {Time}", message.Sender, message.Receiver, now);
        }
        else
        {
            throw new NotImplementedException("Solo messaggi pubblici e privati supportati in AddMessage");
        }
    }

    public List<ChatMessage> GetPrivateHistory(string user1, string user2)
    {
        var roomId = GetRoomId(user1, user2);
        var bound = _psGetPrivateHistory.Bind(roomId);
        var rows = _session.Execute(bound);
        var result = new List<ChatMessage>();
        foreach (var row in rows)
        {
            var msg = new ChatMessage
            {
                Sender = row.GetValue<string>("sender"),
                Receiver = row.GetValue<string>("receiver"),
                Content = row.GetValue<string>("content"),
                Timestamp = row.GetValue<DateTime>("timestamp"),
                Type = (MessageType)row.GetValue<int>("msg_type"),
                IsRead = row.GetValue<bool>("is_read")
            };
            result.Add(msg);
        }
        return result;
    }

    public List<ChatMessage> GetPublicHistory(DateTime cutoffDate)
    {
        var bound = _psGetPublicHistory.Bind("global");
        var rows = _session.Execute(bound);
        var result = new List<ChatMessage>();
        foreach (var row in rows)
        {
            var msg = new ChatMessage
            {
                Sender = row.GetValue<string>("sender"),
                Receiver = null,
                Content = row.GetValue<string>("content"),
                Timestamp = row.GetValue<DateTime>("timestamp"),
                Type = (MessageType)row.GetValue<int>("msg_type"),
                IsRead = row.GetValue<DateTime>("timestamp") < cutoffDate
            };
            result.Add(msg);
        }
        return result;
    }

    public List<string> GetUnreadSenders(string receiver)
    {
        var bound = _psGetUnread.Bind(receiver);
        var rowSet = _session.Execute(bound);
        return rowSet.Select(row => row.GetValue<string>("sender")).ToList();
    }

    public bool UserExists(string username)
    {
        var statement = _psSelectUser.Bind(username);
        var rowSet = _session.Execute(statement);
        return rowSet.GetRows().Any();
    }

    public bool AddUser(UserData user)
    {
        // 1. Controlla se l'utente esiste già
        if (UserExists(user.Username))
        {
            return false;
        }

        // 2. Hash della password (Work Factor 12 come standard Enterprise)
        string serverSideHash = BCrypt.Net.BCrypt.HashPassword(user.Password, 12);

        // 3. Prepara i dati di default (watermarks vuoti e data di logout)
        var emptyWatermarks = new Dictionary<string, DateTime>();
        DateTime defaultLogout = DateTime.UtcNow;

        // 4. Esegue l'inserimento su ScyllaDB
        var boundStatement = _psInsertUser.Bind(user.Username, serverSideHash, defaultLogout, emptyWatermarks);
        _session.Execute(boundStatement);

        _logger.LogInformation("Utente {User} salvato con successo su ScyllaDB.", user.Username);
        return true;
    }

    public bool ValidateCredentials(string username, string password)
    {
        // 1. Cerca l'utente nel DB
        var boundStatement = _psGetUserAuth.Bind(username);
        var rowSet = _session.Execute(boundStatement);
        var row = rowSet.FirstOrDefault();

        // Se non c'è la riga, l'utente non esiste
        if (row == null)
        {
            return false;
        }

        // 2. Estrai l'hash salvato su DB e verificalo
        string storedHash = row.GetValue<string>("password");
        return BCrypt.Net.BCrypt.Verify(password, storedHash);
    }

    public List<string> GetBlockedUsers(string blocker)
    {
        var boundStatement = _psGetBlockedUsers.Bind(blocker);
        var rowSet = _session.Execute(boundStatement);

        // Mappa i risultati della query in una lista di stringhe
        return rowSet.Select(row => row.GetValue<string>("blocked")).ToList();
    }

    public DateTime UpdateUserLogout(string username)
    {
        var now = DateTime.UtcNow;
        var boundStatement = _psUpdateLogout.Bind(now, username);
        _session.Execute(boundStatement);

        _logger.LogInformation("Logout aggiornato per {User} a {Time}", username, now);
        return now;
    }

    public void BlockUser(string blocker, string blocked)
    {
        var now = DateTime.UtcNow;

        // Inserisci in user_blocks (chi ha bloccato chi)
        var boundBlockStatement = _psInsertBlock.Bind(blocker, blocked, now);
        _session.Execute(boundBlockStatement);

        var boundBlockedByStatement = _psInsertBlockedBy.Bind(blocked, blocker, now);
        _session.Execute(boundBlockedByStatement);

        _logger.LogInformation("Utente {Blocker} ha bloccato {Blocked}", blocker, blocked);
    }
    
    public bool ChangePassword(string username, string oldClientHash, string newClientHash)
    {
        // 1. Cerca l'utente e ottieni la password salvata
        var boundGetStatement = _psGetUserPassword.Bind(username);
        var rowSet = _session.Execute(boundGetStatement);
        var row = rowSet.FirstOrDefault();

        if (row == null)
        {
            return false;
        }

        // 2. Verifica che la vecchia password corrisponda
        string storedHash = row.GetValue<string>("password");
        if (!BCrypt.Net.BCrypt.Verify(oldClientHash, storedHash))
        {
            return false;
        }

        // 3. Hash della nuova password e aggiorna il DB
        string newServerHash = BCrypt.Net.BCrypt.HashPassword(newClientHash, 12);
        var boundUpdateStatement = _psUpdatePassword.Bind(newServerHash, username);
        _session.Execute(boundUpdateStatement);

        _logger.LogInformation("Password aggiornata per {User}", username);
        return true;
    }
    
    public DateTime GetLastLogout(string username)
    {
        var boundStatement = _psGetLastLogout.Bind(username);
        var rowSet = _session.Execute(boundStatement);
        var row = rowSet.FirstOrDefault();

        if (row == null)
        {
            return DateTime.MinValue;
        }

        return row.GetValue<DateTime>("last_logout");
    }
    
    public List<ChatMessage> GetMessagesToUpdate(string sender, string receiver)
    {
        // Nota: Con il sistema dei watermark in ScyllaDB (come in FileChatRepository),
        // non modifichiamo IsRead nei messaggi singoli. Restituiamo lista vuota.
        return new List<ChatMessage>();
    }
    
    public List<string> GetUsersWhoBlockedMe(string username)
    {
        // Ottieni tutti gli utenti dalla tabella blocked_by_users che hanno 'username' come 'blocked'
        var boundStatement = _psGetBlockedBy.Bind(username);
        var rowSet = _session.Execute(boundStatement);

        return rowSet.Select(row => row.GetValue<string>("blocker")).ToList();
    }
    
    public bool IsBlocked(string sender, string recipient)
    {
        // Controlla se recipient ha bloccato sender
        var boundStatement = _psSelectBlockStatus.Bind(recipient, sender);
        var rowSet = _session.Execute(boundStatement);
        return rowSet.GetRows().Any();
    }
    
    public void UnblockUser(string blocker, string blocked)
    {
        // Elimina da user_blocks
        var boundDeleteStatement = _psDeleteBlock.Bind(blocker, blocked);
        _session.Execute(boundDeleteStatement);

        // Elimina da blocked_by_users
        var boundDeleteBlockedByStatement = _psDeleteBlockedBy.Bind(blocked, blocker);
        _session.Execute(boundDeleteBlockedByStatement);

        _logger.LogInformation("Utente {Blocker} ha sbloccato {Blocked}", blocker, blocked);
    }

    public void UpdateReadWatermark(string receiver, string sender)
    {
        var now = DateTime.UtcNow;

        // 1. Aggiorna SOLO la chiave specifica della mappa (Vero approccio NoSQL, zero race conditions!)
        var boundUpdateStatement = _psUpdateReadWatermarks.Bind(sender, now, receiver);
        _session.Execute(boundUpdateStatement);

        // 2. Rimuove la notifica dalla tabella unread_notifications (Il pezzo che l'IA aveva perso!)
        var boundRemoveUnread = _psRemoveUnread.Bind(receiver, sender);
        _session.Execute(boundRemoveUnread);

        _logger.LogInformation("Watermark aggiornato e notifiche pulite per {Receiver} da {Sender}", receiver, sender);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                if (_session != null)
                {
                    _session.Dispose();
                    _logger.LogInformation("[ScyllaDB] Connessione chiusa e risorse di rete liberate correttamente.");
                }
            }

            disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
