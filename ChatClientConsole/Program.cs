using ChatClientConsole.Commands;
using ChatClientConsole.Configs;
using ChatClientConsole.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace ChatClientConsole;

class Program
{
    static async Task Main(string[] args)
    {
        Console.Title = "Chat Client alfa";

        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<ClientSettings>()
            .BindConfiguration((nameof(ClientSettings)))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Configuration
        services.AddSingleton<IConfiguration>(config);
        // NetworkService
        services.AddSingleton<NetworkService>();
        // ChatManager
        services.AddSingleton<ChatManager>();
        // UiService
        services.AddSingleton<UiService>();
        // ClientApp
        services.AddSingleton<ClientApp>();
        // Commands
        var commandType = typeof(IClientCommand);
        var commands = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => commandType.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var command in commands)
        {
            services.AddSingleton(typeof(IClientCommand), command);
        }

        var serviceProvider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            }
        );

        var app = serviceProvider.GetRequiredService<ClientApp>();
        await app.RunAsync();
    }
}