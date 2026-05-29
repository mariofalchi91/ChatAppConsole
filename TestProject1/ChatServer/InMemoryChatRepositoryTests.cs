using ChatCommons;
using ChatServer.Repository;

namespace TestProject1;

public class InMemoryChatRepositoryTests
{
    [Fact]
    public void AddUser_NewUser_ReturnsTrueAndUserExists()
    {
        var repo = new InMemoryChatRepository();

        var added = repo.AddUser(new UserData { Username = "alice", Password = "pwd1" });

        Assert.True(added);
        Assert.True(repo.UserExists("alice"));
    }

    [Fact]
    public void AddUser_DuplicateUser_ReturnsFalse()
    {
        var repo = new InMemoryChatRepository();
        _ = repo.AddUser(new UserData { Username = "alice", Password = "pwd1" });

        var addedAgain = repo.AddUser(new UserData { Username = "alice", Password = "pwd2" });

        Assert.False(addedAgain);
    }

    [Fact]
    public void ValidateCredentials_WithCorrectAndWrongPassword_ReturnsExpectedResult()
    {
        var repo = new InMemoryChatRepository();
        _ = repo.AddUser(new UserData { Username = "alice", Password = "pwd1" });

        Assert.True(repo.ValidateCredentials("alice", "pwd1"));
        Assert.False(repo.ValidateCredentials("alice", "wrong"));
        Assert.False(repo.ValidateCredentials("missing", "pwd1"));
    }

    [Fact]
    public void ChangePassword_WithValidOldPassword_UpdatesCredentials()
    {
        var repo = new InMemoryChatRepository();
        _ = repo.AddUser(new UserData { Username = "alice", Password = "old" });

        var changed = repo.ChangePassword("alice", "old", "new");

        Assert.True(changed);
        Assert.False(repo.ValidateCredentials("alice", "old"));
        Assert.True(repo.ValidateCredentials("alice", "new"));
    }

    [Fact]
    public void ChangePassword_WithWrongOldPassword_ReturnsFalse()
    {
        var repo = new InMemoryChatRepository();
        _ = repo.AddUser(new UserData { Username = "alice", Password = "old" });

        var changed = repo.ChangePassword("alice", "wrong", "new");

        Assert.False(changed);
        Assert.True(repo.ValidateCredentials("alice", "old"));
    }

    [Fact]
    public void GetPrivateHistory_ReturnsMessagesOrderedByTimestamp()
    {
        var repo = new InMemoryChatRepository();
        var t1 = DateTime.UtcNow.AddMinutes(-2);
        var t2 = DateTime.UtcNow.AddMinutes(-1);

        repo.AddMessage(new ChatMessage { Sender = "alice", Receiver = "bob", Content = "m2", Type = MessageType.Private, Timestamp = t2 });
        repo.AddMessage(new ChatMessage { Sender = "bob", Receiver = "alice", Content = "m1", Type = MessageType.Private, Timestamp = t1 });
        repo.AddMessage(new ChatMessage { Sender = "eve", Receiver = "bob", Content = "other", Type = MessageType.Private, Timestamp = t1 });

        var history = repo.GetPrivateHistory("alice", "bob");

        Assert.Equal(2, history.Count);
        Assert.Equal("m1", history[0].Content);
        Assert.Equal("m2", history[1].Content);
    }

    [Fact]
    public void GetPublicHistory_SetsIsReadBasedOnCutoff()
    {
        var repo = new InMemoryChatRepository();
        var oldMsgTime = DateTime.UtcNow.AddMinutes(-10);
        var newMsgTime = DateTime.UtcNow.AddMinutes(-1);
        var cutoff = DateTime.UtcNow.AddMinutes(-5);

        repo.AddMessage(new ChatMessage { Sender = "alice", Content = "old", Type = MessageType.Public, Timestamp = oldMsgTime });
        repo.AddMessage(new ChatMessage { Sender = "bob", Content = "new", Type = MessageType.Public, Timestamp = newMsgTime });

        var history = repo.GetPublicHistory(cutoff);

        Assert.Equal(2, history.Count);
        Assert.True(history.Single(m => m.Content == "old").IsRead);
        Assert.False(history.Single(m => m.Content == "new").IsRead);
    }

