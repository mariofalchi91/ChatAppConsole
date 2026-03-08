using System.ComponentModel.DataAnnotations;

namespace ChatClientConsole.Configs;

public class ClientSettings
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "ServerUrl is required")]
    [Url(ErrorMessage = "Invalid URL")]
    public string ServerUrl { get; init; } = string.Empty;
}
