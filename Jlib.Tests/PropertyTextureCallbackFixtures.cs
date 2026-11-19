using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Jlib.Runtime;

internal static partial class PropertyTextureCallbackCoverage {
    private static class Initializers {
        internal static int Modern, StaticDelegate, Unsupported;
    }

    private static class LegacyOwner {
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void UpdateGasColour(Region different, int one, int two, int three, int four) {
            different.Writes.Add("legacy");
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Render(Region r, int a, int b, int c, int d) => UpdateGasColour(r, a, b, c, d);
    }

    private static class ModernOwner {
        private static Callback UpdateGasColour = delegate(Region different, int one, int two, int three, int four) {
            different.Writes.Add("modern");
        };
        static ModernOwner() { Initializers.Modern++; }
        internal static void Render(Region r, int a, int b, int c, int d) => UpdateGasColour(r, a, b, c, d);
        internal static void ReplaceWithForeign() { UpdateGasColour = ForeignBody; }
    }

    private static class StaticDelegateOwner {
        internal static readonly Callback UpdateGasColour = new Callback(Original);
        static StaticDelegateOwner() { Initializers.StaticDelegate++; }
        private static void Original(Region r, int a, int b, int c, int d) => r.Writes.Add("static");
    }

    private static class WrongReturnOwner {
        private static int UpdateGasColour(Region r, int a, int b, int c, int d) => 1;
    }
    private static class WrongArgumentsOwner {
        private static void UpdateGasColour(Region r, long a, int b, int c, int d) { }
    }
    private sealed class InstanceOwner {
        private void UpdateGasColour(Region r, int a, int b, int c, int d) { }
    }

    private static class MulticastOwner {
        internal static readonly Callback UpdateGasColour = new Callback(First) + new Callback(Second);
        static MulticastOwner() { Initializers.Unsupported++; }
        private static void First(Region r, int a, int b, int c, int d) { }
        private static void Second(Region r, int a, int b, int c, int d) { }
    }
    private static class FactoryOwner {
        internal static readonly Callback UpdateGasColour = Create();
        static FactoryOwner() { Initializers.Unsupported++; }
        private static Callback Create() => delegate(Region r, int a, int b, int c, int d) { };
    }
    private static class MultipleAssignmentsOwner {
        internal static readonly Callback UpdateGasColour;
        static MultipleAssignmentsOwner() {
            UpdateGasColour = new Callback(First);
            UpdateGasColour = new Callback(Second);
            Initializers.Unsupported++;
        }
        private static void First(Region r, int a, int b, int c, int d) { }
        private static void Second(Region r, int a, int b, int c, int d) { }
    }
    private static class ForeignOwner {
        internal static readonly Callback UpdateGasColour = new Callback(ForeignBody);
        static ForeignOwner() { Initializers.Unsupported++; }
    }
    private static void ForeignBody(Region r, int a, int b, int c, int d) => r.Writes.Add("foreign");

    [HarmonyPatch]
    private static class UnavailablePatch {
        internal static int TargetRequests;
        [HarmonyPrepare]
        private static bool Prepare() => PropertyTextureCallbackTarget.TryResolve(typeof(WrongReturnOwner),
            "UpdateGasColour", typeof(Region), out _, out _);
        [HarmonyTargetMethod]
        private static MethodBase TargetMethod() {
            TargetRequests++;
            throw new InvalidOperationException("unavailable target was requested");
        }
        [HarmonyPrefix]
        private static bool Prefix() => true;
    }
}
