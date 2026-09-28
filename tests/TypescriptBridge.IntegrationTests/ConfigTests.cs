using TypescriptBridge.Tool;

namespace TypescriptBridge.IntegrationTests;

// Tests for Config.Load, which reads typescript-bridge.json, validates it
// against the schema, and deserializes it into a Config object.
public sealed class ConfigTests : IDisposable
{
    // Directory used for temporary config files created during tests.
    private readonly string _tempDir;

    // Creates a unique temporary directory for each test instance.
    public ConfigTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TypescriptBridgeTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    // Removes the temporary directory after each test.
    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    // Writes a config file into the temp directory and returns its path.
    private string WriteConfig(string content)
    {
        var path = Path.Combine(_tempDir, "typescript-bridge.json");
        File.WriteAllText(path, content);
        return path;
    }

    // Verifies that a complete valid configuration is read correctly.
    [Fact]
    public void Load_ReadsCompleteValidConfig()
    {
        var path = WriteConfig("""
            {
              "output": {
                "className": "MyProvider",
                "fieldName": "MyCode"
              },
              "typescript": {
                "target": "es2022",
                "format": "esm"
              },
              "minify": {
                "level": "full",
                "keepNames": false
              }
            }
            """);

        var config = Config.Load(path);

        Assert.Equal("MyProvider", config.Output.ClassName);
        Assert.Equal("MyCode", config.Output.FieldName);
        Assert.Equal("es2022", config.TypeScript.Target);
        Assert.Equal("esm", config.TypeScript.Format);
        Assert.Equal("full", config.Minify.Level);
        Assert.False(config.Minify.KeepNames);
    }

    // Verifies that an empty object applies all defaults.
    [Fact]
    public void Load_AppliesDefaultsForEmptyObject()
    {
        var path = WriteConfig("{}");

        var config = Config.Load(path);

        Assert.Equal("TypescriptProvider", config.Output.ClassName);
        Assert.Equal("TypescriptCode", config.Output.FieldName);
        Assert.Equal("es2020", config.TypeScript.Target);
        Assert.Equal("iife", config.TypeScript.Format);
        Assert.Equal("aggressive", config.Minify.Level);
        Assert.True(config.Minify.KeepNames);
    }

    // Verifies that comments in the JSON file are ignored.
    [Fact]
    public void Load_AcceptsCommentsInJson()
    {
        var path = WriteConfig("""
            {
              // This is a comment.
              "output": {
                "className": "MyProvider" // trailing comment
              }
            }
            """);

        var config = Config.Load(path);

        Assert.Equal("MyProvider", config.Output.ClassName);
    }

    // Verifies that trailing commas are accepted.
    [Fact]
    public void Load_AcceptsTrailingCommas()
    {
        var path = WriteConfig("""
            {
              "output": {
                "className": "MyProvider",
              },
            }
            """);

        var config = Config.Load(path);

        Assert.Equal("MyProvider", config.Output.ClassName);
    }

    // Verifies that a missing file reports ConfigInvalidJson.
    [Fact]
    public void Load_RejectsMissingFile()
    {
        var path = Path.Combine(_tempDir, "does-not-exist.json");

        var exception = Assert.Throws<TypescriptBridgeException>(
            () => Config.Load(path));

        Assert.Equal(ErrorCodes.ConfigInvalidJson, exception.Code);
        Assert.Contains("not found", exception.Message);
    }

    // Verifies that a syntactically invalid JSON reports ConfigInvalidJson.
    [Fact]
    public void Load_RejectsInvalidJson()
    {
        var path = WriteConfig("{ this is not valid json }");

        var exception = Assert.Throws<TypescriptBridgeException>(
            () => Config.Load(path));

        Assert.Equal(ErrorCodes.ConfigInvalidJson, exception.Code);
    }

    // Verifies that an unknown property is reported as a validation failure.
    [Fact]
    public void Load_RejectsUnknownProperty()
    {
        var path = WriteConfig("""{ "unknown": true }""");

        var exception = Assert.Throws<TypescriptBridgeException>(
            () => Config.Load(path));

        Assert.Equal(ErrorCodes.ConfigValidationFailed, exception.Code);
    }

    // Verifies that an invalid target value is reported as a validation failure.
    [Fact]
    public void Load_RejectsInvalidTarget()
    {
        var path = WriteConfig("""{ "typescript": { "target": "es1999" } }""");

        var exception = Assert.Throws<TypescriptBridgeException>(
            () => Config.Load(path));

        Assert.Equal(ErrorCodes.ConfigValidationFailed, exception.Code);
    }
}
