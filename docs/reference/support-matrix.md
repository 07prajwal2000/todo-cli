# Support matrix

## Supported platforms

Releases are published for four runtime identifiers (RIDs), one self-contained single-file binary each.

| Platform | RID | Release artifact |
| --- | --- | --- |
| Windows x64 | `win-x64` | `todo-win-x64.exe` |
| Linux x64 | `linux-x64` | `todo-linux-x64` |
| macOS Apple silicon (arm64) | `osx-arm64` | `todo-osx-arm64` |
| macOS Intel (x64) | `osx-x64` | `todo-osx-x64` |

::: note No Windows arm64, no Linux arm64, no Alpine build
The release matrix is exactly those four RIDs. Windows arm64 runs the x64 binary under emulation on
Windows 11. Linux arm64 and musl-based distributions (Alpine) would need to build from source with
their own RID — see below.
:::

## Requirements at runtime

| Requirement | Detail |
| --- | --- |
| .NET runtime | **None.** The binary is self-contained; the framework is inside it. |
| Google account | Owns the spreadsheet. |
| Google Cloud project | One, with the Google Sheets API enabled and a service account. |
| Network | Access to `sheets.googleapis.com` and `oauth2.googleapis.com`. |
| Disk | ~52 MB for the binary, plus two small JSON files in the config directory. |
| Dashboard | A modern browser for `todo server`. The UI uses `<dialog>`, `fetch` and `localStorage`. |

Nothing is written outside the config directory. There is no installer, no service, no registry key
and no `ProgramData` directory.

## Build requirements

| Tool | Version | Why |
| --- | --- | --- |
| .NET SDK | **10.0** (verified against `10.0.403`) | Target framework is `net10.0` |
| Git | any recent | To clone |
| Node.js | 20 or later | **Only** to build this documentation site |

`Directory.Build.props` sets `TargetFramework` to `net10.0` for the whole solution, plus
`TreatWarningsAsErrors`, `Nullable=enable`, `ImplicitUsings=enable`, `InvariantGlobalization=true`,
`GenerateDocumentationFile=true` and central package management.

::: warning `TreatWarningsAsErrors` is on
A warning fails the build. This includes style analyzer warnings from `EnforceCodeStyleInBuild` and
`AnalysisMode=Recommended`. If you build from source and hit an `IDE…` or `CA…` diagnostic, it is being
treated as an error on purpose.
:::

## Build from source

```bash
git clone <repository-url> todo-cli
cd todo-cli
dotnet build Todo.slnx --configuration Release
dotnet test Todo.slnx --configuration Release
```

`Todo.slnx` is the solution file — the XML-based format, not `.sln`.

### Publish a single binary

This is the exact shape the release workflow uses:

::: code-group

```bash [Linux / macOS]
dotnet publish src/Todo.Cli/Todo.Cli.csproj \
  --configuration Release \
  --runtime linux-x64 \
  --self-contained true \
  --output artifacts/linux-x64 \
  -p:PublishSingleFile=true \
  -p:EnableCompressionInSingleFile=true \
  -p:DebugType=none
```

```powershell [Windows (PowerShell)]
dotnet publish src/Todo.Cli/Todo.Cli.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  --output artifacts/win-x64 `
  -p:PublishSingleFile=true `
  -p:EnableCompressionInSingleFile=true `
  -p:DebugType=none
```

:::

Swap `--runtime` for the RID you need. The output directory should contain exactly one file:

```text
artifacts/linux-x64/todo
```

```text
    Directory: D:\ProgrammingFiles\OSS\todo-cli\src\Todo.Cli\bin\Release\net10.0\win-x64\publish

