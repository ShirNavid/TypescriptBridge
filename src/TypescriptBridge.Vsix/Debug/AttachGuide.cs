using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace TypescriptBridge.Vsix.Debug
{
    // Guides the user through a manual debugger attach.
    //
    // B0 established that Visual Studio's built-in JavaScript/TypeScript
    // debugger can attach to Edge through the Chrome DevTools Protocol
    // (CDP) when the URL and the remote debugging port are known.
    //
    // This class:
    //   - writes a clear instruction block to a dedicated Output pane
    //   - copies the URL to the clipboard
    //   - shows a short modal dialog with the essential information
    internal static class AttachGuide
    {
        private static readonly Guid PaneGuid = new Guid("a3b1c2d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d");
        private const string PaneTitle = "TypescriptBridge";

        // Shows the attach guidance for a running session.
        public static async Task ShowAsync(DebugManifest manifest)
        {
            if (manifest is null) throw new ArgumentNullException(nameof(manifest));

            var url = $"http://127.0.0.1:{manifest.Port}/index.html";
            var cdp = $"http://127.0.0.1:{manifest.Port}";

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                Clipboard.SetText(url);
            }
            catch
            {
                // Clipboard access can fail transiently. Not fatal.
            }

            WriteToOutputWindow(manifest, url, cdp);

            MessageBox.Show(
                "TypescriptBridge: debug session is running.\n\n" +
                "To attach the Visual Studio JavaScript debugger:\n\n" +
                "  1. In Visual Studio, open the Debug menu.\n" +
                "  2. Choose 'Attach to Process...'.\n" +
                "  3. In the 'Attach to' field, select:\n" +
                "     JavaScript and TypeScript (Chrome DevTools Protocol / V8 Inspector)\n" +
                "  4. Select the msedge.exe process that is running with the URL below.\n" +
                "  5. Click Attach.\n\n" +
                "URL (already copied to clipboard):\n" +
                url + "\n\n" +
                "Once attached, breakpoints in ts/app.ts will bind.",
                "TypescriptBridge — Attach the debugger",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        // Writes a detailed instruction block to the dedicated pane.
        // Must be called on the UI thread.
        private static void WriteToOutputWindow(DebugManifest manifest, string url, string cdp)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var outputWindow = GetOutputWindow();
            if (outputWindow is null)
            {
                return;
            }

            var pane = GetOrCreatePane(outputWindow);
            if (pane is null)
            {
                return;
            }

            pane.OutputString("===== TypescriptBridge debug session =====\n");
            pane.OutputString($"Session:  {manifest.SessionId}\n");
            pane.OutputString($"Project:  {manifest.ProjectRoot}\n");
            pane.OutputString($"Root:     {manifest.SessionRoot}\n");
            pane.OutputString($"JS:       {manifest.JavaScript}\n");
            pane.OutputString($"Map:      {manifest.SourceMap}\n");
            pane.OutputString($"URL:      {url}\n");
            pane.OutputString($"CDP:      {cdp}\n");
            pane.OutputString("\n");
            pane.OutputString("Attach instructions:\n");
            pane.OutputString("  1. Debug -> Attach to Process...\n");
            pane.OutputString("  2. Attach to: JavaScript and TypeScript (Chrome DevTools Protocol / V8 Inspector)\n");
            pane.OutputString("  3. Select msedge.exe with the matching URL.\n");
            pane.OutputString("  4. Click Attach.\n");
            pane.OutputString("\n");
            pane.OutputString("To stop: run 'Start TypeScript Debugging' again to end the current session.\n");
            pane.OutputString("===========================================\n");
        }

        // Resolves the VS Output window service. Must be called on the
        // UI thread.
        private static IVsOutputWindow? GetOutputWindow()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var service = ServiceProvider.GlobalProvider
                .GetService(typeof(SVsOutputWindow));

            return service as IVsOutputWindow;
        }

        // Returns the TypescriptBridge pane, creating it on first use.
        private static IVsOutputWindowPane? GetOrCreatePane(IVsOutputWindow outputWindow)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var paneGuid = PaneGuid;

            var hr = outputWindow.GetPane(ref paneGuid, out var pane);
            if (ErrorHandler.Succeeded(hr) && pane is not null)
            {
                return pane;
            }

            hr = outputWindow.CreatePane(
                ref paneGuid,
                PaneTitle,
                fInitVisible: 0,
                fClearWithSolution: 0);

            if (ErrorHandler.Failed(hr))
            {
                return null;
            }

            hr = outputWindow.GetPane(ref paneGuid, out pane);
            if (ErrorHandler.Failed(hr))
            {
                return null;
            }

            return pane;
        }
    }
}
