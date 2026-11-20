# Notes

Notes are simple markdown documents that live next to your tasks, on one extra tab called `_notes`.
They are not scoped to a space. Like `_meta`, the leading underscore keeps the tab out of
`todo spaces`, so it can never collide with a space of your own (even one named `notes`).

| Column | Meaning |
| --- | --- |
| `id` | Positive integer, `max + 1` on add. |
| `title` | Required, max 200 chars. |
| `content` | Markdown, max 50 000 chars (the Google Sheets cell limit). |
| `created_at` | ISO-8601 UTC, `yyyy-MM-ddTHH:mm:ssZ`. |

The tab is created on first use.

## Commands

```bash
todo notes add "Standup" -c "# Monday"                    # inline markdown
todo notes add "Design" -f design.md                      # from a file
cat design.md | todo notes add "Design" --stdin           # from stdin
todo notes ls                                             # newest first; -q <text> filters
todo notes show 3                                         # prints the raw markdown
todo notes edit 3 --title "New title" -f updated.md
todo notes rm 3 -y                                        # -y skips the confirmation
```

Every command accepts `--json`. `--content`, `--file` and `--stdin` are mutually exclusive; on
`edit`, omitting all three leaves the content unchanged.

## Dashboard

`todo server` has a **Notes** view (`#/notes`): a searchable list on the left and a rendered markdown
viewer on the right. **New note** and **Edit** open an editor with a live preview; `Ctrl+S` saves.
Markdown is rendered with GitHub-flavoured extensions (tables, task lists, fenced code) and always
sanitized, so raw HTML such as `<script>` or `onerror=` never executes. Links open in a new tab.

The HTTP API is listed under [Dashboard](/guide/dashboard#routes).

## Limits

Ids are `max + 1`, so deleting the newest note frees its id for the next one. Writes are `RAW`, like
tasks, so `=SUM(1,2)` in a note stays text.
