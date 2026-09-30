using System;
using System.IO;
using Microsoft.Win32;

namespace TypescriptBridge.DebugHost
{
    // Locates the Edge browser executable on the current machine.
    //
    // Search order:
    //   1. Registry key for Edge (App Paths)
    //   2. Standard installation paths under Program Files
    //   3. Explicit error if none found
    //
    // Only Edge is supported in the initial implementation. Chrome is
    // a possible follow-up but is intentionally out of scope here.
    public static class EdgeLocator
    {
        // Returns the full path to msedge.exe or throws.
        public static string Locate()
        {
            // 1. Registry: HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\msedge.exe
            var fromRegistry = TryFromRegistry();
            if (fromRegistry is not null)
            {
                return fromRegistry;
            }

            // 2. Standard install locations.
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            var candidates = new[]
            {
                Path.Combine(programFilesX86, "Microsoft", "Edge", "Application", "msedge.exe"),
                Path.Combine(programFiles,    "Microsoft", "Edge", "Application", "msedge.exe"),
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            throw new FileNotFoundException(
                "Microsoft Edge was not found on this machine. " +
                "Install Edge or configure a browser path manually.");
        }

        // Reads the App Paths registry key.
        private static string? TryFromRegistry()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\msedge.exe");

                if (key is null)
                {
                    return null;
                }

                var value = key.GetValue(null) as string;
                if (!string.IsNullOrWhiteSpace(value) && File.Exists(value))
                {
                    return value;
                }
            }
            catch
            {
                // Registry read failure is not fatal. Fall back to path probing.
            }

            return null;
        }
    }
}

