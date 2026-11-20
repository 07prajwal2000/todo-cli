# Configuration

Configuration is two files in one directory, plus two environment variables. There is no registry, no
`appsettings.json`, no machine-wide install and no config search path beyond the documented order.

## Where the config lives

Resolution order in `ConfigPaths.ResolveDirectory`:

1. The `TODO_CONFIG_DIR` environment variable, if set and non-blank.
2. **Windows only:** `%APPDATA%\todo` — that is, `Environment.SpecialFolder.ApplicationData`.
3. Otherwise: `${XDG_CONFIG_HOME}/todo` if `XDG_CONFIG_HOME` is set and non-blank, else
   `~/.config/todo`.

Note the third rule: **macOS deliberately uses the XDG layout**, not `~/Library/Application Support`.
One documented location for every POSIX host beats a per-platform special case, and it means the same
dotfile setup works on a Mac and on Linux.

| OS | Default directory |
| --- | --- |
| Windows | `C:\Users\<you>\AppData\Roaming\todo` |
| Linux | `~/.config/todo`, or `$XDG_CONFIG_HOME/todo` |
| macOS | `~/.config/todo`, or `$XDG_CONFIG_HOME/todo` |

Print the resolved paths without guessing:

```bash
todo config path
```

```text
C:\Users\you\AppData\Roaming\todo\config.json
config dir: C:\Users\you\AppData\Roaming\todo
```

::: tip Windows: Roaming, not Local
The directory is `ApplicationData`, which is `%APPDATA%` — the **Roaming** profile. On a domain-joined
machine with roaming profiles that means the config (including the credential copy) follows you
between machines. That is convenient and also means the service-account key travels. If that is not
acceptable, set `TODO_CONFIG_DIR` to a local path.
:::

## The files

| File | Purpose | Created by |
| --- | --- | --- |
| `config.json` | Non-secret settings | `todo init`, `todo config set` |
| `credentials.json` | A **copy** of the service-account JSON key | `todo init` only |

### `config.json`

```json
{
  "provider": "google-sheets",
  "spreadsheetId": "1AbCdEfGhIjKlMnOpQrStUvWxYz0123456789",
  "defaultSpace": "default",
  "credentialsFile": "credentials.json",
  "serverPort": 12000
}
```

| Field | Default | Meaning |
| --- | --- | --- |
| `provider` | `google-sheets` | Backend name. Only `google-sheets` is supported. |
| `spreadsheetId` | `""` | Target spreadsheet. Required — an empty value makes every command fail with exit 3. |
| `defaultSpace` | `default` | Space used when neither `-s` nor `TODO_SPACE` is given. Must be a valid space name. |
| `credentialsFile` | `credentials.json` | Filename recorded in the config. The CLI reads `credentials.json` from the config directory. |
| `serverPort` | `12000` | Default port for `todo server`, overridable with `-p`. |

Reading is tolerant of hand-edits: property names are matched case-insensitively, `//` comments are
skipped, and trailing commas are allowed. Unknown properties are ignored. Writes are always
pretty-printed camelCase JSON.

A file that exists but has a blank `spreadsheetId` is rejected with:

```text
✗ Spreadsheet ID is missing. Run 'todo init' or 'todo config set spreadsheetId <id>'.
```

### `credentials.json`

An exact copy of the service-account key you pointed `todo init` at. The CLI parses it as:

```json
{
  "type": "service_account",
  "client_email": "todo-cli@todo-cli-482913.iam.gserviceaccount.com",
  "private_key": "-----BEGIN PRIVATE KEY-----\n…\n-----END PRIVATE KEY-----\n"
}
```

Only `client_email` and `private_key` are consumed. Other fields are ignored. When `type` is present
it must be exactly `service_account`.

Because the CLI reads this copy, not your original download, you can delete the download after `init`
succeeds. That also makes moving machines trivial: copy the whole config directory.

::: danger This file is a credential
`credentials.json` is the private key, in plaintext, on disk. `todo` protects it with file
permissions only — see below — and nothing else. `todo` does not encrypt it, does not store it in a
keychain, and does not redact it from `todo doctor` output (which prints only the email).
:::

## File permissions

Every write goes through `AtomicFile.WriteAsync`, which:

1. Writes the content to a sibling temp file `<name>.<guid>.tmp`.
2. Sets the temp file's mode to `0600` (`UserRead | UserWrite`) **before** the rename, so the file is
   never readable by others, not even for the instant between creation and rename.
3. Renames the temp file over the destination.
4. Deletes the temp file on failure.

Permission tightening is skipped on Windows, where ACLs are inherited from the profile directory, and
is best-effort on filesystems that cannot express UNIX modes — a failed `chmod` never turns a
successful save into an error.

