using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace Jlib.Runtime {
    /// <summary>Opt-in measurements of the original state machine bucket callbacks.</summary>
    public static class StateMachineBucketTelemetry {
        private const int MaximumNames = 1024;
        private const int MaximumNameLength = 256;
        private const string OverflowName = "<overflow>";
        private static readonly object Gate = new object();
        private static readonly object InstallGate = new object();
        private static readonly Dictionary<string, Sample> Samples = new Dictionary<string, Sample>(StringComparer.Ordinal);
        private static readonly Func<long> ReadAllocatedBytes = ProbeAllocationCounter();
        private static volatile bool enabled;

        /// <summary>Defaults to false. Calls already being measured finish recording after disabling.</summary>
        public static bool Enabled { get => enabled; set => enabled = value; }

        /// <summary>Installs once across Harmony owners. Missing or changed call sites fail explicitly.</summary>
        public static void Install(Harmony harmony) {
            if (harmony == null) throw new ArgumentNullException(nameof(harmony));
            lock (InstallGate) {
                var target = AccessTools.Method(typeof(StateMachineUpdater.BucketGroup),
                    "AdvanceOneSubTick", new[] { typeof(float) });
                if (target == null) throw new MissingMethodException("BucketGroup.AdvanceOneSubTick(float)");
                var transpiler = AccessTools.Method(typeof(StateMachineBucketTelemetry), nameof(Transpile));
                var patches = Harmony.GetPatchInfo(target);
                if (patches != null) {
                    foreach (var patch in patches.Transpilers)
                        if (patch.PatchMethod == transpiler) return;
                }
                harmony.Patch(target, transpiler: new HarmonyMethod(transpiler));
            }
        }

        /// <summary>
        /// Atomically copies and clears completed samples. In-flight callbacks appear in a later snapshot.
        /// At most 1024 names are retained, including an overflow aggregate for excess names or names
        /// longer than 256 characters. Null names also use that aggregate. Ticks use Stopwatch.Frequency.
        /// AllocatedBytes is zero when the runtime does not support a per-thread allocation counter.
        /// </summary>
        public static Sample[] SnapshotAndReset() {
            lock (Gate) {
                var result = new Sample[Samples.Count];
                Samples.Values.CopyTo(result, 0);
                Samples.Clear();
                return result;
            }
        }

        /// <summary>Immutable aggregate for one bucket name.</summary>
        public readonly struct Sample {
            public string Name { get; }
            public long Calls { get; }
            public long Ticks { get; }
            public long AllocatedBytes { get; }
            public long MaxTicks { get; }

            internal Sample(string name, long calls, long ticks, long allocatedBytes, long maxTicks) {
                Name = name;
                Calls = calls;
                Ticks = ticks;
                AllocatedBytes = allocatedBytes;
                MaxTicks = maxTicks;
            }
        }

        private static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions) {
            var original = AccessTools.Method(typeof(StateMachineUpdater.BaseUpdateBucket),
                "Update", new[] { typeof(float) });
            var wrapper = AccessTools.Method(typeof(StateMachineBucketTelemetry), nameof(Update));
            int replacements = 0;
            var result = new List<CodeInstruction>();
            foreach (var instruction in instructions) {
                if (instruction.opcode == OpCodes.Callvirt && Equals(instruction.operand, original)) {
                    // Mutating the instruction preserves branch labels and exception block metadata.
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = wrapper;
                    replacements++;
                }
                result.Add(instruction);
            }
            if (replacements != 1)
                throw new InvalidOperationException("Expected exactly one bucket Update(float) virtual call.");
            return result;
        }

        private static void Update(StateMachineUpdater.BaseUpdateBucket bucket, float dt) {
            if (!enabled) {
                bucket.Update(dt);
                return;
            }
            long allocated = AllocationCount();
            long started = Stopwatch.GetTimestamp();
            try {
                bucket.Update(dt);
            } finally {
                long elapsed = Stopwatch.GetTimestamp() - started;
                long bytes = Math.Max(0, AllocationCount() - allocated);
                // Diagnostic failures must never replace a callback's exception or stop simulation.
                try {
                    string name = bucket.name;
                    lock (Gate) {
                        if (name == null || name.Length > MaximumNameLength ||
                                (!Samples.ContainsKey(name) && Samples.Count >= MaximumNames - 1))
                            name = OverflowName;
                        Samples.TryGetValue(name, out var previous);
                        Samples[name] = new Sample(name, previous.Calls + 1,
                            previous.Ticks + elapsed, previous.AllocatedBytes + bytes,
                            Math.Max(previous.MaxTicks, elapsed));
                    }
                } catch (Exception) { }
            }
        }

        private static long AllocationCount() {
            try { return ReadAllocatedBytes == null ? 0 : ReadAllocatedBytes(); }
            catch (Exception) { return 0; }
        }

        private static Func<long> ProbeAllocationCounter() {
            try {
                var method = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread",
                    BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
                if (method == null || method.ReturnType != typeof(long)) return null;
                var counter = (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), method);
                long before = counter();
                var probe = new byte[128];
                long after = counter();
                GC.KeepAlive(probe);
                // Some Unity Mono versions expose a stub which always returns zero.
                return after > before ? counter : null;
            } catch (Exception) { return null; }
        }
    }
}
