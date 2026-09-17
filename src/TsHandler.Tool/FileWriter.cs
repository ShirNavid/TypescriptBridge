using System.Text;

namespace TsHandler.Tool;

internal static class FileWriter
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static void WriteIfChanged(string path, string content)
    {
        if (File.Exists(path))
        {
            var existing = File.ReadAllText(path, Utf8NoBom);
            if (string.Equals(existing, content, StringComparison.Ordinal))
                return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var tempPath = path + ".tmp";
        File.WriteAllText(tempPath, content, Utf8NoBom);

        if (File.Exists(path))
            File.Replace(tempPath, path, destinationBackupFileName: null);
        else
            File.Move(tempPath, path);
    }
}
