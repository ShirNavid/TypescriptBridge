================================================================================
TYPESCRIPTBRIDGE — VISION DOCUMENT
================================================================================

Version:     4.0
Date:        2026-10-01
Status:      Final
Audience:    Everyone who wants to understand what this product is and why
             it exists.

This document describes the developer experience. It does not describe the
internal implementation. It describes what the developer sees, what the
developer does, and how the developer feels while using the product.

Change history:
    3.1 -> 4.0  Single-project architecture. The .esproj project type has
                been removed. F5 is now orchestrated by a Visual Studio
                extension (VSIX). The net6.0 framework blocker is gone.

================================================================================
PREFACE
================================================================================

TypescriptBridge is a way for a developer to:

    - write TypeScript in Visual Studio,
    - debug that TypeScript in Visual Studio with real breakpoints,
    - consume the same TypeScript from C# as an embedded string,
    - and do all of that from a single clean project,
    - without needing to understand the underlying machinery.

This document explains what that experience looks like.

It is written to be read by:
    - a developer who is deciding whether to use this product,
    - a developer who has just installed this product,
    - a maintainer who needs to remember what the product is supposed to feel like.

It is not written to explain how the product is built. A separate document
covers that.

================================================================================
PART 1 — WHO IS THIS FOR
================================================================================

1.1 THE PRIMARY DEVELOPER

The primary developer is someone who:

    - works in Visual Studio,
    - writes TypeScript,
    - wants breakpoints in their TypeScript to work,
    - and wants to consume that TypeScript from C#.

This developer may be:
    - a C# developer who needs to embed JavaScript in a C# application,
    - a TypeScript developer who wants to keep their tooling inside Visual Studio,
    - a developer who is tired of complex JavaScript build pipelines.

1.2 THE SECONDARY DEVELOPER

The secondary developer is someone who:

    - receives a MyApp project created by the primary developer,
    - wants to consume the compiled JavaScript as a C# string,
    - and does not want to learn anything about TypeScript.

This developer works in a separate C# project (for example MyConsumer).
They reference MyApp and read TypescriptProvider.TypescriptCode.

1.3 THE NON-DEVELOPER

This product is not for:

    - developers who only want to use npm and webpack,
    - developers who want a full React/Vue/Angular setup,
    - developers who want hot module reloading,
    - developers who work primarily in Visual Studio Code.

The product is intentionally narrow. It does one thing well.

================================================================================
PART 2 — WHAT THE DEVELOPER RECEIVES
================================================================================

2.1 A SINGLE COMMAND

The developer creates a project with:

    dotnet new typescript-bridge -n MyApp

This is the only command required to start.

2.2 A SINGLE PROJECT

When the developer opens the generated solution in Visual Studio, they see:

    Solution 'MyApp' (1 project)
    └── MyApp (csproj)
        ├── ts/
        │   ├── src/
        │   │   └── app.ts
        │   ├── package.json
        │   └── tsconfig.json
        ├── config.json
        └── README.md

One project. One folder. Everything the developer edits on a daily basis
lives inside it.

The MyApp project is a standard C# class library. It compiles Bridge.cs
into an assembly that other C# projects can reference. It also serves as
the F5 target through the TypescriptBridge VSIX extension.

The developer does not need a second project for consumption.

2.3 A FOCUSED TYPESCRIPT FOLDER

All TypeScript work happens inside a single folder:

    ts/
        src/
            app.ts          the entry point
            ...             additional .ts files
            default-definitions/
                            (infrastructure, not shown)
        package.json        npm dependency manifest
        tsconfig.json       TypeScript language service configuration
        node_modules/       npm packages (hidden by Visual Studio)

The developer edits files in ts/src/ and, when needed, runs npm install
inside ts/ to add dependencies.

Everything related to TypeScript lives under one folder. There is no
ambiguity about where a .ts file should go, where npm packages are
installed, or where the TypeScript configuration lives.

2.4 A SMALL SET OF SUPPORTING FILES

The developer also sees, at the project root:

    config.json      settings for the project (optional to edit)
    README.md        documentation
    Bridge.cs        generated C# file

These are visible because they are useful. They are not clutter.

2.5 A HIDDEN SET OF INFRASTRUCTURE FILES

