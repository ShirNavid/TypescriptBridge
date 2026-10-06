using System.Text.Json;
using System.Text.Json.Serialization;

namespace TypescriptBridge.Tool;

public sealed class Config
{
    [JsonPropertyName("output")]
    public OutputConfig Output { get; init; } = new();

    [JsonPropertyName("bundle")]
    public BundleConfig Bundle { get; init; } = new();

    [JsonPropertyName("release-minify")]
    public ReleaseMinifyConfig ReleaseMinify { get; init; } = new();

    [JsonPropertyName("run")]
    public RunConfig Run { get; init; } = new();

    [JsonPropertyName("isTestProject")]
    public bool IsTestProject { get; init; }

    [JsonPropertyName("entrypoint")]
    public string Entrypoint { get; init; } = "src/app.ts";

    [JsonPropertyName("testEntrypoint")]
    public string TestEntrypoint { get; init; } = "tests/index.ts";

    // Named application bundles. Null retains the legacy single-entrypoint config.
    [JsonPropertyName("entrypoints")]
    public Dictionary<string, EntryPointConfig>? EntryPoints { get; init; }

    public IReadOnlyList<(string Name, string FieldName, string Source)> GetBuildEntries()
    {
        if (IsTestProject)
            return new[] { ("test", Output.FieldName, TestEntrypoint) };

        if (EntryPoints is not null)
            return EntryPoints.Select(entry =>
                (entry.Key, entry.Value.FieldName, entry.Value.Source)).ToArray();

        return new[] { ("main", Output.FieldName, Entrypoint) };
    }

    public static Config Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new TypescriptBridgeException(
                ErrorCodes.ConfigInvalidJson,
                $"Config file not found: {path}");
        }

        string json;

        try
        {
            json = File.ReadAllText(path);
        }
        catch (IOException ex)
        {
            throw new TypescriptBridgeException(
                ErrorCodes.ConfigInvalidJson,
                $"Failed to read config: {ex.Message}");
        }

        JsonDocument doc;

        try
        {
            doc = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });
        }
        catch (JsonException ex)
        {
            throw new TypescriptBridgeException(
                ErrorCodes.ConfigInvalidJson,
                $"Invalid JSON in {path}: {ex.Message}");
        }

        using (doc)
        {
            SchemaValidator.Validate(doc.RootElement);
        }

        Config? config;

        try
        {
            config = JsonSerializer.Deserialize<Config>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new TypescriptBridgeException(
                ErrorCodes.ConfigInvalidJson,
                $"Invalid JSON in {path}: {ex.Message}");
        }

        if (config is null)
        {
            throw new TypescriptBridgeException(
                ErrorCodes.ConfigInvalidJson,
                $"Config deserialized to null: {path}");
        }

        return config;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };
}

public sealed class EntryPointConfig
{
    [JsonPropertyName("fieldName")]
    public required string FieldName { get; init; }

    [JsonPropertyName("source")]
    public required string Source { get; init; }
}
public sealed class OutputConfig
{
    [JsonPropertyName("className")]
    public string ClassName { get; init; } = "TypescriptProvider";

    [JsonPropertyName("fieldName")]
    public string FieldName { get; init; } = "TypescriptCode";
}

// Bundler-level options that affect the emitted JavaScript shape.
//
// Note: the TypeScript target is NOT part of this class. The target
// lives in tsconfig.json (compilerOptions.target), which is the single
// source of truth for TypeScript compiler semantics. The Tool reads
// the target from tsconfig.json and passes it to EsbuildRunner as a
// separate parameter.
public sealed class BundleConfig
{
    [JsonPropertyName("format")]
    public string Format { get; init; } = "iife";
}

public sealed class ReleaseMinifyConfig
{
    [JsonPropertyName("level")]
    public string Level { get; init; } = "aggressive";

    [JsonPropertyName("keepNames")]
    public bool KeepNames { get; init; } = true;
}

public sealed class RunConfig
{
    [JsonPropertyName("entrypoint")]
    public string Entrypoint { get; init; } = "main";

    [JsonPropertyName("browser")]
    public string Browser { get; init; } = "edge";

    // Port used by the debug HTTP host. The generated launch.json
    // references this exact value, so it must be stable between build
    // and run.
    [JsonPropertyName("port")]
    public int Port { get; init; } = 45000;
}
