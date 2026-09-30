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
        Console.WriteLine($"  minify level  = {config.ReleaseMinify.Level}");
        Console.WriteLine($"  keepNames     = {config.ReleaseMinify.KeepNames}");
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

        // Branch on the operation mode.
        if (options.Mode == "debug")
        {
            var sessionDir = DebugSession.Run(
                options,
                config,
                esbuildPath);

            Console.WriteLine($"  session dir   = {sessionDir}");

            return 0;
        }

        // Default: build mode.
        // Two esbuild invocations are performed on every build:
        //   - one with BUILD_MODE="DEBUG"
        //   - one with BUILD_MODE="RELEASE"
        // Both payloads are embedded in Bridge.cs and the C# compiler
        // selects the appropriate payload via #if DEBUG / #else.
        var runner = new EsbuildRunner(esbuildPath);

        // Debug payload.
        var jsDebugPath = Path.Combine(
            intermediateDir,
            "typescript-bridge.debug.js");

        runner.Run(
            appEntryPoint,
            jsDebugPath,
            config.TypeScript,
            config.ReleaseMinify,
            "DEBUG",
            options.ProjectDirectory);

        Console.WriteLine($"  debug js      = {jsDebugPath}");

        // Release payload.
        var jsReleasePath = Path.Combine(
            intermediateDir,
            "typescript-bridge.release.js");

        runner.Run(
            appEntryPoint,
            jsReleasePath,
            config.TypeScript,
            config.ReleaseMinify,
            "RELEASE",
            options.ProjectDirectory);

        Console.WriteLine($"  release js    = {jsReleasePath}");

        // Read both payloads and wrap them into a single C# field.
        var jsDebugContent = File.ReadAllText(jsDebugPath);
        var jsReleaseContent = File.ReadAllText(jsReleasePath);

        var csContent = CSharpGenerator.Generate(
            jsDebugContent,
            jsReleaseContent,
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
    // Absolute path to config.json.
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

    // Operation mode.
    // "build"  -> normal Bridge.cs generation (default).
    // "debug"  -> debug session artifacts (js, map, html, server, esproj).
    public string Mode { get; init; } = "build";

    // Session id for debug mode. Required when Mode == "debug".
    public string? SessionId { get; init; }

    // Root directory for debug session artifacts.
    // Required when Mode == "debug".
    public string? DebugOutputRoot { get; init; }

    // Port for the local HTTP debug host. Required when Mode == "debug".
    public int Port { get; init; }

    // Parses the command line arguments into a ToolOptions instance.
    public static ToolOptions Parse(string[] args)
    {
        string? configPath = null;
        string? projectDir = null;
        string? intermediate = null;
        string? esbuildPath = null;
        string? configuration = null;
        string? mode = null;
        string? sessionId = null;
        string? debugOutput = null;
        int port = 0;

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

                case "--mode":
                    mode = RequireValue(
                        args,
                        ref i,
                        "--mode");
                    break;

                case "--session":
                    sessionId = RequireValue(
                        args,
                        ref i,
                        "--session");
                    break;

                case "--debug-output":
                    debugOutput = RequireValue(
                        args,
                        ref i,
                        "--debug-output");
                    break;

                case "--port":
                    var portValue = RequireValue(
                        args,
                        ref i,
                        "--port");

                    if (!int.TryParse(portValue, out port) || port < 1 || port > 65535)
                    {
                        throw new TypescriptBridgeException(
                            ErrorCodes.General,
                            $"Invalid --port value: {portValue}");
                    }
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

        // Default mode is "build".
        if (string.IsNullOrWhiteSpace(mode))
        {
            mode = "build";
        }

        mode = mode.ToLowerInvariant();

        if (mode != "build" && mode != "debug")
        {
            throw new TypescriptBridgeException(
                ErrorCodes.General,
                $"Invalid --mode value: {mode}. Expected 'build' or 'debug'.");
        }

        // Validate debug-mode requirements.
        if (mode == "debug")
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                throw new TypescriptBridgeException(
                    ErrorCodes.General,
                    "Missing required argument for debug mode: --session");
            }

            if (string.IsNullOrWhiteSpace(debugOutput))
            {
                throw new TypescriptBridgeException(
                    ErrorCodes.General,
                    "Missing required argument for debug mode: --debug-output");
            }

            if (port == 0)
            {
                throw new TypescriptBridgeException(
                    ErrorCodes.General,
                    "Missing required argument for debug mode: --port");
            }
        }

        return new ToolOptions
        {
            ConfigPath = Path.GetFullPath(configPath),
            ProjectDirectory = Path.GetFullPath(projectDir),
            IntermediateOutputPath = Path.GetFullPath(intermediate, Path.GetFullPath(projectDir)),
            EsbuildPath = esbuildPath,
            Configuration = configuration,
            Mode = mode,
            SessionId = sessionId,
            DebugOutputRoot = debugOutput is null ? null : Path.GetFullPath(debugOutput),
            Port = port,
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
        Console.WriteLine();
        Console.WriteLine("Debug session mode:");
        Console.WriteLine(
            "  TypescriptBridge.Tool --config <path> --project <path> --intermediate <path> --mode debug --session <id> --debug-output <path> --port <n> [--esbuild <path>]");
    }
}






