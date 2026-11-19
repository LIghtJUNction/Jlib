using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;

internal static partial class PropertyTextureReferenceCoverage {
    private readonly record struct Instruction(OpCode Code, int Token);
    private static readonly Dictionary<short, OpCode> Opcodes = typeof(OpCodes).GetFields(
        BindingFlags.Public | BindingFlags.Static).Where(field => field.FieldType == typeof(OpCode))
        .Select(field => (OpCode)field.GetValue(null)!).ToDictionary(opcode => opcode.Value);

    private static IEnumerable<Instruction> ReadInstructions(byte[] bytes) {
        for (int at = 0; at < bytes.Length;) {
            short value = bytes[at++];
            if (value == 0xfe) value = unchecked((short)(0xfe00 | bytes[at++]));
            var opcode = Opcodes[value];
            int size = opcode.OperandType switch {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineI or OperandType.ShortInlineVar or OperandType.ShortInlineBrTarget => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => checked(4 + 4 * BitConverter.ToInt32(bytes, at)),
                _ => 4
            };
            if (size < 0 || at + size > bytes.Length) throw new InvalidDataException("Invalid native callback initializer IL");
            int token = size >= 4 ? BitConverter.ToInt32(bytes, at) : 0;
            yield return new Instruction(opcode, token);
            at += size;
        }
    }

    private static string TypeName(MetadataReader reader, EntityHandle handle) {
        if (handle.Kind == HandleKind.TypeDefinition) {
            var type = reader.GetTypeDefinition((TypeDefinitionHandle)handle);
            var parent = type.GetDeclaringType();
            return parent.IsNil ? Qualified(reader.GetString(type.Namespace), reader.GetString(type.Name)) :
                TypeName(reader, parent) + "+" + reader.GetString(type.Name);
        }
        if (handle.Kind == HandleKind.TypeReference) {
            var type = reader.GetTypeReference((TypeReferenceHandle)handle);
            return type.ResolutionScope.Kind == HandleKind.TypeReference ?
                TypeName(reader, type.ResolutionScope) + "+" + reader.GetString(type.Name) :
                Qualified(reader.GetString(type.Namespace), reader.GetString(type.Name));
        }
        return "";
    }

    private static string Qualified(string ns, string name) => ns.Length == 0 ? name : ns + "." + name;
}
