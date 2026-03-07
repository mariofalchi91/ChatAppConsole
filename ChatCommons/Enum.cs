namespace ChatCommons;

/// <summary>
/// Represents the possible outcomes of a login attempt.
/// </summary>
public enum LoginResult
{
    /// <summary>
    /// Login succeeded: the user was authenticated and is allowed to enter the chat.
    /// </summary>
    Success,

    /// <summary>
    /// The provided username or password is invalid.
    /// Use this value when authentication fails due to wrong credentials.
    /// </summary>
    InvalidCredentials,

    /// <summary>
    /// The user is already connected from another location/session.
    /// Use this to indicate duplicate/active sessions when concurrent logins are not allowed.
    /// </summary>
    AlreadyConnected
}

/// <summary>
/// Specifies the type or category of a chat message.
/// This helps the client and server handle messages differently (display, routing, visibility).
/// </summary>
public enum MessageType
{
    /// <summary>
    /// A public message broadcasted to all connected users or to a common channel.
    /// </summary>
    Public,

    /// <summary>
    /// A private message addressed to a single user.
    /// </summary>
    Private,

    /// <summary>
    /// A system message used for notifications and status updates
    /// (for example: "Mario has connected", server notices, or automated alerts).
    /// </summary>
    System,

    /// <summary>
    /// A group message intended for future group/chatroom support.
    /// Reserved for messages sent to a subset/group of users.
    /// TODO not yet implemented
    /// </summary>
    Group
}