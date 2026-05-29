using ChatClientConsole.Configs;
using ChatClientConsole.Services;
using ChatCommons;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.Extensions;

namespace TestProject1;

public class ChatManagerTests
{
    [Fact]
    public async Task InitializeAsync_LoadsBlockedUsersInCache()
    {
        var network = CreateNetworkMock();
        network.GetBlockedListAsync().Returns(["Alice", "Bob"]);
        var manager = new ChatManager(network, Substitute.For<UiService>(), CreateKeyService());

        await manager.InitializeAsync();

        Assert.True(manager.IsUserBlocked("alice"));
        Assert.True(manager.IsUserBlocked("BOB"));
    }

    [Fact]
    public async Task BlockUserAsync_WhenSuccess_AddsToCacheAndPrintsMessage()
    {
        var network = CreateNetworkMock();
        network.BlockUserAsync("bob").Returns(true);
        var ui = Substitute.For<UiService>();
        var manager = new ChatManager(network, ui, CreateKeyService());

        await manager.BlockUserAsync("bob");

        Assert.True(manager.IsUserBlocked("bob"));
        ui.Received(1).PrintSystemMessage("[BLOCCATO] bob non potrà più scriverti.", true);
    }

    [Fact]
    public async Task BlockUserAsync_WhenFailure_DoesNotAddToCacheAndPrintsError()
    {
        var network = CreateNetworkMock();
        network.BlockUserAsync("bob").Returns(false);
        var ui = Substitute.For<UiService>();
        var manager = new ChatManager(network, ui, CreateKeyService());

        await manager.BlockUserAsync("bob");

        Assert.False(manager.IsUserBlocked("bob"));
        ui.Received(1).PrintSystemMessage("[ERRORE] Impossibile bloccare bob (Utente non trovato o sei tu).", true);
    }

    [Fact]
    public async Task UnblockUserAsync_WhenSuccess_RemovesFromCache()
    {
        var network = CreateNetworkMock();
        network.BlockUserAsync("bob").Returns(true);
        network.UnblockUserAsync("bob").Returns(true);
        var manager = new ChatManager(network, Substitute.For<UiService>(), CreateKeyService());

        await manager.BlockUserAsync("bob");
        await manager.UnblockUserAsync("bob");

        Assert.False(manager.IsUserBlocked("bob"));
    }

