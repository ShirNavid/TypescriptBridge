# Tests

Run the .NET integration tests:

```powershell
dotnet test tests/TypescriptBridge.IntegrationTests/TypescriptBridge.IntegrationTests.csproj
```

Run the source-map tests:

```powershell
node --test tests/ServerSourceMapTests.test.js
```

Run the generated-template smoke test. It builds an isolated project, checks
`config.json` and `tsconfig.json` regeneration, verifies C# consumption, and
removes its temporary files afterward:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests/TemplateSmoke.Tests.ps1
```

After building the three release archives in `artifacts/`, verify their
versions and icons without opening the images:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests/ReleaseArtifacts.Tests.ps1
```

After VSIX changes, open a generated project in Visual Studio and verify
F5 breakpoints, Stop Debugging, and a second F5. These require Visual Studio
and cannot be checked by the command-line tests.
