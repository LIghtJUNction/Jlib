using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Jlib.Runtime;

internal static class DiagnosticsPatchCoverage {
    private const string Owner = "Jlib.Tests.Diagnostics.Primary";
    private const string OtherOwner = "Jlib.Tests.Diagnostics.Other";
    private static readonly MethodInfo Target = Method(nameof(Original));
    private static readonly MethodInfo Prefix = Method(nameof(CountCall));
    private static int calls;
    private static int transpilerRuns;

    internal static void Run() {
        InvalidRequestsPreservePatches();
        SharedMethodsKeepOtherRegistrations();
        MultipleOwnerPatchesAreRefused();
        PriorityChangesPreserveMetadata();
        FailedReorderingRestoresPatch();
        EqualPriorityReorderingIsRefused();
        WildcardOwnerIsRefused();
    }

    private static void InvalidRequestsPreservePatches() {
        var harmony = new Harmony(Owner);
        harmony.Patch(Target, prefix: new HarmonyMethod(Prefix));
        try {
            foreach (var type in new[] { "Unknown", "prefix", "", null }) {
                Program.Check(!JlibDiagnostics.AdjustPatchPriority(Target, Prefix, type!, Owner, 50, out _),
                    "unknown patch type refused");
                AssertPrefix(Owner, Priority.Normal, "invalid type preserves prefix");
            }
            foreach (var owner in new[] { OtherOwner, "", null }) {
                Program.Check(!JlibDiagnostics.AdjustPatchPriority(Target, Prefix, "Prefix", owner!, 50, out _),
                    "unregistered owner refused");
                AssertPrefix(Owner, Priority.Normal, "invalid owner preserves prefix");
            }
            Program.Check(!JlibDiagnostics.AdjustPatchPriority(Target, Method(nameof(UnusedPrefix)),
                "Prefix", Owner, 50, out _), "unregistered patch method refused");
            Program.Equal(1, Harmony.GetPatchInfo(Target).Prefixes.Count, "missing patch does not add a registration");
            Program.Check(!JlibDiagnostics.AdjustPatchPriority(Target, Prefix, "Prefix", Owner,
                int.MaxValue, out _), "priority overflow refused");
            AssertPrefix(Owner, Priority.Normal, "overflow preserves prefix");
            calls = 0;
            Original();
            Program.Equal(1, calls, "rejected requests preserve executable prefix");
        } finally {
            harmony.Unpatch(Target, HarmonyPatchType.All, "*");
        }
    }

    private static void SharedMethodsKeepOtherRegistrations() {
        var harmony = new Harmony(Owner);
        var other = new Harmony(OtherOwner);
        harmony.Patch(Target, prefix: new HarmonyMethod(Prefix) { priority = Priority.High },
            postfix: new HarmonyMethod(Prefix));
        other.Patch(Target, prefix: new HarmonyMethod(Prefix));
        try {
            Program.Check(JlibDiagnostics.AdjustPatchPriority(Target, Prefix, "Prefix", Owner, 50, out _),
                "unique owner and stage can be reordered");
            AssertPrefix(Owner, Priority.High + 50, "selected owner priority changed");
            AssertPrefix(OtherOwner, Priority.Normal, "other owner remains registered");
            var info = Harmony.GetPatchInfo(Target);
            Program.Equal(2, info.Prefixes.Count, "shared prefix has both owners");
            Program.Equal(1, info.Postfixes.Count, "same method in another stage remains registered");
            calls = 0;
            Original();
            Program.Equal(3, calls, "all three registrations still execute");
        } finally {
            harmony.Unpatch(Target, HarmonyPatchType.All, "*");
        }
    }

    private static void MultipleOwnerPatchesAreRefused() {
        var harmony = new Harmony(Owner);
        harmony.Patch(Target, prefix: new HarmonyMethod(Prefix));
        harmony.Patch(Target, prefix: new HarmonyMethod(Method(nameof(UnusedPrefix))));
        try {
            Program.Check(!JlibDiagnostics.AdjustPatchPriority(Target, Prefix, "Prefix", Owner, 50, out _),
                "multiple patches for one owner and stage refused");
            Program.Equal(2, Harmony.GetPatchInfo(Target).Prefixes.Count, "refusal keeps both owner patches");
            AssertPrefix(Owner, Priority.Normal, "ambiguous selection keeps priority");
        } finally {
            harmony.Unpatch(Target, HarmonyPatchType.All, "*");
        }
    }

    private static void PriorityChangesPreserveMetadata() {
        var harmony = new Harmony(Owner);
        var writer = FileLog.LogWriter;
        using var discard = new StreamWriter(Stream.Null);
        FileLog.LogWriter = discard;
        try {
            harmony.Patch(Target, prefix: new HarmonyMethod(Prefix) {
                priority = Priority.High,
                before = ["Jlib.Tests.Diagnostics.After"],
                after = ["Jlib.Tests.Diagnostics.Before"],
                debug = true
            });
            Program.Check(JlibDiagnostics.AdjustPatchPriority(Target, Prefix, "Prefix", Owner, 50, out _),
                "valid priority change succeeds");
            var patch = AssertPrefix(Owner, Priority.High + 50, "new priority applied");
            Program.Equal("Jlib.Tests.Diagnostics.After", string.Join(",", patch.before), "before constraints preserved");
            Program.Equal("Jlib.Tests.Diagnostics.Before", string.Join(",", patch.after), "after constraints preserved");
            Program.Check(patch.debug, "patch debug setting preserved");
        } finally {
            harmony.Unpatch(Target, HarmonyPatchType.All, "*");
            FileLog.LogWriter = writer;
        }
    }

