# Spaces

A **space** is one worksheet tab inside the single spreadsheet you configured. It is the only
organisational axis `todo` has: every task belongs to exactly one space, and task ids are scoped to a
space.

```text
One spreadsheet
├── _meta        internal bookkeeping (schema version, id counters) — not a space
├── default      a space
├── home         a space
└── work         a space
```

There is no project hierarchy, no tags and no cross-space query. If you want a task in two places,
it is two tasks.

## The space model

| Property | Behaviour |
| --- | --- |
| Storage | One worksheet tab per space, in the one spreadsheet from `config.json`. |
| Fallback | `default` is the fallback space. It is created by `todo init` and cannot be deleted. |
| Id scope | Ids are per-space. Two spaces can each have a task `#1`. |
| Auto-create | `todo add -s <space>` creates the tab if it does not exist. |
| Reserved | Tabs whose name starts with `_` are internal and never listed as spaces. `_meta` (bookkeeping) and `_notes` (markdown notes). |
| Ordering | `todo spaces` lists them case-insensitively alphabetical. |
| Deletion | `todo space rm` deletes the worksheet tab and every task in it. |

Tabs that Google creates or that you create by hand are picked up as spaces on the next command, as
long as the name is a valid space name and the tab has (or can be given) a header row. A tab with the
schema headers is fully usable immediately; a tab with no header row is repaired by `todo doctor --fix`.

::: tip A space is not a security boundary
Every space lives in the same spreadsheet and is readable by everyone that spreadsheet is shared
with. Use separate spreadsheets — not separate spaces — if you need separate access control.
:::

## Choosing the active space

Resolution order, implemented once in `SpaceResolver` so every command agrees:

1. `-s` / `--space <SPACE>` on the command.
2. The `TODO_SPACE` environment variable.
3. The `defaultSpace` value in `config.json`.

```bash
todo ls                        # uses TODO_SPACE, else the configured default
todo ls -s work                # explicit
TODO_SPACE=home todo ls        # environment
```

```powershell
todo ls
todo ls -s work
$env:TODO_SPACE = 'home'; todo ls
```

Blank values are skipped, so `TODO_SPACE=""` falls through to the configured default rather than
erroring. An invalid name in the flag or the environment is a validation error (exit 2); an invalid
`defaultSpace` in `config.json` is a configuration error (exit 3).

## Naming rules

A space name becomes a worksheet tab title, so it must satisfy both Google's tab naming rules and the
reserved-name rule for internal tabs. `SpaceName.IsValid` enforces all of it:

| Rule | Detail |
| --- | --- |
| Required | Non-empty after trimming. |
| Max length | 64 characters. |
| Forbidden characters | `/` `\` `?` `*` `[` `]` `:` |
| No leading underscore | A leading `_` is reserved for internal tabs such as `_meta`. |
| No leading or trailing apostrophe | `'` at either end is rejected. |
| Case-insensitive uniqueness | `Work` and `work` are the same tab. Google enforces this; `todo` relies on it. |

Surrounding whitespace is trimmed before validation. Failure messages are specific:

```text
✗ Space name may not contain any of: / \ ? * [ ] :
✗ Space name may not start with an underscore.
✗ Space name may not start or end with an apostrophe.
✗ Space name must be at most 64 characters.
✗ Space name is required.
```

Escaping is handled for you. `SheetLayout.QuoteTitle` always single-quotes tab titles — `'my
space'!A1:H1` — and doubles embedded apostrophes, so names with spaces work without extra thought.

## Managing spaces

```bash
todo spaces                 # list
todo space add work         # create
todo space rm work          # delete (prompts)
todo space rm work -y       # delete without prompting
```

`todo space add` writes the tab and immediately fills its header row, so the space is usable the
moment the command returns. Creating an existing space is a conflict:

```text
✗ Space 'work' already exists.
```

::: danger Deleting a space deletes its tasks
`todo space rm` removes the worksheet tab. Every row in it goes with it. `todo` has no undo; recovery
depends on the spreadsheet's own **File → Version history**.

The default space is protected:

```text
✗ The default space cannot be removed.
```

To make a different space the default, change the config instead:

```bash
todo config set defaultSpace work
```
:::

## Sheet layout per space

Each space tab holds a header row plus one row per task. The columns are fixed by `TaskSchema` and
resolved by header name:

| A | B | C | D | E | F | G | H |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `id` | `name` | `description` | `status` | `due_on` | `created_at` | `completed_at` | `remarks` |

Required columns are `id`, `name`, `status` and `created_at`. The other four may be absent — a tab
with only the four required headers is valid and the missing fields simply read as empty.

::: warning Keep the canonical column order
Reads resolve columns by header name, so a reordered tab reads correctly. Writes do not: they always
start at column A and emit cells in canonical order, so editing or adding a task on a reordered tab
scrambles the row. Column order is part of the contract. See
[Sheet schema](/reference/sheet-schema#the-task-tab).
:::

`todo` reads `A:Z` rather than `A:H`. That is deliberate: a column you add by hand shows up as data
instead of being silently truncated, so you notice the divergence instead of corrupting a row.

The column contract, the `_meta` tab and the id-allocation rule have their own page:
[Sheet schema](/reference/sheet-schema).

## Ids

Ids are **per space**, auto-increment, and never reused.

- The next id for a space is `max(counter, highest existing id + 1)`, floored at 1.
- The counter lives in the `_meta` tab as `next_id:<space>`.
- Because the higher of counter and highest-existing wins, a manual row deletion does not cause a
  collision, and a failed counter write does not either.
- A deleted id is never handed out again, so `#7` means the same task forever — including in your
  shell history and in any note you wrote down.

```text
default tab:  ids 1, 2, 3        → next add gets 4
work tab:     ids 1, 2           → next add gets 3
delete default #2                → default next add still gets 4, never 2
```

## Concurrency

Two processes writing to the same space at the same time are governed by Google Sheets' own
last-write-wins semantics. `todo` does not take locks and does not merge. The one race it does handle
explicitly is tab creation: if two processes bootstrap the same space simultaneously, the loser of
the `AddSheet` call adopts the tab the winner created instead of reporting a spurious conflict.

Practical guidance: one process per space at a time. Interactive CLI use and a dashboard open in a
browser on the same space is fine for normal editing; bulk scripting while also clicking in the sheet
is not.

## Moving tasks between spaces

There is no `todo move`. To relocate a task, read it, re-create it in the target space, and delete it
from the source:

```bash
todo show 7 -s home --json
todo add "call the bank" -s work -d "ask about the wire transfer" --due 2026-11-28 --remarks "ref 88213"
todo rm 7 -s home -y
```

The task gets a **new id** in the target space, since ids are per-space. `created_at` is reset to the
moment of the new insert; `completed_at` is not carried across. If preserving any of that matters,
edit the sheet directly — see [what is safe to edit by hand](/reference/sheet-schema#safe-to-edit-by-hand).
