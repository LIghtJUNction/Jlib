using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

internal static partial class PropertyTextureReferenceCoverage {
    internal static void Run(string managedDirectory) {
        using var stream = File.OpenRead(Path.Combine(managedDirectory, "Assembly-CSharp.dll"));
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        var ownerHandle = reader.TypeDefinitions.Single(handle =>
            TypeName(reader, handle) == "PropertyTextures");
        var owner = reader.GetTypeDefinition(ownerHandle);
        foreach (string name in new[] { "UpdateGasColour", "UpdateLiquid" }) {
            var legacy = owner.GetMethods().Where(handle => {
                var method = reader.GetMethodDefinition(handle);
                return reader.GetString(method.Name) == name && (method.Attributes & MethodAttributes.Static) != 0
                    && Compatible(reader, method);
            }).ToArray();
            if (legacy.Length == 1) {
                Program.Check(true, name + " has the verified legacy static signature");
                continue;
            }
            var fields = owner.GetFields().Where(handle =>
                reader.GetString(reader.GetFieldDefinition(handle).Name) == name).ToArray();
            Program.Equal(1, fields.Length, name + " has one native callback field");
            var fieldHandle = fields.Single();
            var field = reader.GetFieldDefinition(fieldHandle);
            Program.Check((field.Attributes & FieldAttributes.Static) != 0, name + " callback is static");
            var signature = reader.GetBlobReader(field.Signature);
            signature.ReadSignatureHeader();
            Program.Equal(SignatureTypeCode.TypeHandle, signature.ReadSignatureTypeCode(),
                name + " has a concrete delegate type");
            var callbackHandle = signature.ReadTypeHandle();
            Program.Equal(HandleKind.TypeDefinition, callbackHandle.Kind, name + " delegate belongs to native assembly");
            var callback = reader.GetTypeDefinition((TypeDefinitionHandle)callbackHandle);
            Program.Equal("System.MulticastDelegate", TypeName(reader, callback.BaseType), name + " field is a delegate");
            var invoke = callback.GetMethods().Single(handle =>
                reader.GetString(reader.GetMethodDefinition(handle).Name) == "Invoke");
            Program.Check(Compatible(reader, reader.GetMethodDefinition(invoke)),
                name + " delegate accepts exactly TextureRegion and four Int32 values");

            var initializer = owner.GetMethods().Single(handle =>
                reader.GetString(reader.GetMethodDefinition(handle).Name) == ".cctor");
            var code = ReadInstructions(pe.GetMethodBody(reader.GetMethodDefinition(initializer).RelativeVirtualAddress)
                .GetILBytes()!).Where(op => op.Code != OpCodes.Nop).ToArray();
            var assignments = Enumerable.Range(0, code.Length).Where(i => code[i].Code == OpCodes.Stsfld &&
                code[i].Token == MetadataTokens.GetToken(fieldHandle)).ToArray();
            Program.Equal(1, assignments.Length, name + " has one callback initializer");
            int store = assignments.Single();
            Program.Check(store >= 2 && code[store - 1].Code == OpCodes.Newobj &&
                code[store - 2].Code == OpCodes.Ldftn, name + " uses the supported direct delegate initializer");
            var constructor = MetadataTokens.EntityHandle(code[store - 1].Token);
            Program.Equal(callbackHandle, DeclaringType(reader, constructor), name + " constructs its declared delegate");
            var bodyHandle = MetadataTokens.EntityHandle(code[store - 2].Token);
            Program.Equal(HandleKind.MethodDefinition, bodyHandle.Kind, name + " callback body is a native method");
            var body = reader.GetMethodDefinition((MethodDefinitionHandle)bodyHandle);
            Program.Check(Compatible(reader, body) && OwnedBy(reader, body.GetDeclaringType(), ownerHandle),
                name + " target has the verified signature and native owner");
            Console.WriteLine("PASS installed callback metadata: " + name + " -> " +
                TypeName(reader, body.GetDeclaringType()) + "." + reader.GetString(body.Name));
        }
        Console.WriteLine("Property-texture metadata inspected as data; no game assemblies or initializers executed.");
    }

    private static bool Compatible(MetadataReader reader, MethodDefinition method) {
        var signature = reader.GetBlobReader(method.Signature);
        var header = signature.ReadSignatureHeader();
        if (header.IsGeneric) return false;
        if (signature.ReadCompressedInteger() != 5 || signature.ReadSignatureTypeCode() != SignatureTypeCode.Void ||
                signature.ReadSignatureTypeCode() != SignatureTypeCode.TypeHandle ||
                TypeName(reader, signature.ReadTypeHandle()) != "TextureRegion")
            return false;
        for (int i = 0; i < 4; i++)
            if (signature.ReadSignatureTypeCode() != SignatureTypeCode.Int32) return false;
        return signature.RemainingBytes == 0;
    }

    private static bool OwnedBy(MetadataReader reader, TypeDefinitionHandle type, TypeDefinitionHandle owner) {
        while (!type.IsNil) {
            if (type == owner) return true;
            type = reader.GetTypeDefinition(type).GetDeclaringType();
        }
        return false;
    }

    private static EntityHandle DeclaringType(MetadataReader reader, EntityHandle method) {
        if (method.Kind == HandleKind.MethodDefinition)
            return reader.GetMethodDefinition((MethodDefinitionHandle)method).GetDeclaringType();
        if (method.Kind == HandleKind.MemberReference)
            return reader.GetMemberReference((MemberReferenceHandle)method).Parent;
        return default;
    }
}
