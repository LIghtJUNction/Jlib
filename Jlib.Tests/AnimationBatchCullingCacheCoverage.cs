#nullable disable
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Cache = Jlib.Runtime.AnimationBatchCullingCache;

// Integration: link ../Jlib/Runtime/AnimationBatchCullingCache.cs into the existing
// JLIB_HOST_TESTS project and invoke AnimationBatchCullingCacheCoverage.Run().
// All targets below are host fixtures. No game or Unity assemblies are executed.
internal static class AnimationBatchCullingCacheCoverage
{
    private static readonly Chunk Min = new Chunk(0, 0), Max = new Chunk(4, 4);

    internal static void Run()
    {
        var harmony = new Harmony("Jlib.Tests.AnimationBatchCullingCache");
        Check(Cache.Install(harmony), Cache.LastFailure);
        Check(Cache.Install(new Harmony(harmony.Id + ".consumer")), "installation is shared");
        try
        {
            Manager.CreateInstance();
            var manager = Manager.Instance;
            manager.CompleteInit();
            var world = new Set(1, new Chunk(2, 2), false);
            var ui = new Set(2, new Chunk(1000, 1000), true);
            var controller = new Controller(world);
            manager.Register(controller);
            manager.Register(new Controller(ui));
            Cull(manager);
            Check(manager.Calls == 1 && manager.ActiveCount == 2, "initial world and distant UI");
            Cull(manager);
            Check(manager.Calls == 1, "stable view skips original");
            manager.UpdateActiveArea(Min, new Chunk(1, 4));
            Check(manager.Calls == 2 && manager.ActiveCount == 1, "max X changes visibility");
            manager.UpdateActiveArea(new Chunk(0, 3), Max);
            Check(manager.Calls == 3 && manager.ActiveCount == 1, "min Y changes visibility");
            manager.UpdateActiveArea(new Chunk(3, 0), Max);
            Check(manager.Calls == 4, "min X participates in key");
            manager.UpdateActiveArea(Min, new Chunk(4, 1));
            Check(manager.Calls == 5, "max Y participates in key");
            Cull(manager);

            Changes(manager, () => manager.Register(new Controller(world)), "register into existing set");
            Changes(manager, () => world.Add(new Controller(world)), "direct BatchSet.Add");
            Changes(manager, () => controller.Batch.Deregister(controller), "actual deregister path");
            Changes(manager, () => manager.Register(controller), "re-register");
            Changes(manager, () =>
            {
                Batch old = world.GetBatch(0);
                world.RemoveBatch(old);
                world.AddBatch(new Batch());
            }, "same-count batch replacement");
            Batch moving = world.GetBatch(0);
            Changes(manager, () => moving.SetBatchSet(ui), "batch migration changes owner/XY/active");
            Changes(manager, () => moving.OverrideZ(17), "Z position");
            Changes(manager, () => moving.SetLayer(9), "layer");
            Changes(manager, () => ui.SetActive(false), "set active transition");
            Changes(manager, () => ui.SetActive(true), "set activation");
            Changes(manager, () => ui.GetBatch(0).Deactivate(), "batch direct deactivation");
            Changes(manager, () => ui.GetBatch(0).Activate(), "batch direct activation");
            int before = manager.Calls;
            ui.SetActive(true);
            ui.GetBatch(0).Activate();
            ui.SetDirty();
            manager.UpdateDirty(73);
            manager.Render();
            Cull(manager);
            Check(manager.Calls == before, "no-op activation and animation dirtiness retain cache");
            Check(manager.DirtyFrame == 73 && manager.Renders == 1, "dirty and rendering still execute");
            var direct = new Controller(ui);
            Changes(manager, () => ui.GetBatch(0).Register(direct), "direct batch register");
            Changes(manager, () =>
            {
                Batch batch = ui.GetBatch(0);
                batch.Deregister(direct);
                batch.Register(new Controller(ui));
            }, "same-count controller replacement");
            Changes(manager, () => ui.GetBatch(0).Clear(), "batch clear");
            Changes(manager, () => ui.Clear(), "set clear");

            var migrating = new Controller(new Set(3, new Chunk(2, 2), false));
            Changes(manager, () => manager.Register(migrating), "new world set");
            Changes(manager, () =>
            {
                manager.Deregister(migrating);
                migrating.Target = new Set(4, new Chunk(500, 500), true);
                manager.Register(migrating);
            }, "world to UI registration");
            Changes(manager, () =>
            {
                manager.Deregister(migrating);
                migrating.Target = new Set(5, new Chunk(2, 2), false);
                manager.Register(migrating);
            }, "UI to world registration");
            Changes(manager, () => Cache.Invalidate(manager), "explicit invalidation");
            Changes(manager, () => manager.Clear(), "manager clear if exposed");
            Changes(manager, () => manager.Reset(), "manager reset if exposed");
            Changes(manager, () => manager.CompleteInit(), "reinitialization");
            Changes(manager, () => Manager.DestroyInstance(), "destroy resets singleton lifetime");

            var other = new Manager();
            Cull(other);
            int firstCalls = manager.Calls;
            Cull(manager);
            Check(other.Calls == 1 && manager.Calls == firstCalls + 1, "manager identity is part of key");
            SameCountReplacement();
            FailuresAndReentrancy(manager);
            ConstantWorkAndAllocation();

            // A partially removed invalidation surface must be repaired before skipping.
            harmony.Unpatch(AccessTools.DeclaredMethod(typeof(Set), nameof(Set.RemoveBatch)),
                HarmonyPatchType.All, harmony.Id);
            Check(Cache.Install(harmony), "partial installation repaired");
            Changes(manager, () => manager.Register(new Controller(new Set(42, Min, false))),
                "mutation invalidation after reinstall");
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            Cache.Invalidate();
        }
    }