Verify on POSIX:

```bash
ls -l ~/.config/todo
# -rw------- 1 you you  config.json
# -rw------- 1 you you  credentials.json
```

If you end up with `-rw-r--r--`, the filesystem refused the mode. Move the config directory somewhere
that supports UNIX permissions, or accept the exposure.

## Environment variables

Exactly three variables are read. There is no other environment surface — no
`TODO_SPREADSHEET_ID`, no `TODO_CREDENTIALS_FILE`, no `GOOGLE_APPLICATION_CREDENTIALS` support.

| Variable | Read by | Effect |
| --- | --- | --- |
| `TODO_CONFIG_DIR` | `ConfigPaths.ResolveDirectory` | Overrides the config directory entirely. Trailing separators are trimmed. Blank is ignored. |
| `TODO_SPACE` | `SpaceResolver.ResolveAsync` | Default space when `-s` is not given. Blank is ignored, falling through to the configured `defaultSpace`. |
| `XDG_CONFIG_HOME` | `ConfigPaths.ResolveDirectory` | Base for `~/.config/todo` on non-Windows hosts. Ignored on Windows. |

::: warning No env-var override for the spreadsheet id or the credential path
`TODO_SHEET_ID`, `TODO_CREDENTIALS` and similar do not exist. The spreadsheet id and default space
live in `config.json`; the credential is always `<config dir>/credentials.json`. To point a shell
session at a different sheet or a different account, point `TODO_CONFIG_DIR` at a different directory
holding its own `config.json` and `credentials.json`. That is the supported mechanism for
"multiple profiles":

```bash
TODO_CONFIG_DIR=~/.config/todo-work todo ls
```

```powershell
$env:TODO_CONFIG_DIR = "$HOME\.config\todo-work"; todo ls
```
:::

`TODO_SPACE` is the exception, and it is genuinely useful for scoping a shell:

```bash
export TODO_SPACE=work
todo ls          # reads the 'work' space
todo add "…"     # adds to the 'work' space
```

## Changing settings with `todo config`

```bash
todo config path                    # where the files are
todo config show                    # current settings
todo config show --json             # machine-readable
todo config set <KEY> <VALUE>       # change one setting
```

Keys are matched case-insensitively, and each has an alias:

| Key | Alias | Validation |
| --- | --- | --- |
| `spreadsheetId` | `spreadsheet` | Bare id or Sheets URL that yields an id of at least 20 alphanumerics |
| `defaultSpace` | `space` | Must satisfy the space-name rules (see [Spaces](/guide/spaces#naming-rules)) |
| `serverPort` | `port` | Integer in 1–65535 |
| `provider` | — | Only `google-sheets` |

```bash
todo config set spreadsheetId https://docs.google.com/spreadsheets/d/1AbCdEf…/edit
todo config set defaultSpace work
todo config set serverPort 12100
```

Setting an unknown key is a validation error:

```text
✗ 'colour' is not a known setting. Use spreadsheetId, defaultSpace, serverPort or provider.
```

::: tip `config set` writes, it does not verify
A new `spreadsheetId` is stored without contacting Google. Mistyped ids and unshared sheets only
surface on the next command that talks to Sheets. Run `todo doctor` after changing it.
:::

## Editing the files by hand

`config.json` is safe to hand-edit and tolerant of comments and trailing commas. `credentials.json`
should only ever be replaced wholesale with another key file.

If you hand-edit and the result is unparseable:

```text
✗ The configuration at /home/you/.config/todo/config.json is not valid JSON: …
```

...which is exit **3**. Fix the JSON, or delete the file and re-run `todo init`.

## Reconfiguring from scratch

```bash
todo init --force     # re-prompts for key + spreadsheet, overwrites both files
```

To start completely clean, remove the directory first:

::: code-group

```bash [Linux / macOS]
rm -rf "${TODO_CONFIG_DIR:-${XDG_CONFIG_HOME:-$HOME/.config}/todo}"
todo init
```

```powershell [Windows (PowerShell)]
Remove-Item -Recurse -Force "$env:APPDATA\todo"
todo init
```

:::

Deleting the config does **not** touch the spreadsheet. Your tasks stay exactly where they are; the
next `todo init` re-attaches to the same sheet and finds them.

## Configuration checklist

| Question | Command |
| --- | --- |
| Where is my config? | `todo config path` |
| What is my effective configuration? | `todo config show` |
| Is the spreadsheet reachable and the schema sane? | `todo doctor` |
| Which service account email do I share the sheet with? | `todo doctor` (the `service account` row) |
| Which spaces exist? | `todo spaces` |
| What port will `todo server` use? | `todo config show`, `serverPort` row |