    [Fact]
    public async Task RefreshCurrentViewAsync_WhenPublic_DelegatesToSwitchPublic()
    {
        var manager = CreateManagerMock();
        manager.Configure().SwitchToPublicAsync().Returns(Task.CompletedTask);

        await manager.RefreshCurrentViewAsync();

        await manager.Received(1).SwitchToPublicAsync();
        await manager.DidNotReceive().SwitchToPrivateAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task RefreshCurrentViewAsync_WhenPrivate_DelegatesToSwitchPrivate()
    {
        var manager = CreateManagerMock();
        SetChatType(manager, MessageType.Private);
        SetPartner(manager, "bob");
        manager.Configure().SwitchToPrivateAsync("bob").Returns(Task.CompletedTask);

        await manager.RefreshCurrentViewAsync();

        await manager.Received(1).SwitchToPrivateAsync("bob");
    }

    [Fact]
    public async Task SwitchToPrivateAsync_WhenUserDoesNotExist_PrintsError()
    {
        var network = CreateNetworkMock();
        network.CheckUserExists("ghost").Returns(false);
        var ui = Substitute.For<UiService>();
        var manager = new ChatManager(network, ui, CreateKeyService()) { MyUsername = "alice" };

        await manager.SwitchToPrivateAsync("ghost");

        ui.Received(1).PrintSystemMessage("[ERRORE] Utente ghost non trovato.", true);
        Assert.Equal(MessageType.Public, manager.CurrentChatType);
    }

    [Fact]
    public async Task SwitchToPrivateAsync_Success_LoadsHistoryAndMarksRead()
    {
        var sharedKey = "shared-bob";
        var history = new List<ChatMessage>
        {
            new() { Sender = "alice", Receiver = "bob", Content = CryptoService.EncryptMessage("1", sharedKey), Type = MessageType.Private, IsRead = true, Timestamp = DateTime.UtcNow.AddMinutes(-2) },
            new() { Sender = "bob", Receiver = "alice", Content = CryptoService.EncryptMessage("2", sharedKey), Type = MessageType.Private, IsRead = false, Timestamp = DateTime.UtcNow.AddMinutes(-1) }
        };

        var network = CreateNetworkMock();
        network.CheckUserExists("bob").Returns(true);
        network.GetPrivateHistory("alice", "bob").Returns(history);
        network.MarkAsRead("bob").Returns(Task.CompletedTask);

        var ui = Substitute.For<UiService>();
        ui.ReadPassword().Returns(sharedKey);
        var manager = new ChatManager(network, ui, CreateKeyService()) { MyUsername = "alice" };
        manager.InitializeCryptoSession("alice", "password");

        await manager.SwitchToPrivateAsync("bob");

        Assert.Equal(MessageType.Private, manager.CurrentChatType);
        Assert.Equal("bob", manager.CurrentChatPartnerName);
        await network.Received(1).MarkAsRead("bob");
        ui.Received(1).PrintMessage("alice", "1", Arg.Any<DateTime>(), true, true, MessageType.Private, false);
        ui.Received(1).PrintMessage("bob", "2", Arg.Any<DateTime>(), false, false, MessageType.Private, false);
    }

    [Fact]
    public async Task SwitchToPrivateAsync_WhenHistoryCannotBeDecrypted_DoesNotMarkAsRead()
    {
        var history = new List<ChatMessage>
        {
            new() { Sender = "bob", Receiver = "alice", Content = CryptoService.EncryptMessage("secret", "real-key"), Type = MessageType.Private, IsRead = false, Timestamp = DateTime.UtcNow }
        };

        var network = CreateNetworkMock();
        network.CheckUserExists("bob").Returns(true);
        network.GetPrivateHistory("alice", "bob").Returns(history);
        network.MarkAsRead("bob").Returns(Task.CompletedTask);

        var ui = Substitute.For<UiService>();
        ui.ReadPassword().Returns("wrong-key");

        var manager = new ChatManager(network, ui, CreateKeyService()) { MyUsername = "alice" };
        manager.InitializeCryptoSession("alice", "password");

        await manager.SwitchToPrivateAsync("bob");

        Assert.Equal(MessageType.Public, manager.CurrentChatType);
        await network.DidNotReceive().MarkAsRead("bob");
    }

    private static NetworkService CreateNetworkMock()
    {
        var options = Options.Create(new ClientSettings { ServerUrl = "http://localhost" });
        return Substitute.For<NetworkService>(options);
    }

    private static PrivateChatKeyService CreateKeyService()
    {
        var settings = Options.Create(new ClientSettings
        {
            ServerUrl = "http://localhost",
            E2EPrivate = new E2EPrivateSettings
            {
                EnableLocalKeyVault = false,
                LocalKeyVaultPath = ".chatclient/test-e2e-keys.json"
            }
        });
        return new PrivateChatKeyService(settings);
    }

    private static ChatManager CreateManagerMock()
    {
        var network = CreateNetworkMock();
        var ui = Substitute.For<UiService>();
        return Substitute.ForPartsOf<ChatManager>(network, ui, CreateKeyService());
    }

    private static void SetChatType(ChatManager manager, MessageType type)
    {
        var backingField = typeof(ChatManager).GetField("<CurrentChatType>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(backingField);
        backingField!.SetValue(manager, type);
    }

    private static void SetPartner(ChatManager manager, string partner)
    {
        var backingField = typeof(ChatManager).GetField("<CurrentChatPartnerName>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(backingField);
        backingField!.SetValue(manager, partner);
    }
}
