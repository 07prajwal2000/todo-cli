using Todo.Application.Common;
using Todo.Application.Setup;
using Todo.Domain.Spaces;

namespace Todo.Cli.Infrastructure;

/// <summary>
/// Resolves which space a command acts on: explicit flag, then the environment, then the configured
/// default. Centralised so every command agrees and the precedence is documented in one place.
/// </summary>
public sealed class SpaceResolver(ISettingsStore settingsStore)
{
    public const string EnvironmentVariable = "TODO_SPACE";

    public async Task<Result<SpaceName>> ResolveAsync(string? flagValue, CancellationToken cancellationToken)
    {
        var candidate = FirstNonEmpty(
            flagValue,
            Environment.GetEnvironmentVariable(EnvironmentVariable));

        if (candidate is not null)
        {
            return SpaceName.TryCreate(candidate) is { } named
                ? Result<SpaceName>.Success(named)
                : Result<SpaceName>.Failure(TodoError.Validation(
                    SpaceName.IsValid(candidate, out var error) ? "Invalid space name." : error!));
        }

        var settings = await settingsStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (!settings.TryGetValue(out var loaded))
        {
            return Result<SpaceName>.Failure(settings.Error!);
        }

        return SpaceName.TryCreate(loaded.DefaultSpace) is { } fallback
            ? Result<SpaceName>.Success(fallback)
            : Result<SpaceName>.Failure(TodoError.Configuration(
                $"The configured default space '{loaded.DefaultSpace}' is not a valid tab name."));
    }

    private static string? FirstNonEmpty(params string?[] candidates) =>
        candidates.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