    private static void SameCountReplacement()
    {
        var manager = new Manager();
        manager.Register(new Controller(new Set(1, Min, false)));
        Cull(manager);
        Changes(manager, () =>
        {
            manager.Clear();
            manager.Register(new Controller(new Set(1, new Chunk(900, 900), true)));
        }, "one-for-one world to UI replacement at unchanged view");
        Check(manager.ActiveCount == 1, "distant replacement UI is active");
        Changes(manager, () =>
        {
            manager.Clear();
            manager.Register(new Controller(new Set(1, new Chunk(900, 900), false)));
        }, "one-for-one UI to world replacement at unchanged view");
        Check(manager.ActiveCount == 0, "distant replacement world is culled");
    }

    private static void FailuresAndReentrancy(Manager manager)
    {
        Cache.Invalidate();
        int before = manager.Calls;
        manager.Fail = true;
        try { Cull(manager); throw new Exception("missing cull exception"); }
        catch (InvalidOperationException) { }
        manager.Fail = false;
        Cull(manager);
        Cull(manager);
        Check(manager.Calls == before + 2, "failed cull retries then stabilizes");

        var failing = new Controller(new Set(10, Min, false)) { Fail = true };
        try { manager.Register(failing); throw new Exception("missing mutation exception"); }
        catch (InvalidOperationException) { }
        before = manager.Calls;
        Cull(manager);
        Check(manager.Calls == before + 1, "partial mutation failure invalidates");
        Cull(manager);
        Check(manager.Calls == before + 1, "mutation depth recovers after failure");

        var nested = new Controller(new Set(11, Min, false)) { Registered = () => Cull(manager) };
        before = manager.Calls;
        manager.Register(nested);
        Cull(manager);
        Cull(manager);
        Check(manager.Calls == before + 2, "cull inside mutation cannot seed stable cache");

        Cache.Invalidate();
        manager.DuringCull = () => Cull(manager);
        before = manager.Calls;
        Cull(manager);
        Cull(manager);
        Cull(manager);
        Check(manager.Calls == before + 3, "recursive culls need a complete subsequent pass");

        Cache.Invalidate();
        manager.DuringCull = () => manager.Register(new Controller(new Set(12, Min, false)));
        before = manager.Calls;
        Cull(manager);
        Cull(manager);
        Cull(manager);
        Check(manager.Calls == before + 2, "structural mutation during cull requires another pass");
    }

