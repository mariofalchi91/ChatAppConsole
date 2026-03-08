namespace ChatServer.Configs;

using System.ComponentModel.DataAnnotations;

public class ScyllaSettings
{
    [Required]
    [MinLength(1, ErrorMessage = "At least one node must be specified.")]
    public string[] Nodes { get; init; }

    [Required(AllowEmptyStrings = false, ErrorMessage = "Keyspace is required.")]
    public string Keyspace { get; init; }

    [Required(AllowEmptyStrings = false, ErrorMessage = "Username is required.")]
    public string Username { get; init; }

    [Required(AllowEmptyStrings = false, ErrorMessage = "Password is required.")]
    public string Password { get; init; }

    [Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535.")]
    public int Port { get; init; }
}