using ChatCommons;

namespace TestProject1;

public class GeneralSanityTests
{
    [Fact]
    public void ChatEvents_ContainsKnownSignalRMethods()
    {
        Assert.Equal("SendPublicMessageAsync", ChatEvents.SendPublic);
        Assert.Equal("SendPrivateMessageAsync", ChatEvents.SendPrivate);
    }

    [Fact]
    public void MessageType_DefaultEnumValue_IsPublic()
    {
        var value = default(MessageType);
        Assert.Equal(MessageType.Public, value);
    }
}