    [Fact]
    public void GetUnreadSenders_ReturnsDistinctUnreadSenders()
    {
        var repo = new InMemoryChatRepository();

        repo.AddMessage(new ChatMessage { Sender = "alice", Receiver = "bob", Content = "1", Type = MessageType.Private, IsRead = false });
        repo.AddMessage(new ChatMessage { Sender = "alice", Receiver = "bob", Content = "2", Type = MessageType.Private, IsRead = false });
        repo.AddMessage(new ChatMessage { Sender = "carol", Receiver = "bob", Content = "3", Type = MessageType.Private, IsRead = false });
        repo.AddMessage(new ChatMessage { Sender = "dan", Receiver = "bob", Content = "4", Type = MessageType.Private, IsRead = true });

        var senders = repo.GetUnreadSenders("bob");

        Assert.Equal(2, senders.Count);
        Assert.Contains("alice", senders);
        Assert.Contains("carol", senders);
    }

    [Fact]
    public void UpdateReadWatermark_MarksMatchingMessagesAsRead()
    {
        var repo = new InMemoryChatRepository();

        repo.AddMessage(new ChatMessage { Sender = "alice", Receiver = "bob", Content = "a", Type = MessageType.Private, IsRead = false });
        repo.AddMessage(new ChatMessage { Sender = "alice", Receiver = "bob", Content = "b", Type = MessageType.Private, IsRead = false });
        repo.AddMessage(new ChatMessage { Sender = "carol", Receiver = "bob", Content = "c", Type = MessageType.Private, IsRead = false });

        repo.UpdateReadWatermark("bob", "alice");
        var fromAlice = repo.GetPrivateHistory("alice", "bob");

        Assert.All(fromAlice.Where(m => m.Sender == "alice" && m.Receiver == "bob"), m => Assert.True(m.IsRead));
        Assert.False(repo.GetMessagesToUpdate("carol", "bob")[0].IsRead);
    }

    [Fact]
    public void BlockAndUnblockUser_UpdatesBlockState()
    {
        var repo = new InMemoryChatRepository();

        repo.BlockUser("bob", "alice");

        Assert.True(repo.IsBlocked("alice", "bob"));
        Assert.Contains("alice", repo.GetBlockedUsers("bob"));
        Assert.Contains("bob", repo.GetUsersWhoBlockedMe("alice"));

        repo.UnblockUser("bob", "alice");

        Assert.False(repo.IsBlocked("alice", "bob"));
        Assert.DoesNotContain("alice", repo.GetBlockedUsers("bob"));
    }

    [Fact]
    public void BlockUser_IsCaseInsensitive()
    {
        var repo = new InMemoryChatRepository();

        repo.BlockUser("Bob", "Alice");

        Assert.True(repo.IsBlocked("alice", "bob"));
        Assert.Contains("Alice", repo.GetBlockedUsers("bob"));
        Assert.Contains("Bob", repo.GetUsersWhoBlockedMe("ALICE"));
    }

    [Fact]
    public void UpdateUserLogout_AndGetLastLogout_WorkAsExpected()
    {
        var repo = new InMemoryChatRepository();
        _ = repo.AddUser(new UserData { Username = "alice", Password = "pwd" });

        var logout = repo.UpdateUserLogout("alice");

        Assert.NotEqual(DateTime.MinValue, logout);
        Assert.Equal(logout, repo.GetLastLogout("alice"));
        Assert.Equal(DateTime.MinValue, repo.GetLastLogout("missing"));
    }

    [Fact]
    public void GetBlockedUsers_ForUnknownUser_ReturnsEmptyList()
    {
        var repo = new InMemoryChatRepository();

        var blocked = repo.GetBlockedUsers("ghost");

        Assert.Empty(blocked);
    }

    [Fact]
    public void UnblockUser_ForUnknownUser_DoesNotThrow()
    {
        var repo = new InMemoryChatRepository();

        var ex = Record.Exception(() => repo.UnblockUser("ghost", "target"));

        Assert.Null(ex);
    }
}
