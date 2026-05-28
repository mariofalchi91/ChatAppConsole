using System.ComponentModel.DataAnnotations;

namespace ChatClientConsole.Configs;

public class ClientSettings
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "ServerUrl is required")]
    [Url(ErrorMessage = "Invalid URL")]
    public string ServerUrl { get; init; } = string.Empty;

    [Required]
    public E2EPrivateSettings E2EPrivate { get; init; } = new();
}

public class E2EPrivateSettings
{
    public bool EnableLocalKeyVault { get; init; } = false;

    [Required(AllowEmptyStrings = false, ErrorMessage = "LocalKeyVaultPath is required")]
    public string LocalKeyVaultPath { get; init; } = ".chatclient/e2e-private-keys.json";
}
