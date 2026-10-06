# Runs the generated-template checks in an isolated workspace.
# The workspace is removed after the run unless -KeepArtifacts is supplied.
[CmdletBinding()]
param([switch] $KeepArtifacts)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$objRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'obj'))
$workRoot = Join-Path $objRoot ('TemplateSmoke-' + [Guid]::NewGuid().ToString('N'))
$projectDir = Join-Path $workRoot 'SmokeApp'
$projectPath = Join-Path $projectDir 'SmokeApp.csproj'
$packageDir = Join-Path $workRoot 'packages'
$feedDir = Join-Path $workRoot 'feed'
$hiveDir = Join-Path $workRoot 'hive'
$buildPackage = Join-Path $repoRoot 'src\TypescriptBridge.Build\TypescriptBridge.Build.csproj'
$toolProject = Join-Path $repoRoot 'src\TypescriptBridge.Tool\TypescriptBridge.Tool.csproj'
$templateContent = Join-Path $repoRoot 'templates\TypescriptBridge.Template\content'
$encoding = New-Object Text.UTF8Encoding($false)

function Invoke-DotNet([string[]] $arguments) {
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Assert-True([bool] $condition, [string] $message) {
    if (-not $condition) { throw $message }
}

try {
    New-Item -ItemType Directory -Path $workRoot, $feedDir -Force | Out-Null

    # Pack the current sources so NuGet cannot silently use an older package.
    Invoke-DotNet -arguments @('build', $toolProject, '-c', 'Release', '-v:q')
    Invoke-DotNet -arguments @('pack', $buildPackage, '-c', 'Release', '-o', $feedDir, '-v:q')

    Invoke-DotNet -arguments @('new', '--debug:custom-hive', $hiveDir, 'install', $templateContent, '--force')
    Invoke-DotNet -arguments @('new', '--debug:custom-hive', $hiveDir, 'typescript-bridge', '-n', 'SmokeApp', '-o', $projectDir)

    [xml] $solution = [IO.File]::ReadAllText((Join-Path $projectDir 'SmokeApp.slnx'))
    Assert-True (@($solution.Solution.Project).Count -eq 1) 'The template did not generate exactly one project.'
    Assert-True (-not (Test-Path (Join-Path $projectDir 'SmokeApp.esproj'))) 'An old .esproj was generated.'
    Assert-True (-not (Test-Path (Join-Path $projectDir 'SmokeApp.Provider.csproj'))) 'An old Provider project was generated.'

    Invoke-DotNet -arguments @('restore', $projectPath, '--packages', $packageDir, "-p:RestoreSources=$feedDir", '-v:q')
    Invoke-DotNet -arguments @('build', $projectPath, '--no-restore', '-v:q')

    $modulePath = Join-Path $projectDir 'ts\src\typescript-bridge\typescript-bridge.ts'
    Assert-True (Test-Path -LiteralPath $modulePath) 'Importable TypeScript module is missing.'
    Assert-True (-not (Test-Path (Join-Path $projectDir 'ts\src\default-definitions\typescript-bridge.d.ts'))) 'Old global declarations remain.'
    $module = [IO.File]::ReadAllText($modulePath)
    Assert-True ($module.Contains('export type BUILD_MODE = "DEBUG" | "RELEASE";')) 'BUILD_MODE union type is missing.'
    $bridgePath = Join-Path $projectDir 'Bridge.cs'
    $bridge = [IO.File]::ReadAllText($bridgePath)
    Assert-True ($bridge.Contains('namespace SmokeApp;')) 'Bridge.cs is outside the project namespace.'
    Assert-True ($bridge.Contains('public static class TypescriptProvider')) 'The default provider is missing.'

    # Changing config.json must regenerate the public C# API.
    $configPath = Join-Path $projectDir 'config.json'
    $config = [IO.File]::ReadAllText($configPath)
    Assert-True ($config.Contains('"className": "TypescriptProvider"')) 'Unexpected template class name.'
    Assert-True ($config.Contains('"fieldName": "TypescriptCode"')) 'Unexpected template field name.'
    $config = $config.Replace('"className": "TypescriptProvider"', '"className": "SmokeProvider"')
    $config = $config.Replace('"fieldName": "TypescriptCode"', '"fieldName": "SmokeCode"')
    [IO.File]::WriteAllText($configPath, $config, $encoding)
    Invoke-DotNet -arguments @('build', $projectPath, '--no-restore', '-v:q')
    $bridgePath = Join-Path $projectDir 'Bridge.cs'
    $bridge = [IO.File]::ReadAllText($bridgePath)
    Assert-True ($bridge.Contains('public static class SmokeProvider')) 'config.json did not update the class.'
    Assert-True ($bridge.Contains('public static readonly string SmokeCode')) 'config.json did not update the field.'

    # Named entrypoints must generate independent fields in one provider.
    $chartPath = Join-Path $projectDir 'ts\src\chart.ts'
    $reportPath = Join-Path $projectDir 'ts\src\report.ts'
    [IO.File]::WriteAllText($chartPath, 'console.log("chart payload");', $encoding)
    [IO.File]::WriteAllText($reportPath, 'console.log("report payload");', $encoding)
    $settings = [IO.File]::ReadAllText($configPath) | ConvertFrom-Json
    $settings.entrypoints | Add-Member -NotePropertyName chart -NotePropertyValue ([pscustomobject]@{ fieldName = 'ChartCode'; source = 'src/chart.ts' })
    $settings.entrypoints | Add-Member -NotePropertyName report -NotePropertyValue ([pscustomobject]@{ fieldName = 'ReportCode'; source = 'src/report.ts' })
    $settings.run.entrypoint = 'chart'
    [IO.File]::WriteAllText($configPath, ($settings | ConvertTo-Json -Depth 12), $encoding)
    Invoke-DotNet -arguments @('build', $projectPath, '--no-restore', '-v:q')
    $bridgePath = Join-Path $projectDir 'Bridge.cs'
    $bridge = [IO.File]::ReadAllText($bridgePath)
    Assert-True ($bridge.Contains('public static readonly string SmokeCode')) 'Main field is missing.'
    Assert-True ($bridge.Contains('public static readonly string ChartCode')) 'Chart field is missing.'
    Assert-True ($bridge.Contains('public static readonly string ReportCode')) 'Report field is missing.'
    Assert-True ($bridge.Contains('chart payload') -and $bridge.Contains('report payload')) 'Separate bundles are missing.'

    # Existing single-entrypoint configs still generate the original field.
    $settings.PSObject.Properties.Remove('entrypoints')
    $settings.output | Add-Member -NotePropertyName fieldName -NotePropertyValue 'SmokeCode'
    $settings.run.entrypoint = 'main'
    $settings | Add-Member -NotePropertyName entrypoint -NotePropertyValue 'src/app.ts'
    [IO.File]::WriteAllText($configPath, ($settings | ConvertTo-Json -Depth 12), $encoding)
    Invoke-DotNet -arguments @('build', $projectPath, '--no-restore', '-v:q')
    $bridgePath = Join-Path $projectDir 'Bridge.cs'
    $bridge = [IO.File]::ReadAllText($bridgePath)
    Assert-True ($bridge.Contains('public static readonly string SmokeCode')) 'Legacy field is missing.'
    Assert-True (-not $bridge.Contains('public static readonly string ChartCode')) 'Old named field remains in legacy mode.'

    # ES2020 preserves optional chaining; ES2019 must transform it.
    $appPath = Join-Path $projectDir 'ts\src\app.ts'
    [IO.File]::AppendAllText($appPath, "`nconsole.log(globalThis?.document?.title ?? 'fallback');`n", $encoding)
    Invoke-DotNet -arguments @('build', $projectPath, '--no-restore', '-v:q')
    $bridgePath = Join-Path $projectDir 'Bridge.cs'
    $bridge = [IO.File]::ReadAllText($bridgePath)
    Assert-True ($bridge.Contains('globalThis?.document?.title')) 'ES2020 did not preserve optional chaining.'

    $tsconfigPath = Join-Path $projectDir 'ts\tsconfig.json'
    $tsconfig = [IO.File]::ReadAllText($tsconfigPath)
    Assert-True ($tsconfig.Contains('"target": "es2020"')) 'Unexpected template TypeScript target.'
    [IO.File]::WriteAllText($tsconfigPath, $tsconfig.Replace('"target": "es2020"', '"target": "es2019"'), $encoding)
    Invoke-DotNet -arguments @('build', $projectPath, '--no-restore', '-v:q')
    $bridgePath = Join-Path $projectDir 'Bridge.cs'
    $bridge = [IO.File]::ReadAllText($bridgePath)
    Assert-True (-not $bridge.Contains('globalThis?.document?.title')) 'tsconfig.json did not change the generated JavaScript.'
    Assert-True ($bridge.Contains('fallback')) 'The transformed expression is missing from Bridge.cs.'

    # A separate C# application must be able to consume the generated field.
    $consumerDir = Join-Path $workRoot 'Consumer'
    New-Item -ItemType Directory -Path $consumerDir | Out-Null
    $consumerProject = Join-Path $consumerDir 'Consumer.csproj'
    [IO.File]::WriteAllText($consumerProject, @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework></PropertyGroup>
  <ItemGroup><ProjectReference Include="..\SmokeApp\SmokeApp.csproj" /></ItemGroup>
</Project>
'@, $encoding)
    [IO.File]::WriteAllText((Join-Path $consumerDir 'Program.cs'), @'
using SmokeApp;
if (!SmokeProvider.SmokeCode.Contains("fallback"))
    throw new Exception("The generated JavaScript was not embedded.");
Console.WriteLine("C# consumer passed.");
'@, $encoding)
    Invoke-DotNet -arguments @('restore', $consumerProject, '--packages', $packageDir, "-p:RestoreSources=$feedDir", '-v:q')
    Invoke-DotNet -arguments @('run', '--project', $consumerProject, '--no-restore', '-v:q')

    Write-Host 'Template smoke test passed.' -ForegroundColor Green
}
finally {
    if (-not $KeepArtifacts -and (Test-Path -LiteralPath $workRoot)) {
        # Check the resolved target before any recursive removal.
        $resolvedTarget = (Resolve-Path -LiteralPath $workRoot).Path
        if ([IO.Path]::GetDirectoryName($resolvedTarget) -ne $objRoot -or
            -not [IO.Path]::GetFileName($resolvedTarget).StartsWith('TemplateSmoke-')) {
            throw "Refusing to remove unexpected path: $resolvedTarget"
        }
        $targetItem = Get-Item -LiteralPath $resolvedTarget -Force
        if (($targetItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Refusing to remove a reparse point: $resolvedTarget"
        }
        Remove-Item -LiteralPath $resolvedTarget -Recurse -Force
    }
}
