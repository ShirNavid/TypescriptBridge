using System;
using System.IO;

namespace TypescriptBridge.DebugHost
{
    // Detects whether a supported browser is installed on this machine.
    // This is used by the runtime host to fail early with a clear error
    // instead of trying to launch a browser that does not exist.
    //
    // The same discovery logic is also used by the VSIX to filter the
    // JSON schema enum for run.browser.
    public static class BrowserValidator
    {
        // Known browsers.
        public const string Edge = "edge";
        public const string Chrome = "chrome";

        // Returns true if the named browser is installed.
        public static bool IsInstalled(string name)
        {
            return name?.ToLowerInvariant() switch
            {
                Edge => FindEdge() is not null,
                Chrome => FindChrome() is not null,
                _ => false,
            };
        }

        // Returns the full path to msedge.exe or null.
        public static string? FindEdge()
        {
            var candidates = new[]
            {
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    "Microsoft", "Edge", "Application", "msedge.exe"),

                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "Microsoft", "Edge", "Application", "msedge.exe"),
            };

            foreach (var c in candidates)
            {
                if (File.Exists(c)) return c;
            }

            return null;
        }

        // Returns the full path to chrome.exe or null.
        public static string? FindChrome()
        {
            var candidates = new[]
            {
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "Google", "Chrome", "Application", "chrome.exe"),

                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    "Google", "Chrome", "Application", "chrome.exe"),

                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Google", "Chrome", "Application", "chrome.exe"),
            };

            foreach (var c in candidates)
            {
                if (File.Exists(c)) return c;
            }

            return null;
        }
    }
}
