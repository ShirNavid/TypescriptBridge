using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TypescriptBridge.Tool;

// Orchestrates a single debug session.
// Produces all artifacts required by Visual Studio's existing
// JavaScript/TypeScript debugger through the .esproj project system.
internal static class DebugSession
{
    // Runs the full debug session artifact generation.
    public static string Run(
        ToolOptions options,
        Config config,
        string esbuildPath)
    {
        if (options.SessionId is null || options.DebugOutputRoot is null)
        {
            throw new TypescriptBridgeException(
                ErrorCodes.General,
                "Debug session options are not set.");
        }

        // Compute the session directory.
        var sessionDir = Path.Combine(
            options.DebugOutputRoot,
            options.SessionId);

        // Ensure a clean session directory.
        if (Directory.Exists(sessionDir))
        {
            Directory.Delete(sessionDir, recursive: true);
        }

        Directory.CreateDirectory(sessionDir);

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

        // Output paths inside the session directory.
        var debugJsPath = Path.Combine(sessionDir, "debug.js");
        var debugMapPath = Path.Combine(sessionDir, "debug.js.map");
        var htmlPath = Path.Combine(sessionDir, "index.html");
        var serverPath = Path.Combine(sessionDir, "server.js");
        var esprojPath = Path.Combine(sessionDir, "TypescriptDebug.esproj");
        var vsCodeDir = Path.Combine(sessionDir, ".vscode");
        var launchJsonPath = Path.Combine(vsCodeDir, "launch.json");
        var manifestPath = Path.Combine(sessionDir, "debug-manifest.json");

        Directory.CreateDirectory(vsCodeDir);

        // Compile the TypeScript with source map.
        var runner = new EsbuildRunner(esbuildPath);

        runner.RunDebug(
            appEntryPoint,
            debugJsPath,
            config.TypeScript,
            options.ProjectDirectory);

        // Verify the generated artifacts.
        if (!File.Exists(debugJsPath))
        {
            throw new TypescriptBridgeException(
                ErrorCodes.EsbuildFailed,
                $"esbuild did not produce expected output: {debugJsPath}");
        }

        if (!File.Exists(debugMapPath))
        {
            throw new TypescriptBridgeException(
                ErrorCodes.EsbuildFailed,
                $"esbuild did not produce expected source map: {debugMapPath}");
        }

        // Produce the HTML host.
        File.WriteAllText(
            htmlPath,
            BuildHtml(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        // Produce the HTTP server script.
        File.WriteAllText(
            serverPath,
            BuildServerJs(options.Port),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        // Produce the .esproj file.
        File.WriteAllText(
            esprojPath,
            BuildEsproj(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        // Produce the .vscode/launch.json file.
        File.WriteAllText(
            launchJsonPath,
            BuildLaunchJson(options.Port),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        // Produce the debug manifest.
        var manifest = new DebugManifest
        {
            SessionId = options.SessionId,
            ProjectRoot = options.ProjectDirectory,
            SessionRoot = sessionDir,
            JavaScript = debugJsPath,
            SourceMap = debugMapPath,
            Html = htmlPath,
            ServerScript = serverPath,
            Esproj = esprojPath,
            LaunchJson = launchJsonPath,
            Port = options.Port,
            Format = config.TypeScript.Format,
            Configuration = options.Configuration,
        };

        File.WriteAllText(
            manifestPath,
            JsonSerializer.Serialize(
                manifest,
                new JsonSerializerOptions { WriteIndented = true }),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        return sessionDir;
    }

    // Produces a minimal HTML host that loads the compiled JavaScript.
    private static string BuildHtml()
    {
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html>").Append('\n');
        sb.Append("<html>").Append('\n');
        sb.Append("<head>").Append('\n');
        sb.Append("    <meta charset=\"utf-8\" />").Append('\n');
        sb.Append("    <title>TypescriptBridge Debug</title>").Append('\n');
        sb.Append("</head>").Append('\n');
        sb.Append("<body>").Append('\n');
        sb.Append("    <h1>TypescriptBridge Debug Session</h1>").Append('\n');
        sb.Append("    <script src=\"debug.js\"></script>").Append('\n');
        sb.Append("</body>").Append('\n');
        sb.Append("</html>").Append('\n');
        return sb.ToString();
    }

    // Produces the Node.js HTTP static server.
    // The server is intentionally tiny:
    //   - binds to loopback only
    //   - serves only files inside the session directory
    //   - rejects path traversal
    private static string BuildServerJs(int port)
    {
        var sb = new StringBuilder();
        sb.Append("// TypescriptBridge debug session HTTP server.").Append('\n');
        sb.Append("// Node.js built-in modules only. No dependencies.").Append('\n');
        sb.Append('\n');
        sb.Append("const http = require(\"http\");").Append('\n');
        sb.Append("const fs = require(\"fs\");").Append('\n');
        sb.Append("const path = require(\"path\");").Append('\n');
        sb.Append('\n');
        sb.Append("const root = __dirname;").Append('\n');
        sb.Append($"const port = {port};").Append('\n');
        sb.Append('\n');
        sb.Append("const mimeTypes = {").Append('\n');
        sb.Append("    \".html\": \"text/html; charset=utf-8\",").Append('\n');
        sb.Append("    \".js\":   \"application/javascript; charset=utf-8\",").Append('\n');
        sb.Append("    \".map\":  \"application/json; charset=utf-8\",").Append('\n');
        sb.Append("    \".ts\":   \"application/typescript; charset=utf-8\",").Append('\n');
        sb.Append("    \".json\": \"application/json; charset=utf-8\",").Append('\n');
        sb.Append("};").Append('\n');
        sb.Append('\n');
        sb.Append("const server = http.createServer((req, res) => {").Append('\n');
        sb.Append("    let urlPath = decodeURIComponent(req.url.split(\"?\")[0]);").Append('\n');
        sb.Append("    if (urlPath === \"/\") {").Append('\n');
        sb.Append("        urlPath = \"/index.html\";").Append('\n');
        sb.Append("    }").Append('\n');
        sb.Append('\n');
        sb.Append("    const requested = path.resolve(root, \".\" + urlPath);").Append('\n');
        sb.Append('\n');
        sb.Append("    // Prevent path traversal. The resolved path must be inside the").Append('\n');
        sb.Append("    // session root directory (or equal to it).").Append('\n');
        sb.Append("    if (requested !== root && !requested.startsWith(root + path.sep)) {").Append('\n');
        sb.Append("        res.writeHead(403);").Append('\n');
        sb.Append("        res.end(\"Forbidden\");").Append('\n');
        sb.Append("        return;").Append('\n');
        sb.Append("    }").Append('\n');
        sb.Append('\n');
        sb.Append("    fs.readFile(requested, (err, data) => {").Append('\n');
        sb.Append("        if (err) {").Append('\n');
        sb.Append("            res.writeHead(404);").Append('\n');
        sb.Append("            res.end(\"Not found\");").Append('\n');
        sb.Append("            return;").Append('\n');
        sb.Append("        }").Append('\n');
        sb.Append("        const ext = path.extname(filePath).toLowerCase();").Append('\n');
        sb.Append("        const mime = mimeTypes[ext] || \"application/octet-stream\";").Append('\n');
        sb.Append("        res.writeHead(200, { \"Content-Type\": mime });").Append('\n');
        sb.Append("        res.end(data);").Append('\n');
        sb.Append("    });").Append('\n');
        sb.Append("});").Append('\n');
        sb.Append('\n');
        sb.Append("server.listen(port, \"127.0.0.1\", () => {").Append('\n');
        sb.Append("    console.log(\"TypescriptBridge debug server listening on http://127.0.0.1:\" + port + \"/\");").Append('\n');
        sb.Append("});").Append('\n');
        return sb.ToString();
    }

    // Produces the .esproj file that Visual Studio will open.
    // The SDK version must match the installed Visual Studio JavaScript
    // Project System SDK. Currently 1.0.6165494 on the tested environment.
    private static string BuildEsproj()
    {
        var sb = new StringBuilder();
        sb.Append("<Project Sdk=\"Microsoft.VisualStudio.JavaScript.Sdk/1.0.6165494\">").Append('\n');
        sb.Append("  <PropertyGroup>").Append('\n');
        sb.Append("    <ShouldRunNpmInstall>false</ShouldRunNpmInstall>").Append('\n');
        sb.Append("    <ShouldRunBuildScript>false</ShouldRunBuildScript>").Append('\n');
        sb.Append("    <StartupCommand>node \"$(MSBuildProjectDirectory)\\server.js\"</StartupCommand>").Append('\n');
        sb.Append("  </PropertyGroup>").Append('\n');
        sb.Append("</Project>").Append('\n');
        return sb.ToString();
    }

    // Produces the .vscode/launch.json file that tells Visual Studio
    // how to launch Edge and attach the JavaScript debugger.
    private static string BuildLaunchJson(int port)
    {
        var sb = new StringBuilder();
        sb.Append("{").Append('\n');
        sb.Append("  \"version\": \"0.2.0\",").Append('\n');
        sb.Append("  \"configurations\": [").Append('\n');
        sb.Append("    {").Append('\n');
        sb.Append("      \"name\": \"TypescriptBridge Debug (Edge)\",").Append('\n');
        sb.Append("      \"type\": \"edge\",").Append('\n');
        sb.Append("      \"request\": \"launch\",").Append('\n');
        sb.Append($"      \"url\": \"http://127.0.0.1:{port}/index.html\",").Append('\n');
        sb.Append("      \"webRoot\": \"${workspaceFolder}\"").Append('\n');
        sb.Append("    }").Append('\n');
        sb.Append("  ]").Append('\n');
        sb.Append("}").Append('\n');
        return sb.ToString();
    }
}

// Manifest that describes the produced debug artifacts.
// Written to debug-manifest.json inside the session directory.
internal sealed class DebugManifest
{
    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }

    [JsonPropertyName("projectRoot")]
    public required string ProjectRoot { get; init; }

    [JsonPropertyName("sessionRoot")]
    public required string SessionRoot { get; init; }

    [JsonPropertyName("javascript")]
    public required string JavaScript { get; init; }

    [JsonPropertyName("sourceMap")]
    public required string SourceMap { get; init; }

    [JsonPropertyName("html")]
    public required string Html { get; init; }

    [JsonPropertyName("serverScript")]
    public required string ServerScript { get; init; }

    [JsonPropertyName("esproj")]
    public required string Esproj { get; init; }

    [JsonPropertyName("launchJson")]
    public required string LaunchJson { get; init; }

    [JsonPropertyName("port")]
    public required int Port { get; init; }

    [JsonPropertyName("format")]
    public required string Format { get; init; }

    [JsonPropertyName("configuration")]
    public required string Configuration { get; init; }
}

