# Commands

Every command below matches `src/Todo.Cli/Commands/*.cs` and the registrations in
`src/Todo.Cli/Composition/CommandRegistrar.cs`. Flag names, aliases and defaults are exactly what the
binary accepts; `todo <command> --help` prints the same list.

## Command surface

```text
todo init                 Configure credentials and spreadsheet, then prepare the sheet
todo add <NAME>           Add a task to a space
todo ls                   List tasks (alias: list)
todo show <ID>            Show one task in detail
todo edit <ID>            Edit a task's fields
todo status <ID> <STATE>  Move a task to a specific state
todo done <ID>            Shortcut for 'status <id> completed'
todo start <ID>           Shortcut for 'status <id> ongoing'
todo reopen <ID>          Shortcut for 'status <id> pending'
todo rm <ID>              Delete one or more tasks from the sheet
todo spaces               List spaces (sheet tabs)
todo space add <NAME>     Create a space
todo space rm <NAME>      Delete a space and its tasks
todo doctor               Check configuration, credentials and sheet schema
todo server               Serve the local dashboard
todo config <ACTION>      Inspect or change configuration
```

`list` exists as a hidden alias for `ls` — it works but is not advertised in help. The root
`--version` flag reports `0.1.0`.

## Options shared by task commands

`add`, `ls`, `show`, `edit`, `status`, `done`, `start`, `reopen`, `rm` and `server` all inherit the
same two groups of options.

| Option | Description |
| --- | --- |
| `-s`, `--space <SPACE>` | Space (sheet tab) to act on. Resolution order: this flag, then `$TODO_SPACE`, then the configured default space. |
| `--json` | Emit machine-readable JSON instead of a table. |
| `-v`, `--verbose` | Accepted and parsed. Currently has no effect — see the note below. |
| `--no-color` | Accepted and parsed. Currently has no effect — see the note below. |

::: warning `-v` and `--no-color` are declared but not wired up
Both flags are declared on `GlobalSettings` and appear in `--help`, and `todo -v …` / `todo --no-color
…` parse without error. No code reads `GlobalSettings.Verbose` or `GlobalSettings.NoColor`, so neither
changes any behaviour: there is no extra diagnostic output, and ANSI colour is not suppressed. To get
plain output today, redirect through a pager or strip escapes; to get colour off, use
`--json` where it is available.
:::

`spaces`, `space add`, `space rm`, `doctor` and `config` do **not** inherit these — they have no
`-s`, and only `spaces` and `config show` have `--json`.

## init

Interactive. Creates the config directory, writes `config.json` and `credentials.json`, probes the
spreadsheet, and creates the `_meta` and default-space tabs with their header rows if missing.

```bash
todo init
```

| Option | Description |
| --- | --- |
| `--force` | Overwrite an existing configuration. |

Without `--force`, running `init` when `config.json` already exists prints a warning, tells you to
re-run with `--force`, and exits **1**.

Three prompts, all validated before anything is written:

| Prompt | Validation |
| --- | --- |
| Path to your Google service account key (JSON) | File must exist and parse as a service-account key with `client_email` and `private_key`. |
| Spreadsheet ID or URL | Must yield an id of at least 20 alphanumeric characters, either bare or from a `/spreadsheets/d/<id>` URL. |
| Default space (sheet tab) | Defaults to `default`. Must satisfy the space-name rules. |

```bash
todo init --force
```

On success you get `✓ Configuration written and the sheet is ready.` followed by the service
account email and the spreadsheet URL to share with it.

## add

Create a task. The target space is created automatically if it does not exist yet.

```bash
todo add <NAME> [OPTIONS]
```

| Argument / option | Description |
| --- | --- |
| `<NAME>` | Task name (required). Trimmed. Max 200 characters. |
| `-d`, `--desc <TEXT>` | Longer description. Max 4000 characters. |
| `--due <DATE>` | Due date as `yyyy-MM-dd`. |
| `-t`, `--status <STATE>` | Initial state. Default `pending`. All aliases accepted. |
| `--remarks <TEXT>` | Free-form remarks. Max 4000 characters. |

