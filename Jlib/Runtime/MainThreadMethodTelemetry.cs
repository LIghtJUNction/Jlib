using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using HarmonyLib;

namespace Jlib.Runtime {
    /// <summary>
    /// Main-thread instrumentation. First Install must run on the game's main thread.
    /// Install and SnapshotAndReset require that same thread. GC counts use gen0 only
    /// (Boehm has one generation). GC and allocation samples include children and must
    /// not be summed across ancestors. Wall ticks use Stopwatch.Frequency and CPU
    /// ticks use ThreadCpuClock.Frequency.
    /// </summary>
    public static class MainThreadMethodTelemetry {
        public const int MethodLimit = 1024;
        public const int NestingLimit = 256;
        public struct Sample {
            public string Name;
            public long Calls, InclusiveTicks, SelfTicks, MaxTicks, GcCollections, AllocatedBytes;
            public long InclusiveCpuTicks, SelfCpuTicks;
        }
        private struct Frame {
            internal long Start, Children, Bytes;
            internal long CpuStart, CpuChildren;
            internal int Gc;
        }
        // Value-type Harmony state, including for skipped/overflowing invocations.
        public struct Scope {
            internal int Method, Slot;
            internal bool Active, Overflow;
        }
        private static readonly ModuleBuilder PatchModule = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("Jlib.MainThreadMethodTelemetry.Patches"), AssemblyBuilderAccess.Run)
            .DefineDynamicModule("Patches");
        private static int patchSequence;
        private static readonly Dictionary<MethodBase, int> Methods = new Dictionary<MethodBase, int>();
        private static readonly Sample[] Totals = new Sample[MethodLimit];
        private static readonly Frame[] Frames = new Frame[NestingLimit];
        private static readonly Func<long> AllocationCounter = CreateAllocationCounter();
        private static int mainThread, depth;
        private static volatile bool enabled;
        public static bool Enabled { get { return enabled; } set { enabled = value; } }
        public static bool AllocationCounterAvailable { get { return AllocationCounter != null; } }
        public static int InstalledMethodCount { get { return Methods.Count; } }
        public static long MethodOverflowCount { get; private set; }
        public static long NestingOverflowCount { get; private set; }

