using System.Reflection;
using Jlib.Runtime;

internal static class GameComponentReplacementCoverage {
    internal static void Run() {
        Program.Equal(GameComponentReplacement.SimDllMapName, "SimDLL", "SimDLL map name");

        var assembly = typeof(GameComponentReplacement).Assembly;
        Program.Throws<ArgumentNullException>(() => GameComponentReplacement.Replace(null!, "SimDLL", "/tmp/x"),
            "null managed assembly", "managedAssembly");
        Program.Throws<ArgumentNullException>(() => GameComponentReplacement.Replace(assembly, null!, "/tmp/x"),
            "null dll map name", "dllMapName");
        Program.Throws<ArgumentNullException>(() => GameComponentReplacement.Replace(assembly, "SimDLL", null!),
            "null replacement path", "absoluteReplacementPath");
        Program.Throws<ArgumentException>(() => GameComponentReplacement.Replace(assembly, "  ", "/tmp/x"),
            "blank dll map name", "dllMapName");

        var relative = GameComponentReplacement.Replace(assembly, "SimDLL", "relative/lib.so");
        Program.Equal(GameComponentReplacementState.Refused, relative.State, "relative path refused");
        Program.Check(!relative.Succeeded, "relative path is not success");

        string missing = Path.Combine(Path.GetTempPath(), "jlib-missing-native-" + Guid.NewGuid().ToString("N") + ".so");
        var absent = GameComponentReplacement.Replace(assembly, "SimDLL", missing);
        Program.Equal(GameComponentReplacementState.Refused, absent.State, "missing file refused");

        string present = Path.Combine(Path.GetTempPath(), "jlib-present-native-" + Guid.NewGuid().ToString("N") + ".so");
        File.WriteAllBytes(present, new byte[] { 0x7f, 0x45, 0x4c, 0x46 });
        try {
            var outsideMono = JlibApi.ReplaceSimDll(assembly, present);
            Program.Equal(GameComponentReplacementState.Refused, outsideMono.State,
                "host without Unity Mono refuses mapping");
            Program.Check(outsideMono.Message.IndexOf("Unity Mono", StringComparison.Ordinal) >= 0,
                "refusal mentions Unity Mono");
            Program.Check(!JlibApi.TryGetMappedNativeLibrary(assembly, GameComponentReplacement.SimDllMapName, out _),
                "refused mapping is not recorded");
        } finally {
            File.Delete(present);
        }

        Program.Throws<ArgumentNullException>(() => JlibApi.ReplaceNativeLibrary(null!, "SimDLL", "/tmp/x"),
            "JlibApi null assembly", "managedAssembly");
    }
}
