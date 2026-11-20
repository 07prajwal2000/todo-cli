# Install and first task

This page takes you from a downloaded binary to a working `todo ls`. Creating the Google credential
is the longest step; it has its own page: [Google Cloud setup](/guide/google-cloud-setup).

## Requirements

| Requirement | Notes |
| --- | --- |
| Windows, Linux or macOS on x64 or arm64 | Matches the four release artifacts below. |
| A Google account | The spreadsheet lives in a Google account you can sign into. |
| A Google Cloud project | Free. A service account and a Sheets API enablement are enough. |
| Network access to `sheets.googleapis.com` | Every command is a live API call. |

The binary is self-contained: no .NET runtime install is required. Building from source instead
requires the .NET 10 SDK — see [Support matrix](/reference/support-matrix).

## 1. Get the binary

Download the artifact for your platform from the releases page.

| Platform | Artifact |
| --- | --- |
| Windows x64 | `todo-win-x64.exe` |
| Linux x64 | `todo-linux-x64` |
| macOS Apple silicon | `todo-osx-arm64` |
| macOS Intel | `todo-osx-x64` |

Download the matching asset from the repository's releases page, then put it on `PATH`.

::: code-group

```bash [Linux]
# after downloading todo-linux-x64 into the current directory
chmod +x todo-linux-x64
sudo mv todo-linux-x64 /usr/local/bin/todo
```

```powershell [Windows (PowerShell)]
# after downloading todo-win-x64.exe into the current directory
New-Item -ItemType Directory -Force "$env:LOCALAPPDATA\Programs\todo" | Out-Null
Move-Item .\todo-win-x64.exe "$env:LOCALAPPDATA\Programs\todo\todo.exe"
# add $env:LOCALAPPDATA\Programs\todo to your user PATH once, then reopen the shell
```

```bash [macOS]
# after downloading todo-osx-arm64 (or todo-osx-x64) into the current directory
chmod +x todo-osx-arm64
xattr -d com.apple.quarantine todo-osx-arm64   # only if Gatekeeper blocks the unsigned binary
sudo mv todo-osx-arm64 /usr/local/bin/todo
```

:::

The Linux and macOS binaries are unsigned. On macOS, `xattr -d com.apple.quarantine` clears the
download flag so Gatekeeper stops refusing to launch it.

Confirm the binary works:

```bash
todo --version
```

```text
0.1.0
```

## 2. Create the Google credential

Short version, so you know where you are going: you need a service-account **JSON key file** and a
spreadsheet **shared with that service account's email address as Editor**. Both steps are covered in
detail — with the exact console buttons — on [Google Cloud setup](/guide/google-cloud-setup).

Do not skip the sharing step. A service account has no Drive UI, so an unshared sheet is invisible
to it and the first real command fails with 403.

## 3. Run `todo init`

```bash
todo init
```

It is an interactive prompt sequence. There are no flags to memorise beyond `--force`.

```text
todo init
Answers are stored only in your personal config directory.
Location: C:\Users\you\AppData\Roaming\todo

Path to your Google service account key (JSON): C:\keys\todo-sa.json
Spreadsheet ID or URL: https://docs.google.com/spreadsheets/d/1AbCdEfGhIjKlMnOpQrStUvWxYz0123456789/edit
Default space (sheet tab): default

✓ Configuration written and the sheet is ready.

Next: share the spreadsheet with this address as Editor:
  todo-cli@my-project-123456.iam.gserviceaccount.com
  https://docs.google.com/spreadsheets/d/1AbCdEfGhIjKlMnOpQrStUvWxYz0123456789

Config: C:\Users\you\AppData\Roaming\todo\config.json
Try: todo add "my first task" --due 2026-12-01
```

What happened behind those prompts:

1. The key file was validated: it must be JSON, `type` must be `service_account` (when present), and
   it must contain `client_email` and `private_key`.
2. A copy of the key was written to `credentials.json` inside the config directory with owner-only
   permissions on POSIX. Your original file is not referenced afterwards, so you can delete it.
3. `config.json` was written with the spreadsheet id and default space.
4. The spreadsheet was probed: the CLI authenticated, read the spreadsheet metadata, and created the
   `_meta` tab and the default space tab with their header rows if they were missing.

`todo init` accepts a bare spreadsheet id **or** a full Sheets URL. Ids shorter than 20 characters or
containing non-alphanumerics are rejected, as are URLs without a `/spreadsheets/d/` segment.

::: warning The order matters on a brand new machine
`todo init` verifies access immediately, so the spreadsheet must already be shared with the service
account before you run it. If you run `init` first, it fails at the probe step with a permission
error. Share the sheet, then re-run `todo init --force`.
:::

## 4. Verify with `todo doctor`

```bash
todo doctor
```

```text
settings          C:\Users\you\AppData\Roaming\todo\config.json
credentials       C:\Users\you\AppData\Roaming\todo\credentials.json
spreadsheet       My Tasks
spreadsheet id    1AbCdEfGhIjKlMnOpQrStUvWxYz0123456789
default space     default
service account   todo-cli@my-project-123456.iam.gserviceaccount.com
tabs              _meta, default
schema version    1
schema            ready (v1)
✓ Everything looks healthy.
```

`todo doctor` exits 0 when the schema is ready, 1 when the sheet does not match the schema, and 3
when configuration or credentials cannot be loaded at all. Add `--fix` to create missing tabs and
headers in place.

## 5. First task

```bash
todo add "write the docs" --due 2026-12-01 --desc "cover init, add, ls" -t pending
todo ls
todo start 1
todo done 1
```

Expected shapes:

```text
✓ Added #1 'write the docs' to 'default'.
```

```text
#   Name             Status    Due         Created
1   write the docs   pending   2026-12-01  2026-11-20
```

```text
✓ #1 'write the docs' is now ongoing.
```

```text
✓ #1 'write the docs' is now completed.
```

## 6. Optional: open the dashboard

```bash
todo server
```

This serves a board at `http://127.0.0.1:12000` and opens your browser unless you pass `--no-open`.
See [Dashboard](/guide/dashboard).

## Next steps

- Learn the flag surface for every command: [Commands](/guide/commands)
- Split work into worksheet tabs: [Spaces](/guide/spaces)
- Move the config somewhere else or point at a different sheet: [Configuration](/guide/configuration)
- Anything failed? [Troubleshooting](/guide/troubleshooting)
