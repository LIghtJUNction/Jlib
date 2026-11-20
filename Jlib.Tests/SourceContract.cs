using System.Buffers.Binary;
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
        PreviewPngContractCoverage();
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
            } else if (args[i] == "--property-texture-managed") {
                if (i + 1 >= args.Length) throw new ArgumentException("--property-texture-managed <Managed directory>");
                PropertyTextureReferenceCoverage.Run(args[++i]);
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
        Program.Check(attributes.Contains("*.dll export-ignore", StringComparison.Ordinal), "source archives omit compiled DLLs");
        var notices = File.ReadAllText(Path.Combine(root, "Jlib", "THIRD-PARTY-NOTICES.md"));
        var upstream = File.ReadAllText(Path.Combine(root, "UPSTREAM.toml"));
        var revision = upstream.Split('\n').Select(line => line.Trim()).First(line => line.StartsWith("upstream_revision", StringComparison.Ordinal));
        var hash = revision.Split('=', 2)[1].Trim().Trim('"');
        Program.Check(hash.Length == 40, "upstream revision length");
        Program.Check(notices.Contains(hash, StringComparison.Ordinal), "notices cite the upstream revision");
        Program.Check(notices.Contains("MIT", StringComparison.Ordinal), "notices keep the MIT statement");

        using var png = File.OpenRead(Path.Combine(root, "Jlib", "preview.png"));
        var header = new byte[PngHeaderBytes];
        int headerLength = png.ReadAtLeast(header, PngHeaderBytes, throwOnEndOfStream: false);
        ValidatePreviewPngHeader(header.AsSpan(0, headerLength), png.Length);
        Program.Check(true, "preview meets the square PNG header and project byte-limit contract");
    }


    // This is the repository's Legacy publisher limit, not a universal Steam limit.
    private const long MaxPreviewBytes = 10 * 1024 * 1024;
    private const int PngHeaderBytes = 33;

    private static void ValidatePreviewPngHeader(ReadOnlySpan<byte> header, long fileLength) {
        if (fileLength <= 0 || fileLength > MaxPreviewBytes)
            throw new InvalidDataException("Preview must be nonempty and at most 10 MiB.");
        if (header.Length < PngHeaderBytes || fileLength < PngHeaderBytes)
            throw new InvalidDataException("Preview has a truncated PNG signature or IHDR chunk.");
        ReadOnlySpan<byte> signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (!header[..8].SequenceEqual(signature))
            throw new InvalidDataException("Preview must have the complete PNG signature.");
        if (BinaryPrimitives.ReadInt32BigEndian(header.Slice(8, 4)) != 13 ||
                !header.Slice(12, 4).SequenceEqual("IHDR"u8))
            throw new InvalidDataException("Preview must start with a 13-byte IHDR chunk.");
        int width = BinaryPrimitives.ReadInt32BigEndian(header.Slice(16, 4));
        int height = BinaryPrimitives.ReadInt32BigEndian(header.Slice(20, 4));
        if (width <= 0 || height <= 0 || width != height)
            throw new InvalidDataException("Preview dimensions must be positive and square.");
    }

    private static void PreviewPngContractCoverage() {
        foreach (int size in new[] { 1, 512, 1254, int.MaxValue }) {
            var header = PreviewHeader(size, size);
            ValidatePreviewPngHeader(header, PngHeaderBytes);
            Program.Check(true, "positive square PNG header accepted without a fixed pixel size: " + size);
        }

        var valid = PreviewHeader(1254, 1254);
        ValidatePreviewPngHeader(valid, MaxPreviewBytes);
        Program.Check(true, "preview accepts the project byte limit without allocating that file size");
        Program.Throws<InvalidDataException>(
            () => ValidatePreviewPngHeader(valid, MaxPreviewBytes + 1), "preview rejects one byte over the project limit");
        foreach (long length in new long[] { -1, 0, PngHeaderBytes - 1 })
            Program.Throws<InvalidDataException>(
                () => ValidatePreviewPngHeader(valid, length), "preview rejects invalid file length: " + length);

        foreach (int length in new[] { 0, 7, 8, 15, 16, 23, 24, 28, 32 })
            Program.Throws<InvalidDataException>(
                () => ValidatePreviewPngHeader(valid.AsSpan(0, length), PngHeaderBytes),
                "preview rejects truncated PNG header: " + length);
        for (int i = 0; i < 8; i++) {
            var corrupted = (byte[])valid.Clone();
            corrupted[i] ^= 1;
            Program.Throws<InvalidDataException>(
                () => ValidatePreviewPngHeader(corrupted, corrupted.Length), "preview checks signature byte: " + i);
        }

        var wrongType = (byte[])valid.Clone();
        wrongType[12] = (byte)'X';
        Program.Throws<InvalidDataException>(
            () => ValidatePreviewPngHeader(wrongType, wrongType.Length), "preview rejects a first chunk other than IHDR");
        foreach (int length in new[] { 0, 12, 14 }) {
            var wrongLength = (byte[])valid.Clone();
            BinaryPrimitives.WriteInt32BigEndian(wrongLength.AsSpan(8, 4), length);
            Program.Throws<InvalidDataException>(
                () => ValidatePreviewPngHeader(wrongLength, wrongLength.Length), "preview rejects invalid IHDR length: " + length);
        }
        foreach (var dimensions in new[] { (0, 1), (1, 0), (int.MinValue, 1), (1, int.MinValue), (512, 1254), (1254, 512) }) {
            var invalid = PreviewHeader(dimensions.Item1, dimensions.Item2);
            Program.Throws<InvalidDataException>(
                () => ValidatePreviewPngHeader(invalid, invalid.Length), "preview rejects invalid dimensions: " + dimensions);
        }
    }

    // Header fixtures exercise the contract only; this is not a PNG pixel decoder.
    private static byte[] PreviewHeader(int width, int height) {
        var header = new byte[PngHeaderBytes];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(header, 0);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(8, 4), 13);
        "IHDR"u8.CopyTo(header.AsSpan(12, 4));
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(16, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(20, 4), height);
        header[24] = 8;
        header[25] = 6;
        return header;
    }

    private static void Documents(string root) {
        var readme = File.ReadAllText(Path.Combine(root, "Jlib", "README.md"));
        foreach (var token in new[] {
            Jlib.Runtime.JlibApi.Version, "CheckCompatibility", "RequireCompatibility", "DiscoverProviders",
            "RegisterConsumer", "ReplaceNativeLibrary", "ReplaceSimDll", "JlibContract.Inspect",
            "JlibCompileContractIntoBootstrap", "Jlib.Tests", "real game session"
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
            Console.WriteLine("SKIP: Jlib/bin/Release/*/Jlib.dll is absent. Build both targets against your installed game's Managed directory before inspecting assemblies.");
        var zip = Path.Combine(root, "artifacts", "Jlib.zip");
        if (File.Exists(zip))
            InspectPackage(zip);
        else
            Console.WriteLine("SKIP: artifacts/Jlib.zip is absent. Build the net48 target against your installed game's Managed directory to create the Mod ZIP.");
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
