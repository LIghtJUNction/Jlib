#nullable disable
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using HarmonyLib;
#if JLIB_HOST_TESTS
using NavGrid = NavigationGraphExpansionCoverage.Graph;
using NavTable = NavigationLinkParallelismCoverage.Table;
using NavGridUpdater = NavigationLinkParallelismCoverage.Updater;
using NavigationCells = NavigationGraphExpansionCoverage.Cells;
#else
using NavigationCells = global::Grid;
#endif

namespace Jlib.Runtime
{
    /// <summary>Partitions independent dirty-cell link writes using the game's job runner.</summary>
    public static class NavigationLinkParallelism
    {
        private delegate void CreateLinks(int cell, NavTable table, int maxLinks, NavGrid.Link[] links,
            NavGrid.Transition[][] transitions, Dictionary<int, int> teleports);
        private const int MinimumParallelCells = 512;
        private const int BulkDirtyCellCount = 32768;
        private const int MinimumTransitionRequests = 4096;
        private static readonly object InstallLock = new object();
        private static CreateLinks create;
        private static AccessTools.FieldRef<NavTable, short[]> validCells;
        private static int mainThread;
        [ThreadStatic] private static int jobDepth;
        [ThreadStatic] private static Work reusable;
        public static string LastFailure { get; private set; }

        private sealed class Work
        {
            internal readonly WorkItemCollection<Chunk, Work> Jobs = new WorkItemCollection<Chunk, Work>();
            internal readonly HashSet<int> Unique = new HashSet<int>();
            internal List<int> Cells;
            internal NavTable Table;
            internal int MaxLinks;
            internal NavGrid.Link[] Links;
            internal NavGrid.Transition[][] Transitions;
            internal Dictionary<int, int> Teleports;
            internal Exception Error;
            internal bool Busy;

            internal void Clear()
            {
                Jobs.Reset(null);
                Unique.Clear();
                Cells = null;
                Table = null;
                Links = null;
                Transitions = null;
                Teleports = null;
                Error = null;
                Busy = false;
            }
        }

        private struct Chunk : IWorkItem<Work>
        {
            internal int Start, End;
            public void Run(Work work, int threadIndex)
            {
                try
                {
                    for (int i = Start; i < End; i++)
                        create(work.Cells[i], work.Table, work.MaxLinks, work.Links, work.Transitions, work.Teleports);
                }
                catch (Exception error) { Interlocked.CompareExchange(ref work.Error, error, null); }
            }
        }

        public static bool Install(Harmony harmony)
        {
            if (harmony == null) throw new ArgumentNullException(nameof(harmony));
            lock (InstallLock)
            {
                var plans = new List<(MethodInfo Target, MethodInfo Hook)>();
                try
                {
                    MethodInfo links = AccessTools.Method(typeof(NavGridUpdater), "UpdateLinks")
                        ?? throw new MissingMethodException("NavGridUpdater.UpdateLinks");
                    MethodInfo job = AccessTools.Method(typeof(JobManager), "Run", new[] { typeof(IWorkItemCollection) })
                        ?? throw new MissingMethodException("JobManager.Run");
                    MethodInfo globalJob = AccessTools.Method(typeof(GlobalJobManager), "Run", new[] { typeof(IWorkItemCollection) })
                        ?? throw new MissingMethodException("GlobalJobManager.Run");
                    MethodInfo hook = AccessTools.Method(typeof(NavigationLinkParallelism), nameof(BeforeLinks));
                    MethodInfo enter = AccessTools.Method(typeof(NavigationLinkParallelism), nameof(EnterJobs));
                    MethodInfo exit = AccessTools.Method(typeof(NavigationLinkParallelism), nameof(ExitJobs));
                    var jobPatches = Harmony.GetPatchInfo(job);
                    bool hasEnter = jobPatches?.Prefixes.Any(p => p.PatchMethod == enter) == true;
                    bool hasExit = jobPatches?.Finalizers.Any(p => p.PatchMethod == exit) == true;
                    var globalPatches = Harmony.GetPatchInfo(globalJob);
                    bool hasGlobalEnter = globalPatches?.Prefixes.Any(p => p.PatchMethod == enter) == true;
                    bool hasGlobalExit = globalPatches?.Finalizers.Any(p => p.PatchMethod == exit) == true;
                    bool hasLinks = Harmony.GetPatchInfo(links)?.Prefixes.Any(p => p.PatchMethod == hook) == true;
                    if (hasEnter && hasExit && hasGlobalEnter && hasGlobalExit && hasLinks) return true;
                    MethodInfo target = AccessTools.Method(typeof(NavGridUpdater), "CreateLinks", new[] {
                        typeof(int), typeof(NavTable), typeof(int), typeof(NavGrid.Link[]),
                        typeof(NavGrid.Transition[][]), typeof(Dictionary<int, int>) })
                        ?? throw new MissingMethodException("NavGridUpdater.CreateLinks(cell,...)");
                    validCells = AccessTools.FieldRefAccess<NavTable, short[]>("ValidCells");
                    create = (CreateLinks)Delegate.CreateDelegate(typeof(CreateLinks), target);
                    mainThread = Thread.CurrentThread.ManagedThreadId;
                    if (!hasEnter) plans.Add((job, enter));
                    if (!hasExit) plans.Add((job, exit));
                    if (!hasGlobalEnter) plans.Add((globalJob, enter));
                    if (!hasGlobalExit) plans.Add((globalJob, exit));
                    if (!hasLinks) plans.Add((links, hook));
                    if (!hasEnter || !hasExit)
                        harmony.Patch(job, prefix: hasEnter ? null : new HarmonyMethod(enter),
                            finalizer: hasExit ? null : new HarmonyMethod(exit));
                    if (!hasGlobalEnter || !hasGlobalExit)
                        harmony.Patch(globalJob, prefix: hasGlobalEnter ? null : new HarmonyMethod(enter),
                            finalizer: hasGlobalExit ? null : new HarmonyMethod(exit));
                    if (!hasLinks) harmony.Patch(links, prefix: new HarmonyMethod(hook));
                    LastFailure = null;
                    return true;
                }
                catch (Exception error)
                {
                    foreach (var plan in plans) harmony.Unpatch(plan.Target, plan.Hook);
                    LastFailure = error.ToString();
                    return false;
                }
            }
        }

