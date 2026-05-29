using ChatServer.Configs;
using System.ComponentModel.DataAnnotations;

namespace TestProject1;

public class ConfigValidationTests
{
    [Fact]
    public void ChatSettings_WithMissingDataFolderPath_IsInvalid()
    {
        var model = new ChatSettings { DataFolderPath = string.Empty };

        Assert.False(TryValidate(model));
    }

    [Fact]
    public void ScyllaSettings_WithInvalidValues_IsInvalid()
    {
        var model = new ScyllaSettings
        {
            Nodes = [],
            Keyspace = string.Empty,
            Username = string.Empty,
            Password = string.Empty,
            Port = 0
        };

        Assert.False(TryValidate(model));
    }

    [Fact]
    public void ScyllaSettings_WithValidValues_IsValid()
    {
        var model = new ScyllaSettings
        {
            Nodes = ["127.0.0.1"],
            Keyspace = "chat_app",
            Username = "scylla",
            Password = "pwd",
            Port = 9042
        };

        Assert.True(TryValidate(model));
    }

    private static bool TryValidate(object model)
    {
        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();
        return Validator.TryValidateObject(model, context, results, validateAllProperties: true);
    }
}
