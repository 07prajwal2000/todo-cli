using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Todo.Application.Common;
using Todo.Application.Tasks;
using Todo.Domain.Spaces;
using Todo.Domain.Tasks;
using Todo.Web.Assets;
using Todo.Web.Contracts;

namespace Todo.Web.Http;

/// <summary>
/// All dashboard endpoints. Handlers are static methods rather than lambdas so the request/response
/// mapping can be exercised directly by tests without a running server.
/// </summary>
internal static class DashboardEndpoints
{
    internal static void MapDashboardApi(this IEndpointRouteBuilder endpoints, DashboardDependencies dependencies)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(dependencies);

        var api = endpoints.MapGroup("/api");

        api.MapGet("/health", () => Health(dependencies));
        api.MapGet("/spaces", (CancellationToken ct) => ListSpacesAsync(dependencies, ct));
        api.MapPost("/spaces", (HttpContext context, CancellationToken ct) => CreateSpaceAsync(context, dependencies, ct));
        api.MapDelete("/spaces/{name}", (string name, CancellationToken ct) => DeleteSpaceAsync(name, dependencies, ct));

        api.MapGet("/tasks", (HttpContext context, CancellationToken ct) => ListTasksAsync(context, dependencies, ct));
        api.MapGet("/tasks/{id}", (string id, HttpContext context, CancellationToken ct) =>
            GetTaskAsync(id, context, dependencies, ct));
        api.MapPost("/tasks", (HttpContext context, CancellationToken ct) => CreateTaskAsync(context, dependencies, ct));
        api.MapPatch("/tasks/{id}", (string id, HttpContext context, CancellationToken ct) =>
            PatchTaskAsync(id, context, dependencies, ct));
        api.MapDelete("/tasks/{id}", (string id, HttpContext context, CancellationToken ct) =>
            DeleteTaskAsync(id, context, dependencies, ct));
    }

    internal static void MapDashboardAssets(this IEndpointRouteBuilder endpoints, EmbeddedAssetProvider assets)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(assets);

        // Serves the embedded bundle. Unknown non-root paths are 404 rather than index.html so a
        // stale asset reference fails loudly instead of silently returning HTML.
        endpoints.MapGet("/{**path}", (string? path, HttpContext context) => ServeAsset(path, context, assets));
    }

    internal static IResult ServeAsset(string? path, HttpContext? context, EmbeddedAssetProvider assets)
    {
        ArgumentNullException.ThrowIfNull(assets);

        var requestPath = string.IsNullOrEmpty(path) ? "/" : "/" + path;

        if (!assets.TryResolve(requestPath, out var fileName))
        {
            return Results.NotFound();
        }

        if (context is not null)
        {
            context.Response.Headers.CacheControl = EmbeddedAssetProvider.CacheControlFor(fileName);
        }

        return Results.Stream(
            assets.OpenRead(fileName),
            EmbeddedAssetProvider.ContentTypeFor(fileName));
    }

    internal static IResult Health(DashboardDependencies dependencies)
    {
        ArgumentNullException.ThrowIfNull(dependencies);

        return Results.Json(
            new HealthResponse
            {
                Status = "ok",
                Provider = dependencies.Provider,
                DefaultSpace = DefaultSpaceOf(dependencies).Value,
                SpreadsheetId = dependencies.SpreadsheetId,
                ServerTime = dependencies.Clock.UtcNow.UtcDateTime,
            },
            DashboardJsonContext.Default.HealthResponse);
    }

    internal static async Task<IResult> ListSpacesAsync(
        DashboardDependencies dependencies,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dependencies);

        var listed = await dependencies.SpaceService.ListAsync(cancellationToken).ConfigureAwait(false);

        return ResultMapping.ToHttpResult(listed, spaces => Results.Json(
            new SpacesResponse
            {
                Spaces = [.. spaces.Select(space => space.Value)],
                DefaultSpace = DefaultSpaceOf(dependencies).Value,
            },
            DashboardJsonContext.Default.SpacesResponse));
    }

    internal static async Task<IResult> CreateSpaceAsync(
        HttpContext context,
        DashboardDependencies dependencies,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(dependencies);

        var body = await ReadJsonAsync(context, DashboardJsonContext.Default.CreateSpaceRequest, cancellationToken)
            .ConfigureAwait(false);

        if (!body.TryGetValue(out var request))
        {
            return ResultMapping.ToHttpResult(body.Error!);
        }

        var created = await dependencies.SpaceService
            .CreateAsync(request.Name, cancellationToken)
            .ConfigureAwait(false);

        return ResultMapping.ToHttpResult(created, space => Results.Json(
            new SpaceResponse { Name = space.Value },
            DashboardJsonContext.Default.SpaceResponse,
            statusCode: StatusCodes.Status201Created));
    }

    internal static async Task<IResult> DeleteSpaceAsync(
        string name,
        DashboardDependencies dependencies,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dependencies);

        var deleted = await dependencies.SpaceService.DeleteAsync(name, cancellationToken).ConfigureAwait(false);

        return ResultMapping.ToHttpResult(deleted, Results.NoContent);
    }

    internal static async Task<IResult> ListTasksAsync(
        HttpContext context,
        DashboardDependencies dependencies,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(dependencies);

        var space = ResolveSpace(context.Request.Query["space"], dependencies);
        if (!space.TryGetValue(out var spaceName))
        {
            return ResultMapping.ToHttpResult(space.Error!);
        }

        var query = BuildQuery(context.Request, out var queryError);
        if (queryError is not null)
        {
            return ResultMapping.ToHttpResult(queryError);
        }

        var listed = await dependencies.TaskService
            .ListAsync(spaceName, query, cancellationToken)
            .ConfigureAwait(false);

        return ResultMapping.ToHttpResult(listed, tasks => Results.Json(
            new TasksResponse { Tasks = TaskMapping.ToDtos(tasks), Space = spaceName.Value },
            DashboardJsonContext.Default.TasksResponse));
    }

    internal static async Task<IResult> GetTaskAsync(
        string id,
        HttpContext context,
        DashboardDependencies dependencies,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(dependencies);

        var space = ResolveSpace(context.Request.Query["space"], dependencies);
        if (!space.TryGetValue(out var spaceName))
        {
            return ResultMapping.ToHttpResult(space.Error!);
        }

        if (!TryParseId(id, out var taskId))
        {
            return NotFoundTask(id);
        }

        var found = await dependencies.TaskService
            .GetAsync(spaceName, taskId, cancellationToken)
            .ConfigureAwait(false);

        return ResultMapping.ToHttpResult(found, task => Results.Json(
            TaskMapping.ToDto(task),
            DashboardJsonContext.Default.TaskDto));
    }

    internal static async Task<IResult> CreateTaskAsync(
        HttpContext context,
        DashboardDependencies dependencies,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(dependencies);

        var body = await ReadJsonAsync(context, DashboardJsonContext.Default.CreateTaskRequest, cancellationToken)
            .ConfigureAwait(false);

        if (!body.TryGetValue(out var request))
        {
            return ResultMapping.ToHttpResult(body.Error!);
        }

        var space = ResolveSpace(request.Space, dependencies);
        if (!space.TryGetValue(out var spaceName))
        {
            return ResultMapping.ToHttpResult(space.Error!);
        }

        if (!TryParseState(request.Status, out var state, out var stateError))
        {
            return ResultMapping.ToHttpResult(stateError!);
        }

        if (!TryParseDueDate(request.DueOn, out var dueOn, out var dueError))
        {
            return ResultMapping.ToHttpResult(dueError!);
        }

        var added = await dependencies.TaskService
            .AddAsync(
                spaceName,
                new TaskDraftInput
                {
                    Name = request.Name,
                    Description = request.Description,
                    State = state,
                    DueOn = dueOn,
                    Remarks = request.Remarks,
                },
                cancellationToken)
            .ConfigureAwait(false);

        return ResultMapping.ToHttpResult(added, task => Results.Json(
            TaskMapping.ToDto(task),
            DashboardJsonContext.Default.TaskDto,
            statusCode: StatusCodes.Status201Created));
    }

    /// <summary>
    /// A single PATCH can carry both content edits and a status change. The edit is applied first so
    /// the state change operates on the already-edited task and the response is the final state.
    /// </summary>
    internal static async Task<IResult> PatchTaskAsync(
        string id,
        HttpContext context,
        DashboardDependencies dependencies,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(dependencies);

        var space = ResolveSpace(context.Request.Query["space"], dependencies);
        if (!space.TryGetValue(out var spaceName))
        {
            return ResultMapping.ToHttpResult(space.Error!);
        }

        if (!TryParseId(id, out var taskId))
        {
            return NotFoundTask(id);
        }

        var body = await ReadJsonAsync(context, DashboardJsonContext.Default.UpdateTaskRequest, cancellationToken)
            .ConfigureAwait(false);

        if (!body.TryGetValue(out var request))
        {
            return ResultMapping.ToHttpResult(body.Error!);
        }

        var hasStateChange = !string.IsNullOrWhiteSpace(request.Status);
        var target = TaskState.Pending;

        if (hasStateChange && !TaskStates.TryParse(request.Status, out target))
        {
            return UnknownStatus(request.Status!);
        }

        if (!TryParseDueDate(request.DueOn, out var dueOn, out var dueError))
        {
            return ResultMapping.ToHttpResult(dueError!);
        }

        var hasEdit = request.Name is not null
            || request.Description is not null
            || request.Remarks is not null
            || dueOn is not null
            || request.ClearDue is true;

        var current = await dependencies.TaskService
            .GetAsync(spaceName, taskId, cancellationToken)
            .ConfigureAwait(false);

        if (!current.TryGetValue(out var task))
        {
            return ResultMapping.ToHttpResult(current.Error!);
        }

        if (hasEdit)
        {
            var edited = await dependencies.TaskService
                .EditAsync(
                    spaceName,
                    taskId,
                    new TaskEditInput
                    {
                        Name = request.Name,
                        Description = request.Description,
                        DueOn = dueOn,
                        ClearDue = request.ClearDue is true,
                        Remarks = request.Remarks,
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            if (!edited.TryGetValue(out task))
            {
                return ResultMapping.ToHttpResult(edited.Error!);
            }
        }

        if (hasStateChange && task.Status != target)
        {
            var changed = await dependencies.TaskService
                .ChangeStateAsync(spaceName, taskId, target, cancellationToken)
                .ConfigureAwait(false);

            if (!changed.TryGetValue(out task))
            {
                return ResultMapping.ToHttpResult(changed.Error!);
            }
        }

        return Results.Json(TaskMapping.ToDto(task), DashboardJsonContext.Default.TaskDto);
    }

    internal static async Task<IResult> DeleteTaskAsync(
        string id,
        HttpContext context,
        DashboardDependencies dependencies,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(dependencies);

        var space = ResolveSpace(context.Request.Query["space"], dependencies);
        if (!space.TryGetValue(out var spaceName))
        {
            return ResultMapping.ToHttpResult(space.Error!);
        }

        if (!TryParseId(id, out var taskId))
        {
            return NotFoundTask(id);
        }

        var deleted = await dependencies.TaskService
            .DeleteAsync(spaceName, taskId, cancellationToken)
            .ConfigureAwait(false);

        return ResultMapping.ToHttpResult(deleted, Results.NoContent);
    }

    internal static TaskQuery BuildQuery(HttpRequest request, out TodoError? error)
    {
        ArgumentNullException.ThrowIfNull(request);

        error = null;
        var includeAll = false;
        var states = new List<TaskState>();

        var rawStatus = request.Query["status"].ToString();

        if (!string.IsNullOrWhiteSpace(rawStatus))
        {
            foreach (var token in rawStatus.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (string.Equals(token, "all", StringComparison.OrdinalIgnoreCase))
                {
                    includeAll = true;
                    continue;
                }

                if (!TaskStates.TryParse(token, out var state))
                {
                    error = UnknownStatusError(token);
                    return new TaskQuery();
                }

                if (!states.Contains(state))
                {
                    states.Add(state);
                }
            }
        }

        var search = request.Query["q"].ToString();

        return new TaskQuery
        {
            States = states,
            SearchTerm = string.IsNullOrWhiteSpace(search) ? null : search,
            IncludeAllStates = includeAll,
        };
    }

    internal static Result<SpaceName> ResolveSpace(string? requested, DashboardDependencies dependencies)
    {
        ArgumentNullException.ThrowIfNull(dependencies);

        if (string.IsNullOrWhiteSpace(requested))
        {
            return DefaultSpaceOf(dependencies);
        }

        if (!SpaceName.IsValid(requested, out var error))
        {
            return TodoError.Validation(error!, new Dictionary<string, string[]> { ["space"] = [error!] });
        }

        return SpaceName.Create(requested);
    }

    internal static SpaceName DefaultSpaceOf(DashboardDependencies dependencies) =>
        SpaceName.TryCreate(dependencies.DefaultSpace) ?? SpaceName.Default;

    internal static bool TryParseId(string? raw, out TaskId id)
    {
        id = TaskId.None;

        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value <= 0)
        {
            return false;
        }

        id = new TaskId(value);
        return true;
    }

    internal static async Task<Result<T>> ReadJsonAsync<T>(
        HttpContext context,
        JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(typeInfo);

        if (context.Request.ContentLength is 0)
        {
            return TodoError.Validation("A JSON request body is required.", FieldError("body"));
        }

        try
        {
            var body = await context.Request
                .ReadFromJsonAsync(typeInfo, cancellationToken)
                .ConfigureAwait(false);

            return body is null
                ? TodoError.Validation("A JSON request body is required.", FieldError("body"))
                : Result<T>.Success(body);
        }
        catch (JsonException exception)
        {
            return TodoError.Validation($"The request body is not valid JSON: {exception.Message}", FieldError("body"));
        }
        catch (BadHttpRequestException exception)
        {
            return TodoError.Validation(exception.Message, FieldError("body"));
        }
    }

    private static IResult NotFoundTask(string? id) =>
        ResultMapping.ToHttpResult(TodoError.NotFound($"Task '{id}' was not found."));

    private static IResult UnknownStatus(string raw) => ResultMapping.ToHttpResult(UnknownStatusError(raw));

    private static TodoError UnknownStatusError(string raw) =>
        TodoError.Validation(
            $"Unknown status '{raw}'. Accepted values: all, {TaskStates.DescribeAccepted()}.",
            new Dictionary<string, string[]> { ["status"] = [$"Accepted values: all, {TaskStates.DescribeAccepted()}."] });

    private static Dictionary<string, string[]> FieldError(string field, string message) =>
        new(StringComparer.Ordinal) { [field] = [message] };

    private static Dictionary<string, string[]> FieldError(string field) => FieldError(field, "Required.");

    private static bool TryParseState(string? raw, out TaskState state, out TodoError? error)
    {
        state = TaskState.Pending;
        error = null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        if (TaskStates.TryParse(raw, out state))
        {
            return true;
        }

        error = TodoError.Validation(
            $"Unknown status '{raw}'. Accepted values: {TaskStates.DescribeAccepted()}.",
            new Dictionary<string, string[]> { ["status"] = [$"Accepted values: {TaskStates.DescribeAccepted()}."] });

        return false;
    }

    private static bool TryParseDueDate(string? raw, out DueDate? dueOn, out TodoError? error)
    {
        dueOn = null;
        error = null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        if (DueDate.TryParse(raw, out var parsed))
        {
            dueOn = parsed;
            return true;
        }

        error = TodoError.Validation(
            $"'{raw}' is not a valid due date. Expected format: {DueDate.Format}.",
            new Dictionary<string, string[]> { [TaskSchema.DueOnColumn] = [$"Expected format: {DueDate.Format}."] });

        return false;
    }
}
