using System.Collections.Generic;

namespace TypescriptBridge.Vsix.Browser
{
    // Provides the list of browsers installed on the current machine.
    //
    // Implemented by BrowserProvider and consumed by the JSON schema
    // selector to filter the run.browser enum.
    internal interface IBrowserProvider
    {
        // Returns the identifiers of all supported browsers that are
        // installed on this machine. The returned list contains a subset
        // of { "edge", "chrome" }. It may be empty if none are found.
        IReadOnlyList<string> GetInstalledBrowsers();
    }
}
