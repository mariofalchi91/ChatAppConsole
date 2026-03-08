using ChatServer.Configs;
using ChatCommons;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace ChatServer.Repository;

public class FileChatRepository : InMemoryChatRepository, IChatRepository
{
    private readonly string _usersFilePath;
    private readonly string _blacklistFilePath;
    private readonly string _privateChatsFolder;
    private readonly string _publicChatFilePath;
    private readonly Lock _fileLock = new();
    private readonly Lock _publicFileLock = new();
    private readonly Dictionary<string, Lock> _privateChatLocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _locksLock = new(); // Protegge accesso a _privateChatLocks
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };
    private readonly ILogger<FileChatRepository> _logger;

    public FileChatRepository(IOptions<ChatSettings> settings, ILogger<FileChatRepository> logger)
    {
        _logger = logger;
        string folder = settings.Value.DataFolderPath;
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }

        _usersFilePath = Path.Combine(folder, "users.json");
        _blacklistFilePath = Path.Combine(folder, "blacklist.json");
        _privateChatsFolder = Path.Combine(folder, "PrivateChats");
        _publicChatFilePath = Path.Combine(folder, "public.jsonl");

        // Crea cartella PrivateChats se non esiste
        if (!Directory.Exists(_privateChatsFolder))
        {
            Directory.CreateDirectory(_privateChatsFolder);
        }

        LoadUsers();
        LoadBlacklist();
    }

    /// <summary>
    /// Genera il nome del file per una coppia di utenti ordinando i nomi alfabeticamente.
    /// Esempio: GetPrivateChatFilePath("mario", "alberto") -> "private_alberto_mario.jsonl"
    /// </summary>
    private static string GetPrivateChatFileName(string user1, string user2)
    {
        var users = new[] { user1, user2 };
        Array.Sort(users, StringComparer.OrdinalIgnoreCase);
        return $"private_{users[0]}_{users[1]}.jsonl";
    }

    private string GetPrivateChatFilePath(string user1, string user2)
    {
        return Path.Combine(_privateChatsFolder, GetPrivateChatFileName(user1, user2));
    }

    /// <summary>
    /// Ottiene il lock specifico per una coppia di utenti (thread-safe).
    /// </summary>
    private Lock GetPrivateChatLock(string user1, string user2)
    {
        string fileName = GetPrivateChatFileName(user1, user2);
        
        lock (_locksLock)
        {
            if (!_privateChatLocks.TryGetValue(fileName, out var fileLock))
            {
                fileLock = new Lock();
                _privateChatLocks[fileName] = fileLock;
            }
            return fileLock;
        }
    }

    /// <summary>
    /// Estrae i due nomi utente dal nome del file JSONL.
    /// Esempio: "private_alberto_mario.jsonl" -> ("alberto", "mario")
    /// </summary>
    private static (string user1, string user2) ExtractUsersFromFileName(string fileName)
    {
        // Format: private_user1_user2.jsonl
        string withoutExtension = Path.GetFileNameWithoutExtension(fileName);
        string[] parts = withoutExtension.Split('_');
        
        if (parts.Length >= 3)
        {
            string user1 = parts[1];
            string user2 = parts[2];
            return (user1, user2);
        }

        return ("", "");
    }

    private void LoadBlacklist()
    {
        if (!File.Exists(_blacklistFilePath))
        {
            return;
        }

        lock (_fileLock)
        {
            try
            {
                string json = File.ReadAllText(_blacklistFilePath);
                var loaded = JsonSerializer.Deserialize<Dictionary<string, HashSet<string>>>(json);

                if (loaded != null)
                {
                    lock (_lockBlacklist)
                    {
                        foreach (var kvp in loaded)
                        {
                            _blacklists[kvp.Key] = new HashSet<string>(kvp.Value, StringComparer.OrdinalIgnoreCase);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ERR] Impossibile caricare blacklist dal file {FilePath}", _blacklistFilePath);
            }
        }
    }

    public override void BlockUser(string blocker, string blocked)
    {
        base.BlockUser(blocker, blocked); // Aggiorna RAM
        SaveBlacklist(); // Salva su disco
    }

    private void SaveBlacklist()
    {
        lock (_fileLock) // Usiamo lock file generico o specifico, basta essere coerenti
        {
            try
            {
                lock (_lockBlacklist)
                {
                    string json = JsonSerializer.Serialize(_blacklists, _options);
                    File.WriteAllText(_blacklistFilePath, json);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ERR] Impossibile salvare blacklist nel file {FilePath}", _blacklistFilePath);
            }
        }
    }

    public override void UnblockUser(string blocker, string blocked)
    {
        base.UnblockUser(blocker, blocked); // Aggiorna RAM
        SaveBlacklist(); // Salva su disco
    }

    private void LoadUsers()
    {
        if (!File.Exists(_usersFilePath))
        {
            return;
        }

        lock (_fileLock)
        {
            try
            {
                string json = File.ReadAllText(_usersFilePath);
                var loaded = JsonSerializer.Deserialize<List<UserData>>(json);

                if (loaded != null)
                {
                    // Popoliamo la ConcurrentBag del padre
                    foreach (var u in loaded)
                    {
                        // Aggiungiamo direttamente alla lista protetta
                        // (bypassiamo AddUser per non ri-hashare la password o innescare salvataggi)
                        _users.Add(u);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ERR] Impossibile caricare utenti dal file {FilePath}", _usersFilePath);
            }
        }
    }

    public override bool AddUser(UserData user)
    {
        bool success = base.AddUser(user);
        if (success)
        {
            SaveUsers();
        }
        return success;
    }

    private void SaveUsers()
    {
        lock (_fileLock)
        {
            try
            {

                string json = JsonSerializer.Serialize(_users.ToList(), _options);
                File.WriteAllText(_usersFilePath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ERR] Impossibile salvare utenti nel file {FilePath}", _usersFilePath);
            }
        }
    }

    public override DateTime UpdateUserLogout(string username)
    {
        var date = base.UpdateUserLogout(username);
        if (date != DateTime.MinValue)
        {
            SaveUsers();
        }
        return date;
    }

    public override bool ChangePassword(string username, string oldHash, string newHash)
    {
        bool success = base.ChangePassword(username, oldHash, newHash);
        if (success)
        {
            SaveUsers();
        }
        return success;
    }

    /// <summary>
    /// Override di AddMessage per gestire i messaggi privati su file JSONL.
    /// I messaggi pubblici rimangono in memoria (gestiti dalla classe base).
    /// </summary>
    public override void AddMessage(ChatMessage message)
    {
        if (message.Type == MessageType.Private)
        {
            // Salva il messaggio privato su file JSONL
            SavePrivateMessageToFile(message);
        }
        else
        {
            // Salva il messaggio pubblico su file JSONL
            SavePublicMessageToFile(message);
        }
    }

    /// <summary>
    /// Salva un messaggio privato nel file JSONL corrispondente (append).
    /// Thread-safe con lock specifico per il file della coppia di utenti.
    /// </summary>
    private void SavePrivateMessageToFile(ChatMessage message)
    {
        string filePath = GetPrivateChatFilePath(message.Sender, message.Receiver);
        var chatLock = GetPrivateChatLock(message.Sender, message.Receiver);

        lock (chatLock)
        {
            try
            {
                // Serializza il messaggio come JSON
                string json = JsonSerializer.Serialize(message);
                
                // Append della riga al file (crea il file se non esiste)
                File.AppendAllText(filePath, json + Environment.NewLine);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ERR] Impossibile salvare messaggio privato da {Sender} a {Receiver} nel file {FilePath}", message.Sender, message.Receiver, filePath);
            }
        }
    }

    private void SavePublicMessageToFile(ChatMessage message)
    {
        lock (_publicFileLock)
        {
            try
            {
                string json = JsonSerializer.Serialize(message);
                File.AppendAllText(_publicChatFilePath, json + Environment.NewLine);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ERR] Impossibile salvare messaggio pubblico da {Sender} nel file {FilePath}", message.Sender, _publicChatFilePath);
            }
        }
    }

    /// <summary>
    /// Override di GetPrivateHistory per caricare i messaggi da file JSONL con IsRead calcolato dinamicamente.
    /// Legge il file riga per riga e calcola IsRead basato sul watermark dell'utente ricevente.
    /// </summary>
    public override List<ChatMessage> GetPrivateHistory(string user1, string user2)
    {
        string filePath = GetPrivateChatFilePath(user1, user2);
        var chatLock = GetPrivateChatLock(user1, user2);

        lock (chatLock)
        {
            var messages = new List<ChatMessage>();

            // Se il file non esiste, restituisci lista vuota
            if (!File.Exists(filePath))
            {
                return messages;
            }

            try
            {
                // Leggi il file riga per riga
                foreach (string line in File.ReadLines(filePath))
                {
                    // Salta righe vuote
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    try
                    {
                        // Deserializza ogni riga come un ChatMessage
                        var msg = JsonSerializer.Deserialize<ChatMessage>(line);
                        if (msg != null)
                        {
                            // Calcola IsRead dinamicamente basato sul watermark
                            // Se il messaggio è indirizzato a user1, controlla il watermark di user1
                            // Se il messaggio è indirizzato a user2, controlla il watermark di user2
                            
                            string receiver = msg.Receiver;
                            string sender = msg.Sender;
                            
                            var receiverUser = _users.FirstOrDefault(u => u.Username == receiver);
                            if (receiverUser != null)
                            {
                                // Se il timestamp del messaggio è <= al watermark, è letto
                                lock (_lockBlacklist)
                                {
                                    if (receiverUser.ReadWatermarks.TryGetValue(sender, out DateTime watermark))
                                    {
                                        msg.IsRead = msg.Timestamp <= watermark;
                                    }
                                    else
                                    {
                                        // Nessun watermark = messaggio non letto
                                        msg.IsRead = false;
                                    }
                                }
                            }
                            
                            messages.Add(msg);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[WARN] Impossibile deserializzare riga nel file {FilePath}", filePath);
                        // Continua con la prossima riga
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ERR] Impossibile leggere storico privato da {FilePath}", filePath);
            }

            return messages;
        }
    }

    /// <summary>
    /// Override di GetPublicHistory per caricare i messaggi pubblici da file JSONL.
    /// I messaggi vengono letti riga per riga e l'attributo IsRead viene impostato in base alla data di cutoff fornita.
    /// </summary>
    public override List<ChatMessage> GetPublicHistory(DateTime cutoffDate)
    {
        var messages = new List<ChatMessage>();
        if (!File.Exists(_publicChatFilePath))
            return messages;
        lock (_publicFileLock)
        {
            try
            {
                foreach (string line in File.ReadLines(_publicChatFilePath))
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;
                    try
                    {
                        var msg = JsonSerializer.Deserialize<ChatMessage>(line);
                        if (msg != null)
                        {
                            msg.IsRead = msg.Timestamp < cutoffDate;
                            messages.Add(msg);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[WARN] Impossibile deserializzare riga nel file {FilePath}", _publicChatFilePath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ERR] Impossibile leggere storico pubblico da {FilePath}", _publicChatFilePath);
            }
        }
        return messages;
    }

    /// <summary>
    /// Override di MarkMessagesAsRead per aggiornare il watermark dell'utente ricevente.
    /// Imposta ReadWatermarks[sender] = DateTime.UtcNow e salva su disco.
    /// Non modifica i file JSONL (immutabili).
    /// </summary>
    public override List<ChatMessage> GetMessagesToUpdate(string sender, string receiver)
    {
        // In questo context con watermark, non abbiamo bisogno di caricare i messaggi
        // per aggiornarli. Invece, aggiorniamo direttamente il watermark.
        // Restituiamo una lista vuota perché non modifichiamo più IsRead nei messaggi.
        return new List<ChatMessage>();
    }

    /// <summary>
    /// Override per aggiornare il watermark quando i messaggi vengono segnati come letti.
    /// Trova l'utente ricevente e aggiorna ReadWatermarks[sender] al timestamp corrente.
    /// </summary>
    public override void UpdateReadWatermark(string receiver, string sender)
    {
        var receiverUser = _users.FirstOrDefault(u => u.Username == receiver);
        if (receiverUser == null)
            return;

        lock (_lockBlacklist) // Usiamo lo stesso lock per la protezione degli utenti
        {
            receiverUser.ReadWatermarks[sender] = DateTime.UtcNow;
        }

        // Salva il watermark aggiornato su disco
        SaveUsers();
    }

    /// <summary>
    /// Override di GetUnreadSenders per verificare messaggi non letti basato su watermark.
    /// Itera sui file JSONL nella cartella PrivateChats e legge solo l'ultima riga di ogni file.
    /// Se l'ultimo messaggio è destinato a 'receiver' e il suo timestamp è > del watermark, il mittente è "non letto".
    /// Strategia a basso consumo di memoria: legge file per file, non tutto in memoria.
    /// </summary>
    public override List<string> GetUnreadSenders(string receiver)
    {
        var unreadSenders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Ottieni l'utente ricevente per accedere ai watermark
        var receiverUser = _users.FirstOrDefault(u => u.Username == receiver);
        if (receiverUser == null || !Directory.Exists(_privateChatsFolder))
        {
            return new List<string>();
        }

        try
        {
            // Itera su tutti i file JSONL nella cartella PrivateChats
            string[] privateFiles = Directory.GetFiles(_privateChatsFolder, "private_*.jsonl");

            foreach (string filePath in privateFiles)
            {
                // Estrai i nomi utente dal nome file
                string fileName = Path.GetFileName(filePath);
                var (user1, user2) = ExtractUsersFromFileName(fileName);

                // Verifica se questo file contiene il receiver
                if (!user1.Equals(receiver, StringComparison.OrdinalIgnoreCase) &&
                    !user2.Equals(receiver, StringComparison.OrdinalIgnoreCase))
                {
                    continue; // Questo file non riguarda il receiver
                }

                // Ottieni il lock specifico per questo file
                var chatLock = GetPrivateChatLock(user1, user2);

                lock (chatLock)
                {
                    try
                    {
                        // Leggi solo l'ultima riga del file (memory-efficient)
                        string lastLine = File.ReadLines(filePath).LastOrDefault();

                        if (string.IsNullOrWhiteSpace(lastLine))
                            continue;

                        // Deserializza l'ultimo messaggio
                        var lastMsg = JsonSerializer.Deserialize<ChatMessage>(lastLine);
                        if (lastMsg == null)
                            continue;

                        // Verifica se il messaggio è destinato a 'receiver'
                        if (!lastMsg.Receiver.Equals(receiver, StringComparison.OrdinalIgnoreCase))
                            continue;

                        // Leggi il watermark dal receiverUser
                        lock (_lockBlacklist)
                        {
                            receiverUser.ReadWatermarks.TryGetValue(lastMsg.Sender, out DateTime watermark);
                            
                            // Se il timestamp del messaggio è > al watermark, il mittente ha messaggi non letti
                            if (lastMsg.Timestamp > watermark)
                            {
                                unreadSenders.Add(lastMsg.Sender);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[WARN] Errore durante lettura dell'ultima riga di {FilePath}", filePath);
                        // Continua con il prossimo file
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ERR] Errore durante GetUnreadSenders per {Receiver}", receiver);
        }

        return unreadSenders.ToList();
    }
}
