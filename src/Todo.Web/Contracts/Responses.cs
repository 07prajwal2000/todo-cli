namespace Todo.Web.Contracts;

public sealed record HealthResponse
{
    public string Status { get; init; } = "ok";

    public string Provider { get; init; } = string.Empty;

    public string? DefaultSpace { get; init; }

    public string? SpreadsheetId { get; init; }

    public DateTime ServerTime { get; init; }
}

public sealed record SpacesResponse
{
    public IReadOnlyList<string> Spaces { get; init; } = [];

    public string DefaultSpace { get; init; } = string.Empty;
}

public sealed record SpaceResponse
{
    public string Name { get; init; } = string.Empty;
}

public sealed record TasksResponse
{
    public IReadOnlyList<TaskDto> Tasks { get; init; } = [];

    public string Space { get; init; } = string.Empty;
}

public sealed record ErrorResponse
{
    public string Code { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    /// <summary>Omitted entirely when there are no field errors, so the body stays <c>{code, message}</c>.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string[]>? Fields { get; init; }
}
