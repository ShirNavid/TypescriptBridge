namespace TsHandler.Tool;

public static class ErrorCodes
{
    public const string General = "TS_HANDLER0001";
    public const string ConfigInvalidJson = "TS_HANDLER0002";
    public const string ConfigValidationFailed = "TS_HANDLER0003";
    public const string EntryPointNotFound = "TS_HANDLER0004";
    public const string LargePayload = "TS_HANDLER0005";
    public const string EsbuildUnavailable = "TS_HANDLER0006";
    public const string EsbuildFailed = "TS_HANDLER0007";
    public const string NodeUnavailable = "TS_HANDLER0008";
    public const string NpmInstallFailed = "TS_HANDLER0009";
    public const string InvalidProject = "TS_HANDLER0010";
    public const string UnsupportedExternalDependency = "TS_HANDLER0011";
    public const string UnsupportedMultiTargeting = "TS_HANDLER0012";
}

public sealed class TsHandlerException : Exception
{
    public string Code { get; }
    public int ExitCode { get; }

    public TsHandlerException(string code, string message, int exitCode = 1)
        : base(message)
    {
        Code = code;
        ExitCode = exitCode;
    }

    public TsHandlerException(string code, string message, Exception inner, int exitCode = 1)
        : base(message, inner)
    {
        Code = code;
        ExitCode = exitCode;
    }
}
