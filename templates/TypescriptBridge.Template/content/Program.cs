// TypescriptBridge runtime host.
//
// This program runs only when the project is executed directly
// (dotnet run) or as part of the Visual Studio F5 workflow driven by
// the companion .esproj. It is NOT invoked when the project is
// consumed as a library from another C# project.
//
// Responsibilities:
//   1. Locate the project root (the directory that contains config.json).
//   2. Load config.json.
//   3. Resolve and validate the configured browser (run.browser).
//   4. Resolve and validate the configured port (run.port).
//   5. Verify the port is free on loopback.
//   6. Invoke the Tool in --mode debug to generate the session artifacts
//      (app.js, app.js.map, index.html, debug-manifest.json).
//   7. Read debug-manifest.json to find the session directory and port.
//   8. Start an HttpHostServer (from TypescriptBridge.DebugHost).
//   9. If launched by Visual Studio (F5), do not open a browser; the
//      launch.json in the .esproj owns that step. If launched directly
//      (dotnet run), open the configured browser.
//  10. Wait for cancellation (Ctrl+C or process kill).
//  11. Stop the HTTP host and clean up.
//
// Exit codes:
//   0  success
//   1  general error (config not found, tool failed, etc.)
//   2  the configured browser is not installed on this machine
//   3  the configured port is busy

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Runtime.Versioning;
using TypescriptBridge.DebugHost;

// This entire program is Windows-only: it uses HttpListener, Win32
// Job Objects, and browser launch APIs which are not portable.
[assembly: SupportedOSPlatform("windows")]

// Local options object shared by all JsonSerializer calls.
var jsonOptions = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
};

// -----------------------------------------------------------------------------
// 1. Locate the project root.
// -----------------------------------------------------------------------------

var projectRoot = FindProjectRoot(AppContext.BaseDirectory);

if (projectRoot is null)
{
    Console.Error.WriteLine("TypescriptBridge: could not locate project root.");
    Console.Error.WriteLine("Expected config.json and ts/app.ts in a parent directory.");
    return 1;
}

Console.WriteLine($"TypescriptBridge: project root = {projectRoot}");

// -----------------------------------------------------------------------------
// 2. Load config.json (browser and port).
// -----------------------------------------------------------------------------

var configPath = Path.Combine(projectRoot, "config.json");
if (!File.Exists(configPath))
{
    Console.Error.WriteLine($"TypescriptBridge: config.json not found at {configPath}.");
    return 1;
}

var config = JsonSerializer.Deserialize<RunConfig>(File.ReadAllText(configPath), jsonOptions);

if (config?.Run is null)
{
    Console.Error.WriteLine("TypescriptBridge: config.json does not contain a 'run' section.");
    return 1;
}

Console.WriteLine($"TypescriptBridge: run.browser = {config.Run.Browser}");
Console.WriteLine($"TypescriptBridge: run.port = {config.Run.Port}");

// -----------------------------------------------------------------------------
// 3. Validate the configured browser.
// -----------------------------------------------------------------------------

var browser = config.Run.Browser?.ToLowerInvariant() ?? "edge";

if (browser != "edge" && browser != "chrome")
{
    Console.Error.WriteLine($"TypescriptBridge: unknown run.browser value '{browser}'.");
    return 1;
}

if (!BrowserValidator.IsInstalled(browser))
{
    Console.Error.WriteLine($"TypescriptBridge: the configured browser '{browser}' is not installed on this machine.");
    Console.Error.WriteLine("Update run.browser in config.json to a browser that is installed.");
    return 2;
}

// -----------------------------------------------------------------------------
// 4. Validate the configured port.
// -----------------------------------------------------------------------------

var port = config.Run.Port;

if (port < 1 || port > 65535)
{
    Console.Error.WriteLine($"TypescriptBridge: run.port must be between 1 and 65535 (got {port}).");
    return 1;
}

if (!PortAllocator.IsFree(port))
{
    Console.Error.WriteLine($"TypescriptBridge: port {port} is already in use.");
    Console.Error.WriteLine("Close the application that is using it, or change run.port in config.json.");
    return 3;
}

// -----------------------------------------------------------------------------
// 5. Locate the Tool and esbuild.
// -----------------------------------------------------------------------------

var toolDll = Path.Combine(AppContext.BaseDirectory, "tools", "net8.0", "any", "TypescriptBridge.Tool.dll");
var esbuild = Path.Combine(AppContext.BaseDirectory, "tools", "esbuild", "win-x64", "esbuild.exe");

if (!File.Exists(toolDll))
{
    Console.Error.WriteLine($"TypescriptBridge: Tool not found at {toolDll}.");
    Console.Error.WriteLine("The TypescriptBridge.Build package may not have copied its runtime payload.");
    return 1;
}

if (!File.Exists(esbuild))
{
    Console.Error.WriteLine($"TypescriptBridge: esbuild not found at {esbuild}.");
    return 1;
}

// -----------------------------------------------------------------------------
// 6. Invoke the Tool in debug mode.
// -----------------------------------------------------------------------------

var sessionId = "session-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);

var intermediate = Path.Combine(projectRoot, "obj", "TypescriptBridge", "intermediate");
var debugRoot = Path.Combine(projectRoot, "obj", "TypescriptBridge", "debug");

Directory.CreateDirectory(intermediate);
Directory.CreateDirectory(debugRoot);

Console.WriteLine($"TypescriptBridge: starting Tool --mode debug (session {sessionId}, port {port})");

