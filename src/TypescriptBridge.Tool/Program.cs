using System.Text.Json;
using TypescriptBridge.Tool;

// Entry point for the tool. All real work happens inside Run so that
// we can keep the top-level statement simple and testable.
return Run(args);

// Runs the whole pipeline: load config, read tsconfig.json, locate
// esbuild, invoke esbuild with BUILD_MODE injected, and write Bridge.cs.
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

        // Load tsconfig.json and read compilerOptions.target.
        var tsconfigPath = Path.Combine(
            options.ProjectDirectory,
            "ts",
            "tsconfig.json");

        var target = TsConfigReader.ReadTarget(tsconfigPath);

        Console.WriteLine($"TypescriptBridge: config loaded from {options.ConfigPath}");
        Console.WriteLine($"  class         = {config.Output.ClassName}");
        Console.WriteLine($"  field         = {config.Output.FieldName}");
        Console.WriteLine($"  target        = {target} (from tsconfig.json)");
        Console.WriteLine($"  format        = {config.Bundle.Format}");
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
            "src",
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
            target,
            config.Bundle.Format,
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
            target,
            config.Bundle.Format,
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
            config.Output,
            options.RootNamespace);

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

    // The namespace of the generated C# provider (normally RootNamespace).
    public string? RootNamespace { get; init; }

    // Parses the command line arguments into a ToolOptions instance.
    public static ToolOptions Parse(string[] args)
    {
        string? configPath = null;
        string? projectDir = null;
        string? intermediate = null;
        string? esbuildPath = null;
        string? configuration = null;
        string? rootNamespace = null;

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

                case "--namespace":
                    rootNamespace = RequireValue(
                        args,
                        ref i,
                        "--namespace");
                    break;

                // Reject arguments that belonged to the removed debug mode.
                case "--mode":
                case "--session":
                case "--debug-output":
                case "--port":
                    throw new TypescriptBridgeException(
                        ErrorCodes.General,
                        $"Argument {args[i]} is no longer supported. " +
                        "The Tool has been simplified to build-only mode. " +
                        "Debug artifact generation is now handled by server.js.");

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
            RootNamespace = rootNamespace,
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
            "  TypescriptBridge.Tool --config <path> --project <path> --intermediate <path> [--esbuild <path>] [--configuration <name>] [--namespace <name>]");
        Console.WriteLine();
        Console.WriteLine("The Tool has no debug mode. Debug artifact generation is handled by server.js in the generated project.");
    }
}

// Reads compilerOptions.target from tsconfig.json.
// Hard-fails when the file is missing or the property is missing.
internal static class TsConfigReader
{
    public static string ReadTarget(string tsconfigPath)
    {
        if (!File.Exists(tsconfigPath))
        {
            throw new TypescriptBridgeException(
                ErrorCodes.ConfigValidationFailed,
                $"tsconfig.json not found: {tsconfigPath}");
        }

        string json;

        try
        {
            json = File.ReadAllText(tsconfigPath);
        }
        catch (IOException ex)
        {
            throw new TypescriptBridgeException(
                ErrorCodes.ConfigInvalidJson,
                $"Failed to read tsconfig.json: {ex.Message}");
        }

        JsonDocument doc;

        try
        {
            doc = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });
        }
        catch (JsonException ex)
        {
            throw new TypescriptBridgeException(
                ErrorCodes.ConfigInvalidJson,
                $"Invalid JSON in tsconfig.json: {ex.Message}");
        }

        using (doc)
        {
            if (!doc.RootElement.TryGetProperty("compilerOptions", out var compilerOptions))
            {
                throw new TypescriptBridgeException(
                    ErrorCodes.ConfigValidationFailed,
                    "tsconfig.json: missing \"compilerOptions\" object.");
            }

            if (!compilerOptions.TryGetProperty("target", out var target))
            {
                throw new TypescriptBridgeException(
                    ErrorCodes.ConfigValidationFailed,
                    "tsconfig.json: missing \"compilerOptions.target\".");
            }

            if (target.ValueKind != JsonValueKind.String)
            {
                throw new TypescriptBridgeException(
                    ErrorCodes.ConfigValidationFailed,
                    "tsconfig.json: \"compilerOptions.target\" must be a string.");
            }

            var value = target.GetString();

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new TypescriptBridgeException(
                    ErrorCodes.ConfigValidationFailed,
                    "tsconfig.json: \"compilerOptions.target\" must not be empty.");
            }

            return value!;
        }
    }
}

