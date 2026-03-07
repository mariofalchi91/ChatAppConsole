using ChatCommons;
using System.Collections.Concurrent;

namespace ChatServer.Repository
{
    public class InMemoryChatRepository : IChatRepository
    {
        protected readonly ConcurrentBag<UserData> _users = [];
        protected readonly ConcurrentBag<ChatMessage> _messages = [];
        protected readonly int _workFactor = 12;
        // Key: Utente che blocca (Blocker) -> Value: Lista di utenti bloccati (Blocked)
        protected readonly Dictionary<string, HashSet<string>> _blacklists = new(StringComparer.OrdinalIgnoreCase);
        // Lock per thread-safety sul dizionario (che non è Concurrent di natura come il Bag)
        protected readonly Lock _lockBlacklist = new();

        public virtual void AddMessage(ChatMessage message)
        {         
            // Solo i messaggi PUBBLICI vanno in memoria
            // I messaggi PRIVATI vengono gestiti dalle sottoclassi (es. FileChatRepository)
            if (message.Type == MessageType.Public)
            {
                _messages.Add(message);
            }
        }

        public virtual bool AddUser(UserData user)
        {
            if (UserExists(user.Username))
            {
                return false;
            }

            string serverSideHash = BCrypt.Net.BCrypt.HashPassword(user.Password, _workFactor);
            user.Password = serverSideHash;

            _users.Add(user);
            return true;
        }

        public virtual DateTime GetLastLogout(string username)
        {
            var userDb = _users.FirstOrDefault(u => u.Username == username);
            return userDb?.LastLogout ?? DateTime.MinValue;
        }

        public virtual List<ChatMessage> GetPrivateHistory(string user1, string user2)
        {
            var dbMessages = _messages
                .Where(m =>
                    m.Type == MessageType.Private &&
                    ((m.Sender == user1 && m.Receiver == user2) ||
                     (m.Sender == user2 && m.Receiver == user1)))
                .OrderBy(m => m.Timestamp)
                .ToList();
            return dbMessages;
        }

        public virtual List<ChatMessage> GetPublicHistory(DateTime cutoffDate)
        {
            var history = _messages
                .Where(m => m.Type == MessageType.Public)
                .OrderBy(m => m.Timestamp)
                .Select(m => new ChatMessage // Creiamo una copia per non modificare l'originale nel DB!
                {
                    Sender = m.Sender,
                    Receiver = m.Receiver,
                    Content = m.Content,
                    Timestamp = m.Timestamp,
                    Type = m.Type,
                    // LOGICA CRUCIALE: Se il messaggio è più vecchio della mia ultima uscita -> LETTO (Grigio)
                    // Se è più recente -> NON LETTO (Colorato)
                    IsRead = m.Timestamp < cutoffDate
                })
                .ToList();
            return history;
        }

        public virtual List<string> GetUnreadSenders(string receiver)
        {
            var senders = _messages
                .Where(m => m.Receiver == receiver && m.IsRead == false)
                .Select(m => m.Sender)
                .Distinct()
                .ToList();
            return senders;
        }

        public virtual bool ValidateCredentials(string username, string password)
        {
            var user = _users.FirstOrDefault(u => u.Username == username);

            if (user == null)
            {
                return false;
            }

            return BCrypt.Net.BCrypt.Verify(password, user.Password);
        }

        public virtual List<ChatMessage> GetMessagesToUpdate(string sender, string receiver)
        {
            var messagesToUpdate = _messages
                .Where(m => m.Sender == sender && m.Receiver == receiver && !m.IsRead)
                .ToList();
            return messagesToUpdate;
        }

        public virtual DateTime UpdateUserLogout(string username)
        {
            var userDb = _users.FirstOrDefault(u => u.Username == username);
            userDb?.LastLogout = DateTime.UtcNow;
            return userDb?.LastLogout ?? DateTime.MinValue;
        }

        public virtual bool UserExists(string username)
        {
            return _users.Any(u => u.Username == username);
        }

        public virtual bool ChangePassword(string username, string oldClientHash, string newClientHash)
        {
            var user = _users.FirstOrDefault(u => u.Username == username);
            if (user == null)
            {
                return false;
            }

            if (!BCrypt.Net.BCrypt.Verify(oldClientHash, user.Password))
            {
                return false;
            }

            string newServerHash = BCrypt.Net.BCrypt.HashPassword(newClientHash, _workFactor);
            user.Password = newServerHash;
            return true;
        }

        public virtual void BlockUser(string blocker, string blocked)
        {
            lock (_lockBlacklist)
            {
                if (!_blacklists.TryGetValue(blocker, out HashSet<string> value))
                {
                    value = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    _blacklists[blocker] = value;
                }

                value.Add(blocked);
            }
        }

        public virtual void UnblockUser(string blocker, string blocked)
        {
            lock (_lockBlacklist)
            {
                if (_blacklists.TryGetValue(blocker, out HashSet<string> value))
                {
                    value.Remove(blocked);
                }
            }
        }

        public virtual bool IsBlocked(string sender, string recipient)
        {
            lock (_lockBlacklist)
            {
                // Controllo se il RECIPIENT ha bloccato il SENDER
                if (!_blacklists.TryGetValue(recipient, out HashSet<string> value))
                {
                    return false;
                }

                return value.Contains(sender);
            }
        }

        public virtual List<string> GetBlockedUsers(string blocker)
        {
            lock (_lockBlacklist)
            {
                if (!_blacklists.TryGetValue(blocker, out HashSet<string> value))
                {
                    return [];
                }

                return [.. value];
            }
        }

        public virtual List<string> GetUsersWhoBlockedMe(string username)
        {
            lock (_lockBlacklist)
            {
                // Cerco tutti gli utenti che hanno 'username' nella loro blacklist
                var usersWhoBlockedMe = _blacklists
                    .Where(kvp => kvp.Value.Contains(username))
                    .Select(kvp => kvp.Key)
                    .ToList();
                return usersWhoBlockedMe;
            }
        }
    }
}
