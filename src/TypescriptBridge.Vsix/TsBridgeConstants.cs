using System;

namespace TypescriptBridge.Vsix
{
    internal static class TsBridgeConstants
    {
        public const string CommandName = "typescript-bridge";

        // The debugger name that VS uses to identify the debugger
        // provider. This is the value that appears in the
        // launchSettings.json profile's debuggerName field, if any.
        // For commandName-based routing, this is NOT used directly
        // by VS; instead VS invokes our IDebugLaunchProvider when
        // the active profile's commandName matches.
        public const string DebuggerName = "TypescriptBridgeDebugger";

        public static readonly Guid JavaScriptDebugEngineGuid =
            new Guid("394120B6-2FF9-4D0D-8953-913EF5CD0BCD");

        public const string JavaScriptDebugExecutable = "js-debug";

        public const int JavaScriptAdditionalLaunchOption = 0x20;

        public const string LogFileName = "TypescriptBridge.Vsix.log";
    }
}
