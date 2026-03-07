# ChatClientConsole - Client Console di Chat Real-Time

Client console interattivo per la soluzione di chat costruito con .NET 10.

## ?? Descrizione

ChatClientConsole è il frontend della soluzione di chat. Fornisce un'interfaccia console interattiva per connettersi al server SignalR, inviare/ricevere messaggi in tempo reale e gestire il profilo utente.

## ?? Responsabilità

- ? Connessione e comunicazione con server SignalR
- ? Autenticazione e registrazione utenti
- ? Invio/ricezione messaggi pubblici e privati
- ? Visualizzazione dell'interfaccia utente in console
- ? Gestione comandi utente
- ? Configurazione da file appsettings.json

## ?? Struttura del Progetto

```
ChatClientConsole/
??? Program.cs                      # Entry point e DI setup
??? ClientApp.cs                    # Logica applicazione principale
??? NetworkService.cs               # Connessione SignalR
??? ChatManager.cs                  # Gestione logica chat
??? UiService.cs                    # Rendering interfaccia console
??? appsettings.json               # Configurazione server
?
??? Commands/
?   ??? IClientCommand.cs          # Interfaccia comando
?   ??? HelpCommand.cs             # Comando /help
?   ??? PasswordCommand.cs         # Comando /password
?   ??? ExitCommand.cs             # Comando /exit
?   ??? PrivateChatCommand.cs      # Comando /pm (messaggi privati)
?   ??? RestoreCommand.cs          # Comando /restore (cronologia)
?
??? Configs/
?   ??? ChatSettings.cs            # Configurazione tipizzata
?
??? ChatClientConsole.csproj       # File progetto
```

## ?? Componenti Principali

### Program.cs

Entry point dell'applicazione. Configura **Dependency Injection** e carica la configurazione:

```csharp
var services = new ServiceCollection();

// Configurazione
services.AddOptions<ChatSettings>()
    .Bind(config.GetSection(nameof(ChatSettings)))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Servizi
services.AddSingleton<NetworkService>();
services.AddSingleton<ChatManager>();
services.AddSingleton<UiService>();
```

### ClientApp.cs

Logica principale dell'applicazione. Gestisce il flusso di:
- Collegamento al server
- Schermata login/registrazione
- Loop principale di chat
- Elaborazione comandi

### NetworkService.cs

Gestisce la **connessione SignalR** al server:

```csharp
private HubConnection? _connection;

public async Task ConnectAsync(string serverUrl)
{
    _connection = new HubConnectionBuilder()
        .WithUrl(serverUrl)
        .WithAutomaticReconnect()
        .Build();
    
    // Registra receiver per messaggi
    _connection.On<ChatMessage>(ChatEvents.ReceivePublic, OnMessageReceived);
    _connection.On<ChatMessage>(ChatEvents.ReceivePrivate, OnPrivateMessageReceived);
    
    await _connection.StartAsync();
}
```

**Responsabilità:**
- Avviare/terminare connessione
- Registrare handler per messaggi in arrivo
- Invocare metodi hub del server

### ChatManager.cs

Gestisce la **logica della chat**:

```csharp
public async Task SendPublicMessageAsync(string content)
public async Task SendPrivateMessageAsync(string receiver, string content)
public async Task<LoginResult> LoginAsync(string username, string password)
public async Task RegisterAsync(string username, string password)
public async Task<bool> ChangePasswordAsync(string oldPassword, string newPassword)
```

Mantiene lo **stato della chat**:
- Utente corrente
- Cronologia messaggi
- Utenti online
- Messaggi non letti

### UiService.cs

Gestisce il **rendering della console**:

```csharp
public void DisplayWelcome()
public void DisplayLoginMenu()
public void DisplayChatInterface()
public void DisplayMessage(ChatMessage message)
public void DisplayError(string error)
public void ClearScreen()
```

Features:
- Formattazione colorata dei messaggi
- Layout responsivo
- Gestione input utente

### Commands/ (Pattern Command)

Implementa il pattern **Command** per estendibilità:

```csharp
public interface IClientCommand
{
    string CommandName { get; }
    string Description { get; }
    Task ExecuteAsync(string[] args);
}
```

**Comandi disponibili:**
- `/help` - Visualizza aiuto comandi
- `/password` - Cambia password
- `/pm @username [message]` - Invia messaggio privato
- `/restore` - Ripristina cronologia chat
- `/exit` - Esce dall'applicazione

### Configs/ChatSettings.cs

Configurazione tipizzata:

```csharp
public class ChatSettings
{
    public string? ServerUrl { get; set; }
    public int ReconnectDelay { get; set; }
    public string? LogLevel { get; set; }
}
```

Caricata da `appsettings.json`:

```json
{
  "ChatSettings": {
    "ServerUrl": "http://localhost:5000/chatHub",
    "ReconnectDelay": 5000,
    "LogLevel": "Information"
  }
}
```

## ?? Avvio Applicazione

### Esecuzione Standard

```bash
cd ChatClientConsole
dotnet run
```

### Con Configurazione Personalizzata

Modificare `appsettings.json` prima di avviare:

