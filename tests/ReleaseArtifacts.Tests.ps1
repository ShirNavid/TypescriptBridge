# Checks the contents of already-built release archives without opening image data.
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$releaseDir = Join-Path $repoRoot 'artifacts'
[xml] $props = [IO.File]::ReadAllText((Join-Path $repoRoot 'Directory.Build.props'))
$version = $props.Project.PropertyGroup.Version
[xml] $sourceManifest = [IO.File]::ReadAllText((Join-Path $repoRoot 'src/TypescriptBridge.Vsix/source.extension.vsixmanifest'))
[xml] $sourceProject = [IO.File]::ReadAllText((Join-Path $repoRoot 'templates/TypescriptBridge.Template/content/TypescriptProject.csproj'))
$sourceReference = @($sourceProject.Project.ItemGroup.PackageReference | Where-Object Include -eq 'TypescriptBridge.Build')
if ($sourceManifest.PackageManifest.Metadata.Identity.Version -ne $version -or
    $sourceReference.Count -ne 1 -or $sourceReference[0].Version -ne $version) {
    throw 'Release source versions do not match Directory.Build.props.'
}
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Open-Artifact([string] $name) {
    $file = Join-Path $releaseDir $name
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw "Missing release artifact: $file" }
    return [IO.Compression.ZipFile]::OpenRead($file)
}

function Require-Entry($archive, [string] $name) {
    $entry = $archive.GetEntry($name)
    if ($null -eq $entry) { throw "Missing $name in $($archive.ToString())" }
    return $entry
}

function Read-EntryText($entry) {
    $reader = New-Object IO.StreamReader($entry.Open())
    try { return $reader.ReadToEnd() } finally { $reader.Dispose() }
}

foreach ($packageId in @('TypescriptBridge.Build', 'TypescriptBridge.Template')) {
    $archive = Open-Artifact "$packageId.$version.nupkg"
    try {
        $null = Require-Entry $archive 'icon.png'
        [xml] $nuspec = Read-EntryText (Require-Entry $archive "$packageId.nuspec")
        if ($nuspec.package.metadata.version -ne $version) { throw "$packageId has the wrong version." }
        if ($nuspec.package.metadata.icon -ne 'icon.png') { throw "$packageId does not declare its icon." }

        if ($packageId -eq 'TypescriptBridge.Build') {
            $null = Require-Entry $archive 'build/TypescriptBridge.Build.targets'
            $null = Require-Entry $archive 'tools/esbuild/win-x64/esbuild.exe'
        } else {
            $null = Require-Entry $archive 'content/.template.config/icon.png'
            $null = Require-Entry $archive 'content/TypescriptProject.slnx'
            $null = Require-Entry $archive 'content/ts/src/typescript-bridge/typescript-bridge.ts'
            if ($archive.GetEntry('content/ts/src/default-definitions/typescript-bridge.d.ts')) {
                throw 'The template still contains global TypeScript declarations.'
            }
            [xml] $project = Read-EntryText (Require-Entry $archive 'content/TypescriptProject.csproj')
            $reference = @($project.Project.ItemGroup.PackageReference | Where-Object Include -eq 'TypescriptBridge.Build')
            if ($reference.Count -ne 1 -or $reference[0].Version -ne $version) {
                throw 'The template does not reference the matching Build package.'
            }
            if ($archive.GetEntry('content/TypescriptProject.esproj') -or
                $archive.GetEntry('content/TypescriptProject.Provider.csproj')) {
                throw 'The template still includes the old projects.'
            }
        }
    } finally { $archive.Dispose() }
}

$archive = Open-Artifact "TypescriptBridge.Vsix.$version.vsix"
try {
    $null = Require-Entry $archive 'icon.png'
    [xml] $manifest = Read-EntryText (Require-Entry $archive 'extension.vsixmanifest')
    if ($manifest.PackageManifest.Metadata.Identity.Version -ne $version) { throw 'VSIX version mismatch.' }
    if ($manifest.PackageManifest.Metadata.Icon -ne 'icon.png' -or
        $manifest.PackageManifest.Metadata.PreviewImage -ne 'icon.png') {
        throw 'VSIX icon and preview image are not declared.'
    }
} finally { $archive.Dispose() }

Write-Host "Release artifacts $version passed package checks." -ForegroundColor Green
