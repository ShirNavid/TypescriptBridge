using TypescriptBridge.Tool;

namespace TypescriptBridge.IntegrationTests;

// Tests for Config.Load, which reads config.json, validates it
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
        var path = Path.Combine(_tempDir, "config.json");
        File.WriteAllText(path, content);
        return path;
    }

    // Named entries produce multiple bundles, while test mode keeps one test source.
    [Fact]
    public void Load_ResolvesNamedAndTestEntrypointsSeparately()
    {
        var path = WriteConfig("""
            { "entrypoints": {
                "main": { "fieldName": "MainCode", "source": "src/main.ts" },
                "chart": { "fieldName": "ChartCode", "source": "src/chart.ts" }
              }, "run": { "entrypoint": "chart" } }
            """);
        var config = Config.Load(path);
        Assert.Equal(2, config.GetBuildEntries().Count);
        Assert.Equal("ChartCode", config.GetBuildEntries()[1].FieldName);
        Assert.Equal("chart", config.Run.Entrypoint);

        path = WriteConfig("""
            { "isTestProject": true,
              "entrypoints": { "main": { "fieldName": "MainCode", "source": "src/main.ts" } },
              "testEntrypoint": "tests/index.ts" }
            """);
        config = Config.Load(path);
        Assert.Single(config.GetBuildEntries());
        Assert.Equal("tests/index.ts", config.GetBuildEntries()[0].Source);
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
              "bundle": {
                "format": "esm"
              },
              "release-minify": {
                "level": "full",
                "keepNames": false
              }
            }
            """);

        var config = Config.Load(path);

        Assert.Equal("MyProvider", config.Output.ClassName);
        Assert.Equal("MyCode", config.Output.FieldName);
        Assert.Equal("esm", config.Bundle.Format);
        Assert.Equal("full", config.ReleaseMinify.Level);
        Assert.False(config.ReleaseMinify.KeepNames);
    }

    // Verifies that an empty object applies all defaults.
    [Fact]
    public void Load_AppliesDefaultsForEmptyObject()
    {
        var path = WriteConfig("{}");

        var config = Config.Load(path);

        Assert.Equal("TypescriptProvider", config.Output.ClassName);
        Assert.Equal("TypescriptCode", config.Output.FieldName);
        Assert.Equal("iife", config.Bundle.Format);
        Assert.Equal("aggressive", config.ReleaseMinify.Level);
        Assert.True(config.ReleaseMinify.KeepNames);
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

    // Verifies that the obsolete "typescript" section is rejected with a
    // migration message.
    [Fact]
    public void Load_RejectsObsoleteTypeScriptSection()
    {
        var path = WriteConfig("""{ "typescript": { "target": "es2020", "format": "iife" } }""");

        var exception = Assert.Throws<TypescriptBridgeException>(
            () => Config.Load(path));

        Assert.Equal(ErrorCodes.ConfigValidationFailed, exception.Code);
        Assert.Contains("no longer supported", exception.Message);
        Assert.Contains("tsconfig.json", exception.Message);
        Assert.Contains("bundle.format", exception.Message);
    }

    // Verifies that an invalid bundle format value is rejected.
    [Fact]
    public void Load_RejectsInvalidBundleFormat()
    {
        var path = WriteConfig("""{ "bundle": { "format": "umd" } }""");

        var exception = Assert.Throws<TypescriptBridgeException>(
            () => Config.Load(path));

        Assert.Equal(ErrorCodes.ConfigValidationFailed, exception.Code);
        Assert.Contains("umd", exception.Message);
    }

    // Verifies that an unknown property inside bundle is rejected.
    [Fact]
    public void Load_RejectsUnknownBundleProperty()
    {
        var path = WriteConfig("""{ "bundle": { "unknownSetting": true } }""");

        var exception = Assert.Throws<TypescriptBridgeException>(
            () => Config.Load(path));

        Assert.Equal(ErrorCodes.ConfigValidationFailed, exception.Code);
    }

    // -------------------------------------------------------------------------
    // Run section tests.
    // -------------------------------------------------------------------------

    // Defaults: an empty config must yield edge browser and port 45000.
    [Fact]
    public void Load_UsesDefaultRunConfig()
    {
        var path = WriteConfig("{}");

        var config = Config.Load(path);

        Assert.Equal("edge", config.Run.Browser);
        Assert.Equal(45000, config.Run.Port);
    }

    // Explicit edge browser is accepted.
    [Fact]
    public void Load_ReadsEdgeBrowser()
    {
        var path = WriteConfig("""
            {
              "run": {
                "browser": "edge"
              }
            }
            """);

        var config = Config.Load(path);

        Assert.Equal("edge", config.Run.Browser);
    }

    // Chrome is accepted.
    [Fact]
    public void Load_ReadsChromeBrowser()
    {
        var path = WriteConfig("""
            {
              "run": {
                "browser": "chrome"
              }
            }
            """);

        var config = Config.Load(path);

        Assert.Equal("chrome", config.Run.Browser);
    }

    // Unknown browser rejected.
    [Fact]
    public void Load_RejectsUnknownBrowser()
    {
        var path = WriteConfig("""{ "run": { "browser": "firefox" } }""");

        var exception = Assert.Throws<TypescriptBridgeException>(
            () => Config.Load(path));

        Assert.Equal(ErrorCodes.ConfigValidationFailed, exception.Code);
        Assert.Contains("firefox", exception.Message);
    }

    // Unknown property inside run rejected.
    [Fact]
    public void Load_RejectsUnknownRunProperty()
    {
        var path = WriteConfig("""{ "run": { "unknownSetting": true } }""");

        var exception = Assert.Throws<TypescriptBridgeException>(
            () => Config.Load(path));

        Assert.Equal(ErrorCodes.ConfigValidationFailed, exception.Code);
    }

    // Valid port is accepted.
    [Fact]
    public void Load_ReadsRunPort()
    {
        var path = WriteConfig("""{ "run": { "port": 45678 } }""");

        var config = Config.Load(path);

        Assert.Equal(45678, config.Run.Port);
    }

    // Port out of range rejected.
    [Fact]
    public void Load_RejectsOutOfRangePort()
    {
        var path = WriteConfig("""{ "run": { "port": 99999 } }""");

        var exception = Assert.Throws<TypescriptBridgeException>(
            () => Config.Load(path));

        Assert.Equal(ErrorCodes.ConfigValidationFailed, exception.Code);
    }

    // Port of wrong type rejected.
    [Fact]
    public void Load_RejectsNonNumericPort()
    {
        var path = WriteConfig("""{ "run": { "port": "8080" } }""");

        var exception = Assert.Throws<TypescriptBridgeException>(
            () => Config.Load(path));

        Assert.Equal(ErrorCodes.ConfigValidationFailed, exception.Code);
    }
}
