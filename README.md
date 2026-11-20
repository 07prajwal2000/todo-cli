# todo

A single-binary .NET 10 CLI TODO manager where the only backend is a Google Sheet, reached through the
official Google Sheets v4 API. No database, no server-side account, no sync service — your tasks are rows
in a spreadsheet you already own. A local dashboard is available via `todo server`.

## Features

- **The sheet is the database.** Each space is a worksheet tab; the layout is declared in one place and
  columns are resolved by header name.
- **Service-account auth only.** One JSON key, one sharing step. No OAuth consent screen, no refresh
  tokens, nothing that expires after seven days.
- **Four states with explicit transitions.** `pending`, `ongoing`, `backlog`, `completed`, plus aliases
  (`done`/`wip`/`open`/`later`). Illegal moves are rejected. `completed_at` is stamped and cleared
  automatically.
- **Per-space auto-increment ids** that are never reused, so `#7` always means the same task.
- **RAW writes.** Sheets never coerces `1-2` into a date and a name starting with `=` stays text, which
  also prevents formula injection into your sheet.
- **Markdown notes.** `todo notes add|ls|show|edit|rm` stores simple notes (`id`, `title`, `content`,
  `created_at`) on a `_notes` tab, with a markdown viewer and editor in the dashboard.
- **Local-only dashboard.** `todo server` binds `127.0.0.1` only, on port 12000, with a request guard
  that rejects foreign origins and requires a client header for mutations.
- **Scriptable.** Stable exit codes (0 success, 1 failure, 2 usage, 3 configuration), `--json` output.
- **AI-agent friendly.** Every read command has `--json`, mutations accept `-y/--yes` to skip prompts,
  exit codes are stable, and the dashboard exposes the same operations as a local JSON HTTP API — so an
  agent never has to parse human-formatted output.
- **Honest packaging.** One self-contained, compressed binary per platform, ~52 MB. The official Google
  SDK is reflection-based and not trim/AOT safe, so trimming is deliberately off rather than shipping a
  binary that crashes.

## Quickstart

```bash
todo init                                    # service-account key path, then spreadsheet id/URL
todo add "write the docs" --due 2026-12-01
todo ls
```

```text
✓ Added #1 'write the docs' to 'default'.

#   Name             Status    Due         Created
1   write the docs   pending   2026-12-01  2026-11-26
```

Prerequisites, before `todo init` will succeed:

1. A Google Cloud project with the **Google Sheets API** enabled.
2. A **service account** in that project, with a downloaded **JSON key**.
3. A spreadsheet **shared with the service account's `client_email` as Editor**. This is the step people
   miss, and it is the reason setups fail.

The full walkthrough — exact console buttons, both failure modes, key rotation — is in
[`docs/guide/google-cloud-setup.md`](docs/guide/google-cloud-setup.md).

## Documentation

The documentation site lives in [`docs/`](docs/) and is built with VitePress. Run it locally:

```bash
cd docs
npm install
npm run docs:dev
```

Or build it: `cd docs && npm install && npm run docs:build` → output in `docs/.vitepress/dist`.

| Page | Contents |
| --- | --- |
| [Getting started](docs/guide/getting-started.md) | Install, `init`, first task, `doctor` |
| [Google Cloud setup](docs/guide/google-cloud-setup.md) | The full credential walkthrough |
| [Commands](docs/guide/commands.md) | Every command, flag, alias and exit code |
| [Spaces](docs/guide/spaces.md) | The tab model, naming rules, ids |
| [Notes](docs/guide/notes.md) | `todo notes`, the notes tab, the dashboard editor |
| [Dashboard](docs/guide/dashboard.md) | `todo server`, the HTTP API, the local-only security model |
| [Configuration](docs/guide/configuration.md) | Config locations, env vars, `todo config` |
| [Sheet schema](docs/reference/sheet-schema.md) | The column contract and what is safe to edit by hand |
| [Architecture](docs/reference/architecture.md) | Hexagonal layering and the extension recipes |
| [Support matrix](docs/reference/support-matrix.md) | RIDs, building from source, why the binary is large |
| [Troubleshooting](docs/guide/troubleshooting.md) | Error → cause → fix, exit codes |

## Build the single binary

Requires the .NET 10 SDK.

```bash
dotnet publish src/Todo.Cli/Todo.Cli.csproj \
  --configuration Release \
  --runtime linux-x64 \
  --self-contained true \
  --output artifacts/linux-x64 \
  -p:PublishSingleFile=true \
  -p:EnableCompressionInSingleFile=true \
  -p:DebugType=none
```

Swap `--runtime` for `win-x64`, `osx-arm64` or `osx-x64`. The output directory contains exactly one
file. Those four RIDs are what CI publishes as `todo-win-x64.exe`, `todo-linux-x64`, `todo-osx-arm64` and
`todo-osx-x64`.

Build and test the solution:

```bash
dotnet build Todo.slnx --configuration Release
dotnet test  Todo.slnx --configuration Release
```

## Dashboard

```bash
todo server              # http://127.0.0.1:12000, opens a browser
todo server -p 8080      # different port
todo server --no-open    # headless
```

Loopback only, by design. See [`docs/guide/dashboard.md`](docs/guide/dashboard.md).

## Project layout

```text
src/
  Todo.Domain                          core: tasks, states, transitions, schema, space names
  Todo.Application                     ports and services (TaskService, SpaceService, SetupService)
  Todo.Infrastructure.Configuration    config paths, JSON stores, credential store
  Todo.Infrastructure.GoogleSheets     Sheets gateway, repositories, retry, row mapping
  Todo.Web                             loopback dashboard host and embedded UI
  Todo.Cli                             composition root and commands
tests/
  Todo.Domain.Tests … Todo.Web.Tests   unit and integration tests
  Todo.Architecture.Tests              enforces the layer dependency rule
```

## License

No license has been chosen for this repository yet.
