using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace TypescriptBridge.Vsix
{
    // Starts and stops the Node.js debug server that ships with a
    // TypeScript Bridge project.
    //
    // The server is expected to live next to the project file as
    // server.js and to accept a single argument, --f5, which tells it
    // to start the HTTP listener without opening a browser.
    //
    // The launcher owns the Node process created for F5. The launch
    // provider disposes it on launch failure or before a later F5.
    internal sealed class TsBridgeServerLauncher : IDisposable
    {
        // The Node process. Null until Start is called.
        private Process? _process;

        // The port that the server is expected to listen on. Set by
        // Start and used by WaitForHttpReady.
        private int _port;

        // Readiness must come from this process, not another listener on the port.
        private volatile bool _listening;
        private volatile string? _startupError;

        public string? StartupError => _startupError;

        // Starts node server.js --f5 inside the given project directory.
        //
        // Returns true if the process started successfully. Returns
        // false if the file was not found or the process failed to
        // start.
        public bool Start(string projectDirectory, int port)
        {
            // A new F5 must never inherit the listener from an older session.
            Dispose();
            _port = port;
            _listening = false;
            _startupError = null;

            var serverPath = Path.Combine(projectDirectory, "server.js");
            if (!File.Exists(serverPath))
            {
                TsBridgeLogger.Log($"server.js not found: {serverPath}");
                return false;
            }

            var psi = new ProcessStartInfo
            {
                FileName = "node",
                Arguments = $"\"{serverPath}\" --f5",
                WorkingDirectory = projectDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            try
            {
                var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
                _process = process;
                process.OutputDataReceived += (s, e) =>
                {
                    if (e.Data != null) TsBridgeLogger.Log($"[server.out] {e.Data}");
                };
                process.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data == null) return;
                    TsBridgeLogger.Log($"[server.err] {e.Data}");
                    if (!ReferenceEquals(_process, process)) return;
                    if (e.Data.Contains($"HTTP server listening on http://127.0.0.1:{_port}/"))
                        _listening = true;
                    else if (e.Data.Contains($"port {_port} is already in use"))
                        _startupError = e.Data;
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                TsBridgeLogger.Log($"Started node server.js (PID {process.Id}).");
                return true;
            }
            catch (Exception ex)
            {
                TsBridgeLogger.Log($"Failed to start node server.js: {ex.Message}");
                _process = null;
                return false;
            }
        }

        // Waits for this Node process to report that it owns the HTTP
        // listener, then confirms it accepts TCP connections.
        //
        // Returns true if the listener is ready. Returns false if the
        // timeout expires.
        public async Task<bool> WaitForHttpReadyAsync(TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;

            while (DateTime.UtcNow < deadline)
            {
                if (_startupError != null)
                {
                    TsBridgeLogger.Log(_startupError);
                    return false;
                }

                if (_process == null || _process.HasExited)
                {
                    TsBridgeLogger.Log("Node debug server exited before the HTTP listener was ready.");
                    return false;
                }

                if (!_listening)
                {
                    await Task.Delay(100);
                    continue;
                }

                try
                {
                    using (var client = new TcpClient())
                    {
                        var connectTask = client.ConnectAsync("127.0.0.1", _port);
                        var completed = await Task.WhenAny(
                            connectTask,
                            Task.Delay(TimeSpan.FromMilliseconds(500)));

                        if (completed == connectTask && !connectTask.IsFaulted && client.Connected)
                        {
                            TsBridgeLogger.Log($"HTTP listener ready on port {_port}.");
                            return true;
                        }
                    }
                }
                catch
                {
                    // Keep waiting.
                }

                await Task.Delay(100);
            }

            TsBridgeLogger.Log($"Timed out waiting for HTTP listener on port {_port}.");
            return false;
        }

        // Returns the TCP port that the launcher was started with.
        public int Port => _port;

        // Stops the Node process if it is running.
        public void Dispose()
        {
            if (_process == null) return;

            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill();
                    _process.WaitForExit(2000);
                }
            }
            catch
            {
                // Ignore.
            }
            finally
            {
                try { _process.Dispose(); } catch { }
                _process = null;
                TsBridgeLogger.Log("Stopped node server.js.");
            }
        }
    }
}

