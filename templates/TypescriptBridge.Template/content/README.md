# {ProjectName}

Write TypeScript in `ts/app.ts`. On every build it is compiled into
JavaScript and embedded into a C# readonly string field.

No Node.js or npm required.

## Quick start

1. Write TypeScript in `ts/app.ts`.
2. Run `dotnet build`.
3. Use the generated C# field:

    string html = "<html><body><script>"
                + TypescriptProvider.TypescriptCode
                + "</script></body></html>";

The generated file is `Bridge.cs`. Do not edit it manually.

## Configuration

Optional. All settings have sensible defaults in `typescript-bridge.json`:

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

### output

| Setting | Description | Default |
| --- | --- | --- |
| `className` | Generated C# static class name. | `TypescriptProvider` |
| `fieldName` | Generated C# readonly string field name. | `TypescriptCode` |

Both values must be valid C# identifiers and must not be C# keywords.

### typescript

| Setting | Values | Default |
| --- | --- | --- |
| `target` | `es2015` ... `es2022`, `esnext` | `es2020` |
| `format` | `iife`, `esm`, `cjs` | `iife` |

### minify

| Setting | Values | Default |
| --- | --- | --- |
| `level` | `off`, `light`, `full`, `aggressive` | `aggressive` |
| `keepNames` | `true`, `false` | `true` |

## BUILD_MODE

The current build configuration is available as a global constant
named `BUILD_MODE`:

    Debug   -> "DEBUG"
    Release -> "RELEASE"

Use it in `ts/app.ts`:

    if (BUILD_MODE === "DEBUG") {
        console.log("Debug mode");
    }

`BUILD_MODE` is available everywhere in your TypeScript code, with full
IntelliSense. Do not declare a variable with the same name.

## Files

- `{ProjectName}.csproj` — the .NET project file.
- `ts/app.ts` — the TypeScript entry point.
- `typescript-bridge.json` — optional configuration.
- `Bridge.cs` — generated, do not edit.

## IntelliSense

Full IntelliSense for `typescript-bridge.json` is available out of the
box in Visual Studio and VS Code. No setup is required.

## Requirements

- .NET 8 SDK or later

## Notes

- `Bridge.cs` is regenerated on every build and is not checked into
  source control.
- The TypeScript entry point is always `ts/app.ts`.
