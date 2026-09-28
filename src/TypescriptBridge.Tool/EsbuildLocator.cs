using System.Runtime.InteropServices;

namespace TypescriptBridge.Tool;

internal static class EsbuildLocator
{
    public static string Locate(string? explicitPath)
    {
        // 1. explicit path from CLI
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            if (File.Exists(explicitPath))
                return Path.GetFullPath(explicitPath);

            throw new TypescriptBridgeException(
                ErrorCodes.EsbuildUnavailable,
                $"esbuild not found at explicit path: {explicitPath}");
        }

        // 2. environment variable
        var envPath = Environment.GetEnvironmentVariable("TS_HANDLER_ESBUILD_PATH");
        if (!string.IsNullOrWhiteSpace(envPath) && File.Exists(envPath))
            return Path.GetFullPath(envPath);

        // 3. next to the Tool assembly (NuGet scenario)
        var assemblyDir = AppContext.BaseDirectory;
        var besideTool = Path.Combine(assemblyDir, "esbuild" + GetExeSuffix());
        if (File.Exists(besideTool))
            return besideTool;

        // 3b. also check "tools/esbuild/<rid>" next to assembly
        var rid = GetRuntimeIdentifier();
        var besideToolRid = Path.Combine(assemblyDir, "esbuild", rid, "esbuild" + GetExeSuffix());
        if (File.Exists(besideToolRid))
            return besideToolRid;

        // 4. dev scenario: walk up from assembly dir looking for "tools/esbuild/<rid>/esbuild.exe"
        var devPath = FindInDevTree(assemblyDir, rid);
        if (devPath is not null)
            return devPath;

        throw new TypescriptBridgeException(
            ErrorCodes.EsbuildUnavailable,
            $"esbuild binary not found. " +
            $"Looked for RID '{rid}'. " +
            $"Set TS_HANDLER_ESBUILD_PATH or pass --esbuild <path>.");
    }

    private static string? FindInDevTree(string startDir, string rid)
    {
        var current = new DirectoryInfo(startDir);
        for (int depth = 0; depth < 8 && current is not null; depth++)
        {
            var candidate = Path.Combine(
                current.FullName,
                "tools", "esbuild", rid,
                "esbuild" + GetExeSuffix());

            if (File.Exists(candidate))
                return candidate;

            current = current.Parent;
        }
        return null;
    }

    private static string GetExeSuffix()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "";
    }

    private static string GetRuntimeIdentifier()
    {
        var arch = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "x86",
            _ => throw new TypescriptBridgeException(
                ErrorCodes.EsbuildUnavailable,
                $"Unsupported architecture: {RuntimeInformation.OSArchitecture}"),
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return $"win-{arch}";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return $"linux-{arch}";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return $"osx-{arch}";

        throw new TypescriptBridgeException(
            ErrorCodes.EsbuildUnavailable,
            $"Unsupported OS: {RuntimeInformation.OSDescription}");
    }
}
