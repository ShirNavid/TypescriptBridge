using TypescriptBridge.Tool;

namespace TypescriptBridge.IntegrationTests;

// Tests for the minify/minification behavior of EsbuildRunner.
// These tests verify that DEBUG builds never receive minification flags,
// and that release builds respect the release-minify configuration.
public sealed class EsbuildRunnerTests
{
    // Builds the argument list for a simple invocation with the given settings.
    private static List<string> BuildArgsWithMinify(
        string buildMode,
        string level,
        bool keepNames)
    {
        var args = new List<string>();
        var ts = new TypeScriptConfig
        {
            Target = "es2020",
            Format = "iife",
        };
        var releaseMinify = new ReleaseMinifyConfig
        {
            Level = level,
            KeepNames = keepNames,
        };

        EsbuildRunner.AddMinifyArgs(args, releaseMinify, buildMode);

        return args;
    }

    // In DEBUG, no minify flags must be added, regardless of the config.
    [Fact]
    public void AddMinifyArgs_Debug_AddsNoFlags()
    {
        var args = BuildArgsWithMinify("DEBUG", "aggressive", keepNames: true);

        Assert.Empty(args);
    }

    // In DEBUG, even "off" level should not produce flags.
    [Fact]
    public void AddMinifyArgs_DebugWithOff_AddsNoFlags()
    {
        var args = BuildArgsWithMinify("DEBUG", "off", keepNames: false);

        Assert.Empty(args);
    }

    // In RELEASE with level "off", no minify flags are added.
    [Fact]
    public void AddMinifyArgs_ReleaseWithOff_AddsNoFlags()
    {
        var args = BuildArgsWithMinify("RELEASE", "off", keepNames: true);

        Assert.Empty(args);
    }

    // In RELEASE with level "light", only --minify-whitespace is added.
    [Fact]
    public void AddMinifyArgs_ReleaseWithLight_AddsWhitespaceFlag()
    {
        var args = BuildArgsWithMinify("RELEASE", "light", keepNames: false);

        Assert.Contains("--minify-whitespace", args);
        Assert.DoesNotContain("--minify-syntax", args);
        Assert.DoesNotContain("--minify", args);
    }

    // In RELEASE with level "full", both whitespace and syntax flags are added.
    [Fact]
    public void AddMinifyArgs_ReleaseWithFull_AddsWhitespaceAndSyntax()
    {
        var args = BuildArgsWithMinify("RELEASE", "full", keepNames: false);

        Assert.Contains("--minify-whitespace", args);
        Assert.Contains("--minify-syntax", args);
        Assert.DoesNotContain("--minify", args);
    }

    // In RELEASE with level "aggressive" and keepNames true,
    // both --minify and --keep-names are added.
    [Fact]
    public void AddMinifyArgs_ReleaseAggressiveWithKeepNames_AddsMinifyAndKeepNames()
    {
        var args = BuildArgsWithMinify("RELEASE", "aggressive", keepNames: true);

        Assert.Contains("--minify", args);
        Assert.Contains("--keep-names", args);
    }

    // In RELEASE with level "aggressive" and keepNames false,
    // only --minify is added.
    [Fact]
    public void AddMinifyArgs_ReleaseAggressiveWithoutKeepNames_AddsMinifyOnly()
    {
        var args = BuildArgsWithMinify("RELEASE", "aggressive", keepNames: false);

        Assert.Contains("--minify", args);
        Assert.DoesNotContain("--keep-names", args);
    }

    // An unknown level throws a bridge exception.
    [Fact]
    public void AddMinifyArgs_UnknownLevel_Throws()
    {
        var ex = Assert.Throws<TypescriptBridgeException>(
            () => BuildArgsWithMinify("RELEASE", "extreme", keepNames: false));

        Assert.Equal(ErrorCodes.ConfigValidationFailed, ex.Code);
    }

    // BuildArgs always contains the BUILD_MODE define.
    [Fact]
    public void BuildArgs_IncludesBuildModeDefine()
    {
        var ts = new TypeScriptConfig
        {
            Target = "es2020",
            Format = "iife",
        };

        var args = EsbuildRunner.BuildArgs(
            "entry.ts",
            "out.js",
            ts,
            "DEBUG");

        Assert.Contains("--define:BUILD_MODE=\"DEBUG\"", args);
    }

    // BuildArgs includes the target, format, and outfile.
    [Fact]
    public void BuildArgs_IncludesTargetFormatOutfile()
    {
        var ts = new TypeScriptConfig
        {
            Target = "es2022",
            Format = "esm",
        };

        var args = EsbuildRunner.BuildArgs(
            "entry.ts",
            "out.js",
            ts,
            "RELEASE");

        Assert.Contains("--target=es2022", args);
        Assert.Contains("--format=esm", args);
        Assert.Contains("--outfile=out.js", args);
        Assert.Contains("--bundle", args);
    }
}
