using ChatClientConsole.Commands;
using ChatClientConsole.Configs;
using ChatClientConsole.Services;
using ChatCommons;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace TestProject1;

public class CommandTests
{
    [Fact]
    public void BlockCommand_CanExecute_WorksForExpectedInputs()
    {
        var command = new BlockCommand(CreateManagerMock(), Substitute.For<UiService>());

        Assert.True(command.CanExecute("#block"));
        Assert.True(command.CanExecute("#block user"));
        Assert.False(command.CanExecute("#blo"));
    }

    [Fact]
    public async Task BlockCommand_ExecuteAsync_InvalidSyntax_PrintsError()
    {
        var ui = Substitute.For<UiService>();
        var command = new BlockCommand(CreateManagerMock(), ui);

        await command.ExecuteAsync("#block");

        ui.Received(1).Print("Sintassi errata!", false);
    }

    [Fact]
    public async Task BlockCommand_ExecuteAsync_ValidInput_CallsManager()
    {
        var manager = CreateManagerMock();
        manager.BlockUserAsync("alice").Returns(Task.CompletedTask);
        var command = new BlockCommand(manager, Substitute.For<UiService>());

        await command.ExecuteAsync("#block alice");

        await manager.Received(1).BlockUserAsync("alice");
    }

    [Fact]
    public void UnblockCommand_CanExecute_WorksAsExpected()
    {
        var command = new UnblockCommand(CreateManagerMock(), Substitute.For<UiService>());

        Assert.True(command.CanExecute("#unblock"));
        Assert.True(command.CanExecute("#unblock user"));
        Assert.False(command.CanExecute("#restore"));
    }

    [Fact]
    public async Task UnblockCommand_ExecuteAsync_ValidInput_CallsManager()
    {
        var manager = CreateManagerMock();
        manager.UnblockUserAsync("bob").Returns(Task.CompletedTask);
        var command = new UnblockCommand(manager, Substitute.For<UiService>());

        await command.ExecuteAsync("#unblock bob");

        await manager.Received(1).UnblockUserAsync("bob");
    }

    [Fact]
    public void PrivateChatCommand_CanExecute_RequiresAtAndUsername()
    {
        var command = new PrivateChatCommand(CreateManagerMock());

        Assert.True(command.CanExecute("@alice"));
        Assert.False(command.CanExecute("alice"));
        Assert.False(command.CanExecute("@"));
    }

    [Fact]
    public async Task PrivateChatCommand_ExecuteAsync_CallsSwitchToPrivate()
    {
        var manager = CreateManagerMock();
        manager.SwitchToPrivateAsync("alice").Returns(Task.CompletedTask);
        var command = new PrivateChatCommand(manager);

        await command.ExecuteAsync("@alice");

        await manager.Received(1).SwitchToPrivateAsync("alice");
    }

    [Fact]
    public void ExitCommand_CanExecute_OnlyForExitToken()
    {
        var command = new ExitCommand(Substitute.For<UiService>(), CreateManagerMock());

        Assert.True(command.CanExecute("#exit"));
        Assert.False(command.CanExecute("#exit now"));
    }

    [Fact]
    public async Task ExitCommand_ExecuteAsync_InPublic_PrintsInfo()
    {
        var ui = Substitute.For<UiService>();
        var manager = CreateManagerMock();
        SetChatType(manager, MessageType.Public);
        var command = new ExitCommand(ui, manager);

        await command.ExecuteAsync("#exit");

        ui.Received(1).PrintSystemMessage("[INFO] Sei già nella pubblica.", true);
    }

    [Fact]
    public async Task ExitCommand_ExecuteAsync_InPrivate_SwitchesToPublic()
    {
        var manager = CreateManagerMock();
        manager.SwitchToPublicAsync().Returns(Task.CompletedTask);
        SetChatType(manager, MessageType.Private);
        var command = new ExitCommand(Substitute.For<UiService>(), manager);

        await command.ExecuteAsync("#exit");

        await manager.Received(1).SwitchToPublicAsync();
    }

