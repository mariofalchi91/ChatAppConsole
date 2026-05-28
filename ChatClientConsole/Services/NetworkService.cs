using ChatClientConsole.Configs;
using ChatCommons;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace ChatClientConsole.Services;

public class NetworkService
{
    private readonly HubConnection _connection;
    public event Action<string, string> OnPublicMessageReceived;
    public event Action<string, string> OnPrivateMessageReceived;
    public event Action<string> OnSystemNotificationReceived;

    public NetworkService(IOptions<ClientSettings> options)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl(options.Value.ServerUrl)
            .WithAutomaticReconnect()
            .Build();

        _connection.On<string, string>(ChatEvents.ReceivePublic, (u, m) => OnPublicMessageReceived?.Invoke(u, m));
        _connection.On<string, string>(ChatEvents.ReceivePrivate, (u, m) => OnPrivateMessageReceived?.Invoke(u, m));
        _connection.On<string>(ChatEvents.ReceiveSystemNotification, (notification) => OnSystemNotificationReceived?.Invoke(notification));
    }

    public virtual async Task<bool> Register(string user, string pass)
    {
        var hashedPsw = CryptoService.HashCredentials(user, pass);
        return await _connection.InvokeAsync<bool>(ChatEvents.Register, user, hashedPsw);
    }

    public virtual async Task<LoginResult> Login(string user, string pass)
    {
        var hashedPsw = CryptoService.HashCredentials(user, pass);
        return await _connection.InvokeAsync<LoginResult>(ChatEvents.Login, user, hashedPsw);
    }

    public virtual async Task<bool> ChangePasswordAsync(string username, string oldPassword, string newPassword)
    {
        string oldHash = CryptoService.HashCredentials(username, oldPassword);
        string newHash = CryptoService.HashCredentials(username, newPassword);
        return await _connection.InvokeAsync<bool>(ChatEvents.ChangePassword, oldHash, newHash);
    }

    public virtual async Task ConnectAsync() => await _connection.StartAsync();
    public virtual async Task<bool> CheckUserExists(string user) => await _connection.InvokeAsync<bool>(ChatEvents.CheckUser, user);
    public virtual async Task SendPublicMessage(string user, string msg) => await _connection.InvokeAsync(ChatEvents.SendPublic, user, msg);
    public virtual async Task SendPrivateMessage(string sender, string receiver, string msg) => await _connection.InvokeAsync(ChatEvents.SendPrivate, sender, receiver, msg);
    public virtual async Task<List<ChatMessage>> GetPublicHistory() => await _connection.InvokeAsync<List<ChatMessage>>(ChatEvents.GetPublicHistory);
    public virtual async Task<List<ChatMessage>> GetPrivateHistory(string me, string other) => await _connection.InvokeAsync<List<ChatMessage>>(ChatEvents.GetPrivateHistory, me, other);
    public virtual async Task<List<string>> GetUnreadSenders(string username) => await _connection.InvokeAsync<List<string>>(ChatEvents.GetUnreadSenders, username);
    public virtual async Task MarkAsRead(string sender) => await _connection.InvokeAsync(ChatEvents.MarkMessagesAsRead, sender);
    public virtual async Task<bool> BlockUserAsync(string username) => await _connection.InvokeAsync<bool>(ChatEvents.BlockUser, username);
    public virtual async Task<bool> UnblockUserAsync(string username) => await _connection.InvokeAsync<bool>(ChatEvents.UnblockUser, username);
    public virtual async Task<List<string>> GetBlockedListAsync() => await _connection.InvokeAsync<List<string>>(ChatEvents.GetBlockedList);
}