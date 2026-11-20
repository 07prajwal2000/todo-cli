# Google Cloud setup

`todo` authenticates with a **service account** — a robot Google identity that belongs to a Google
Cloud project rather than to a person. There is no OAuth consent screen, no browser login, no refresh
token, and nothing that expires after seven days.

Getting this right is six steps. Almost every failed setup is one of two mistakes: the **Google
Sheets API was never enabled**, or the **spreadsheet was never shared with the service account**.

```text
1. Create a Google Cloud project
2. Enable the Google Sheets API
3. Create a service account
4. Create a JSON key and download it
5. Share the spreadsheet with the service account's client_email as Editor
6. Run todo init, then todo doctor
```

::: danger Read this before you start
The JSON key you download in step 4 is a long-lived credential. Anyone who has that file can read
and write every spreadsheet shared with that service account, until the key is revoked. Do not
commit it, do not paste it into a chat, and do not put it in a synced folder you do not control.
`todo init` copies it into your user config directory with owner-only permissions; that copy is the
one the CLI uses.
:::

Everything below assumes you are signed into the Google account that owns (or can edit) the
spreadsheet.

## Step 1 — Create a Google Cloud project {#step-1}

Open the project selector:

```text
https://console.cloud.google.com/projectcreate
```

1. In the **Project name** field, enter something recognisable, for example `todo-cli`.
2. The **Project ID** is auto-generated from the name (for example `todo-cli-482913`). Google appends
   digits because project ids are globally unique. Leave it as generated, or edit it; either is fine.
3. **Location** can stay at *No organization* for a personal account.
4. Click **Create**.
5. Wait for the notification, then confirm the project selector at the top of the console shows your
   new project. Every step below applies to the currently selected project.

