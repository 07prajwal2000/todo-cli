# Architecture

The solution is six projects in a hexagonal (ports and adapters) arrangement. The dependency rule is
not a convention — it is enforced by a test project.

```text
                    ┌─────────────────────────────┐
                    │  Todo.Cli   (composition)   │
                    │  Todo.Web   (dashboard)     │
                    └───────────┬─────────────────┘
                                │ depends on
              ┌─────────────────┼──────────────────┐
              ▼                 ▼                  ▼
   ┌────────────────────┐ ┌──────────────────┐ ┌──────────────┐
   │ Infrastructure.    │ │ Infrastructure.  │ │ Application  │
   │ GoogleSheets       │ │ Configuration    │ │ (ports +     │
   │ (adapter)          │ │ (adapter)        │ │  services)   │
   └─────────┬──────────┘ └────────┬─────────┘ └──────┬───────┘
             │                     │                  │
             └─────────────────────┴──────────────────┘
                                ▼
                     ┌─────────────────────┐
                     │   Todo.Domain       │
                     │   (no references)   │
                     └─────────────────────┘
```

## The projects

| Project | Type | Depends on | Contents |
| --- | --- | --- | --- |
| `Todo.Domain` | core | **nothing but the BCL** | `TaskItem`, `TaskState`, `TaskStates`, `StatusTransitions`, `TaskTransitionPolicy`, `TaskSchema`, `DueDate`, `TaskId`, `TaskValidator`, `SpaceName`, `FieldErrors` |
| `Todo.Application` | core | `Domain` | `TaskService`, `TaskQuery`, `TaskSorter`, `SpaceService`, `SetupService`, `DiagnosticsService`, the ports (`ITaskRepository`, `ISpaceRepository`, `IBackendProbe`, `ISettingsStore`, `ICredentialStore`, `IClock`), `Result<T>`, `TodoError`, `ErrorCode` |
| `Todo.Infrastructure.Configuration` | adapter | `Application`, `Domain` | `ConfigPaths`, `JsonSettingsStore`, `FileCredentialStore`, `ServiceAccountKey`, `AtomicFile`, `SystemClock` |
| `Todo.Infrastructure.GoogleSheets` | adapter | `Application`, `Domain` | `SheetsConnectionFactory`, `SheetsGateway`, `SheetsTaskRepository`, `SheetsSpaceRepository`, `SheetsBackendProbe`, `RetryPolicy`, `GoogleRequestExecutor`, `Mapping/*` |
| `Todo.Web` | presentation | `Application`, `Domain` | `DashboardHost`, `DashboardOptions`, `DashboardEndpoints`, `LocalRequestGuard`, `ResultMapping`, embedded `wwwroot` assets |
| `Todo.Cli` | composition root + presentation | everything | `Program`, `ServiceRegistration`, `CommandRegistrar`, every command, `ConsoleReporter`, `ExitCodeMapper`, `SpaceResolver` |

`Todo.Cli` is the only project that references every other one. `Todo.Web` never sees configuration or
composition; it receives a `DashboardDependencies` record assembled by the CLI.

## Why the layers exist

Each seam earns its place by removing a specific class of bug.

**`Todo.Domain` has zero external references.** No Google SDK, no DI container, no HTTP. The rules that
must never drift — the transition graph, `completed_at` maintenance, validation limits, the space
naming rules, the schema declaration — are testable with no I/O and no mocks. `TaskTransitionPolicy`
owns the derived `completed_at` field so no caller can forget it or set it inconsistently.

**`Todo.Application` holds ports, not implementations.** `ITaskRepository` is five methods, `TaskQuery`
is a filter object, `Result<T>` is an explicit success/failure that cannot be ignored the way an
exception can. `TaskService` does validation and filtering; it never knows whether the rows came from
Sheets or a test double.

**Adapters are interchangeable and ignorant of each other.** The two infrastructure projects do not
reference each other. `GoogleSheets` parses the credential JSON itself (`SheetsConnectionFactory`)
rather than reaching into `Configuration`, so it stays usable standalone while remaining the only
project that knows Google's auth types.

**Presentations are thin.** `Todo.Cli` commands parse flags and call one service method. `Todo.Web`
endpoints do the same over HTTP. Neither re-implements a rule. `ResultMapping` is the single place any
`TodoError` becomes an HTTP status, and `ExitCodeMapper` is the single place any `TodoError` becomes a
process exit code.

**`ErrorCode` is the shared vocabulary** between the two presentations. Seven values —
`Validation`, `NotFound`, `Conflict`, `Configuration`, `Permission`, `Unavailable`, `Unexpected` — map
to four exit codes and eight HTTP statuses, and both mappings live in one file each.

## The dependency rule is enforced by tests

`tests/Todo.Architecture.Tests/LayerDependencyTests.cs` fails the build if the layering is violated. It
uses two mechanisms because neither alone is sufficient:

- **NetArchTest type rules** catch a single type reaching across a layer.
- **Module-reference assertions** catch a project or package reference even when no type uses it yet,
  which matters for assemblies that declare almost no types.

One fact per edge, so a violation names exactly one broken dependency. The rules:

