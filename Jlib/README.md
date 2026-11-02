# Jlib

Jlib is a standalone shared library mod for **Oxygen Not Included**, forked from
[Peter Han's PLib](https://github.com/peterhaneve/ONIMods). It keeps the
`PeterHan.PLib.*` source API while supplying one `Jlib.dll` to its consumers.

## Install / 安装

Install and enable the Jlib prerequisite mod, place it above dependent mods,
and restart the game. Installing the NuGet package is for developers; it does
not install the game mod. Jlib itself does not change colony gameplay.

订阅并启用 Jlib，将它排在依赖它的 Mod 前面，然后重启游戏。多个 Mod 共用一份
`Jlib.dll`。NuGet 包是开发引用，不能代替游戏中的前置 Mod。

## Consumer contract

- Assembly: `Jlib`, version `0.2.0.0`; mod static ID: `LIghtJUNction.Jlib`.
- `Jlib.Runtime.JlibApi.AbiVersion` is the public integer constant `1`.
  ABI must match exactly. New methods that existing callers can ignore stay on ABI 1;
  `Version` is how a consumer asks for those methods.
- `Jlib.Runtime.JlibApi.StaticId`, `Version` (`0.2.0`), and `PLibVersion` identify the provider.
- `CheckCompatibility(requiredAbi, minimumVersion)` reports a `JlibCompatibility` result.
  `RequireCompatibility` throws `JlibCompatibilityException` when that result is not compatible.
- `DiscoverProviders(requiredAbi, minimumVersion)` reflects over assemblies already loaded
  in the process and evaluates the single `Jlib.Runtime.JlibApi` it finds.
  These constants are compile-time values, so a consumer must not treat a copy of
  `JlibApi.Version` baked into its own assembly as proof of which provider the game loaded.
- `RegisterConsumer(Assembly implementation, KMod.Mod owner)` associates a dynamically
  loaded implementation with its Mod before calling its `OnLoad`. The same pair may be
  registered again. A second owner, a blank content path, the provider mod
  (`staticID` `LIghtJUNction.Jlib`), or the `Jlib` assembly itself is rejected.
  PLib then resolves options, config, translations and image paths from the owning Mod root,
  including consumers registered after options initialization.
  `PeterHan.PLib.Core.PUtil.RegisterModAssembly` keeps its previous behavior for existing
  PLib callers and does not apply these extra checks.
- Retained PLib component protocol: `4.25.0.0`.
- NuGet package: `LIghtJUNction.Jlib` version `0.2.0`, targets `net48` and `netstandard2.1`.

Use `PackageReference Include="LIghtJUNction.Jlib" Version="0.2.0" ExcludeAssets="runtime"`
or a project reference with `Private="false"`. Do not merge the shared assembly
or include `Jlib.dll` / `PLib*.dll` in a consumer release. A consumer that has
Jlib-dependent types must validate and load the enabled Jlib provider **before**
ONI enumerates those implementation types; a late `OnLoad` check is insufficient.
Workshop required items help users subscribe but do not enforce ONI enablement.

A bootstrap that cannot reference `Jlib.dll` can compile the game-free helper instead.
Set `JlibCompileContractIntoBootstrap` to `true` on that bootstrap project only
(the implementation project should reference Jlib and leave the property unset).
The packed file is `build/JlibContract.cs`. From a checkout of this repository the
same opt-in compiles `Jlib/Runtime/JlibContract.cs`.

```csharp
var found = Jlib.Runtime.JlibContract.Inspect(
    AppDomain.CurrentDomain.GetAssemblies(),
    requiredAbi: Jlib.Runtime.JlibContract.ExpectedAbi,
    minimumVersion: "0.2.0");
if (!found.IsCompatible)
    throw new InvalidOperationException(found.Message);
```

Once the shared provider is the assembly actually executing `JlibApi`, the implementation
project can call `JlibApi.RequireCompatibility(1, "0.2.0")` and then
`JlibApi.RegisterConsumer(implementation, owner)`.

订阅端在枚举自己的实现类型之前，要用已加载的 Jlib 提供者核对 ABI `1` 和最低库版本。
`0.2.0` 增加了上述检查，`RegisterConsumer` 的方法签名没有改变。

A game-free test host may explicitly set `JlibCopyLocalForHost=true` to copy
the development library to its output. This exception is only for host tests;
game Mod projects must keep the default shared-provider behavior.

## Build and package

Install a .NET SDK and use a legitimate local game installation. Set the path
to its `OxygenNotIncluded_Data/Managed` directory:

```sh
dotnet build Jlib/Jlib.csproj -c Release -p:OniManagedPath=/path/to/OxygenNotIncluded_Data/Managed
dotnet pack Jlib/Jlib.csproj -c Release --no-build -p:OniManagedPath=/path/to/OxygenNotIncluded_Data/Managed
```

`ONI_MANAGED_PATH` is an alternative to `OniManagedPath`. Outputs are
`artifacts/Jlib.zip`, `artifacts/mod/Jlib/` and `artifacts/nuget/`.
`JlibArtifactDir` can override that output root. Game references are compile-only
and never copied or packed. The standalone game package uses the `net48` build;
`netstandard2.1` is also available to development and host-test consumers.

The inherited `Lib/` folder is enough to compile `netstandard2.1` and inspect that
assembly. It is not enough for the shipping `net48` Mod ZIP: `Lib/` has no
`mscorlib.dll`, and a desktop .NET Framework `mscorlib` cannot replace the game's
copy because the UI code uses the game's `ReadOnlySpan`. Point `OniManagedPath`
at an installed `OxygenNotIncluded_Data/Managed` directory for `net48`, the Mod
ZIP, and the two-target NuGet package.

`JlibDistPath` optionally mirrors the Mod ZIP and directory for an external build
tool. When this project is checked out at `OniMods/libs/Jlib`, it detects the
parent's `onim.toml` and Mod project and mirrors to the parent's `dist/` for `onim`.

`preview.png`, when supplied next to this README, is copied into the Mod package.
Use an original 512×512 PNG; no upstream preview image is bundled automatically.
The two `description` files contain Steam Workshop descriptions.

Game-free contract checks, including the ownership helper and the packaging rules
that do not need a built DLL:

```sh
dotnet run --project Jlib.Tests -c Release
# or
Jlib/tools/test-contract.sh
```

After a local game build, the same host can inspect outputs:

```sh
Jlib/tools/test-contract.sh --inspect Jlib/bin/Release/net48/Jlib.dll Jlib/bin/Release/netstandard2.1/Jlib.dll
Jlib/tools/test-contract.sh --inspect-package artifacts/Jlib.zip
```

The default run prints `SKIP` for `artifacts/Jlib.zip` and `artifacts/nuget` when
they are absent, and inspects `Jlib/bin/Release/net48/Jlib.dll` and
`Jlib/bin/Release/netstandard2.1/Jlib.dll` when those outputs are present.

Build/package verification does not prove Unity/Mono or real-game compatibility.
Loading the enabled provider beside real consumers still requires a real game session.
That session is what exercises `JlibMod.OnLoad`, Harmony patches, options, UI,
buildings, lighting, database paths, and Workshop load order. The contract tests
do not start Oxygen Not Included and do not execute game assemblies.

## License

Inherited PLib source and original Jlib code are MIT-licensed. See `LICENSE` and
`THIRD-PARTY-NOTICES.md`. No game assemblies or inherited artwork are distributed
in the Jlib packages. Git source archives also exclude the inherited `Lib/*.dll`
reference assemblies; build Jlib against your own installed game.
