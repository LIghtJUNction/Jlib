using System.Reflection;
using System.Text;
using Jlib.Runtime;
using PeterHan.PLib.Core;

internal static partial class Program {
    private static void ResourceContract() {
        string fixture = Path.Combine(Path.GetTempPath(), "Jlib.Resources." + Guid.NewGuid().ToString("N"));
        string root = Path.Combine(fixture, "mod");
        string assets = Path.Combine(root, "assets");
        Directory.CreateDirectory(assets);
        try {
            Global.Instance = null;
            Assembly consumer = NewAssembly();
            Throws<InvalidOperationException>(() => ModResources.ResolvePath(consumer, "assets/help.txt"),
                "resource ownership required before late registration");
            PUtil.RegisterModAssembly(consumer, NewMod(root));
            string expected = Path.Combine(assets, "help.txt");
            Equal(expected, ModResources.ResolvePath(consumer, "assets/help.txt"), "late registration resolves owner root");
            Equal(expected, ModResources.ResolvePath(consumer, @"assets\help.txt"), "portable backslash separator");
            Equal(expected, ModResources.ResolvePath(consumer, "assets/../assets/./help.txt"), "normalized contained path");
            Check(!File.Exists(expected), "resolution does not create resources");
            Throws<FileNotFoundException>(() => ModResources.ReadAllBytes(consumer, "assets/help.txt"), "missing bytes propagate");
            Throws<FileNotFoundException>(() => ModResources.ReadAllText(consumer, "assets/help.txt"), "missing text propagates");

            File.WriteAllText(expected, "缺氧 resources", new UTF8Encoding(false));
            Equal("缺氧 resources", ModResources.ReadAllText(consumer, "assets/help.txt"), "UTF-8 text");
            Check(Encoding.UTF8.GetBytes("缺氧 resources").SequenceEqual(ModResources.ReadAllBytes(consumer, "assets/help.txt")),
                "exact byte contents");
            File.WriteAllText(expected, "changed", new UTF8Encoding(false));
            Equal("changed", ModResources.ReadAllText(consumer, "assets/help.txt"), "resource contents are not cached");
            File.WriteAllText(expected, "BOM 缺氧", Encoding.Unicode);
            Equal("BOM 缺氧", ModResources.ReadAllText(consumer, "assets/help.txt"), "default text detects UTF-16 BOM");
            Equal("BOM 缺氧", ModResources.ReadAllText(consumer, "assets/help.txt", Encoding.ASCII), "explicit encoding still detects BOM");
            File.WriteAllBytes(expected, [0xe9]);
            Equal("é", ModResources.ReadAllText(consumer, "assets/help.txt", Encoding.Latin1), "explicit encoding without BOM");
            Throws<DecoderFallbackException>(() => ModResources.ReadAllText(consumer, "assets/help.txt", new UTF8Encoding(false, true)),
                "decoder errors propagate");
            Throws<UnauthorizedAccessException>(() => ModResources.ReadAllBytes(consumer, "assets"), "directory read failure propagates");

            Throws<ArgumentNullException>(() => ModResources.ResolvePath(null!, "assets/help.txt"), "null consumer", "implementation");
            Throws<ArgumentNullException>(() => ModResources.ResolvePath(consumer, null!), "null resource path", "relativePath");
            Throws<ArgumentNullException>(() => ModResources.ReadAllText(consumer, "assets/help.txt", null!), "null encoding", "encoding");
            foreach (string invalid in new[] { "", " ", ".", "assets/..", "..", "../outside.txt", "../mod-other/outside.txt",
                    @"..\outside.txt", "assets/../../outside.txt", "/etc/passwd", @"\outside", @"\\server\share\file",
                    "C:/outside", @"C:\outside", "C:outside", "file:stream", "bad\0name", root })
                Throws<ArgumentException>(() => ModResources.ResolvePath(consumer, invalid), "reject resource path " + invalid, "relativePath");

            Assembly relativeOwner = NewAssembly();
            PUtil.RegisterModAssembly(relativeOwner, NewMod("relative/mod"));
            Throws<InvalidOperationException>(() => ModResources.ResolvePath(relativeOwner, "assets/help.txt"), "relative owner root rejected");
            Assembly emptyOwner = NewAssembly();
            PUtil.RegisterModAssembly(emptyOwner, NewMod(""));
            Throws<InvalidOperationException>(() => ModResources.ResolvePath(emptyOwner, "assets/help.txt"), "empty owner root rejected");
            Assembly nativeConsumer = NewAssembly();
            var nativeOwner = NewMod(root + Path.DirectorySeparatorChar);
            nativeOwner.loaded_mod_data = new KMod.LoadedModData { dlls = [nativeConsumer] };
            Global.Instance = new Global { modManager = new ModManager { mods = [nativeOwner] } };
            Equal(expected, ModResources.ResolvePath(nativeConsumer, "assets/help.txt"), "native owner with trailing separator");
            string alternateRoot = Path.Combine(fixture, "alternate");
            PUtil.RegisterModAssembly(nativeConsumer, NewMod(alternateRoot));
            Equal(Path.Combine(alternateRoot, "assets", "help.txt"), ModResources.ResolvePath(nativeConsumer, "assets/help.txt"),
                "explicit owner supersedes earlier native resource lookup");

            if (Path.DirectorySeparatorChar == '/') {
                string outside = Path.Combine(fixture, "outside.txt");
                File.WriteAllText(outside, "symlink target");
                File.CreateSymbolicLink(Path.Combine(assets, "linked.txt"), outside);
                Equal("symlink target", ModResources.ReadAllText(consumer, "assets/linked.txt"), "documented lexical-only symlink semantics");
                Throws<ArgumentException>(() => ModResources.ResolvePath(consumer, "../MOD/outside.txt"), "Unix containment is case-sensitive", "relativePath");
            }
        } finally {
            Global.Instance = null;
            Directory.Delete(fixture, true);
        }
    }
}
