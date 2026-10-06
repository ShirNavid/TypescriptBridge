namespace TypescriptBridge.Tool;

// Wraps the esbuild binary and translates BundleConfig and
// ReleaseMinifyConfig into the correct command line arguments.
//
// The TypeScript target is NOT obtained from Config. It is passed in
// explicitly by the caller, which reads it from tsconfig.json
// (compilerOptions.target). This keeps tsconfig.json as the single
// source of truth for TypeScript compiler semantics.
internal sealed class EsbuildRunner
{
    // Absolute path to the esbuild binary that will be invoked.
    private readonly string _esbuildPath;

    // Creates a runner bound to a specific esbuild binary.
    public EsbuildRunner(string esbuildPath)
    {
        _esbuildPath = esbuildPath;
    }

    // Runs esbuild against the given entry point and writes the bundled
    // JavaScript to outputPath.
    //
    // buildMode is injected into the imported BUILD_MODE value via --define so that user code
    // can branch on the build configuration at compile time. The value is
    // expected to be a plain string such as "DEBUG" or "RELEASE".
    public string Run(
        string entryPoint,
        string outputPath,
        string target,
        string format,
        ReleaseMinifyConfig releaseMinify,
        string buildMode,
        string projectStatus,
        bool isTestProject,
        string workingDirectory)
    {
        var args = BuildArgs(
            entryPoint,
            outputPath,
            target,
            format,
            buildMode,
            projectStatus,
            isTestProject);

        AddMinifyArgs(args, releaseMinify, buildMode);

        return Execute(args, workingDirectory);
    }

    internal static List<string> BuildArgs(
        string entryPoint,
        string outputPath,
        string target,
        string format,
        string buildMode)
        => BuildArgs(entryPoint, outputPath, target, format, buildMode, "Library", false);

    // Builds the base esbuild argument list.
    internal static List<string> BuildArgs(
        string entryPoint,
        string outputPath,
        string target,
        string format,
        string buildMode,
        string projectStatus,
        bool isTestProject)
    {
        return new List<string>
        {
            entryPoint,
            "--bundle",
            $"--format={format}",
            $"--target={target}",
            $"--outfile={outputPath}",

            // Define the module placeholders as compile-time constants.
            //
            // esbuild will replace each placeholder identifier in the imported module.
            // The BUILD_MODE placeholder is replaced with the literal string
            // value, then tree-shake any branches that become unreachable.
            //
            // The inner quotes are part of the esbuild --define syntax:
            // the value must be a JSON-encoded string, so "DEBUG" is
            // written as \"DEBUG\" on the command line.
            $"--define:__TYPESCRIPT_BRIDGE_BUILD_MODE__=\"{buildMode}\"",
            $"--define:__TYPESCRIPT_BRIDGE_PROJECT_STATUS__=\"{projectStatus}\"",
            $"--define:__TYPESCRIPT_BRIDGE_IS_TEST_PROJECT__={(isTestProject ? "true" : "false")}",
        };
    }

    // Adds minification flags according to the configured level.
    internal static void AddMinifyArgs(
        List<string> args,
        ReleaseMinifyConfig releaseMinify,
        string buildMode)
    {
        switch (buildMode == "DEBUG" ? "off" : releaseMinify.Level)
        {
            case "off":
                break;

            case "light":
                args.Add("--minify-whitespace");
                break;

            case "full":
                args.Add("--minify-whitespace");
                args.Add("--minify-syntax");
                break;

            case "aggressive":
                args.Add("--minify");

                if (releaseMinify.KeepNames)
                    args.Add("--keep-names");

                break;

            default:
                throw new TypescriptBridgeException(
                    ErrorCodes.ConfigValidationFailed,
                    $"release-minify.level: unknown value \"{releaseMinify.Level}\".");
        }
    }

    // Starts esbuild, waits for it to finish, and returns its stdout.
    // Any non-zero exit code is reported as an EsbuildFailed error.
    private string Execute(
        List<string> args,
        string workingDirectory)
    {
        ProcessResult result;

        try
        {
            result = ProcessRunner.Run(
                _esbuildPath,
                args,
                workingDirectory);
        }
        catch (Exception ex)
        {
            throw new TypescriptBridgeException(
                ErrorCodes.EsbuildFailed,
                $"Failed to start esbuild: {ex.Message}",
                ex);
        }

        if (result.ExitCode != 0)
        {
            var errorText = string.IsNullOrWhiteSpace(result.StandardError)
                ? result.StandardOutput
                : result.StandardError;

            throw new TypescriptBridgeException(
                ErrorCodes.EsbuildFailed,
                $"esbuild failed with exit code {result.ExitCode}:{Environment.NewLine}{errorText}");
        }

        return result.StandardOutput;
    }
}
