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
              "typescript": {
                "target": "es2020",
                "format": "iife"
              },
              "minify": {
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

    // Verifies that an unsupported target value is rejected.
    [Fact]
    public void Validate_RejectsUnknownTarget()
    {
        var json = """{ "typescript": { "target": "es1999" } }""";

        var exception = Assert.Throws<TypescriptBridgeException>(
            () => SchemaValidator.Validate(Parse(json)));

        Assert.Equal(ErrorCodes.ConfigValidationFailed, exception.Code);
        Assert.Contains("es1999", exception.Message);
    }

    // Verifies that an unsupported format value is rejected.
    [Fact]
    public void Validate_RejectsUnknownFormat()
    {
        var json = """{ "typescript": { "format": "umd" } }""";

        var exception = Assert.Throws<TypescriptBridgeException>(
            () => SchemaValidator.Validate(Parse(json)));

        Assert.Equal(ErrorCodes.ConfigValidationFailed, exception.Code);
        Assert.Contains("umd", exception.Message);
    }

    // Verifies that an unsupported minify level is rejected.
    [Fact]
    public void Validate_RejectsUnknownMinifyLevel()
    {
        var json = """{ "minify": { "level": "extreme" } }""";

        var exception = Assert.Throws<TypescriptBridgeException>(
            () => SchemaValidator.Validate(Parse(json)));

        Assert.Equal(ErrorCodes.ConfigValidationFailed, exception.Code);
        Assert.Contains("extreme", exception.Message);
    }

    // Verifies that a non-boolean keepNames is rejected.
    [Fact]
    public void Validate_RejectsNonBooleanKeepNames()
    {
        var json = """{ "minify": { "keepNames": "yes" } }""";

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
}
