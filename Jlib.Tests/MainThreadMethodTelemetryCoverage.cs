using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using T = Jlib.Runtime.MainThreadMethodTelemetry;

internal static class MainThreadMethodTelemetryCoverage {
    private static readonly Exception Failure = new InvalidOperationException("original");
    private static bool snapshotInside, disableInside;
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Child() { Thread.SpinWait(20000); return 42; }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Parent() {
        int result = Child();
        if (snapshotInside) T.SnapshotAndReset();
        if (disableInside) T.Enabled = false;
        return result;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Throwing() { throw Failure; }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Recurse(int n) { return n == 0 ? Child() : Recurse(n - 1); }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Collect() { GC.Collect(0); GC.KeepAlive(new byte[8192]); }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CollectParent() { Collect(); }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Empty() { }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long MeasureCalls() {
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++) Empty();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
    private static void Generic<TValue>() { }
    [System.Runtime.InteropServices.DllImport("unused-telemetry-test")]
    private static extern void Native();
    private abstract class AbstractFixture { public abstract void Missing(); }
    private static MethodInfo Method(string name) => AccessTools.Method(typeof(MainThreadMethodTelemetryCoverage), name);
    private static T.Sample Find(T.Sample[] samples, string name) =>
        samples.Single(s => s.Name.Contains(" " + name + "("));

    internal static void Run() {
        var harmony = new Harmony("Jlib.Tests.MainThreadMethodTelemetry");
        var targets = new[] { Method(nameof(Child)), Method(nameof(Parent)), Method(nameof(Throwing)),
            Method(nameof(Recurse)), Method(nameof(Collect)), Method(nameof(CollectParent)), Method(nameof(Empty)) };
        Program.Equal(7, T.Install(harmony, targets), "installs ordinary return types");
        try {
            Program.Equal(0, T.Install(new Harmony(harmony.Id + ".other"), targets), "duplicate across owners");
            Program.Equal(0, T.Install(harmony, new MethodBase[] { Method(nameof(Generic)),
                AccessTools.Method(typeof(AbstractFixture), "Missing"),
                Method(nameof(Native)) }), "unsupported skipped");
            T.Enabled = true;
            Program.Equal(42, Parent(), "return untouched");
            var samples = T.SnapshotAndReset();
            var parent = Find(samples, nameof(Parent));
            var child = Find(samples, nameof(Child));
            Program.Equal(1L, parent.Calls, "parent counted");
            Program.Equal(parent.InclusiveTicks - child.InclusiveTicks, parent.SelfTicks, "nested self subtracts child");
            Program.Equal(child.InclusiveTicks, child.SelfTicks, "leaf self");
            if (Jlib.Runtime.ThreadCpuClock.Available) {
                Program.Check(parent.InclusiveCpuTicks > 0, "thread CPU is measured");
                Program.Equal(parent.InclusiveCpuTicks - child.InclusiveCpuTicks,
                    parent.SelfCpuTicks, "nested CPU self subtracts child");
                Program.Equal(child.InclusiveCpuTicks, child.SelfCpuTicks, "leaf CPU self");
            }
            Program.Equal(parent.InclusiveTicks, parent.MaxTicks, "maximum");
            Exception? caught = null;
            try { Throwing(); } catch (Exception error) { caught = error; }
            Program.Check(ReferenceEquals(Failure, caught), "original exception identity");
            Program.Equal(1L, T.SnapshotAndReset().Single().Calls, "throw counted");
            T.Enabled = false;
            Parent();
            Program.Equal(0, T.SnapshotAndReset().Length, "disabled empty");
            T.Enabled = true;
            Task.Run(() => Parent()).GetAwaiter().GetResult();
            Program.Equal(0, T.SnapshotAndReset().Length, "worker excluded");
            Task.Run(() => Program.Throws<InvalidOperationException>(() => T.SnapshotAndReset(),
                "worker snapshot rejected")).GetAwaiter().GetResult();
            snapshotInside = true;
            Parent();
            snapshotInside = false;
            Program.Equal(1L, Find(T.SnapshotAndReset(), nameof(Parent)).Calls, "snapshot keeps active scope");
            disableInside = true;
            Parent();
            disableInside = false;
            Program.Equal(1L, Find(T.SnapshotAndReset(), nameof(Parent)).Calls, "disable retains active scope");
            T.Enabled = true;
            Recurse(300);
            samples = T.SnapshotAndReset();
            Program.Equal(256L, samples.Sum(s => s.Calls), "bounded captured recursion");
            Program.Equal(46L, T.NestingOverflowCount, "all excess invocations visible");
            Parent();
            Program.Equal(2L, T.SnapshotAndReset().Sum(s => s.Calls), "stack recovers after overflow");
            CollectParent();
            samples = T.SnapshotAndReset();
            Program.Equal(1L, Find(samples, nameof(Collect)).GcCollections, "gen0 collection");
            Program.Equal(1L, Find(samples, nameof(CollectParent)).GcCollections, "ancestor includes same collection");
            Program.Check(T.AllocationCounterAvailable, "host allocation supported");
            Program.Check(Find(samples, nameof(Collect)).AllocatedBytes >= 8192, "allocation measured");
            for (int i = 0; i < 10; i++) MeasureCalls();
            T.SnapshotAndReset();
            long allocated = MeasureCalls();
            Program.Equal(0L, allocated, "no per-call allocation");
            Program.Equal(10000L, T.SnapshotAndReset().Single().Calls, "empty calls captured");

            var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("TelemetryBound"), AssemblyBuilderAccess.Run);
            var type = assembly.DefineDynamicModule("main").DefineType("Targets");
            for (int i = 0; i < T.MethodLimit; i++) {
                var method = type.DefineMethod("M" + i, MethodAttributes.Public | MethodAttributes.Static, typeof(void), Type.EmptyTypes);
                method.GetILGenerator().Emit(OpCodes.Ret);
            }
            var methods = type.CreateType()!.GetMethods(BindingFlags.Public | BindingFlags.Static);
            Program.Equal(T.MethodLimit - targets.Length, T.Install(harmony, methods), "method limit enforced");
            Program.Equal(T.MethodLimit, T.InstalledMethodCount, "successful count bounded");
            Program.Equal((long)targets.Length, T.MethodOverflowCount, "method overflow visible");
        } finally {
            T.Enabled = false;
            harmony.UnpatchAll(harmony.Id);
        }
    }
}
