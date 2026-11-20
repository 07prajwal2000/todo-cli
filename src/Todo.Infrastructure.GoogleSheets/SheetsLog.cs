using Microsoft.Extensions.Logging;

namespace Todo.Infrastructure.GoogleSheets;

/// <summary>
/// Source-generated log methods. They keep the message templates out of the hot paths and avoid
/// formatting arguments when the level is disabled.
/// </summary>
internal static partial class SheetsLog
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Created the Google Sheets client for {Email}.")]
    public static partial void ClientCreated(ILogger logger, string? email);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Google Sheets request failed on attempt {Attempt} of {MaxAttempts}; retrying in {Delay}.")]
    public static partial void RequestRetrying(ILogger logger, int attempt, int maxAttempts, TimeSpan delay);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Warning,
        Message = "Google Sheets request failed on attempt {Attempt} of {MaxAttempts}.")]
    public static partial void RequestFailed(ILogger logger, Exception exception, int attempt, int maxAttempts);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Information,
        Message = "Created sheet tab {Title}.")]
    public static partial void SheetCreated(ILogger logger, string title);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Information,
        Message = "Sheet tab {Title} already existed; adopting it.")]
    public static partial void SheetAdopted(ILogger logger, string title);

    [LoggerMessage(
        EventId = 1005,
        Level = LogLevel.Warning,
        Message = "Task {TaskId} was written to space {Space} but the id counter could not be saved: {Reason}")]
    public static partial void IdCounterNotSaved(ILogger logger, int taskId, string space, string reason);
}
