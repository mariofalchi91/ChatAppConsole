using ChatCommons;
using ChatServer.Configs;
using ChatServer.Repository;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace TestProject1;

public class FileChatRepositoryTests
{
    [Fact]
    public void AddUser_PersistsAcrossRepositoryInstances()
    {
        var folder = CreateTempFolder();
        try
        {
            var repo1 = CreateRepository(folder);
            _ = repo1.AddUser(new UserData { Username = "alice", Password = "pwd" });

            var repo2 = CreateRepository(folder);

            Assert.True(repo2.UserExists("alice"));
            Assert.True(repo2.ValidateCredentials("alice", "pwd"));
        }
        finally
        {
            DeleteFolder(folder);
        }
    }

    [Fact]
    public void BlockUser_PersistsAcrossRepositoryInstances()
    {
        var folder = CreateTempFolder();
        try
        {
            var repo1 = CreateRepository(folder);
            repo1.BlockUser("bob", "alice");

            var repo2 = CreateRepository(folder);

            Assert.True(repo2.IsBlocked("alice", "bob"));
        }
        finally
        {
            DeleteFolder(folder);
        }
    }

    [Fact]
    public void AddMessage_PrivateMessage_PersistsAndLoads()
    {
        var folder = CreateTempFolder();
        try
        {
            var repo1 = CreateRepository(folder);
            _ = repo1.AddUser(new UserData { Username = "alice", Password = "pwd" });
            _ = repo1.AddUser(new UserData { Username = "bob", Password = "pwd" });
            var timestamp = DateTime.UtcNow.AddMinutes(-2);

            repo1.AddMessage(new ChatMessage
            {
                Sender = "alice",
                Receiver = "bob",
                Content = "hello",
                Timestamp = timestamp,
                Type = MessageType.Private
            });

            var repo2 = CreateRepository(folder);
            var history = repo2.GetPrivateHistory("alice", "bob");

            Assert.Single(history);
            Assert.Equal("hello", history[0].Content);
            Assert.Equal("alice", history[0].Sender);
            Assert.Equal("bob", history[0].Receiver);
        }
        finally
        {
            DeleteFolder(folder);
        }
    }

    [Fact]
    public void AddMessage_PublicMessage_PersistsAndAppliesCutoff()
    {
        var folder = CreateTempFolder();
        try
        {
            var repo1 = CreateRepository(folder);
            repo1.AddMessage(new ChatMessage
            {
                Sender = "alice",
                Content = "public-message",
                Timestamp = DateTime.UtcNow.AddMinutes(-10),
                Type = MessageType.Public
            });

            var repo2 = CreateRepository(folder);
            var history = repo2.GetPublicHistory(DateTime.UtcNow.AddMinutes(-5));

            Assert.Single(history);
            Assert.Equal("public-message", history[0].Content);
            Assert.True(history[0].IsRead);
        }
        finally
        {
            DeleteFolder(folder);
        }
    }

    [Fact]
    public void UpdateReadWatermark_ChangesUnreadSendersAndIsReadStatus()
    {
        var folder = CreateTempFolder();
        try
        {
            var repo = CreateRepository(folder);
            _ = repo.AddUser(new UserData { Username = "alice", Password = "pwd" });
            _ = repo.AddUser(new UserData { Username = "bob", Password = "pwd" });

            repo.AddMessage(new ChatMessage
            {
                Sender = "alice",
                Receiver = "bob",
                Content = "unread",
                Timestamp = DateTime.UtcNow.AddMinutes(-1),
                Type = MessageType.Private
            });

            var unreadBefore = repo.GetUnreadSenders("bob");
            Assert.Contains("alice", unreadBefore);

            repo.UpdateReadWatermark("bob", "alice");

            var unreadAfter = repo.GetUnreadSenders("bob");
            var history = repo.GetPrivateHistory("alice", "bob");

            Assert.DoesNotContain("alice", unreadAfter);
            Assert.Single(history);
            Assert.True(history[0].IsRead);
        }
        finally
        {
            DeleteFolder(folder);
        }
    }

    [Fact]
    public void GetPublicHistory_WithCorruptedLine_SkipsInvalidLineAndReturnsValidMessages()
    {
        var folder = CreateTempFolder();
        try
        {
            var repo = CreateRepository(folder);
            repo.AddMessage(new ChatMessage
            {
                Sender = "alice",
                Content = "valid",
                Timestamp = DateTime.UtcNow.AddMinutes(-1),
                Type = MessageType.Public
            });

            var publicPath = Path.Combine(folder, "public.jsonl");
            File.AppendAllText(publicPath, "{ not valid json }" + Environment.NewLine);

            var history = repo.GetPublicHistory(DateTime.UtcNow);

            Assert.Single(history);
            Assert.Equal("valid", history[0].Content);
        }
        finally
        {
            DeleteFolder(folder);
        }
    }

    [Fact]
    public void GetPrivateHistory_WithCorruptedLine_SkipsInvalidLineAndReturnsValidMessages()
    {
        var folder = CreateTempFolder();
        try
        {
            var repo = CreateRepository(folder);
            _ = repo.AddUser(new UserData { Username = "alice", Password = "pwd" });
            _ = repo.AddUser(new UserData { Username = "bob", Password = "pwd" });

            repo.AddMessage(new ChatMessage
            {
                Sender = "alice",
                Receiver = "bob",
                Content = "valid-private",
                Timestamp = DateTime.UtcNow,
                Type = MessageType.Private
            });

            var privateFolder = Path.Combine(folder, "PrivateChats");
            var privateFilePath = Path.Combine(privateFolder, "private_alice_bob.jsonl");
            File.AppendAllText(privateFilePath, "not-json-line" + Environment.NewLine);

            var history = repo.GetPrivateHistory("alice", "bob");

            Assert.Single(history);
            Assert.Equal("valid-private", history[0].Content);
        }
        finally
        {
            DeleteFolder(folder);
        }
    }

    private static FileChatRepository CreateRepository(string folder)
    {
        var options = Options.Create(new ChatSettings { DataFolderPath = folder });
        var logger = new Mock<ILogger<FileChatRepository>>();
        return new FileChatRepository(options, logger.Object);
    }

    private static string CreateTempFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "chat-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static void DeleteFolder(string folder)
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
