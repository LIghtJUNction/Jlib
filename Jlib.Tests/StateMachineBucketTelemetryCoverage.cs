using System.Runtime.CompilerServices;
using HarmonyLib;
using Jlib.Runtime;

// Host fixture mirrors the inspected game's bucket call site. No game assemblies execute.
public class StateMachineUpdater {
    public abstract class BaseUpdateBucket {
        public string name { get; }
        public abstract int count { get; }
        protected BaseUpdateBucket(string name) { this.name = name; }
        public abstract void Update(float dt);
    }

    public class BucketGroup {
        private readonly List<BaseUpdateBucket> buckets = new();
        public void AddBucket(BaseUpdateBucket bucket) => buckets.Add(bucket);
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void AdvanceOneSubTick(float dt) {
            int count = buckets.Count;
            for (int i = 0; i < count; i++) {
                BaseUpdateBucket bucket = buckets[i];
                if (bucket.count != 0) bucket.Update(dt);
            }
        }
    }
}

internal static class StateMachineBucketTelemetryCoverage {
    private sealed class Bucket(string name, Action<float> callback, int itemCount = 1)
            : StateMachineUpdater.BaseUpdateBucket(name) {
        public override int count => itemCount;
        public override void Update(float dt) => callback(dt);
    }

    internal static void Run() {
        var harmony = new Harmony("Jlib.Tests.BucketTelemetry");
        StateMachineBucketTelemetry.Install(harmony);
        StateMachineBucketTelemetry.Install(new Harmony("Jlib.Tests.BucketTelemetry.Other"));
        var target = AccessTools.Method(typeof(StateMachineUpdater.BucketGroup), "AdvanceOneSubTick");
        Program.Equal(1, Harmony.GetPatchInfo(target).Transpilers.Count, "install is idempotent across owners");
        try {
            StateMachineBucketTelemetry.SnapshotAndReset();
            var order = new List<int>();
            var group = new StateMachineUpdater.BucketGroup();
            group.AddBucket(new Bucket("same", dt => { Program.Equal(0.25f, dt, "dt preserved"); order.Add(1); }));
            group.AddBucket(new Bucket("empty", _ => throw new Exception("empty bucket ran"), 0));
            group.AddBucket(new Bucket("same", _ => order.Add(2)));
            StateMachineBucketTelemetry.Enabled = false;
            group.AdvanceOneSubTick(0.25f);
            Program.Equal("1,2", string.Join(",", order), "disabled original order");
            Program.Equal(0, StateMachineBucketTelemetry.SnapshotAndReset().Length, "disabled has no samples");
            order.Clear();
            StateMachineBucketTelemetry.Enabled = true;
            group.AdvanceOneSubTick(0.25f);
            Program.Equal("1,2", string.Join(",", order), "enabled original order");
            var sample = StateMachineBucketTelemetry.SnapshotAndReset().Single();
            Program.Equal("same", sample.Name, "aggregate by bucket name");
            Program.Equal(2L, sample.Calls, "both callbacks counted");
            Program.Check(sample.Ticks >= 0 && sample.AllocatedBytes >= 0, "nonnegative counters");
            Program.Equal(0, StateMachineBucketTelemetry.SnapshotAndReset().Length, "snapshot resets");

            var timed = new StateMachineUpdater.BucketGroup();
            long callbackTicks = 0;
            timed.AddBucket(new Bucket("timed", _ => {
                long start = System.Diagnostics.Stopwatch.GetTimestamp();
                Thread.Sleep(10);
                callbackTicks = System.Diagnostics.Stopwatch.GetTimestamp() - start;
            }));
            timed.AdvanceOneSubTick(1);
            Program.Check(StateMachineBucketTelemetry.SnapshotAndReset().Single().Ticks >= callbackTicks,
                "recorded ticks enclose the complete callback");

            var failure = new InvalidOperationException("original failure");
            var throwing = new StateMachineUpdater.BucketGroup();
            throwing.AddBucket(new Bucket("throw", _ => throw failure));
            throwing.AddBucket(new Bucket("unreachable", _ => throw new Exception("continued after failure")));
            foreach (bool enabled in new[] { false, true }) {
                StateMachineBucketTelemetry.Enabled = enabled;
                Exception? caught = null;
                try { throwing.AdvanceOneSubTick(1); } catch (Exception e) { caught = e; }
                Program.Check(ReferenceEquals(failure, caught), "same exception propagates");
                var samples = StateMachineBucketTelemetry.SnapshotAndReset();
                Program.Equal(enabled ? 1 : 0, samples.Length, "finally counts throwing callbacks only when enabled");
                if (enabled) Program.Equal("throw", samples.Single().Name, "following bucket never executes");
            }

            var toggling = new StateMachineUpdater.BucketGroup();
            toggling.AddBucket(new Bucket("toggle", _ => StateMachineBucketTelemetry.Enabled = false));
            toggling.AddBucket(new Bucket("disabled-next", _ => { }));
            StateMachineBucketTelemetry.Enabled = true;
            toggling.AdvanceOneSubTick(1);
            Program.Equal("toggle", StateMachineBucketTelemetry.SnapshotAndReset().Single().Name,
                "in-flight sample finishes and next callback sees disabled");

            StateMachineBucketTelemetry.Enabled = true;
            var allocated = new StateMachineUpdater.BucketGroup();
            allocated.AddBucket(new Bucket("allocation", _ => GC.KeepAlive(new byte[8192])));
            allocated.AdvanceOneSubTick(1);
            Program.Check(StateMachineBucketTelemetry.SnapshotAndReset().Single().AllocatedBytes >= 8192,
                "host optional allocation delegate measures original allocation");

            var bounded = new StateMachineUpdater.BucketGroup();
            for (int i = 0; i < 2000; i++) bounded.AddBucket(new Bucket("bucket" + i, _ => { }));
            bounded.AddBucket(new Bucket(new string('x', 10000), _ => { }));
            bounded.AdvanceOneSubTick(1);
            var limited = StateMachineBucketTelemetry.SnapshotAndReset();
            Program.Check(limited.Length <= 1024, "name cardinality bounded");
            Program.Equal(2001L, limited.Sum(s => s.Calls), "overflow preserves total calls");
            Program.Check(limited.All(s => s.Name.Length <= 256), "retained name size bounded");

            var concurrent = new StateMachineUpdater.BucketGroup();
            concurrent.AddBucket(new Bucket("parallel", _ => Thread.SpinWait(100)));
            long total = 0;
            var worker = Task.Run(() => Parallel.For(0, 4000, _ => concurrent.AdvanceOneSubTick(1)));
            while (!worker.IsCompleted) {
                total += StateMachineBucketTelemetry.SnapshotAndReset().Sum(s => s.Calls);
                Thread.Yield();
            }
            worker.GetAwaiter().GetResult();
            total += StateMachineBucketTelemetry.SnapshotAndReset().Sum(s => s.Calls);
            Program.Equal(4000L, total, "concurrent snapshots neither lose nor duplicate completed samples");
        } finally {
            StateMachineBucketTelemetry.Enabled = false;
            StateMachineBucketTelemetry.SnapshotAndReset();
            harmony.Unpatch(target, HarmonyPatchType.Transpiler, harmony.Id);
        }
    }
}
