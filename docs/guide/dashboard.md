# Dashboard

`todo server` starts a small local web board on top of the same spreadsheet. It is not a separate
application: it is a second presentation layer over the same `TaskService` and `SpaceService` the CLI
uses, so a card you move in the browser and a `todo status` in a terminal go through identical
validation and identical transitions.

```bash
todo server
```

```text
Dashboard http://127.0.0.1:12000 (Ctrl+C to stop)
Space: default
```

Your browser opens automatically. Ctrl+C stops it.

## Server options

| Option | Default | Description |
| --- | --- | --- |
| `-p`, `--port <PORT>` | `serverPort` from config, which is `12000` | Port to listen on. |
| `--no-open` | off | Do not launch a browser. |
| `-s`, `--space <SPACE>` | resolved space | Space the dashboard opens on. |
| `--no-color`, `-v`, `--verbose`, `--json` | — | Inherited from the shared option set; `--json` has no effect on this command. |

```bash
todo server                          # port 12000, opens a browser
todo server -p 8080                  # different port
todo server --no-open                # headless / remote shell
todo server -s work                  # open on the 'work' space
```

Port validation and failure behaviour:

- A port outside 1–65535 is rejected before the server starts: exit **2**.
- A port already in use surfaces as `Port <n> could not be opened: … Try -p <other port>.` with exit
  **1**.
- Ctrl+C prints `Dashboard stopped.` and exits **0**.

The effective port comes from the config unless `-p` is given, so you can persist a preferred port:

```bash
todo config set serverPort 12100
```

## Binding: loopback only

The server binds **`127.0.0.1` only** — `UseUrls("http://127.0.0.1:<port>")`, never `0.0.0.0` or `+`.
It is unreachable from another machine on your network, and unreachable from a container's host
unless you explicitly proxy to it.

This is a deliberate design decision, and the reason for the request guard below: the dashboard has
**no authentication**. Anyone who can reach the port can read and mutate your sheet through it.

::: danger Consequences of having no authentication
If you forward the port (`ssh -L 12000:127.0.0.1:12000`, a devcontainer port mapping, a reverse
proxy), you have published an unauthenticated write API to your task sheet to whoever can reach that
forward. `todo` does not offer auth, TLS or bind-address options. Treat "reachable" as "trusted".
:::

## Local-only security model

A loopback server is still reachable from any web page the user happens to have open — a page can
`fetch('http://127.0.0.1:12000/api/tasks')` from the browser. To keep that from being a write channel,
every mutating request must clear three checks in `LocalRequestGuard`:

| Check | Rule | Rejection |
| --- | --- | --- |
| Method | Only `POST`, `PUT`, `PATCH`, `DELETE` are gated. `GET` is ungated. | — |
| Origin | If an `Origin` header is present, it must be exactly `http://127.0.0.1:<bound port>` or `http://localhost:<bound port>`. Scheme, host and port all compared. | `403` `Cross-origin requests are not allowed.` |
| Client header | `X-Todo-Client: dashboard` is required. | `403` `The X-Todo-Client: dashboard header is required.` |
| Content type | When a body is present, `Content-Type` must be `application/json`. | `403` `Content-Type must be application/json.` |

Notes on how this actually behaves:

- A **missing** `Origin` header is allowed, so `curl` works. The custom header is what stops a casual
  cross-site `fetch` — browsers will not send a custom header cross-origin without a CORS preflight,
  and the server sends no CORS headers, so the preflight fails.
- A **foreign** `Origin` is rejected outright, so even a form-style request from another site cannot
  get through.
- The comparison uses the port Kestrel actually bound (`context.Connection.LocalPort`), not the
  configured one, so it stays correct on an ephemeral port.
- The guard runs as middleware, before routing, for **every** request.

To call the API from a script you must therefore send both headers:

```bash
curl -s http://127.0.0.1:12000/api/spaces

curl -s -X POST http://127.0.0.1:12000/api/tasks \
  -H 'Content-Type: application/json' \
  -H 'X-Todo-Client: dashboard' \
  -d '{"name":"from curl","status":"pending"}'
```

```powershell
Invoke-RestMethod http://127.0.0.1:12000/api/spaces

Invoke-RestMethod -Method Post -Uri http://127.0.0.1:12000/api/tasks `
  -ContentType 'application/json' `
  -Headers @{ 'X-Todo-Client' = 'dashboard' } `
  -Body '{"name":"from curl","status":"pending"}'
