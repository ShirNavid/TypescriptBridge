using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using TypescriptBridge.DebugHost;
using System.Threading.Tasks;

namespace TypescriptBridge.Vsix.Debug
{
    // Orchestrates a single debug session.
    //
    // Responsibilities:
    //   - generate a session id
    //   - invoke the Tool to produce the debug artifacts
    //   - read debug-manifest.json
    //   - start the local HTTP server on the manifest port
    //   - launch Edge with the isolated profile
    //   - stop and clean up on demand
    //
    // The debugger attach step (B5) is intentionally out of scope here.
    internal sealed class DebugSessionManager : IDisposable
    {
        private HttpHostServer? _httpServer;
        private BrowserLauncher? _browserLauncher;
        private string? _sessionRoot;
        private DebugManifest? _manifest;

        public DebugManifest? Manifest => _manifest;
        public Process? BrowserProcess => _browserLauncher?.Process;

        // Starts a new debug session for the given project root.
        public async Task StartAsync(string projectRoot, CancellationToken cancellationToken)
        {
            if (_manifest is not null)
            {
                throw new InvalidOperationException("A debug session is already running.");
            }

            // Generate a fresh session id.
            var sessionId = "session-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);

            // Pick an available TCP port for the HTTP server.
            var port = PortAllocator.FindFreeLoopbackPort();

            // Run the Tool. It produces all artifacts and debug-manifest.json.
            _sessionRoot = await ToolRunner.RunDebugAsync(
                projectRoot,
                sessionId,
                port,
                cancellationToken).ConfigureAwait(false);

            // Read the manifest.
            var manifestPath = Path.Combine(_sessionRoot, "debug-manifest.json");
            var json = await Task.Run(() => File.ReadAllText(manifestPath), cancellationToken).ConfigureAwait(false);
            var manifest = JsonSerializer.Deserialize<DebugManifest>(json)
                ?? throw new InvalidOperationException("Failed to deserialize debug-manifest.json.");
            _manifest = manifest;

            // Start the HTTP server on the manifest port.
            _httpServer = new HttpHostServer(_sessionRoot, manifest.Port);
            _httpServer.Start();

            // Launch Edge with an isolated profile under the session dir.
            var profileDir = Path.Combine(_sessionRoot, "browser-profile");
            var url = $"http://127.0.0.1:{manifest.Port}/index.html";

            _browserLauncher = new BrowserLauncher();
            _browserLauncher.Launch(url, profileDir, manifest.Port);

            // Give the browser a moment to start the debug listener
            // before we tell the user how to attach.
            await Task.Delay(1500, cancellationToken).ConfigureAwait(false);

            // Guide the user through a manual debugger attach (MVP path).
            await AttachGuide.ShowAsync(manifest).ConfigureAwait(false);
        }

        // Stops the session and releases all resources.
        public void Stop()
        {
            try { _browserLauncher?.Dispose(); } catch { /* ignore */ }
            _browserLauncher = null;

            try { _httpServer?.Dispose(); } catch { /* ignore */ }
            _httpServer = null;

            _manifest = null;
            _sessionRoot = null;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}



