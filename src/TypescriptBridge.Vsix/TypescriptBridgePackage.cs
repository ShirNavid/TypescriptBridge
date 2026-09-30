using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace TypescriptBridge.Vsix
{
    // Main entry point for the TypescriptBridge Visual Studio extension.
    // Provides the package that hosts the "Start TypeScript Debugging"
    // and "Stop TypeScript Debugging" commands.
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("TypescriptBridge", "TypeScript debugging for TypescriptBridge projects.", "1.0.1")]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(PackageGuidString)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "PackageGuidString is a standard suffix.")]
    public sealed class TypescriptBridgePackage : AsyncPackage
    {
        // Unique GUID for the package.
        public const string PackageGuidString = "b3b3f7a1-4f2e-4d6a-9c1a-2f7e5b8d9c3e";

        // Called asynchronously when Visual Studio loads the package.
        protected override async Task InitializeAsync(
            CancellationToken cancellationToken,
            IProgress<ServiceProgressData> progress)
        {
            await base.InitializeAsync(cancellationToken, progress);

            // Switch to the UI thread to initialize commands.
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            // Register commands.
            await StartTypescriptDebugCommand.InitializeAsync(this, cancellationToken);
        }

        // Called when the package is disposed. Ensures that any
        // running debug session is stopped cleanly.
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                StartTypescriptDebugCommand.Shutdown();
            }

            base.Dispose(disposing);
        }
    }
}

