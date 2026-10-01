using System;
using System.IO;

namespace TypescriptBridge.Vsix
{
    // Simple file logger used by the VSIX while it is running.
    //
    // The logger writes to %TEMP%\TypescriptBridge.Vsix.log. It never
    // throws: any IO failure is silently ignored so that a broken log
    // file can never affect the Visual Studio process.
    //
    // The log is written as one line per event. Each line starts with
    // an ISO 8601 timestamp so that the order of events can be
    // reconstructed from the file alone.
    internal static class TsBridgeLogger
    {
        // Full path to the log file. The directory is the system
        // temporary folder, which is available to the VSIX without
        // requiring any special permissions.
        private static readonly string LogPath = Path.Combine(
            Path.GetTempPath(),
            TsBridgeConstants.LogFileName);

        // Appends a single line to the log file.
        //
        // This method is safe to call from any thread. It does not
        // take a lock: Visual Studio calls into the provider from a
        // single thread, and the log is only a diagnostic aid.
        public static void Log(string message)
        {
            try
            {
                var line = DateTime.Now.ToString("o") + " " + message + Environment.NewLine;
                File.AppendAllText(LogPath, line);
            }
            catch
            {
                // Never let logging break Visual Studio.
            }
        }
    }
}