The developer does not see:

    server.js                    Node.js HTTP server for debug sessions
    Prepare-DebugSession.ps1     regenerates launch.json before each F5
    .vscode/                     contains the generated launch.json
    ts/src/default-definitions/  contains the BUILD_MODE declaration
    ts/node_modules/             npm packages, hidden by VS by default
    obj/                         intermediate build output
    bin/                         final build output

These exist on disk. They are maintained automatically. They are not part
of the developer-facing project surface.

They are hidden because the developer does not need to know they exist
in order to do their work.

2.6 A PREREQUISITE: THE VSIX EXTENSION

Before F5 can work, the TypescriptBridge Visual Studio extension must be
installed. The template installs it automatically on first build.

After installation, Visual Studio must be restarted once.

This is the only extra step the developer must take. After the restart,
F5 works for every TypescriptBridge project on the machine.

The developer never interacts with the VSIX directly. It is invisible
during normal work. It only appears in Tools > Extensions and Updates.

================================================================================
PART 3 — THE DAILY DEVELOPER EXPERIENCE
================================================================================

3.1 OPENING THE PROJECT

The developer opens MyApp.slnx in Visual Studio.

Solution Explorer shows one project: MyApp (csproj).

The developer expands MyApp. They see:

    ts/
        src/
            app.ts
        package.json
        tsconfig.json
    config.json
    README.md

They do not see any of the files listed in section 2.5.

3.2 WRITING TYPESCRIPT

The developer opens ts/src/app.ts.

They write TypeScript code. IntelliSense works. Types are checked. Errors
appear under the cursor. The TypeScript language service is fully
functional.

The developer can also:

    - add new .ts files anywhere under ts/src/,
    - create subfolders under ts/src/,
    - import from other .ts files,
    - use any npm package that they install via ts/package.json,
    - configure ts/tsconfig.json for stricter or looser type checking.

If they want to add an npm dependency, they open a terminal in the project
folder and run:

    cd ts
    npm install some-package

The node_modules folder is created under ts/. It is hidden from Solution
Explorer by Visual Studio's default behavior.

3.3 USING BUILD_MODE

The developer can reference a global constant called BUILD_MODE.

    if (BUILD_MODE === "DEBUG") {
        console.log("running in debug mode");
    } else {
        console.log("running in release mode");
    }

This constant is available everywhere in the TypeScript code. It has
full IntelliSense. It is replaced at compile time with the literal
string "DEBUG" or "RELEASE".

The developer does not need to declare BUILD_MODE. It is provided by
the project infrastructure. IntelliSense sees it because the language
service has been told about it via a declaration file that lives under
ts/src/default-definitions/. That folder is hidden from Solution Explorer.

3.4 CONFIGURING THE PROJECT (OPTIONAL)

The developer can open config.json to change settings:

    - the name of the generated C# class,
    - the name of the generated C# field,
    - the output format of the bundled JavaScript,
    - the minification level for release builds,
    - which browser to use for debugging,
    - which port the debug HTTP server listens on.

Most developers never edit this file. The defaults are sensible.

3.5 PRESSING F5

The developer places a breakpoint on a line of TypeScript code.

They press F5.

The following happens, in order, invisibly to the developer:

     1. Visual Studio queries the TypescriptBridge VSIX.
     2. The VSIX runs Prepare-DebugSession.ps1.
     3. The script reads config.json.
     4. The script regenerates .vscode/launch.json with the current
        browser, port, and a fresh per-session browser profile directory.
     5. The VSIX starts the Node.js debug server.
     6. The Node.js server invokes esbuild on ts/src/app.ts.
     7. The Node.js server writes debug.js, debug.js.map, and index.html
        into obj/TypescriptBridge/debug/.
     8. The Node.js server starts an HTTP listener on 127.0.0.1:<port>.
     9. The Node.js server waits.
    10. The VSIX hands launch.json to Visual Studio.
    11. Visual Studio launches the configured browser at
        http://127.0.0.1:<port>/index.html.
    12. Visual Studio attaches the built-in JavaScript/TypeScript debugger.
    13. The browser loads the page, executes debug.js, and the breakpoint
        in the original TypeScript source is hit.

From the developer's perspective, F5 is one action. The browser opens.
The breakpoint is hit. Locals, Call Stack, Step Over, Step Into, and
Continue all work.

The developer does not need to know that a Node.js server is involved.
The developer does not need to know that a source map was generated.
The developer does not need to configure anything.

