# AGENTS.md

This file helps AI coding agents become productive quickly in this repository.

## Project Snapshot

- Stack: .NET 10, C# 14, SignalR, xUnit.
- Solution shape:
  - `ChatServer`: ASP.NET Core + SignalR backend.
  - `ChatClientConsole`: interactive console client.
  - `ChatCommons`: shared contracts and crypto helpers.
  - `TestProject1`: unit tests.

For feature-level details, use the existing docs first:
- [Root overview](README.md)
- [Server docs](ChatServer/README.md)
- [Client docs](ChatClientConsole/README.md)
- [Shared library docs](ChatCommons/README.md)
- [Test suite scope](TestProject1/README.md)

## Build, Test, Run

Run from repository root unless noted.

For local builds, prefer restoring only from nuget.org:

```bash
dotnet restore ChatAppConsole.slnx --source https://api.nuget.org/v3/index.json
dotnet build ChatAppConsole.slnx --no-restore --configuration Debug /p:TreatWarningsAsErrors=true
dotnet test ChatAppConsole.slnx --no-build --configuration Debug --logger "trx;LogFileName=test-results.trx"
```

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

CI reference:
- [GitHub Actions workflow](.github/workflows/dotnet-ci.yml)

Test suite reference:
- [TestProject1 README](TestProject1/README.md)

## Architecture Boundaries

- Keep shared contracts/event names in `ChatCommons` so client/server stay aligned.
- `ChatServer/Program.cs` is the composition root for repository selection (InMemory/File/Scylla).
- `ChatClientConsole/Program.cs` auto-registers commands via reflection from `IClientCommand` implementations.
- Add new client commands by creating a new class in `ChatClientConsole/Commands` implementing `IClientCommand`.

## Conventions That Matter

- Prefer DI-registered services over static/global state.
- Preserve SignalR event name constants from `ChatCommons/ChatEvents.cs` (avoid hardcoded strings).
- Keep thread-safety patterns intact:
  - Server repositories use locking/concurrent collections.
  - Client UI output is synchronized in `UiService`.
- Keep repository abstraction clean: server logic depends on `IChatRepository`, not concrete implementations.

## Environment and Pitfalls

- Workspace may run from Windows over WSL path. Prefer running `dotnet` in the same environment where files are mounted.
- When running from Windows UNC paths (for example `\\wsl.localhost\...`), `dotnet test` may fail loading `testhost.dll`.
  - Prefer a non-UNC local path or run commands directly in a consistent WSL/local environment.
- Default server setup uses `ScyllaRepository`; local runs require Scylla config in `ChatServer/appsettings.json`.
  - If Scylla is unavailable for local development, switch DI registration in `ChatServer/Program.cs` to in-memory or file repository.
- Client endpoint is configured in `ChatClientConsole/appsettings.json` (`ChatSettings:ServerUrl`).
- HTTPS redirection is enabled on server; local cert/trust issues can block client connections.

## Test Boundaries

- Current baseline in `TestProject1` focuses on unit-testable behavior only.
- Integration tests are intentionally out of scope for now (real Scylla, real SignalR end-to-end).
- Before adding new tests, check [TestProject1 README](TestProject1/README.md) to avoid duplicating scope.

## Agent Working Rules

- Make minimal, targeted edits; avoid unrelated refactors.
- When changing behavior, update or add tests in `TestProject1` where feasible.
- Validate with build/test commands before finishing substantial changes.
- If repo behavior seems unclear, consult existing READMEs before inventing conventions.
