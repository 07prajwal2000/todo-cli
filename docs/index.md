---
layout: home

hero:
  name: todo
  text: A CLI TODO manager that lives in a Google Sheet
  tagline: One self-contained .NET 10 binary. Your tasks are rows in a spreadsheet you already own, edited through the official Google Sheets v4 API. No database, no server-side account, no sync service.
  actions:
    - theme: brand
      text: Get started
      link: /guide/getting-started
    - theme: alt
      text: Google Cloud setup
      link: /guide/google-cloud-setup
    - theme: alt
      text: Commands
      link: /guide/commands

features:
  - title: The sheet is the database
    details: Every space is a worksheet tab. Columns are resolved by header name, ids are per-space and never reused, and the whole state including the id counter lives in the same spreadsheet you can open in a browser.
  - title: Service-account auth only
    details: No OAuth consent screen, no refresh tokens, no client secrets. You download one JSON key, share the spreadsheet with the service account's email, and you are done.
  - title: Four states, explicit transitions
    details: pending, ongoing, backlog, completed with a documented transition graph. Moving a task into completed stamps completed_at; moving it out clears it. Illegal moves are rejected as usage errors.
  - title: Local dashboard
    details: todo server starts a loopback-only board on port 12000. Cards, status chips, search, keyboard shortcuts. It reads and writes the same sheet through the same application services as the CLI.
  - title: Honest single-file build
    details: Around 52 MB per platform, self-contained and compressed. The official Google SDK is reflection-based and not trim/AOT safe, so the build deliberately skips PublishTrimmed and PublishAot rather than shipping a binary that crashes at runtime.
  - title: Scriptable surface
    details: Stable exit codes (0, 1, 2, 3), --json output on the read commands, and a documented HTTP contract for the dashboard on top of the same TodoError model.
---

## What it is, and what it is not

`todo` is a command-line TODO manager whose only storage is a Google Sheet you own. The binary talks
to the Google Sheets v4 API directly using a Google Cloud service account. There is nothing in
between: no hosted service, no account to register, no local database to back up.

Not included, by design:

- No offline cache. Every command is a live API call.
- No multi-user concurrency control. Sheets' own last-write-wins semantics apply.
- No encryption at rest beyond whatever your Google account already provides. The sheet is a
  spreadsheet; anyone shared on it can read it.

## Thirty seconds to a task

```bash
todo init                       # paste the service-account key path, then the spreadsheet ID
todo add "write the docs" --due 2026-12-01
todo ls
```

If `todo init` is what you need help with, read [Google Cloud setup](/guide/google-cloud-setup)
first — it is the longest page here on purpose, because credential setup is where this tool is
easiest to get wrong.

## Where to go next

| If you want to | Read |
| --- | --- |
| Install a release binary and add a task | [Install and first task](/guide/getting-started) |
| Obtain and configure the Google credential | [Google Cloud setup](/guide/google-cloud-setup) |
| Look up a flag | [Commands](/guide/commands) |
| Understand worksheet tabs and the space model | [Spaces](/guide/spaces) |
| Run the local board | [Dashboard](/guide/dashboard) |
| Find the config file on this machine | [Configuration](/guide/configuration) |
| Fix a 403, a 429 or a schema conflict | [Troubleshooting](/guide/troubleshooting) |
| Know exactly which cells are safe to touch by hand | [Sheet schema](/reference/sheet-schema) |
| Understand the layering before changing code | [Architecture](/reference/architecture) |
| Build from source | [Support matrix](/reference/support-matrix) |