3.6 DEBUGGING IN PRACTICE

Once the breakpoint is hit, the developer can:

    - inspect the Locals window,
    - inspect the Call Stack,
    - step over (F10),
    - step into (F11),
    - continue (F5),
    - stop debugging (Shift+F5).

The debugger is the standard Visual Studio JavaScript/TypeScript debugger.
There is no custom debugger. The VSIX only orchestrates the launch; the
debugging itself is provided by Visual Studio.

When the developer stops debugging:

    - The Node.js server stops.
    - The HTTP port is released.
    - The browser debug session ends.
    - The browser process is closed.

The developer can press F5 again immediately, without restarting Visual
Studio. The next debug session uses a fresh browser profile to prevent
browser cache from serving stale JavaScript.

3.7 CHANGING TYPESCRIPT BETWEEN RUNS

The developer edits ts/src/app.ts.

They press F5 again.

The new code is compiled and executed. The breakpoint is hit in the new
code. There is no stale state.

3.8 CHANGING THE BROWSER

The developer opens config.json.

They change "browser": "edge" to "browser": "chrome".

They press F5.

The next debug session launches Chrome instead of Edge.

No Visual Studio restart is required. No project rebuild is required.

3.9 CHANGING THE PORT

The developer opens config.json.

They change "port": 45000 to "port": 46000.

They press F5.

The next debug session uses the new port.

No Visual Studio restart is required.

3.10 USING NODE_MODULES

The developer wants to use an npm package.

They open a terminal in the project folder. They run:

    cd ts
    npm install lodash

A node_modules folder is created under ts/. It is hidden in Solution
Explorer. It is listed in .gitignore.

The developer can now import lodash from ts/src/app.ts:

    import _ from "lodash";

IntelliSense resolves the import. esbuild bundles the package into the
final JavaScript. The debugger sees the original TypeScript source.

================================================================================
PART 4 — THE C# CONSUMPTION EXPERIENCE
================================================================================

4.1 WHO CONSUMES

The developer who consumes the compiled TypeScript from C# is often a
different person from the developer who wrote the TypeScript.

This consumer developer works in a separate project, for example
MyConsumer. They do not want to know anything about TypeScript.

4.2 HOW TO REFERENCE

The consumer adds a project reference to MyApp:

    <ProjectReference Include="..\MyApp\MyApp.csproj" />

This is a normal C# library reference. It works exactly like any other
library reference. Visual Studio recognizes it.

4.3 WHAT THEY GET

After referencing MyApp, the consumer can write:

    using MyApp;

    string js = TypescriptProvider.TypescriptCode;

The string contains the compiled JavaScript. It is ready to embed in
an HTML page, pass to a WebView, include in a report, or use in any
other way the consumer needs.

4.4 HOW IT IS UPDATED

When the TypeScript developer changes ts/src/app.ts, they press F5 in
Visual Studio. This runs the debug session.

Note: F5 does not regenerate Bridge.cs.

Bridge.cs is regenerated by the C# build, not by the debug session.
Bridge.cs is regenerated when:

    - the solution is built, or
    - an external C# project that references MyApp is built.

This means the C# consumer must rebuild their project to see the latest
JavaScript. This is normal and expected: C# code depends on C# builds,
not on TypeScript debug sessions.

================================================================================
PART 5 — WHAT THE DEVELOPER NEVER NEEDS TO DO
================================================================================

The developer never needs to:

    - configure a build system manually,
    - write a launch.json file,
    - set up a webpack or rollup configuration,
    - edit a .csproj file by hand,
    - edit Bridge.cs,
    - run npm install before pressing F5 for the first time,
    - open a terminal,
    - know what esbuild is,
    - know what a source map is,
    - know that Node.js is involved,
    - configure a debug adapter,
    - attach the debugger manually,
    - refresh Visual Studio after changing config.json (beyond the
      one-time restart after VSIX installation),
    - restart Visual Studio after adding a new .ts file.

================================================================================
PART 6 — WHAT THE DEVELOPER NEVER SEES
================================================================================

The developer never sees:

    - the Node.js HTTP server,
    - the esbuild invocation,
    - the source map generation,
    - the launch.json generation,
    - the default-definitions folder,
    - the obj/TypescriptBridge directory,
    - the browser profile directory,
    - the .vscode folder,
    - the TypescriptBridge VSIX in daily work,
    - any file that is described in section 2.5.