```

## The UI

A single page, no framework, no build step — the HTML, CSS and JavaScript are embedded into the
assembly (`EmbeddedResource Include="wwwroot\**\*"`) and served from the manifest, so the binary
stays a single file.

### Layout

- **Header toolbar** — space selector with add/delete buttons, a search box, status filter chips with
  live counts, and a **New task** button.
- **Status line** — `Showing N of M tasks`, plus the active search term.
- **Task grid** — one card per task.
- **Footer** — `Loopback only · / search · n new task · Esc close`.

### Task cards

Each card shows the id badge, a coloured state badge, the name, the description (if any), a due-date
line, collapsed remarks (if any), and action buttons.

The due-date line is computed client-side from `due_on` and today's local date:

| Condition | Rendered |
| --- | --- |
| Task is `completed` with a due date | `Done · was due 1 Dec 2026` |
| Due today | `Due today` |
| Due tomorrow | `Due tomorrow` |
| Due within 3 days | `Due in 2d` |
| Due further out | `Due in 14d` |
| Past due | `5d overdue` |

Cards whose due date has passed get an overdue accent; cards due within three days get a "soon"
accent. Completed cards are visually de-emphasised.

### Actions

The action buttons offered depend on the current state and are, by construction, only the allowed
transitions:

| Current state | Buttons |
| --- | --- |
| `backlog` | **Start** (→ ongoing), **Queue** (→ pending), Edit, Delete |
| `pending` | **Start** (→ ongoing), **Complete** (→ completed), Edit, Delete |
| `ongoing` | **Complete** (→ completed), **Pause** (→ pending), Edit, Delete |
| `completed` | **Reopen** (→ pending), Edit, Delete |

State changes are **optimistic**: the card updates immediately, the `PATCH` follows, and on failure
the previous state is restored with an error toast.

In the edit dialog the status dropdown disables every option that is not a legal target from the
task's current state.

::: note The UI cannot reach every legal transition
The domain allows `ongoing → backlog` and `completed → ongoing`, but no button or dropdown option
produces them. Use the CLI for those:

```bash
todo status 4 backlog
todo status 4 ongoing
```
:::

## Keyboard shortcuts

| Key | Action |
| --- | --- |
| `/` | Focus the search box and select its contents |
| `n` | Open the new-task dialog |
| `Esc` | Close the open dialog (browser-native `<dialog>` behaviour) |

Shortcuts are suppressed while focus is in an `input`, `textarea`, `select` or a contenteditable
element, when any modifier key (`Ctrl`, `Cmd`, `Alt`) is held, and while a modal dialog is open.

Search is debounced by 250 ms and matches **name, description and remarks**, case-insensitively.

## HTTP API

All responses are `application/json` with camelCase properties. The JSON shapes are source-generated
(`DashboardJsonContext`), so they are fixed at compile time.

### Routes

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/api/health` | Server identity and configured target |
| `GET` | `/api/spaces` | List spaces |
| `POST` | `/api/spaces` | Create a space → `201` |
| `DELETE` | `/api/spaces/{name}` | Delete a space and its tasks → `204` |
| `GET` | `/api/tasks` | List tasks, honouring `space`, `status`, `q` |
| `GET` | `/api/tasks/{id}` | One task |
| `POST` | `/api/tasks` | Create a task → `201` |
| `PATCH` | `/api/tasks/{id}` | Partial update, including a state change |
| `DELETE` | `/api/tasks/{id}` | Delete a task → `204` |
| `GET` | `/{path}` | Embedded static assets; unknown paths are `404` |

Unknown non-root asset paths return `404` rather than `index.html`, so a stale asset reference fails
loudly instead of silently receiving HTML.

### `GET /api/health`

```json
{
  "status": "ok",
  "provider": "google-sheets",
  "defaultSpace": "default",
  "spreadsheetId": "1AbCdEfGhIjKlMnOpQrStUvWxYz0123456789",
  "serverTime": "2026-11-20T09:41:07.1234567Z"
}
```

`/api/health` does **not** call Google. It reports what the process was configured with, so it stays
fast and works even when the sheet is unreachable.

### `GET /api/tasks`

| Query parameter | Meaning |
| --- | --- |
| `space` | Space to read. Omitted → the resolved default space. |
| `status` | Comma-separated states. The literal `all` means no state filter. Aliases accepted. |
| `q` | Free-text search over name, description and remarks. |

Defaults are the **same** as the CLI. With no `status` parameter at all, the filter is
`{pending, ongoing}` — backlog and completed tasks are hidden. The literal `all` turns the state filter
off entirely.

That is why the UI always sends `status=all`: it wants every task in the space regardless of state.

| `status` value | Effective filter |
| --- | --- |
| absent | `pending`, `ongoing` |
| `all` | none |
| `backlog` | `backlog` |
| `pending,done` | `pending`, `completed` |
| `all,completed` | `completed` — explicit states win, `all` is redundant |

`all` only has an effect when no explicit state is also present, because `TaskService.ApplyFilter`
checks `States.Count > 0` before it looks at `IncludeAllStates`.

```bash
curl -s 'http://127.0.0.1:12000/api/tasks?space=work&status=all&q=bank'
```

