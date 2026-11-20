namespace Todo.Application.Common;

public sealed record TodoError
{
    private TodoError(ErrorCode code, string message, IReadOnlyDictionary<string, string[]>? fields)
    {
        Code = code;
        Message = message;
        Fields = fields;
    }

    public ErrorCode Code { get; }

    public string Message { get; }

    public IReadOnlyDictionary<string, string[]>? Fields { get; }

    public static TodoError Validation(string message, IReadOnlyDictionary<string, string[]>? fields = null) =>
        new(ErrorCode.Validation, message, fields);

    public static TodoError NotFound(string message) => new(ErrorCode.NotFound, message, null);

    public static TodoError Conflict(string message) => new(ErrorCode.Conflict, message, null);

    public static TodoError Configuration(string message) => new(ErrorCode.Configuration, message, null);

    public static TodoError Permission(string message) => new(ErrorCode.Permission, message, null);

    public static TodoError Unavailable(string message) => new(ErrorCode.Unavailable, message, null);

    public static TodoError Unexpected(string message) => new(ErrorCode.Unexpected, message, null);
}
