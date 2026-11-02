using System.IO.Compression;

internal static class SourceContract {
    private static readonly string[] ModPackageFiles = {
        "mod.yaml", "mod_info.yaml", "README.md", "THIRD-PARTY-NOTICES.md",
        "description.txt", "description.en.txt", "LICENSE", "preview.png"
    };

    private static readonly string[] ForbiddenAssemblies = {
        "Assembly-CSharp.dll", "Assembly-CSharp-firstpass.dll", "0Harmony.dll", "UnityEngine.dll",
        "UnityEngine.CoreModule.dll", "FMODUnity.dll", "Newtonsoft.Json.dll", "mscorlib.dll",
        "System.IO.Compression.dll", "System.Net.Http.dll"
    };

    internal static void Run(string[] args) {
        var root = RepoRoot();
        PackagingSources(root);
        Documents(root);
        var sawInspect = false;
        for (int i = 0; i < args.Length; i++) {
            if (args[i] == "--inspect") {
                int count = 0;
                while (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal)) {
                    Program.InspectLibrary(args[++i]);
                    count++;
                }
                if (count == 0)
                    throw new ArgumentException("Usage: --inspect <Jlib.dll> [more.dll]");
                sawInspect = true;
            } else if (args[i] == "--inspect-package") {
                if (i + 1 >= args.Length)
                    throw new ArgumentException("Usage: --inspect-package <Jlib.zip>");
                InspectPackage(args[++i]);
            } else {
                throw new ArgumentException("Usage: dotnet run --project Jlib.Tests -- [--inspect <Jlib.dll> [more.dll]] [--inspect-package <Jlib.zip>]");
            }
        }
        if (!sawInspect)
            InspectBuiltArtifacts(root);
    }

    private static void PackagingSources(string root) {
        var version = Jlib.Runtime.JlibApi.Version;
        var project = File.ReadAllText(Path.Combine(root, "Jlib", "Jlib.csproj"));
        Program.Check(project.Contains("<Version>" + version + "</Version>", StringComparison.Ordinal), "csproj Version");
        Program.Check(project.Contains("<AssemblyVersion>" + version + ".0</AssemblyVersion>", StringComparison.Ordinal), "csproj AssemblyVersion");
        Program.Check(project.Contains("<FileVersion>" + version + ".0</FileVersion>", StringComparison.Ordinal), "csproj FileVersion");
        Program.Check(project.Contains("<PackageId>LIghtJUNction.Jlib</PackageId>", StringComparison.Ordinal), "package id");
        Program.Check(project.Contains("<IncludeSourceRevisionInInformationalVersion>false</IncludeSourceRevisionInInformationalVersion>", StringComparison.Ordinal), "stable informational version");
        Program.Check(project.Contains("Exclude=\"../PLibCore/obj/**;../PLibCore/bin/**;../PLibCore/Properties/AssemblyInfo.cs\"", StringComparison.Ordinal), "inherited file version is not compiled");
        Program.Check(project.Contains("PackagePath=\"build/LIghtJUNction.Jlib.targets\"", StringComparison.Ordinal), "nuget targets");
        Program.Check(project.Contains("PackagePath=\"build/JlibContract.cs\"", StringComparison.Ordinal), "nuget contract source");
        Program.Check(project.Contains("PackagePath=\"buildTransitive/LIghtJUNction.Jlib.targets\"", StringComparison.Ordinal), "transitive targets");
        Program.Check(!project.Contains("Private=\"true\"", StringComparison.Ordinal), "game references stay compile-only");
        foreach (var name in ModPackageFiles)
            Program.Check(project.Contains(name, StringComparison.Ordinal), "mod package lists " + name);
        Program.Check(project.Contains("ZipDirectory", StringComparison.Ordinal) && project.Contains("Jlib.zip", StringComparison.Ordinal), "mod zip");

        var targets = File.ReadAllText(Path.Combine(root, "Jlib", "consumer", "Jlib.targets"));
        Program.Check(targets.Contains("JlibCopyLocalForHost", StringComparison.Ordinal), "host copy switch");
        Program.Check(targets.Contains("'Jlib.dll'", StringComparison.Ordinal), "copy-local removal names Jlib.dll");
        Program.Check(targets.Contains("JlibCompileContractIntoBootstrap", StringComparison.Ordinal), "bootstrap opt-in");
        Program.Check(targets.Contains("buildTransitive", StringComparison.Ordinal), "transitive import does not compile the contract twice");

        var info = File.ReadAllLines(Path.Combine(root, "Jlib", "mod_info.yaml"));
        Program.Check(info.Any(line => line.Trim() == "version: \"" + version + "\""), "mod_info version");
        Program.Check(info.Any(line => line.Trim() == "APIVersion: 2"), "mod_info API version");
        var mod = File.ReadAllLines(Path.Combine(root, "Jlib", "mod.yaml"));
        Program.Check(mod.Any(line => line.Trim() == "staticID: " + Jlib.Runtime.JlibApi.StaticId), "mod.yaml static ID");

        var protocol = File.ReadAllText(Path.Combine(root, "Jlib", "PVersion.cs"));
        Program.Check(protocol.Contains("\"" + Jlib.Runtime.JlibApi.PLibVersion + "\"", StringComparison.Ordinal), "PVersion protocol constant");
        Program.Equal(Jlib.Runtime.JlibContract.ExpectedAbi, Jlib.Runtime.JlibApi.AbiVersion, "ABI re-export");
        Program.Equal(Jlib.Runtime.JlibContract.ExpectedStaticId, Jlib.Runtime.JlibApi.StaticId, "static ID re-export");
        Program.Equal(Jlib.Runtime.JlibContract.ExpectedPLibVersion, Jlib.Runtime.JlibApi.PLibVersion, "protocol re-export");

        var attributes = File.ReadAllText(Path.Combine(root, ".gitattributes"));
        Program.Check(attributes.Contains("/Lib/*.dll export-ignore", StringComparison.Ordinal), "source archives omit inherited reference DLLs");
        var notices = File.ReadAllText(Path.Combine(root, "Jlib", "THIRD-PARTY-NOTICES.md"));
        var upstream = File.ReadAllText(Path.Combine(root, "UPSTREAM.toml"));
        var revision = upstream.Split('\n').Select(line => line.Trim()).First(line => line.StartsWith("upstream_revision", StringComparison.Ordinal));
        var hash = revision.Split('=', 2)[1].Trim().Trim('"');
        Program.Check(hash.Length == 40, "upstream revision length");
        Program.Check(notices.Contains(hash, StringComparison.Ordinal), "notices cite the upstream revision");
        Program.Check(notices.Contains("MIT", StringComparison.Ordinal), "notices keep the MIT statement");

        var png = File.ReadAllBytes(Path.Combine(root, "Jlib", "preview.png"));
        Program.Check(png.Length > 8 && png[0] == 0x89 && png[1] == 0x50 && png[2] == 0x4E && png[3] == 0x47, "preview is a PNG");
        int width = ReadBigEndian(png, 16);
        int height = ReadBigEndian(png, 20);
        Program.Equal(512, width, "preview width");
        Program.Equal(512, height, "preview height");
    }

    private static void Documents(string root) {
        var readme = File.ReadAllText(Path.Combine(root, "Jlib", "README.md"));
        foreach (var token in new[] {
            Jlib.Runtime.JlibApi.Version, "CheckCompatibility", "RequireCompatibility", "DiscoverProviders",
            "RegisterConsumer", "JlibContract.Inspect", "JlibCompileContractIntoBootstrap", "Jlib.Tests",
            "real game session"
        })
            Program.Check(readme.Contains(token, StringComparison.Ordinal), "README mentions " + token);
        foreach (var description in new[] { "description.txt", "description.en.txt" }) {
            var text = File.ReadAllText(Path.Combine(root, "Jlib", description));
            Program.Check(text.Contains(Jlib.Runtime.JlibApi.StaticId, StringComparison.Ordinal), description + " names the static ID");
            Program.Check(text.Contains("Jlib.dll", StringComparison.Ordinal), description + " names the shared assembly");
            Program.Check(text.Contains("ABI", StringComparison.Ordinal), description + " mentions the ABI check");
        }
        var script = File.ReadAllText(Path.Combine(root, "Jlib", "tools", "test-contract.sh"));
        Program.Check(script.Contains("dotnet run --project Jlib.Tests/Jlib.Tests.csproj", StringComparison.Ordinal), "contract script runs the host");
    }

    private static void InspectBuiltArtifacts(string root) {
        var inspected = 0;
        foreach (var framework in new[] { "net48", "netstandard2.1" }) {
            var dll = Path.Combine(root, "Jlib", "bin", "Release", framework, "Jlib.dll");
            if (!File.Exists(dll))
                continue;
            Program.InspectLibrary(dll);
            inspected++;
        }
        if (inspected == 0)
            Console.WriteLine("SKIP: Jlib/bin/Release/*/Jlib.dll is absent. netstandard2.1 can build against the inherited Lib references; the net48 Mod build needs the game's mscorlib.");
        var zip = Path.Combine(root, "artifacts", "Jlib.zip");
        if (File.Exists(zip))
            InspectPackage(zip);
        else
            Console.WriteLine("SKIP: artifacts/Jlib.zip is absent. The Mod ZIP is the net48 build, and this checkout's Lib folder has no mscorlib.dll. A desktop .NET Framework mscorlib is not a substitute: Jlib's UI code needs the game's ReadOnlySpan.");
        var nuget = Path.Combine(root, "artifacts", "nuget");
        if (Directory.Exists(nuget)) {
            var packages = Directory.GetFiles(nuget, "*.nupkg");
            if (packages.Length == 0)
                Console.WriteLine("SKIP: artifacts/nuget contains no nupkg.");
            foreach (var package in packages)
                InspectNupkg(package);
        } else {
            Console.WriteLine("SKIP: artifacts/nuget is absent. dotnet pack of the shipping package needs the net48 game-managed directory as well as netstandard2.1.");
        }
    }

    internal static void InspectPackage(string path) {
        using var zip = ZipFile.OpenRead(path);
        var names = zip.Entries.Select(entry => entry.FullName.Replace('\\', '/')).ToList();
        foreach (var required in ModPackageFiles.Append("Jlib.dll"))
            Program.Check(names.Contains(required), path + " contains " + required);
        foreach (var entry in names) {
            var file = Path.GetFileName(entry);
            if (file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                Program.Equal("Jlib.dll", file, path + " assembly entry " + entry);
            Program.Check(!ForbiddenAssemblies.Contains(file, StringComparer.OrdinalIgnoreCase), path + " does not pack " + file);
        }
        var mod = OpenText(zip, "mod.yaml");
        Program.Check(mod.Contains("staticID: " + Jlib.Runtime.JlibApi.StaticId, StringComparison.Ordinal), path + " static ID");
        var info = OpenText(zip, "mod_info.yaml");
        Program.Check(info.Contains("version: \"" + Jlib.Runtime.JlibApi.Version + "\"", StringComparison.Ordinal), path + " mod version");
        var dll = zip.GetEntry("Jlib.dll") ?? throw new InvalidOperationException(path + " is missing Jlib.dll");
        var temp = Path.Combine(Path.GetTempPath(), "jlib-inspect-" + Guid.NewGuid().ToString("n") + ".dll");
        dll.ExtractToFile(temp);
        try {
            Program.InspectLibrary(temp);
        } finally {
            File.Delete(temp);
        }
        Console.WriteLine("Inspected package: " + Path.GetFullPath(path));
    }

    private static void InspectNupkg(string path) {
        using var zip = ZipFile.OpenRead(path);
        var names = zip.Entries.Select(entry => entry.FullName.Replace('\\', '/')).ToList();
        foreach (var required in new[] {
            "lib/net48/Jlib.dll", "lib/netstandard2.1/Jlib.dll", "README.md", "THIRD-PARTY-NOTICES.md",
            "build/LIghtJUNction.Jlib.targets", "build/JlibContract.cs", "buildTransitive/LIghtJUNction.Jlib.targets"
        })
            Program.Check(names.Contains(required), path + " contains " + required);
        foreach (var entry in names) {
            var file = Path.GetFileName(entry);
            Program.Check(!ForbiddenAssemblies.Contains(file, StringComparer.OrdinalIgnoreCase), path + " does not pack " + file);
            if (file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                Program.Equal("Jlib.dll", file, path + " assembly entry " + entry);
        }
        Console.WriteLine("Inspected package: " + Path.GetFullPath(path));
    }

    private static string OpenText(ZipArchive zip, string name) {
        var entry = zip.GetEntry(name) ?? throw new InvalidOperationException("Missing " + name);
        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static int ReadBigEndian(byte[] data, int offset) =>
        (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];

    private static string RepoRoot() {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() }) {
            var dir = new DirectoryInfo(start);
            while (dir != null) {
                if (File.Exists(Path.Combine(dir.FullName, "Jlib", "JlibApi.cs")) &&
                        File.Exists(Path.Combine(dir.FullName, "Jlib.Tests", "Jlib.Tests.csproj")))
                    return dir.FullName;
                dir = dir.Parent;
            }
        }
        throw new InvalidOperationException("Repository root not found from " + AppContext.BaseDirectory);
    }
}
