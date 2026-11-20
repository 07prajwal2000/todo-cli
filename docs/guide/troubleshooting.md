# Troubleshooting

Fixes are ordered by likelihood. Every message quoted here is copied from the source, so you can
search for it verbatim.

## First response to any failure

```bash
todo doctor
```

It checks, in order: can the config be read, can the credential be read, can Google authenticate, does
the spreadsheet exist, are the tabs there, do the headers match the schema. Whatever it prints is
closer to the real problem than the error message from the command you were running.

```bash
todo config show          # is the spreadsheet id what you expect?
todo config path          # is there a config on this machine at all?
```

## Error → cause → fix

### Google API errors {#google-api-errors}

| Message | Cause | Fix |
| --- | --- | --- |
| `Google Sheets denied access to spreadsheet '<id>'. Share the spreadsheet with <email> as Editor.` | The sheet is not shared with the service account, is shared with a different address, or is shared with a role below Editor. | Open the sheet's **Share** dialog, add the exact `client_email` from the JSON key as **Editor**. See [step 5](/guide/google-cloud-setup#step-5). This is the single most common failure. |
| `Google Sheets rejected the request. Share the spreadsheet with the service account as Editor.` | Same as above — this is the raw 403/401 before the message is enriched with the account address. | Same fix. If it persists, confirm the credential in the config directory is the key for the account you shared with. |
| `The spreadsheet or sheet was not found. Check the spreadsheet ID and sharing settings.` | 404: wrong spreadsheet id, or the sheet is unshared so Google will not admit it exists. | Re-copy the id from the URL. A sheet you can see but the service account cannot returns 404, not 403 — so a 404 is often still a sharing problem. |
| `Google Sheets quota exceeded. Try again shortly.` | 429 after 5 retry attempts. | Wait a minute and retry. Read/limit is ~300 reads and ~300 writes per minute per project, ~60 each per minute per user. Avoid tight scripted loops. |
| `Google Sheets is temporarily unavailable. Try again shortly.` | 500/502/503/504 after 5 retry attempts. | Google-side hiccup. Retry; check the [Google Workspace status dashboard](https://www.google.com/appsstatus/dashboard/). |
| `Could not reach Google Sheets: <detail>` | DNS, proxy or TLS failure. | Check connectivity to `sheets.googleapis.com` and any corporate proxy configuration. |
| `The Google Sheets request timed out. Try again shortly.` | The HTTP request was cancelled by the client timeout. | Retry. Large sheets with many rows take longer per command. |
| `Google Sheets rejected the request: <detail>` | 400 from the API, commonly a bad range or a malformed write. | Usually a malformed sheet layout: run `todo doctor --fix`, or repair the header row by hand. |
| `Google Sheets returned HTTP <code>: <detail>` | An unhandled status. | Report it with the detail — this is not an expected path. |

### Credential and configuration errors

| Message | Cause | Fix |
| --- | --- | --- |
| `No configuration found at <path>. Run 'todo init'.` | No `config.json` in the resolved directory. | `todo init`, or set `TODO_CONFIG_DIR` to the directory that holds your config. |
| `Credentials not found at <path>. Run 'todo init'.` | `config.json` exists but `credentials.json` does not. | Re-run `todo init`, or copy `credentials.json` from the machine where it exists. |
| `The credentials at '<path>' could not be read: <detail>. Run 'todo init' …` | The credential file exists but reading failed. | Check file permissions; on POSIX it should be `-rw-------`. |
| `The credentials at '<path>' are empty. …` | Zero-byte `credentials.json`. | Re-run `todo init --force`. |
| `The credentials at '<path>' are not valid JSON: <detail>` | Truncated or hand-mangled file. | Delete it and re-run `todo init --force`. |
| `The service-account key is missing 'client_email' or 'private_key'. …` | Wrong JSON file — an OAuth client secret, a gcloud config, or a key with the fields stripped. | Create a key under **IAM & Admin → Service Accounts → Keys**, JSON type. |
| `The service-account private key could not be read: <detail>` | `private_key` cannot be turned into an RSA credential: truncated PEM, escaped newlines lost by an editor, wrong key type. | Download a fresh key file and `todo init --force`. |
| `This is not a service account key (expected type 'service_account').` | You downloaded an OAuth **client** secret. | Use the service-account Keys tab, not **APIs & Services → Credentials**. |
| `The key has no 'client_email' — is this a service account key?` | Wrong file. | Same as above. |
| `The service account key is not valid JSON.` / `The service account key is empty.` | File is not JSON at all, or blank. | Re-download the key. |
| `Spreadsheet ID is missing. Run 'todo init' or 'todo config set spreadsheetId <id>'.` | `config.json` has an empty `spreadsheetId`. | `todo config set spreadsheetId <id>`. |
| `The configuration at <path> is not valid JSON: <detail>` | Hand-edited config with a syntax error. | Fix the JSON, or delete the file and re-run `todo init`. |
| `The configuration at <path> is empty.` | `config.json` deserialised to null. | Delete and re-run `todo init`. |
| `No spreadsheet is configured. Run 'todo init' to set the spreadsheet ID.` | Id blank at the connection layer. | `todo config set spreadsheetId <id>`. |
| `The configured default space '<name>' is not a valid tab name.` | `defaultSpace` in config violates the tab-name rules. | `todo config set defaultSpace <valid-name>`. |

### Google-reported auth errors

`invalid_grant`, `invalid_client` and similar strings come from Google's OAuth endpoint at
`oauth2.googleapis.com/token`, wrapped by the SDK. They appear inside the credential error messages
above rather than as their own line.

| Google error | Cause | Fix |
| --- | --- | --- |
| `invalid_grant` | The private key does not correspond to `client_email`, the key was deleted, the service account was deleted, or the project was deleted. Also: system clock more than a few minutes off, since the signed JWT has an `iat`/`exp` window. | Check the clock (`timedatectl`, `w32tm /resync`). Otherwise download a fresh key and `todo init --force`. If the service account or project is gone, create a new one and re-share the sheet. |
| `invalid_client` | `client_email` does not name an existing service account. | Verify the address in the console's service account list. |
| `unauthorized_client` | The service account is disabled or restricted by org policy. | Re-enable it in **IAM & Admin → Service Accounts**, or talk to your Workspace admin. |
| `API has not been used in project <n> before or it is disabled` | Google Sheets API not enabled for the project. | [Enable it](/guide/google-cloud-setup#step-2). This is the second most common failure. |

### Sheet schema errors

| Message | Cause | Fix |
| --- | --- | --- |
| `The sheet is missing required column '<name>'. Run 'todo doctor --fix'.` | A space tab's header row lacks `id`, `name`, `status` or `created_at`. | `todo doctor --fix`. |
| `The '<tab>' tab has no header row. Run 'todo doctor --fix'.` | The tab exists but row 1 is empty. | `todo doctor --fix`. |
| `Row <n> has an unrecognised status '<value>'. Accepted values: pending, ongoing, backlog, completed.` | A `status` cell holds something outside the accepted set. | Correct the cell. Canonical values and aliases both parse. |
| `Row <n> has an invalid task id '<value>'. Ids must be whole numbers.` | Non-numeric `id` cell. | Fix or delete the row. Ids are integers. |
| `Row <n> does not have a task id.` | Blank `id` cell in a non-blank row. | Fill the id or delete the row. |
| `Row <n> has an invalid due date '<value>'. Expected yyyy-MM-dd.` | Manual date entry in another format, or a cell Google reformatted. | Re-type as `2026-12-01`, or set the cell format to plain text. |
| `Row <n> has an invalid created_at value '<value>'. Expected an ISO-8601 timestamp.` | Hand-edited or locale-formatted timestamp. | Fix to `YYYY-MM-DDTHH:MM:SSZ`, or delete the row and re-add the task. |
| `Row <n> has an invalid completed_at value '<value>'. Expected an ISO-8601 timestamp.` | Same, for `completed_at`. | Same. |
| `Some columns are still out of place. Fix the header row by hand: existing data is never moved automatically.` | `--fix` could repair what it could but headers still do not validate. | Fix the header row manually. `--fix` will never reorder columns because that would re-associate data with the wrong field. |

Any of the `Row <n>` errors are **conflicts** and exit **1**.

### Task and space errors

| Message | Cause | Fix |
| --- | --- | --- |
| `Task <id> was not found in space '<space>'.` | No task with that id in that space. Ids are per space. | `todo ls --all -s <space>` to find the right id. |
| `Cannot move a task from <a> to <b>. From <a> you may move to: …` | The transition is not in the allowed graph. | Use an allowed target. See [allowed transitions](/guide/commands#allowed-transitions). |
| `Space '<name>' already exists.` | `todo space add` on an existing tab. | Pick another name, or select the existing one with `-s`. |
| `The default space cannot be removed.` | `todo space rm default`. | Protected by design. Change `defaultSpace` in config instead. |
| `Space name may not contain any of: / \ ? * [ ] :` | Invalid tab character. | Rename. |
| `Space name may not start with an underscore.` | `_` is reserved for internal tabs. | Rename. |
| `Space name may not start or end with an apostrophe.` | `'` breaks A1-notation quoting at the edges. | Rename. |
| `Space name must be at most 64 characters.` | Too long. | Shorten. |
| `'<value>' is not a valid state. Use one of: pending, ongoing, backlog, completed.` | Unknown state token. | Use a canonical name or a documented alias. |
| `'<value>' is not a valid date. Use yyyy-MM-dd, for example 2026-12-01.` | Localised or partial date. | Use exactly `yyyy-MM-dd`. |
| `'<value>' is not a valid sort field. Use default, due, id, status or created.` | Unknown `--sort`. | Use one of the five. |
| `Port <n> is out of range. Pick a port between 1 and 65535.` | `-p` outside the range. | Pick a valid port. |
| `Port <n> could not be opened: <detail> Try -p <other port>.` | Port already bound. | `todo server -p <other>`. |
| `Name is required.` | Empty task name. | Supply a name. |
| `Name must be at most 200 characters.` | Too long. | Shorten. |
| `Description must be at most 4000 characters.` / `Remarks must be at most 4000 characters.` | Too long. | Shorten. |
| `Due date must be in the year 1900 or later.` | A parsed date before 1900. | Use a realistic date. |
| `The task is not valid.` | Headline above one or more field errors. | Read the field lines underneath. |

### CLI usage errors

| Message | Cause | Fix |
| --- | --- | --- |
| `Nothing to change. Pass at least one of --name, --desc, --due, --clear-due or --remarks.` | `todo edit <id>` with no field flags. | Pass at least one. |
| `Use either --due or --clear-due, not both.` | Both flags on `todo edit`. | Pass one. |
| `'config set' needs a key and a value.` | `todo config set` with a missing argument. | `todo config set <key> <value>`. |
| `'<action>' is not a config action. Use path, show or set.` | Unknown `todo config` action. | Use one of the three. |
| `'<key>' is not a known setting. Use spreadsheetId, defaultSpace, serverPort or provider.` | Unknown `config set` key. | Use a documented key. |
| `'<input>' does not contain any task ids.` | `todo rm` with no integers. | Pass `1` or `1,2,3`. |
| `Only 'google-sheets' is supported in this version.` | `config set provider` to anything else. | Leave it alone. |
| `A configuration already exists at <path>.` + `Re-run with --force to replace it.` | `todo init` when already configured. | `todo init --force`. |
| `Unexpected error: <detail>` | An unhandled exception. Exit 1. | Report it with the message. This should not happen. |

## Quota {#quota}

Sheets v4 enforces, per **project**, roughly **300 read requests and 300 write requests per minute**, and
per **user** about **60 reads and 60 writes per minute**. A single CLI command issues a handful of calls
— `todo add` reads the tab, reads `_meta`, appends the row, then upserts the counter — so interactive
use never approaches the limit. Scripted bulk operations can.

When the quota is hit, `todo` does not fail on the first 429. `RetryPolicy` retries retryable statuses
(429, 500, 502, 503, 504) up to **5 attempts** with truncated exponential backoff: 1s, 2s, 4s, capped at
8s, each stretched by up to 25 % jitter. Only after the budget is exhausted do you see
`Google Sheets quota exceeded. Try again shortly.` (exit 1). That is why a quota failure can take
roughly 15 seconds to surface rather than being instant.

There is no batching flag to reduce call count. Pacing your own script is the only lever: the quota is
per project, not per space, so spreading work across spaces does not raise the ceiling.

## Exit codes {#exit-codes}

| Code | Name | Meaning | Typical triggers |
| --- | --- | --- | --- |
| `0` | Success | Done. | Any successful command; a cancelled confirmation prompt; a no-op state change; Ctrl+C. |
| `1` | Failure | Not found, conflict, unavailable, unexpected. | Missing task, schema conflict, Google 5xx/429 after retries, `todo init` without `--force`, `doctor` schema mismatch. |
| `2` | Usage | Validation or argument parsing. | Illegal transition, bad date, bad state, bad space name, bad port, unknown flag, unknown command. |
| `3` | Configuration | Config or credentials broken, or Google said permission denied. | Missing `config.json`, unreadable `credentials.json`, 403 from Google. |

Derived from `ExitCodeMapper`:

```text
None                    → 0
Validation              → 2
Configuration           → 3
Permission              → 3
NotFound                → 1
Conflict                → 1
Unavailable             → 1
Unexpected              → 1
```

Branch on these in scripts:

::: code-group

```bash [bash]
todo add "$1" || case $? in
  1) echo "backend problem (not found / conflict / unavailable)" >&2 ;;
  2) echo "bad arguments" >&2 ;;
  3) echo "config or permission problem" >&2 ;;
esac
```

```powershell [Windows (PowerShell)]
todo.exe add $task
switch ($LASTEXITCODE) {
  0 { }
  1 { Write-Error 'backend problem (not found / conflict / unavailable)' }
  2 { Write-Error 'bad arguments' }
  3 { Write-Error 'config or permission problem' }
}
```

:::

## Diagnosing by hand

When the messages are not enough, test the pieces separately.

### Is the credential parseable?

```bash
python3 -c 'import json; k=json.load(open("'"$HOME"'/.config/todo/credentials.json")); print(k["type"], k["client_email"])'
```

### Does the spreadsheet exist and is it shared?

```bash
curl -s -H "Authorization: Bearer $(gcloud auth print-access-token --impersonate-service-account=SA_EMAIL 2>/dev/null)" \
  "https://sheets.googleapis.com/v4/spreadsheets/$SPREADSHEET_ID?fields=properties.title,sheets.properties.title"
```

A `403` here means sharing. A `404` means the id. To confirm sharing independently of tokens, open the
sheet's **Share** dialog and read the list.

### Is the header row right?

Open the tab in the browser and confirm row 1, in any order you like:

```text
id | name | description | status | due_on | created_at | completed_at | remarks
```

Order does not matter — columns are resolved by header name. Only `id`, `name`, `status` and
`created_at` are required to exist somewhere in row 1. Trailing whitespace in a header name is
trimmed, and matching is case-insensitive.

### Is `_meta` intact?

The `_meta` tab is a two-column key/value store.

```text
key                 value
schema_version      1
next_id:default     8
next_id:work        3
```

A missing `_meta` tab is recreated by `todo doctor --fix`. A missing `next_id:<space>` entry is not
fatal: the next add computes `max(counter, highest existing + 1)` and rewrites the counter.

## Known non-issues

These look like problems and are not.

| Symptom | Why it is fine |
| --- | --- |
| Binary is ~52 MB | Self-contained .NET plus the Google SDK. Deliberate — see [why no trimming](/reference/support-matrix#why-the-binary-is-large). |
| `todo ls` hides finished tasks | Defaults to `ongoing` + `pending`. Use `--all` or `--status`. |
| A due date shows as a number in the sheet | Cosmetic. `todo` reads unformatted values, so the underlying value is what counts. |
| Column order in the sheet differs from the docs | Safe. Columns are resolved by header name. |
| The dashboard does not refresh on its own | No polling by design. Reload the page. |
| `todo server` is not reachable from another machine | Binds `127.0.0.1` only, on purpose. |
| `curl` works but a browser `fetch` from another site gets 403 | The `X-Todo-Client` check is doing its job. |
| A `next_id` value looks higher than your task count | Correct behaviour. A failed counter write can create gaps, and ids are never reused. |
| `todo init` printed the share reminder *after* succeeding | By design. See the note below. |

::: tip The `todo init` share prompt is a reminder, not a failure
`todo init` finishes with:

```text
Next: share the spreadsheet with this address as Editor:
  todo-cli@todo-cli-482913.iam.gserviceaccount.com
```

That message is printed on success too, because the CLI cannot tell whether you performed step 5
before or after running `init`. If `init` actually completed and printed that, the sheet was already
readable — re-sharing is harmless.
:::

## Getting a useful bug report together

Include:

1. The exact command and its full output (both stdout and stderr).
2. `todo --version` — currently `0.1.0`.
3. `todo doctor` output. It contains no secrets: paths, spreadsheet title and id, tab names, schema
   version and the service-account **email** — never the private key or any task content.
4. Which release artifact you run, or the SDK version if built from source.

Never paste `credentials.json` or the `private_key` value.
