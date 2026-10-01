================================================================================
TYPESCRIPTBRIDGE — ARCHITECTURE AND IMPLEMENTATION PLAN
================================================================================

Version:     11.0
Date:        2026-10-01
Status:      Forward-looking. Replaces v10.0.
Companion:   Vision Document v4.0
Audience:    Implementation agent

Change history:
    10.0 -> 11.0 All external review feedback has been integrated.
                 The DebugLaunchOperation enum has been fully decoded.
                 The IVsDebugger4 and VsDebugTargetInfo4 interfaces have
                 been located and dumped. A definitive path forward has
                 been identified: call IVsDebugger4.LaunchDebugTargets4
                 directly from within the VSIX to bypass the launch
                 profile routing that was forcing the managed CoreCLR
                 engine.

================================================================================
PART 0 — WHERE WE ARE
================================================================================

0.1 WHAT WORKS

    Verified end-to-end:

        - The TypescriptBridge.Vsix builds, installs into Visual
          Studio 2026 Community, and loads.
        - Visual Studio invokes our launch provider when F5 is pressed
          on a project whose launchSettings.json contains
          commandName = "typescript-bridge".
        - Our provider reads config.json, spawns node server.js --f5,
          waits for the HTTP listener, reads .vscode/launch.json, and
          returns DebugLaunchSettings to Visual Studio.
        - The Node.js server runs esbuild and generates debug.js,
          debug.js.map, and index.html under obj/TypescriptBridge/debug.
        - The HTTP listener serves those files.
        - A browser opens and loads the page.
        - The browser console shows the expected output with the
          correct source-map prefix (app.ts:2).
        - IVsHierarchy is obtained through the MEF contract name
          Microsoft.VisualStudio.ProjectSystem.Microsoft.VisualStudio.Shell.Interop.IVsHierarchy
          and assigned to DebugLaunchSettings.Project.

0.2 WHAT DOES NOT WORK

    Visual Studio does not start the JavaScript debug adapter when
    our provider is registered through IDebugProfileLaunchTargetsProvider.

    Observable symptoms:

        - JavaScriptDebugAdapterBridge.exe is never spawned.
        - No Dkm (Debug Kernel Manager) process appears.
        - The browser is not launched with --remote-debugging-port.
        - No per-session browser profile directory is created.
        - Breakpoints in ts/src/app.ts remain hollow circles.
        - No error message appears. The debug session simply ends.

    The provider's output is correct in every field. Visual Studio
    still routes the launch to the managed CoreCLR engine for a C#
    class library, and that engine cannot start a process whose
    executable is the symbolic name "js-debug".

0.3 ROOT CAUSE (CONFIRMED BY DECOMPILATION AND EXTERNAL REVIEW)

    Visual Studio dispatches debug launches through the shell
    debugger service, which selects the debug engine based on
    VsDebugTargetInfo4.guidLaunchDebugEngine.

    When a launch profile is routed through
    IDebugProfileLaunchTargetsProvider, Visual Studio's own
    LaunchProfilesDebugLaunchProvider handles the launch. That
    provider is registered with [ExportDebugger("ProjectDebugger")]
    and is designed for managed projects. It builds a
    VsDebugTargetInfo4 and calls IVsDebugger4.LaunchDebugTargets4.

    For a C# class library, the shell debugger sees the managed
    project and prefers the managed debug engine, ignoring the
    JavaScript debug engine GUID we set. This is why the launch
    never reaches the JavaScript adapter.

    The JavaScript Project System (JSPS) avoids this because it owns
    its own project system (.esproj) and registers its provider with
    [ExportDebugger("LaunchJsonDebugger")] under the JSProjectSystem
    capability. Visual Studio then routes its launch directly to the
    JavaScript engine without going through the managed project
    dispatch.

    External review (see PART 8) confirmed this analysis. The
    reviewer identified the correct low-level fallback:
    IVsDebugger4.LaunchDebugTargets4.

================================================================================
PART 1 — VERIFIED FACTS
================================================================================

1.1 ENVIRONMENT

    Visual Studio 2026 Community, version 18.10.12224.181.
    .NET SDK 10.0.401.
    Node.js 24.18.0 at C:\Program Files\nodejs\node.exe.
    Microsoft Edge and Google Chrome installed.

