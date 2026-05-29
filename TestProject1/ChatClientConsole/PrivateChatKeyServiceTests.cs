using ChatClientConsole.Configs;
using ChatClientConsole.Services;
using Microsoft.Extensions.Options;

namespace TestProject1;

public class PrivateChatKeyServiceTests
{
    [Fact]
    public void TryEncryptDecryptForPeer_WithValidKey_RoundTrips()
    {
        var service = CreateService(enableLocalVault: false);
        Assert.True(service.TryInitializeSession("alice", "pwd", out _));
        Assert.True(service.TrySetKeyForPeer("bob", "shared", out _));

        Assert.True(service.TryEncryptForPeer("bob", "ciao", out var cipher, out _));
        Assert.True(service.TryDecryptForPeer("bob", cipher, out var plain, out _));

        Assert.Equal("ciao", plain);
    }

    [Fact]
    public void TrySetKeyForPeer_WhenAlreadyExists_Fails()
    {
        var service = CreateService(enableLocalVault: false);
        Assert.True(service.TryInitializeSession("alice", "pwd", out _));
        Assert.True(service.TrySetKeyForPeer("bob", "shared", out _));

        var ok = service.TrySetKeyForPeer("bob", "other", out var error);

        Assert.False(ok);
        Assert.Contains("gia configurata", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LocalVault_WhenEnabled_LoadsEncryptedKeysAcrossSessions()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"chat-e2e-{Guid.NewGuid():N}.json");
        try
        {
            var first = CreateService(enableLocalVault: true, vaultPath: tempFile);
            Assert.True(first.TryInitializeSession("alice", "pwd", out _));
            Assert.True(first.TrySetKeyForPeer("bob", "shared", out _));

            var second = CreateService(enableLocalVault: true, vaultPath: tempFile);
            Assert.True(second.TryInitializeSession("alice", "pwd", out _));
            Assert.True(second.HasKeyForPeer("bob"));
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void LocalVault_WithDifferentLoginPassword_FailsToInitialize()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"chat-e2e-{Guid.NewGuid():N}.json");
        try
        {
            var first = CreateService(enableLocalVault: true, vaultPath: tempFile);
            Assert.True(first.TryInitializeSession("alice", "pwd", out _));
            Assert.True(first.TrySetKeyForPeer("bob", "shared", out _));

            var second = CreateService(enableLocalVault: true, vaultPath: tempFile);
            var initialized = second.TryInitializeSession("alice", "pwd-changed", out var error);

            Assert.False(initialized);
            Assert.Contains("Vault E2E non leggibile", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static PrivateChatKeyService CreateService(bool enableLocalVault, string? vaultPath = null)
    {
        var path = vaultPath ?? Path.Combine(Path.GetTempPath(), $"chat-e2e-{Guid.NewGuid():N}.json");

        var options = Options.Create(new ClientSettings
        {
            ServerUrl = "http://localhost",
            E2EPrivate = new E2EPrivateSettings
            {
                EnableLocalKeyVault = enableLocalVault,
                LocalKeyVaultPath = path
            }
        });

        return new PrivateChatKeyService(options);
    }
}