    private static void FailedReorderingRestoresPatch() {
        var target = Method(nameof(OriginalValue));
        var first = Method(nameof(ReplaceOneWithTwo));
        var harmony = new Harmony(Owner);
        var other = new Harmony(OtherOwner);
        harmony.Patch(target, transpiler: new HarmonyMethod(first) { priority = Priority.High });
        other.Patch(target, transpiler: new HarmonyMethod(Method(nameof(ReplaceRemainingOne))) {
            priority = Priority.Normal
        });
        try {
            Program.Equal(2, OriginalValue(), "original transpiler order executes");
            Program.Check(!JlibDiagnostics.AdjustPatchPriority(target, first, "Transpiler", Owner, -300, out _),
                "incompatible transpiler order refused");
            var patches = Harmony.GetPatchInfo(target).Transpilers;
            Program.Equal(2, patches.Count, "failed reorder retains both transpilers");
            Program.Equal(Priority.High, patches.Single(p => p.owner == Owner).priority,
                "failed reorder restores original priority");
            Program.Equal(2, OriginalValue(), "failed reorder restores original executable behavior");
        } finally {
            harmony.Unpatch(target, HarmonyPatchType.All, "*");
        }
    }

    private static void EqualPriorityReorderingIsRefused() {
        var target = Method(nameof(OriginalValue));
        var first = Method(nameof(ReplaceOneWithTwo));
        var harmony = new Harmony(Owner);
        var other = new Harmony(OtherOwner);
        harmony.Patch(target, transpiler: new HarmonyMethod(first));
        other.Patch(target, transpiler: new HarmonyMethod(Method(nameof(ReplaceRemainingOne))));
        try {
            Program.Equal(2, OriginalValue(), "equal-priority transpilers execute in registration order");
            string registrations = Describe(Harmony.GetPatchInfo(target).Transpilers);
            int runs = transpilerRuns;
            Program.Check(!JlibDiagnostics.AdjustPatchPriority(target, first, "Transpiler", Owner, -50, out _),
                "ambiguous equal-priority adjustment refused");
            Program.Equal(2, OriginalValue(), "equal-priority refusal preserves original executable behavior");
            Program.Equal(registrations, Describe(Harmony.GetPatchInfo(target).Transpilers),
                "equal-priority refusal preserves registration indices");
            Program.Equal(runs, transpilerRuns, "equal-priority refusal does not rebuild the pipeline");
            Program.Check(JlibDiagnostics.AdjustPatchPriority(target, first, "Transpiler", Owner, 0, out _),
                "zero adjustment succeeds without replacing the patch");
            Program.Equal(runs, transpilerRuns, "zero adjustment does not rebuild the pipeline");
            Program.Equal(registrations, Describe(Harmony.GetPatchInfo(target).Transpilers),
                "zero adjustment preserves registration indices");
        } finally {
            harmony.Unpatch(target, HarmonyPatchType.All, "*");
        }
    }

    private static void WildcardOwnerIsRefused() {
        var harmony = new Harmony("*");
        var other = new Harmony(OtherOwner);
        harmony.Patch(Target, prefix: new HarmonyMethod(Prefix) { priority = Priority.High });
        other.Patch(Target, prefix: new HarmonyMethod(Prefix));
        try {
            string registrations = Describe(Harmony.GetPatchInfo(Target).Prefixes);
            bool adjusted = JlibDiagnostics.AdjustPatchPriority(Target, Prefix, "Prefix", "*", 50, out _);
            Program.Equal(2, Harmony.GetPatchInfo(Target).Prefixes.Count, "wildcard request preserves both owners");
            Program.Check(!adjusted, "wildcard owner is refused");
            Program.Equal(registrations, Describe(Harmony.GetPatchInfo(Target).Prefixes),
                "wildcard refusal preserves registrations and priorities");
            calls = 0;
            Original();
            Program.Equal(2, calls, "wildcard refusal preserves both executable patches");
        } finally {
            harmony.Unpatch(Target, HarmonyPatchType.All, "*");
        }
    }

    private static string Describe(IEnumerable<Patch> patches) => string.Join("|",
        patches.Select(p => $"{p.owner}:{p.priority}:{p.index}"));

    private static Patch AssertPrefix(string owner, int priority, string scenario) {
        var patch = Harmony.GetPatchInfo(Target).Prefixes.SingleOrDefault(p => p.owner == owner && p.PatchMethod == Prefix);
        Program.Check(patch != null, scenario + " exists");
        Program.Equal(priority, patch!.priority, scenario + " priority");
        return patch;
    }

    private static MethodInfo Method(string name) => typeof(DiagnosticsPatchCoverage).GetMethod(name,
        BindingFlags.NonPublic | BindingFlags.Static)!;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Original() { }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int OriginalValue() => 1;

    private static void CountCall() => calls++;

    private static void UnusedPrefix() { }

    private static IEnumerable<CodeInstruction> ReplaceOneWithTwo(IEnumerable<CodeInstruction> instructions) {
        transpilerRuns++;
        var code = instructions.ToList();
        var literal = code.SingleOrDefault(instruction => instruction.opcode == OpCodes.Ldc_I4_1);
        if (literal == null)
            throw new InvalidOperationException("Expected the original literal before the other transpiler runs.");
        literal.opcode = OpCodes.Ldc_I4_2;
        return code;
    }

    private static IEnumerable<CodeInstruction> ReplaceRemainingOne(IEnumerable<CodeInstruction> instructions) {
        transpilerRuns++;
        foreach (var instruction in instructions) {
            if (instruction.opcode == OpCodes.Ldc_I4_1)
                instruction.opcode = OpCodes.Ldc_I4_3;
            yield return instruction;
        }
    }
}
