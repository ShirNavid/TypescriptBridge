using TsHandler.Tool;

return Run(args);

static int Run(string[] args)
{
    ToolOptions options;
    try
    {
        options = ToolOptions.Parse(args);
    }
    catch (TsHandlerException ex)
    {
        WriteError(ex);
        return ex.ExitCode;
    }

    try
    {
        var config = Config.Load(options.ConfigPath);

        Console.WriteLine($"TsHandler: config loaded from {options.ConfigPath}");
        Console.WriteLine($"  target        = {config.TypeScript.Target}");
        Console.WriteLine($"  format        = {config.TypeScript.Format}");
        Console.WriteLine($"  minify level  = {config.Release.Minify.Level}");
        Console.WriteLine($"  keepNames     = {config.Release.Minify.KeepNames}");
        Console.WriteLine($"  configuration = {options.Configuration}");

        var esbuildPath = EsbuildLocator.Locate(options.EsbuildPath);
        Console.WriteLine($"  esbuild       = {esbuildPath}");

        var runner = new EsbuildRunner(esbuildPath);

        var entryPoint = Path.Combine(options.ProjectDirectory, "ts", "app.ts");
        if (!File.Exists(entryPoint))
        {
            throw new TsHandlerException(
                ErrorCodes.EntryPointNotFound,
                $"Entry point not found: {entryPoint}");
        }

        var intermediateDir = options.IntermediateOutputPath;
        Directory.CreateDirectory(intermediateDir);
        var debugJs = Path.Combine(intermediateDir, "debug.js");
        var releaseJs = Path.Combine(intermediateDir, "release.js");

        runner.RunDebug(entryPoint, debugJs, config.TypeScript, config.Debug, options.ProjectDirectory);
        Console.WriteLine($"  debug.js      = {debugJs}");

        runner.RunRelease(entryPoint, releaseJs, config.TypeScript, config.Release, options.ProjectDirectory);
        Console.WriteLine($"  release.js    = {releaseJs}");

        var debugContent = File.ReadAllText(debugJs);
        var releaseContent = File.ReadAllText(releaseJs);

        var csContent = CSharpGenerator.Generate(debugContent, releaseContent);
        var csPath = Path.Combine(options.ProjectDirectory, Config.GeneratedClassName + ".cs");
        FileWriter.WriteIfChanged(csPath, csContent);
        Console.WriteLine($"  cs            = {csPath}");

        return 0;
    }
    catch (TsHandlerException ex)
    {
        WriteError(ex);
        return ex.ExitCode;
    }
    catch (Exception ex)
    {
        WriteError(new TsHandlerException(ErrorCodes.General, ex.Message, ex));
        return 1;
    }
}

static void WriteError(TsHandlerException ex)
{
    Console.Error.WriteLine($"error {ex.Code}: {ex.Message}");
}

internal sealed class ToolOptions
{
    public required string ConfigPath { get; init; }
    public required string ProjectDirectory { get; init; }
    public required string IntermediateOutputPath { get; init; }
    public required string Configuration { get; init; }
    public string? EsbuildPath { get; init; }

    public static ToolOptions Parse(string[] args)
    {
        string? configPath = null;
        string? projectDir = null;
        string? intermediate = null;
        string? configuration = null;
        string? esbuildPath = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--config":
                    configPath = RequireValue(args, ref i, "--config");
                    break;
                case "--project":
                    projectDir = RequireValue(args, ref i, "--project");
                    break;
                case "--intermediate":
                    intermediate = RequireValue(args, ref i, "--intermediate");
                    break;
                case "--configuration":
                    configuration = RequireValue(args, ref i, "--configuration");
                    break;
                case "--esbuild":
                    esbuildPath = RequireValue(args, ref i, "--esbuild");
                    break;
                case "--help":
                case "-h":
                    PrintHelp();
                    Environment.Exit(0);
                    break;
                default:
                    throw new TsHandlerException(
                        ErrorCodes.General,
                        $"Unknown argument: {args[i]}");
            }
        }

        if (configPath is null)
            throw new TsHandlerException(ErrorCodes.General, "Missing required argument: --config");
        if (projectDir is null)
            throw new TsHandlerException(ErrorCodes.General, "Missing required argument: --project");
        if (intermediate is null)
            throw new TsHandlerException(ErrorCodes.General, "Missing required argument: --intermediate");
        if (configuration is null)
            throw new TsHandlerException(ErrorCodes.General, "Missing required argument: --configuration");

        if (configuration != "Debug" && configuration != "Release")
        {
            throw new TsHandlerException(
                ErrorCodes.General,
                $"Invalid --configuration value: '{configuration}'. Expected 'Debug' or 'Release'.");
        }

        return new ToolOptions
        {
            ConfigPath = Path.GetFullPath(configPath),
            ProjectDirectory = Path.GetFullPath(projectDir),
            IntermediateOutputPath = Path.GetFullPath(intermediate),
            Configuration = configuration,
            EsbuildPath = esbuildPath,
        };
    }

    private static string RequireValue(string[] args, ref int i, string name)
    {
        if (i + 1 >= args.Length)
            throw new TsHandlerException(ErrorCodes.General, $"Missing value for {name}");
        i++;
        return args[i];
    }

    private static void PrintHelp()
    {
        Console.WriteLine("TsHandler.Tool");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  TsHandler.Tool --config <path> --project <path> --intermediate <path> --configuration <Debug|Release> [--esbuild <path>]");
    }
}