using System;
using System.Diagnostics;
using System.IO;

namespace TypescriptBridge.DebugHost
{
    // Launches Edge with an isolated user data directory and the
    // remote debugging port enabled, then assigns the resulting
    // process to a job object so that the whole process tree is
    // killed when the job is disposed.
    public sealed class BrowserLauncher : IDisposable
    {
        private BrowserProcessJob? _job;
        private Process? _process;

        public Process? Process => _process;

        public Process Launch(string url, string userDataDir, int remotePort)
        {
            if (!Directory.Exists(userDataDir))
            {
                Directory.CreateDirectory(userDataDir);
            }

            var edge = EdgeLocator.Locate();

            _job = new BrowserProcessJob();

            var psi = new ProcessStartInfo
            {
                FileName = edge,
                UseShellExecute = false,
            };

            psi.Arguments = string.Join(" ", new[]
            {
                Quote($"--user-data-dir={userDataDir}"),
                Quote($"--remote-debugging-port={remotePort}"),
                "--no-first-run",
                "--no-default-browser-check",
                Quote(url),
            });

            _process = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start Microsoft Edge.");

            try
            {
                _job.AssignProcess(_process);
            }
            catch
            {
                try { _process.Kill(); } catch { /* ignore */ }
                throw;
            }

            return _process;
        }

        public void Dispose()
        {
            if (_process is not null)
            {
                try
                {
                    if (!_process.HasExited)
                    {
                        _process.WaitForExit(2000);
                    }
                }
                catch { /* ignore */ }

                _process.Dispose();
                _process = null;
            }

            _job?.Dispose();
            _job = null;
        }

        // Quotes a single command-line argument for CreateProcess.
        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }
    }
}

