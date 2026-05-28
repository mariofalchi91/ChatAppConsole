using ChatClientConsole.Commands;
using ChatClientConsole.Configs;
using ChatClientConsole.Services;
using ChatCommons;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;

namespace TestProject1;

public class CommandTests
{
    [Fact]
    public void BlockCommand_CanExecute_WorksForExpectedInputs()
    {
        var command = new BlockCommand(CreateManagerMock().Object, new Mock<UiService>().Object);

        Assert.True(command.CanExecute("#block"));
        Assert.True(command.CanExecute("#block user"));
        Assert.False(command.CanExecute("#blo"));
    }

    [Fact]
    public async Task BlockCommand_ExecuteAsync_InvalidSyntax_PrintsError()
    {
        var ui = new Mock<UiService>();
        var command = new BlockCommand(CreateManagerMock().Object, ui.Object);

        await command.ExecuteAsync("#block");

        ui.Verify(u => u.Print("Sintassi errata!", false), Times.Once);
    }

    [Fact]
    public async Task BlockCommand_ExecuteAsync_ValidInput_CallsManager()
    {
        var manager = CreateManagerMock();
        manager.Setup(m => m.BlockUserAsync("alice")).Returns(Task.CompletedTask);
        var command = new BlockCommand(manager.Object, new Mock<UiService>().Object);

        await command.ExecuteAsync("#block alice");

        manager.Verify(m => m.BlockUserAsync("alice"), Times.Once);
    }

    [Fact]
    public void UnblockCommand_CanExecute_WorksAsExpected()
    {
        var command = new UnblockCommand(CreateManagerMock().Object, new Mock<UiService>().Object);

        Assert.True(command.CanExecute("#unblock"));
        Assert.True(command.CanExecute("#unblock user"));
        Assert.False(command.CanExecute("#restore"));
    }

    [Fact]
    public async Task UnblockCommand_ExecuteAsync_ValidInput_CallsManager()
    {
        var manager = CreateManagerMock();
        manager.Setup(m => m.UnblockUserAsync("bob")).Returns(Task.CompletedTask);
        var command = new UnblockCommand(manager.Object, new Mock<UiService>().Object);

        await command.ExecuteAsync("#unblock bob");

        manager.Verify(m => m.UnblockUserAsync("bob"), Times.Once);
    }

    [Fact]
    public void PrivateChatCommand_CanExecute_RequiresAtAndUsername()
    {
        var command = new PrivateChatCommand(CreateManagerMock().Object);

        Assert.True(command.CanExecute("@alice"));
        Assert.False(command.CanExecute("alice"));
        Assert.False(command.CanExecute("@"));
    }

    [Fact]
    public async Task PrivateChatCommand_ExecuteAsync_CallsSwitchToPrivate()
    {
        var manager = CreateManagerMock();
        manager.Setup(m => m.SwitchToPrivateAsync("alice")).Returns(Task.CompletedTask);
        var command = new PrivateChatCommand(manager.Object);

        await command.ExecuteAsync("@alice");

        manager.Verify(m => m.SwitchToPrivateAsync("alice"), Times.Once);
    }

    [Fact]
    public void ExitCommand_CanExecute_OnlyForExitToken()
    {
        var command = new ExitCommand(new Mock<UiService>().Object, CreateManagerMock().Object);

        Assert.True(command.CanExecute("#exit"));
        Assert.False(command.CanExecute("#exit now"));
    }

    [Fact]
    public async Task ExitCommand_ExecuteAsync_InPublic_PrintsInfo()
    {
        var ui = new Mock<UiService>();
        var manager = CreateManagerMock();
        SetChatType(manager.Object, MessageType.Public);
        var command = new ExitCommand(ui.Object, manager.Object);

        await command.ExecuteAsync("#exit");

        ui.Verify(u => u.PrintSystemMessage("[INFO] Sei già nella pubblica.", true), Times.Once);
    }

    [Fact]
    public async Task ExitCommand_ExecuteAsync_InPrivate_SwitchesToPublic()
    {
        var manager = CreateManagerMock();
        manager.Setup(m => m.SwitchToPublicAsync()).Returns(Task.CompletedTask);
        SetChatType(manager.Object, MessageType.Private);
        var command = new ExitCommand(new Mock<UiService>().Object, manager.Object);

        await command.ExecuteAsync("#exit");

        manager.Verify(m => m.SwitchToPublicAsync(), Times.Once);
    }

    [Fact]
    public async Task RestoreCommand_ExecuteAsync_RefreshesView()
    {
        var manager = CreateManagerMock();
        manager.Setup(m => m.RefreshCurrentViewAsync()).Returns(Task.CompletedTask);
        var command = new RestoreCommand(manager.Object);

        await command.ExecuteAsync("#restore");

        manager.Verify(m => m.RefreshCurrentViewAsync(), Times.Once);
    }

