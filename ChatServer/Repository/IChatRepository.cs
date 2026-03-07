using ChatCommons;

namespace ChatServer.Repository
{
    public interface IChatRepository
    {
        // Gestione Utenti
        bool UserExists(string username);
        bool AddUser(UserData user);
        bool ValidateCredentials(string username, string password);
        DateTime UpdateUserLogout(string username);
        DateTime GetLastLogout(string username);
        // Gestione Messaggi
        void AddMessage(ChatMessage message);
        List<ChatMessage> GetPublicHistory(DateTime cutoffDate);
        List<ChatMessage> GetPrivateHistory(string user1, string user2);
        bool ChangePassword(string username, string oldClientHash, string newClientHash);
        // Gestione Notifiche
        List<string> GetUnreadSenders(string receiver);
        List<ChatMessage> GetMessagesToUpdate(string sender, string receiver);
        // Gestione Blocchi ---
        void BlockUser(string blocker, string blocked);
        void UnblockUser(string blocker, string blocked);
        bool IsBlocked(string sender, string recipient); // Controlla se il sender è nella blacklist del recipient
        List<string> GetBlockedUsers(string blocker);
        List<string> GetUsersWhoBlockedMe(string username); // Ottiene chi ha bloccato l'utente
    }
}