Examples:

```bash
todo add "renew the domain"
todo add "write the release notes" --due 2026-12-05 -t backlog
todo add "call the bank" -d "ask about the wire transfer" --remarks "ref 88213" --due 2026-11-28
todo add "ship v1" -s work -t ongoing --json
```

```json
{
  "id": 4,
  "name": "ship v1",
  "description": "",
  "status": "ongoing",
  "createdAt": "2026-11-20T09:41:07.1234567+00:00",
  "remarks": ""
}
```

A bad date or state fails before any network call:

```text
✗ '05/12/2026' is not a valid date. Use yyyy-MM-dd, for example 2026-12-01.
```

## ls

List tasks. Also registered as `list`.

```bash
todo ls [OPTIONS]
```

| Option | Description |
| --- | --- |
| `--status <STATE>` | Filter by state. Comma-separate for several. Aliases accepted. |
| `--all` | Include backlog and completed tasks. |
| `--due-before <DATE>` | Only tasks due on or before this `yyyy-MM-dd` date. |
| `--sort <FIELD>` | `default`, `due`, `id`, `status`, `created`. `state` is accepted as an alias for `status`. |

### Default state filter

Without `--status` and without `--all`, `ls` shows **ongoing and pending only**. With `--all` it
includes backlog and completed. An explicit `--status` wins over both.

Resolution order inside `TaskService.ApplyFilter`:

1. If `--status` was given, that set is used verbatim. `--all` is then irrelevant.
2. Otherwise, if `--all` was given, no state filter is applied at all.
3. Otherwise the set is `{ongoing, pending}`.

### Default sort

`--sort default` — which is what you get with no flag — orders by:

1. state priority: **ongoing → pending → backlog → completed**
2. due date ascending, **undated last**
3. id ascending

Other sort fields:

| `--sort` value | Ordering |
| --- | --- |
| `default` | state priority, then due ascending (undated last), then id |
| `due` | due ascending with undated last, then id. State is ignored. |
| `id` | id ascending |
| `status` | state priority, then id |
| `created` | `created_at` **descending**, then id |

Examples:

```bash
todo ls
todo ls --all
todo ls --status backlog,pending
todo ls --status done --sort created
todo ls --due-before 2026-11-30 --sort due
todo ls -s work --all --json
```

```json
{
  "space": "work",
  "tasks": [
    {
      "id": 2,
      "name": "review the RFC",
      "description": "",
      "status": "ongoing",
      "dueOn": "2026-11-25",
      "createdAt": "2026-11-18T08:00:00+00:00",
      "remarks": ""
    }
  ]
}
```

::: tip `ls` has no text search
There is no `--search` flag on the CLI. Search exists only in the dashboard, as the `q` query
parameter on `GET /api/tasks`. Use your shell: `todo ls --json | jq '.tasks[] | select(.name | test("bank";"i"))'`.
:::

## show

Print one task in full.

```bash
todo show <ID> [OPTIONS]
```

| Argument | Description |
| --- | --- |
| `<ID>` | Numeric task id, scoped to the resolved space. |

```bash
todo show 7
```

```text
id            7
space         default
name          call the bank
status        pending
due           2026-11-28 (in 8d)
created       2026-11-20 09:41 UTC
description   ask about the wire transfer
remarks       ref 88213
```

A missing id is a not-found error, exit **1**:

```text
✗ Task 99 was not found in space 'default'.
```

## edit

Replace individual fields. Every field flag is optional, but at least one is required.

```bash
todo edit <ID> [OPTIONS]
```

| Option | Description |
| --- | --- |
| `--name <TEXT>` | Replace the name. |
| `-d`, `--desc <TEXT>` | Replace the description. |
| `--due <DATE>` | Set the due date (`yyyy-MM-dd`). |
| `--clear-due` | Remove the due date. |
| `--remarks <TEXT>` | Replace the remarks. |

