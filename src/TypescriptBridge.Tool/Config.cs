using System.Text.Json;
using System.Text.Json.Serialization;

namespace TypescriptBridge.Tool;

public sealed class Config
{
    [JsonPropertyName("output")]
    public OutputConfig Output { get; init; } = new();

    [JsonPropertyName("typescript")]
    public TypeScriptConfig TypeScript { get; init; } = new();

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

public sealed class TypeScriptConfig
{
    [JsonPropertyName("target")]
    public string Target { get; init; } = "es2020";

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


