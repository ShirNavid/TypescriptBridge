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
    [JsonPropertyName("browser")]
    public string Browser { get; init; } = "edge";

    // Port used by the debug HTTP host. The generated launch.json
    // references this exact value, so it must be stable between build
    // and run.
    [JsonPropertyName("port")]
    public int Port { get; init; } = 45000;
}