Rules enforced before any network call:

- `--due` and `--clear-due` are mutually exclusive: `Use either --due or --clear-due, not both.`
- With no field flags at all: `Nothing to change. Pass at least one of --name, --desc, --due, --clear-due or --remarks.`
- Only the supplied fields are validated, so `--due` alone does not re-validate the existing name.
- `edit` cannot change the state. Use `todo status`, `done`, `start` or `reopen`.

```bash
todo edit 4 --name "write the 1.0 release notes"
todo edit 4 --due 2026-12-12
todo edit 4 --clear-due
todo edit 4 -d "" --remarks ""
```

::: warning Clearing is not the same as omitting
Passing `-d ""` clears the description. Omitting `-d` leaves it alone. The same applies to
`--remarks`. Due dates are cleared with the explicit `--clear-due` flag rather than a blank value.
:::

## status

Move a task to a specific state.

```bash
todo status <ID> <STATE> [OPTIONS]
```

| Argument | Description |
| --- | --- |
| `<ID>` | Numeric task id. |
| `<STATE>` | Target state. Canonical names and all aliases accepted. |

```bash
todo status 4 ongoing
todo status 4 done
todo status 4 backlog
```

### Canonical states

| Canonical | Meaning |
| --- | --- |
| `pending` | Queued, next up. The default for new tasks. |
| `ongoing` | Actively being worked on. |
| `backlog` | Deliberately parked. |
| `completed` | Finished. |

### Aliases

The CLI and the dashboard's `status` field accept these interchangeably with the canonical names:

| Input | Resolves to |
| --- | --- |
| `done`, `complete`, `closed` | `completed` |
| `wip`, `doing`, `started` | `ongoing` |
| `open`, `todo` | `pending` |
| `icebox`, `later` | `backlog` |

Matching is case-insensitive and surrounding whitespace is trimmed. Only canonical names are ever
written to the sheet.

### Allowed transitions

| From | May move to |
| --- | --- |
| `backlog` | `pending`, `ongoing` |
| `pending` | `ongoing`, `backlog`, `completed` |
| `ongoing` | `completed`, `pending`, `backlog` |
| `completed` | `pending`, `ongoing` |

Moving a task to the state it already has is a no-op and still exits 0.

Any other move is rejected as a validation error, which means **exit code 2**:

```text
✗ Cannot move a task from completed to backlog. From completed you may move to: pending, ongoing.
```

`completed_at` is maintained by the domain, not by command code:

- Transitioning **into** `completed` stamps `completed_at` with the current UTC instant.
- Any transition **away from** `completed` clears `completed_at` to blank.

## done, start, reopen

Fixed-target shortcuts for `status`. They take only an `<ID>` plus the shared options.

```bash
todo done <ID>      # status <ID> completed
todo start <ID>     # status <ID> ongoing
todo reopen <ID>    # status <ID> pending
```

They are literally the same operation: `StateCommands.ApplyAsync` is shared, so the transition rules
and the `completed_at` maintenance are identical.

```bash
todo start 4
# ✓ #4 'write the 1.0 release notes' is now ongoing.

todo done 4
# ✓ #4 'write the 1.0 release notes' is now completed.
```

::: warning `reopen` targets pending, not backlog
`todo reopen` moves a task to `pending`. From `completed` the only reachable states are `pending` and
`ongoing`, so moving a finished task straight to `backlog` requires two steps:
`todo reopen 4 && todo status 4 backlog`.
:::

## rm

Delete one or more tasks. Deletion removes the row from the sheet with `DeleteDimension`, so ids are
never recycled.

```bash
todo rm <ID> [OPTIONS]
```

| Argument / option | Description |
| --- | --- |
| `<ID>` | A single id, or a comma-separated list. |
| `-y`, `--yes` | Skip the confirmation prompt. |

```bash
todo rm 4
todo rm 4,5,9 -y
```

