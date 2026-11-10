#nullable disable
using System;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace Jlib.Runtime {
    /// <summary>Keeps rendering responsive while an asynchronous SimDLL finishes a frame.</summary>
    public static class SimulationFrameScheduler {
        private static readonly ConditionalWeakTable<Game, TickBudget> Budgets = new();
        private static Func<bool> isReady;
        public static long DeferredCalls { get; private set; }
        public static long AllowedCalls { get; private set; }

        /// <summary>The readiness callback must propagate worker faults, and allow startup before the first publication.</summary>
        public static void Install(Harmony harmony, Func<bool> canAdvance) {
            if (harmony == null) throw new ArgumentNullException(nameof(harmony));
            if (canAdvance == null) throw new ArgumentNullException(nameof(canAdvance));
            if (isReady != null) {
                if (!Equals(isReady, canAdvance))
                    throw new InvalidOperationException("A different simulation scheduler is already registered.");
                return;
            }
            var target = AccessTools.Method(typeof(Game), "SimEveryTick", new[] { typeof(float) });
            if (target == null) throw new MissingMethodException("Game.SimEveryTick(float) is missing.");
            harmony.Patch(target, prefix: new HarmonyMethod(typeof(SimulationFrameScheduler), nameof(BeforeFrame)) {
                priority = Priority.Last
            });
            isReady = canAdvance;
        }

        private static TickBudget CreateBudget(Game _) => new();

        private static bool BeforeFrame(Game __instance, ref float __0, float ___simDt,
                int ___simSubTick, bool __runOriginal) {
            if (!__runOriginal) return true;
            // Native computation overlaps the managed sub-ticks which use the
            // current immutable publication. Stop only before its next handoff.
            bool ready = isReady();
            if (!Budgets.GetValue(__instance, CreateBudget).TryAdvance(__0, ready,
                    ___simDt, ___simSubTick, out var elapsed)) {
                DeferredCalls++;
                return false;
            }
            __0 = elapsed;
            AllowedCalls++;
            return true;
        }

        internal sealed class TickBudget {
            private float pending;

            internal bool TryAdvance(float delta, bool ready, float accumulated, int subTick, out float elapsed) {
                // Game.SimEveryTick already limits each call to 0.2 seconds.
                // Keep that bound across deferred render frames, and discard
                // the remaining budget on pause instead of advancing later.
                pending = delta > 0f ? Math.Min(0.2f, pending + delta) : 0f;
                elapsed = 0f;
                if (!ready) {
                    const float step = 1f / 60f;
                    // Sub-tick 0 calls UnsafeSim200ms. Leave a quarter-tick
                    // margin so float rounding cannot cross that boundary.
                    float safeTotal = (11 - subTick + 0.25f) * step;
                    if (accumulated > safeTotal) return false;
                    elapsed = Math.Min(pending, Math.Max(0f, safeTotal - accumulated));
                    // Below one sub-tick vanilla performs a zero-time native
                    // handoff instead. That also has to wait for readiness.
                    if (accumulated + elapsed < step) return false;
                    pending -= elapsed;
                    return true;
                }
                // A ready result permits one handoff. Protect against float
                // rounding admitting a thirteenth sub-tick and a second wait.
                float oneHandoff = (23 - subTick + 0.25f) * (1f / 60f);
                elapsed = Math.Min(pending, Math.Max(0f, oneHandoff - accumulated));
                pending -= elapsed;
                return true;
            }
        }
    }
}
