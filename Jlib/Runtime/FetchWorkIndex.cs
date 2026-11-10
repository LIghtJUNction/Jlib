#nullable disable
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace Jlib.Runtime {
    /// <summary>Tracks pickup membership and offset invalidation at their mutation sites.</summary>
    public static class FetchWorkIndex {
        private sealed class Group {
            internal readonly PartitionedWorkIndex<FetchManager.Fetchable> Index = new();
            internal Manager Owner;
        }
        private sealed class Item { internal Group Group; internal int Scope; }
        private sealed class Manager {
            internal readonly HashSet<Pickupable> Dirty = new();
            internal readonly List<Pickupable> Drain = new();
            internal readonly List<FetchManager.FetchablesByPrefabId> Groups = new();
            internal readonly WorkItemCollection<PickupJob, object> Jobs = new();
            internal bool Initialized;
            internal bool Supported = true;
            internal readonly Dictionary<int, int> Parents = new();
        }
        private struct PickupJob : IWorkItem<object> {
            internal FetchManager.FetchablesByPrefabId Group;
            internal Navigator Navigator;
            internal int Worker;
            public void Run(object context, int threadIndex) { Group.UpdatePickups(Navigator, Worker); }
        }
        private static readonly ConditionalWeakTable<FetchManager, Manager> Managers = new();
        private static readonly ConditionalWeakTable<FetchManager.FetchablesByPrefabId, Group> Groups = new();
        private static readonly ConditionalWeakTable<Pickupable, Item> Items = new();
        private static readonly Func<FetchManager.Fetchable, int> ScopeOf = value => Scope(value.pickupable.cachedCell);
        private static bool installed;

        public static IndexedCandidates<FetchManager.Fetchable> GetCandidates(
                FetchManager.FetchablesByPrefabId group, Navigator navigator) =>
            new(group.fetchables.GetDataList(), GetIndices(group, navigator));

        // Parent worlds match the chore provider's rocket-interior semantics.
        private static int Scope(int cell) {
            if (!Grid.IsValidCell(cell)) return -1;
            var world = ClusterManager.Instance?.GetWorld(Grid.WorldIdx[cell]);
            return world == null ? -1 : world.ParentWorldId;
        }

        public static IReadOnlyList<int> GetIndices(FetchManager.FetchablesByPrefabId group, Navigator navigator) {
            if (!installed || !Groups.TryGetValue(group, out var state) || !state.Owner.Supported) return null;
            int scope = Scope(navigator.cachedCell);
            if (scope < 0) return null;
            var source = group.fetchables.GetDataList();
            // Invalid cells cannot be excluded on an unrecognized/custom world.
            if (state.Index.Get(source, -1, ScopeOf).Count != 0) return null;
            return state.Index.Get(source, scope, ScopeOf);
        }

        internal static void Install(Harmony harmony) {
            if (installed) return;
            Patch(harmony, typeof(FetchManager), "Add", nameof(Added), false);
            Patch(harmony, typeof(FetchManager), "Remove", nameof(Removing), true);
            Patch(harmony, typeof(Pickupable), "UpdateCachedCell", nameof(Moved), false);
            Patch(harmony, typeof(Workable), "SetOffsets", nameof(OffsetsChanged), false);
            Patch(harmony, typeof(Workable), "SetOffsetTable", nameof(OffsetsChanged), false);
            Patch(harmony, typeof(OffsetTableTracker), "OnCellChanged", nameof(TableChanged), false);
            harmony.Patch(AccessTools.Method(typeof(FetchManager), "UpdatePickups", new[] { typeof(Navigator), typeof(WorkerBase) }),
                prefix: new HarmonyMethod(typeof(FetchWorkIndex), nameof(UpdatePickups)));
            installed = true;
        }

        private static void Patch(Harmony harmony, Type type, string method, string hook, bool prefix) {
            var callback = new HarmonyMethod(typeof(FetchWorkIndex), hook);
            harmony.Patch(AccessTools.Method(type, method), prefix: prefix ? callback : null, postfix: prefix ? null : callback);
        }

        private static void Register(Manager manager, FetchManager.FetchablesByPrefabId group, Pickupable pickup) {
            var state = Groups.GetValue(group, _ => new Group());
            state.Owner = manager;
            state.Index.Invalidate();
            var item = Items.GetValue(pickup, _ => new Item());
            item.Group = state; item.Scope = Scope(pickup.cachedCell);
            manager.Supported &= pickup.GetType() == typeof(Pickupable);
            manager.Dirty.Add(pickup);
        }

        private static void Added(FetchManager __instance, Pickupable __0) {
            var manager = Managers.GetValue(__instance, _ => new Manager());
            Register(manager, __instance.prefabIdToFetchables[__0.KPrefabID.PrefabID()], __0);
        }

        private static void Removing(FetchManager __instance, Tag __0, HandleVector<int>.Handle __1) {
            if (!__instance.prefabIdToFetchables.TryGetValue(__0, out var group)) return;
            var pickup = group.fetchables.GetData(__1).pickupable;
            if (Items.TryGetValue(pickup, out var item)) {
                item.Group.Index.Invalidate(); item.Group.Owner.Dirty.Remove(pickup);
                Items.Remove(pickup);
            }
        }

        private static void Moved(Pickupable __instance) {
            if (Items.TryGetValue(__instance, out var item)) {
                int scope = Scope(__instance.cachedCell);
                if (scope != item.Scope) {
                    item.Scope = scope;
                    item.Group.Index.Invalidate();
                }
            }
        }

        private static void OffsetsChanged(Workable __instance) {
            if (__instance is Pickupable pickup && Items.TryGetValue(pickup, out var item))
                item.Group.Owner.Dirty.Add(pickup);
        }

        private static void TableChanged(KMonoBehaviour ___cmp) {
            if (___cmp is Pickupable pickup) OffsetsChanged(pickup);
        }

        private static bool UpdatePickups(FetchManager __instance, Navigator navigator, WorkerBase worker,
                List<FetchManager.Pickup> ___pickups) {
            var manager = Managers.GetValue(__instance, _ => new Manager());
            if (!manager.Initialized) {
                foreach (var group in __instance.prefabIdToFetchables.Values)
                    foreach (var entry in group.fetchables.GetDataList()) Register(manager, group, entry.pickupable);
                manager.Initialized = true;
            }
            if (!manager.Supported) return true;
            bool hierarchyChanged = false;
            foreach (int worldId in ClusterManager.Instance.GetWorldIDsSorted()) {
                int parent = ClusterManager.Instance.GetWorld(worldId).ParentWorldId;
                if (!manager.Parents.TryGetValue(worldId, out int previousParent) || previousParent != parent) {
                    manager.Parents[worldId] = parent;
                    hierarchyChanged = true;
                }
            }
            if (hierarchyChanged)
                foreach (var group in __instance.prefabIdToFetchables.Values)
                    if (Groups.TryGetValue(group, out var state)) state.Index.Invalidate();
            // Refresh only offsets explicitly invalidated by game events. Work
            // callbacks may enqueue more entries; drain to a separate reused list.
            manager.Drain.Clear(); manager.Drain.AddRange(manager.Dirty); manager.Dirty.Clear();
            foreach (var pickup in manager.Drain)
                if (pickup != null) pickup.GetOffsets(pickup.cachedCell);
            manager.Drain.Clear();
            manager.Jobs.Reset(null); manager.Groups.Clear();
            int workerId = worker.GetComponent<KPrefabID>().InstanceID;
            foreach (var group in __instance.prefabIdToFetchables.Values) {
                var indices = GetIndices(group, navigator);
                if (indices != null && indices.Count == 0) continue;
                manager.Groups.Add(group);
                manager.Jobs.Add(new PickupJob { Group = group, Navigator = navigator, Worker = workerId });
            }
            bool previous = OffsetTracker.isExecutingWithinJob;
            try {
                OffsetTracker.isExecutingWithinJob = true;
                GlobalJobManager.Run(manager.Jobs);
            } finally { OffsetTracker.isExecutingWithinJob = previous; }
            ___pickups.Clear();
            foreach (var group in manager.Groups) ___pickups.AddRange(group.finalPickups);
            ___pickups.Sort(Compare);
            return false;
        }

        private static int Compare(FetchManager.Pickup a, FetchManager.Pickup b) {
            int result = a.PathCost.CompareTo(b.PathCost);
            if (result != 0) return result;
            result = b.foodQuality.CompareTo(a.foodQuality);
            return result != 0 ? result : b.freshness.CompareTo(a.freshness);
        }
    }
}