Every id is resolved first. If any id is missing, the command fails with a not-found error (exit 1)
**before deleting anything**, so a batch is all-or-nothing at the lookup stage:

```text
✗ Task 9 was not found in space 'default'.
```

The prompt lists what is about to go:

```text
Delete 2 task(s) from 'default'?
  #4 write the 1.0 release notes
  #5 renew the domain
[Y/n]
```

Answering no prints `Cancelled.` and exits **0** — a deliberate cancel is not an error.

Ids that are not positive integers, and any input that yields no ids at all, produce
`'<input>' does not contain any task ids.` with exit **2**.

## spaces

List every space (worksheet tab) in the spreadsheet. Internal tabs whose name starts with `_` are
excluded, so `_meta` never appears.

```bash
todo spaces [--json]
```

```text
  default (default)
  home
  work
```

```json
{
  "spaces": ["default", "home", "work"],
  "defaultSpace": "default"
}
```

Ordering is case-insensitive alphabetical. The configured default space is marked `(default)` in the
table output; in JSON it is the `defaultSpace` field.

## space add

Create a space. This adds a worksheet tab and immediately writes the header row.

```bash
todo space add <NAME>
```

```bash
todo space add work
# ✓ Created space 'work'.
```

A duplicate name is a conflict, exit **1**:

```text
✗ Space 'work' already exists.
```

An invalid name is a validation error, exit **2**. See [Spaces](/guide/spaces) for the naming rules.

## space rm

Delete a space and every task in it. The worksheet tab is deleted.

```bash
todo space rm <NAME> [OPTIONS]
```

| Option | Description |
| --- | --- |
| `-y`, `--yes` | Skip the confirmation prompt. |

```bash
todo space rm work
todo space rm work -y
```

::: danger This is a row-and-tab deletion, not a flag
`todo space rm` deletes the worksheet tab, and with it every task in that tab. Google keeps the
deleted tab in the spreadsheet's version history for a while, so recovery is usually possible via
**File → Version history**, but `todo` itself has no undo.

The default space cannot be deleted:

```text
✗ The default space cannot be removed.
```
:::

## doctor

Read-only health check across config, credentials, spreadsheet access and sheet schema.

```bash
todo doctor [OPTIONS]
```

| Option | Description |
| --- | --- |
| `--fix` | Recreate missing tabs and headers when the schema is not ready. |

```bash
todo doctor
todo doctor --fix
```

Exit codes from `doctor` specifically:

| Exit | Meaning |
| --- | --- |
| `0` | Schema ready, or repaired successfully. |
| `1` | Sheet does not match the schema and `--fix` was not given, or `--fix` could not fully repair it. |
| `3` | Config or credentials could not be loaded. Accompanied by a hint to run `todo init`. |

`--fix` prints the actions it took:

```text
• Ensured '_meta' tab and schema_version.
• Wrote the header row of 'work'.
• Appended missing header(s) to 'home': remarks.
```

::: warning `--fix` never moves existing data
Repair only writes a missing header row, or appends missing column names to the **end** of an
existing header row. It will not reorder, rename or delete anything, because moving a column would
silently re-associate existing data with a different field.

If required columns are present but in a shape it cannot repair, it says so and exits 1:

```text
! Some columns are still out of place. Fix the header row by hand: existing data is never moved automatically.
```
:::

## server

Serve the local dashboard.

```bash
todo server [OPTIONS]
```

| Option | Description |
| --- | --- |
| `-p`, `--port <PORT>` | Port to listen on. Loopback only. Defaults to the configured `serverPort`, which is `12000`. |
| `--no-open` | Do not open the dashboard in a browser. |

```bash
todo server
todo server -p 8080
todo server --no-open
```

The server binds **`http://127.0.0.1:<port>` only** and never `0.0.0.0`. A port outside 1–65535 is
rejected with exit **2**; a port already in use fails with an unavailable error and exit **1**. Full
details, including the HTTP contract and the local-only security model, are on [Dashboard](/guide/dashboard).

## config

Inspect or change configuration without re-running the interactive setup.

