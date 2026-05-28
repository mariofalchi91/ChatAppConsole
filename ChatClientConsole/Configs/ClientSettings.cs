using System.ComponentModel.DataAnnotations;

namespace ChatClientConsole.Configs;

public class ClientSettings : IValidatableObject
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "ServerUrl is required")]
    [Url(ErrorMessage = "Invalid URL")]
    public string ServerUrl { get; init; } = string.Empty;

    [Required]
    public E2EPrivateSettings E2EPrivate { get; init; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (E2EPrivate is null)
        {
            yield return new ValidationResult("E2EPrivate settings are required.", [nameof(E2EPrivate)]);
            yield break;
        }

        if (E2EPrivate.EnableLocalKeyVault && string.IsNullOrWhiteSpace(E2EPrivate.LocalKeyVaultPath))
        {
            yield return new ValidationResult(
                "LocalKeyVaultPath is required when local key vault is enabled.",
                [nameof(E2EPrivate.LocalKeyVaultPath)]
            );
        }
    }
}

public class E2EPrivateSettings
{
    public bool EnableLocalKeyVault { get; init; } = false;

    [Required(AllowEmptyStrings = false, ErrorMessage = "LocalKeyVaultPath is required")]
    public string LocalKeyVaultPath { get; init; } = ".chatclient/e2e-private-keys.json";
}
