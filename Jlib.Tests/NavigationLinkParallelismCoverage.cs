#nullable disable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using Optimization = Jlib.Runtime.NavigationLinkParallelism;
using Graph = NavigationGraphExpansionCoverage.Graph;

internal static class NavigationLinkParallelismCoverage
{
    private const string Owner = "Jlib.Tests.NavigationLinkParallelism";
    private static int assertions;

    internal static void Run(bool benchmark)
    {
        var harmony = new Harmony(Owner);
        NavigationGraphExpansionCoverage.Cells.Width = 64;
        NavigationGraphExpansionCoverage.Cells.Height = 64;
        var method = AccessTools.Method(typeof(Updater), "UpdateLinks");
        var random = new Random(7221);
        foreach (int count in new[] { 0, 1, 511, 512, 1000, 4096 })
        {
            int[] cells = Enumerable.Range(0, 4096).OrderBy(_ => random.Next()).Take(count).ToArray();
            var table = new Table(4096);
            var baseline = new Graph.Link[4096];
            var candidate = new Graph.Link[4096];
            var list = cells.ToList();
            var transitions = Transitions();
            var teleports = new Dictionary<int, int>();
            harmony.Unpatch(method, HarmonyPatchType.Prefix, Owner);
            Updater.Update(list, table, baseline, transitions, teleports);
            Check(Optimization.Install(harmony), Optimization.LastFailure);
            int dispatches = GlobalJobManager.Runs;
            Updater.Update(list, table, candidate, transitions, teleports);
            Check(GlobalJobManager.Runs == dispatches + (count >= 512 && GlobalJobManager.ThreadCount > 1 ? 1 : 0),
                "parallel path executes for eligible workloads");
            Check(baseline.Select(item => item.Value).SequenceEqual(candidate.Select(item => item.Value)), "every link word matches");
        }
        var duplicates = Enumerable.Repeat(7, 600).ToList();
        var state = new Table(4096);
        var links = new Graph.Link[4096];
        int runs = GlobalJobManager.Runs;
        Updater.Update(duplicates, state, links, Transitions(), new Dictionary<int, int>());
        Check(GlobalJobManager.Runs == runs, "duplicate destinations stay serial");
        var empty = new Table(4096);
        empty.ClearValid();
        runs = GlobalJobManager.Runs;
        Updater.Update(Enumerable.Range(0, 4096).ToList(), empty, links, Transitions(), new Dictionary<int, int>());
        Check(GlobalJobManager.Runs == runs, "empty source masks avoid scheduling overhead");
        NavigationGraphExpansionCoverage.Cells.Height = 512;
        var bulkEmpty = new Table(32768);
        bulkEmpty.ClearValid();
        var bulkLinks = Enumerable.Repeat(new Graph.Link { Value = -1 }, 32768).ToArray();
        runs = GlobalJobManager.Runs;
        Updater.Update(Enumerable.Range(0, 32768).ToList(), bulkEmpty, bulkLinks,
            Transitions(), new Dictionary<int, int>());
        Check(GlobalJobManager.Runs == runs + (GlobalJobManager.ThreadCount > 1 ? 1 : 0),
            "very large empty updates still use parallel clearing");
        Check(bulkLinks.All(link => link.Value == 0), "every empty link was cleared");
        NavigationGraphExpansionCoverage.Cells.Height = 64;
        // A graph update executed inside another job must keep that runner intact.
        var nested = new WorkItemCollection<Nested, object>();
        nested.Reset(null);
        nested.Add(new Nested());
        runs = GlobalJobManager.Runs;
        GlobalJobManager.Run(nested);
        Check(GlobalJobManager.Runs == runs + 1, "no nested runner dispatch");
        // Worker exceptions return to the caller and release reusable references.
        state.FailCell = 7;
        try
        {
            Updater.Update(Enumerable.Range(0, 1024).ToList(), state, links,
                Transitions(), new Dictionary<int, int>());
            throw new Exception("worker failure was swallowed");
        }
        catch (InvalidOperationException error) { Check(error.Message == "link failure", "worker exception propagated"); }
        state.FailCell = -1;
        Updater.Update(Enumerable.Range(0, 1024).ToList(), state, links,
            Transitions(), new Dictionary<int, int>());
        Check(links[7].Value == Updater.Expected(7, state), "retry after failure");
        foreach (Type type in new[] { typeof(JobManager), typeof(GlobalJobManager) })
        {
            var patches = Harmony.GetPatchInfo(AccessTools.Method(type, "Run"));
            Check(patches.Prefixes.Count(p => p.PatchMethod.DeclaringType == typeof(Optimization)) == 1,
                "one job-depth prefix per runner");
            Check(patches.Finalizers.Count(p => p.PatchMethod.DeclaringType == typeof(Optimization)) == 1,
                "one job-depth finalizer per runner");
        }
        if (benchmark) Benchmark(harmony, method);
        harmony.UnpatchAll(Owner);
        Console.WriteLine("PASS navigation links " + assertions + " assertions");
    }

