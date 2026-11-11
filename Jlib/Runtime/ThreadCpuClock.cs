using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Jlib.Runtime {
    // Diagnostic-only Linux thread CPU time. Wall time also includes descheduling,
    // paging and GPU/job waits, which must not be reported as CPU execution.
    public static class ThreadCpuClock {
        public const long Frequency = 1000000000;
        public static readonly bool Available = Probe();

        [StructLayout(LayoutKind.Sequential)]
        private struct Timespec {
            internal long Seconds, Nanoseconds;
        }

        [DllImport("libc", EntryPoint = "clock_gettime")]
        private static extern int ReadNative(int clock, out Timespec value);

        private static bool Probe() {
            if (IntPtr.Size != 8 || Environment.OSVersion.Platform != PlatformID.Unix
                || !File.Exists("/proc/self/stat")) return false;
            try { return ReadNative(3, out _) == 0; }
            catch (DllNotFoundException) { return false; }
            catch (EntryPointNotFoundException) { return false; }
        }

        public static long Read() {
            if (!Available || ReadNative(3, out var value) != 0) return 0;
            return value.Seconds * Frequency + value.Nanoseconds;
        }
    }
}
