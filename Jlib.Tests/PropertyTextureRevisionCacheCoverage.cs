// Link ../Jlib/Runtime/PropertyTextureRevisionCache.cs into the host project,
// define JLIB_PROPERTY_TEXTURE_REVISION_CACHE_TESTS and call Run() from its runner.
// The opt-in guard permits independent delivery without editing shared project files.
#if JLIB_PROPERTY_TEXTURE_REVISION_CACHE_TESTS
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Jlib.Multicomponent;
using Cache = Jlib.Runtime.PropertyTextureRevisionCache;

internal static class PropertyTextureRevisionCacheCoverage {
    private sealed class Provider : IMulticomponentGridRevisionProvider {
        internal ulong Revision = 1;
        internal bool Throw;
        public ulong GridRevision => Throw ? throw new InvalidOperationException("revision") : Revision;
    }

    private static Cache.Key MakeKey(Provider provider) => new Cache.Key {
        Provider = provider, Revision = provider.Revision,
        Game = new object(), World = new object(), Manager = new object(),
        Buffer = new object(), Texture = new object(), WorldId = 1,
        X0 = 1, Y0 = 2, X1 = 20, Y1 = 30,
        PressureMin = 15, PressureMax = 200, MinVisibility = 0.1f
    };
    private static void Check(bool value, string message) {
        if (!value) throw new InvalidOperationException(message);
    }
    private static void Render(Cache.Key key) {
        Check(Cache.Begin(key, out var state), "changed input must render");
        Cache.Complete(true, state);
        Check(!Cache.Begin(key, out _), "successful render must suppress repeat");
    }
    private static void Changed(Cache.Key original, Cache.Key changed) {
        Cache.Invalidate();
        Render(original);
        Render(changed);
        // Returning to an older key must redraw the single physical buffer.
        Render(original);
    }

    private enum SimProperty { GasPressure = 1, GasColour = 2, Flow = 5 }
    private struct PressureSettings {
        public float Min, Max;
    }
    private sealed class Fixture {
        // Match the game's private nested value type and by-ref entry point.
        private struct TextureProperties {
            public SimProperty simProperty;
            public bool updatedExternally;
            public int OriginalTouches;
        }
        private PressureSettings PressureRange = new PressureSettings { Min = 15, Max = 200 };
        private float MinPressureVisibility = 0.1f;
        internal Provider Provider = new Provider();
        internal Cache.Key Context;
        internal int Originals, Prefixes;
        internal bool ThrowOriginal;
        internal float ObservedMin, ObservedMax, ObservedVisibility;
        internal readonly Exception Failure = new InvalidOperationException("stub original");