These exist. They work. But they are not part of the developer's world.

================================================================================
PART 7 — WHAT IS VISIBLE AND WHY
================================================================================

Under MyApp (csproj):

    ts/src/app.ts
        The main file the developer edits.

    ts/src/**/*.ts
        Additional TypeScript files added by the developer.
        They appear automatically.

    ts/package.json
        Allows npm dependencies. Visible because the developer
        might want to add dependencies.

    ts/tsconfig.json
        Allows the developer to change TypeScript language settings.
        Visible because it is a standard TypeScript file.

    config.json
        Allows the developer to change project settings. Visible
        because it is a project-level configuration file.

    README.md
        Documentation. Visible because it is the entry point for
        new users.

    Bridge.cs
        The generated C# file. Visible because the C# developer
        needs to see that it exists.

Anything not in this list is hidden.

================================================================================
PART 8 — WHAT IS HIDDEN AND WHY
================================================================================

Files are hidden from Solution Explorer for one of three reasons:

Reason 1: Infrastructure.
    The file exists only to make something else work. The developer
    does not interact with it.

    Examples: server.js, Prepare-DebugSession.ps1, .vscode/launch.json.

Reason 2: Generated content.
    The file is created and recreated automatically. Editing it would
    be pointless because it will be overwritten.

    Examples: ts/src/default-definitions/typescript-bridge.d.ts,
    Bridge.cs (though Bridge.cs is visible; it is not hidden).

Reason 3: External packages.
    The file or folder comes from a package manager. Visual Studio
    hides it by default for performance reasons.

    Examples: ts/node_modules.

================================================================================
PART 9 — WHAT THE DEVELOPER CAN DO
================================================================================

The developer can:

    - write TypeScript in ts/src/app.ts,
    - add additional .ts files anywhere under ts/src/,
    - create subfolders under ts/src/,
    - import between .ts files,
    - install npm packages into ts/node_modules,
    - configure TypeScript settings in ts/tsconfig.json,
    - configure TypescriptBridge settings in config.json,
    - change the debug browser between Edge and Chrome,
    - change the debug port,
    - use BUILD_MODE inside their TypeScript,
    - press F5 to debug,
    - place breakpoints anywhere in their TypeScript,
    - inspect variables while paused,
    - step through their code,
    - change their code and press F5 again.

The developer can do all of this without restarting Visual Studio,
without rebuilding the solution, and without understanding the
underlying machinery.

================================================================================
PART 10 — WHAT THE DEVELOPER CANNOT DO
================================================================================

The developer cannot:

    - run the project without Node.js installed,
    - run the project without the TypescriptBridge VSIX installed,
    - debug TypeScript in a browser other than Edge or Chrome,
    - use the project in Visual Studio Code with the same experience,
    - use the project on Linux or macOS (as of version 1.0),
    - use TypeScript features that require a bundler other than esbuild,
    - run the project without Visual Studio (as of version 1.0),
    - run the project without the .NET SDK installed,
    - place TypeScript files outside ts/src/ and expect them to be
      bundled. Only ts/src/ is the source folder.

These are intentional limits. They keep the product focused.

================================================================================
PART 11 — WHY THIS DESIGN
================================================================================

The design is driven by three principles.

11.1 PRINCIPLE ONE: THE DEVELOPER SHOULD SEE ONLY WHAT THEY NEED

A typical TypeScript project in another framework might show:

    - a package.json,
    - a tsconfig.json,
    - a webpack.config.js,
    - a .babelrc,
    - an index.html,
    - a dist folder,
    - a node_modules folder with hundreds of subfolders,
    - a .eslintrc,
    - a .prettierrc,
    - and many other files.

The developer has to learn what each of these does.

TypescriptBridge shows the developer the minimum needed to do the work.
Everything else is hidden or handled automatically.

11.2 PRINCIPLE TWO: THE DEVELOPER SHOULD NOT NEED TO CONFIGURE ANYTHING

After dotnet new, the developer can immediately:

    - open the project,
    - write TypeScript,
    - press F5,
    - hit a breakpoint.

No npm install is required for the initial project. No configuration
is required. No build step is required before F5.

Configuration files exist because some developers want to customize
their project. But the default configuration is complete.