```json
{
  "tasks": [
    {
      "id": 7,
      "name": "call the bank",
      "description": "ask about the wire transfer",
      "status": "pending",
      "dueOn": "2026-11-28",
      "createdAt": "2026-11-20T09:41:07.1234567",
      "remarks": "ref 88213"
    }
  ],
  "space": "work"
}
```

::: warning The dashboard's task shape is not the CLI's
Both surfaces expose the same field names, but they are separate contracts and they differ:

| | CLI (`--json`) | Dashboard (`/api/...`) |
| --- | --- | --- |
| Serializer context | `CliJsonContext` | `DashboardJsonContext` |
| Timestamp type | `DateTimeOffset` (`2026-11-20T09:41:07.1234567+00:00`) | `DateTime` (`2026-11-20T09:41:07.1234567`) |
| Null fields | Omitted (`JsonIgnoreCondition.WhenWritingNull`) | Emitted as `null` (`JsonIgnoreCondition.Never`) |

So a script that compares or parses timestamps across the two must normalise both the offset and the
null handling. They are deliberately independent so a change in one cannot silently break the other.
:::

### `PATCH /api/tasks/{id}`

Every field is optional. `null`/absent means "leave as is". Clearing the due date is expressed with
`clearDue: true`, not a null date.

```json
{
  "name": "call the bank",
  "description": "ask about the wire",
  "remarks": "ref 88213",
  "dueOn": "2026-12-02",
  "clearDue": false,
  "status": "ongoing"
}
```

A single PATCH can carry both content edits and a state change. The edit is applied first, then the
state change, so the response is the final state of the task. A `status` equal to the current state
is skipped rather than treated as an illegal transition.

### `GET /api/tasks/{id}` and the `space` parameter

`GET`, `PATCH` and `DELETE` on `/api/tasks/{id}` all take `space` from the **query string**. Without
it they act on the resolved default space, not on whatever the browser has selected.

### Error responses

Every failure is `{ "code", "message" }`, plus `fields` when field-level validation failed:

```json
{
  "code": "validation",
  "message": "Unknown status 'sprinting'. Accepted values: pending, ongoing, backlog, completed.",
  "fields": {
    "status": ["Accepted values: pending, ongoing, backlog, completed."]
  }
}
```

`code` is the lowercased `ErrorCode` name: `validation`, `notfound`, `conflict`, `configuration`,
`permission`, `unavailable`, `unexpected`.

HTTP status mapping, from `ResultMapping.StatusCodeFor`:

| Application error | HTTP status |
| --- | --- |
| `None` | `200` |
| `Validation` | `400` |
| `NotFound` | `404` |
| `Conflict` | `409` |
| `Configuration` | `503` |
| `Permission` | `403` |
| `Unavailable` | `502` |
| `Unexpected` | `500` |

The same mapping governs the guard's own rejections, which are `Permission` → `403`.

Practical consequences worth knowing:

- A missing config or credential is **`503`**, not `500` — the dashboard is running but cannot reach
  its backend.
- A Google-side outage or a 429 that outlived the retry budget is **`502`**.
- A sheet that is not shared with the service account is **`403`** and carries the same "share the
  spreadsheet with …" message the CLI prints.
- The dashboard never returns a redirect or an HTML error page. Every failure is JSON.

### Request body rules

- An empty body on a `POST`/`PATCH` is `400` with `{"code":"validation","message":"A JSON request body is required.","fields":{"body":["Required."]}}`.
- Malformed JSON is `400` with the parse message.
- A `Content-Type` other than `application/json` on a request with a body is rejected by the guard
  with `403` before the handler sees it.

## Troubleshooting the dashboard

| Symptom | Cause | Fix |
| --- | --- | --- |
| `Port 12000 could not be opened: …` (exit 1) | Something else holds the port | `todo server -p 12100` |
| Browser shows a blank page | JS disabled, or an asset request failed | Check the browser console; assets must load from the same origin |
| `Cannot reach the dashboard server. Is it still running?` | The server stopped, or the tab outlived it | Restart `todo server` |
| `403` on every action but loading works | The client header is missing — you are calling the API directly | Send `X-Todo-Client: dashboard` |
| `503` immediately | `config.json` or `credentials.json` unreadable | `todo doctor` |
| `403` from Google, shown as a permission error | Sheet not shared with the service account | [Google Cloud setup](/guide/google-cloud-setup#step-5) |
| `404` on `/api/tasks/99` | No such task **in that space** | Pass `?space=` for the right space |

## What the dashboard does not do

- No authentication, no TLS, no remote bind.
- No live refresh — no polling, no websocket, no SSE. Reload the page to see changes another process
  made. (Your own mutations update the UI immediately.)
- No bulk operations. One task at a time.
- No drag-and-drop between states. Use the buttons.
