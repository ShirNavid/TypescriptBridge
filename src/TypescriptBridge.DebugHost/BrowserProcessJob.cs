using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TypescriptBridge.DebugHost
{
    // Wraps a Win32 Job Object that owns the browser process tree.
    //
    // When the job handle is closed, all processes assigned to the job
    // are killed by the operating system. This guarantees that the
    // browser is terminated when the debug session stops, without
    // resorting to taskkill or image-name based termination.
    internal sealed class BrowserProcessJob : IDisposable
    {
        private IntPtr _handle;

        // Creates a new job object configured to kill on close.
        public BrowserProcessJob()
        {
            _handle = CreateJobObject(IntPtr.Zero, null);
            if (_handle == IntPtr.Zero)
            {
                throw new InvalidOperationException(
                    $"CreateJobObject failed with Win32 error {Marshal.GetLastWin32Error()}.");
            }

            // Configure the job so that closing the handle kills all
            // assigned processes. This is the entire point of the job.
            var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
            info.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;

            var length = Marshal.SizeOf(info);
            var infoPtr = Marshal.AllocHGlobal(length);
            try
            {
                Marshal.StructureToPtr(info, infoPtr, false);

                var ok = SetInformationJobObject(
                    _handle,
                    JobObjectExtendedLimitInformation,
                    infoPtr,
                    (uint)length);

                if (!ok)
                {
                    throw new InvalidOperationException(
                        $"SetInformationJobObject failed with Win32 error {Marshal.GetLastWin32Error()}.");
                }
            }
            finally
            {
                Marshal.FreeHGlobal(infoPtr);
            }
        }

        // Assigns an already-started process to this job.
        public void AssignProcess(Process process)
        {
            if (process is null)
            {
                throw new ArgumentNullException(nameof(process));
            }

            var ok = AssignProcessToJobObject(_handle, process.Handle);
            if (!ok)
            {
                throw new InvalidOperationException(
                    $"AssignProcessToJobObject failed with Win32 error {Marshal.GetLastWin32Error()}.");
            }
        }

        // Closes the job handle. All assigned processes are killed.
        public void Dispose()
        {
            if (_handle != IntPtr.Zero)
            {
                CloseHandle(_handle);
                _handle = IntPtr.Zero;
            }
        }

        // ---------- Win32 interop ----------

        private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;
        private const int JobObjectExtendedLimitInformation = 9;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetInformationJobObject(
            IntPtr hJob,
            int jobObjectInformationClass,
            IntPtr lpJobObjectInformation,
            uint cbJobObjectInformationLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }
    }
}

