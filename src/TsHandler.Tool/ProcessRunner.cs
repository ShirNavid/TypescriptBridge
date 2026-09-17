using System.Diagnostics;
using System.Text;

namespace TsHandler.Tool;

internal sealed record ProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);

internal static class ProcessRunner
{
    public static ProcessResult Run(
        string fileName,
        IEnumerable<string> arguments,
        string? workingDirectory = null,
        IDictionary<string, string>? environment = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var arg in arguments)
        {
            psi.ArgumentList.Add(arg);
        }

        if (workingDirectory is not null)
        {
            psi.WorkingDirectory = workingDirectory;
        }

        if (environment is not null)
        {
            foreach (var (key, value) in environment)
            {
                psi.Environment[key] = value;
            }
        }

        using var process = new Process { StartInfo = psi };

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null) stdout.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null) stderr.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        return new ProcessResult(
            process.ExitCode,
            stdout.ToString(),
            stderr.ToString());
    }
}
