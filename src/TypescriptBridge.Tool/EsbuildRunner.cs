namespace TypescriptBridge.Tool;

// Wraps the esbuild binary and translates TypeScriptConfig and ReleaseMinifyConfig
// into the correct command line arguments.
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
    // buildMode is injected as BUILD_MODE via --define so that user code
    // can branch on the build configuration at compile time. The value is
    // expected to be a plain string such as "DEBUG" or "RELEASE".
    public string Run(
        string entryPoint,
        string outputPath,
        TypeScriptConfig ts,
        ReleaseMinifyConfig releaseMinify,
        string buildMode,
        string workingDirectory)
    {
        var args = BuildArgs(
            entryPoint,
            outputPath,
            ts,
            buildMode);

        AddMinifyArgs(args, releaseMinify, buildMode);

        return Execute(args, workingDirectory);
    }

    // Runs esbuild for a debug session.
    //
    // This method produces JavaScript and a linked source map that
    // Visual Studio's existing JavaScript/TypeScript debugger can
    // consume through the .esproj project system.
    //
    // Differences from the normal Run method:
    //   - BUILD_MODE is always "DEBUG"
    //   - no minification is applied
    //   - --keep-names is enabled
    //   - a linked source map is produced
    //   - sourcesContent is embedded in the map
    public string RunDebug(
        string entryPoint,
        string outputPath,
        TypeScriptConfig ts,
        string workingDirectory)
    {
        var args = new List<string>
        {
            entryPoint,
            "--bundle",
            $"--format={ts.Format}",
            $"--target={ts.Target}",
            $"--outfile={outputPath}",

            // Inject the debug build mode as a compile-time constant.
            "--define:BUILD_MODE=\"DEBUG\"",

            // Preserve function and class names for stack traces.
            "--keep-names",

            // Emit a linked source map.
            "--sourcemap=linked",

            // Embed the original TypeScript source in the map.
            // This makes source availability robust.
            "--sources-content=true",
        };

        return Execute(args, workingDirectory);
    }

    // Builds the base esbuild argument list.
    internal static List<string> BuildArgs(
        string entryPoint,
        string outputPath,
        TypeScriptConfig ts,
        string buildMode)
    {
        return new List<string>
        {
            entryPoint,
            "--bundle",
            $"--format={ts.Format}",
            $"--target={ts.Target}",
            $"--outfile={outputPath}",

            // Define BUILD_MODE as a compile-time constant.
            //
            // esbuild will replace every occurrence of the identifier
            // BUILD_MODE in any bundled module with the literal string
            // value, then tree-shake any branches that become unreachable.
            //
            // The inner quotes are part of the esbuild --define syntax:
            // the value must be a JSON-encoded string, so "DEBUG" is
            // written as \"DEBUG\" on the command line.
            $"--define:BUILD_MODE=\"{buildMode}\"",
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




