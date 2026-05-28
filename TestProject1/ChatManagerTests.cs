using ChatClientConsole.Configs;
using ChatClientConsole.Services;
using ChatCommons;
using Microsoft.Extensions.Options;
using Moq;

namespace TestProject1;

public class ChatManagerTests
{
    [Fact]
    public async Task InitializeAsync_LoadsBlockedUsersInCache()
    {
        var network = CreateNetworkMock();
        network.Setup(n => n.GetBlockedListAsync()).ReturnsAsync(["Alice", "Bob"]);
        var manager = new ChatManager(network.Object, new Mock<UiService>().Object);

        await manager.InitializeAsync();

        Assert.True(manager.IsUserBlocked("alice"));
        Assert.True(manager.IsUserBlocked("BOB"));
    }

    [Fact]
    public async Task BlockUserAsync_WhenSuccess_AddsToCacheAndPrintsMessage()
    {
        var network = CreateNetworkMock();
        network.Setup(n => n.BlockUserAsync("bob")).ReturnsAsync(true);
        var ui = new Mock<UiService>();
        var manager = new ChatManager(network.Object, ui.Object);

        await manager.BlockUserAsync("bob");

        Assert.True(manager.IsUserBlocked("bob"));
        ui.Verify(u => u.PrintSystemMessage("[BLOCCATO] bob non potrà più scriverti.", true), Times.Once);
    }

    [Fact]
    public async Task BlockUserAsync_WhenFailure_DoesNotAddToCacheAndPrintsError()
    {
        var network = CreateNetworkMock();
        network.Setup(n => n.BlockUserAsync("bob")).ReturnsAsync(false);
        var ui = new Mock<UiService>();
        var manager = new ChatManager(network.Object, ui.Object);

        await manager.BlockUserAsync("bob");

        Assert.False(manager.IsUserBlocked("bob"));
        ui.Verify(u => u.PrintSystemMessage("[ERRORE] Impossibile bloccare bob (Utente non trovato o sei tu).", true), Times.Once);
    }

    [Fact]
    public async Task UnblockUserAsync_WhenSuccess_RemovesFromCache()
    {
        var network = CreateNetworkMock();
        network.Setup(n => n.BlockUserAsync("bob")).ReturnsAsync(true);
        network.Setup(n => n.UnblockUserAsync("bob")).ReturnsAsync(true);
        var manager = new ChatManager(network.Object, new Mock<UiService>().Object);

        await manager.BlockUserAsync("bob");
        await manager.UnblockUserAsync("bob");

        Assert.False(manager.IsUserBlocked("bob"));
    }

    [Fact]
    public async Task RefreshCurrentViewAsync_WhenPublic_DelegatesToSwitchPublic()
    {
        var manager = CreateManagerMock();
        manager.Setup(m => m.SwitchToPublicAsync()).Returns(Task.CompletedTask);
        manager.Setup(m => m.RefreshCurrentViewAsync()).CallBase();

        await manager.Object.RefreshCurrentViewAsync();

        manager.Verify(m => m.SwitchToPublicAsync(), Times.Once);
        manager.Verify(m => m.SwitchToPrivateAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RefreshCurrentViewAsync_WhenPrivate_DelegatesToSwitchPrivate()
    {
        var manager = CreateManagerMock();
        SetChatType(manager.Object, MessageType.Private);
        SetPartner(manager.Object, "bob");
        manager.Setup(m => m.SwitchToPrivateAsync("bob")).Returns(Task.CompletedTask);
        manager.Setup(m => m.RefreshCurrentViewAsync()).CallBase();

        await manager.Object.RefreshCurrentViewAsync();

        manager.Verify(m => m.SwitchToPrivateAsync("bob"), Times.Once);
    }

    [Fact]
    public async Task SwitchToPrivateAsync_WhenUserDoesNotExist_PrintsError()
    {
        var network = CreateNetworkMock();
        network.Setup(n => n.CheckUserExists("ghost")).ReturnsAsync(false);
        var ui = new Mock<UiService>();
        var manager = new ChatManager(network.Object, ui.Object) { MyUsername = "alice" };

        await manager.SwitchToPrivateAsync("ghost");

        ui.Verify(u => u.PrintSystemMessage("[ERRORE] Utente ghost non trovato.", true), Times.Once);
        Assert.Equal(MessageType.Public, manager.CurrentChatType);
    }

    [Fact]
    public async Task SwitchToPrivateAsync_Success_LoadsHistoryAndMarksRead()
    {
        var history = new List<ChatMessage>
        {
            new() { Sender = "alice", Receiver = "bob", Content = "1", Type = MessageType.Private, IsRead = true, Timestamp = DateTime.UtcNow.AddMinutes(-2) },
            new() { Sender = "bob", Receiver = "alice", Content = "2", Type = MessageType.Private, IsRead = false, Timestamp = DateTime.UtcNow.AddMinutes(-1) }
        };

        var network = CreateNetworkMock();
        network.Setup(n => n.CheckUserExists("bob")).ReturnsAsync(true);
        network.Setup(n => n.GetPrivateHistory("alice", "bob")).ReturnsAsync(history);
        network.Setup(n => n.MarkAsRead("bob")).Returns(Task.CompletedTask);

        var ui = new Mock<UiService>();
        var manager = new ChatManager(network.Object, ui.Object) { MyUsername = "alice" };

        await manager.SwitchToPrivateAsync("bob");

        Assert.Equal(MessageType.Private, manager.CurrentChatType);
        Assert.Equal("bob", manager.CurrentChatPartnerName);
        network.Verify(n => n.MarkAsRead("bob"), Times.Once);
        ui.Verify(u => u.PrintMessage("alice", "1", It.IsAny<DateTime>(), true, true, MessageType.Private, false), Times.Once);
        ui.Verify(u => u.PrintMessage("bob", "2", It.IsAny<DateTime>(), false, false, MessageType.Private, false), Times.Once);
    }

    private static Mock<NetworkService> CreateNetworkMock()
    {
        var options = Options.Create(new ClientSettings { ServerUrl = "http://localhost" });
        return new Mock<NetworkService>(options) { CallBase = false };
    }

    private static Mock<ChatManager> CreateManagerMock()
    {
        var network = CreateNetworkMock();
        var ui = new Mock<UiService>();
        return new Mock<ChatManager>(network.Object, ui.Object) { CallBase = false };
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
