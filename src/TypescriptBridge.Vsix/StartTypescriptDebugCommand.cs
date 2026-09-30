using System;
using System.ComponentModel.Design;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using TypescriptBridge.Vsix.Debug;
using Task = System.Threading.Tasks.Task;

namespace TypescriptBridge.Vsix
{
    // The "Start TypeScript Debugging" command.
    //
    // Current scope (B4): resolve the project root, run the Tool,
    // start the HTTP host, launch Edge with an isolated profile.
    // The debugger attach step (B5) is not yet wired in.
    internal static class StartTypescriptDebugCommand
    {
        public const string CommandSetGuidString = "c8a3d2e4-6f1b-4a7c-8d9e-5b2f3a1c7d4e";
        public const int CommandId = 0x0100;
        public static readonly Guid CommandSetGuid = new Guid(CommandSetGuidString);

        private static TypescriptBridgePackage? _package;
        private static DebugSessionManager? _session;

        public static async Task InitializeAsync(
            TypescriptBridgePackage package,
            CancellationToken cancellationToken)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));

            await package.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService is null)
            {
                return;
            }

            var menuCommandId = new CommandID(CommandSetGuid, CommandId);
            var menuItem = new MenuCommand(Execute, menuCommandId);
            commandService.AddCommand(menuItem);
        }

        private static void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var projectRoot = TryResolveProjectRoot();
            if (projectRoot is null)
            {
                MessageBox.Show(
                    "TypescriptBridge: could not find a project directory containing config.json and ts/app.ts.",
                    "TypescriptBridge",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            // If a session is already running, stop it first.
            if (_session is not null)
            {
                _session.Stop();
                _session.Dispose();
                _session = null;
                MessageBox.Show(
                    "TypescriptBridge: previous debug session stopped.",
                    "TypescriptBridge",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            // Start the session on a background thread so that the UI
            // thread is not blocked while the Tool runs.
            _ = Task.Run(async () =>
            {
                var manager = new DebugSessionManager();
                try
                {
                    await manager.StartAsync(projectRoot, CancellationToken.None).ConfigureAwait(false);
                    _session = manager;

                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    MessageBox.Show(
                        $"TypescriptBridge: debug session started.\n\n" +
                        $"Project: {projectRoot}\n" +
                        $"Port: {manager.Manifest?.Port}\n" +
                        $"URL: http://127.0.0.1:{manager.Manifest?.Port}/index.html",
                        "TypescriptBridge",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    manager.Dispose();

                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    MessageBox.Show(
                        $"TypescriptBridge: failed to start debug session.\n\n{ex.Message}",
                        "TypescriptBridge",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            });
        }

        // Walks up from the current directory looking for config.json
        // and ts/app.ts.
        private static string? TryResolveProjectRoot()
        {
            var current = new DirectoryInfo(Environment.CurrentDirectory);
            for (int depth = 0; depth < 8 && current is not null; depth++)
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

        // Stops the current session, if any. Called from package disposal.
        public static void Shutdown()
        {
            if (_session is not null)
            {
                _session.Stop();
                _session.Dispose();
                _session = null;
            }
        }
    }
}
