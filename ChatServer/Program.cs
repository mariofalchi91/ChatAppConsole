using ChatServer;
using ChatServer.Configs;
using ChatServer.Repository;

var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddConfiguration(config);
builder.Services.AddOptions<ChatSettings>()
            .BindConfiguration((nameof(ChatSettings)))
            .ValidateDataAnnotations()
            .ValidateOnStart();
builder.Services.AddOpenApi();
builder.Services.AddSignalR();
//builder.Services.AddSingleton<IChatRepository, InMemoryChatRepository>();
builder.Services.AddSingleton<IChatRepository, FileChatRepository>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// per problemi con https commentare la riga sotto
app.UseHttpsRedirection();

// Questo è l'URL che userà il tuo telefono Android: http://tuo-ip:porta/chat
app.MapHub<ChatHub>("/chat");

// Un piccolo endpoint di test per capire se il server è vivo dal browser
app.MapGet("/", () => "Il server della chat è attivo e funzionante!");

app.Run();