```bash
todo config <ACTION> [KEY] [VALUE] [OPTIONS]
```

| Argument / option | Description |
| --- | --- |
| `<ACTION>` | `path`, `show` or `set`. Any other value is a validation error. |
| `[KEY]` | Setting key for `set`. |
| `[VALUE]` | New value for `set`. |
| `--json` | Machine-readable JSON. `show` only. |

### config path

Prints the config file path and the directory. Takes no arguments.

```bash
todo config path
```

```text
C:\Users\you\AppData\Roaming\todo\config.json
config dir: C:\Users\you\AppData\Roaming\todo
```

### config show

Prints the effective settings.

```bash
todo config show
todo config show --json
```

```text
provider          google-sheets
spreadsheetId     1AbCdEfGhIjKlMnOpQrStUvWxYz0123456789
defaultSpace      default
credentialsFile   credentials.json
serverPort        12000
location          C:\Users\you\AppData\Roaming\todo\config.json
```

```json
{
  "provider": "google-sheets",
  "spreadsheetId": "1AbCdEfGhIjKlMnOpQrStUvWxYz0123456789",
  "defaultSpace": "default",
  "credentialsFile": "credentials.json",
  "serverPort": 12000
}
```

### config set

Writes one setting back to `config.json`.

```bash
todo config set <KEY> <VALUE>
```

| Key | Aliases | Validation |
| --- | --- | --- |
| `spreadsheetId` | `spreadsheet` | Must be a bare id or a Sheets URL that yields an id |
| `defaultSpace` | `space` | Must satisfy the space-name rules |
| `serverPort` | `port` | Integer in 1–65535 |
| `provider` | — | Only `google-sheets` is accepted |

```bash
todo config set spreadsheetId 1AbCdEfGhIjKlMnOpQrStUvWxYz0123456789
todo config set spreadsheetId https://docs.google.com/spreadsheets/d/1AbCdEfGhIjKlMnOpQrStUvWxYz0123456789/edit
todo config set defaultSpace work
todo config set serverPort 12100
```

Key matching is case-insensitive and whitespace-trimmed. Success prints `Set <key>.` with exit 0.

::: tip `config set` does not verify the sheet
It writes the value and exits. A wrong spreadsheet id or a sheet that is not shared only surfaces the
next time a command talks to Google. Run `todo doctor` afterwards.
:::

## Exit codes

The same four codes are used across the CLI, derived from the application error code in
`ExitCodeMapper`.

| Code | Name | When |
| --- | --- | --- |
| `0` | Success | Command completed. Includes a deliberately cancelled prompt and a no-op state change. |
| `1` | Failure | Not found, conflict, or the backend is unavailable/unexpected. Also: `todo init` without `--force` when config exists, and a schema mismatch from `doctor`. |
| `2` | Usage | Validation errors and argument parsing errors. Includes disallowed state transitions, bad dates, bad space names, invalid ports, unknown states. |
| `3` | Configuration | Config file or credentials missing/unreadable, or a permission error from Google. |

Mapping, from `ErrorCode` to exit code:

| Application error | Exit |
| --- | --- |
| `None` | 0 |
| `Validation` | 2 |
| `Configuration`, `Permission` | 3 |
| `NotFound`, `Conflict`, `Unavailable`, `Unexpected` | 1 |

A `CommandParseException` from Spectre.Console.Cli — an unknown command or an unknown flag — is also
exit **2**, and Ctrl+C returns **0** after printing `Cancelled.`

## Global behaviour worth knowing

- All errors are one red line, with field-keyed messages underneath when the validator has them.
  No stack traces; `Program.Handle` catches everything.
- `--json` output is pretty-printed camelCase and omits `null` optional fields. Task objects expose
  `id`, `name`, `description`, `status`, `dueOn`, `createdAt`, `completedAt`, `remarks`.
- Status is always emitted in canonical form in JSON (`ongoing`, not `wip`), so scripts never have to
  normalise aliases.
