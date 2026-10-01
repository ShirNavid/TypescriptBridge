using TypescriptBridge.Tool;

namespace TypescriptBridge.IntegrationTests;

// Tests that changes to tsconfig.json reach the esbuild target argument.
public sealed class TsConfigReaderTests : IDisposable
{
    private readonly string _path = Path.Combine(
        Path.GetTempPath(),
        "TypescriptBridgeTsConfig_" + Guid.NewGuid().ToString("N") + ".json");

    public void Dispose()
    {
        if (File.Exists(_path))
            File.Delete(_path);
    }

    // Re-reading the same file must use its latest target on the next build.
    [Fact]
    public void ReadTarget_AfterFileChange_UpdatesEsbuildTarget()
    {
        File.WriteAllText(_path, """{ "compilerOptions": { "target": "es2020" } }""");
        var firstTarget = TsConfigReader.ReadTarget(_path);

        File.WriteAllText(_path, """{ "compilerOptions": { "target": "es2019" } }""");
        var secondTarget = TsConfigReader.ReadTarget(_path);

        var args = EsbuildRunner.BuildArgs(
            "app.ts",
            "out.js",
            secondTarget,
            "iife",
            "DEBUG");

        Assert.Equal("es2020", firstTarget);
        Assert.Equal("es2019", secondTarget);
        Assert.Contains("--target=es2019", args);
        Assert.DoesNotContain("--target=es2020", args);
    }

    // A missing target must fail with an actionable configuration error.
    [Fact]
    public void ReadTarget_MissingTarget_ReportsTsconfigError()
    {
        File.WriteAllText(_path, """{ "compilerOptions": {} }""");

        var error = Assert.Throws<TypescriptBridgeException>(
            () => TsConfigReader.ReadTarget(_path));

        Assert.Equal(ErrorCodes.ConfigValidationFailed, error.Code);
        Assert.Contains("compilerOptions.target", error.Message);
    }
}
