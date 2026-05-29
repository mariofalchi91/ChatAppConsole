using ChatCommons;

namespace TestProject1;

public class ChatMessageTests
{
    [Fact]
    public void NewInstance_SetsExpectedDefaults()
    {
        var before = DateTime.UtcNow;
        var message = new ChatMessage();
        var after = DateTime.UtcNow;

        Assert.NotEqual(Guid.Empty, message.Id);
        Assert.Equal(default, message.Type);
        Assert.False(message.IsRead);
        Assert.InRange(message.Timestamp, before.AddSeconds(-1), after.AddSeconds(1));
    }

    [Fact]
    public void Properties_CanBeAssignedAndReadBack()
    {
        var id = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;
        var message = new ChatMessage
        {
            Id = id,
            Type = MessageType.Private,
            Sender = "alice",
            Receiver = "bob",
            Content = "hello",
            Timestamp = timestamp,
            IsRead = true
        };

        Assert.Equal(id, message.Id);
        Assert.Equal(MessageType.Private, message.Type);
        Assert.Equal("alice", message.Sender);
        Assert.Equal("bob", message.Receiver);
        Assert.Equal("hello", message.Content);
        Assert.Equal(timestamp, message.Timestamp);
        Assert.True(message.IsRead);
    }
}
