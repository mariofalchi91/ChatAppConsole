# ChatAppConsole

High-performance .NET 10 messenger featuring a modern architecture optimized for massive scalability, ultra-low latency, and secure real-time communication.

## 🏗️ High-Level Architecture

The solution is built using **.NET 10** and **C# 14**, structured around a clean separation of concerns:

* **ChatServer**: The core ASP.NET Core backend powered by SignalR for real-time bi-directional communication. It uses a robust Dependency Injection pattern to seamlessly switch between repository implementations.
* **ChatClientConsole**: An interactive, command-based terminal application acting as the primary client.
* **ChatCommons**: A shared class library containing domain models, interfaces, and SignalR event contracts.
* **TestProject1**: The automated testing suite ensuring code reliability and preventing regressions.

## ✨ Current Features

* **Real-Time Messaging**: Instant public channels and one-to-one private chats via ASP.NET Core SignalR.
* **Pluggable Storage**: Interfaces designed for DI (`IChatRepository`), currently supporting `InMemoryChatRepository` and `FileChatRepository`, allowing hot-swapping of the persistence layer.
* **Robust Authentication**: Secure password hashing using `BCrypt.Net-Next`.
* **State Management**: Advanced session tracking, login/logout timestamps, and read receipt watermarks.

## 🚀 CI/CD Pipeline

The repository is protected by a strict **GitHub Actions Continuous Integration (CI)** pipeline. 
Every Pull Request targeting the `master` branch must pass the "Gold Standard" checks:
* Dependency restoration and security vulnerability scanning.
* Strict compilation (`TreatWarningsAsErrors=true`).
* Automated execution of the test suite (`TestProject1`).

## 🗺️ Future Roadmap

This project is actively evolving towards a production-ready, highly available distributed system:

- [ ] **ScyllaDB Persistence**: Migrating the storage layer to a high-performance C++ NoSQL database. We will utilize the official `ScyllaDBCSharpDriver` with Shard-Aware routing for sub-millisecond read/write latency.
- [ ] **Comprehensive Unit Testing**: Expanding `TestProject1` to achieve high code coverage across all core components, including robust mocks for repository and network interactions.
- [ ] **Zero-Trust End-to-End Encryption (E2EE)**: Implementing symmetric encryption using pre-shared keys (shared out-of-band). The server and database will only process and store ciphertext, ensuring a true Zero-Trust architecture where the infrastructure has zero visibility into message content.
- [ ] **Containerization**: Full Docker support for seamless deployment of the server, database, and background workers.

## ⚡ Quick Start

1. **Start the Server:**
   ```bash
   cd ChatServer
   dotnet run
   ```
   *The SignalR hub will listen on `https://localhost:7161/chat`.*

2. **Start the Client:**
   ```bash
   cd ChatClientConsole
   dotnet run
   ```
   *Connects automatically based on the `appsettings.json` configuration.*