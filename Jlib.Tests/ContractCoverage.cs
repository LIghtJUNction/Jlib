using System.Reflection;
using System.Reflection.Emit;
using Jlib.Runtime;

internal static class ContractCoverage {
    internal static void Run() {
        var current = JlibApi.CheckCompatibility(JlibApi.AbiVersion, JlibApi.Version);
        Program.Equal(JlibCompatibilityStatus.Compatible, current.Status, "current provider satisfies its own version");
        Program.Equal("jlib.compatible", current.Code, "compatible code");
        Program.Check(current.Message.Length > 0, "compatible message");
        JlibApi.RequireCompatibility(JlibApi.AbiVersion, null);

        var olderFloor = JlibApi.CheckCompatibility(JlibApi.AbiVersion, "0.1.0");
        Program.Equal(JlibCompatibilityStatus.Compatible, olderFloor.Status, "0.2.0 satisfies a 0.1.0 minimum");
        var sameFourPart = JlibContract.Evaluate(1, "0.2.0", JlibApi.PLibVersion, JlibApi.StaticId, 1, "0.2.0.0");
        Program.Equal(JlibCompatibilityStatus.Compatible, sameFourPart.Status, "0.2.0 matches minimum 0.2.0.0");
        var numericOrder = JlibContract.Evaluate(1, "0.10.0", JlibApi.PLibVersion, JlibApi.StaticId, 1, "0.9.0");
        Program.Equal(JlibCompatibilityStatus.Compatible, numericOrder.Status, "version comparison is numeric");
        var tooOld = JlibApi.CheckCompatibility(JlibApi.AbiVersion, "99.0.0");
        Program.Equal(JlibCompatibilityStatus.VersionOlderThanRequired, tooOld.Status, "future minimum is rejected");
        Program.Equal("jlib.version.older", tooOld.Code, "older code");

        var abi = JlibApi.CheckCompatibility(JlibApi.AbiVersion + 1, JlibApi.Version);
        Program.Equal(JlibCompatibilityStatus.AbiMismatch, abi.Status, "ABI must match exactly");
        var protocol = JlibContract.Evaluate(1, JlibApi.Version, "4.24.0.0", JlibApi.StaticId, 1, null);
        Program.Equal(JlibCompatibilityStatus.ProtocolMismatch, protocol.Status, "PLib protocol must match");
        var staticId = JlibContract.Evaluate(1, JlibApi.Version, JlibApi.PLibVersion, "Other.Jlib", 1, null);
        Program.Equal(JlibCompatibilityStatus.StaticIdMismatch, staticId.Status, "static ID must match");
        var unreadable = JlibContract.Evaluate(2, "not-a-version", JlibApi.PLibVersion, JlibApi.StaticId, 1, null);
        Program.Equal(JlibCompatibilityStatus.VersionUnreadable, unreadable.Status, "unreadable version wins over ABI");

        Program.Throws<ArgumentOutOfRangeException>(() => JlibApi.CheckCompatibility(-1), "negative ABI", "requiredAbi");
        Program.Throws<ArgumentException>(() => JlibApi.CheckCompatibility(1, "v0.2.0"), "prefixed minimum", "minimumVersion");
        Program.Throws<JlibCompatibilityException>(() => JlibApi.RequireCompatibility(JlibApi.AbiVersion + 1), "require rejects ABI");
        try {
            JlibApi.RequireCompatibility(99);
            Program.Check(false, "require should throw");
        } catch (JlibCompatibilityException error) {
            Program.Equal(JlibCompatibilityStatus.AbiMismatch, error.Result.Status, "exception carries the result");
        }

        var reflected = JlibContract.Inspect(new[] { typeof(JlibApi).Assembly }, JlibApi.AbiVersion, JlibApi.Version);
        Program.Equal(JlibCompatibilityStatus.Compatible, reflected.Status, "reflection reads the real constants");
        Program.Equal(JlibApi.StaticId, reflected.ProviderStaticId, "reflected static ID");
        Program.Equal(typeof(JlibApi).Assembly.GetName().Name, reflected.ProviderAssemblyName, "reflected assembly name");

        Program.Equal(JlibCompatibilityStatus.ProviderMissing,
            JlibContract.Inspect(new Assembly?[] { null, typeof(string).Assembly }, 1, null).Status,
            "assemblies without JlibApi are not providers");
        Program.Throws<ArgumentNullException>(() => JlibContract.Inspect(null, 1, null), "null assembly list", "assemblies");

        var ambiguous = JlibContract.Inspect(new[] { DefineProvider("Jlib.Fake.A", 1, "0.2.0", JlibApi.PLibVersion, JlibApi.StaticId),
            DefineProvider("Jlib.Fake.B", 1, "0.2.0", JlibApi.PLibVersion, JlibApi.StaticId) }, 1, "0.2.0");
        Program.Equal(JlibCompatibilityStatus.ProviderAmbiguous, ambiguous.Status, "two providers are ambiguous");
        Program.Equal(2, ambiguous.ProviderCount, "ambiguous count");

        var incomplete = JlibContract.Inspect(new[] { DefineProvider("Jlib.Fake.Incomplete", 1, "0.2.0", JlibApi.PLibVersion, JlibApi.StaticId, omit: "PLibVersion") }, 1, null);
        Program.Equal(JlibCompatibilityStatus.IdentityIncomplete, incomplete.Status, "missing constant is incomplete");
        var wrongType = JlibContract.Inspect(new[] { DefineProvider("Jlib.Fake.WrongType", 1, "0.2.0", JlibApi.PLibVersion, JlibApi.StaticId, abiAsString: true) }, 1, null);
        Program.Equal(JlibCompatibilityStatus.IdentityIncomplete, wrongType.Status, "wrong constant type is incomplete");
        var foreign = JlibContract.Inspect(new[] { DefineProvider("Other.Jlib", 1, "9.0.0", "9.0.0.0", "Other.Jlib") }, 1, null);
        Program.Equal(JlibCompatibilityStatus.StaticIdMismatch, foreign.Status, "reflected foreign static ID");

        Registration();
    }