        [HarmonyPriority(Priority.First)]
        private static void EnterJobs(out bool __state)
        {
            jobDepth++;
            __state = true;
        }

        [HarmonyPriority(Priority.Last)]
        private static Exception ExitJobs(Exception __exception, bool __state)
        {
            if (__state) jobDepth--;
            return __exception;
        }

        private static bool HasParallelWork(List<int> cells, NavTable table, NavGrid.Transition[][] transitions)
        {
            if (table == null || transitions == null || transitions.Length < 11) return false;
            short[] masks = validCells(table);
            int requests = 0;
            const int samples = 32;
            for (int sample = 0; sample < samples; sample++)
            {
                int cell = cells[(int)((long)sample * (cells.Count - 1) / (samples - 1))];
                if (cell < 0 || cell >= masks.Length) return false;
                int mask = masks[cell];
                for (int type = 0; type < 11; type++)
                    if ((mask & (1 << type)) != 0) requests += transitions[type]?.Length ?? 0;
            }
            // Empty navigation tables and very cheap updates avoid a job barrier.
            return (long)requests * cells.Count >= (long)samples * MinimumTransitionRequests;
        }

        private static bool BeforeLinks(List<int> __0, NavTable __1, int __2, NavGrid.Link[] __3,
            NavGrid.Transition[][] __4, Dictionary<int, int> __5)
        {
            if (__0 == null || __0.Count < MinimumParallelCells || __2 <= 0 || GlobalJobManager.ThreadCount < 2
                || Thread.CurrentThread.ManagedThreadId != mainThread || jobDepth != 0) return true;
            if (__0.Count < BulkDirtyCellCount && !HasParallelWork(__0, __1, __4)) return true;
            var work = reusable ?? (reusable = new Work());
            if (work.Busy) return true;
            work.Unique.Clear();
            foreach (int cell in __0)
            {
                if (!NavigationCells.IsValidCell(cell) || !work.Unique.Add(cell))
                {
                    work.Unique.Clear();
                    return true;
                }
            }
            work.Busy = true;
            try
            {
                work.Cells = __0;
                work.Table = __1;
                work.MaxLinks = __2;
                work.Links = __3;
                work.Transitions = __4;
                work.Teleports = __5;
                work.Jobs.Reset(work);
                int size = Math.Max(64, (__0.Count + GlobalJobManager.ThreadCount * 4 - 1)
                    / (GlobalJobManager.ThreadCount * 4));
                for (int start = 0; start < __0.Count; start += size)
                    work.Jobs.Add(new Chunk { Start = start, End = Math.Min(start + size, __0.Count) });
                GlobalJobManager.Run(work.Jobs);
                if (work.Error != null) ExceptionDispatchInfo.Capture(work.Error).Throw();
                return false;
            }
            finally { work.Clear(); }
        }
    }
}
