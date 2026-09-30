using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TypescriptBridge.Vsix.Debug
{
    // Runs TypescriptBridge.Tool in --mode debug and returns the path
    // to the session directory the Tool produced.
    internal static class ToolRunner
    {
        private const string ToolRelativePath = @"tools\net8.0\any\TypescriptBridge.Tool.dll";
        private const string EsbuildRelativePath = @"tools\esbuild\win-x64\esbuild.exe";

        public static async Task<string> RunDebugAsync(
            string projectRoot,
            string sessionId,
            int port,
            CancellationToken cancellationToken)
        {
            var vsixDir = Path.GetDirectoryName(typeof(ToolRunner).Assembly.Location)
                ?? throw new InvalidOperationException("Cannot determine VSIX directory.");

            var toolDll = Path.Combine(vsixDir, ToolRelativePath);
            if (!File.Exists(toolDll))
            {
                throw new FileNotFoundException(
                    $"TypescriptBridge.Tool.dll not found at {toolDll}.", toolDll);
            }

            var esbuild = Path.Combine(vsixDir, EsbuildRelativePath);
            if (!File.Exists(esbuild))
            {
                throw new FileNotFoundException(
                    $"esbuild.exe not found at {esbuild}.", esbuild);
            }

            var configPath = Path.Combine(projectRoot, "config.json");
            var intermediate = Path.Combine(projectRoot, "obj", "TypescriptBridge", "intermediate");
            var debugRoot = Path.Combine(projectRoot, "obj", "TypescriptBridge", "debug");

            Directory.CreateDirectory(intermediate);
            Directory.CreateDirectory(debugRoot);

            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = projectRoot,
            };

            psi.Arguments = string.Join(" ", new[]
            {
                "exec",
                Quote(toolDll),
                "--config", Quote(configPath),
                "--project", Quote(projectRoot),
                "--intermediate", Quote(intermediate),
                "--mode", "debug",
                "--session", Quote(sessionId),
                "--debug-output", Quote(debugRoot),
                "--port", port.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "--esbuild", Quote(esbuild),
            });

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();

            using var process = new Process { StartInfo = psi };
            process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await Task.Run(() => process.WaitForExit(), cancellationToken).ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"TypescriptBridge.Tool failed with exit code {process.ExitCode}.\n" +
                    $"stdout:\n{stdout}\nstderr:\n{stderr}");
            }

            var sessionDir = Path.Combine(debugRoot, sessionId);
            var manifestPath = Path.Combine(sessionDir, "debug-manifest.json");

            if (!File.Exists(manifestPath))
            {
                throw new InvalidOperationException(
                    $"Tool succeeded but debug-manifest.json was not produced at {manifestPath}.");
            }

            return sessionDir;
        }

        // Quotes a single command-line argument for CreateProcess.
        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }
    }
}
