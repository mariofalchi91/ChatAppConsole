# Copilot Instructions for ChatAppConsole

## Scope and Goal

This repository contains a .NET 10 real-time chat solution with 4 projects:
- `ChatServer`: ASP.NET Core + SignalR backend
- `ChatClientConsole`: interactive console client
- `ChatCommons`: shared contracts and crypto helpers
- `TestProject1`: xUnit tests

Goal for coding agents:
- make focused, minimal changes
- preserve client/server contract compatibility
- keep behavior consistent unless the task explicitly asks for changes

## Language for Chat

- Interact with the user in Italian in GitHub Copilot Chat responses, unless the user explicitly asks for another language.

## Always Start Here

Before implementing non-trivial changes, read:
- `AGENTS.md`
- `README.md`
- `ChatServer/README.md`
- `ChatClientConsole/README.md`
- `ChatCommons/README.md`

## Build, Test, Run

From repository root:

```bash
dotnet restore
dotnet build --configuration Debug /p:TreatWarningsAsErrors=true
dotnet test --configuration Debug --logger "trx;LogFileName=test-results.trx"
```

Run server:

```bash
cd ChatServer
dotnet run
```

Run client:

```bash
cd ChatClientConsole
dotnet run
```

## Codebase Rules

1. Keep shared SignalR event names in `ChatCommons/ChatEvents.cs`; do not introduce hardcoded duplicate strings in client/server.
2. Keep server logic depending on `IChatRepository`; do not couple business logic to a concrete repository.
3. Register new client commands via `IClientCommand` implementations under `ChatClientConsole/Commands`.
4. Preserve thread-safety patterns:
   - server repositories use locks/concurrent collections
   - client console output synchronization stays in `UiService`
5. Prefer DI-managed services over static/global state.

## Repository Selection and Local Dev

- `ChatServer/Program.cs` is the repository composition point.
- Default registration may use `ScyllaRepository`; local development can require switching to `InMemoryChatRepository` or `FileChatRepository` if Scylla is unavailable.
- Client endpoint is configured in `ChatClientConsole/appsettings.json`.

## Environment Pitfalls

- Workspace can be used through WSL path mapping from Windows.
- Run `dotnet` commands in the same environment where files are mounted to avoid path/tooling inconsistencies.
- HTTPS redirection and local certificate trust can affect SignalR connectivity.

## Change Discipline

- Avoid broad refactors unless requested.
- When behavior changes, add or update tests in `TestProject1` when feasible.
- Keep edits scoped to the user request.
- If conventions are unclear, follow existing code in nearby files before introducing new patterns.
