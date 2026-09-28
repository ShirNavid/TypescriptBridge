using TypescriptBridge.Tool;

// Entry point for the tool. All real work happens inside Run so that
// we can keep the top-level statement simple and testable.
return Run(args);

// Runs the whole pipeline: load config, locate esbuild, invoke esbuild
// with BUILD_MODE injected, and write Bridge.cs.
static int Run(string[] args)
{
    ToolOptions options;

    // Parse CLI arguments. If parsing fails, report and exit.
    try
    {
        options = ToolOptions.Parse(args);
    }
    catch (TypescriptBridgeException ex)
    {
        WriteError(ex);
        return ex.ExitCode;
    }

    try
    {
        // Load and validate the configuration file.
        var config = Config.Load(options.ConfigPath);

        Console.WriteLine($"TypescriptBridge: config loaded from {options.ConfigPath}");
        Console.WriteLine($"  class         = {config.Output.ClassName}");
        Console.WriteLine($"  field         = {config.Output.FieldName}");
        Console.WriteLine($"  target        = {config.TypeScript.Target}");
        Console.WriteLine($"  format        = {config.TypeScript.Format}");
        Console.WriteLine($"  minify level  = {config.Minify.Level}");
        Console.WriteLine($"  keepNames     = {config.Minify.KeepNames}");
        Console.WriteLine($"  configuration = {options.Configuration}");

        // Locate the esbuild binary using the standard lookup chain.
        var esbuildPath = EsbuildLocator.Locate(options.EsbuildPath);
        Console.WriteLine($"  esbuild       = {esbuildPath}");

        // The user's TypeScript entry point.
        var appEntryPoint = Path.Combine(
            options.ProjectDirectory,
            "ts",
            "app.ts");

        if (!File.Exists(appEntryPoint))
        {
            throw new TypescriptBridgeException(
                ErrorCodes.EntryPointNotFound,
                $"Entry point not found: {appEntryPoint}");
        }

        // Ensure the intermediate directory exists.
        var intermediateDir = options.IntermediateOutputPath;
        Directory.CreateDirectory(intermediateDir);

        // Normalize the build mode to upper case so that user code can
        // always compare against "DEBUG" or "RELEASE" regardless of the
        // casing used in MSBuild (Debug vs debug).
        var buildMode = options.Configuration.ToUpperInvariant();

        Console.WriteLine($"  build mode    = {buildMode}");

        // Path where esbuild writes the bundled JavaScript.
        var jsPath = Path.Combine(
            intermediateDir,
            "typescript-bridge.js");

        // Run esbuild on the user's entry point. BUILD_MODE is injected
        // via --define inside EsbuildRunner as a compile-time constant.
        var runner = new EsbuildRunner(esbuildPath);

        runner.Run(
            appEntryPoint,
            jsPath,
            config.TypeScript,
            config.Minify,
            buildMode,
            options.ProjectDirectory);

        Console.WriteLine($"  javascript    = {jsPath}");

        // Read the bundled JavaScript and wrap it into a C# field.
        var jsContent = File.ReadAllText(jsPath);

        var csContent = CSharpGenerator.Generate(
            jsContent,
            config.Output);

        // Write Bridge.cs next to the project file, only if content changed.
        var csPath = Path.Combine(
            options.ProjectDirectory,
            "Bridge.cs");

        FileWriter.WriteIfChanged(
            csPath,
            csContent);

        Console.WriteLine($"  cs            = {csPath}");

        return 0;
    }
    catch (TypescriptBridgeException ex)
    {
        WriteError(ex);
        return ex.ExitCode;
    }
    catch (Exception ex)
    {
        // Wrap unexpected exceptions into a bridge exception so that
        // the caller always sees a stable error code.
        WriteError(
            new TypescriptBridgeException(
                ErrorCodes.General,
                ex.Message,
                ex));

        return 1;
    }
}

// Writes an error to stderr using the standard bridge format.
static void WriteError(TypescriptBridgeException ex)
{
    Console.Error.WriteLine(
        $"error {ex.Code}: {ex.Message}");
}

// Holds all CLI options parsed from the command line.
internal sealed class ToolOptions
{
    // Absolute path to typescript-bridge.json.
    public required string ConfigPath { get; init; }

    // Absolute path to the project directory containing ts/app.ts.
    public required string ProjectDirectory { get; init; }

    // Absolute path to the intermediate output directory.
    public required string IntermediateOutputPath { get; init; }

    // Optional explicit path to the esbuild binary.
    public string? EsbuildPath { get; init; }

    // Build configuration name (Debug, Release, ...).
    // Defaults to "Debug" when not supplied, so that manual tool runs
    // still produce a deterministic BUILD_MODE value.
    public string Configuration { get; init; } = "Debug";

    // Parses the command line arguments into a ToolOptions instance.
    public static ToolOptions Parse(string[] args)
    {
        string? configPath = null;
        string? projectDir = null;
        string? intermediate = null;
        string? esbuildPath = null;
        string? configuration = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--config":
                    configPath = RequireValue(
                        args,
                        ref i,
                        "--config");
                    break;

                case "--project":
                    projectDir = RequireValue(
                        args,
                        ref i,
                        "--project");
                    break;

                case "--intermediate":
                    intermediate = RequireValue(
                        args,
                        ref i,
                        "--intermediate");
                    break;

                case "--esbuild":
                    esbuildPath = RequireValue(
                        args,
                        ref i,
                        "--esbuild");
                    break;

                case "--configuration":
                    configuration = RequireValue(
                        args,
                        ref i,
                        "--configuration");
                    break;

                case "--help":
                case "-h":
                    PrintHelp();
                    Environment.Exit(0);
                    break;

                default:
                    throw new TypescriptBridgeException(
                        ErrorCodes.General,
                        $"Unknown argument: {args[i]}");
            }
        }

        // Validate required arguments.
        if (configPath is null)
        {
            throw new TypescriptBridgeException(
                ErrorCodes.General,
                "Missing required argument: --config");
        }

        if (projectDir is null)
        {
            throw new TypescriptBridgeException(
                ErrorCodes.General,
                "Missing required argument: --project");
        }

        if (intermediate is null)
        {
            throw new TypescriptBridgeException(
                ErrorCodes.General,
                "Missing required argument: --intermediate");
        }

        // Apply the default configuration if it was not provided.
        // This keeps manual tool invocations working without extra flags.
        if (string.IsNullOrWhiteSpace(configuration))
        {
            configuration = "Debug";
        }

        return new ToolOptions
        {
            ConfigPath = Path.GetFullPath(configPath),
            ProjectDirectory = Path.GetFullPath(projectDir),
            IntermediateOutputPath = Path.GetFullPath(intermediate, Path.GetFullPath(projectDir)),
            EsbuildPath = esbuildPath,
            Configuration = configuration,
        };
    }

    // Reads the next argument value or throws if it is missing.
    private static string RequireValue(
        string[] args,
        ref int i,
        string name)
    {
        if (i + 1 >= args.Length)
        {
            throw new TypescriptBridgeException(
                ErrorCodes.General,
                $"Missing value for {name}");
        }

        i++;

        return args[i];
    }

    // Prints CLI usage information.
    private static void PrintHelp()
    {
        Console.WriteLine("TypescriptBridge.Tool");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine(
            "  TypescriptBridge.Tool --config <path> --project <path> --intermediate <path> [--esbuild <path>] [--configuration <name>]");
    }
}
