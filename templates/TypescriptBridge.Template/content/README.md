# TypescriptProject

A TypescriptBridge project.

Write TypeScript in `ts/app.ts`. On every build it is compiled to
JavaScript and embedded into a C# readonly string field
(`TypescriptProvider.TypescriptCode`). The same project can be used
as a C# library or run directly as a debugging host — with no
configuration change.

## Two ways to use this project

### 1. As a library

Reference this project from another C# project:

    <ProjectReference Include="..\TypescriptProject\TypescriptProject.csproj" />

Then use the compiled TypeScript in your C# code:

    string html = "<html><body><script>"
                + TypescriptProvider.TypescriptCode
                + "</script></body></html>";

Program.cs is present in the assembly but is never invoked. There is
no HTTP server, no browser, and no debug session in this mode.

### 2. As an executable

This project can run itself. When it runs, it starts a local HTTP
server on loopback, serves the compiled JavaScript, and opens a
browser so the TypeScript can be debugged directly.

There are two ways to run it:

#### F5 in Visual Studio

1. Open `TypescriptProject.esproj` in Visual Studio 2026.
2. Right-click `TypescriptProject.esproj` and select
   **Set as Startup Project**.
3. Set a breakpoint in `ts/app.ts`.
4. Press **F5**.

Visual Studio starts the C# host, opens the configured browser, and
attaches the JavaScript debugger. Breakpoints in `ts/app.ts` are hit.

#### Direct run

    dotnet run

The browser opens automatically. No debugger attaches.
To debug manually, use:

    Debug -> Attach to Process
    Attach to: JavaScript and TypeScript (Chrome DevTools Protocol / V8 Inspector)
    Select the browser process, click Attach.

## Configuration

`config.json`:

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
      },
      "run": {
        "browser": "edge",
        "port": 45000
      }
    }

### run.browser

`edge` or `chrome`. Determines which browser is launched when the
project runs as an executable. Ignored when the project is referenced
as a library.

### run.port

Port for the debug HTTP host. Must be reachable on loopback.
Default: 45000.

The generated `.vscode/launch.json` (used by F5) references this exact
port. Changing it here is the only place you need to change it.

## Files

- `ts/app.ts` — the TypeScript entry point.
- `Program.cs` — the runtime host (runs only when the project is
  executed).
- `config.json` — configuration.
- `TypescriptProject.esproj` — Visual Studio debug-launch project
  used by F5.
- `Prepare-DebugSession.ps1` — regenerates `.vscode/launch.json`
  from the template on every build.
- `.vscode/launch.template.json` — the source template for
  `launch.json`; do not edit `launch.json` directly.
- `Bridge.cs` — generated; do not edit.

## Requirements

- .NET 8 SDK or later
- Visual Studio 2026 for F5 debugging
- Edge or Chrome (whichever is configured in `run.browser`)

## Notes

- The C# host serves the compiled JavaScript over loopback HTTP.
- The JavaScript debugger is Visual Studio's existing JavaScript
  debugger.
- No custom debugger is involved.
