// TypescriptBridge.DebugHost is intentionally Windows-only.
// It uses the Win32 registry (via Microsoft.Win32) and Job Objects
// (via kernel32). Declaring the supported platform at the assembly
// level allows the analyzer to treat the whole assembly as Windows-only.
//
// SupportedOSPlatformAttribute does not exist on .NET Framework 4.7.2,
// so the attribute is only emitted for the net8.0 target.

#if NET8_0_OR_GREATER
using System.Runtime.Versioning;

[assembly: SupportedOSPlatform("windows")]
#endif
