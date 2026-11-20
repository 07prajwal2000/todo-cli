namespace Todo.Cli.Infrastructure;

/// <summary>
/// Exit codes are part of the CLI's contract: scripts branch on them, so the mapping from an
/// application error to a process exit code lives in one place.
/// </summary>
public static class ExitCodes
{
    public const int Success = 0;
    public const int Failure = 1;
    public const int Usage = 2;
    public const int Configuration = 3;
}
