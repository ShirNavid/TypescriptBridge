# TsHandler

A build-time integration that compiles a TypeScript entry point into JavaScript and embeds it into a C# readonly string field.

## Install

    dotnet new install TsHandler.Template

## Quick start

    dotnet new ts-handler -n MyProject
    cd MyProject

Edit ts/app.ts, run dotnet build, and use:

    string html = "<html><body><script>"
                + TypescriptProvider.TypescriptCode
                + "</script></body></html>";

## Configuration

    {
      "version": 1,
      "typescript": {
        "target": "es2020",
        "format": "iife"
      },
      "debug": {
        "sourceMap": { "enabled": true }
      },
      "release": {
        "minify": { "level": "aggressive", "keepNames": false }
      }
    }

Minify levels: off, light, full, aggressive.

## Packages

- TsHandler.Build
- TsHandler.Template

## Requirements

- .NET 8 SDK or later
- Node.js is not required for basic use

## License

MIT. See LICENSE.txt.