11.3 PRINCIPLE THREE: THE DEVELOPER SHOULD NOT NEED TO KNOW HOW IT WORKS

The developer should not need to know:

    - that Node.js is used internally,
    - that esbuild compiles TypeScript,
    - that a source map is generated,
    - that an HTTP server is started,
    - that the debugger is attached via CDP,
    - that a browser profile is created per session,
    - that a Visual Studio extension orchestrates the launch.

All of these things are true. All of them work. But the developer's
mental model is simply:

    "I write TypeScript. I press F5. It runs in a browser. My
     breakpoints work."

That is enough.

================================================================================
PART 12 — THE COMPLETE PICTURE
================================================================================

Consider a developer named Sam.

Sam has a C# application. Sam wants to embed some JavaScript in the
application without learning webpack or setting up an npm project.

Sam installs the TypescriptBridge template:

    dotnet new install TypescriptBridge.Template

Sam creates a new project:

    dotnet new typescript-bridge -n MyEmbeddedScripts

Sam opens MyEmbeddedScripts.slnx in Visual Studio.

On the first build, the TypescriptBridge VSIX is installed automatically.
Sam restarts Visual Studio once, as instructed by the build output.

Sam sees one project: MyEmbeddedScripts.

Sam expands MyEmbeddedScripts. They see:

    ts/
        src/
            app.ts
        package.json
        tsconfig.json
    config.json
    README.md

Sam opens ts/src/app.ts. They write TypeScript that renders a small chart
onto a canvas element.

Sam places a breakpoint on the line where the chart is drawn.

Sam presses F5.

The browser opens. The chart is drawn. The breakpoint is hit. Sam
inspects a few variables. Sam presses F5 to continue. The chart finishes
drawing.

Sam makes a change. Sam presses F5 again. The new code runs.

Later, Sam references MyEmbeddedScripts from their C# application. Sam
writes:

    string html = $"<html><body><canvas id='chart'></canvas><script>{TypescriptProvider.TypescriptCode}</script></body></html>";

Sam's C# application now embeds the TypeScript-compiled chart code.

Sam never opened a terminal. Sam never ran npm install. Sam never
learned what esbuild is. Sam never wrote a build script. Sam never
configured a bundler. Sam never edited a .csproj file. Sam never
looked inside the internal obj/ folder.

Sam just wrote TypeScript and pressed F5.

That is the vision of TypescriptBridge.

================================================================================
PART 13 — WHAT MAKES THIS DIFFERENT
================================================================================

13.1 COMPARED TO A PLAIN NODE.JS PROJECT

A plain Node.js project requires:

    - package.json with dependencies,
    - a bundler configuration,
    - a development server,
    - manual browser attachment,
    - manual source map setup.

TypescriptBridge does all of this automatically.

13.2 COMPARED TO A PLAIN C# PROJECT

A plain C# project cannot:

    - contain TypeScript files with IntelliSense,
    - compile TypeScript at build time,
    - provide TypeScript breakpoints,
    - expose TypeScript-compiled JavaScript as a C# string.

TypescriptBridge adds all of this without adding complexity.

13.3 COMPARED TO A FULL WEB PROJECT

A full web project (React, Vue, Angular) requires:

    - a specific framework,
    - many dependencies,
    - a specific folder structure,
    - a specific build pipeline.

TypescriptBridge has none of these requirements. It is framework-agnostic
and structure-agnostic within the ts/src/ folder.

================================================================================
PART 14 — HOW THE PRODUCT EVOLVES
================================================================================

The product is designed to grow in specific directions.

Future versions may add:

    - support for additional browsers,
    - support for additional operating systems,
    - support for hot reloading,
    - support for additional bundlers,
    - support for other TypeScript output formats,
    - pre-installed VSIX through the Visual Studio Marketplace.

Future versions will not add:

    - framework-specific features,
    - complex configuration surfaces,
    - additional visible infrastructure,
    - requirements for the developer to understand the internals.

The single-project simplicity is the product's most important feature.
It is protected above all else.

================================================================================
PART 15 — IN ONE SENTENCE
================================================================================

TypescriptBridge gives a developer the simplest possible way to write
TypeScript inside Visual Studio, debug it with real breakpoints, and
consume the result from C# — without learning any new tooling and
without seeing anything that is not essential.

================================================================================
END OF VISION DOCUMENT v4.0
================================================================================
