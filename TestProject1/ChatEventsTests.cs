using ChatCommons;
using System.Reflection;

namespace TestProject1;

public class ChatEventsTests
{
    [Fact]
    public void AllConstants_AreNotNullOrWhiteSpace()
    {
        var values = GetEventValues();

        Assert.NotEmpty(values);
        Assert.All(values, value => Assert.False(string.IsNullOrWhiteSpace(value)));
    }

    [Fact]
    public void AllConstants_AreUnique()
    {
        var values = GetEventValues();

        Assert.Equal(values.Length, values.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void ServerAndClientCriticalEvents_HaveExpectedValues()
    {
        Assert.Equal("ReceivePublicMessage", ChatEvents.ReceivePublic);
        Assert.Equal("ReceivePrivateMessage", ChatEvents.ReceivePrivate);
        Assert.Equal("ReceiveSystemNotification", ChatEvents.ReceiveSystemNotification);

        Assert.Equal("SendPublicMessageAsync", ChatEvents.SendPublic);
        Assert.Equal("SendPrivateMessageAsync", ChatEvents.SendPrivate);
        Assert.Equal("Login", ChatEvents.Login);
        Assert.Equal("Register", ChatEvents.Register);
        Assert.Equal("GetPublicHistory", ChatEvents.GetPublicHistory);
        Assert.Equal("GetPrivateHistory", ChatEvents.GetPrivateHistory);
        Assert.Equal("MarkMessagesAsRead", ChatEvents.MarkMessagesAsRead);
        Assert.Equal("BlockUser", ChatEvents.BlockUser);
        Assert.Equal("UnblockUser", ChatEvents.UnblockUser);
        Assert.Equal("GetBlockedList", ChatEvents.GetBlockedList);
    }

    private static string[] GetEventValues()
    {
        return typeof(ChatEvents)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToArray();
    }
}
