# Test Suite Overview

This suite covers everything that has been assessed as **unit-testable** in the project, excluding real integration/network/database testing.

## What We Are Testing

### ChatCommons
- `CryptoService`
  - encrypt/decrypt roundtrip
  - invalid inputs (null/empty/malformed base64/short payload)
  - wrong password
  - UTF-8 and very large payloads
  - credential hash (expected value, determinism, case-insensitive username)
- `ChatEvents`
  - non-empty and unique constants
  - mapping of critical constants to names used by client/server
- `Enum`
  - values and order of `LoginResult` and `MessageType`
- `ChatMessage`
  - defaults and property assignment

### ChatServer
- `InMemoryChatRepository`
  - users, credentials, password change
  - public/private history
  - unread senders and watermark
  - block/unblock/case-insensitive blacklist
  - logout and last logout
  - edge cases for missing users
- `FileChatRepository`
  - users/blacklist persistence
  - public/private jsonl message persistence
  - read/unread watermark behavior
  - tolerance for corrupted lines in jsonl files
- `ChatHub`
  - register (success/fail)
  - login (already connected / success / invalid credentials)
  - public send (all and allExcept)
  - private send (receiver blocked and silent-drop)
  - change password, block/unblock, mark-as-read, get blocked list
  - on disconnected
  - private history in-memory branch
- Server config
  - DataAnnotations validation for `ChatSettings` and `ScyllaSettings`

### ChatClientConsole
- Commands (`Block`, `Unblock`, `PrivateChat`, `Password`, `Exit`, `Restore`, `BlockedList`, `Help`)
  - `CanExecute`
  - main `ExecuteAsync` branches
  - side-effect checks toward manager/network/ui (mocked)
- `ChatManager`
  - blocked cache initialization
  - block/unblock
  - public/private refresh routing
  - private switch (user not found / success + mark read)
- Client config
  - DataAnnotations validation for `ClientSettings`

## What Is Still Out of Scope (Important)

### Out by choice (non-integration)
- `ScyllaRepository` with a real Scylla/Cassandra DB
- `NetworkService` against a real SignalR connection (remote hub)
- full client/server end-to-end flows

### Out due to intrinsic difficulty or low pure-unit ROI
- `UiService` with real console I/O (colors/cursor/readkey)
- `ClientApp` as full orchestrator (`RunAsync`, login loop, chat loop, event wiring) without introducing additional seams

## Notes on Production Code Changes

To improve unit testability without changing functional behavior:
- some client-side classes/methods were changed to `public`/`virtual`:
  - `BlockCommand` and `PasswordCommand` were made public
  - main public methods in `UiService` and `NetworkService` were made virtual
  - main public methods in `ChatManager` were made virtual

## Running Tests

Typical commands:

```bash
dotnet restore ChatAppConsole.slnx --source https://api.nuget.org/v3/index.json
dotnet build ChatAppConsole.slnx --no-restore --configuration Debug /p:TreatWarningsAsErrors=true
dotnet test --configuration Debug --logger "trx;LogFileName=test-results.trx"
```

If the environment runs from a Windows UNC path, some test hosts may fail while loading `testhost.dll`; in that case use a non-UNC local path or a consistent WSL/local environment.
