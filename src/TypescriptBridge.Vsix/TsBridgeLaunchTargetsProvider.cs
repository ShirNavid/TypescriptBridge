using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Debug;
using Microsoft.VisualStudio.ProjectSystem.VS.Debug;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace TypescriptBridge.Vsix
{
    // Launch provider for the typescript-bridge command name.
    //
    // Iteration 10: we bypass the launch-profile routing and call
    // IVsDebugger4.LaunchDebugTargets4 directly. Visual Studio's own
    // DebugLaunchProviderBase does exactly this. By doing it ourselves
    // we avoid the managed CoreCLR routing that occurs when Visual
    // Studio sees a C# class library project.
    [Export(typeof(IDebugProfileLaunchTargetsProvider))]
    [AppliesTo("LaunchProfiles")]
    [Order(50)]
    internal sealed class TsBridgeLaunchTargetsProvider : IDebugProfileLaunchTargetsProvider, IVsDebuggerEvents
    {
        private readonly ConfiguredProject _project;
        private readonly TsBridgeServerLauncher _launcher = new TsBridgeServerLauncher();
        private IVsDebugger? _debugger;
        private uint _debuggerEventsCookie;

        // The VS service provider. Used to obtain the shell debugger.
        [Import]
        internal SVsServiceProvider? ServiceProvider { get; set; }

        // The VsHierarchy of the current project.
        [ImportMany("Microsoft.VisualStudio.ProjectSystem.Microsoft.VisualStudio.Shell.Interop.IVsHierarchy")]
        private IEnumerable<Lazy<IVsHierarchy, IOrderPrecedenceMetadataView>>? VsHierarchies { get; set; }

        [ImportingConstructor]
        public TsBridgeLaunchTargetsProvider(ConfiguredProject project)
        {
            _project = project;
        }

        private IVsHierarchy? GetVsHierarchy()
        {
            try
            {
                if (VsHierarchies == null) return null;
                return VsHierarchies.FirstOrDefault()?.Value;
            }
            catch
            {
                return null;
            }
        }

        public bool SupportsProfile(ILaunchProfile profile)
        {
            if (profile == null)
            {
                return false;
            }

            var matches = string.Equals(
                profile.CommandName,
                TsBridgeConstants.CommandName,
                StringComparison.OrdinalIgnoreCase);

            TsBridgeLogger.Log($"SupportsProfile: commandName='{profile.CommandName}', matches={matches}");

            return matches;
        }

        public async Task<IReadOnlyList<IDebugLaunchSettings>> QueryDebugTargetsAsync(
            DebugLaunchOptions launchOptions,
            ILaunchProfile profile)
        {
            TsBridgeLogger.Log("===== QueryDebugTargetsAsync START =====");

            try
            {
                var projectDir = GetProjectDirectory();
                TsBridgeLogger.Log($"Project directory: {projectDir}");

                if (string.IsNullOrEmpty(projectDir))
                {
                    TsBridgeLogger.Log("Project directory is empty. Aborting.");
                    return new IDebugLaunchSettings[0];
                }

                // Generate the browser configuration for this specific F5 session.
                PrepareDebugSession(projectDir);

                var port = ReadPortFromConfig(projectDir);
                TsBridgeLogger.Log($"Port: {port}");

                if (!_launcher.Start(projectDir, port))
                {
                    throw new InvalidOperationException("Failed to start server.js. See the TypescriptBridge VSIX log.");
                }

                var ready = await _launcher.WaitForHttpReadyAsync(TimeSpan.FromSeconds(20));
                if (!ready)
                {
                    throw new InvalidOperationException(
                        _launcher.StartupError ?? "The debug server did not become ready. See the TypescriptBridge VSIX log.");
                }

                var launchJson = ReadLaunchJson(projectDir);
                TsBridgeLogger.Log($"launch.json (first 300 chars): {Truncate(launchJson, 300)}");

                // Launch the JavaScript debugger directly.
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                LaunchJavascriptDebugger(projectDir, launchJson);

                TsBridgeLogger.Log("===== QueryDebugTargetsAsync END (returning empty) =====");
                return new IDebugLaunchSettings[0];
            }
            catch (Exception ex)
            {
                TsBridgeLogger.Log($"QueryDebugTargetsAsync failed: {ex}");
                _launcher.Dispose();
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                UnsubscribeFromDebugger();
                throw;
            }
        }

        // Refresh launch.json before each session so browser and port changes
        // take effect without rebuilding the C# project.
        private static void PrepareDebugSession(string projectDir)
        {
            var scriptPath = Path.Combine(projectDir, "Prepare-DebugSession.ps1");
            if (!File.Exists(scriptPath))
            {
                throw new FileNotFoundException("Debug session preparation script not found.", scriptPath);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -ProjectDir \"{projectDir}\"",
                WorkingDirectory = projectDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using (var process = Process.Start(startInfo) ??
                throw new InvalidOperationException("Could not start debug session preparation."))
            {
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                TsBridgeLogger.Log($"Prepare-DebugSession: {output.Trim()}");
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        $"Prepare-DebugSession.ps1 failed with exit code {process.ExitCode}: {error.Trim()}");
                }
            }
        }

        public Task OnBeforeLaunchAsync(
            DebugLaunchOptions launchOptions,
            ILaunchProfile profile)
        {
            TsBridgeLogger.Log("OnBeforeLaunchAsync was called.");
            return Task.CompletedTask;
        }

        public Task OnAfterLaunchAsync(
            DebugLaunchOptions launchOptions,
            ILaunchProfile profile)
        {
            TsBridgeLogger.Log("OnAfterLaunchAsync was called.");
            return Task.CompletedTask;
        }

        // Calls IVsDebugger4.LaunchDebugTargets4 with a target that
        // tells Visual Studio to use the JavaScript debug engine.
        private void LaunchJavascriptDebugger(string projectDir, string launchJson)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (ServiceProvider == null)
            {
                throw new InvalidOperationException("Visual Studio service provider is unavailable.");
            }

            var debugger4 = ServiceProvider.GetService(typeof(SVsShellDebugger)) as IVsDebugger4;
            if (debugger4 == null)
            {
                throw new InvalidOperationException("IVsDebugger4 is unavailable from SVsShellDebugger.");
            }

            // Stop the owned Node process when Visual Studio leaves debug mode.
            SubscribeToDebugger();

            TsBridgeLogger.Log("Building VsDebugTargetInfo4.");

            var target = new VsDebugTargetInfo4();

            // dlo = 1 is DebugLaunchOperation.CreateProcess.
            target.dlo = 1;

            // StopDebuggingOnEnd = 0x20.
            target.LaunchFlags = 0x20;

            target.bstrExe = TsBridgeConstants.JavaScriptDebugExecutable;
            target.bstrOptions = launchJson;
            target.bstrCurDir = projectDir;
            target.guidLaunchDebugEngine = TsBridgeConstants.JavaScriptDebugEngineGuid;
            target.project = GetVsHierarchy() ??
                throw new InvalidOperationException("Visual Studio project hierarchy is unavailable.");

            // Allocate a block of memory and copy the GUID into it.
            var engineBytes = TsBridgeConstants.JavaScriptDebugEngineGuid.ToByteArray();
            var enginePtr = Marshal.AllocCoTaskMem(engineBytes.Length);
            Marshal.Copy(engineBytes, 0, enginePtr, engineBytes.Length);

            try
            {
                target.pDebugEngines = enginePtr;
                target.dwDebugEngineCount = 1;

                target.guidPortSupplier = Guid.Empty;
                target.bstrPortName = null;
                target.bstrRemoteMachine = null;
                target.bstrArg = null;
                target.bstrEnv = null;

                TsBridgeLogger.Log("Calling IVsDebugger4.LaunchDebugTargets4.");
                TsBridgeLogger.Log($"  dlo = {target.dlo}");
                TsBridgeLogger.Log($"  LaunchFlags = 0x{target.LaunchFlags:X}");
                TsBridgeLogger.Log($"  bstrExe = {target.bstrExe}");
                TsBridgeLogger.Log($"  bstrCurDir = {target.bstrCurDir}");
                TsBridgeLogger.Log($"  guidLaunchDebugEngine = {target.guidLaunchDebugEngine}");
                TsBridgeLogger.Log($"  dwDebugEngineCount = {target.dwDebugEngineCount}");
                TsBridgeLogger.Log($"  bstrOptions.Length = {(target.bstrOptions?.Length ?? 0)}");
                TsBridgeLogger.Log($"  project = {(target.project != null ? "set" : "null")}");

                var targets = new[] { target };
                var results = new VsDebugTargetProcessInfo[1];

                debugger4.LaunchDebugTargets4(1, targets, results);

                TsBridgeLogger.Log("LaunchDebugTargets4 returned successfully.");
                TsBridgeLogger.Log($"  result.ProcessId = {results[0].dwProcessId}");
            }
            finally
            {
                Marshal.FreeCoTaskMem(enginePtr);
            }
        }

        public int OnModeChange(DBGMODE newMode)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (newMode == DBGMODE.DBGMODE_Design)
            {
                TsBridgeLogger.Log("Debugger returned to design mode.");
                _launcher.Dispose();
                UnsubscribeFromDebugger();
            }
            return 0;
        }

        private void SubscribeToDebugger()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_debugger != null) return;

            var debugger = ServiceProvider?.GetService(typeof(SVsShellDebugger)) as IVsDebugger
                ?? throw new InvalidOperationException("IVsDebugger is unavailable from SVsShellDebugger.");
            Marshal.ThrowExceptionForHR(debugger.AdviseDebuggerEvents(this, out var cookie));
            _debugger = debugger;
            _debuggerEventsCookie = cookie;
        }

        private void UnsubscribeFromDebugger()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_debugger == null) return;
            try
            {
                Marshal.ThrowExceptionForHR(_debugger.UnadviseDebuggerEvents(_debuggerEventsCookie));
            }
            catch (Exception ex)
            {
                TsBridgeLogger.Log($"Could not unsubscribe from debugger events: {ex.Message}");
            }
            finally
            {
                _debugger = null;
                _debuggerEventsCookie = 0;
            }
        }

        private string GetProjectDirectory()
        {
            try
            {
                var projectPath = _project.UnconfiguredProject.FullPath;
                if (!string.IsNullOrEmpty(projectPath))
                {
                    return Path.GetDirectoryName(projectPath) ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                TsBridgeLogger.Log($"GetProjectDirectory error: {ex.Message}");
            }

            return string.Empty;
        }

        private static int ReadPortFromConfig(string projectDir)
        {
            var configPath = Path.Combine(projectDir, "config.json");
            using (var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(configPath)))
            {
                if (doc.RootElement.TryGetProperty("run", out var run) &&
                    run.TryGetProperty("port", out var portProp) &&
                    portProp.TryGetInt32(out var port) &&
                    port >= 1 && port <= 65535)
                {
                    return port;
                }
            }

            throw new InvalidDataException($"Invalid run.port in {configPath}.");
        }

        private string ReadLaunchJson(string projectDir)
        {
            var launchPath = Path.Combine(projectDir, ".vscode", "launch.json");
            using (var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(launchPath)))
            {
                if (doc.RootElement.TryGetProperty("configurations", out var configs) &&
                    configs.ValueKind == System.Text.Json.JsonValueKind.Array &&
                    configs.GetArrayLength() > 0)
                {
                    return configs[0].GetRawText();
                }
            }

            throw new InvalidDataException($"No debug configuration found in {launchPath}.");
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Length <= max ? s : s.Substring(0, max) + "...";
        }
    }
}



