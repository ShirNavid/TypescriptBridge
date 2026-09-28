using System.Text.Json;

namespace TypescriptBridge.Tool;

internal static class SchemaValidator
{
    private static readonly HashSet<string> AllowedRootProperties = new(StringComparer.Ordinal)
    {
        "output",
        "typescript",
        "minify",
    };

    private static readonly HashSet<string> AllowedOutputProperties = new(StringComparer.Ordinal)
    {
        "className",
        "fieldName",
    };

    private static readonly HashSet<string> AllowedTypeScriptProperties = new(StringComparer.Ordinal)
    {
        "target",
        "format",
    };

    private static readonly HashSet<string> AllowedMinifyProperties = new(StringComparer.Ordinal)
    {
        "level",
        "keepNames",
    };

    private static readonly HashSet<string> ValidTargets = new(StringComparer.Ordinal)
    {
        "es2015",
        "es2016",
        "es2017",
        "es2018",
        "es2019",
        "es2020",
        "es2021",
        "es2022",
        "esnext",
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

        ValidateProperties(root, AllowedRootProperties, "$");

        if (root.TryGetProperty("output", out var output))
            ValidateOutput(output);

        if (root.TryGetProperty("typescript", out var typescript))
            ValidateTypeScript(typescript);

        if (root.TryGetProperty("minify", out var minify))
            ValidateMinify(minify);
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

    private static void ValidateTypeScript(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            Fail("$.typescript", "must be an object.");

        ValidateProperties(element, AllowedTypeScriptProperties, "$.typescript");

        if (element.TryGetProperty("target", out var target))
        {
            if (target.ValueKind != JsonValueKind.String)
                Fail("$.typescript.target", "must be a string.");

            var value = target.GetString()!;

            if (!ValidTargets.Contains(value))
                Fail("$.typescript.target", $"unknown value \"{value}\".");
        }

        if (element.TryGetProperty("format", out var format))
        {
            if (format.ValueKind != JsonValueKind.String)
                Fail("$.typescript.format", "must be a string.");

            var value = format.GetString()!;

            if (!ValidFormats.Contains(value))
                Fail("$.typescript.format", $"unknown value \"{value}\".");
        }
    }

    private static void ValidateMinify(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            Fail("$.minify", "must be an object.");

        ValidateProperties(element, AllowedMinifyProperties, "$.minify");

        if (element.TryGetProperty("level", out var level))
        {
            if (level.ValueKind != JsonValueKind.String)
                Fail("$.minify.level", "must be a string.");

            var value = level.GetString()!;

            if (!ValidMinifyLevels.Contains(value))
                Fail("$.minify.level", $"unknown value \"{value}\".");
        }

        if (element.TryGetProperty("keepNames", out var keepNames) &&
            keepNames.ValueKind != JsonValueKind.True &&
            keepNames.ValueKind != JsonValueKind.False)
        {
            Fail("$.minify.keepNames", "must be a boolean.");
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
