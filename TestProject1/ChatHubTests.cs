using ChatCommons;
using ChatServer;
using ChatServer.Repository;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using NSubstitute;
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
        var repository = Substitute.For<IChatRepository>();
        repository.UserExists("alice").Returns(true);

        var hub = CreateHub(repository);

        var result = hub.Register("alice", "pwd");

        Assert.False(result);
        repository.DidNotReceive().AddUser(Arg.Any<UserData>());
    }

    [Fact]
    public void Register_WhenAddUserSucceeds_ReturnsTrue()
    {
        var repository = Substitute.For<IChatRepository>();
        repository.UserExists("newuser").Returns(false);
        repository.AddUser(Arg.Any<UserData>()).Returns(true);

        var hub = CreateHub(repository);

        var result = hub.Register("newuser", "pwd");

        Assert.True(result);
        repository.Received(1).AddUser(Arg.Is<UserData>(u => u.Username == "newuser" && u.Password == "pwd"));
    }

    [Fact]
    public void Login_WhenAlreadyConnected_ReturnsAlreadyConnected()
    {
        SetConnectedUser("alice", "conn-existing");

        var repository = Substitute.For<IChatRepository>();
        var hub = CreateHub(repository, connectionId: "conn-new");

        var result = hub.Login("alice", "pwd");

        Assert.Equal(LoginResult.AlreadyConnected, result);
        repository.DidNotReceive().ValidateCredentials(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public void Login_WhenInvalidCredentials_ReturnsInvalidCredentials()
    {
        var repository = Substitute.For<IChatRepository>();
        repository.ValidateCredentials("alice", "wrong").Returns(false);

        var hub = CreateHub(repository, connectionId: "conn-1");

        var result = hub.Login("alice", "wrong");

        Assert.Equal(LoginResult.InvalidCredentials, result);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsSuccessAndNotifiesAll()
    {
        var repository = Substitute.For<IChatRepository>();
        repository.ValidateCredentials("alice", "pwd").Returns(true);

        var allProxy = Substitute.For<IClientProxy>();
        var clients = BuildClients(allProxy: allProxy);
        var hub = CreateHub(repository, clients, "conn-1");

        var result = hub.Login("alice", "pwd");

        Assert.Equal(LoginResult.Success, result);
        await allProxy.Received(1).SendCoreAsync(
            ChatEvents.UserConnected,
            Arg.Is<object?[]>(args => args.Length == 1 && (string)args[0]! == "alice"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendPublicMessageAsync_WhenNoBlocks_StoresMessageAndBroadcastsToAll()
    {
        var repository = Substitute.For<IChatRepository>();
        repository.GetUsersWhoBlockedMe("alice").Returns([]);
        repository.GetBlockedUsers("alice").Returns([]);

        var allProxy = Substitute.For<IClientProxy>();
        var clients = BuildClients(allProxy: allProxy);
        var hub = CreateHub(repository, clients);

        await hub.SendPublicMessageAsync("alice", "hello-all");

        repository.Received(1).AddMessage(Arg.Is<ChatMessage>(m =>
            m.Sender == "alice" &&
            m.Content == "hello-all" &&
            m.Type == MessageType.Public &&
            m.Receiver == null));

        await allProxy.Received(1).SendCoreAsync(
            ChatEvents.ReceivePublic,
            Arg.Is<object?[]>(args => args.Length == 2 && (string)args[0]! == "alice" && (string)args[1]! == "hello-all"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendPublicMessageAsync_WhenBlocksExist_UsesAllExcept()
    {
        SetConnectedUser("blocker", "conn-blocker");
        SetConnectedUser("blocked", "conn-blocked");

        var repository = Substitute.For<IChatRepository>();
        repository.GetUsersWhoBlockedMe("alice").Returns(["blocker"]);
        repository.GetBlockedUsers("alice").Returns(["blocked"]);

        var allExceptProxy = Substitute.For<IClientProxy>();
        var clients = BuildClients(allExceptProxy: allExceptProxy);
        var hub = CreateHub(repository, clients);

        await hub.SendPublicMessageAsync("alice", "msg");

        await allExceptProxy.Received(1).SendCoreAsync(
            ChatEvents.ReceivePublic,
            Arg.Is<object?[]>(args => args.Length == 2 && (string)args[0]! == "alice" && (string)args[1]! == "msg"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendPrivateMessageAsync_WhenReceiverBlockedSender_SendsSystemNotification()
    {
        var repository = Substitute.For<IChatRepository>();
        repository.IsBlocked("bob", "alice").Returns(true);

        var callerProxy = Substitute.For<ISingleClientProxy>();
        var clients = BuildClients(callerProxy: callerProxy);
        var hub = CreateHub(repository, clients);

        await hub.SendPrivateMessageAsync("alice", "bob", "hello");

        repository.DidNotReceive().AddMessage(Arg.Any<ChatMessage>());
        await callerProxy.Received(1).SendCoreAsync(
            ChatEvents.ReceiveSystemNotification,
            Arg.Is<object?[]>(args => args.Length == 1 && args[0] is string),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendPrivateMessageAsync_WhenSenderBlockedReceiver_SilentDropButEchoToCaller()
    {
        var repository = Substitute.For<IChatRepository>();
        repository.IsBlocked(Arg.Any<string>(), Arg.Any<string>()).Returns(false, true);

        var callerProxy = Substitute.For<ISingleClientProxy>();
        var clients = BuildClients(callerProxy: callerProxy);
        var hub = CreateHub(repository, clients);

        await hub.SendPrivateMessageAsync("alice", "bob", "hello");

        repository.DidNotReceive().AddMessage(Arg.Any<ChatMessage>());
        await callerProxy.Received(1).SendCoreAsync(
            ChatEvents.ReceivePrivate,
            Arg.Is<object?[]>(args => args.Length == 2 && (string)args[0]! == "alice" && (string)args[1]! == "hello"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void BlockUser_WhenTargetExists_ReturnsTrueAndCallsRepository()
    {
        SetConnectedUser("me", "conn-1");

        var repository = Substitute.For<IChatRepository>();
        repository.UserExists("other").Returns(true);

        var hub = CreateHub(repository, connectionId: "conn-1");

        var result = hub.BlockUser("other");

        Assert.True(result);
        repository.Received(1).BlockUser("me", "other");
    }

    [Fact]
    public void ChangePassword_WhenUserNotConnected_ReturnsFalse()
    {
        var hub = CreateHub(Substitute.For<IChatRepository>(), connectionId: "conn-unknown");

        var result = hub.ChangePassword("old", "new");

        Assert.False(result);
    }

    [Fact]
    public void ChangePassword_WhenUserConnected_CallsRepository()
    {
        SetConnectedUser("alice", "conn-1");
        var repository = Substitute.For<IChatRepository>();
        repository.ChangePassword("alice", "old", "new").Returns(true);
        var hub = CreateHub(repository, connectionId: "conn-1");

        var result = hub.ChangePassword("old", "new");

        Assert.True(result);
        repository.Received(1).ChangePassword("alice", "old", "new");
    }

    [Fact]
    public void UnblockUser_WhenTryingToUnblockSelf_ReturnsFalse()
    {
        SetConnectedUser("alice", "conn-1");
        var hub = CreateHub(Substitute.For<IChatRepository>(), connectionId: "conn-1");

        var result = hub.UnblockUser("ALICE");

        Assert.False(result);
    }

    [Fact]
    public async Task MarkMessagesAsRead_WhenNoConnectedUser_DoesNothing()
    {
        var repository = Substitute.For<IChatRepository>();
        var hub = CreateHub(repository, connectionId: "conn-missing");

        await hub.MarkMessagesAsRead("bob");

        repository.DidNotReceive().UpdateReadWatermark(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task GetBlockedList_WhenNoConnectedUser_ReturnsEmptyList()
    {
        var hub = CreateHub(Substitute.For<IChatRepository>(), connectionId: "conn-missing");

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

        var repository = Substitute.For<IChatRepository>();
        repository.GetPrivateHistory("alice", "bob").Returns([dbMessage]);
        var hub = CreateHub(repository);

        var result = await hub.GetPrivateHistory("alice", "bob");

        Assert.Single(result);
        Assert.False(result[0].IsRead);
        Assert.True(dbMessage.IsRead);
    }

    [Fact]
    public async Task OnDisconnectedAsync_RemovesConnectedUserAndUpdatesLogout()
    {
        SetConnectedUser("alice", "conn-1");

        var repository = Substitute.For<IChatRepository>();
        repository.UpdateUserLogout("alice").Returns(DateTime.UtcNow);

        var hub = CreateHub(repository, connectionId: "conn-1");

        await hub.OnDisconnectedAsync(null!);

        repository.Received(1).UpdateUserLogout("alice");
        Assert.False(GetConnectedUsers().ContainsKey("alice"));
    }

    public void Dispose()
    {
        ClearConnectedUsers();
    }

    private static ChatHub CreateHub(IChatRepository repository, IHubCallerClients? clients = null, string connectionId = "conn-default")
    {
        var logger = Substitute.For<ILogger<ChatHub>>();
        var hub = new ChatHub(logger, repository)
        {
            Context = BuildContext(connectionId),
            Clients = clients ?? BuildClients()
        };

        return hub;
    }

    private static HubCallerContext BuildContext(string connectionId)
    {
        var context = Substitute.For<HubCallerContext>();
        context.ConnectionId.Returns(connectionId);
        return context;
    }
    private static IHubCallerClients BuildClients(
        IClientProxy? allProxy = null,
        IClientProxy? allExceptProxy = null,
        ISingleClientProxy? callerProxy = null,
        ISingleClientProxy? targetProxy = null)
    {
        var all = allProxy ?? Substitute.For<IClientProxy>();
        var allExcept = allExceptProxy ?? Substitute.For<IClientProxy>();
        var caller = callerProxy ?? Substitute.For<ISingleClientProxy>();
        var target = targetProxy ?? Substitute.For<ISingleClientProxy>();

        var clients = Substitute.For<IHubCallerClients>();
        clients.All.Returns(all);
        clients.Caller.Returns(caller);
        clients.AllExcept(Arg.Any<IReadOnlyList<string>>()).Returns(allExcept);
        clients.Client(Arg.Any<string>()).Returns(target);

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
