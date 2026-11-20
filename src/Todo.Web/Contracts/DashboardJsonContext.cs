using System.Text.Json.Serialization;

namespace Todo.Web.Contracts;

/// <summary>
/// Source-generated serialization for every wire type. Registration is required for trimming/AOT
/// safety and it also guarantees the dashboard's JSON shape is fixed at compile time.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    WriteIndented = false)]
[JsonSerializable(typeof(TaskDto))]
[JsonSerializable(typeof(CreateTaskRequest))]
[JsonSerializable(typeof(UpdateTaskRequest))]
[JsonSerializable(typeof(CreateSpaceRequest))]
[JsonSerializable(typeof(HealthResponse))]
[JsonSerializable(typeof(SpacesResponse))]
[JsonSerializable(typeof(SpaceResponse))]
[JsonSerializable(typeof(TasksResponse))]
[JsonSerializable(typeof(ErrorResponse))]
public sealed partial class DashboardJsonContext : JsonSerializerContext;