todo.exe    51,819,842 bytes
```

The project file sets `PublishSingleFile`, `SelfContained`, `EnableCompressionInSingleFile` and
`DebugType=none` as defaults, and clears `AllowedReferenceRelatedFileExtensions` so `.pdb` and `.xml`
files never land next to the executable. A `RemoveDocumentationFromPublish` target strips leftover
`.xml` and `.pdb` from the publish list and drops the IIS-only `aspnetcorev2_inprocess` shim, because
"single file" should be true, not approximately true.

::: tip Run on a different architecture
Cross-publishing works: `--runtime osx-arm64` on a Linux x64 machine produces a working macOS arm64
binary. There is no native compilation step in this project.
:::

## Why the binary is large

~52 MB per platform, compressed. That is a deliberate trade-off, not an oversight.

The official Google SDK — `Google.Apis.Sheets.v4` plus `Google.Apis.Auth` and the whole
`Google.Apis` request layer — is **reflection-based and not trim-safe or AOT-safe**. It discovers and
builds requests through reflection over generated service classes, and the auth layer parses credential
JSON reflectively too. Under `PublishTrimmed` the trimmer cannot prove which members are reached, so it
removes things the SDK needs at runtime and the binary fails with `MissingMethodException` or a silent
null on the first API call. `PublishAot` fails harder and earlier.

So the project deliberately sets neither:

| Property | Value | Note |
| --- | --- | --- |
| `PublishSingleFile` | `true` | One file per RID |
| `SelfContained` | `true` | No .NET runtime install required |
| `EnableCompressionInSingleFile` | `true` | Shrinks the file at the cost of a small startup decompress |
| `PublishTrimmed` | **not set** | Would break the Google SDK |
| `PublishAot` | **not set** | Would break the Google SDK |

The trade is explicit: **one self-contained file per RID beats a smaller AOT binary that crashes.** A
52 MB download in exchange for "no runtime to install, no trim bugs to debug, no per-platform SDK
differences" is the right call for a CLI whose entire reason to exist is a frictionless setup.

::: danger Do not "optimise" this by adding PublishTrimmed
If you need a smaller binary, the honest options are (a) remove the dependency on the reflective Google
SDK by hand-writing the REST calls, or (b) accept the size. Adding `PublishTrimmed=true` produces a
binary that builds cleanly and fails at runtime — the worst possible failure mode for a tool whose
first command is a network call.
:::

The parts that *are* AOT-friendly are already written that way, which is why enabling trimming looks
tempting: `Todo.Web` uses `System.Text.Json` source generation (`DashboardJsonContext`), `Todo.Cli` uses
`CliJsonContext` for `--json` output, and logging uses `[LoggerMessage]` source generation. Those remove
reflection from *this* code — not from the Google SDK.

## Release artifacts and CI

`.github/workflows/release.yml` runs on a `v*` tag or manual dispatch, and has four jobs:

| Job | What it does |
| --- | --- |
| `verify` | `dotnet restore` → `dotnet format --verify-no-changes` → `dotnet build` → `dotnet test`, all against `Todo.slnx` on .NET `10.0.x` |
| `publish` | Matrix over the four RIDs. Publishes, asserts **exactly one** non-`.pdb`/`.xml` file exists, renames it to the artifact name, uploads it |
| `docs` | Builds this site with Node 20 and deploys it to GitHub Pages |
| `github-release` | On a tag, attaches every artifact to the GitHub release with generated release notes |

The `publish` job's rename step is worth knowing about if you add a file to the output: it counts files
and fails the release if the count is not 1. That is what keeps the "single file" promise honest.

The `docs` job:

```yaml
- uses: actions/setup-node@v4
  with:
    node-version: "20"
    cache: npm
    cache-dependency-path: docs/package-lock.json
- run: npm ci
  working-directory: docs
- run: npm run docs:build
  working-directory: docs
- uses: actions/upload-pages-artifact@v3
  with:
    path: docs/.vitepress/dist
```

Consequences for this site: `docs/package.json` must define `docs:build`, `docs/package-lock.json` must
be committed for `npm ci` to work, and the output must land in `docs/.vitepress/dist`. Because it deploys
as a GitHub Pages *project* site (`https://<owner>.github.io/todo-cli/`), VitePress is configured with
`base: '/todo-cli/'`.

## Building this documentation locally

```bash
cd docs
npm install
npm run docs:dev       # live dev server, usually http://localhost:5173/todo-cli/
```

```bash
cd docs
npm run docs:build     # production build into .vitepress/dist
npm run docs:preview   # serve the built output
```

`npm install` requires dev dependencies, so make sure `NODE_ENV=production` is not set in the shell —
npm omits `devDependencies` when it is, and VitePress itself is a dev dependency.

The build fails on dead internal links by default. That is intentional: a broken link is an error, not a
warning, and the setting is not relaxed to make builds pass.

## Version

`0.1.0`, from `CommandRegistrar.SetApplicationVersion`. Reported by `todo --version`.

## At a glance

| | |
| --- | --- |
| Language / framework | C# 13, .NET 10 (`net10.0`) |
| Binary | Single file, self-contained, compressed, ~52 MB |
| Storage | One Google Sheet, via Sheets API v4 |
| Auth | Google Cloud service account, JSON key |
| Database | None |
| Server component | None (`todo server` is a local loopback process) |
| Telemetry | None |
| Projects | 6 source, 6 test |
| State | 4 states, 4 transition edges per state |