var toolArgs = string.Join(" ", new[]
{
    "exec", Quote(toolDll),
    "--config", Quote(configPath),
    "--project", Quote(projectRoot),
    "--intermediate", Quote(intermediate),
    "--mode", "debug",
    "--session", Quote(sessionId),
    "--debug-output", Quote(debugRoot),
    "--port", port.ToString(System.Globalization.CultureInfo.InvariantCulture),
    "--esbuild", Quote(esbuild),
});

var toolPsi = new ProcessStartInfo
{
    FileName = "dotnet",
    Arguments = toolArgs,
    UseShellExecute = false,
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    CreateNoWindow = true,
    WorkingDirectory = projectRoot,
};

using (var toolProcess = Process.Start(toolPsi)!)
{
    toolProcess.OutputDataReceived += (_, e) => { if (e.Data is not null) Console.WriteLine($"  tool: {e.Data}"); };
    toolProcess.ErrorDataReceived += (_, e) => { if (e.Data is not null) Console.Error.WriteLine($"  tool-err: {e.Data}"); };
    toolProcess.BeginOutputReadLine();
    toolProcess.BeginErrorReadLine();
    toolProcess.WaitForExit();

    if (toolProcess.ExitCode != 0)
    {
        Console.Error.WriteLine($"TypescriptBridge: Tool failed with exit code {toolProcess.ExitCode}.");
        return toolProcess.ExitCode;
    }
}

// -----------------------------------------------------------------------------
// 7. Read the manifest.
// -----------------------------------------------------------------------------

var sessionDir = Path.Combine(debugRoot, sessionId);
var manifestPath = Path.Combine(sessionDir, "debug-manifest.json");

if (!File.Exists(manifestPath))
{
    Console.Error.WriteLine($"TypescriptBridge: debug-manifest.json not found at {manifestPath}.");
    return 1;
}

var manifest = JsonSerializer.Deserialize<DebugManifest>(File.ReadAllText(manifestPath), jsonOptions)
    ?? throw new InvalidOperationException("Failed to read debug-manifest.json.");

Console.WriteLine($"TypescriptBridge: session = {manifest.SessionRoot}");
Console.WriteLine($"TypescriptBridge: URL = http://127.0.0.1:{manifest.Port}/index.html");

// -----------------------------------------------------------------------------
// 8. Start the HTTP server.
// -----------------------------------------------------------------------------

using var httpServer = new HttpHostServer(manifest.SessionRoot, manifest.Port);
httpServer.Start();
Console.WriteLine($"TypescriptBridge: HTTP host listening on http://127.0.0.1:{manifest.Port}/");

// -----------------------------------------------------------------------------
// 9. In direct execution (not from .esproj), open the browser.
//    In the F5 path, launch.json owns the browser launch.
// -----------------------------------------------------------------------------

var isUnderDebugger = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("VSDEBUG"));
BrowserLauncher? browserLauncher = null;

if (!isUnderDebugger)
{
    Console.WriteLine($"TypescriptBridge: launching browser ({config.Run.Browser}).");
    var profileDir = Path.Combine(sessionDir, "browser-profile");

    browserLauncher = new BrowserLauncher();
    browserLauncher.Launch(
        $"http://127.0.0.1:{manifest.Port}/index.html",
        profileDir,
        manifest.Port);
}

// -----------------------------------------------------------------------------
// 10. Wait for cancellation.
// -----------------------------------------------------------------------------

using var lifetime = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    lifetime.Cancel();
};

Console.WriteLine("TypescriptBridge: press Ctrl+C to stop.");

try
{
    await Task.Delay(Timeout.Infinite, lifetime.Token);
}
catch (TaskCanceledException)
{
    // Expected on Ctrl+C.
}

// -----------------------------------------------------------------------------
// 11. Cleanup.
// -----------------------------------------------------------------------------

browserLauncher?.Dispose();
httpServer.Stop();

Console.WriteLine("TypescriptBridge: stopped.");
return 0;

// -----------------------------------------------------------------------------
// Helpers.
// -----------------------------------------------------------------------------

static string? FindProjectRoot(string startDir)
{
    var current = new DirectoryInfo(startDir);
    for (int depth = 0; depth < 10 && current is not null; depth++)
    {
        var configPath = Path.Combine(current.FullName, "config.json");
        var entryPoint = Path.Combine(current.FullName, "ts", "app.ts");

        if (File.Exists(configPath) && File.Exists(entryPoint))
        {
            return current.FullName;
        }

        current = current.Parent;
    }
    return null;
}

static string Quote(string value)
{
    return "\"" + value.Replace("\"", "\\\"") + "\"";
}

// -----------------------------------------------------------------------------
// Config model (only the parts this program needs).
// -----------------------------------------------------------------------------

internal sealed class RunConfig
{
    [JsonPropertyName("run")]
    public RunSection? Run { get; init; }
}

internal sealed class RunSection
{
    [JsonPropertyName("browser")]
    public string Browser { get; init; } = "edge";

    [JsonPropertyName("port")]
    public int Port { get; init; } = 45000;
}

// -----------------------------------------------------------------------------
// Manifest model (mirrors the Tool's manifest shape).
// -----------------------------------------------------------------------------

internal sealed class DebugManifest
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; init; } = "";

    [JsonPropertyName("sessionRoot")]
    public string SessionRoot { get; init; } = "";

    [JsonPropertyName("port")]
    public int Port { get; init; }

    [JsonPropertyName("browser")]
    public string Browser { get; init; } = "edge";
}








