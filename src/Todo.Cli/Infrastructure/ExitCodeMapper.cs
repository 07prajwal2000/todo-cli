using Todo.Application.Common;

namespace Todo.Cli.Infrastructure;

public static class ExitCodeMapper
{
    public static int For(ErrorCode code) => code switch
    {
        ErrorCode.None => ExitCodes.Success,
        ErrorCode.Validation => ExitCodes.Usage,
        ErrorCode.Configuration or ErrorCode.Permission => ExitCodes.Configuration,
        _ => ExitCodes.Failure,
    };
}
