# ChatClientConsole - Interactive Terminal Client

The **ChatClientConsole** is a robust, interactive terminal application acting as the primary user interface for the ChatApp ecosystem. Built with **.NET 10**, it provides a seamless real-time messaging experience through a highly decoupled, asynchronous, and thread-safe architecture.

## 🏗️ Architecture & Design Patterns

The client is engineered to handle unpredictable, real-time asynchronous events without compromising the user experience or locking the main thread.

* **Dependency Injection (DI)**: Utilizes `Microsoft.Extensions.DependencyInjection` to decouple services (`NetworkService`, `UiService`, `ChatManager`). All dependencies are strictly validated at startup.
* **Command Pattern with Reflection**: User commands (e.g., `#password`, `#block username`, `#help`, `@username`) are abstracted behind the `IClientCommand` interface. The `Program.cs` uses Reflection to dynamically discover and register all commands at runtime, adhering to the Open/Closed Principle (OCP).
* **Thread-Safe UI (`UiService`)**: Writing to the console asynchronously while the user is typing can cause severe visual glitches. The `UiService` implements strict locking mechanisms (`Lock _consoleLock`) to gracefully handle incoming messages, clear the current line, print the message, and restore the user's prompt without text corruption.


## 🧩 Core Services

* **`ClientApp`**: The application's orchestrator. Manages the main infinite loop, the login flow, and acts as the router between user input (commands vs. chat messages) and the underlying services.
* **`NetworkService`**: The dedicated SignalR wrapper. It encapsulates the `HubConnection` and translates raw network events into strongly-typed C# events (`Action<string, string>`), completely shielding the rest of the app from SignalR dependencies.
* **`ChatManager`**: The state machine. Tracks the current view (Public vs. Private), caches the blocked users list locally to prevent redundant network calls, and processes read receipts (watermarks) when entering a private room.

## 🔒 Client-Side Security

The client implements a **Pre-Hashing Security Model**:
Passwords are never transmitted to the server in plain text. The `NetworkService` computes a local SHA-256 hash using the password, a user-specific salt (username), and a statically configured secret pepper (`ClientPepper`). The server only receives and stores this pre-hashed payload.

## 🚀 Quick Start

1. Ensure your `appsettings.json` is correctly configured in the root of the `ChatClientConsole` project:
   ```json
   {
     "ChatSettings": {
       "ServerUrl": "http://localhost:5000/chat",
       "ClientPepper": "YOUR_SUPER_SECRET_MIN_10_CHARS_PEPPER"
     }
   }
   ```
2. Run the client:
   ```bash
   dotnet build
   dotnet run
   ```
3. Follow the on-screen instructions to register a new account or log in.