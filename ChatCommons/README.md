# ChatCommons - Classi Comuni Condivise

Libreria di classi .NET condivisa tra il server e il client della soluzione di chat.

## ?? Descrizione

ChatCommons contiene **modelli dati** e **costanti** utilizzate da entrambi i progetti (ChatAppConsole e ChatClientConsole) per una comunicazione corretta e coerente.

## ?? Responsabilità

- ?? Definire modelli dati comuni (ChatMessage, UserData, LoginResult)
- ?? Esporre costanti di eventi SignalR
- ?? Garantire coerenza tra server e client

## ?? Struttura del Progetto

```
ChatCommons/
??? ChatEvents.cs          # Costanti eventi SignalR
??? ChatMessage.cs         # Modello messaggio chat
??? UserData.cs            # Modello dati utente
??? LoginResult.cs         # Enum risultato login
??? ChatCommons.csproj     # File progetto
```

## ?? Componenti Principali

### ChatEvents.cs

Contiene costanti di **metodi SignalR** utilizzati nel protocollo di comunicazione:

```csharp
public static class ChatEvents
{
    // Metodi Server -> Client (Receiver)
    public const string ReceivePublic = "ReceivePublicMessage";
    public const string ReceivePrivate = "ReceivePrivateMessage";
    public const string UserConnected = "UserConnected";
    public const string UserDisconnected = "UserDisconnected";
    
    // Metodi Client -> Server (Invoke)
    public const string SendPublic = "SendPublicMessageAsync";
    public const string SendPrivate = "SendPrivateMessageAsync";
    public const string Login = "Login";
    public const string CheckUser = "CheckUserExists";
    public const string Register = "Register";
    public const string GetPublicHistory = "GetPublicHistory";
    public const string GetPrivateHistory = "GetPrivateHistory";
    public const string GetUnreadSenders = "GetUnreadSenders";
    public const string MarkMessagesAsRead = "MarkMessagesAsRead";
    public const string ChangePassword = "ChangePassword";
}
```

**Utilizzo nel client:**
```csharp
await connection.InvokeAsync(ChatEvents.SendPublic, messageContent);
connection.On(ChatEvents.ReceivePublic, (ChatMessage msg) => { /* handle */ });
```

### ChatMessage.cs

Modello rappresentativo di un messaggio chat:

```csharp
public class ChatMessage
{
    /// <summary>Username dell'autore del messaggio</summary>
    public string Sender { get; set; }
    
    /// <summary>Username del destinatario (null = messaggio pubblico)</summary>
    public string? Receiver { get; set; }
    
    /// <summary>Contenuto del messaggio</summary>
    public string Content { get; set; }
    
    /// <summary>Timestamp UTC di invio</summary>
    public DateTime Timestamp { get; set; }
    
    /// <summary>Flag di lettura (true = letto, false = non letto)</summary>
    public bool IsRead { get; set; }
}
```

**Proprietà:**
- `Sender`: Username di chi invia (required)
- `Receiver`: Username di chi riceve. `null` significa messaggio pubblico
- `Content`: Testo del messaggio
- `Timestamp`: Quando è stato inviato (UTC)
- `IsRead`: Stato di lettura (solo per messaggi privati)

### UserData.cs

Modello rappresentativo di un utente:

```csharp
public class UserData
{
    /// <summary>Nome utente univoco</summary>
    public string Username { get; set; }
    
    /// <summary>Password hashata (server-side)</summary>
    public string Password { get; set; }
    
    /// <summary>Data/ora ultimo logout</summary>
    public DateTime LastLogout { get; set; }
}
```

**Proprietà:**
- `Username`: Identificatore univoco dell'utente
- `Password`: Hash BCrypt della password (lato server), plain text (lato client)
- `LastLogout`: Timestamp dell'ultimo logout (usato per determinare messaggi "vecchi")

### LoginResult.cs

Enum rappresentante lo stato di un tentativo di login:

```csharp
public enum LoginResult
{
    /// <summary>Login riuscito</summary>
    Success = 0,
    
    /// <summary>Username/password non validi</summary>
    InvalidCredentials = 1,
    
    /// <summary>Utente già connesso da un'altra sessione</summary>
    AlreadyConnected = 2,
    
    /// <summary>Utente non trovato nel sistema</summary>
    UserNotFound = 3
}
```

## ?? Flusso di Comunicazione

```
???????????????????????????????????????????????????????
?                   Client Console                     ?
?  (ChatClientConsole)                                ?
???????????????????????????????????????????????????????
                     ?
                     ? Riferimento a ChatCommons
                     ? (ChatMessage, UserData, etc)
                     ?
                     ???????????????????????????????????
                     ?                                 ?
                     v                                 v
          ????????????????????????????????????????  
          ?       ChatCommons                    ?  
          ?  (Shared Classes)                    ?  
          ?  ??????????????????????????????????  ?  
          ?  ? ChatMessage                    ?  ?  
          ?  ? UserData                       ?  ?  
          ?  ? LoginResult                    ?  ?  
          ?  ? ChatEvents                     ?  ?  
          ?  ??????????????????????????????????  ?  
          ????????????????????????????????????????
                     ^
                     ? Riferimento a ChatCommons
                     ?
???????????????????????????????????????????????????????
?                   Server                            ?
?  (ChatAppConsole/ChatHub)                          ?
???????????????????????????????????????????????????????
```

## ?? Utilizzo nei Progetti

### Nel Server (ChatAppConsole)

```csharp
using ChatCommons;

public class ChatHub : Hub
{
    public void AddMessage(ChatMessage message)
    {
        // Logica con ChatMessage
    }
    
    public bool AddUser(UserData user)
    {
        // Logica con UserData
    }
}
```

### Nel Client (ChatClientConsole)

```csharp
using ChatCommons;

public class ChatManager
{
    public async Task SendPublicMessage(string content)
    {
        var message = new ChatMessage 
        { 
            Sender = _currentUser,
            Receiver = null,
            Content = content,
            Timestamp = DateTime.UtcNow
        };
        
        await _connection.InvokeAsync(
            ChatEvents.SendPublic, 
            message
        );
    }
}
```

## ?? Dipendenze

**Nessuna dipendenza esterna**. ChatCommons utilizza solo .NET Standard:
- System.Collections
- System.Runtime
- System.Linq

## ?? Principi di Design

1. **Condivisione**: Evita duplicazione di codice tra server e client
2. **Semplicità**: Modelli semplici e diretti
3. **Type-Safety**: Utilizzo di enum e classi fortemente tipizzate
4. **Decoupling**: Non dipende da implementazioni specifiche

## ?? Linee Guida per Modifiche

Se aggiungi nuove classi o proprietà:

1. ? Assicurati che siano serialize/deserializzabili da JSON
2. ? Documenta proprietà con XML comments
3. ? Utilizza `string?` per proprietà nullable
4. ? Usa `DateTime.UtcNow` per timestamp

## ?? Sicurezza

- **Password**: Non sono mai memorizzate in plain text lato server
- **Transmission**: SignalR gestisce l'encryption della trasmissione
- **Hashing**: BCrypt con work factor 12

## ?? Riferimenti

- [Microsoft: Shared Assemblies in .NET](https://learn.microsoft.com/en-us/dotnet/standard/assembly/)
- [SignalR Protocol](https://learn.microsoft.com/en-us/aspnet/core/signalr/protocol/)

---

**Progetto parte della soluzione ChatAppConsole**
