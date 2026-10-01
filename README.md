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

Use `config.json` to choose the generated class and field names, bundle format, release minification, debug browser, and HTTP port. Set the compilation target in `ts/tsconfig.json`. The `BUILD_MODE` global is `"DEBUG"` or `"RELEASE"` according to the C# build configuration.

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

Publish the two `.nupkg` files to NuGet and upload the `.vsix` to Visual Studio Marketplace after checking the generated project in Visual Studio.