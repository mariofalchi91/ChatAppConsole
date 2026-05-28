using ChatCommons;

namespace TestProject1;

public class EnumTests
{
    [Fact]
    public void LoginResult_HasExpectedValuesAndOrder()
    {
        var values = Enum.GetValues<LoginResult>();

        Assert.Equal(new[]
        {
            LoginResult.Success,
            LoginResult.InvalidCredentials,
            LoginResult.AlreadyConnected
        }, values);
    }

    [Fact]
    public void MessageType_HasExpectedValuesAndOrder()
    {
        var values = Enum.GetValues<MessageType>();

        Assert.Equal(new[]
        {
            MessageType.Public,
            MessageType.Private,
            MessageType.System,
            MessageType.Group
        }, values);
    }
}
