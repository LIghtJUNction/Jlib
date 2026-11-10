#nullable disable
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace Jlib.Runtime {
    /// <summary>Shared, opt-in work-list optimizations preserving game eligibility checks.</summary>
    public static class CpuWorkOptimizations {
        private static bool installed;
        public static bool FetchInstalled { get; private set; }
        public static bool NavigationInstalled { get; private set; }
        private static readonly ConditionalWeakTable<FetchManager, FetchIndex> FetchIndices = new();
        private static readonly Predicate<Navigator> ValidOrigin = nav => Grid.IsValidCell(nav.cachedCell);

        private sealed class FetchIndex {
            internal readonly OrderedCandidateIndex<FetchManager.Pickup, Tag> Tags =
                new((pickup, tag) => pickup.pickupable.KPrefabID != null && pickup.pickupable.KPrefabID.HasTag(tag));
        }

        public static void Install(Harmony harmony) {
            if (installed) return;
            if (harmony == null) throw new ArgumentNullException(nameof(harmony));
            // FastTrack owns these pipelines. Let its implementation remain authoritative.
            if (AccessTools.TypeByName("PeterHan.FastTrack.GamePatches.FetchManagerFastUpdate") != null) return;
            harmony.Patch(AccessTools.Method(typeof(AsyncPathProber.Manager), "TickFrame"),
                transpiler: new HarmonyMethod(typeof(CpuWorkOptimizations), nameof(SelectPathRequests)));
            NavigationInstalled = true;
            var update = AccessTools.Method(typeof(FetchManager), "UpdatePickups", new[] { typeof(Navigator), typeof(WorkerBase) });
            var find = AccessTools.Method(typeof(FetchManager), "FindFetchTarget", new[] { typeof(Storage), typeof(FetchChore) });
            if (AccessTools.TypeByName("PeterHan.EfficientFetch.EfficientFetchManager") == null
                    && AccessTools.TypeByName("QualityOfLifeONI.EfficientFetchManager") == null
                    && CanReplace(update, harmony.Id) && CanReplace(find, harmony.Id)) {
                FetchWorkIndex.Install(harmony);
                harmony.Patch(update, postfix: new HarmonyMethod(typeof(CpuWorkOptimizations), nameof(RefreshFetchIndex)));
                harmony.Patch(AccessTools.Method(typeof(FetchManager), "UpdateTags"),
                    postfix: new HarmonyMethod(typeof(CpuWorkOptimizations), nameof(RefreshFetchIndex)));
                harmony.Patch(find, prefix: new HarmonyMethod(typeof(CpuWorkOptimizations), nameof(FindFetchTarget)));
                FetchInstalled = true;
            }
            installed = true;
        }

        private static bool CanReplace(MethodBase method, string owner) {
            if (method == null) throw new MissingMethodException("ONI fetch method is missing.");
            var patches = Harmony.GetPatchInfo(method);
            if (patches == null) return true;
            foreach (var patch in patches.Prefixes) if (patch.owner != owner) return false;
            foreach (var patch in patches.Transpilers) if (patch.owner != owner) return false;
            return true;
        }

        private static IEnumerable<CodeInstruction> SelectPathRequests(IEnumerable<CodeInstruction> instructions) {
            var sort = AccessTools.Method(typeof(List<Navigator>), "Sort", new[] { typeof(Comparison<Navigator>) });
            var select = AccessTools.Method(typeof(CpuWorkOptimizations), nameof(SelectNavigators));
            int replaced = 0;
            foreach (var instruction in instructions) {
                if (instruction.Calls(sort)) {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = select;
                    replaced++;
                }
                yield return instruction;
            }
            if (replaced != 1) throw new InvalidOperationException("ONI navigator ordering contract changed.");
        }

        private static void SelectNavigators(List<Navigator> values, Comparison<Navigator> comparison) {
            // TickFrame consumes at most four valid origins. Ages, navigation
            // failure penalties, ability cloning and result ownership stay native.
            BoundedWorkSelection.SelectFirst(values, comparison, 4, ValidOrigin);
        }

        private static void RefreshFetchIndex(FetchManager __instance, List<FetchManager.Pickup> ___pickups) {
            FetchIndices.GetValue(__instance, _ => new FetchIndex()).Tags.Reset(___pickups);
        }

        private static bool FindFetchTarget(FetchManager __instance, Storage destination, FetchChore chore,
                ref Pickupable __result) {
            // MatchID may contain several IDs. Preserve that game's path until a
            // stable merge index is available; a single mandatory tag is exact.
            if (chore.criteria != FetchChore.MatchCriteria.MatchTags
                    || !FetchIndices.TryGetValue(__instance, out var index)) return true;
            var candidates = index.Tags.Get(chore.tagsFirst);
            for (int i = 0; i < candidates.Count; i++) {
                var pickup = candidates[i].pickupable;
                if (FetchManager.IsFetchablePickup(pickup, chore, destination)) {
                    __result = pickup;
                    return false;
                }
            }
            __result = null;
            return false;
        }
    }
}
