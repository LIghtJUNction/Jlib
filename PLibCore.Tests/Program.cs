#nullable enable
using System.Reflection;
using System.Reflection.Emit;
using PeterHan.PLib.Core;

internal static class Program {
    private static int assertions;
    private static int assemblies;

    private static int Main() {
        try {
            OwnershipContract();
            Console.WriteLine($"PASS: {assertions} ownership assertions; no game DLLs executed.");
            return 0;
        } catch (Exception error) {
            Console.Error.WriteLine(error);
            return 1;
        } finally {
            Global.Instance = null;
        }
    }

    private static void OwnershipContract() {
        var implementation = NewAssembly();
        var owner = NewMod("/mods/CycleTrim");

        Global.Instance = null;
        Equal(null, PUtil.GetModInfo(implementation), "unregistered assembly before Global exists");
        Equal(null, PUtil.GetRegisteredModPath(implementation), "unregistered path");
        PUtil.RegisterModAssembly(implementation, owner);
        Equal(owner, PUtil.GetModInfo(implementation), "registered owner before Global exists");
        Equal("/mods/CycleTrim", PUtil.GetRegisteredModPath(implementation), "registered original mod root");

        PUtil.RegisterModAssembly(implementation, owner);
        PUtil.RegisterModAssembly(implementation, owner);
        Equal(owner, PUtil.GetModInfo(implementation), "same owner registration is idempotent");
        Equal("/mods/CycleTrim", PUtil.GetRegisteredModPath(implementation), "idempotent registration preserves root");

        var competingOwner = NewMod("/mods/other");
        Throws<InvalidOperationException>(() => PUtil.RegisterModAssembly(implementation, competingOwner), "conflicting owner rejected");
        Equal(owner, PUtil.GetModInfo(implementation), "conflict preserves original owner");
        Equal("/mods/CycleTrim", PUtil.GetRegisteredModPath(implementation), "conflict preserves original root");

        Throws<ArgumentNullException>(() => PUtil.RegisterModAssembly(null!, owner), "null implementation", "implementation");
        Throws<ArgumentNullException>(() => PUtil.RegisterModAssembly(NewAssembly(), null!), "null owner", "owner");
        Throws<ArgumentNullException>(() => PUtil.GetModInfo(null!), "null lookup", "implementation");

        var nativeAssembly = NewAssembly();
        var nativeOwner = NewMod("/mods/OniMcp");
        nativeOwner.loaded_mod_data = new KMod.LoadedModData { dlls = [nativeAssembly] };
        Global.Instance = new Global();
        Equal(null, PUtil.GetModInfo(nativeAssembly), "absent mod manager");
        Global.Instance.modManager = new ModManager();
        Equal(null, PUtil.GetModInfo(nativeAssembly), "absent native mod list");
        Global.Instance.modManager.mods = [null, NewMod("/mods/not-loaded"),
            new KMod.Mod { ContentPath = "/mods/no-dlls", loaded_mod_data = new KMod.LoadedModData() }, nativeOwner];
        Equal(nativeOwner, PUtil.GetModInfo(nativeAssembly), "native loaded assembly lookup through incomplete entries");
        Equal(null, PUtil.GetRegisteredModPath(nativeAssembly), "native lookup does not manufacture dynamic registration");
        Equal(owner, PUtil.GetModInfo(implementation), "registered lookup survives native mod manager creation");

        var lateAssembly = NewAssembly();
        Equal(null, PUtil.GetModInfo(lateAssembly), "missing assembly queried before late registration");
        Equal(null, PUtil.GetRegisteredModPath(lateAssembly), "late assembly initially has no owner path");
        var lateOwner = NewMod("/mods/OniOnline");
        PUtil.RegisterModAssembly(lateAssembly, lateOwner);
        Equal(lateOwner, PUtil.GetModInfo(lateAssembly), "late registration visible after earlier miss");
        Equal("/mods/OniOnline", PUtil.GetRegisteredModPath(lateAssembly), "late root visible after earlier miss");

        var formerlyNative = NewAssembly();
        var nativeBeforeRegistration = NewMod("/mods/native-before-registration");
        nativeBeforeRegistration.loaded_mod_data = new KMod.LoadedModData { dlls = [formerlyNative] };
        Global.Instance.modManager.mods.Add(nativeBeforeRegistration);
        Equal(nativeBeforeRegistration, PUtil.GetModInfo(formerlyNative), "native lookup before explicit ownership");
        var registeredAfterNative = NewMod("/mods/explicit-owner");
        PUtil.RegisterModAssembly(formerlyNative, registeredAfterNative);
        Equal(registeredAfterNative, PUtil.GetModInfo(formerlyNative), "explicit registration takes precedence over native listing");
        Equal("/mods/explicit-owner", PUtil.GetRegisteredModPath(formerlyNative), "explicit precedence uses registered root");
    }

    private static Assembly NewAssembly() => AssemblyBuilder.DefineDynamicAssembly(
        new AssemblyName("Jlib.OwnershipFixture." + ++assemblies), AssemblyBuilderAccess.RunAndCollect);

    private static KMod.Mod NewMod(string path) => new() { ContentPath = path };

    private static void Equal(object? expected, object? actual, string scenario) =>
        Check(Equals(expected, actual), scenario + $" (expected {expected ?? "null"}, actual {actual ?? "null"})");

    private static void Check(bool condition, string scenario) {
        assertions++;
        if (!condition)
            throw new InvalidOperationException("FAIL: " + scenario);
    }

    private static void Throws<T>(Action action, string scenario, string? parameter = null) where T : Exception {
        try {
            action();
        } catch (T error) {
            Check(parameter == null || error is ArgumentException argument && argument.ParamName == parameter,
                scenario + " parameter name");
            return;
        }
        Check(false, scenario + " should throw " + typeof(T).Name);
    }
}
