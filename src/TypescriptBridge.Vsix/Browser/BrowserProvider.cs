using System.Collections.Generic;
using System.ComponentModel.Composition;
using TypescriptBridge.DebugHost;

namespace TypescriptBridge.Vsix.Browser
{
    // Discovers installed browsers and caches the result.
    //
    // The result is computed lazily on first call and then held for
    // the lifetime of the process. If the user installs a browser while
    // Visual Studio is running, the change is picked up on the next
    // Visual Studio restart.
    [Export(typeof(IBrowserProvider))]
    internal sealed class BrowserProvider : IBrowserProvider
    {
        private readonly object _gate = new();
        private IReadOnlyList<string>? _cached;

        public IReadOnlyList<string> GetInstalledBrowsers()
        {
            lock (_gate)
            {
                if (_cached is not null)
                {
                    return _cached;
                }

                var list = new List<string>(2);

                if (BrowserValidator.IsInstalled(BrowserValidator.Edge))
                {
                    list.Add(BrowserValidator.Edge);
                }

                if (BrowserValidator.IsInstalled(BrowserValidator.Chrome))
                {
                    list.Add(BrowserValidator.Chrome);
                }

                _cached = list;
                return _cached;
            }
        }
    }
}