    private static void Registration() {
        var implementation = Program.NewAssembly();
        var owner = Program.NewMod("/mods/CycleTrim");
        owner.staticID = "CycleTrim";
        JlibApi.RegisterConsumer(implementation, owner);
        JlibApi.RegisterConsumer(implementation, owner);
        Program.Equal(owner, PUtilOwner(implementation), "JlibApi registration is idempotent");
        Program.Equal("/mods/CycleTrim", PeterHan.PLib.Core.PUtil.GetRegisteredModPath(implementation), "JlibApi keeps the mod root");

        var competing = Program.NewMod("/mods/other");
        competing.staticID = "Other";
        Program.Throws<InvalidOperationException>(() => JlibApi.RegisterConsumer(implementation, competing), "JlibApi rejects a second owner");
        Program.Equal(owner, PUtilOwner(implementation), "rejected owner leaves the first registration");

        Program.Throws<ArgumentNullException>(() => JlibApi.RegisterConsumer(null!, owner), "null implementation", "implementation");
        Program.Throws<ArgumentNullException>(() => JlibApi.RegisterConsumer(Program.NewAssembly(), null!), "null consumer owner", "owner");

        var blank = Program.NewMod("  ");
        blank.staticID = "CycleTrim";
        var blankImplementation = Program.NewAssembly();
        Program.Throws<ArgumentException>(() => JlibApi.RegisterConsumer(blankImplementation, blank), "blank content path", "owner");
        Program.Equal(null, PUtilOwner(blankImplementation), "blank path is not registered");

        var providerMod = Program.NewMod("/mods/Jlib");
        providerMod.staticID = JlibApi.StaticId;
        var named = Program.NewAssembly();
        Program.Throws<InvalidOperationException>(() => JlibApi.RegisterConsumer(named, providerMod), "provider static ID");
        Program.Equal(null, PUtilOwner(named), "provider mod is not registered");

        var providerAssembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(JlibContract.ProviderAssemblyName), AssemblyBuilderAccess.RunAndCollect);
        Program.Throws<InvalidOperationException>(() => JlibApi.RegisterConsumer(providerAssembly, owner), "assembly named Jlib");
        Program.Throws<InvalidOperationException>(() => JlibApi.RegisterConsumer(typeof(JlibApi).Assembly, owner), "assembly that contains JlibApi");

        var legacyAssembly = Program.NewAssembly();
        var legacyOwner = Program.NewMod(" ");
        legacyOwner.staticID = JlibApi.StaticId;
        PeterHan.PLib.Core.PUtil.RegisterModAssembly(legacyAssembly, legacyOwner);
        Program.Equal(legacyOwner, PUtilOwner(legacyAssembly), "PLib registration helper still accepts owners that JlibApi rejects");
    }

    private static KMod.Mod? PUtilOwner(Assembly implementation) =>
        PeterHan.PLib.Core.PUtil.GetModInfo(implementation);

    private static Assembly DefineProvider(string assemblyName, int abi, string version, string protocol,
            string staticId, string? omit = null, bool abiAsString = false) {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(assemblyName), AssemblyBuilderAccess.RunAndCollect);
        var module = assembly.DefineDynamicModule(assemblyName);
        var type = module.DefineType(JlibContract.ApiTypeName, TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
        if (omit != "AbiVersion") {
            if (abiAsString)
                Literal(type, "AbiVersion", "1");
            else
                Literal(type, "AbiVersion", abi);
        }
        if (omit != "Version")
            Literal(type, "Version", version);
        if (omit != "PLibVersion")
            Literal(type, "PLibVersion", protocol);
        if (omit != "StaticId")
            Literal(type, "StaticId", staticId);
        type.CreateType();
        return assembly;
    }

    private static void Literal(TypeBuilder type, string name, object value) {
        var field = type.DefineField(name, value.GetType(), FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal);
        field.SetConstant(value);
    }
}
