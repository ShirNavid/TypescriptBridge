using System.Text.Json;

namespace TsHandler.Tool;

internal static class SchemaValidator
{
    private static readonly string[] AllowedTargets = new[]
    {
        "es2015", "es2016", "es2017", "es2018", "es2019",
        "es2020", "es2021", "es2022", "esnext"
    };

    private static readonly string[] AllowedFormats = new[] { "iife", "esm", "cjs" };

    private static readonly string[] AllowedMinifyLevels = new[]
    {
        "off", "light", "full", "aggressive"
    };

    public static void Validate(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new TsHandlerException(
                ErrorCodes.ConfigValidationFailed,
                "$: root must be a JSON object.");
        }

        RequireProperty(root, "version", "$");
        RequireProperty(root, "typescript", "$");

        RejectUnknownProperties(root, "$",
            "version", "typescript", "debug", "release");

        var version = root.GetProperty("version");
        if (version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var v))
        {
            throw new TsHandlerException(
                ErrorCodes.ConfigValidationFailed,
                "$.version: must be an integer.");
        }
        if (v != 1)
        {
            throw new TsHandlerException(
                ErrorCodes.ConfigValidationFailed,
                $"$.version: must be 1 (got {v}).");
        }

        ValidateTypeScript(root.GetProperty("typescript"));

        if (root.TryGetProperty("debug", out var debug))
        {
            ValidateDebug(debug);
        }

        if (root.TryGetProperty("release", out var release))
        {
            ValidateRelease(release);
        }
    }

    private static void ValidateTypeScript(JsonElement ts)
    {
        if (ts.ValueKind != JsonValueKind.Object)
            throw Invalid("$.typescript", "must be an object.");

        RejectUnknownProperties(ts, "$.typescript", "target", "format");

        if (ts.TryGetProperty("target", out var target))
        {
            if (target.ValueKind != JsonValueKind.String)
                throw Invalid("$.typescript.target", "must be a string.");
            var t = target.GetString()!;
            if (!AllowedTargets.Contains(t, StringComparer.Ordinal))
                throw Invalid("$.typescript.target",
                    $"value \"{t}\" is not one of [{string.Join(", ", AllowedTargets)}].");
        }

        if (ts.TryGetProperty("format", out var format))
        {
            if (format.ValueKind != JsonValueKind.String)
                throw Invalid("$.typescript.format", "must be a string.");
            var f = format.GetString()!;
            if (!AllowedFormats.Contains(f, StringComparer.Ordinal))
                throw Invalid("$.typescript.format",
                    $"value \"{f}\" is not one of [{string.Join(", ", AllowedFormats)}].");
        }
    }

    private static void ValidateDebug(JsonElement debug)
    {
        if (debug.ValueKind != JsonValueKind.Object)
            throw Invalid("$.debug", "must be an object.");

        RejectUnknownProperties(debug, "$.debug", "sourceMap");

        if (debug.TryGetProperty("sourceMap", out var sm))
        {
            if (sm.ValueKind != JsonValueKind.Object)
                throw Invalid("$.debug.sourceMap", "must be an object.");

            RejectUnknownProperties(sm, "$.debug.sourceMap", "enabled");
            CheckOptionalBool(sm, "enabled", "$.debug.sourceMap");
        }
    }

    private static void ValidateRelease(JsonElement release)
    {
        if (release.ValueKind != JsonValueKind.Object)
            throw Invalid("$.release", "must be an object.");

        RejectUnknownProperties(release, "$.release", "minify");

        if (release.TryGetProperty("minify", out var minify))
        {
            if (minify.ValueKind != JsonValueKind.Object)
                throw Invalid("$.release.minify", "must be an object.");

            RejectUnknownProperties(minify, "$.release.minify", "level", "keepNames");

            if (minify.TryGetProperty("level", out var level))
            {
                if (level.ValueKind != JsonValueKind.String)
                    throw Invalid("$.release.minify.level", "must be a string.");
                var lv = level.GetString()!;
                if (!AllowedMinifyLevels.Contains(lv, StringComparer.Ordinal))
                    throw Invalid("$.release.minify.level",
                        $"value \"{lv}\" is not one of [{string.Join(", ", AllowedMinifyLevels)}].");
            }

            CheckOptionalBool(minify, "keepNames", "$.release.minify");
        }
    }

    private static void CheckOptionalBool(JsonElement obj, string property, string parentPath)
    {
        if (obj.TryGetProperty(property, out var value))
        {
            if (value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False)
                throw Invalid($"{parentPath}.{property}", "must be a boolean.");
        }
    }

    private static void RequireProperty(JsonElement obj, string name, string parentPath)
    {
        if (!obj.TryGetProperty(name, out _))
            throw Invalid(parentPath, $"required property \"{name}\" is missing.");
    }

    private static void RejectUnknownProperties(JsonElement obj, string path, params string[] allowed)
    {
        var allowedSet = new HashSet<string>(allowed, StringComparer.Ordinal);
        foreach (var prop in obj.EnumerateObject())
        {
            if (!allowedSet.Contains(prop.Name))
            {
                throw Invalid(path,
                    $"unknown property \"{prop.Name}\". Allowed: [{string.Join(", ", allowed)}].");
            }
        }
    }

    private static TsHandlerException Invalid(string path, string message)
    {
        return new TsHandlerException(
            ErrorCodes.ConfigValidationFailed,
            $"{path}: {message}",
            exitCode: 3);
    }
}