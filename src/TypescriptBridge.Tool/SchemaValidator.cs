using System.Text.Json;

namespace TypescriptBridge.Tool;

internal static class SchemaValidator
{
    private static readonly HashSet<string> AllowedRootProperties = new(StringComparer.Ordinal)
    {
        "output",
        "bundle",
        "release-minify",
        "run",
        "isTestProject",
        "entrypoint",
        "entrypoints",
        "testEntrypoint",
    };

    private static readonly HashSet<string> AllowedOutputProperties = new(StringComparer.Ordinal)
    {
        "className",
        "fieldName",
    };

    private static readonly HashSet<string> AllowedBundleProperties = new(StringComparer.Ordinal)
    {
        "format",
    };

    private static readonly HashSet<string> AllowedMinifyProperties = new(StringComparer.Ordinal)
    {
        "level",
        "keepNames",
    };

    private static readonly HashSet<string> AllowedRunProperties = new(StringComparer.Ordinal)
    {
        "entrypoint",
        "browser",
        "port",
    };

    private static readonly HashSet<string> ValidFormats = new(StringComparer.Ordinal)
    {
        "iife",
        "esm",
        "cjs",
    };

    private static readonly HashSet<string> ValidMinifyLevels = new(StringComparer.Ordinal)
    {
        "off",
        "light",
        "full",
        "aggressive",
    };

    private static readonly HashSet<string> ValidBrowsers = new(StringComparer.Ordinal)
    {
        "edge",
        "chrome",
    };

    private static readonly HashSet<string> CSharpKeywords = new(StringComparer.Ordinal)
    {
        "abstract",
        "as",
        "base",
        "bool",
        "break",
        "byte",
        "case",
        "catch",
        "char",
        "checked",
        "class",
        "const",
        "continue",
        "decimal",
        "default",
        "delegate",
        "do",
        "double",
        "else",
        "enum",
        "event",
        "explicit",
        "extern",
        "false",
        "finally",
        "fixed",
        "float",
        "for",
        "foreach",
        "goto",
        "if",
        "implicit",
        "in",
        "int",
        "interface",
        "internal",
        "is",
        "lock",
        "long",
        "namespace",
        "new",
        "null",
        "object",
        "operator",
        "out",
        "override",
        "params",
        "private",
        "protected",
        "public",
        "readonly",
        "ref",
        "return",
        "sbyte",
        "sealed",
        "short",
        "sizeof",
        "stackalloc",
        "static",
        "string",
        "struct",
        "switch",
        "this",
        "throw",
        "true",
        "try",
        "typeof",
        "uint",
        "ulong",
        "unchecked",
        "unsafe",
        "ushort",
        "using",
        "virtual",
        "void",
        "volatile",
        "while",
    };

    public static void Validate(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            Fail("$", "must be a JSON object.");
        }

        ValidateRootProperties(root);

        if (root.TryGetProperty("output", out var output))
            ValidateOutput(output);

        if (root.TryGetProperty("entrypoints", out var entrypoints))
            ValidateEntryPoints(entrypoints);

        // F5 needs one known application entry; test projects use testEntrypoint.
        if (root.TryGetProperty("run", out var runSettings) &&
            runSettings.ValueKind == JsonValueKind.Object &&
            (!root.TryGetProperty("isTestProject", out var testFlag) || testFlag.ValueKind != JsonValueKind.True))
        {
            var selected = runSettings.TryGetProperty("entrypoint", out var choice)
                ? (choice.ValueKind == JsonValueKind.String ? choice.GetString()! : "main")
                : "main";
            if (entrypoints.ValueKind == JsonValueKind.Object && !entrypoints.TryGetProperty(selected, out _))
                Fail("$.run.entrypoint", $"unknown entrypoint \"{selected}\".");
            if (entrypoints.ValueKind == JsonValueKind.Undefined && selected != "main")
                Fail("$.run.entrypoint", "requires an entrypoints map.");
        }
        if (root.TryGetProperty("bundle", out var bundle))
            ValidateBundle(bundle);

        if (root.TryGetProperty("release-minify", out var releaseMinify))
            ValidateMinify(releaseMinify);