        private static Func<long> CreateAllocationCounter() {
            var method = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", Type.EmptyTypes);
            if (method == null) return null;
            try {
                var counter = (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), method);
                long before = counter();
                var probe = new byte[128];
                long after = counter();
                GC.KeepAlive(probe);
                return after > before ? counter : null;
            } catch (NotSupportedException) { return null; }
        }
        private static void RequireMainThread() {
            if (Thread.CurrentThread.ManagedThreadId != mainThread)
                throw new InvalidOperationException("Telemetry installation and snapshots require the installation thread.");
        }
        /// <summary>
        /// Returns newly installed count. Repeated methods are ignored across Harmony owners.
        /// Unsupported bodies are skipped. Patch failures throw with the target identified;
        /// earlier successful installations remain visible in InstalledMethodCount.
        /// Overflow counters are cumulative since installation and are not reset by snapshots.
        /// </summary>
        public static int Install(Harmony harmony, IEnumerable<MethodBase> methods) {
            if (harmony == null) throw new ArgumentNullException(nameof(harmony));
            if (methods == null) throw new ArgumentNullException(nameof(methods));
            Interlocked.CompareExchange(ref mainThread, Thread.CurrentThread.ManagedThreadId, 0);
            RequireMainThread();
            int installed = 0;
            foreach (var method in methods) {
                if (method == null || Methods.ContainsKey(method) || method.IsAbstract ||
                    method.ContainsGenericParameters || method.GetMethodBody() == null) continue;
                if (Methods.Count == MethodLimit) { MethodOverflowCount++; continue; }
                int index = Methods.Count;
                Totals[index].Name = method.DeclaringType?.FullName + "." + method;
                var wrapper = CreateWrapper(index);
                var prefix = wrapper.GetMethod("Prefix");
                var finalizer = wrapper.GetMethod("Finalizer");
                try {
                    harmony.Patch(method, prefix: new HarmonyMethod(prefix),
                        finalizer: new HarmonyMethod(finalizer));
                } catch (Exception error) {
                    harmony.Unpatch(method, prefix);
                    harmony.Unpatch(method, finalizer);
                    throw new InvalidOperationException("Failed to instrument " + method, error);
                }
                Methods.Add(method, index);
                installed++;
            }
            return installed;
        }
        // A baked integer avoids Harmony's allocating GetMethodFromHandle injection.
        // Both wrappers share a declaring type so Harmony shares their struct __state.
        private static Type CreateWrapper(int index) {
            var type = PatchModule.DefineType("Patch" + patchSequence++, TypeAttributes.Public);
            var prefix = type.DefineMethod("Prefix", MethodAttributes.Public | MethodAttributes.Static,
                typeof(void), new[] { typeof(Scope).MakeByRefType() });
            prefix.DefineParameter(1, ParameterAttributes.Out, "__state");
            var il = prefix.GetILGenerator();
            il.Emit(OpCodes.Ldc_I4, index);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, typeof(MainThreadMethodTelemetry).GetMethod(nameof(BeginScope)));
            il.Emit(OpCodes.Ret);
            var finalizer = type.DefineMethod("Finalizer", MethodAttributes.Public | MethodAttributes.Static,
                typeof(Exception), new[] { typeof(Exception), typeof(Scope) });
            finalizer.DefineParameter(1, ParameterAttributes.None, "__exception");
            finalizer.DefineParameter(2, ParameterAttributes.None, "__state");
            il = finalizer.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Call, typeof(MainThreadMethodTelemetry).GetMethod(nameof(EndScope)));
            il.Emit(OpCodes.Ret);
            return type.CreateType();
        }
        /// <summary>Infrastructure entry point for generated Harmony wrappers only.</summary>
        public static void BeginScope(int index, out Scope __state) {
            __state = default(Scope);
            if (!enabled || Thread.CurrentThread.ManagedThreadId != mainThread) return;
            __state.Active = true;
            __state.Method = index;
            if (depth == NestingLimit) {
                __state.Overflow = true;
                NestingOverflowCount++;
                return;
            }
            __state.Slot = depth++;
            Frames[__state.Slot] = new Frame {
                Gc = GC.CollectionCount(0),
                Bytes = AllocationCounter == null ? 0 : AllocationCounter(),
                Start = Stopwatch.GetTimestamp(),
                CpuStart = ThreadCpuClock.Read()
            };
        }
        // Returning the same exception preserves Harmony's original throw behavior.
        public static Exception EndScope(Exception __exception, Scope __state) {
            if (!__state.Active) return __exception;
            if (__state.Overflow) return __exception;
            long cpuEnd = ThreadCpuClock.Read();
            long end = Stopwatch.GetTimestamp();
            Frame frame = Frames[__state.Slot];
            long ticks = end - frame.Start;
            long cpuTicks = Math.Max(0, cpuEnd - frame.CpuStart);
            depth--;
            if (depth > 0) {
                Frames[depth - 1].Children += ticks;
                Frames[depth - 1].CpuChildren += cpuTicks;
            }
            ref Sample total = ref Totals[__state.Method];
            total.Calls++;
            total.InclusiveTicks += ticks;
            total.SelfTicks += Math.Max(0, ticks - frame.Children);
            total.InclusiveCpuTicks += cpuTicks;
            total.SelfCpuTicks += Math.Max(0, cpuTicks - frame.CpuChildren);
            total.MaxTicks = Math.Max(total.MaxTicks, ticks);
            total.GcCollections += GC.CollectionCount(0) - frame.Gc;
            if (AllocationCounter != null) total.AllocatedBytes += AllocationCounter() - frame.Bytes;
            return __exception;
        }
        /// <summary>
        /// Clears completed aggregates only. Active scopes retain their complete lifetime.
        /// Beyond depth 256, calls are omitted and their time remains in the deepest
        /// captured scope's self time. NestingOverflowCount makes that loss visible.
        /// </summary>
        public static Sample[] SnapshotAndReset() {
            RequireMainThread();
            int count = 0;
            for (int i = 0; i < Methods.Count; i++) if (Totals[i].Calls != 0) count++;
            var result = new Sample[count];
            int next = 0;
            for (int i = 0; i < Methods.Count; i++) {
                if (Totals[i].Calls == 0) continue;
                result[next++] = Totals[i];
                string name = Totals[i].Name;
                Totals[i] = new Sample { Name = name };
            }
            return result;
        }
    }
}
