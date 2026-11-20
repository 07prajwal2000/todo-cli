namespace Todo.Application.Common;

public enum ErrorCode
{
    None = 0,
    Validation,
    NotFound,
    Conflict,
    Configuration,
    Permission,
    Unavailable,
    Unexpected,
}
