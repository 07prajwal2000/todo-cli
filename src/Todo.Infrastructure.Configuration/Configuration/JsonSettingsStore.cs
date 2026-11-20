using System.Text.Json;
using Todo.Application.Common;
using Todo.Application.Setup;

namespace Todo.Infrastructure.Configuration;

public sealed class JsonSettingsStore : ISettingsStore
{
    private const string MissingSpreadsheetIdMessage =
        "Spreadsheet ID is missing. Run 'todo init' or 'todo config set spreadsheetId <id>'.";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly ConfigPaths _paths;

    public JsonSettingsStore(ConfigPaths paths) => _paths = paths;

    public async Task<Result<TodoSettings>> LoadAsync(CancellationToken cancellationToken)
    {
        var path = _paths.SettingsFilePath;
        if (!File.Exists(path))
        {
            return Result<TodoSettings>.Failure(TodoError.Configuration(
                $"No configuration found at {path}. Run 'todo init'."));
        }

        string json;
        try
        {
            json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Result<TodoSettings>.Failure(TodoError.Configuration(
                $"Could not read the configuration at {path}: {exception.Message}"));
        }

        return Parse(json, path);
    }

    public async Task<Result> SaveAsync(TodoSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var json = JsonSerializer.Serialize(settings, SerializerOptions);
        try
        {
            await AtomicFile.WriteAsync(_paths.SettingsFilePath, json, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Result.Failure(TodoError.Configuration(
                $"Could not write the configuration to {_paths.SettingsFilePath}: {exception.Message}"));
        }

        return Result.Success();
    }

    public Task<Result<bool>> ExistsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<Result<bool>>(Result<bool>.Success(File.Exists(_paths.SettingsFilePath)));

    public string DescribeLocation() => _paths.SettingsFilePath;

    private static Result<TodoSettings> Parse(string json, string path)
    {
        TodoSettings? settings;
        try
        {
            settings = JsonSerializer.Deserialize<TodoSettings>(json, SerializerOptions);
        }
        catch (JsonException exception)
        {
            return Result<TodoSettings>.Failure(TodoError.Configuration(
                $"The configuration at {path} is not valid JSON: {exception.Message}"));
        }

        if (settings is null)
        {
            return Result<TodoSettings>.Failure(TodoError.Configuration(
                $"The configuration at {path} is empty."));
        }

        if (string.IsNullOrWhiteSpace(settings.SpreadsheetId))
        {
            return Result<TodoSettings>.Failure(TodoError.Configuration(MissingSpreadsheetIdMessage));
        }

        return Result<TodoSettings>.Success(settings);
    }
}
