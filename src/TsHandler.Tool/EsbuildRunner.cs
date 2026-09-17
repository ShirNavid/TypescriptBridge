namespace TsHandler.Tool;

internal sealed class EsbuildRunner
{
    private readonly string _esbuildPath;

    public EsbuildRunner(string esbuildPath)
    {
        _esbuildPath = esbuildPath;
    }

    public string RunDebug(
        string entryPoint,
        string outputPath,
        TypeScriptConfig ts,
        DebugConfig debug,
        string workingDirectory)
    {
        var args = BuildCommonArgs(entryPoint, outputPath, ts);

        if (debug.SourceMap.Enabled)
        {
            args.Add("--sourcemap=inline");
        }

        // Debug: never minify
        return Run(args, workingDirectory);
    }

    public string RunRelease(
        string entryPoint,
        string outputPath,
        TypeScriptConfig ts,
        ReleaseConfig release,
        string workingDirectory)
    {
        var args = BuildCommonArgs(entryPoint, outputPath, ts);

        AddMinifyArgs(args, release.Minify);

        // Release: no source map (intentional - keeps code opaque)

        return Run(args, workingDirectory);
    }

    private static void AddMinifyArgs(List<string> args, MinifyConfig minify)
    {
        switch (minify.Level)
        {
            case "off":
                // no minification
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
                if (minify.KeepNames)
                    args.Add("--keep-names");
                break;

            default:
                throw new TsHandlerException(
                    ErrorCodes.ConfigValidationFailed,
                    $"release.minify.level: unknown value \"{minify.Level}\".");
        }
    }

    private static List<string> BuildCommonArgs(
        string entryPoint,
        string outputPath,
        TypeScriptConfig ts)
    {
        return new List<string>
        {
            entryPoint,
            "--bundle",
            $"--format={ts.Format}",
            $"--target={ts.Target}",
            $"--outfile={outputPath}",
        };
    }

    private string Run(List<string> args, string workingDirectory)
    {
        ProcessResult result;
        try
        {
            result = ProcessRunner.Run(_esbuildPath, args, workingDirectory);
        }
        catch (Exception ex)
        {
            throw new TsHandlerException(
                ErrorCodes.EsbuildFailed,
                $"Failed to start esbuild: {ex.Message}",
                ex);
        }

        if (result.ExitCode != 0)
        {
            var errorText = string.IsNullOrWhiteSpace(result.StandardError)
                ? result.StandardOutput
                : result.StandardError;

            throw new TsHandlerException(
                ErrorCodes.EsbuildFailed,
                $"esbuild failed with exit code {result.ExitCode}:{Environment.NewLine}{errorText}");
        }

        return result.StandardOutput;
    }
}