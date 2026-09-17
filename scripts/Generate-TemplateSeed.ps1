# ============================================================================
# Generate-TemplateSeed.ps1
# ============================================================================

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$toolDll  = Join-Path $repoRoot "src\TsHandler.Tool\bin\Release\net8.0\TsHandler.Tool.dll"
$templateContent = Join-Path $repoRoot "templates\TsHandler.Template\content"
$esbuildExe = Join-Path $repoRoot "tools\esbuild\win-x64\esbuild.exe"
$intermediate = Join-Path $repoRoot "obj\template-seed"

if (-not (Test-Path $toolDll)) {
    throw "Tool DLL not found: $toolDll. Build the Tool first: dotnet build src\TsHandler.Tool -c Release"
}

if (-not (Test-Path $esbuildExe)) {
    throw "esbuild not found: $esbuildExe"
}

Remove-Item $intermediate -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $intermediate | Out-Null

Write-Host "Generating seed from template content..." -ForegroundColor Cyan
Write-Host "  Tool:         $toolDll"
Write-Host "  Content:      $templateContent"
Write-Host "  esbuild:      $esbuildExe"
Write-Host "  Intermediate: $intermediate"
Write-Host ""

& dotnet exec $toolDll `
    --config (Join-Path $templateContent "ts-handler.json") `
    --project $templateContent `
    --intermediate $intermediate `
    --configuration "Debug" `
    --esbuild $esbuildExe

if ($LASTEXITCODE -ne 0) {
    throw "Tool failed with exit code $LASTEXITCODE"
}

$generated = Join-Path $templateContent "TypescriptProvider.cs"
$final = Join-Path $templateContent "TypescriptProvider.cs.template"

if (-not (Test-Path $generated)) {
    throw "Expected generated file not found: $generated"
}

Move-Item $generated $final -Force

Write-Host ""
Write-Host "Seed generated: $final" -ForegroundColor Green
Write-Host "  Size: $((Get-Item $final).Length) bytes"