        if (root.TryGetProperty("run", out var run))
            ValidateRun(run);
    }

    // Validates the root properties and provides a specific migration
    // message when the obsolete "typescript" section is present.
    private static void ValidateRootProperties(JsonElement element)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name == "typescript")
            {
                Fail(
                    "$.typescript",
                    "is no longer supported. " +
                    "Move \"target\" to tsconfig.json compilerOptions.target " +
                    "and \"format\" to config.json bundle.format.");
            }

            if (property.Name == "isTestProject" && property.Value.ValueKind != JsonValueKind.True && property.Value.ValueKind != JsonValueKind.False)
                Fail("$.isTestProject", "must be a boolean.");

            if (property.Name is "entrypoint" or "testEntrypoint" && property.Value.ValueKind != JsonValueKind.String)
                Fail($"$.{property.Name}", "must be a string.");

            if (!AllowedRootProperties.Contains(property.Name))
            {
                Fail($"$.{property.Name}", "unknown property.");
            }
        }
    }

    private static void ValidateEntryPoints(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.EnumerateObject().Any())
            Fail("$.entrypoints", "must be a non-empty object.");

        var fields = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in element.EnumerateObject())
        {
            using var key = JsonDocument.Parse(JsonSerializer.Serialize(entry.Name));
            ValidateIdentifier(key.RootElement, $"$.entrypoints.{entry.Name}");
            var location = $"$.entrypoints.{entry.Name}";
            if (entry.Value.ValueKind != JsonValueKind.Object)
                Fail(location, "must be an object.");
            ValidateProperties(entry.Value, new HashSet<string>(StringComparer.Ordinal) { "fieldName", "source" }, location);
            if (!entry.Value.TryGetProperty("fieldName", out var field))
                Fail(location + ".fieldName", "is required.");
            ValidateIdentifier(field, location + ".fieldName");
            if (!fields.Add(field.GetString()!))
                Fail(location + ".fieldName", "must be unique.");
            if (!entry.Value.TryGetProperty("source", out var source) || source.ValueKind != JsonValueKind.String)
                Fail(location + ".source", "must be a string.");
            var path = source.GetString()!;
            if (string.IsNullOrWhiteSpace(path) || path.StartsWith('/') || path.Contains(':') ||
                path.Split('/', '\\').Any(part => part is ".." or "." or ""))
                Fail(location + ".source", "must be a relative path under ts/.");
        }
    }
    private static void ValidateBundle(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            Fail("$.bundle", "must be an object.");

        ValidateProperties(element, AllowedBundleProperties, "$.bundle");

        if (element.TryGetProperty("format", out var format))
        {
            if (format.ValueKind != JsonValueKind.String)
                Fail("$.bundle.format", "must be a string.");

            var value = format.GetString()!;

            if (!ValidFormats.Contains(value))
                Fail("$.bundle.format", $"unknown value \"{value}\".");
        }
    }

    private static void ValidateRun(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            Fail("$.run", "must be an object.");

        ValidateProperties(element, AllowedRunProperties, "$.run");

        if (element.TryGetProperty("entrypoint", out var entrypoint) &&
            (entrypoint.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(entrypoint.GetString())))
            Fail("$.run.entrypoint", "must be a non-empty string.");
        if (element.TryGetProperty("browser", out var browser))
        {
            if (browser.ValueKind != JsonValueKind.String)
                Fail("$.run.browser", "must be a string.");

            var value = browser.GetString()!;

            if (!ValidBrowsers.Contains(value))
                Fail("$.run.browser", $"unknown value \"{value}\".");
        }

        if (element.TryGetProperty("port", out var port))
        {
            if (port.ValueKind != JsonValueKind.Number)
                Fail("$.run.port", "must be a number.");

            if (!port.TryGetInt32(out var value))
                Fail("$.run.port", "must be a 32-bit integer.");

            if (value < 1 || value > 65535)
                Fail("$.run.port", "must be between 1 and 65535.");
        }
    }

    private static void ValidateOutput(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            Fail("$.output", "must be an object.");

        ValidateProperties(element, AllowedOutputProperties, "$.output");

        if (element.TryGetProperty("className", out var className))
            ValidateIdentifier(className, "$.output.className");

        if (element.TryGetProperty("fieldName", out var fieldName))
            ValidateIdentifier(fieldName, "$.output.fieldName");
    }

    private static void ValidateMinify(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            Fail("$.release-minify", "must be an object.");

        ValidateProperties(element, AllowedMinifyProperties, "$.release-minify");

        if (element.TryGetProperty("level", out var level))
        {
            if (level.ValueKind != JsonValueKind.String)
                Fail("$.release-minify.level", "must be a string.");

            var value = level.GetString()!;

            if (!ValidMinifyLevels.Contains(value))
                Fail("$.release-minify.level", $"unknown value \"{value}\".");
        }

        if (element.TryGetProperty("keepNames", out var keepNames) &&
            keepNames.ValueKind != JsonValueKind.True &&
            keepNames.ValueKind != JsonValueKind.False)
        {
            Fail("$.release-minify.keepNames", "must be a boolean.");
        }
    }

    private static void ValidateProperties(
        JsonElement element,
        HashSet<string> allowed,
        string path)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (!allowed.Contains(property.Name))
                Fail($"{path}.{property.Name}", "unknown property.");
        }
    }

    private static void ValidateIdentifier(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.String)
            Fail(path, "must be a string.");

        var value = element.GetString()!;

        if (string.IsNullOrWhiteSpace(value))
            Fail(path, "must not be empty.");

        if (!char.IsLetter(value[0]) && value[0] != '_')
            Fail(path, "must start with a letter or underscore.");

        for (int i = 1; i < value.Length; i++)
        {
            if (!char.IsLetterOrDigit(value[i]) && value[i] != '_')
                Fail(path, "must contain only letters, digits, or underscores.");
        }

        if (CSharpKeywords.Contains(value))
            Fail(path, $"\"{value}\" is a C# keyword.");
    }

    private static void Fail(string path, string message)
    {
        throw new TypescriptBridgeException(
            ErrorCodes.ConfigValidationFailed,
            $"{path}: {message}");
    }
}