    [Fact]
    public async Task BlockedListCommand_ExecuteAsync_PrintsListThroughManager()
    {
        var manager = CreateManagerMock();
        manager.Setup(m => m.PrintBlockedList());
        var command = new BlockedListCommand(manager.Object);

        await command.ExecuteAsync("#blocked");

        manager.Verify(m => m.PrintBlockedList(), Times.Once);
    }

    [Fact]
    public async Task PasswordCommand_ExecuteAsync_MismatchedPasswords_ShowsErrorAndSkipsNetwork()
    {
        var ui = new Mock<UiService>();
        ui.SetupSequence(u => u.ReadPassword())
            .Returns("oldPwd")
            .Returns("newPwd1")
            .Returns("newPwd2");

        var manager = CreateManagerMock();
        manager.Object.MyUsername = "alice";

        var network = CreateNetworkMock();
        var command = new PasswordCommand(ui.Object, manager.Object, network.Object);

        await command.ExecuteAsync("#password");

        network.Verify(n => n.ChangePasswordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        ui.Verify(u => u.PrintSystemMessage("[ERRORE] Le nuove password non coincidono.", true), Times.Once);
    }

    [Fact]
    public async Task PasswordCommand_ExecuteAsync_ValidInputs_CallsChangePassword()
    {
        var ui = new Mock<UiService>();
        ui.SetupSequence(u => u.ReadPassword())
            .Returns("oldPwd")
            .Returns("newPassword")
            .Returns("newPassword");

        var manager = CreateManagerMock();
        manager.Object.MyUsername = "alice";

        var network = CreateNetworkMock();
        network.Setup(n => n.ChangePasswordAsync("alice", "oldPwd", "newPassword")).ReturnsAsync(true);
        var command = new PasswordCommand(ui.Object, manager.Object, network.Object);

        await command.ExecuteAsync("#password");

        network.Verify(n => n.ChangePasswordAsync("alice", "oldPwd", "newPassword"), Times.Once);
        ui.Verify(u => u.PrintSystemMessage("[SUCCESSO] Password aggiornata correttamente!", true), Times.Once);
    }

    [Fact]
    public async Task HelpCommand_ExecuteAsync_PrintsSortedCommandList()
    {
        var ui = new Mock<UiService>();

        var services = new ServiceCollection();
        services.AddSingleton<IClientCommand>(new FakeCommand("#zeta", "z"));
        services.AddSingleton<IClientCommand>(new FakeCommand("#alpha", "a"));
        var provider = services.BuildServiceProvider();

        var command = new HelpCommand(ui.Object, provider);

        await command.ExecuteAsync("#help");

        ui.Verify(u => u.PrintSystemMessage(It.Is<string>(text =>
            text.Contains("#alpha") && text.Contains("#zeta") &&
            text.IndexOf("#alpha", StringComparison.Ordinal) < text.IndexOf("#zeta", StringComparison.Ordinal)), false), Times.Once);
    }

    [Fact]
    public async Task KeySetCommand_ExecuteAsync_ValidInput_SetsKey()
    {
        var manager = CreateManagerMock();
        string setError = string.Empty;
        manager.Setup(m => m.SetPrivateKey("bob", "secret", out setError)).Returns(true);

        var ui = new Mock<UiService>();
        ui.Setup(u => u.ReadPassword()).Returns("secret");
        var command = new KeySetCommand(manager.Object, ui.Object);

        await command.ExecuteAsync("#keyset bob");

        manager.Verify(m => m.SetPrivateKey("bob", "secret", out setError), Times.Once);
    }

    [Fact]
    public async Task KeyResetCommand_ExecuteAsync_ValidInput_ResetsKey()
    {
        var manager = CreateManagerMock();
        string resetError = string.Empty;
        manager.Setup(m => m.ResetPrivateKey("bob", out resetError)).Returns(true);

        var command = new KeyResetCommand(manager.Object, new Mock<UiService>().Object);

        await command.ExecuteAsync("#keyreset bob");

        manager.Verify(m => m.ResetPrivateKey("bob", out resetError), Times.Once);
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
        return new Mock<ChatManager>(network.Object, ui.Object, CreateKeyService()) { CallBase = false };
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

    private static void SetChatType(ChatManager manager, MessageType type)
    {
        var backingField = typeof(ChatManager).GetField("<CurrentChatType>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(backingField);
        backingField!.SetValue(manager, type);
    }

    private sealed class FakeCommand(string usage, string description) : IClientCommand
    {
        public string Usage => usage;
        public string Description => description;
        public bool CanExecute(string input) => input == usage;
        public Task ExecuteAsync(string input) => Task.CompletedTask;
    }
}