        internal int Resets, ShaderReloads;
        internal static int Destroys;
        internal bool ThrowReset, ResetDuringUpdate;
        internal Fixture() { Context = MakeKey(Provider); }
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void DestroyInstance() {
            AssertReleasedContext();
            Destroys++;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal void OnReset(object data = null) {
            AssertReleasedContext();
            Resets++;
            if (ThrowReset) throw Failure;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void OnShadersReloaded() {
            AssertReleasedContext();
            ShaderReloads++;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal void ReloadShaders() { OnShadersReloaded(); }
        internal void Settings(float min, float max, float visibility) {
            PressureRange.Min = min;
            PressureRange.Max = max;
            MinPressureVisibility = visibility;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void UpdateProperty(ref TextureProperties p, int x0, int y0, int x1, int y1) {
            Originals++;
            p.OriginalTouches++;
            // Keep these genuine instance fields observable to the compiler/runner.
            ObservedMin = PressureRange.Min;
            ObservedMax = PressureRange.Max;
            ObservedVisibility = MinPressureVisibility;
            if (ResetDuringUpdate) OnReset();
            if (ThrowOriginal) throw Failure;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal int Call(int property = 1, bool external = false,
                int x0 = 1, int y0 = 2, int x1 = 20, int y1 = 30) {
            var p = new TextureProperties {
                simProperty = (SimProperty)property, updatedExternally = external
            };
            UpdateProperty(ref p, x0, y0, x1, y1);
            return p.OriginalTouches;
        }
    }

    private static bool BeforeFixture(Fixture manager, SimProperty property, bool external,
            int x0, int y0, int x1, int y1, PressureSettings pressure, float visibility,
            out Cache.Scope state) {
        state = default;
        manager.Prefixes++;
        if (property != SimProperty.GasPressure) return true;
        if (external) { Cache.Invalidate(); return true; }
        try {
            var key = manager.Context;
            key.Provider = manager.Provider;
            key.Revision = manager.Provider.GridRevision;
            key.Manager = manager;
            key.X0 = x0; key.Y0 = y0; key.X1 = x1; key.Y1 = y1;
            key.PressureMin = pressure.Min;
            key.PressureMax = pressure.Max;
            key.MinVisibility = visibility;
            return Cache.Begin(key, out state);
        } catch (Exception) { Cache.Invalidate(); return true; }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long MeasureFixtureCalls(Fixture fixture) {
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++) fixture.Call();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long MeasureBeginCalls(Cache.Key key) {
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++) Cache.Begin(key, out _);
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    private static bool suppressOriginal;
    private static bool SuppressOriginal() => !suppressOriginal;

    private static void AssertReleasedContext() {
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        var key = (Cache.Key)typeof(Cache).GetField("completed", flags)!.GetValue(null)!;
        Check(key.Provider == null && key.Game == null && key.World == null
            && key.Manager == null && key.Buffer == null && key.Texture == null,
            "lifecycle prefix releases all global completed-key references before original body");
        Check(!(bool)typeof(Cache).GetField("valid", flags)!.GetValue(null)!, "lifecycle prefix invalidates hit state");
    }

    private abstract class UnpatchableLifecycle {
        internal abstract void Reset();
    }

    private static void InstallationRollbackCoverage(MethodInfo target, MethodInfo before, MethodInfo[] lifecycle) {
        var harmony = new Harmony("Jlib.Tests.PropertyTextureRevisionCache.Rollback");
        var unavailable = AccessTools.Method(typeof(UnpatchableLifecycle), nameof(UnpatchableLifecycle.Reset));
        bool failed = false;
        try {
            // The update hook and two lifecycle hooks install before this abstract
            // method fails. The transaction must remove every hook from this attempt.
            Cache.InstallTarget(harmony, target, before, new[] { lifecycle[0], lifecycle[1], unavailable });
        } catch (Exception) { failed = true; }
        Check(failed, "unsupported lifecycle target fails installation");
        foreach (var method in new[] { target, lifecycle[0], lifecycle[1], unavailable }) {
            var patches = Harmony.GetPatchInfo(method);
            Check(patches == null || !patches.Owners.Contains(harmony.Id), "failed install leaves no owned hooks on " + method.Name);
        }
        AssertReleasedContext();
        var fixture = new Fixture();
        Check(fixture.Call() == 1 && fixture.Call() == 1 && fixture.Prefixes == 0,
            "rolled-back update method retains original behavior");
    }

    private static void WrapperCoverage() {
        var harmony = new Harmony("Jlib.Tests.PropertyTextureRevisionCache");
        var target = AccessTools.Method(typeof(Fixture), "UpdateProperty");
        var before = AccessTools.Method(typeof(PropertyTextureRevisionCacheCoverage), nameof(BeforeFixture));
        // This is the exact production emitter and Harmony installation helper.
        // No substitute prefix or object[]/reflection invocation is used per call.
        var lifecycle = new[] {
            AccessTools.DeclaredMethod(typeof(Fixture), "DestroyInstance", Type.EmptyTypes),
            AccessTools.DeclaredMethod(typeof(Fixture), "OnReset", new[] { typeof(object) }),
            AccessTools.DeclaredMethod(typeof(Fixture), "OnShadersReloaded", Type.EmptyTypes)
        };
        InstallationRollbackCoverage(target, before, lifecycle);
        var wrapper = Cache.InstallTarget(harmony, target, before, lifecycle);
        try {
            var methods = Harmony.GetPatchInfo(target);
            Check(methods.Prefixes.Any(p => p.PatchMethod == wrapper.GetMethod("Prefix")), "emitted prefix installed");
            Check(methods.Postfixes.Any(p => p.PatchMethod == wrapper.GetMethod("Postfix")), "emitted postfix installed");
            Check(methods.Finalizers.Any(p => p.PatchMethod == wrapper.GetMethod("Finalizer")), "emitted finalizer installed");
            foreach (var method in lifecycle)
                Check(Harmony.GetPatchInfo(method).Prefixes.Any(p => p.PatchMethod == wrapper.GetMethod("Invalidate")),
                    "actual lifecycle invalidation prefix installed on " + method.Name);
            Cache.Invalidate();
            var fixture = new Fixture();
            Check(fixture.Call() == 1 && fixture.Originals == 1 && fixture.Prefixes == 1,
                "private nested struct read and original ref write succeed");
            Check(fixture.Call() == 0 && fixture.Originals == 1 && fixture.Prefixes == 2,
                "emitted prefix skips repeated original and preserves caller ref");
            Cache.Invalidate();
            AssertReleasedContext();
            Check(fixture.Call() == 1 && fixture.Call() == 0, "explicit invalidation redraws then skips");
            fixture.OnReset();
            Check(fixture.Resets == 1 && fixture.Call() == 1 && fixture.Call() == 0,
                "actual reset hook redraws despite identical key and identities");
            fixture.ReloadShaders();
            Check(fixture.ShaderReloads == 1 && fixture.Call() == 1 && fixture.Call() == 0,
                "actual shader reload hook redraws despite identical key and identities");
            int destroys = Fixture.Destroys;
            Fixture.DestroyInstance();
            Check(Fixture.Destroys == destroys + 1, "actual static destroy body runs");
            // No intervening UpdateProperty is required to release colony references.
            AssertReleasedContext();
            Check(fixture.Call() == 1 && fixture.Call() == 0, "actual destroy hook invalidates identical key");
            fixture.ThrowReset = true;
            Exception resetError = null;
            try { fixture.OnReset(); } catch (Exception error) { resetError = error; }
            Check(ReferenceEquals(resetError, fixture.Failure), "throwing reset preserves its exception");
            AssertReleasedContext();
            fixture.ThrowReset = false;
            Check(fixture.Call() == 1 && fixture.Call() == 0, "throwing reset invalidates before its body");
            Cache.Invalidate();
            fixture.ResetDuringUpdate = true;
            Check(fixture.Call() == 1, "original can trigger lifecycle reset before postfix");
            AssertReleasedContext();
            fixture.ResetDuringUpdate = false;
            Check(fixture.Call() == 1 && fixture.Call() == 0, "postfix cannot resurrect pre-reset cached context");
            Check(fixture.Call(x0: 2) == 1, "emitted extent argument invalidates");
            Check(fixture.Call(x0: 2) == 0, "changed extent repeat skips");
            Check(fixture.Call() == 1, "return to previous extent redraws");
            Check(fixture.Call(y0: 3) == 1 && fixture.Call(y0: 3) == 0, "emitted y0 argument invalidates then skips");
            Check(fixture.Call(x1: 21) == 1 && fixture.Call(x1: 21) == 0, "emitted x1 argument invalidates then skips");
            Check(fixture.Call(y1: 31) == 1 && fixture.Call(y1: 31) == 0, "emitted y1 argument invalidates then skips");
            Check(fixture.Call() == 1, "restore default extents before pressure tests");
            fixture.Settings(16, 200, 0.1f);
            Check(fixture.Call() == 1 && fixture.ObservedMin == 16, "actual injected pressure minimum invalidates");
            Check(fixture.Call() == 0, "minimum repeat skips");
            fixture.Settings(16, 201, 0.1f);
            Check(fixture.Call() == 1 && fixture.ObservedMax == 201, "actual injected pressure maximum invalidates");
            Check(fixture.Call() == 0, "maximum repeat skips");
            fixture.Settings(16, 201, 0.2f);
            Check(fixture.Call() == 1 && fixture.ObservedVisibility == 0.2f, "actual injected visibility invalidates");
            Check(fixture.Call() == 0, "visibility repeat skips");
            fixture.Provider.Revision++;
            Check(fixture.Call() == 1 && fixture.Call() == 0, "publication change then repeat");
            fixture.Provider = new Provider { Revision = fixture.Provider.Revision };
            Check(fixture.Call() == 1 && fixture.Call() == 0, "provider identity change then repeat");
            Check(fixture.Call(property: 2) == 1 && fixture.Call(property: 2) == 1, "GasColour always executes");
            Check(fixture.Call(property: 5) == 1 && fixture.Call(property: 5) == 1, "external Flow always executes");
            Check(fixture.Call(external: true) == 1 && fixture.Call(external: true) == 1,
                "updatedExternally read through private struct and always executes");
            Check(fixture.Call() == 1 && fixture.Call() == 0, "external gas update invalidates pressure cache");
            fixture.Provider.Revision = 0;
            Check(fixture.Call() == 1 && fixture.Call() == 1, "zero publication never skips");
            fixture.Provider.Revision = 4;
            fixture.Provider.Throw = true;
            Check(fixture.Call() == 1 && fixture.Call() == 1, "provider exception preserves original");
            fixture.Provider.Throw = false;
            Check(fixture.Call() == 1 && fixture.Call() == 0, "provider recovery redraws then skips");
            Cache.Invalidate();
            fixture.ThrowOriginal = true;
            Exception caught = null;
            try { fixture.Call(); } catch (Exception error) { caught = error; }
            Check(ReferenceEquals(caught, fixture.Failure), "actual finalizer preserves original exception identity");
            fixture.ThrowOriginal = false;
            Check(fixture.Call() == 1 && fixture.Call() == 0, "throwing original was never cached");

            var suppress = AccessTools.Method(typeof(PropertyTextureRevisionCacheCoverage), nameof(SuppressOriginal));
            harmony.Patch(target, prefix: new HarmonyMethod(suppress) { priority = Priority.First });
            Cache.Invalidate();
            suppressOriginal = true;
            Check(fixture.Call() == 0, "other Harmony prefix can skip original");
            suppressOriginal = false;
            Check(fixture.Call() == 1 && fixture.Call() == 0, "skipped original was never cached");
            harmony.Unpatch(target, suppress);

            // Warm the same isolated measurement method to settle tiering/OSR.
            // Every measured round must independently report zero allocations.
            for (int round = 0; round < 10; round++) MeasureFixtureCalls(fixture);
            int originalsBeforeMeasurement = fixture.Originals;
            int prefixesBeforeMeasurement = fixture.Prefixes;
            for (int round = 0; round < 10; round++) {
                long allocationDelta = MeasureFixtureCalls(fixture);
                Check(allocationDelta == 0,
                    "actual emitted wrapper and Harmony repeat path allocate zero bytes, round "
                    + round + ", observed " + allocationDelta);
            }
            Check(fixture.Originals == originalsBeforeMeasurement, "measured repeats skipped the original");
            Check(fixture.Prefixes - prefixesBeforeMeasurement == 100000,
                "all measured repeats traversed the emitted wrapper and Harmony prefix");
        } finally {
            suppressOriginal = false;
            harmony.UnpatchAll(harmony.Id);
            Cache.Invalidate();
        }
    }

    internal static void Run() {
        WrapperCoverage();
        var provider = new Provider();
        var key = MakeKey(provider);
        Cache.Invalidate();
        Render(key);
        for (int i = 0; i < 12; i++) {
            var changed = key;
            switch (i) {
                case 0: changed.Game = new object(); break;
                case 1: changed.World = new object(); break;
                case 2: changed.Manager = new object(); break;
                case 3: changed.Buffer = new object(); break;
                case 4: changed.Texture = new object(); break;
                case 5: changed.WorldId++; break;
                case 6: changed.X0++; break;
                case 7: changed.Y0++; break;
                case 8: changed.X1++; break;
                case 9: changed.Y1++; break;
                case 10: changed.PressureMin++; break;
                case 11: changed.PressureMax++; break;
            }
            Changed(key, changed);
        }
        var visibility = key;
        visibility.MinVisibility = 0.2f;
        Changed(key, visibility);
        var replacement = key;
        replacement.Provider = new Provider();
        Changed(key, replacement);
        provider.Revision++;
        key.Revision = provider.Revision;
        Render(key);

        var zero = key;
        zero.Revision = 0;
        Check(Cache.Begin(zero, out var state), "zero revision passes");
        Cache.Complete(true, state);
        Check(Cache.Begin(zero, out _), "zero revision never cached");
        Render(key);
        provider.Throw = true;
        Check(Cache.Begin(key, out state), "revision exception passes");
        Cache.Complete(true, state);
        provider.Throw = false;
        Render(key);

        Cache.Invalidate();
        Check(Cache.Begin(key, out state), "new render");
        provider.Revision++;
        Cache.Complete(true, state);
        key.Revision = provider.Revision;
        Render(key);
        Cache.Invalidate();
        Check(Cache.Begin(key, out state), "other prefix can skip");
        Cache.Complete(false, state);
        Render(key);
        var error = new InvalidOperationException("original");
        Check(ReferenceEquals(error, Cache.Finish(error)), "exception identity preserved");
        Render(key);

        Cache.Invalidate();
        Check(Cache.Begin(key, out state), "completion can fail");
        provider.Throw = true;
        Cache.Complete(true, state);
        provider.Throw = false;
        Render(key);
        var missing = key;
        missing.Texture = null;
        Check(Cache.Begin(missing, out state), "missing texture passes");
        Cache.Complete(true, state);
        Render(key);
        var stale = key;
        stale.Revision--;
        Check(Cache.Begin(stale, out state), "stale sampled revision passes");
        Cache.Complete(true, state);
        Render(key);

        var unsupported = key;
        unsupported.Provider = new object();
        Check(Cache.Begin(unsupported, out state), "unsupported provider passes");
        Cache.Complete(true, state);
        Render(key);
        Cache.Invalidate();
        Check(Cache.Begin(key, out state), "finalizer failure pending render");
        Check(ReferenceEquals(error, Cache.Finish(error)), "pending exception preserved");
        Render(key);

        // Warm the same isolated helper, including interface revision reads.
        for (int round = 0; round < 10; round++) MeasureBeginCalls(key);
        for (int round = 0; round < 10; round++) {
            long allocationDelta = MeasureBeginCalls(key);
            Check(allocationDelta == 0,
                "repeat path allocates zero bytes, round " + round + ", observed " + allocationDelta);
        }
        Cache.Invalidate();
    }
}
#endif
