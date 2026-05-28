# Copilot Instructions for ChatAppConsole

This file intentionally stays minimal to avoid duplicating repository guidance.

For architecture, conventions, build/test commands, environment pitfalls, and test boundaries, use `AGENTS.md` as the single source of truth.

## Language for Chat

- Interact with the user in Italian in GitHub Copilot Chat responses, unless the user explicitly asks for another language.

## Always Start Here

Before implementing non-trivial changes, read:
- `AGENTS.md`
- `README.md`
- `ChatServer/README.md`
- `ChatClientConsole/README.md`
- `ChatCommons/README.md`
- `TestProject1/README.md`

## Scope Reminder

- Keep changes focused and minimal.
- Preserve client/server contract compatibility.
- Unless explicitly requested, avoid behavior changes.