| Rule | Meaning |
| --- | --- |
| `DomainReferencesNothingButTheBaseClassLibrary` | The domain may reference only `System.*`, `netstandard`, `Microsoft.NETCore` and itself |
| `DomainDoesNotDependOn…` (Application, GoogleSheets, Configuration, Web, Cli, GoogleApis, MicrosoftExtensions) | Seven separate facts |
| `ApplicationDoesNotDependOn…` (GoogleSheets, Configuration, Web, Cli, GoogleApis) | The application layer never names an adapter |
| `OnlyGoogleSheetsInfrastructureReferencesGoogleApisTypes` | Google SDK types exist in exactly one project |
| `OnlyGoogleSheetsInfrastructureReferencesGoogleApisAssemblies` | ...and at the module level too |
| `TheTwoAdaptersDoNotReferenceEachOther` | Configuration and GoogleSheets are siblings |
| `NoCoreOrAdapterLayerReferencesAHostAssembly` | Nothing points at a presentation |

`DomainShapeTests` adds shape rules that keep the model closed:

| Rule | Rationale |
| --- | --- |
| Every non-abstract domain class is sealed | The model cannot be extended from outside the assembly |
| Domain types live only in `Todo.Domain`, `.Common`, `.Tasks`, `.Spaces` | No namespace sprawl |
| `TaskId`, `DueDate`, `SpaceName` are value types | They cannot be null |
| `TaskItem`, `FieldErrors` are records | Structural equality |
| No public mutable static fields in the domain | No process-wide state in the core |

::: warning If you are adding a reference, expect a test to stop you
Adding `using Google.Apis...` to `Todo.Application` compiles fine and fails
`ApplicationDoesNotDependOnGoogleApis`. That is the intended workflow: the failure message tells you
which edge you crossed. Do not "fix" it by relaxing the test — move the logic to the adapter.
:::

## Composition

`Todo.Cli/Composition/ServiceRegistration.cs` is the composition root. It is the only place that knows
every layer exists:

```csharp
services.AddConfigurationInfrastructure();   // ConfigPaths, ISettingsStore, ICredentialStore, IClock
services.AddGoogleSheetsInfrastructure();    // RetryPolicy, executor, connection factory, gateway, repositories, probe
services.AddSingleton<TaskService>();
services.AddSingleton<SpaceService>();
services.AddSingleton<SetupService>();
services.AddSingleton<DiagnosticsService>();
```

Everything is a singleton: the services hold only per-process caches, and the credential and
spreadsheet lookups are serialised inside `SheetsConnectionFactory` with a semaphore rather than by
lifetime scoping.

`CommandRegistrar.Register` is the authoritative command list. Adding a command is one line there plus
the command type.

## Request flow

A `todo done 4` is a useful end-to-end trace:

```text
Program.Main
  └─ ServiceRegistration.Build()                        DI container
  └─ CommandApp.RunAsync(["done", "4"])
       └─ DoneCommand.ExecuteAsync                      parse <ID>
            └─ StateCommands.ApplyAsync
                 ├─ SpaceResolver.ResolveAsync           -s → TODO_SPACE → config
                 └─ TaskService.ChangeStateAsync
                      ├─ ITaskRepository.FindAsync       → SheetsTaskRepository
                      │     └─ SheetsGateway.OpenTaskTabAsync
                      │          ├─ EnsureTaskTabAsync   create tab + headers if missing
                      │          ├─ EnsureMetaAsync      create _meta if missing
                      │          └─ ReadValuesAsync      A:Z, UNFORMATTED_VALUE
                      ├─ TaskTransitionPolicy.IsAllowed  StatusTransitions
                      ├─ TaskTransitionPolicy.Apply      stamps/clears completed_at
                      └─ ITaskRepository.UpdateAsync     → RowRange(n), RAW update
            └─ ConsoleReporter.Success + ExitCodes.Success
```

Every Google call in that trace goes through `GoogleRequestExecutor`, which retries 429/5xx up to 5
times with truncated exponential backoff and translates SDK exceptions into `TodoError`. Cancellation is
never swallowed: a cancelled token surfaces as `OperationCanceledException` so Ctrl+C stays instant.

## Extension recipes

### Add a column to the sheet schema

Four edits, in this order. The architecture tests will not stop you here — the domain owns the schema
— but the mappers must agree.

1. **Declare it.** `Todo.Domain/Tasks/TaskSchema.cs`:
   ```csharp
   public const string PriorityColumn = "priority";

   public static IReadOnlyList<string> Columns { get; } =
   [
       IdColumn, NameColumn, DescriptionColumn, StatusColumn,
       DueOnColumn, CreatedAtColumn, CompletedAtColumn, RemarksColumn,
       PriorityColumn,            // append: column order is positional on write
   ];
   ```
   Add it to `RequiredColumns` only if a sheet missing it should be a conflict. Bump `Version` if
   existing sheets need repairing — `todo doctor --fix` appends missing *optional* headers to the end of
   an existing header row, which is why appending matters.

