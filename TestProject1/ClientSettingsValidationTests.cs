using ChatClientConsole.Configs;
using System.ComponentModel.DataAnnotations;

namespace TestProject1;

public class ClientSettingsValidationTests
{
    [Fact]
    public void ClientSettings_WithEmptyServerUrl_IsInvalid()
    {
        var model = new ClientSettings { ServerUrl = string.Empty };

        Assert.False(TryValidate(model));
    }

    [Fact]
    public void ClientSettings_WithInvalidUrl_IsInvalid()
    {
        var model = new ClientSettings { ServerUrl = "not-a-url" };

        Assert.False(TryValidate(model));
    }

    [Fact]
    public void ClientSettings_WithValidUrl_IsValid()
    {
        var model = new ClientSettings { ServerUrl = "http://localhost:5000/chat" };

        Assert.True(TryValidate(model));
    }

    private static bool TryValidate(object model)
    {
        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();
        return Validator.TryValidateObject(model, context, results, validateAllProperties: true);
    }
}
