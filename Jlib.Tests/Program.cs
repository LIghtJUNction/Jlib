using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using PeterHan.PLib.Core;

internal static partial class Program {
    private static int assertions;
    private static int assemblies;

    private static int Main(string[] args) {
        try {
            OwnershipContract();
            ResourceContract();
            if (args.Length != 0) {
                if (args.Length != 3 || args[0] != "--inspect")
                    throw new ArgumentException("Usage: dotnet run --project Jlib.Tests -- [--inspect <net48/Jlib.dll> <netstandard2.1/Jlib.dll>]");
                InspectLibrary(args[1]);
                InspectLibrary(args[2]);
            }
            Console.WriteLine($"PASS: {assertions} assertions; real ownership and resource helpers, no game DLLs executed.");
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

    private static void InspectLibrary(string path) {
        using var stream = File.OpenRead(path);
        using var image = new PEReader(stream);
        var metadata = image.GetMetadataReader();
        var assembly = metadata.GetAssemblyDefinition();
        Equal("Jlib", metadata.GetString(assembly.Name), path + " assembly name");
        Equal(new Version(0, 1, 0, 0), assembly.Version, path + " assembly version");
        Equal(1, ReadConstant(metadata, "Jlib.Runtime", "JlibApi", "AbiVersion"), path + " ABI");
        Equal("LIghtJUNction.Jlib", ReadConstant(metadata, "Jlib.Runtime", "JlibApi", "StaticId"), path + " static ID");
        Equal("0.1.0", ReadConstant(metadata, "Jlib.Runtime", "JlibApi", "Version"), path + " library version");
        Equal("4.25.0.0", ReadConstant(metadata, "Jlib.Runtime", "JlibApi", "PLibVersion"), path + " protocol identity");
        Equal("4.25.0.0", ReadConstant(metadata, "PeterHan.PLib", "PVersion", "VERSION"), path + " PLib arbitration version");
        foreach (var locale in new[] { "zh", "ru", "fr" }) {
            var resourceName = "PeterHan.PLib.Core.PLibStrings." + locale + ".po";
            Check(metadata.ManifestResources.Any(handle => {
                var resource = metadata.GetManifestResource(handle);
                return metadata.GetString(resource.Name) == resourceName && resource.Implementation.IsNil;
            }), path + " embeds " + resourceName);
        }
        Check(!metadata.AssemblyReferences.Any(handle => metadata.GetString(metadata.GetAssemblyReference(handle).Name).StartsWith("PLib", StringComparison.Ordinal)),
            path + " has no separate PLib assembly dependency");
        var resources = metadata.TypeDefinitions.Select(metadata.GetTypeDefinition).Single(type =>
            metadata.GetString(type.Namespace) == "Jlib.Runtime" && metadata.GetString(type.Name) == "ModResources");
        Check((resources.Attributes & (TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed)) ==
            (TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed), path + " public static resource API");
        var resourceMethods = resources.GetMethods().Select(metadata.GetMethodDefinition).ToArray();
        Equal(4, resourceMethods.Length, path + " resource method count");
        var signatures = new List<string>();
        foreach (var method in resourceMethods) {
            Check((method.Attributes & (MethodAttributes.MemberAccessMask | MethodAttributes.Static)) ==
                (MethodAttributes.Public | MethodAttributes.Static), path + " public static resource method");
            var signature = metadata.GetBlobReader(method.Signature);
            signature.ReadSignatureHeader();
            signatures.Add(metadata.GetString(method.Name) + "/" + signature.ReadCompressedInteger());
        }
        Equal("ReadAllBytes/2,ReadAllText/2,ReadAllText/3,ResolvePath/2",
            string.Join(",", signatures.OrderBy(value => value, StringComparer.Ordinal)), path + " resource overloads");
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
