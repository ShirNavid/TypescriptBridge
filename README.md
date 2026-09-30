# TypescriptBridge

Write TypeScript, consume it from C#. The TypeScript entry point is
compiled at build time and embedded into a C# readonly string field.
No Node.js or npm required.

## Install

    dotnet new install TypescriptBridge.Template

## Quick start

    dotnet new typescript-bridge -n MyProject
    cd MyProject
    dotnet build

Edit `ts/app.ts` and rebuild. The compiled JavaScript becomes available
in C# as:

    TypescriptProvider.TypescriptCode

For example:

    string html = "<html><body><script>"
                + TypescriptProvider.TypescriptCode
                + "</script></body></html>";

## Configuration

Optional. All settings have sensible defaults:

    {
      "output": {
        "className": "TypescriptProvider",
        "fieldName": "TypescriptCode"
      },
      "typescript": {
        "target": "es2020",
        "format": "iife"
      },
      "release-minify": {
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

### release-minify

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

- `ts/app.ts` — the TypeScript entry point.
- `tsconfig.json` — TypeScript language service configuration.
- `config.json` — optional configuration.
- `ts/default-definitions/typescript-bridge.d.ts` — declares `BUILD_MODE` for the TypeScript language service.
- `Bridge.cs` — generated, do not edit.

## IntelliSense

Full IntelliSense for `config.json` is available out of the box in
Visual Studio. No setup is required.

## Requirements

- .NET 8 SDK or later

## Notes

- `Bridge.cs` is regenerated on every build and is not checked into
  source control.
- The TypeScript entry point is always `ts/app.ts`.
- `Bridge.cs` contains two JavaScript payloads (debug and release).
  The C# compiler selects the appropriate payload at build time
  via `#if DEBUG`.



