# {ProjectName}

Write TypeScript in ts/app.ts. On every build it is compiled to JavaScript, minified (in Release), and embedded into the C# field TypescriptProvider.TypescriptCode.

## Quick start

1. Write TypeScript in ts/app.ts.
2. Run dotnet build.
3. In C#:

string html = "<html><body><script>" + TypescriptProvider.TypescriptCode + "</script></body></html>";

The class name TypescriptProvider and the field name TypescriptCode are fixed.

## Files

- {ProjectName}.csproj
- TypescriptProvider.cs (generated, do not edit)
- ts-handler.json (optional config)
- ts-handler.schema.json
- README.md
- .vscode/settings.json
- ts/app.ts

Edit only ts/app.ts and optionally ts-handler.json.

## Configuration (ts-handler.json)

{
  "version": 1,
  "typescript": {
    "target": "es2020",
    "format": "iife"
  },
  "debug": {
    "sourceMap": {
      "enabled": true
    }
  },
  "release": {
    "minify": {
      "level": "aggressive",
      "keepNames": false
    }
  }
}

### typescript

target: es2015..es2022, esnext. Default: es2020.
format: iife, esm, cjs. Default: iife.

### debug

sourceMap.enabled: true, false. Default: true.

### release

minify.level: off, light, full, aggressive. Default: aggressive.
minify.keepNames: true, false. Default: false.

## Minify levels

off: no minify.
light: --minify-whitespace.
full: --minify-whitespace --minify-syntax.
aggressive: --minify (renames identifiers to short names).

## Debug vs Release

The generated file contains both versions with #if DEBUG / #else / #endif.

## IntelliSense

VS Code: works automatically.
Visual Studio 2022: add "$schema": "./ts-handler.schema.json" as first entry of ts-handler.json.

## Errors

Invalid ts-handler.json fails the build with a precise error message.

## Requirements

.NET 8 SDK or later.
Node.js and npm are not required for basic use.

## Notes

TypescriptProvider.cs is regenerated on every build.
The entry point is always ts/app.ts.