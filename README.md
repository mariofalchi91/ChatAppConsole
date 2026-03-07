# ChatAppConsole - Real-Time Chat Application

Una applicazione di chat real-time completa costruita con **ASP.NET Core SignalR**, **.NET 10**, e **C# 14**.

## ?? Panoramica della Soluzione

Questa soluzione è composta da **3 progetti** che collaborano per fornire un'esperienza di chat completa:

### Progetti

| Progetto | Tipo | Descrizione |
|----------|------|-------------|
| **ChatAppConsole** | ASP.NET Core Web | Server di chat con SignalR Hub |
| **ChatCommons** | Class Library | Classi comuni e interfacce condivise |
| **ChatClientConsole** | Console App | Client console interattivo |

## ?? Caratteristiche Principali

- ? **Messaggi Pubblici** - Conversazioni in tempo reale con tutti gli utenti connessi
- ? **Messaggi Privati** - Chat uno-a-uno tra utenti
- ? **Autenticazione Sicura** - Password hashate con BCrypt
- ? **Gestione Sessioni** - Tracciamento login/logout degli utenti
- ? **Cronologia Messaggi** - Recupero storia chat pubblica e privata
- ? **Notifiche Lettura** - Messaggi non letti e stato lettura
- ? **Cambio Password** - Modifica sicura della password
- ? **In-Memory Repository** - Database in memoria (espandibile)

## ?? Tecnologie Utilizzate

- **.NET 10** (Long-Term Support)
- **C# 14** con implicit usings
- **ASP.NET Core SignalR** - Comunicazione real-time
- **BCrypt.Net-Next** - Hashing password sicuro
- **Dependency Injection** - Pattern DI integrato
- **Logging** - Microsoft.Extensions.Logging

## ?? Dipendenze Esterne

### ChatAppConsole
```xml
<PackageReference Include="BCrypt.Net-Next" Version="4.0.3" />
<PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.0.2" />
```

### ChatClientConsole
```xml
<PackageReference Include="Microsoft.AspNetCore.SignalR.Client" Version="10.0.2" />
<PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="10.0.2" />
<PackageReference Include="Microsoft.Extensions.Logging.Debug" Version="10.0.0" />
<PackageReference Include="Microsoft.Extensions.Options.ConfigurationExtensions" Version="10.0.2" />
<PackageReference Include="Microsoft.Extensions.Options.DataAnnotations" Version="10.0.2" />
```

## ??? Architettura

```
ChatAppConsole (Solution)
??? ChatAppConsole (Server - ASP.NET Core)
?   ??? ChatHub.cs - SignalR Hub principale
?   ??? Repository/
?   ?   ??? IChatRepository - Interfaccia repository
?   ?   ??? InMemoryChatRepository - Implementazione in-memory
?   ?   ??? UserData.cs - Modello utente
?   ??? Program.cs - Configurazione server
?
??? ChatCommons (Shared Classes)
?   ??? ChatEvents.cs - Costanti eventi SignalR
?   ??? Modelli comuni (ChatMessage, UserData, LoginResult)
?
??? ChatClientConsole (Client - Console App)
    ??? Program.cs - Entry point
    ??? ClientApp.cs - Logica applicazione
    ??? NetworkService.cs - Connessione SignalR
    ??? ChatManager.cs - Gestione chat
    ??? UiService.cs - Interfaccia utente
    ??? Commands/ - Pattern command per comandi utente
    ??? Configs/ - ChatSettings configurazione
    ??? appsettings.json - File configurazione
```

## ?? Quick Start

### Avvio del Server

```bash
cd ChatAppConsole
dotnet run
```

Il server sarà in ascolto su `http://localhost:5000`

### Avvio del Client

```bash
cd ChatClientConsole
dotnet run
```

Il client si connetterà al server configurato in `appsettings.json`

## ?? Documentazione

Vedi i README specifici di ogni progetto:
- [`ChatAppConsole/README.md`](./ChatAppConsole/README.md) - Documentazione server
- [`ChatCommons/README.md`](./ChatCommons/README.md) - Documentazione classi comuni
- [`ChatClientConsole/README.md`](./ChatClientConsole/README.md) - Documentazione client

## ?? Requisiti di Sistema

- **.NET 10 SDK** o superiore
- Windows, Linux, o macOS

## ?? Licenza

Questo progetto è fornito come è. Sentiamo liberi di utilizzarlo e modificarlo secondo le vostre necessità.

## ?? Contributi

I contributi sono benvenuti! Sentiti libero di aprire issue e pull request.

---

**Creato con ?? usando .NET 10 e C# 14**