    private static Graph.Transition[][] Transitions()
    {
        var transitions = new Graph.Transition[11][];
        transitions[0] = new Graph.Transition[64];
        return transitions;
    }

    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Benchmark(Harmony harmony, MethodInfo method)
    {
        var cells = Enumerable.Range(0, 4096).ToList();
        var table = new Table(4096);
        var links = new Graph.Link[4096];
        var transitions = Transitions();
        var teleports = new Dictionary<int, int>();
        for (int round = 0; round < 4; round++)
        {
            bool optimized = round == 1 || round == 2;
            harmony.Unpatch(method, HarmonyPatchType.Prefix, Owner);
            if (optimized) Check(Optimization.Install(harmony), Optimization.LastFailure);
            var samples = new List<double>();
            for (int i = 0; i < 40; i++)
            {
                long start = Stopwatch.GetTimestamp();
                Updater.Update(cells, table, links, transitions, teleports);
                if (i >= 10) samples.Add((Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
            }
            Console.WriteLine($"NAV_LINK_SAMPLE optimized={optimized} mean_ms={samples.Average():F6} cells={cells.Count} threads={GlobalJobManager.ThreadCount}");
        }
    }

    internal sealed class Table
    {
        internal readonly int[] Values;
        private short[] ValidCells;
        internal int FailCell = -1;
        internal Table(int cells)
        {
            Values = Enumerable.Range(0, cells).Select(i => i * 17).ToArray();
            ValidCells = Enumerable.Repeat((short)1, cells).ToArray();
        }
        internal void ClearValid() { Array.Clear(ValidCells); }
        internal bool HasLinks(int cell) => ValidCells[cell] != 0;
    }

    private struct Nested : IWorkItem<object>
    {
        public void Run(object ignored, int threadIndex)
        {
            Updater.Update(Enumerable.Range(0, 1024).ToList(), new Table(4096), new Graph.Link[4096],
                Transitions(), new Dictionary<int, int>());
        }
    }

    internal static class Updater
    {
        internal static void Update(List<int> cells, Table table, Graph.Link[] links,
            Graph.Transition[][] transitions, Dictionary<int, int> teleports)
        {
            UpdateLinks(cells, table, 1, links, transitions, teleports);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void UpdateLinks(List<int> cells, Table table, int maxLinks,
            Graph.Link[] links, Graph.Transition[][] transitions, Dictionary<int, int> teleports)
        {
            foreach (int cell in cells) CreateLinks(cell, table, maxLinks, links, transitions, teleports);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void CreateLinks(int cell, Table table, int maxLinks, Graph.Link[] links,
            Graph.Transition[][] transitions, Dictionary<int, int> teleports)
        {
            if (cell == table.FailCell) throw new InvalidOperationException("link failure");
            links[cell].Value = table.HasLinks(cell) ? Expected(cell, table) : 0;
        }

        internal static int Expected(int cell, Table table)
        {
            int result = 0;
            // Controlled read-only neighbor workload with disjoint per-cell output.
            for (int transition = 0; transition < 64; transition++)
                for (int offset = 0; offset < 8; offset++)
                    result = unchecked((result * 31) ^ table.Values[(cell + transition + offset) % table.Values.Length]);
            return result;
        }
    }
}

// Host adapters model the game's existing synchronous job collection contract.
public interface IWorkItem<T> { void Run(T sharedData, int threadIndex); }
public interface IWorkItemCollection { int Count { get; } void InternalDoWorkItem(int index, int threadIndex); }
public sealed class WorkItemCollection<T, TShared> : IWorkItemCollection where T : struct, IWorkItem<TShared>
{
    private readonly List<T> items = new List<T>();
    private TShared shared;
    public int Count => items.Count;
    public void Reset(TShared value) { items.Clear(); shared = value; }
    public void Add(T item) { items.Add(item); }
    public void InternalDoWorkItem(int index, int threadIndex) { items[index].Run(shared, threadIndex); }
}
public static class GlobalJobManager
{
    public static int ThreadCount => Math.Min(8, Environment.ProcessorCount);
    public static int Runs;
    private static readonly JobManager Manager = new JobManager();
    public static void Run(IWorkItemCollection work) { Interlocked.Increment(ref Runs); Manager.Run(work); }
}
public sealed class JobManager
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Run(IWorkItemCollection work)
    {
        Parallel.For(0, work.Count, new ParallelOptions { MaxDegreeOfParallelism = GlobalJobManager.ThreadCount },
            index => work.InternalDoWorkItem(index, index % GlobalJobManager.ThreadCount));
    }
}