    [Fact]
    public async Task RestoreCommand_ExecuteAsync_RefreshesView()
    {
        var manager = CreateManagerMock();
        manager.RefreshCurrentViewAsync().Returns(Task.CompletedTask);
        var command = new RestoreCommand(manager);

        await command.ExecuteAsync("#restore");

        await manager.Received(1).RefreshCurrentViewAsync();
    }

    [Fact]
    public async Task BlockedListCommand_ExecuteAsync_PrintsListThroughManager()
    {
        var manager = CreateManagerMock();
        var command = new BlockedListCommand(manager);

        await command.ExecuteAsync("#blocked");

        manager.Received(1).PrintBlockedList();
    }

    [Fact]
    public async Task PasswordCommand_ExecuteAsync_MismatchedPasswords_ShowsErrorAndSkipsNetwork()
    {
        var ui = Substitute.For<UiService>();
        ui.ReadPassword().Returns("oldPwd", "newPwd1", "newPwd2");

        var manager = CreateManagerMock();
        manager.MyUsername = "alice";

        var network = CreateNetworkMock();
        var command = new PasswordCommand(ui, manager, network);

        await command.ExecuteAsync("#password");

        await network.DidNotReceive().ChangePasswordAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        ui.Received(1).PrintSystemMessage("[ERRORE] Le nuove password non coincidono.", true);
    }

    [Fact]
    public async Task PasswordCommand_ExecuteAsync_ValidInputs_CallsChangePassword()
    {
        var ui = Substitute.For<UiService>();
        ui.ReadPassword().Returns("oldPwd", "newPassword", "newPassword");

        var manager = CreateManagerMock();
        manager.MyUsername = "alice";

        var network = CreateNetworkMock();
        network.ChangePasswordAsync("alice", "oldPwd", "newPassword").Returns(true);
        var command = new PasswordCommand(ui, manager, network);

        await command.ExecuteAsync("#password");

        await network.Received(1).ChangePasswordAsync("alice", "oldPwd", "newPassword");
        ui.Received(1).PrintSystemMessage("[SUCCESSO] Password aggiornata correttamente!", true);
    }

    [Fact]
    public async Task HelpCommand_ExecuteAsync_PrintsSortedCommandList()
    {
        var ui = Substitute.For<UiService>();

        var services = new ServiceCollection();
        services.AddSingleton<IClientCommand>(new FakeCommand("#zeta", "z"));
        services.AddSingleton<IClientCommand>(new FakeCommand("#alpha", "a"));
        var provider = services.BuildServiceProvider();

        var command = new HelpCommand(ui, provider);

        await command.ExecuteAsync("#help");

        ui.Received(1).PrintSystemMessage(Arg.Is<string>(text =>
            text.Contains("#alpha") && text.Contains("#zeta") &&
            text.IndexOf("#alpha", StringComparison.Ordinal) < text.IndexOf("#zeta", StringComparison.Ordinal)), false);
    }

    [Fact]
    public async Task KeySetCommand_ExecuteAsync_ValidInput_SetsKey()
    {
        var manager = CreateManagerMock();
        string setError = null!;
        manager.SetPrivateKey("bob", "secret", out setError).Returns(true);

        var ui = Substitute.For<UiService>();
        ui.ReadPassword().Returns("secret");
        var command = new KeySetCommand(manager, ui);

        await command.ExecuteAsync("#keyset bob");

        manager.Received(1).SetPrivateKey("bob", "secret", out setError);
    }

    [Fact]
    public async Task KeyResetCommand_ExecuteAsync_ValidInput_ResetsKey()
    {
        var manager = CreateManagerMock();
        string resetError = null!;
        manager.ResetPrivateKey("bob", out resetError).Returns(true);

        var command = new KeyResetCommand(manager, Substitute.For<UiService>());

        await command.ExecuteAsync("#keyreset bob");

        manager.Received(1).ResetPrivateKey("bob", out resetError);
    }

    private static NetworkService CreateNetworkMock()
    {
        var options = Options.Create(new ClientSettings { ServerUrl = "http://localhost" });
        return Substitute.For<NetworkService>(options);
    }

    private static ChatManager CreateManagerMock()
    {
        var network = CreateNetworkMock();
        var ui = Substitute.For<UiService>();
        return Substitute.For<ChatManager>(network, ui, CreateKeyService());
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