1.2 JAVASCRIPT DEBUG ADAPTER

    Located at:

        Common7\IDE\CommonExtensions\Microsoft\JSDiagnostics\Debugger\
            JavaScriptDebugAdapterBridge.exe
            Microsoft.VisualStudio.JavaScript.Diagnostics.JSDebugger.dll
            DebugAdapter/src/*.js
            DebugAdapterExtensions.pkgdef

    The debug engine GUID is {394120B6-2FF9-4D0D-8953-913EF5CD0BCD},
    named "JavaScript and TypeScript" in the AD7Metrics registry.

    The adapter launcher CLSID is {21d902f4-d9da-4330-8ed3-05fa62305f4b}.
    The custom protocol extension CLSID is {D136063F-F239-4BFC-93CE-C0CF4C4BCFAC}.
    The port supplier GUID is {61D1E397-7AA6-4ED9-815A-0C5CA0E728B4}.

1.3 THE DebugLaunchOperation ENUM (FULLY DECODED)

    From decompiling Microsoft.VisualStudio.ProjectSystem.VS.dll:

        public enum DebugLaunchOperation
        {
            AlreadyRunning                = 0,
            CreateProcess                 = 1,
            Custom                        = 2,
            LaunchByWebServer             = 3,
            AttachToHostingProcess        = 4,
            StartDebuggingHostingProcess  = 5,
            LaunchBrowser                 = 6,
            AppPackageDebug               = 7,
            AttachToSuspendedLaunchProcess = 8
        }

    There is no "Launch" member. JSPS uses (DebugLaunchOperation)1,
    which is CreateProcess. Our provider uses the same value.

1.4 IVsDebugger4 (PUBLIC AND ACCESSIBLE)

    From Microsoft.VisualStudio.Shell.Interop.11.0.dll:

        public interface IVsDebugger4
        {
            void LaunchDebugTargets4(
                uint DebugTargetCount,
                VsDebugTargetInfo4[] pDebugTargets,
                VsDebugTargetProcessInfo[] pLaunchResults);

            void EnumCurrentlyDebuggingProjects(out IEnumHierarchies projects);
        }

    The interface is public. It is obtained from the SVsShellDebugger
    service.

1.5 VsDebugTargetInfo4 (PUBLIC AND ACCESSIBLE)

    From Microsoft.VisualStudio.Shell.Interop.11.0.dll:

        public struct VsDebugTargetInfo4
        {
            public uint dlo;
            public uint LaunchFlags;
            public string bstrRemoteMachine;
            public string bstrExe;
            public string bstrArg;
            public string bstrCurDir;
            public string bstrEnv;
            public uint dwProcessId;
            public IntPtr pStartupInfo;
            public Guid guidLaunchDebugEngine;
            public uint dwDebugEngineCount;
            public IntPtr pDebugEngines;
            public Guid guidPortSupplier;
            public string bstrPortName;
            public string bstrOptions;
            public bool fSendToOutputWindow;
            public object pUnknown;
            public Guid guidProcessLanguage;
            public VsAppPackageLaunchInfo AppPackageLaunchInfo;
            public IVsHierarchy project;
        }

1.6 HOW VISUAL STUDIO BUILDS THE STRUCT

    From decompiling DebugLaunchProviderBase.GetDebuggerStruct4:

        protected internal static VsDebugTargetInfo4 GetDebuggerStruct4(IDebugLaunchSettings info)
        {
            List<Guid> debuggerGuids = GetDebuggerGuids(info);
            VsDebugTargetInfo4 val = new VsDebugTargetInfo4
            {
                dlo                   = (uint)info.LaunchOperation,
                LaunchFlags           = (uint)info.LaunchOptions,
                bstrRemoteMachine     = info.RemoteMachine,
                bstrArg               = info.Arguments,
                bstrCurDir            = info.CurrentDirectory,
                bstrExe               = info.Executable,
                bstrEnv               = GetSerializedEnvironmentString(info.Environment),
                guidLaunchDebugEngine = info.LaunchDebugEngineGuid,
                dwDebugEngineCount    = (uint)debuggerGuids.Count,
                pDebugEngines         = GetDebugEngineBytes(debuggerGuids),
                guidPortSupplier      = info.PortSupplierGuid,
                bstrPortName          = info.PortName,
                bstrOptions           = info.Options,
                fSendToOutputWindow   = info.SendToOutputWindow,
                dwProcessId           = (uint)info.ProcessId,
                pUnknown              = info.Unknown,
                guidProcessLanguage   = info.ProcessLanguageGuid
            };
            val.project = info.Project;
            return val;
        }

    And GetDebuggerGuids:

        private static List<Guid> GetDebuggerGuids(IDebugLaunchSettings info)
        {
            List<Guid> list = new List<Guid>(1 + (info.AdditionalDebugEngines?.Count ?? 0))
                { info.LaunchDebugEngineGuid };
            if (info.AdditionalDebugEngines != null)
                list.AddRange(info.AdditionalDebugEngines);
            return list;
        }

    And GetDebugEngineBytes:

        private static IntPtr GetDebugEngineBytes(List<Guid> guids)
        {
            byte[] array = GetGuidBytes(guids);
            IntPtr intPtr = Marshal.AllocCoTaskMem(array.Length);
            Marshal.Copy(array, 0, intPtr, array.Length);
            return intPtr;

            static byte[] GetGuidBytes(List<Guid> list)
            {
                byte[] array2 = new byte[list.Count * 16];
                for (int i = 0; i < list.Count; i++)
                    list[i].ToByteArray().CopyTo(array2, i * 16);
                return array2;
            }
        }

1.7 JSPS'S OWN PROVIDER

    From Microsoft.VisualStudio.JavaScript.ProjectSystem.dll:

        [ExportDebugger("LaunchJsonDebugger")]
        [AppliesTo("JSProjectSystem")]
        internal class LaunchJsonDebugLaunchProvider : DebugLaunchProviderBase
        {
            private sealed class LaunchJsonDebugLaunchSettings : DebugLaunchSettings
            {
                public string PreLaunchTask { get; set; }

                public LaunchJsonDebugLaunchSettings(DebugLaunchOptions options)
                    : base(options)
                { }
            }

            public override async Task<IReadOnlyList<IDebugLaunchSettings>> QueryDebugTargetsAsync(
                DebugLaunchOptions launchOptions)
            {
                IDebugSettings currentSettings = await GetCurrentSettingsAsync();
                List<IDebugLaunchSettings> allDebugLaunchSettings = new List<IDebugLaunchSettings>();

                foreach (IReadOnlyConfiguration expandedSingleConfiguration in currentSettings.ExpandedSingleConfigurations)
                {
                    allDebugLaunchSettings.Add(await CreateLaunchSettingsAsync(launchOptions, expandedSingleConfiguration));
                }

                AddPreLaunchAsDebugTarget();
                return allDebugLaunchSettings;
            }

            private async Task<DebugLaunchSettings> CreateLaunchSettingsAsync(
                DebugLaunchOptions launchOptions, IReadOnlyConfiguration launchConfig)
            {
                LaunchJsonDebugLaunchSettings settings = new LaunchJsonDebugLaunchSettings(launchOptions);

                settings.LaunchOperation      = (DebugLaunchOperation)1;
                settings.Executable           = "js-debug";
                settings.RemoteMachine        = null;
                settings.Options              = await GetLaunchConfigStringAsync(launchConfig);
                settings.LaunchDebugEngineGuid = new Guid("{394120B6-2FF9-4D0D-8953-913EF5CD0BCD}");
                settings.LaunchOptions        = launchOptions | (DebugLaunchOptions)0x20;

                settings.PreLaunchTask = launchConfig.PreLaunchTask;
                return settings;
            }

            public override async Task LaunchAsync(DebugLaunchOptions launchOptions)
            {
                IDebugLaunchSettings[] array = (await QueryDebugTargetsAsync(launchOptions)).ToArray();
                await LaunchAsync(array);
            }
        }

    Note: LaunchAsync(...) is the inherited method from
    DebugLaunchProviderBase, which internally calls
    IVsDebugger4.LaunchDebugTargets4.

1.8 THE MEF CONTRACT NAME FOR IVsHierarchy

    DebugLaunchProviderBase uses this exact contract name:

        Microsoft.VisualStudio.ProjectSystem.Microsoft.VisualStudio.Shell.Interop.IVsHierarchy

    It can be imported by any VSIX:

        [ImportMany("Microsoft.VisualStudio.ProjectSystem.Microsoft.VisualStudio.Shell.Interop.IVsHierarchy")]
        private IEnumerable<Lazy<IVsHierarchy, IOrderPrecedenceMetadataView>>? VsHierarchies { get; set; }

    This works and produces a non-null IVsHierarchy.

================================================================================
PART 2 — THE CHOSEN ARCHITECTURE
================================================================================

2.1 SUMMARY

    The provider keeps the launch profile registration that lets
    Visual Studio invoke it. Inside QueryDebugTargetsAsync, after
    preparing everything, the provider calls
    IVsDebugger4.LaunchDebugTargets4 directly to start the JavaScript
    debug adapter. It then returns an empty list of
    IDebugLaunchSettings so that Visual Studio does not attempt its
    own (managed) launch of the project.

2.2 WHY THIS WORKS

    The profile registration is what makes Visual Studio invoke the
    provider at all. It is a documented extension point, and it
    works.

    The direct call to LaunchDebugTargets4 is what actually starts
    the debugger. It bypasses the managed-project dispatch that was
    overriding the JavaScript debug engine GUID.

    The empty list is what prevents the managed engine from
    attempting (and failing) to launch the class library.

2.3 ARCHITECTURE

    F5
        |
        v
    MyApp.csproj (class library, OutputType=Library)
        |
        | Properties/launchSettings.json
        |   commandName = "typescript-bridge"
        |
        v
    Visual Studio launch profile system
        |
        v
    TsBridgeLaunchTargetsProvider.QueryDebugTargetsAsync
        |
        +--> spawn "node server.js --f5"
        +--> wait for HTTP listener on 127.0.0.1:<port>
        +--> read .vscode/launch.json
        +--> build VsDebugTargetInfo4:
        |       dlo                   = 1  (CreateProcess)
        |       LaunchFlags           = 0x20  (StopDebuggingOnEnd)
        |       bstrExe               = "js-debug"
        |       bstrOptions           = <launch.json>
        |       bstrCurDir            = <project directory>
        |       guidLaunchDebugEngine = {394120B6-2FF9-4D0D-8953-913EF5CD0BCD}
        |       dwDebugEngineCount    = 1
        |       pDebugEngines         = <pointer to Guid bytes>
        |       project               = <IVsHierarchy>
        |
        +--> call IVsDebugger4.LaunchDebugTargets4(1, [target], [result])
        |
        +--> return empty IDebugLaunchSettings list
        |
        v
    Visual Studio JavaScript debug engine
        |
        v
    JavaScriptDebugAdapterBridge.exe
        |
        v
    js-debug
        |
        v
    Edge or Chrome with --remote-debugging-port
        |
        v
    CDP connection
        |
        v
    Breakpoint in ts/src/app.ts

2.4 THE PROJECT SHAPE

    MyApp/
        MyApp.csproj              (OutputType=Library, net8.0)
        MyApp.slnx
        Bridge.cs                 (generated)
        ts/
            src/app.ts
            src/default-definitions/typescript-bridge.d.ts
            package.json
            tsconfig.json
        config.json
        config.schema.json
        server.js
        Prepare-DebugSession.ps1
        .vscode/
            launch.json
            launch.template.json
        Properties/
            launchSettings.json
        README.md

    Single project. Single solution. No .esproj.

2.5 KEY DECISIONS

    D1.   The template generates one C# class library project.
    D2.   There is no .esproj.
    D3.   There is no Microsoft.VisualStudio.JavaScript.SDK dependency.
    D4.   Node.js remains the runtime for the debug HTTP server.
    D5.   server.js is unchanged.
    D6.   The TypescriptBridge VSIX owns F5 orchestration.
    D7.   The VSIX implements IDebugProfileLaunchTargetsProvider for
          the profile registration.
    D8.   The VSIX implements a direct call to
          IVsDebugger4.LaunchDebugTargets4 for the actual launch.
    D9.   The VSIX hands the launch.json content to the JavaScript
          debug engine through VsDebugTargetInfo4.bstrOptions.
    D10.  launch.json is regenerated by Prepare-DebugSession.ps1 on
          each F5.
    D11.  A bootstrap launch.json is shipped in the template.
    D12.  TypeScript sources live under ts/src/.
    D13.  tsconfig.json lives at ts/tsconfig.json.
    D14.  package.json lives at ts/package.json.
    D15.  The C# project is a library, not an executable.
    D16.  The VSIX installs itself into the Visual Studio extensions
          folder on first build of a TypeScript Bridge project.

================================================================================
PART 3 — DEPENDENCIES AND IMPORTS
================================================================================

3.1 THE VSIX REFERENCES

    Microsoft.VisualStudio.SDK 17.14.40265  (ExcludeAssets=runtime)
    System.ComponentModel.Composition 9.0.0
    Microsoft.VSSDK.BuildTools 18.5.38461

3.2 ADDITIONAL REFERENCES FROM THE VISUAL STUDIO INSTALLATION

    Microsoft.VisualStudio.ProjectSystem.dll
    Microsoft.VisualStudio.ProjectSystem.VS.dll
    Microsoft.VisualStudio.ProjectSystem.Managed.dll
    Microsoft.VisualStudio.ProjectSystem.Managed.VS.dll

    Microsoft.VisualStudio.Shell.Interop.11.0.dll  (for IVsDebugger4
                                                    and VsDebugTargetInfo4)

    All with <Private>true</Private> so they are copied into the VSIX.

3.3 REQUIRED NAMESPACES IN THE PROVIDER

    using Microsoft.VisualStudio.ProjectSystem;
    using Microsoft.VisualStudio.ProjectSystem.Debug;
    using Microsoft.VisualStudio.ProjectSystem.VS.Debug;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Shell.Interop;

3.4 THE SVsServiceProvider IMPORT

    [Import]
    internal SVsServiceProvider? ServiceProvider { get; set; }

    Used to obtain IVsDebugger4 from SVsShellDebugger.

    var debugger4 = ServiceProvider.GetService(typeof(SVsShellDebugger)) as IVsDebugger4;

================================================================================
PART 4 — REMAINING WORK
================================================================================

PHASE 3B — DEBUGGER ROUTING (current)
    Goal: Make Visual Studio start the JavaScript debug adapter.

    Steps:
        1. Rebuild the VSIX with the direct
           IVsDebugger4.LaunchDebugTargets4 call.
        2. Install into machine-wide extensions folder.
        3. Press F5 on Phase2Test.slnx.
        4. Verify that JavaScriptDebugAdapterBridge.exe starts.
        5. Verify that the browser launches with --remote-debugging-port.
        6. Verify that a breakpoint in app.ts becomes active.

    Deliverable:
        A working F5 debug session with a TypeScript breakpoint.

    Acceptance:
        A06 through A13 in the acceptance checklist.

    Fallback if this fails:
        Field-by-field comparison of the VsDebugTargetInfo4 we build
        against the one JSPS builds. The decompiled JSPS code gives
        us the exact reference shape. The comparison will reveal any
        remaining discrepancy.

PHASE 4 — BREAKPOINT IN TYPESCRIPT (blocked on 3B)
    Deliverable: Full debug session with breakpoints.
    Acceptance: A08 through A13.

PHASE 5 — TEMPLATE REFACTOR (blocked on 4)
    Deliverable: A single-project template.

PHASE 6 — EDGE CASES AND HARDENING (blocked on 5)
    Deliverable: All edge cases covered.

PHASE 7 — FINAL DOCUMENTATION (blocked on 6)
    Deliverable: Updated documentation.

================================================================================
PART 5 — CONSTRAINTS
================================================================================

R01. Node.js is the runtime. Do not replace it with a C# HTTP host.
R02. The VSIX owns F5 orchestration.
R03. Bridge.cs is generated by the C# build, not by F5.
R04. TypeScript sources live under ts/src/.
R05. tsconfig.json lives at ts/tsconfig.json.
R06. package.json lives at ts/package.json.
R07. The C# project is a library, not an executable.
R08. Debug artifacts live under obj/TypescriptBridge/debug/.
R09. Port from config.json only. No automatic fallback.
R10. server.js in --f5 mode does not launch a browser.
R11. server.js in direct mode launches a browser.
R12. The VSIX must not require an internet connection at runtime.
R13. The VSIX must not require elevated privileges.
R14. The generated project must build and run without any change
     to the global MSBuild or NuGet configuration.
R15. Follow the migration order: prove the replacement, then migrate,
     then remove the old mechanism.
R16. No destructive step before its replacement is verified.

================================================================================
PART 6 — ACCEPTANCE CHECKLIST
================================================================================

    PROJECT STRUCTURE
    A01. dotnet new typescript-bridge -n MyApp succeeds.
    A02. Solution Explorer shows exactly one project.
    A03. Under MyApp:
             ts/src/app.ts
             ts/package.json
             ts/tsconfig.json
             config.json
             README.md
             Bridge.cs
    A04. No infrastructure file is visible in Solution Explorer.
    A05. No BUILD_MODE error in Visual Studio.
    A06. No net6.0 error in Visual Studio.

    F5 AND DEBUGGING
    A07. F5 starts the Node server.
    A08. Browser opens automatically.
    A09. JavaScriptDebugAdapterBridge.exe starts.
    A10. Browser launches with --remote-debugging-port.
    A11. Breakpoint in ts/src/app.ts is hit.
    A12. Locals works.
    A13. Call Stack works.
    A14. F10, F11, and F5 (continue) all work.
    A15. Stop Debugging terminates the Node process.
    A16. HTTP port is released.
    A17. Second F5 works without restarting Visual Studio.
    A18. Changed TypeScript code executes on the next F5.
    A19. run.browser = "edge" works.
    A20. run.browser = "chrome" works.
    A21. run.port change works.
    A22. Occupied run.port fails with a clear error.

    C# CONSUMPTION
    A23. TypescriptProvider.TypescriptCode is available to C# consumers.
    A24. C# build generates Bridge.cs.

    VSIX
    A25. The VSIX appears in Tools > Extensions and Updates.
    A26. The VSIX is installed automatically on first build.
    A27. After one Visual Studio restart, F5 works on any
         TypeScript Bridge project.

    TESTS
    A28. Existing integration tests pass.
    A29. No stale .bak files or orphan directories remain.

================================================================================
PART 7 — RISKS
================================================================================

RISK 1 — IVsDebugger4.LaunchDebugTargets4 may be restricted to internal
         callers.

    Mitigation: The interface is public and documented. If the call
    is blocked, fall back to inspecting whether Visual Studio 2026
    has changed the interface to a later version.

RISK 2 — The VsDebugTargetInfo4 struct may need additional fields
         that our implementation does not set.

    Mitigation: Field-by-field comparison with the JSPS-produced
    target. The decompiled JSPS code gives us the reference shape.

RISK 3 — The pDebugEngines pointer may need a different format.

    Mitigation: Match exactly the format used by
    DebugLaunchProviderBase.GetDebugEngineBytes. Allocate 16 bytes
    per GUID, copy via Guid.ToByteArray, and set dwDebugEngineCount
    accordingly.

RISK 4 — The IVsHierarchy may be the wrong one for multi-project
         solutions.

    Mitigation: The MEF contract name resolves to the hierarchy of
    the current project. In a single-project solution this is
    unambiguous.

RISK 5 — Future Visual Studio versions may change the interface.

    Mitigation: Pin the VSIX to [18.0, 19.0). Test each new release
    before widening the range.

================================================================================
PART 8 — EXTERNAL REVIEW SUMMARY
================================================================================

The architecture in this document was informed by an external review
of an earlier problem statement. Key points from that review:

    - The IDebugProfileLaunchTargetsProvider approach is the correct
      launch layer for custom command names. It should be kept.
    - ExportDebugger("LaunchJsonDebugger") should not be used by a
      third-party extension. That name belongs to JSPS.
    - The enum DebugLaunchOperation does not contain "Launch".
      CreateProcess (1) is the correct value for our scenario.
    - The IVsDebugger4.LaunchDebugTargets4 approach bypasses the
      launch-profile routing. It is the correct low-level fallback.
    - Field-by-field comparison against the JSPS-produced
      VsDebugTargetInfo4 is the next diagnostic if the direct call
      still fails.

The external reviewer also noted that the JavaScript debugger is
shipped as part of Visual Studio, and that the exact wiring between
the shell and the JavaScript debug adapter is an implementation
detail rather than a stable public contract. This is a
maintenance caveat for the product.

================================================================================
END OF ARCHITECTURE AND IMPLEMENTATION PLAN v11.0
================================================================================
