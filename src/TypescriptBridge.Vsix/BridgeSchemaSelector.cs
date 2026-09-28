using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading.Tasks;
using Microsoft.WebTools.Languages.Json.Schema;

namespace TypescriptBridge.Vsix
{
    // Associates the local typescript-bridge.schema.json file with
    // any typescript-bridge.json document that is opened in Visual Studio.
    //
    // Visual Studio's JSON language service uses IJsonSchemaSelector
    // implementations to discover which schema applies to a given file.
    [Export(typeof(IJsonSchemaSelector))]
    internal sealed class BridgeSchemaSelector : IJsonSchemaSelector
    {
        // The exact file name this selector is responsible for.
        private const string TargetFileName = "typescript-bridge.json";

        // The schema file that should be associated with the target file.
        private const string SchemaFileName = "typescript-bridge.schema.json";

        // Event raised when the list of available schemas changes.
        // We have a static list, so this event is never raised.
        public event EventHandler? AvailableSchemasChanged;

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

                // Only match files named typescript-bridge.json.
                var fileName = Path.GetFileName(fileLocation);
                if (!string.Equals(fileName, TargetFileName, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                // Look for the schema file next to the JSON file.
                var directory = Path.GetDirectoryName(fileLocation);
                if (string.IsNullOrEmpty(directory))
                {
                    return null;
                }

                var schemaPath = Path.Combine(directory, SchemaFileName);
                if (File.Exists(schemaPath))
                {
                    return schemaPath;
                }

                return null;
            }
            catch
            {
                // Never throw from a selector; returning null means "no schema".
                return null;
            }
        }

        // Returns the list of schemas this selector knows about.
        // The list is intentionally empty because our schema is discovered
        // dynamically from the file location in GetSchemaFor.
        public Task<IEnumerable<string>> GetAvailableSchemasAsync()
        {
            return Task.FromResult<IEnumerable<string>>(Array.Empty<string>());
        }

        // Raises the AvailableSchemasChanged event.
        // Kept private because the event is never fired in this implementation.
        private void OnAvailableSchemasChanged()
        {
            AvailableSchemasChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
