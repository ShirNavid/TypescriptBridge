using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.WebTools.Languages.Json.Schema;
using TypescriptBridge.Vsix.Browser;

namespace TypescriptBridge.Vsix
{
    // Associates a dynamically-generated schema with any config.json
    // document that is opened in Visual Studio.
    //
    // The schema is generated on disk in %TEMP% based on the set of
    // browsers actually installed on the current machine. If no browser
    // is discovered, or if anything fails, the bundled schema is used
    // as-is (with the full enum).
    [Export(typeof(IJsonSchemaSelector))]
    internal sealed class BridgeSchemaSelector : IJsonSchemaSelector
    {
        // The exact file name this selector is responsible for.
        private const string TargetFileName = "config.json";

        // The bundled schema file name, next to the config.json file.
        private const string BundledSchemaFileName = "config.schema.json";

        // Path under %TEMP% where the filtered schema is written.
        private static readonly string FilteredSchemaDirectory =
            Path.Combine(Path.GetTempPath(), "TypescriptBridge");

        // Event raised when the list of available schemas changes.
        //
        // The implementation never fires this event: our schema is
        // discovered per-file in GetSchemaFor and does not change while
        // Visual Studio is running. The event is required by the
        // IJsonSchemaSelector interface.
#pragma warning disable CS0067
        public event EventHandler? AvailableSchemasChanged;
#pragma warning restore CS0067

        // Returns the absolute path to the schema that should be used for
        // the given file, or null if this selector does not apply.
        public string? GetSchemaFor(string fileLocation)
        {
            try
            {
                if (string.IsNullOrEmpty(fileLocation))
                {
                    return null;
                }

                var fileName = Path.GetFileName(fileLocation);
                if (!string.Equals(fileName, TargetFileName, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                var directory = Path.GetDirectoryName(fileLocation);
                if (string.IsNullOrEmpty(directory))
                {
                    return null;
                }

                var bundledSchema = Path.Combine(directory, BundledSchemaFileName);
                if (!File.Exists(bundledSchema))
                {
                    return null;
                }

                // Try to produce a filtered schema based on installed browsers.
                var filteredSchema = TryBuildFilteredSchema(bundledSchema);
                return filteredSchema ?? bundledSchema;
            }
            catch
            {
                return null;
            }
        }

        // Returns an empty list. This selector discovers schemas
        // dynamically for each file, not through a static list.
        public Task<IEnumerable<string>> GetAvailableSchemasAsync()
        {
            return Task.FromResult<IEnumerable<string>>(Array.Empty<string>());
        }

        // Builds a schema file whose run.browser enum contains only the
        // installed browsers. Returns the path to the filtered schema,
        // or null if filtering cannot be performed.
        private static string? TryBuildFilteredSchema(string bundledSchemaPath)
        {
            var browsers = GetInstalledBrowsers();
            if (browsers.Count == 0)
            {
                // No browser discovered: fall back to the bundled schema.
                return null;
            }

            // If both browsers are installed, the bundled schema is already
            // correct. Return null so the caller uses the bundled one.
            if (browsers.Count == 2)
            {
                return null;
            }

            var schemaText = File.ReadAllText(bundledSchemaPath);

            // Replace the enum inside the browser property. We use a
            // conservative regex that matches the browser block exactly.
            var enumPattern = new Regex(
                "(\"browser\"\\s*:\\s*\\{[^}]*?\"enum\"\\s*:\\s*\\[)([^\\]]*)(\\])",
                RegexOptions.Singleline);

            var replacement = "$1" + Environment.NewLine +
                string.Join("," + Environment.NewLine, BrowserListToJsonLines(browsers)) +
                Environment.NewLine + "          $3";

            var filtered = enumPattern.Replace(schemaText, replacement, 1);

            Directory.CreateDirectory(FilteredSchemaDirectory);

            // The filtered file name depends on the config.json directory
            // so that two different projects do not collide.
            var hash = Math.Abs(bundledSchemaPath.GetHashCode()).ToString("X8");
            var filteredPath = Path.Combine(
                FilteredSchemaDirectory,
                "config.schema." + hash + ".json");

            File.WriteAllText(filteredPath, filtered, new UTF8Encoding(false));
            return filteredPath;
        }

        // Formats each browser identifier as a JSON string line for the
        // enum replacement.
        private static IEnumerable<string> BrowserListToJsonLines(IReadOnlyList<string> browsers)
        {
            for (int i = 0; i < browsers.Count; i++)
            {
                var comma = i < browsers.Count - 1 ? "," : "";
                yield return "            \"" + browsers[i] + "\"" + comma;
            }
        }

        // Resolves the list of installed browsers from the MEF-exported
        // IBrowserProvider. Returns an empty list if the provider cannot
        // be resolved.
        private static IReadOnlyList<string> GetInstalledBrowsers()
        {
            try
            {
                var componentModel = ServiceProvider.GlobalProvider
                    .GetService(typeof(SComponentModel)) as IComponentModel;

                if (componentModel is null)
                {
                    return Array.Empty<string>();
                }

                var provider = componentModel.GetService<IBrowserProvider>();
                return provider?.GetInstalledBrowsers() ?? Array.Empty<string>();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }
    }
}

