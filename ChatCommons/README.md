# ChatCommons - Shared Library

**ChatCommons** is a lightweight **.NET 10 Class Library** that acts as the strict contract between the `ChatServer` and `ChatClientConsole`. By centralizing domain models and network protocols, it ensures strong typing and eliminates magic strings across the entire distributed solution.

## 🧩 Core Components

* **Domain Models (`ChatMessage`)**: The standard payload for all data transfers. It is already architected for NoSQL persistence, featuring a `Guid Id` for distributed uniqueness and robust state tracking (`IsRead`, `Timestamp`).
* **SignalR Contracts (`ChatEvents`)**: A centralized registry of constant strings representing all WebSocket method invocations and event listeners (e.g., `ReceivePublic`, `SendPrivateMessageAsync`). This guarantees that the server and client are always tightly coupled at compile-time, preventing runtime typos.
* **Enumerations (`Enum.cs`)**: Defines standard application states, including robust authentication workflows (`LoginResult`) and message routing categories (`MessageType`, with upcoming support for `Group` channels).

## 💡 Architecture Note
This library must remain completely devoid of any database drivers or heavy dependencies. It is designed to be easily serialized/deserialized and shared across any future client implementation (e.g., Mobile Apps, Web Dashboards) without carrying backend overhead.