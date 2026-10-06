# TypescriptBridge

Write TypeScript in `ts/src/`, debug it with Visual Studio breakpoints, and consume the bundled JavaScript from C#.

## Requirements

- Visual Studio 2026 with the TypescriptBridge VSIX for F5 debugging
- .NET SDK 8.0 or later
- Node.js 18 or later
- Microsoft Edge or Google Chrome

## Create a project

```powershell
dotnet new install TypescriptBridge.Template
dotnet new typescript-bridge -n MyApp
dotnet build MyApp/MyApp.csproj
```

Open `MyApp/MyApp.slnx` in Visual Studio. Install the TypescriptBridge VSIX and restart Visual Studio once before using F5. Set a breakpoint in `ts/src/app.ts` and press F5.

The generated solution contains one C# class library project. Its build bundles TypeScript into `Bridge.cs` in the project namespace:

```csharp
using MyApp;
string javascript = TypescriptProvider.TypescriptCode;
```

Use `config.json` to define multiple named application entrypoints and C# fields. `run.entrypoint` selects which one F5 debugs; test projects still use `testEntrypoint`. Legacy single-entrypoint configurations remain valid. Set the bundle format, release minification, debug browser, and HTTP port there too. Set the compilation target in `ts/tsconfig.json`. Import `BUILD_MODE` from `ts/src/typescript-bridge/typescript-bridge.ts`; its type is `"DEBUG" | "RELEASE"` and its value follows the C# build configuration.

For the generated project layout and further instructions, see its `README.md`.

## Development tests

The reusable tests and their commands are documented in [`tests/README.md`](tests/README.md).

## Prepare a release

Keep the same version in `Directory.Build.props`, the VSIX manifest, and the template's `TypescriptProject.csproj`. The Build package creates `Bridge.cs` when a generated project is built; a seed file is not needed.

```powershell
dotnet build src/TypescriptBridge.Tool/TypescriptBridge.Tool.csproj -c Release
dotnet pack src/TypescriptBridge.Build/TypescriptBridge.Build.csproj -c Release -o artifacts
dotnet pack templates/TypescriptBridge.Template/TypescriptBridge.Template.csproj -c Release -o artifacts
dotnet build src/TypescriptBridge.Vsix/TypescriptBridge.Vsix.csproj -c Release
$version = ([xml](Get-Content Directory.Build.props -Raw)).Project.PropertyGroup.Version
Copy-Item src/TypescriptBridge.Vsix/bin/Release/net472/TypescriptBridge.Vsix.vsix "artifacts/TypescriptBridge.Vsix.$version.vsix"
./tests/ReleaseArtifacts.Tests.ps1
```

## Publish version 1.0.5

1. Sign in to nuget.org and create an API key with Push permission for both package IDs. Publish `artifacts/TypescriptBridge.Build.1.0.5.nupkg` first, then `artifacts/TypescriptBridge.Template.1.0.5.nupkg`:

   ```powershell
   $apiKey = Read-Host 'NuGet API key'
   dotnet nuget push artifacts/TypescriptBridge.Build.1.0.5.nupkg --api-key $apiKey --source https://api.nuget.org/v3/index.json
   dotnet nuget push artifacts/TypescriptBridge.Template.1.0.5.nupkg --api-key $apiKey --source https://api.nuget.org/v3/index.json
   ```

2. In the Visual Studio Marketplace publisher portal, edit the existing TypescriptBridge extension and upload `artifacts/TypescriptBridge.Vsix.1.0.5.vsix`; publish the update. Keep its existing publisher, internal name, and VSIX ID.
3. Once both NuGet packages are available, install the template with `dotnet new install TypescriptBridge.Template@1.0.5`. The template references `TypescriptBridge.Build` version 1.0.5, which NuGet restores on build. Install the VSIX for F5 debugging and restart Visual Studio.

NuGet package versions cannot be overwritten after publication; use a new version if 1.0.5 was already published.

## Named application bundles

Set `entrypoints` in `config.json` to generate one C# field per TypeScript source. For example, `main` and `chart` can generate `MainCode` and `ChartCode`. Set `run.entrypoint` to the name to debug with F5 (default: `main`). The generated project's README includes a full example. Existing single-entrypoint configs remain supported.

The build injects `PROJECT_STATUS` as `"Application"` for executable projects and `"Library"` otherwise. When `isTestProject` is `true`, only `testEntrypoint` is bundled; application entrypoints are ignored. Paths are relative to `ts/`.
