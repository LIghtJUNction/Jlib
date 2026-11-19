using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Jlib.Runtime;

internal static partial class PropertyTextureCallbackCoverage {
    private delegate void Callback(Region region, int x0, int y0, int x1, int y1);
    private readonly record struct Region(List<string> Writes);
    private static bool overrideNative;

    internal static void Run() {
        Program.Check(AccessTools.Method(typeof(ModernOwner), "UpdateGasColour") == null,
            "U60 callback is a field, so legacy method-name patching cannot bind");
        Program.Check(Resolve(typeof(LegacyOwner), out var legacy), "legacy static target resolves");
        Program.Check(Resolve(typeof(ModernOwner), out var modern), "U60 delegate initializer resolves");
        Program.Check(modern != null && !modern.IsStatic && modern.DeclaringType != typeof(ModernOwner),
            "U60 callback resolves its generated instance method");
        Program.Equal(0, Initializers.Modern, "resolving a callback never executes the owner cctor");
        Program.Check(Resolve(typeof(StaticDelegateOwner), out var staticDelegate) && staticDelegate!.IsStatic,
            "an owned static delegate also resolves");
        Program.Equal(0, Initializers.StaticDelegate, "static delegate resolution does not run initialization");

        Refused(typeof(WrongReturnOwner), "unsupported_callback_signature");
        Refused(typeof(WrongArgumentsOwner), "unsupported_callback_signature");
        Refused(typeof(InstanceOwner), "unsupported_callback_signature");
        Refused(typeof(MulticastOwner), "unsupported_callback_initializer");
        Refused(typeof(FactoryOwner), "unsupported_callback_initializer");
        Refused(typeof(MultipleAssignmentsOwner), "multiple_callback_assignments");
        Refused(typeof(ForeignOwner), "unsupported_callback_body");
        Program.Equal(0, Initializers.Unsupported, "rejecting unknown layouts does not run initialization");
        Program.Check(!PropertyTextureCallbackTarget.TryResolve(null!, "UpdateGasColour", typeof(Region),
            out _, out _), "absent native owner is refused");
        Program.Check(!PropertyTextureCallbackTarget.TryResolve(typeof(ModernOwner), "MissingCallback",
            typeof(Region), out _, out _), "absent callback is refused");

        var harmony = new Harmony("Jlib.Tests.PropertyTextureCallbacks");
        try {
            var prefix = new HarmonyMethod(AccessTools.Method(typeof(PropertyTextureCallbackCoverage), nameof(Prefix)));
            harmony.Patch(legacy!, prefix: prefix);
            harmony.Patch(modern!, prefix: prefix);
            var region = new Region(new List<string>());
            overrideNative = true;
            LegacyOwner.Render(region, 2, 3, 4, 5);
            ModernOwner.Render(region, 6, 7, 8, 9);
            Program.Equal("blend:2,3,4,5|blend:6,7,8,9", string.Join("|", region.Writes),
                "real Harmony binds static and generated-instance arguments by position");
            Program.Equal(1, Initializers.Modern, "native initialization occurs only when rendering begins");
            overrideNative = false;
            region.Writes.Clear();
            LegacyOwner.Render(region, 2, 3, 4, 5);
            ModernOwner.Render(region, 6, 7, 8, 9);
            Program.Equal("legacy|modern", string.Join("|", region.Writes),
                "both native bodies remain active when the override is not ready");

            ModernOwner.ReplaceWithForeign();
            overrideNative = true;
            region.Writes.Clear();
            ModernOwner.Render(region, 1, 2, 3, 4);
            Program.Equal("foreign", string.Join("|", region.Writes),
                "a third-party callback replacement is never redirected by the native-body patch");
            harmony.CreateClassProcessor(typeof(UnavailablePatch)).Patch();
            Program.Equal(0, UnavailablePatch.TargetRequests,
                "Prepare skips an unknown layout before Harmony requests an undefined target");
        } finally {
            harmony.UnpatchAll(harmony.Id);
            overrideNative = false;
        }
        Console.WriteLine("PASS property-texture callbacks: legacy/U60 real Harmony, positional arguments, native fallback and initializer isolation");
    }

    private static bool Resolve(Type owner, out MethodInfo? method) {
        bool found = PropertyTextureCallbackTarget.TryResolve(owner, "UpdateGasColour", typeof(Region),
            out var resolved, out string reason);
        method = resolved;
        if (found) Program.Equal("", reason, owner.Name + " has no failure diagnostic");
        return found;
    }

    private static void Refused(Type owner, string expectedReason) {
        bool found = PropertyTextureCallbackTarget.TryResolve(owner, "UpdateGasColour", typeof(Region),
            out var method, out string reason);
        Program.Check(!found && method == null && reason == expectedReason,
            owner.Name + " preserves its callback; expected=" + expectedReason + "; actual=" + reason);
    }

    private static bool Prefix(Region __0, int __1, int __2, int __3, int __4) {
        if (!overrideNative) return true;
        __0.Writes.Add("blend:" + __1 + "," + __2 + "," + __3 + "," + __4);
        return false;
    }
}