```json
{
  "ChatSettings": {
    "ServerUrl": "https://tuoserver.com:5000/chatHub"
  }
}
```

## ?? Flusso Utente

```
???????????????????????????????
?  Avvio Applicazione         ?
?  Program.cs Main()          ?
???????????????????????????????
               ?
               v
???????????????????????????????
?  Connessione al Server      ?
?  NetworkService.Connect()   ?
???????????????????????????????
               ?
               v
???????????????????????????????
?  Menu Login/Registrazione   ?
?  ClientApp.ShowAuthScreen() ?
???????????????????????????????
               ?
       ?????????????????
       ?               ?
       v               v
  ???????????   ????????????
  ? Login   ?   ?Register  ?
  ???????????   ????????????
       ?             ?
       ???????????????
             ?
             v
  ????????????????????????????
  ? Chat Loop                ?
  ? ClientApp.ChatLoop()     ?
  ?                          ?
  ? - Visualizza messaggi    ?
  ? - Legge input utente     ?
  ? - Elabora comandi        ?
  ? - Invia messaggi         ?
  ????????????????????????????
```

## ?? Esempi di Utilizzo

### Inviare Messaggio Pubblico

```
> Ciao a tutti!
```

Il messaggio viene inviato a tutti gli utenti connessi.

### Inviare Messaggio Privato

```
> /pm @mario Ciao Mario, come stai?
```

Il messaggio viene inviato solo all'utente "mario".

### Visualizzare Aiuto

```
> /help
```

Mostra lista dei comandi disponibili.

### Cambiare Password

```
> /password
[Inserisci password attuale]
[Inserisci nuova password]
```

### Ripristinare Cronologia

```
> /restore
```

Scarica cronologia messaggi dal server.

### Uscire

```
> /exit
```

## ?? Protocollo SignalR

### Connessione

```
Client -> Server (HubConnection.StartAsync())
```

### Invio Messaggio Pubblico

```
Client.InvokeAsync(ChatEvents.SendPublic, content)
  -> Hub.SendPublicMessageAsync(content)
    -> Hub.Clients.All.SendAsync(ChatEvents.ReceivePublic, message)
      -> Client.On(ChatEvents.ReceivePublic, OnMessageReceived)
```

### Invio Messaggio Privato

```
Client.InvokeAsync(ChatEvents.SendPrivate, receiver, content)
  -> Hub.SendPrivateMessageAsync(receiver, content)
    -> Hub.Clients.User(receiver).SendAsync(ChatEvents.ReceivePrivate, message)
      -> Specific Client receives message
```

## ?? Dipendenze NuGet

```xml
<PackageReference Include="Microsoft.AspNetCore.SignalR.Client" Version="10.0.2" />
<PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="10.0.2" />
<PackageReference Include="Microsoft.Extensions.Logging.Debug" Version="10.0.0" />
<PackageReference Include="Microsoft.Extensions.Options.ConfigurationExtensions" Version="10.0.2" />
<PackageReference Include="Microsoft.Extensions.Options.DataAnnotations" Version="10.0.2" />
```

## ?? Interfaccia Utente

L'interfaccia è renderizzata in console con:
- **Colori ANSI** per distinzione visiva
- **Timestamp** per ogni messaggio
- **Indicatori di stato** (online/offline)
- **Scroll** per cronologia

Esempio output:

```
???????????????????????????????????????????
?        Chat Application v1.0            ?
?                                         ?
?  Username: mario                        ?
?  Status: Online                         ?
???????????????????????????????????????????
? [10:45] luigi: Ciao a tutti!            ?
? [10:46] mario: Ciao luigi!              ?
? [10:47] @mario mario: Come stai?        ?
?                                         ?
? > _                                     ?
???????????????????????????????????????????
```

## ?? Sicurezza

- ? Password mai trasmesse in plain text (hashate con BCrypt)
- ? Connessione SignalR (supporta SSL/TLS)
- ? Validazione input lato client
- ? Sessione univoca per utente

## ?? Troubleshooting

### Errore: "Could not connect to server"

Verificare:
1. Server è in esecuzione (`http://localhost:5000`)
2. URL in `appsettings.json` è corretto
3. Firewall non blocca la connessione

### Errore: "Invalid credentials"

Verificare:
1. Username esiste nel sistema
2. Password è corretta
3. Utente non è già connesso

## ?? Configurazione Dettagliata

### appsettings.json

```json
{
  "ChatSettings": {
    "ServerUrl": "http://localhost:5000/chatHub",
    "ReconnectDelay": 5000,
    "LogLevel": "Information"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  }
}
```

## ?? Estensioni Future

- [ ] UI grafica (Windows Forms, WPF)
- [ ] Stanze chat (gruppi)
- [ ] Typing indicators ("utente sta scrivendo...")
- [ ] File sharing
- [ ] Emoji support
- [ ] Ricerca messaggi
- [ ] Blocco utenti
- [ ] Profilo utente

## ?? Riferimenti

- [SignalR .NET Client](https://learn.microsoft.com/en-us/aspnet/core/signalr/dotnet-client)
- [Dependency Injection in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection)
- [Configuration in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration)

---

**Progetto parte della soluzione ChatAppConsole**
