using ChatClientConsole.Configs;
using ChatCommons;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace ChatClientConsole.Services;

public class NetworkService
{
    private readonly string pepper;
    private readonly HubConnection _connection;
    public event Action<string, string> OnPublicMessageReceived;
    public event Action<string, string> OnPrivateMessageReceived;
    public event Action<string> OnSystemNotificationReceived;

    public NetworkService(IOptions<ChatSettings> options)
    {
        pepper = options.Value.ClientPepper;

        _connection = new HubConnectionBuilder()
            .WithUrl(options.Value.ServerUrl)
            .WithAutomaticReconnect()
            .Build();

        _connection.On<string, string>(ChatEvents.ReceivePublic, (u, m) => OnPublicMessageReceived?.Invoke(u, m));
        _connection.On<string, string>(ChatEvents.ReceivePrivate, (u, m) => OnPrivateMessageReceived?.Invoke(u, m));
        _connection.On<string>(ChatEvents.ReceiveSystemNotification, (notification) => OnSystemNotificationReceived?.Invoke(notification));
    }

    public async Task<bool> Register(string user, string pass)
    {
        var hashedPsw = ComputeClientHash(user, pass);
        return await _connection.InvokeAsync<bool>(ChatEvents.Register, user, hashedPsw);
    }

    public async Task<LoginResult> Login(string user, string pass)
    {
        var hashedPsw = ComputeClientHash(user, pass);
        return await _connection.InvokeAsync<LoginResult>(ChatEvents.Login, user, hashedPsw);
    }

    public async Task<bool> ChangePasswordAsync(string username, string oldPassword, string newPassword)
    {
        string oldHash = ComputeClientHash(username, oldPassword);
        string newHash = ComputeClientHash(username, newPassword);
        return await _connection.InvokeAsync<bool>(ChatEvents.ChangePassword, oldHash, newHash);
    }

    public async Task ConnectAsync() => await _connection.StartAsync();
    public async Task<bool> CheckUserExists(string user) => await _connection.InvokeAsync<bool>(ChatEvents.CheckUser, user);
    public async Task SendPublicMessage(string user, string msg) => await _connection.InvokeAsync(ChatEvents.SendPublic, user, msg);
    public async Task SendPrivateMessage(string sender, string receiver, string msg) => await _connection.InvokeAsync(ChatEvents.SendPrivate, sender, receiver, msg);
    public async Task<List<ChatMessage>> GetPublicHistory() => await _connection.InvokeAsync<List<ChatMessage>>(ChatEvents.GetPublicHistory);
    public async Task<List<ChatMessage>> GetPrivateHistory(string me, string other) => await _connection.InvokeAsync<List<ChatMessage>>(ChatEvents.GetPrivateHistory, me, other);
    public async Task<List<string>> GetUnreadSenders(string username) => await _connection.InvokeAsync<List<string>>(ChatEvents.GetUnreadSenders, username);
    public async Task MarkAsRead(string sender) => await _connection.InvokeAsync(ChatEvents.MarkMessagesAsRead, sender);
    public async Task<bool> BlockUserAsync(string username) => await _connection.InvokeAsync<bool>(ChatEvents.BlockUser, username);
    public async Task<bool> UnblockUserAsync(string username) => await _connection.InvokeAsync<bool>(ChatEvents.UnblockUser, username);
    public async Task<List<string>> GetBlockedListAsync() => await _connection.InvokeAsync<List<string>>(ChatEvents.GetBlockedList);

    private string ComputeClientHash(string username, string password)
    {
        // psw + salt + pepper
        string rawData = password + username.ToLower() + pepper;
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawData));
        StringBuilder builder = new();
        foreach (var b in bytes)
        {
            builder.Append(b.ToString("x2"));
        }
        return builder.ToString();
    }
}