using ChatCommons;
using ChatServer;
using ChatServer.Repository;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using System.Collections.Concurrent;
using System.Reflection;

namespace TestProject1;

public class ChatHubTests : IDisposable
{
    public ChatHubTests()
    {
        ClearConnectedUsers();
    }

    [Fact]
    public void Register_WhenUserExists_ReturnsFalse()
    {
        var repository = new Mock<IChatRepository>();
        repository.Setup(r => r.UserExists("alice")).Returns(true);

        var hub = CreateHub(repository.Object);

        var result = hub.Register("alice", "pwd");

        Assert.False(result);
        repository.Verify(r => r.AddUser(It.IsAny<UserData>()), Times.Never);
    }

    [Fact]
    public void Register_WhenAddUserSucceeds_ReturnsTrue()
    {
        var repository = new Mock<IChatRepository>();
        repository.Setup(r => r.UserExists("newuser")).Returns(false);
        repository.Setup(r => r.AddUser(It.IsAny<UserData>())).Returns(true);

        var hub = CreateHub(repository.Object);

        var result = hub.Register("newuser", "pwd");

        Assert.True(result);
        repository.Verify(r => r.AddUser(It.Is<UserData>(u => u.Username == "newuser" && u.Password == "pwd")), Times.Once);
    }

    [Fact]
    public void Login_WhenAlreadyConnected_ReturnsAlreadyConnected()
    {
        SetConnectedUser("alice", "conn-existing");

        var repository = new Mock<IChatRepository>();
        var hub = CreateHub(repository.Object, connectionId: "conn-new");

        var result = hub.Login("alice", "pwd");

        Assert.Equal(LoginResult.AlreadyConnected, result);
        repository.Verify(r => r.ValidateCredentials(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Login_WhenInvalidCredentials_ReturnsInvalidCredentials()
    {
        var repository = new Mock<IChatRepository>();
        repository.Setup(r => r.ValidateCredentials("alice", "wrong")).Returns(false);

        var hub = CreateHub(repository.Object, connectionId: "conn-1");

        var result = hub.Login("alice", "wrong");

        Assert.Equal(LoginResult.InvalidCredentials, result);
    }

    [Fact]
    public void Login_WithValidCredentials_ReturnsSuccessAndNotifiesAll()
    {
        var repository = new Mock<IChatRepository>();
        repository.Setup(r => r.ValidateCredentials("alice", "pwd")).Returns(true);

        var allProxy = new Mock<IClientProxy>();
        var clients = BuildClients(allProxy: allProxy.Object);
        var hub = CreateHub(repository.Object, clients.Object, "conn-1");

        var result = hub.Login("alice", "pwd");

        Assert.Equal(LoginResult.Success, result);
        allProxy.Verify(p => p.SendCoreAsync(
            ChatEvents.UserConnected,
            It.Is<object?[]>(args => args.Length == 1 && (string)args[0]! == "alice"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendPublicMessageAsync_WhenNoBlocks_StoresMessageAndBroadcastsToAll()
    {
        var repository = new Mock<IChatRepository>();
        repository.Setup(r => r.GetUsersWhoBlockedMe("alice")).Returns([]);
        repository.Setup(r => r.GetBlockedUsers("alice")).Returns([]);

        var allProxy = new Mock<IClientProxy>();
        var clients = BuildClients(allProxy: allProxy.Object);
        var hub = CreateHub(repository.Object, clients.Object);

        await hub.SendPublicMessageAsync("alice", "hello-all");

        repository.Verify(r => r.AddMessage(It.Is<ChatMessage>(m =>
            m.Sender == "alice" &&
            m.Content == "hello-all" &&
            m.Type == MessageType.Public &&
            m.Receiver == null)), Times.Once);

        allProxy.Verify(p => p.SendCoreAsync(
            ChatEvents.ReceivePublic,
            It.Is<object?[]>(args => args.Length == 2 && (string)args[0]! == "alice" && (string)args[1]! == "hello-all"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendPublicMessageAsync_WhenBlocksExist_UsesAllExcept()
    {
        SetConnectedUser("blocker", "conn-blocker");
        SetConnectedUser("blocked", "conn-blocked");

        var repository = new Mock<IChatRepository>();
        repository.Setup(r => r.GetUsersWhoBlockedMe("alice")).Returns(["blocker"]);
        repository.Setup(r => r.GetBlockedUsers("alice")).Returns(["blocked"]);

        var allExceptProxy = new Mock<IClientProxy>();
        var clients = BuildClients(allExceptProxy: allExceptProxy.Object);
        var hub = CreateHub(repository.Object, clients.Object);

        await hub.SendPublicMessageAsync("alice", "msg");

        allExceptProxy.Verify(p => p.SendCoreAsync(
            ChatEvents.ReceivePublic,
            It.Is<object?[]>(args => args.Length == 2 && (string)args[0]! == "alice" && (string)args[1]! == "msg"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendPrivateMessageAsync_WhenReceiverBlockedSender_SendsSystemNotification()
    {
        var repository = new Mock<IChatRepository>();
        repository.Setup(r => r.IsBlocked("bob", "alice")).Returns(true);

        var callerProxy = new Mock<ISingleClientProxy>();
        var clients = BuildClients(callerProxy: callerProxy.Object);
        var hub = CreateHub(repository.Object, clients.Object);

        await hub.SendPrivateMessageAsync("alice", "bob", "hello");

        repository.Verify(r => r.AddMessage(It.IsAny<ChatMessage>()), Times.Never);
        callerProxy.Verify(p => p.SendCoreAsync(
            ChatEvents.ReceiveSystemNotification,
            It.Is<object?[]>(args => args.Length == 1 && args[0] is string),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendPrivateMessageAsync_WhenSenderBlockedReceiver_SilentDropButEchoToCaller()
    {
        var repository = new Mock<IChatRepository>();
        repository.SetupSequence(r => r.IsBlocked(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(false)
            .Returns(true);

        var callerProxy = new Mock<ISingleClientProxy>();
        var clients = BuildClients(callerProxy: callerProxy.Object);
        var hub = CreateHub(repository.Object, clients.Object);

        await hub.SendPrivateMessageAsync("alice", "bob", "hello");

        repository.Verify(r => r.AddMessage(It.IsAny<ChatMessage>()), Times.Never);
        callerProxy.Verify(p => p.SendCoreAsync(
            ChatEvents.ReceivePrivate,
            It.Is<object?[]>(args => args.Length == 2 && (string)args[0]! == "alice" && (string)args[1]! == "hello"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void BlockUser_WhenTargetExists_ReturnsTrueAndCallsRepository()
    {
        SetConnectedUser("me", "conn-1");

        var repository = new Mock<IChatRepository>();
        repository.Setup(r => r.UserExists("other")).Returns(true);

        var hub = CreateHub(repository.Object, connectionId: "conn-1");

        var result = hub.BlockUser("other");

        Assert.True(result);
        repository.Verify(r => r.BlockUser("me", "other"), Times.Once);
    }

    [Fact]
    public void ChangePassword_WhenUserNotConnected_ReturnsFalse()
    {
        var hub = CreateHub(new Mock<IChatRepository>().Object, connectionId: "conn-unknown");

        var result = hub.ChangePassword("old", "new");

        Assert.False(result);
    }

    [Fact]
    public void ChangePassword_WhenUserConnected_CallsRepository()
    {
        SetConnectedUser("alice", "conn-1");
        var repository = new Mock<IChatRepository>();
        repository.Setup(r => r.ChangePassword("alice", "old", "new")).Returns(true);
        var hub = CreateHub(repository.Object, connectionId: "conn-1");

        var result = hub.ChangePassword("old", "new");

        Assert.True(result);
        repository.Verify(r => r.ChangePassword("alice", "old", "new"), Times.Once);
    }

    [Fact]
    public void UnblockUser_WhenTryingToUnblockSelf_ReturnsFalse()
    {
        SetConnectedUser("alice", "conn-1");
        var hub = CreateHub(new Mock<IChatRepository>().Object, connectionId: "conn-1");

        var result = hub.UnblockUser("ALICE");

        Assert.False(result);
    }

    [Fact]
    public async Task MarkMessagesAsRead_WhenNoConnectedUser_DoesNothing()
    {
        var repository = new Mock<IChatRepository>();
        var hub = CreateHub(repository.Object, connectionId: "conn-missing");

        await hub.MarkMessagesAsRead("bob");

        repository.Verify(r => r.UpdateReadWatermark(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetBlockedList_WhenNoConnectedUser_ReturnsEmptyList()
    {
        var hub = CreateHub(new Mock<IChatRepository>().Object, connectionId: "conn-missing");

        var list = await hub.GetBlockedList();

        Assert.Empty(list);
    }

    [Fact]
    public async Task GetPrivateHistory_InMemoryBranch_MarksDbMessageAsReadButReturnsUnreadFlag()
    {
        var dbMessage = new ChatMessage
        {
            Sender = "bob",
            Receiver = "alice",
            Content = "pending",
            Type = MessageType.Private,
            IsRead = false,
            Timestamp = DateTime.UtcNow
        };

        var repository = new Mock<IChatRepository>();
        repository.Setup(r => r.GetPrivateHistory("alice", "bob")).Returns([dbMessage]);
        var hub = CreateHub(repository.Object);

        var result = await hub.GetPrivateHistory("alice", "bob");

        Assert.Single(result);
        Assert.False(result[0].IsRead);
        Assert.True(dbMessage.IsRead);
    }

    [Fact]
    public async Task OnDisconnectedAsync_RemovesConnectedUserAndUpdatesLogout()
    {
        SetConnectedUser("alice", "conn-1");

        var repository = new Mock<IChatRepository>();
        repository.Setup(r => r.UpdateUserLogout("alice")).Returns(DateTime.UtcNow);

        var hub = CreateHub(repository.Object, connectionId: "conn-1");

        await hub.OnDisconnectedAsync(null!);

        repository.Verify(r => r.UpdateUserLogout("alice"), Times.Once);
        Assert.False(GetConnectedUsers().ContainsKey("alice"));
    }

    public void Dispose()
    {
        ClearConnectedUsers();
    }

    private static ChatHub CreateHub(IChatRepository repository, IHubCallerClients? clients = null, string connectionId = "conn-default")
    {
        var logger = new Mock<ILogger<ChatHub>>();
        var hub = new ChatHub(logger.Object, repository)
        {
            Context = BuildContext(connectionId),
            Clients = clients ?? BuildClients().Object
        };

        return hub;
    }

    private static HubCallerContext BuildContext(string connectionId)
    {
        var context = new Mock<HubCallerContext>();
        context.SetupGet(c => c.ConnectionId).Returns(connectionId);
        return context.Object;
    }

    private static Mock<IHubCallerClients> BuildClients(
        IClientProxy? allProxy = null,
        IClientProxy? allExceptProxy = null,
        ISingleClientProxy? callerProxy = null,
        ISingleClientProxy? targetProxy = null)
    {
        var all = allProxy ?? new Mock<IClientProxy>().Object;
        var allExcept = allExceptProxy ?? new Mock<IClientProxy>().Object;
        var caller = callerProxy ?? new Mock<ISingleClientProxy>().Object;
        var target = targetProxy ?? new Mock<ISingleClientProxy>().Object;

        var clients = new Mock<IHubCallerClients>();
        clients.SetupGet(c => c.All).Returns(all);
        clients.SetupGet(c => c.Caller).Returns(caller);
        clients.Setup(c => c.AllExcept(It.IsAny<IReadOnlyList<string>>())).Returns(allExcept);
        clients.Setup(c => c.Client(It.IsAny<string>())).Returns(target);

        return clients;
    }

    private static ConcurrentDictionary<string, string> GetConnectedUsers()
    {
        var field = typeof(ChatHub).GetField("ConnectedUsers", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);
        return (ConcurrentDictionary<string, string>)field!.GetValue(null)!;
    }

    private static void ClearConnectedUsers()
    {
        GetConnectedUsers().Clear();
    }

    private static void SetConnectedUser(string username, string connectionId)
    {
        GetConnectedUsers()[username] = connectionId;
    }
}
