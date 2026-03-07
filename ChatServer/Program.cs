using ChatServer;
using ChatServer.Configs;
using ChatServer.Repository;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ChatSettings>(builder.Configuration.GetSection(nameof(ChatSettings)));
builder.Services.AddOpenApi();
builder.Services.AddSignalR();
//builder.Services.AddSingleton<IChatRepository, InMemoryChatRepository>();
builder.Services.AddSingleton<IChatRepository, FileChatRepository>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Nota: Per i test locali con Android, l'HTTPS può dare problemi di certificati.
// Se vedi che il telefono non si connette, potresti dover commentare la riga sotto.
app.UseHttpsRedirection();

// 3. Mappiamo l'endpoint della chat
// Questo è l'URL che userà il tuo telefono Android: http://tuo-ip:porta/chat
app.MapHub<ChatHub>("/chat");

// Un piccolo endpoint di test per capire se il server è vivo dal browser
app.MapGet("/", () => "Il server della chat è attivo e funzionante!");

app.Run();