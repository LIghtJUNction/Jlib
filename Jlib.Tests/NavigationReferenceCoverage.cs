using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

internal static class NavigationReferenceCoverage
{
    internal static void Run(string directory)
    {
        var required = new HashSet<string> {
            "NavGrid.field.DirtyCells", "NavGrid.field.DirtyBitFlags", "NavGrid.field.dirtyCellsSwapBuffer",
            "NavTable.field.ValidCells", "NavGrid.method.UpdateGraph/0",
            "NavGridUpdater.method.UpdateLinks/6", "NavGridUpdater.method.CreateLinks/6",
            "JobManager.method.Run/1", "GlobalJobManager.method.Run/1"
        };
        foreach (string file in new[] { "Assembly-CSharp.dll", "Assembly-CSharp-firstpass.dll" })
        {
            using var stream = File.OpenRead(Path.Combine(directory, file));
            using var pe = new PEReader(stream);
            var reader = pe.GetMetadataReader();
            foreach (var handle in reader.TypeDefinitions)
            {
                var type = reader.GetTypeDefinition(handle);
                string name = reader.GetString(type.Name);
                if (name != "NavGrid" && name != "NavTable" && name != "NavGridUpdater"
                    && name != "JobManager" && name != "GlobalJobManager") continue;
                foreach (var fieldHandle in type.GetFields())
                {
                    var field = reader.GetFieldDefinition(fieldHandle);
                    required.Remove(name + ".field." + reader.GetString(field.Name));
                }
                foreach (var methodHandle in type.GetMethods())
                {
                    var method = reader.GetMethodDefinition(methodHandle);
                    var signature = reader.GetBlobReader(method.Signature);
                    var header = signature.ReadSignatureHeader();
                    if (header.IsGeneric) signature.ReadCompressedInteger();
                    int count = signature.ReadCompressedInteger();
                    required.Remove(name + ".method." + reader.GetString(method.Name) + "/" + count);
                }
            }
        }
        if (required.Count != 0)
            throw new InvalidOperationException("Missing installed navigation API: " + string.Join(", ", required));
        Console.WriteLine("PASS installed navigation metadata contract; game assemblies were inspected as data");
    }
}
