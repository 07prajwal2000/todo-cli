namespace Todo.Domain.Tasks;

/// <summary>
/// Single declaration of the sheet column contract. Everything that reads or writes a task
/// tab resolves columns through this type, so adding a column is one edit here plus the mapper.
/// </summary>
public static class TaskSchema
{
    public const int Version = 1;

    public const string IdColumn = "id";
    public const string NameColumn = "name";
    public const string DescriptionColumn = "description";
    public const string StatusColumn = "status";
    public const string DueOnColumn = "due_on";
    public const string CreatedAtColumn = "created_at";
    public const string CompletedAtColumn = "completed_at";
    public const string RemarksColumn = "remarks";

    public static IReadOnlyList<string> Columns { get; } =
    [
        IdColumn,
        NameColumn,
        DescriptionColumn,
        StatusColumn,
        DueOnColumn,
        CreatedAtColumn,
        CompletedAtColumn,
        RemarksColumn,
    ];

    public static IReadOnlyList<string> RequiredColumns { get; } =
    [
        IdColumn,
        NameColumn,
        StatusColumn,
        CreatedAtColumn,
    ];

    public static string MetaTabName => "_meta";
    public static string MetaKeyColumn => "key";
    public static string MetaValueColumn => "value";
    public static string SchemaVersionKey => "schema_version";
    public static string NextIdKeyPrefix => "next_id:";

    public static string NextIdKey(string space) => NextIdKeyPrefix + space;
}