2. **Model it.** `Todo.Domain/Tasks/TaskItem.cs` gets the property; `TaskValidator` gets a limit;
   `ITaskRepository.TaskDraft` and the `TaskDraftInput` / `TaskEditInput` records in `TaskService.cs`
   pass it through.

3. **Map it.** `Todo.Infrastructure.GoogleSheets/Mapping/TaskRowMapper.cs` — add the cell to `ToRow` (in
   `Columns` order) and read it in `FromRow` through `binding.TextAt(...)`.

4. **Surface it.** The CLI flags in `AddCommand` / `EditCommand`, the dashboard's `TaskDto` plus
   `CreateTaskRequest` / `UpdateTaskRequest`, and `SourceGeneration` registration in
   `DashboardJsonContext`.

Test recipe: add a round-trip `ToRow`/`FromRow` case to `TaskRowMapperTests` and a header-validation case
to `TaskSchemaBindingTests`.

### Add a state

1. **Enum.** `Todo.Domain/Tasks/TaskState.cs` — add the member. Named `TaskState`, not `TaskStatus`, so it
   does not collide with `System.Threading.Tasks.TaskStatus`.
2. **Name and aliases.** `TaskStates.cs` — add to `CanonicalNames`, add every alias to `Aliases`, and add
   it to `All` (which is the order `DescribeAccepted()` prints).
3. **Transitions.** `StatusTransitions.Edges` — a missing entry is a `KeyNotFoundException` at runtime,
   not a compile error, so add the key and both directions of intent. If the state means "finished", add
   it to `TaskTransitionPolicy.Apply`'s completed check.
4. **Sort priority.** `TaskSorter.StatePriority` — again a dictionary lookup; a missing key throws.
5. **Presentation.** `ConsoleReporter.FormatStatus` colour, and the dashboard's `STATE_LABELS`,
   `ALLOWED_TRANSITIONS` and status `<select>` in `wwwroot/app.js` + `index.html`.

Then extend `StatusTransitionsTests` and the UI's transition table.

::: danger Three dictionaries are keyed by state and none is exhaustively checked
`StatusTransitions.Edges`, `TaskSorter.StatePriority` and `TaskStates.CanonicalNames` are all
`Dictionary<TaskState, …>` with no fallback. Adding an enum member without touching all three throws at
run time on the first task in that state. This is a known sharp edge, not a safety net.
:::

### Add a backend

The seam exists specifically for this; today only `google-sheets` ships.

1. **Implement the ports** from `Todo.Application`: `ITaskRepository`, `ISpaceRepository`,
   `IBackendProbe`. Optionally `ISettingsStore` / `ICredentialStore` if the backend needs different
   config.
2. **New project** `Todo.Infrastructure.<Backend>` referencing `Application` and `Domain` only.
   **Not** `Configuration`, and not the GoogleSheets project — `TheTwoAdaptersDoNotReferenceEachOther`
   enforces the sibling rule.
3. **Registration.** Add an `Add<Backend>Infrastructure(this IServiceCollection)` extension, mirroring
   `AddGoogleSheetsInfrastructure`. Keep the null-logger fallback so hosts are not forced to configure
   logging.
4. **Selection.** `ProviderNames` in `Todo.Application/Setup/ISettingsStore.cs` currently has one
   constant. Add a second, then branch on `TodoSettings.Provider` in `ServiceRegistration`. `todo config
   set provider` already validates against `ProviderNames.GoogleSheets` only, so it needs the same
   change.
5. **Guard the boundary.** If the new backend brings its own SDK, add a `LayerDependencyTests` fact that
   only that project references it — mirror `OnlyGoogleSheetsInfrastructureReferencesGoogleApisAssemblies`.

### Add a command

1. Write the command class in `Todo.Cli/Commands/`, deriving from `AsyncCommand<TSettings>`.
2. Pick a settings base: `GlobalSettings` for `-s`/`--json`/`-v`/`--no-color`, `SpaceSettings` for
   `-s`/`--json`, or plain `CommandSettings` for none.
3. Return `ExitCodes.Success` on success and route failures through `reporter.ReportError(error)`, which
   prints one red line plus field errors and returns the mapped exit code. Do not return bare integers.
4. Register it in `CommandRegistrar.Register` — one line. For a subcommand group, use `AddBranch`, as
   `space` does.
5. Take services through the constructor; the container resolves them. Never `new` an adapter inside a
   command.

A command should contain parsing and presentation only. Anything else belongs in `Todo.Application`,
where both presentations get it.

## Honest limitations

| Area | Reality |
| --- | --- |
| Column reordering on write | Reads honour header positions; writes are positional from column A. The mapper is not symmetric yet. |
| `-v` / `--no-color` | Declared and parsed, never read. No behaviour. |
| Concurrency | None beyond Sheets' last-write-wins. Tab creation races are handled; row writes are not. |
| State dictionaries | Three lookup tables are not exhaustively checked; a new enum member fails at runtime. |
| Schema migration | `schema_version` exists but there is only version 1 and no migration engine. |
| Trimming / AOT | Not enabled, deliberately — see [Support matrix](/reference/support-matrix#why-the-binary-is-large). |
