using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using PeterHan.PLib.Core;

internal static class Program {
    private static int assertions;
    private static int assemblies;

    private static int Main(string[] args) {
        try {
            var loaded = Jlib.Runtime.JlibApi.DiscoverProviders(Jlib.Runtime.JlibApi.AbiVersion, Jlib.Runtime.JlibApi.Version);
            Equal(Jlib.Runtime.JlibCompatibilityStatus.Compatible, loaded.Status, "the test host sees one real JlibApi");
            Equal(1, loaded.ProviderCount, "one provider before fixture assemblies exist");
            OwnershipContract();
            ContractCoverage.Run();
            SourceContract.Run(args);
            Console.WriteLine($"PASS: {assertions} assertions; real ownership and contract helpers, no game DLLs executed.");
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

    internal static Assembly NewAssembly() => AssemblyBuilder.DefineDynamicAssembly(
        new AssemblyName("Jlib.OwnershipFixture." + ++assemblies), AssemblyBuilderAccess.RunAndCollect);

    internal static KMod.Mod NewMod(string path) => new() { ContentPath = path };

    internal static void InspectLibrary(string path) {
        using var stream = File.OpenRead(path);
        using var image = new PEReader(stream);
        var metadata = image.GetMetadataReader();
        var assembly = metadata.GetAssemblyDefinition();
        Equal("Jlib", metadata.GetString(assembly.Name), path + " assembly name");
        Equal(new Version(Jlib.Runtime.JlibApi.Version + ".0"), assembly.Version, path + " assembly version");
        Equal(Jlib.Runtime.JlibApi.AbiVersion, ReadConstant(metadata, "Jlib.Runtime", "JlibApi", "AbiVersion"), path + " ABI");
        Equal(Jlib.Runtime.JlibApi.StaticId, ReadConstant(metadata, "Jlib.Runtime", "JlibApi", "StaticId"), path + " static ID");
        Equal(Jlib.Runtime.JlibApi.Version, ReadConstant(metadata, "Jlib.Runtime", "JlibApi", "Version"), path + " library version");
        Equal(Jlib.Runtime.JlibApi.PLibVersion, ReadConstant(metadata, "Jlib.Runtime", "JlibApi", "PLibVersion"), path + " protocol identity");
        Equal(Jlib.Runtime.JlibApi.PLibVersion, ReadConstant(metadata, "PeterHan.PLib", "PVersion", "VERSION"), path + " PLib arbitration version");
        Equal(Jlib.Runtime.JlibContract.ExpectedAbi, ReadConstant(metadata, "Jlib.Runtime", "JlibContract", "ExpectedAbi"), path + " contract ABI");
        Equal(Jlib.Runtime.JlibContract.ExpectedStaticId, ReadConstant(metadata, "Jlib.Runtime", "JlibContract", "ExpectedStaticId"), path + " contract static ID");
        Equal(Jlib.Runtime.JlibContract.ExpectedPLibVersion, ReadConstant(metadata, "Jlib.Runtime", "JlibContract", "ExpectedPLibVersion"), path + " contract protocol");
        foreach (var method in new[] { "CheckCompatibility", "RequireCompatibility", "DiscoverProviders", "RegisterConsumer" })
            Check(HasMethod(metadata, "Jlib.Runtime", "JlibApi", method), path + " JlibApi." + method);
        foreach (var method in new[] { "Evaluate", "Inspect", "TryParseVersion" })
            Check(HasMethod(metadata, "Jlib.Runtime", "JlibContract", method), path + " JlibContract." + method);
        Check(HasType(metadata, "Jlib.Runtime", "JlibCompatibility"), path + " compatibility result");
        Check(HasType(metadata, "Jlib.Runtime", "JlibCompatibilityException"), path + " compatibility exception");
        foreach (var locale in new[] { "zh", "ru", "fr" }) {
            var resourceName = "PeterHan.PLib.Core.PLibStrings." + locale + ".po";
            Check(metadata.ManifestResources.Any(handle => {
                var resource = metadata.GetManifestResource(handle);
                return metadata.GetString(resource.Name) == resourceName && resource.Implementation.IsNil;
            }), path + " embeds " + resourceName);
        }
        Check(!metadata.AssemblyReferences.Any(handle => metadata.GetString(metadata.GetAssemblyReference(handle).Name).StartsWith("PLib", StringComparison.Ordinal)),
            path + " has no separate PLib assembly dependency");
        Console.WriteLine("Inspected metadata only: " + Path.GetFullPath(path));
    }

    private static object ReadConstant(MetadataReader metadata, string ns, string typeName, string fieldName) {
        foreach (var handle in metadata.TypeDefinitions) {
            var type = metadata.GetTypeDefinition(handle);
            if (metadata.GetString(type.Namespace) != ns || metadata.GetString(type.Name) != typeName)
                continue;
            foreach (var fieldHandle in type.GetFields()) {
                var field = metadata.GetFieldDefinition(fieldHandle);
                if (metadata.GetString(field.Name) != fieldName)
                    continue;
                var constant = metadata.GetConstant(field.GetDefaultValue());
                var reader = metadata.GetBlobReader(constant.Value);
                return constant.TypeCode switch {
                    ConstantTypeCode.Int32 => reader.ReadInt32(),
                    ConstantTypeCode.String => reader.ReadUTF16(reader.Length),
                    _ => throw new InvalidOperationException("Unexpected constant type: " + constant.TypeCode)
                };
            }
        }
        throw new InvalidOperationException("Required contract field missing: " + ns + "." + typeName + "." + fieldName);
    }

    private static bool HasType(MetadataReader metadata, string ns, string typeName) {
        foreach (var handle in metadata.TypeDefinitions) {
            var type = metadata.GetTypeDefinition(handle);
            if (metadata.GetString(type.Namespace) == ns && metadata.GetString(type.Name) == typeName)
                return true;
        }
        return false;
    }

    private static bool HasMethod(MetadataReader metadata, string ns, string typeName, string methodName) {
        foreach (var handle in metadata.TypeDefinitions) {
            var type = metadata.GetTypeDefinition(handle);
            if (metadata.GetString(type.Namespace) != ns || metadata.GetString(type.Name) != typeName)
                continue;
            foreach (var methodHandle in type.GetMethods()) {
                if (metadata.GetString(metadata.GetMethodDefinition(methodHandle).Name) == methodName)
                    return true;
            }
        }
        return false;
    }

    internal static void Equal(object? expected, object? actual, string scenario) =>
        Check(Equals(expected, actual), scenario + $" (expected {expected ?? "null"}, actual {actual ?? "null"})");

    internal static void Check(bool condition, string scenario) {
        assertions++;
        if (!condition)
            throw new InvalidOperationException("FAIL: " + scenario);
    }

    internal static void Throws<T>(Action action, string scenario, string? parameter = null) where T : Exception {
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
