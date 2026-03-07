using System.ComponentModel.DataAnnotations;

namespace ChatClientConsole.Configs;

public class ChatSettings
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "ServerUrl is required")]
    [Url(ErrorMessage = "Invalid URL")]
    public string ServerUrl { get; init; } = string.Empty;

    [Required(ErrorMessage = "ClientPepper is required")]
    [MinLength(10, ErrorMessage = "ClientPepper should be at least 10 chars")]
    public string ClientPepper { get; set; } = string.Empty;
}
