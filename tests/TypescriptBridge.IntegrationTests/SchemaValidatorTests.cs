using System.Text.Json;
using TypescriptBridge.Tool;

namespace TypescriptBridge.IntegrationTests;

// Tests for SchemaValidator, which rejects malformed or unknown configuration
// properties before the config is deserialized.
public sealed class SchemaValidatorTests
{
    // Parses a JSON string into a JsonElement for use in tests.
    private static JsonElement Parse(string json)
    {
        var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    [Theory]
    [InlineData("""{ "entrypoints": { "main": { "fieldName": "Code", "source": "src/app.ts" }, "chart": { "fieldName": "Code", "source": "src/chart.ts" } } }""", "unique")]
    [InlineData("""{ "entrypoints": { "main": { "fieldName": "Code", "source": "../other.ts" } } }""", "under ts/")]
    [InlineData("""{ "entrypoints": { "main": { "fieldName": "Code", "source": "src/app.ts" } }, "run": { "entrypoint": "chart" } }""", "unknown entrypoint")]
    public void Validate_RejectsInvalidNamedEntrypoints(string json, string message)
    {
        var exception = Assert.Throws<TypescriptBridgeException>(() => SchemaValidator.Validate(Parse(json)));
        Assert.Contains(message, exception.Message);
    }

    // Verifies that a fully-specified valid configuration passes validation.
    [Fact]
    public void Validate_AcceptsCompleteValidConfig()
    {
        var json = """
            {
              "output": {
                "className": "TypescriptProvider",
                "fieldName": "TypescriptCode"
              },
              "bundle": {
                "format": "iife"
              },
              "release-minify": {
                "level": "aggressive",
                "keepNames": true
              }
            }
            """;

        var exception = Record.Exception(() => SchemaValidator.Validate(Parse(json)));

        Assert.Null(exception);
    }

    // Verifies that an empty object passes validation, because every section
    // has a default value.
    [Fact]
    public void Validate_AcceptsEmptyObject()
    {
        var exception = Record.Exception(() => SchemaValidator.Validate(Parse("{}")));

        Assert.Null(exception);
    }

    // Verifies that an unknown property at the root is rejected.
    [Fact]
    public void Validate_RejectsUnknownRootProperty()
    {
        var json = """{ "unknown": 42 }""";

        var exception = Assert.Throws<TypescriptBridgeException>(
            () => SchemaValidator.Validate(Parse(json)));

        Assert.Equal(ErrorCodes.ConfigValidationFailed, exception.Code);
        Assert.Contains("unknown", exception.Message);
    }

    // Verifies that an unknown property inside output is rejected.
    [Fact]
    public void Validate_RejectsUnknownOutputProperty()
    {
        var json = """{ "output": { "bogus": "x" } }""";

        var exception = Assert.Throws<TypescriptBridgeException>(
            () => SchemaValidator.Validate(Parse(json)));

        Assert.Equal(ErrorCodes.ConfigValidationFailed, exception.Code);
    }

    // Verifies that the obsolete "typescript" section is rejected with a
    // clear migration message.
    [Fact]
    public void Validate_RejectsObsoleteTypeScriptSection()
    {
        var json = """{ "typescript": { "target": "es2020" } }""";

        var exception = Assert.Throws<TypescriptBridgeException>(
            () => SchemaValidator.Validate(Parse(json)));

        Assert.Equal(ErrorCodes.ConfigValidationFailed, exception.Code);
        Assert.Contains("no longer supported", exception.Message);
        Assert.Contains("tsconfig.json", exception.Message);
        Assert.Contains("bundle.format", exception.Message);
    }

    // Verifies that an unsupported bundle format value is rejected.
    [Fact]
    public void Validate_RejectsUnknownFormat()
    {
        var json = """{ "bundle": { "format": "umd" } }""";

        var exception = Assert.Throws<TypescriptBridgeException>(
            () => SchemaValidator.Validate(Parse(json)));

        Assert.Equal(ErrorCodes.ConfigValidationFailed, exception.Code);
        Assert.Contains("umd", exception.Message);
    }

    // Verifies that an unknown property inside bundle is rejected.
    [Fact]
    public void Validate_RejectsUnknownBundleProperty()
    {
        var json = """{ "bundle": { "unknownSetting": true } }""";

        var exception = Assert.Throws<TypescriptBridgeException>(
            () => SchemaValidator.Validate(Parse(json)));

        Assert.Equal(ErrorCodes.ConfigValidationFailed, exception.Code);
    }

    // Verifies that an unsupported minify level is rejected.
    [Fact]
    public void Validate_RejectsUnknownMinifyLevel()
    {
        var json = """{ "release-minify": { "level": "extreme" } }""";

        var exception = Assert.Throws<TypescriptBridgeException>(
            () => SchemaValidator.Validate(Parse(json)));

        Assert.Equal(ErrorCodes.ConfigValidationFailed, exception.Code);
        Assert.Contains("extreme", exception.Message);
    }

    // Verifies that a non-boolean keepNames is rejected.
    [Fact]
    public void Validate_RejectsNonBooleanKeepNames()
    {
        var json = """{ "release-minify": { "keepNames": "yes" } }""";

        var exception = Assert.Throws<TypescriptBridgeException>(
            () => SchemaValidator.Validate(Parse(json)));

        Assert.Equal(ErrorCodes.ConfigValidationFailed, exception.Code);
    }

    // Verifies that an invalid identifier for className is rejected.
    [Fact]
    public void Validate_RejectsInvalidClassName()
    {
        var json = """{ "output": { "className": "123abc" } }""";

        var exception = Assert.Throws<TypescriptBridgeException>(
            () => SchemaValidator.Validate(Parse(json)));

        Assert.Equal(ErrorCodes.ConfigValidationFailed, exception.Code);
    }

    // Verifies that a C# keyword as a className is rejected.
    [Fact]
    public void Validate_RejectsCSharpKeywordAsClassName()
    {
        var json = """{ "output": { "className": "class" } }""";

        var exception = Assert.Throws<TypescriptBridgeException>(
            () => SchemaValidator.Validate(Parse(json)));

        Assert.Equal(ErrorCodes.ConfigValidationFailed, exception.Code);
        Assert.Contains("keyword", exception.Message);
    }

    // Verifies that an empty className is rejected.
    [Fact]
    public void Validate_RejectsEmptyClassName()
    {
        var json = """{ "output": { "className": "" } }""";

        var exception = Assert.Throws<TypescriptBridgeException>(
            () => SchemaValidator.Validate(Parse(json)));

        Assert.Equal(ErrorCodes.ConfigValidationFailed, exception.Code);
    }

    // Verifies that a valid custom fieldName passes.
    [Fact]
    public void Validate_AcceptsValidFieldName()
    {
        var json = """{ "output": { "fieldName": "MyCode123" } }""";

        var exception = Record.Exception(() => SchemaValidator.Validate(Parse(json)));

        Assert.Null(exception);
    }

    // Verifies that a non-object root is rejected.
    [Fact]
    public void Validate_RejectsNonObjectRoot()
    {
        var json = """[1, 2, 3]""";

        var exception = Assert.Throws<TypescriptBridgeException>(
            () => SchemaValidator.Validate(Parse(json)));

        Assert.Equal(ErrorCodes.ConfigValidationFailed, exception.Code);
    }

    // -------------------------------------------------------------------------
    // Cross-copy invariant: the two shipped schema copies must be identical.
    // -------------------------------------------------------------------------

    // The repository ships config.schema.json in two places:
    //   src\TypescriptBridge.Build\schema\config.schema.json
    //   templates\TypescriptBridge.Template\content\config.schema.json
    // They must stay byte-identical so that VS IntelliSense and the Tool
    // validator agree.
    [Fact]
    public void Schemas_AreIdentical()
    {
        // Locate the repository root by walking up from the test assembly.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        DirectoryInfo? root = null;
        for (int depth = 0; depth < 10 && dir is not null; depth++)
        {
            if (File.Exists(Path.Combine(dir.FullName, "TypescriptBridge.slnx")))
            {
                root = dir;
                break;
            }
            dir = dir.Parent;
        }

        Assert.NotNull(root);

        var schemaA = Path.Combine(root!.FullName,
            "src", "TypescriptBridge.Build", "schema", "config.schema.json");
        var schemaB = Path.Combine(root.FullName,
            "templates", "TypescriptBridge.Template", "content", "config.schema.json");

        Assert.True(File.Exists(schemaA), $"Missing: {schemaA}");
        Assert.True(File.Exists(schemaB), $"Missing: {schemaB}");

        var contentA = File.ReadAllText(schemaA);
        var contentB = File.ReadAllText(schemaB);

        Assert.Equal(contentA, contentB);
    }
}