    private static void ConstantWorkAndAllocation()
    {
        var large = new Manager();
        for (int i = 0; i < 1000; i++)
            large.Register(new Controller(new Set(i, Min, false)));
        Cull(large);
        int visits = large.Visits;
        for (int i = 0; i < 10000; i++) Cull(large);
        long start = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++) Cull(large);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - start;
        Check(large.Calls == 1 && large.Visits == visits, "stable key never traverses 1000 sets");
        Check(allocated == 0, "stable typed key has no per-call allocation");
    }

    private static void Changes(Manager manager, Action mutation, string label)
    {
        int before = manager.Calls;
        mutation();
        Cull(manager);
        Check(manager.Calls == before + 1, label);
        Cull(manager);
        Check(manager.Calls == before + 1, label + " then stable");
    }
    private static void Cull(Manager manager) { manager.UpdateActiveArea(Min, Max); }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("Animation culling cache " + message);
    }

    internal struct Chunk
    {
        public int x, y;
        internal Chunk(int x, int y) { this.x = x; this.y = y; }
    }
    internal static class Converter
    {
        internal interface IAnimConverter
        {
            Set Target { get; }
            Batch Batch { get; set; }
            void OnRegistered();
        }
    }
    private sealed class Controller : Converter.IAnimConverter
    {
        public Set Target { get; set; }
        public Batch Batch { get; set; }
        internal bool Fail;
        internal Action Registered;
        internal Controller(Set target) { Target = target; }
        public void OnRegistered()
        {
            Registered?.Invoke();
            if (Fail) throw new InvalidOperationException("fixture mutation failure");
        }
    }

    internal sealed class Batch
    {
        private readonly List<Converter.IAnimConverter> controllers = new List<Converter.IAnimConverter>();
        public (int x, int y, float z) position { get; private set; }
        public bool active { get; private set; } = true;
        public Set batchset { get; private set; }
        public int layer { get; private set; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Clear() { controllers.Clear(); batchset = null; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void SetBatchSet(Set set)
        {
            if (batchset != null && batchset != set) batchset.RemoveBatch(this);
            batchset = set;
            if (set != null)
            {
                position = (set.idx.x * 32, set.idx.y * 32, position.z);
                active = set.active;
            }
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool Register(Converter.IAnimConverter controller)
        {
            if (!controllers.Contains(controller)) controllers.Add(controller);
            controller.Batch?.Deregister(controller);
            controller.Batch = this;
            controller.OnRegistered();
            return true;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Deregister(Converter.IAnimConverter controller)
        {
            controllers.Remove(controller);
            controller.Batch = null;
            if (controllers.Count == 0) batchset?.RemoveBatch(this);
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Activate() { active = true; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Deactivate() { active = false; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void OverrideZ(float z) { position = (position.x, position.y, z); }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void SetLayer(int value) { layer = value; }
    }

    internal sealed class Set
    {
        private readonly List<Batch> batches = new List<Batch>();
        internal readonly int Id;
        internal readonly bool UI;
        public Chunk idx { get; private set; }
        public bool active { get; private set; }
        internal Set(int id, Chunk idx, bool ui) { Id = id; this.idx = idx; UI = ui; }
        public Batch GetBatch(int index) => batches[index];
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Clear() { foreach (var batch in batches) batch.Clear(); batches.Clear(); }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Add(Converter.IAnimConverter controller)
        {
            if (batches.Count == 0) AddBatch(new Batch());
            batches[0].Register(controller);
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void AddBatch(Batch batch)
        {
            if (batch.batchset == this) return;
            batch.batchset?.RemoveBatch(batch);
            batch.SetBatchSet(this);
            if (!batches.Contains(batch)) batches.Add(batch);
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void RemoveBatch(Batch batch)
        {
            if (batches.Remove(batch)) batch.SetBatchSet(null);
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void SetActive(bool value)
        {
            if (value != active)
                foreach (var batch in batches)
                    if (value) batch.Activate(); else batch.Deactivate();
            active = value;
        }
        internal void SetDirty() { }
    }

    private struct Info
    {
        internal Set Set;
        internal Chunk Position;
        internal bool Active;
    }
    internal sealed class Manager
    {
        private bool ready;
        private readonly Dictionary<int, Set> sets = new Dictionary<int, Set>();
        private readonly List<Info> uiSets = new List<Info>(), culledSets = new List<Info>();
        private readonly List<Set> activeSets = new List<Set>();
        internal int Calls, Visits, DirtyFrame, Renders;
        internal bool Fail;
        internal Action DuringCull;
        internal int ActiveCount => activeSets.Count;
        internal static Manager Instance { get; private set; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void CreateInstance() { Instance = new Manager(); }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void DestroyInstance()
        {
            if (Instance != null)
            {
                Instance.ready = false;
                foreach (var set in Instance.sets.Values) set.Clear();
                Instance.Clear();
            }
            Instance = null;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void CompleteInit() { ready = true; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Register(Converter.IAnimConverter controller)
        {
            Set set = controller.Target;
            if (!sets.ContainsKey(set.Id))
            {
                sets.Add(set.Id, set);
                (set.UI ? uiSets : culledSets).Add(new Info { Set = set, Position = set.idx });
            }
            set.Add(controller);
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Deregister(Converter.IAnimConverter controller) { controller.Batch?.Deregister(controller); }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Clear() { sets.Clear(); uiSets.Clear(); culledSets.Clear(); activeSets.Clear(); }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Reset() { Clear(); ready = false; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void UpdateActiveArea(Chunk min, Chunk max)
        {
            Calls++;
            if (Fail) throw new InvalidOperationException("fixture cull failure");
            Action callback = DuringCull;
            DuringCull = null;
            callback?.Invoke();
            activeSets.Clear();
            UpdateInfos(uiSets, min, max, true);
            UpdateInfos(culledSets, min, max, false);
        }
        private void UpdateInfos(List<Info> list, Chunk min, Chunk max, bool ui)
        {
            for (int i = 0; i < list.Count; i++)
            {
                Visits++;
                Info info = list[i];
                bool visible = ui || (info.Position.x >= min.x && info.Position.x <= max.x &&
                    info.Position.y >= min.y && info.Position.y <= max.y);
                if (visible) activeSets.Add(info.Set);
                if (visible != info.Active)
                {
                    info.Active = visible;
                    info.Set.SetActive(visible);
                    list[i] = info;
                }
            }
        }
        internal void UpdateDirty(int frame) { if (ready) DirtyFrame = frame; }
        internal void Render() { if (ready) Renders++; }
    }
}
