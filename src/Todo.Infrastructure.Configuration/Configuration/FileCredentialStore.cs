using System.Text.Json;
using Todo.Application.Common;
using Todo.Application.Setup;

namespace Todo.Infrastructure.Configuration;

public sealed class FileCredentialStore : ICredentialStore
{
    private readonly ConfigPaths _paths;

    public FileCredentialStore(ConfigPaths paths) => _paths = paths;

    public async Task<Result<string>> ReadRawAsync(CancellationToken cancellationToken)
    {
        var path = _paths.CredentialsFilePath;
        if (!File.Exists(path))
        {
            return Result<string>.Failure(TodoError.Configuration(
                $"Credentials not found at {path}. Run 'todo init'."));
        }

        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            return Result<string>.Success(json);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Result<string>.Failure(TodoError.Configuration(
                $"Could not read the credentials at {path}: {exception.Message}"));
        }
    }

    public async Task<Result> WriteRawAsync(string json, CancellationToken cancellationToken)
    {
        if (!IsValidJson(json))
        {
            return Result.Failure(TodoError.Validation("The service account key is not valid JSON."));
        }

        try
        {
            await AtomicFile.WriteAsync(_paths.CredentialsFilePath, json, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Result.Failure(TodoError.Configuration(
                $"Could not write the credentials to {_paths.CredentialsFilePath}: {exception.Message}"));
        }

        return Result.Success();
    }

    public string DescribeLocation() => _paths.CredentialsFilePath;

    private static bool IsValidJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
