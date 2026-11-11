#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
#if JLIB_HOST_TESTS
using KAnimBatchManager = AnimationBatchCullingCacheCoverage.Manager;
using BatchSet = AnimationBatchCullingCacheCoverage.Set;
using KAnimBatch = AnimationBatchCullingCacheCoverage.Batch;
using KAnimConverter = AnimationBatchCullingCacheCoverage.Converter;
using Vector2I = AnimationBatchCullingCacheCoverage.Chunk;
#endif

namespace Jlib.Runtime
{
    /// <summary>
    /// Optional main-thread cache for stable animation culling. Install at mod loading.
    /// A hit does constant work without reflection, collection access or value boxing.
    /// Animation dirtiness, texture writes and rendering retain their original cadence.
    /// </summary>
    public static class AnimationBatchCullingCache
    {
        private static readonly object installLock = new object();
        private static List<PatchPlan> installed;
        private static bool enabled;
        private static KAnimBatchManager cachedManager;
        private static Vector2I cachedMin, cachedMax;
        private static bool valid;
        private static ulong revision, cachedRevision, structureRevision, cullSerial;
        private static int mutationDepth, cullDepth;

        public static string LastFailure { get; private set; }

        /// <summary>Installs culling and its mutation hooks together, once across consumers.</summary>
        public static bool Install(Harmony harmony)
        {
            if (harmony == null) throw new ArgumentNullException(nameof(harmony));
            lock (installLock)
            {
                if (installed != null && installed.All(plan => plan.IsPresent())) return true;
                enabled = false;
                Invalidate();
                var plans = new List<PatchPlan>();
                try
                {
                    // Resolve the entire supported surface before installing any skip hook.
                    plans.Add(new PatchPlan(Required(typeof(KAnimBatchManager), "UpdateActiveArea",
                        typeof(Vector2I), typeof(Vector2I)), nameof(BeforeCull), nameof(AfterCull), nameof(FinishCull)));
                    foreach (string name in new[] { "CreateInstance", "DestroyInstance", "CompleteInit" })
                        plans.Add(Structure(Required(typeof(KAnimBatchManager), name)));
                    plans.Add(Structure(Required(typeof(KAnimBatchManager), "Register", typeof(KAnimConverter.IAnimConverter))));
                    // This source has no manager Deregister/Reset/Clear. Cover these public
                    // entry points as well if a supported runtime exposes them.
                    foreach (var method in typeof(KAnimBatchManager).GetMethods(BindingFlags.Public |
                        BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                    {
                        if (method.Name == "Deregister" || method.Name == "DeRegister" ||
                            method.Name == "Reset" || method.Name == "Clear")
                            plans.Add(Structure(method));
                    }
                    plans.Add(Structure(Required(typeof(BatchSet), "Clear")));
                    plans.Add(Structure(Required(typeof(BatchSet), "Add", typeof(KAnimConverter.IAnimConverter))));
                    plans.Add(Structure(Required(typeof(BatchSet), "AddBatch", typeof(KAnimBatch))));
                    plans.Add(Structure(Required(typeof(BatchSet), "RemoveBatch", typeof(KAnimBatch))));
                    plans.Add(new PatchPlan(Required(typeof(BatchSet), "SetActive", typeof(bool)),
                        nameof(BeforeSetActive), null, nameof(FinishMutation)));
                    plans.Add(Structure(Required(typeof(KAnimBatch), "Clear")));
                    plans.Add(Structure(Required(typeof(KAnimBatch), "SetBatchSet", typeof(BatchSet))));
                    plans.Add(Structure(Required(typeof(KAnimBatch), "Register", typeof(KAnimConverter.IAnimConverter))));
                    plans.Add(Structure(Required(typeof(KAnimBatch), "Deregister", typeof(KAnimConverter.IAnimConverter))));
                    plans.Add(Structure(Required(typeof(KAnimBatch), "OverrideZ", typeof(float))));
                    plans.Add(Structure(Required(typeof(KAnimBatch), "SetLayer", typeof(int))));
                    plans.Add(new PatchPlan(Required(typeof(KAnimBatch), "Activate"),
                        nameof(BeforeActivate), null, nameof(FinishMutation)));
                    plans.Add(new PatchPlan(Required(typeof(KAnimBatch), "Deactivate"),
                        nameof(BeforeDeactivate), null, nameof(FinishMutation)));

                    // Also repairs an installation whose hooks were externally removed.
                    if (installed != null) Remove(harmony, installed);
                    foreach (var plan in plans) plan.Apply(harmony);
                    installed = plans;
                    LastFailure = null;
                    enabled = true;
                    return true;
                }
                catch (Exception error)
                {
                    // Never leave a usable skip hook with incomplete invalidation coverage.
                    Remove(harmony, plans);
                    installed = null;
                    LastFailure = error.GetType().Name + " " + error.Message;
                    return false;
                }
            }
        }

        /// <summary>Invalidates all managers conservatively. Call on the animation/main thread.</summary>
        public static void Invalidate()
        {
            unchecked { revision++; structureRevision++; }
            valid = false;
            cachedManager = null;
        }

        /// <summary>Compatibility overload. The global revision also invalidates this manager.</summary>
        public static void Invalidate(object manager)
        {
            if (manager != null) Invalidate();
        }

        private static bool BeforeCull(KAnimBatchManager __instance, Vector2I __0,
            Vector2I __1, out CullCall __state)
        {
            __state = default;
            if (!enabled) return true;
            if (mutationDepth == 0 && cullDepth == 0 && valid &&
                ReferenceEquals(cachedManager, __instance) && cachedRevision == revision &&
                cachedMin.x == __0.x && cachedMin.y == __0.y &&
                cachedMax.x == __1.x && cachedMax.y == __1.y)
                return false;

            valid = false;
            cullDepth++;
            unchecked { cullSerial++; }
            __state = new CullCall
            {
                Entered = true, Serial = cullSerial, Structure = structureRevision,
                Min = __0, Max = __1
            };
            return true;
        }

        private static void AfterCull(KAnimBatchManager __instance, bool __runOriginal, CullCall __state)
        {
            // Internal SetActive/Activate/Deactivate transitions may advance revision.
            // Structural changes or recursive culls require a subsequent complete pass.
            if (!enabled || !__runOriginal || !__state.Entered || cullDepth != 1 ||
                mutationDepth != 0 || __state.Serial != cullSerial ||
                __state.Structure != structureRevision) return;
            cachedManager = __instance;
            cachedMin = __state.Min;
            cachedMax = __state.Max;
            cachedRevision = revision;
            valid = true;
        }

        private static void FinishCull(Exception __exception, CullCall __state)
        {
            if (__exception != null) valid = false;
            if (__state.Entered) cullDepth--;
        }

        private static void BeforeStructure(out MutationCall __state)
        {
            __state = BeginMutation(true);
        }

        private static void BeforeSetActive(BatchSet __instance, bool __0, out MutationCall __state)
        {
            __state = __instance.active != __0 ? BeginMutation(false) : default;
        }

        private static void BeforeActivate(KAnimBatch __instance, out MutationCall __state)
        {
            __state = !__instance.active ? BeginMutation(false) : default;
        }

        private static void BeforeDeactivate(KAnimBatch __instance, out MutationCall __state)
        {
            __state = __instance.active ? BeginMutation(false) : default;
        }

        private static MutationCall BeginMutation(bool structural)
        {
            mutationDepth++;
            unchecked
            {
                revision++;
                if (structural) structureRevision++;
            }
            valid = false;
            cachedManager = null;
            return new MutationCall { Entered = true };
        }

        private static void FinishMutation(Exception __exception, MutationCall __state)
        {
            if (!__state.Entered) return;
            mutationDepth--;
            unchecked { revision++; }
            valid = false;
            // Partial failures cannot be committed by an enclosing cull.
            if (__exception != null) unchecked { structureRevision++; }
        }

        private struct CullCall
        {
            internal bool Entered;
            internal ulong Serial, Structure;
            internal Vector2I Min, Max;
        }

        private struct MutationCall { internal bool Entered; }

        // Source audit (decompiled/BatchSet.cs from Assembly-CSharp-firstpass):
        // * Manager.Register constructs info.spatialIdx and adds dictionary/UI/culled entries.
        //   Its private info lists have no other spatial writers. CompleteInit changes ready.
        //   DestroyInstance clears all manager lists/dictionaries and calls BatchSet.Clear.
        // * BatchSet.idx has a private setter, used only by its constructor. Add/AddBatch/
        //   RemoveBatch/Clear own batch membership and order. SetActive owns set.active.
        // * Controller.DeRegister -> KAnimBatch.Deregister -> BatchSet.RemoveBatch, so
        //   manager-level deregistration is absent in this version. Moving/re-layering a
        //   controller re-registers through these same paths. Same-count replacement is
        //   observed at the remove/add events, with no count-based assumptions.
        // * KAnimBatch.SetBatchSet changes owner/XY/active. OverrideZ changes Z, SetLayer
        //   changes layer. Clear/Register/Deregister change membership/lifetime. Public
        //   Activate/Deactivate can change active independently of BatchSetInfo.isActive.
        // * Controllers exposes a mutable list, but changing it alone cannot change the
        //   culling inputs or output: UpdateActiveArea never reads controller membership.
        //   Normal Register/Deregister events remain conservatively covered above.
        // * SetDirty/UpdateDirty/Render change animation data, not culling membership.
        // A single global revision intentionally makes unrelated managers miss as well.
        // Structural hooks conservatively invalidate on entry, including no-op calls.
        // Active hooks invalidate only on a real transition. No per-frame collection scan.

        private static MethodInfo Required(Type type, string name, params Type[] arguments)
        {
            return AccessTools.DeclaredMethod(type, name, arguments)
                ?? throw new MissingMethodException(type.FullName, name);
        }

        private static PatchPlan Structure(MethodBase method)
        {
            return new PatchPlan(method, nameof(BeforeStructure), null, nameof(FinishMutation));
        }

        private static void Remove(Harmony harmony, IEnumerable<PatchPlan> plans)
        {
            foreach (var plan in plans)
            {
                if (plan.Prefix != null) harmony.Unpatch(plan.Target, plan.Prefix);
                if (plan.Postfix != null) harmony.Unpatch(plan.Target, plan.Postfix);
                if (plan.Finalizer != null) harmony.Unpatch(plan.Target, plan.Finalizer);
            }
        }

        private sealed class PatchPlan
        {
            internal readonly MethodBase Target;
            internal readonly MethodInfo Prefix, Postfix, Finalizer;
            internal PatchPlan(MethodBase target, string prefix, string postfix, string finalizer)
            {
                Target = target;
                Prefix = Hook(prefix);
                Postfix = Hook(postfix);
                Finalizer = Hook(finalizer);
            }
            private static MethodInfo Hook(string name)
            {
                return name == null ? null : AccessTools.DeclaredMethod(typeof(AnimationBatchCullingCache), name);
            }
            internal bool IsPresent()
            {
                var info = Harmony.GetPatchInfo(Target);
                return info != null && (Prefix == null || info.Prefixes.Any(p => p.PatchMethod == Prefix)) &&
                    (Postfix == null || info.Postfixes.Any(p => p.PatchMethod == Postfix)) &&
                    (Finalizer == null || info.Finalizers.Any(p => p.PatchMethod == Finalizer));
            }
            internal void Apply(Harmony harmony)
            {
                harmony.Patch(Target,
                    Prefix == null ? null : new HarmonyMethod(Prefix) { priority = Priority.Last },
                    Postfix == null ? null : new HarmonyMethod(Postfix) { priority = Priority.Last },
                    finalizer: Finalizer == null ? null : new HarmonyMethod(Finalizer));
            }
        }
    }
}
