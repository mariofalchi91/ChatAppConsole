# ChatServer - Core Backend

The **ChatServer** module represents the high-performance backend of the ChatApp solution. Developed in **ASP.NET Core 10**, it exposes a SignalR Hub for real-time bidirectional communication and implements an interface-driven architecture to ensure maximum scalability and flexibility of the data persistence layer.

## 🏗️ Architecture & Patterns

The server is designed following the principles of *Separation of Concerns* (SoC) and *Inversion of Control* (IoC):

* **SignalR Hub (`ChatHub`)**: Manages the lifecycle of WebSocket connections, in-memory session tracking (`ConnectedUsers`), and the routing of public and private messages.
* **Repository Pattern (`IChatRepository`)**: Completely abstracts the data access logic. The server is decoupled from any specific storage technology.
* **Dependency Injection (DI)**: Configured in `Program.cs`, it enables hot-swapping between different storage implementations (e.g., `InMemoryChatRepository`, `FileChatRepository`, and soon `ScyllaChatRepository`) by modifying a single line of code.

## ⚙️ Storage Implementations

Currently, the server supports three persistence engines:

### 1. FileChatRepository (File System Storage)
A thread-safe implementation optimized for disk performance:
* **JSONL (JSON Lines)**: Private messages are saved in an *append-only* fashion within separate files for each user pair (e.g., `private_alberto_mario.jsonl`). This guarantees lightning-fast writes and prevents file corruption.
* **Read Watermarks**: Uses an intelligent watermark-based approach (timestamps) saved in the user profile to dynamically compute the "Read/Unread" status of messages. This eliminates the massive I/O overhead of continuously updating and overwriting chat history files.
* **Fine-Grained Locking**: Implements advanced concurrency control (`_privateChatLocks`) to ensure that specific chat logs can be written concurrently without causing global thread bottlenecks.

### 2. InMemoryChatRepository (Volatile Storage)
Utilizes thread-safe collections like `ConcurrentBag<T>` and lock-protected dictionaries for ultra-fast in-memory operations. Ideal for unit testing environments or transient public message management.

### 3. ScyllaChatRepository (ScyllaDB Storage)
A high-performance, horizontally scalable implementation leveraging **ScyllaDB**:
* **Shard-Aware Driver**: Utilizes the C# ScyllaDB driver for efficient, low-latency operations.
* **Prepared Statements**: All queries are precompiled to minimize execution time and reduce network overhead.
* **Automatic Read/Write Management**: Handles private and public messages, user authentication, blocking, and unread notifications with minimal latency.

## 🔒 Security & Privacy

* **Password Hashing**: All passwords are cryptographically secured using the **BCrypt** algorithm (`BCrypt.Net-Next`) with a Work Factor of 12.
* **Advanced Blocking (Blacklist)**: Blocking logic is strictly enforced server-side. Messages sent by a blocked user undergo a *Silent Drop* (the sender receives the message locally without errors, but the recipient receives no payload), preventing user enumeration and spam.
* **Broadcast Exclusions**: Public message routing automatically excludes the connections of both blocking and blocked users at the Hub level.

## 🚀 Quick Start

1. Ensure your `appsettings.json` is configured in the project root:
   ```json
   {
     "ChatSettings": {
       "DataFolderPath": "./Data"
     }
   }
   ```
2. Restore dependencies and run the server:
   ```bash
   dotnet restore
   dotnet run
   ```
3. The server will expose the SignalR Hub at the `/chat` endpoint (e.g., `https://localhost:7161/chat`). It also includes an OpenApi/Swagger endpoint in the development environment and a simple health-check on the root (`/`).

## 🗺️ Server-Specific Roadmap

* **ScyllaDB Integration**: Implementation of the `ScyllaChatRepository` to leverage *Shard-Aware* drivers and horizontal scalability.
* **Resilient Disconnections**: Addition of advanced SignalR reconnection logic with in-flight message buffering for unstable networks (e.g., mobile connections).