::: tip One project is enough
You do not need a project per spreadsheet. A single project with a single service account can be
shared on any number of sheets. The API quota is counted per project, which is worth remembering if
you later automate heavily — see [Quota](/guide/troubleshooting#quota).
:::

## Step 2 — Enable the Google Sheets API {#step-2}

Creating a service account does **not** enable any API. This step is separate and mandatory.

Direct link to the library filtered to the Sheets API:

```text
https://console.cloud.google.com/apis/library/sheets.googleapis.com
```

1. Make sure the project selector still shows the project from step 1.
2. The page title should read **Google Sheets API**. If you land on the general API library instead,
   type `Google Sheets API` into the **Search for APIs & Services** box and pick the entry whose
   publisher is `googleapis.com`.
3. Click the blue **Enable** button.
4. Wait for the page to switch to the API dashboard (it will show **Manage**, **Create credentials**
   and usage graphs). That means it is enabled.

Equivalent navigation if you prefer clicking through: **APIs & Services** → **Library** → search
`Google Sheets API` → **Enable**.

::: warning "Already enabled"
If the button reads **Manage** instead of **Enable**, the API is already on for this project. Nothing
to do.
:::

You do **not** need to enable the Google Drive API. `todo` talks only to the Sheets v4 API and
addresses the spreadsheet by id, never by searching Drive.

## Step 3 — Create a service account {#step-3}

```text
https://console.cloud.google.com/iam-admin/serviceaccounts/create
```

1. Confirm the project selector shows your project.
2. **Service account details:**
   - **Service account name**: `todo-cli` (any label you like; this is what you will see in the
     console).
   - **Service account ID**: auto-filled from the name, for example `todo-cli`. This becomes the
     left-hand side of the email address.
   - **Service account description**: optional.
   - Click **Create and continue**.
3. **Grant this service account access to project (optional):** leave this empty. Click **Continue**.

   ::: tip Why no project role
   Project IAM roles decide what the account can do to *Google Cloud resources* — buckets, VMs,
   Pub/Sub. They play no part in access to a spreadsheet. A spreadsheet's permissions come only from
   its own **Share** dialog, which is step 5. Granting a project role here would be dead weight and,
     if you picked a broad role such as Editor, an unnecessary escalation.
   :::
4. **Grant users access to this service account (optional):** leave this empty. Click **Done**.
5. You are returned to the service accounts list. Copy the **email** of the new account — it looks
   like:

   ```text
   todo-cli@todo-cli-482913.iam.gserviceaccount.com
   ```

   You will need this address in step 5, and `todo init` prints it back to you as a reminder.

Equivalent navigation: **IAM & Admin** → **Service Accounts** → **+ Create service account**.

## Step 4 — Create and download a JSON key {#step-4}

A service account has no password. It proves its identity by signing a JWT with an RSA private key;
the JSON key file is that private key plus the metadata needed to use it.

1. Go to **IAM & Admin** → **Service Accounts** and click the service account you just created.
2. Open the **Keys** tab.
3. Click **Add key** → **Create new key**.
4. Select **JSON**. (Do not pick P12 — `todo` only parses the JSON form.)
5. Click **Create**.

The browser downloads a file named like `todo-cli-482913-1a2b3c4d5e6f.json`. Move it somewhere
deliberate, for example `~/keys/todo-sa.json`, and give it a memorable name. Do not leave it in
`~/Downloads`, and never add it to a git repository.

Inspect it without printing the private key:

::: code-group

```bash [Linux / macOS]
python3 -c 'import json,sys; k=json.load(open(sys.argv[1])); print(k["type"]); print(k["client_email"]); print(k["project_id"]); print("has private_key:", bool(k.get("private_key")))' ~/keys/todo-sa.json
```

```powershell [Windows (PowerShell)]
$key = Get-Content -Raw "$HOME\keys\todo-sa.json" | ConvertFrom-Json
$key.type
$key.client_email
$key.project_id
"has private_key: $([bool]$key.private_key)"
```

:::

Expected output:

```text
service_account
todo-cli@todo-cli-482913.iam.gserviceaccount.com
todo-cli-482913
has private_key: True
```

The file also contains `private_key_id`, `private_key`, `client_id` and `token_uri`. `todo` reads
only `client_email` and `private_key`; `type`, when present, must be exactly `service_account`.

::: danger Never commit the key
Add the file to your global gitignore, not just one project's:

```bash
echo 'todo-sa*.json' >> ~/.config/git/ignore
```

If the key is ever exposed, revoke it: **IAM & Admin** → **Service Accounts** → your account →
**Keys** → delete the key. Then create a new one and re-run `todo init --force`. Revocation is
immediate — any running process holding the old key starts failing on its next token request.
:::

## Step 5 — Share the spreadsheet with the service account {#step-5}

This is the step that is most often missed, and it is the difference between a working setup and a
403.

Create a spreadsheet if you do not have one: <https://sheets.new>. Then:

1. Copy the service account email from step 3 (or from the JSON key's `client_email`).
2. In the spreadsheet, click **Share** in the top right.
3. Paste the service account email into the **Add people and groups** field.
4. Set the role to **Editor**. Viewer is not enough — `todo init` creates tabs and header rows, and
   every write command needs write access.
5. Leave **Notify people** unchecked. A service account has no inbox, so notifying it is pointless and
   Google may warn about it.
6. Click **Share** (or **Send**).

::: danger Why this is mandatory
A service account is not a human Google account with a Drive UI. It has no "My Drive", no file
picker, and no way to browse to a document. The only files it can see are the ones explicitly shared
with its email address. There is no "it's in the same project" shortcut — a Google Cloud project and
a spreadsheet are unrelated objects, and no project-level permission grants sheet access.

If you skip this, everything looks configured and the first real call fails:

```text
✗ Google Sheets denied access to spreadsheet '1AbCdEf…'. Share the spreadsheet with todo-cli@todo-cli-482913.iam.gserviceaccount.com as Editor.
```
:::

You need write access as **Editor** specifically. **Commenter** and **Viewer** both produce 403 on
write operations, and **Viewer** also breaks `todo init` because the probe creates the `_meta` tab.

### Getting the spreadsheet id

The id is the long string between `/d/` and `/edit` in the URL:

```text
https://docs.google.com/spreadsheets/d/1AbCdEfGhIjKlMnOpQrStUvWxYz0123456789/edit#gid=0
                                     └────────────── spreadsheet id ──────────────┘
```

`todo init` and `todo config set spreadsheetId` both accept the bare id or the whole URL and extract
the id themselves. An id must be at least 20 characters and alphanumeric; anything else is rejected
with `That does not look like a spreadsheet ID.`

## Step 6 — Run `todo init` and verify {#step-6}

```bash
todo init
```

You are prompted three times:

| Prompt | What to enter |
| --- | --- |
| `Path to your Google service account key (JSON)` | Full path to the file from step 4. It is validated immediately: the file must exist, be JSON, and contain `client_email` and `private_key`. |
| `Spreadsheet ID or URL` | The id or the full URL from step 5. |
| `Default space (sheet tab)` | Press Enter to accept `default`, or name your first space. |

Then verify:

```bash
todo doctor
```

A healthy run looks like this:

```text
settings          C:\Users\you\AppData\Roaming\todo\config.json
credentials       C:\Users\you\AppData\Roaming\todo\credentials.json
spreadsheet       My Tasks
spreadsheet id    1AbCdEfGhIjKlMnOpQrStUvWxYz0123456789
default space     default
service account   todo-cli@todo-cli-482913.iam.gserviceaccount.com
tabs              _meta, default
schema version    1
schema            ready (v1)
✓ Everything looks healthy.
```

If the spreadsheet field shows the wrong title, the id is wrong. If `service account` shows
`<unknown>`, the credential copy in the config directory cannot be parsed — re-run `todo init
--force`.

Finally:

```bash
todo add "first task"
todo ls
```

## What `todo init` actually stored

Two files in your config directory, and nothing else:

| File | Contents | Permissions |
| --- | --- | --- |
| `config.json` | `provider`, `spreadsheetId`, `defaultSpace`, `credentialsFile`, `serverPort` | Owner-only on POSIX |
| `credentials.json` | An exact **copy** of the JSON key you pointed at | Owner-only on POSIX |

::: tip Your downloaded key is disposable afterwards
`todo` reads `credentials.json`, not the file you downloaded. Once `todo init` succeeds you can delete
the original download and the CLI keeps working. On a second machine you simply copy `credentials.json`
across and re-run `todo init`, or copy the whole config directory.
:::

Find the location any time with:

```bash
todo config path
```

```text
C:\Users\you\AppData\Roaming\todo\config.json
config dir: C:\Users\you\AppData\Roaming\todo
```

See [Configuration](/guide/configuration) for the per-OS table and the `TODO_CONFIG_DIR` override.

## Troubleshooting the setup

| Symptom | Cause | Fix |
| --- | --- | --- |
| `Google Sheets denied access to spreadsheet '…'. Share the spreadsheet with … as Editor.` (403) | Sheet not shared with the service account, shared with the wrong address, or shared with insufficient role | Open the sheet's **Share** dialog and confirm the exact `client_email` from the JSON key is listed as **Editor** |
| `The spreadsheet or sheet was not found. Check the spreadsheet ID and sharing settings.` (404) | Wrong spreadsheet id, or the id belongs to a sheet the service account cannot see | Re-copy the id from the URL; unshared sheets surface as 404, not 403 |
| `Google Sheets quota exceeded. Try again shortly.` (429) | Read or write quota exhausted | Wait a minute; the CLI already retried with backoff — see the note below |
| `The service-account private key could not be read: …` | Key file truncated, or `private_key` mangled by an editor | Re-download a fresh JSON key and re-run `todo init --force` |
| Google returns `invalid_grant` | The private key does not match `client_email`, or the key was revoked | Download a new key, then re-run `todo init --force` to overwrite the copy in the config dir |
| `Credentials not found at …/credentials.json. Run 'todo init'.` | Config directory is empty on this machine, or was moved | Re-run `todo init`, or copy `config.json` and `credentials.json` from the old machine |
| `This is not a service account key (expected type 'service_account').` | You downloaded an OAuth client secret, not a service-account key | Create a key under **IAM & Admin → Service Accounts → Keys**, not **APIs & Services → Credentials** |
| `The key has no 'client_email' — is this a service account key?` | Wrong JSON file entirely | Same as above |
| 403 mentioning `API has not been used in project … or it is disabled` | Sheets API not enabled | [Step 2](#step-2) |

### About the 429 quota {#quota-429}

Sheets v4 enforces, per project, roughly **300 read requests and 300 write requests per minute**,
with a per-user limit of about **60 read and 60 write requests per minute**. A single CLI command
issues a small handful of calls — `todo add` reads the tab, reads `_meta`, appends the row, then
writes the counter — so ordinary interactive use never approaches the limit.

When a 429 does happen, `todo` does not fail immediately. Retryable statuses (429, 500, 502, 503, 504)
are retried up to **5 attempts** with truncated exponential backoff: 1s, 2s, 4s, 8s capped, plus up to
25 % jitter. Only after those attempts are exhausted do you see the `quota exceeded` message.

### The 7-day expiry rumour does not apply

If you have configured Google OAuth before, you may have met this warning: *"Testing" OAuth consent
screens issue refresh tokens that expire after 7 days*. That rule applies to **OAuth refresh tokens**
for apps in testing mode. Service-account keys are not refresh tokens. A service-account key does not
expire on a timer; it stays valid until you revoke it.

What does end its life:

- **Manual revocation.** Deleting the key in the console (`IAM & Admin` → `Service Accounts` →
  `Keys` → delete) invalidates it immediately. Every `todo` command starts failing with a credential
  error.
- **Deleting the service account.** All of its keys die with it, and the sheet sharing entry turns
  into an orphaned address.
- **Deleting the Google Cloud project.** Same effect, plus you lose the API enablement.
- **Organisation policy.** A Workspace org can enforce key-expiry or key-creation policies on service
  accounts. If your project lives in an org, that policy is the only thing that expires keys on a
  schedule.

### Rotating a key without downtime

Rotation is a two-key dance, and no CLI reconfiguration is needed beyond re-running `todo init`:

1. In the console, open the service account's **Keys** tab and **Add key → Create new key → JSON**.
   Now two keys exist and both work.
2. Run `todo init --force` and point it at the new file. It overwrites `credentials.json`.
3. Confirm with `todo doctor` and `todo ls`.
4. Return to the console and delete the **old** key.

Because the service account's `client_email` never changes, the spreadsheet sharing entry survives
rotation untouched.

### When you do not need to re-share

Sharing is on the spreadsheet and keyed to the service account's email, not to a particular key. So:

- Rotating a key: no re-share.
- Moving to a new machine: no re-share, just copy the config directory.
- New spreadsheet: **yes**, share that sheet too.

## Next steps

- All commands and flags: [Commands](/guide/commands)
- Multi-space setup and the tab model: [Spaces](/guide/spaces)
- Every error message with its cause and fix: [Troubleshooting](/guide/troubleshooting)
