namespace TypescriptBridge.Tool;

// Central list of error codes emitted by the tool.
// Each code is stable and is referenced by CI logs and user-facing messages.
// Codes are intentionally not renumbered when one is removed,
// so that historical log entries remain meaningful.
public static class ErrorCodes
{
    // Unexpected internal error with no more specific code.
    public const string General = "TS_HANDLER0001";

    // The config.json file could not be parsed as JSON.
    public const string ConfigInvalidJson = "TS_HANDLER0002";

    // The config.json file parsed but failed schema validation.
    public const string ConfigValidationFailed = "TS_HANDLER0003";

    // The ts/app.ts entry point was not found in the project directory.
    public const string EntryPointNotFound = "TS_HANDLER0004";

    // The esbuild binary could not be located.
    public const string EsbuildUnavailable = "TS_HANDLER0006";

    // esbuild was started but exited with a non-zero code.
    public const string EsbuildFailed = "TS_HANDLER0007";
}

// Exception type thrown by all tool failures.
// Carries a stable error code and an exit code for the CLI.
public sealed class TypescriptBridgeException : Exception
{
    // Stable error code from ErrorCodes.
    public string Code { get; }

    // Process exit code to return from Main.
    public int ExitCode { get; }

    // Creates an exception with a code and message.
    public TypescriptBridgeException(string code, string message, int exitCode = 1)
        : base(message)
    {
        Code = code;
        ExitCode = exitCode;
    }

    // Creates an exception with a code, message, and inner exception.
    public TypescriptBridgeException(string code, string message, Exception inner, int exitCode = 1)
        : base(message, inner)
    {
        Code = code;
        ExitCode = exitCode;
    }
}

