namespace ChatCommons;

public static class ChatEvents
{
    // Metodi chiamati dal Server sul Client
    public const string ReceivePublic = "ReceivePublicMessage";
    public const string ReceivePrivate = "ReceivePrivateMessage";
    public const string UserConnected = "UserConnected";
    public const string UserDisconnected = "UserDisconnected";
    public const string ReceiveSystemNotification = "ReceiveSystemNotification";

    // Metodi chiamati dal Client sul Server (Opzionale, SignalR usa i nomi dei metodi Hub, ma utile per Invoke)
    public const string SendPublic = "SendPublicMessageAsync";
    public const string SendPrivate = "SendPrivateMessageAsync";
    public const string Login = "Login";
    public const string CheckUser = "CheckUserExists";
    public const string Register="Register";
    public const string GetPublicHistory = "GetPublicHistory";
    public const string GetPrivateHistory = "GetPrivateHistory";
    public const string GetUnreadSenders = "GetUnreadSenders";
    public const string MarkMessagesAsRead = "MarkMessagesAsRead";
    public const string ChangePassword = "ChangePassword";
    public const string BlockUser = "BlockUser";
    public const string UnblockUser = "UnblockUser";
    public const string GetBlockedList = "GetBlockedList";
}