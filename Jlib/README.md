# Jlib

Jlib is a standalone shared library mod for **Oxygen Not Included**, forked from
[Peter Han's PLib](https://github.com/peterhaneve/ONIMods). It keeps the
`PeterHan.PLib.*` source API while supplying one `Jlib.dll` to its consumers.

Jlib ships with [OniMods](https://github.com/LIghtJUNction/OniMods). onim is the toolchain in that repo. It takes an Oxygen Not Included mod straight through development, testing, and Workshop publishing: `onim dev` tries it in game, `onim build` packs it, and `onim publish` sends it to Steam.

## Install / 安装

Install and enable the Jlib prerequisite mod, place it above dependent mods,
and restart the game. Installing the NuGet package is for developers; it does
not install the game mod. Jlib itself does not change colony gameplay.

订阅并启用 Jlib，将它排在依赖它的 Mod 前面，然后重启游戏。多个 Mod 共用一份
`Jlib.dll`。NuGet 包是开发引用，不能代替游戏中的前置 Mod。

## Consumer contract

- Assembly: `Jlib`, version `0.3.1.0`; mod static ID: `LIghtJUNction.Jlib`.
- `Jlib.Runtime.JlibApi.AbiVersion` is the public integer constant `1`.
  ABI must match exactly. New methods that existing callers can ignore stay on ABI 1;
  `Version` is how a consumer asks for those methods.
- `Jlib.Runtime.JlibApi.StaticId`, `Version` (`0.3.1`), and `PLibVersion` identify the provider.
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
- NuGet package: `LIghtJUNction.Jlib` version `0.3.1`, targets `net48` and `netstandard2.1`.
- `ReplaceNativeLibrary` / `ReplaceSimDll` redirect a Mono P/Invoke DLL name (for example
  `SimDLL`) for one loaded managed assembly's MonoImage to an absolute native library path.
  Call only in the Unity Mono game process before `Game.Instance` exists. The mapping cannot
  be undone without a restart. Official ABI checks of the replacement binary stay with the
  caller. Prefer `minimumVersion: "0.3.0"` when you need these methods.

Use `PackageReference Include="LIghtJUNction.Jlib" Version="0.3.1" ExcludeAssets="runtime"`
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
    minimumVersion: "0.3.0");
if (!found.IsCompatible)
    throw new InvalidOperationException(found.Message);
```

Once the shared provider is the assembly actually executing `JlibApi`, the implementation
project can call `JlibApi.RequireCompatibility(1, "0.3.0")` and then
`JlibApi.RegisterConsumer(implementation, owner)`.

```csharp
// Early OnLoad / bootstrap, Unity Mono only, before Game.Instance exists:
var mapped = JlibApi.ReplaceSimDll(
    typeof(Sim).Assembly,
    absoluteReplacementPath: Path.GetFullPath(Path.Combine(modRoot, "linux-x64", "liboni_sim_core.so")));
if (!mapped.Succeeded)
    throw new InvalidOperationException(mapped.Message);
```

订阅端在枚举自己的实现类型之前，要用已加载的 Jlib 提供者核对 ABI `1` 和最低库版本。
`0.3.0` 增加了原生组件替换接口，ABI 仍为 `1`；`RegisterConsumer` 的方法签名没有改变。

A game-free test host may explicitly set `JlibCopyLocalForHost=true` to copy
the development library to its output. This exception is only for host tests;
game Mod projects must keep the default shared-provider behavior.

The source-tree version and the latest published NuGet version can differ. Check
[NuGet](https://www.nuget.org/packages/LIghtJUNction.Jlib) before selecting a
package version; new APIs described above require a provider built from this
source version or a matching published release.

## Managed Unity assets (source version)

`Jlib.Runtime.ModAssets` uses the same explicit consumer assembly and Mod-relative
path rules as `ModResources`. Call all methods from the Unity main thread after
Jlib initializes. APIs are available in this source version; they are not in the
published package until a matching Jlib provider and package are released.

```csharp
using Jlib.Runtime;
using UnityEngine;

var owner = typeof(MyMod).Assembly;
Sprite icon = ModAssets.LoadSprite(owner, "assets/icon.png");
Material material = ModAssets.LoadBundleAsset<Material>(owner, "assets/my.bundle", "MyMaterial");
ModAssets.LoadAudio(owner, "assets/sound.ogg", AudioType.OGGVORBIS, (clip, error) => {
    if (error != null) { Debug.LogError(error); return; }
    ModAssets.PlayOneShot(myAudioSource, clip);
});
// During Mod initialization, before the game's Assets.LoadAnims stage:
ModAssets.RegisterKAnim(owner, "my_machine_kanim", "custom/machine_anim.bytes",
    "custom/machine_build.bytes", "custom/machine_0.png");
// After stopping playback and releasing all user references:
ModAssets.Release(owner);
```

Sprites, audio clips and bundles are cached by owning Mod and resolved path;
audio also distinguishes `AudioType`. Concurrent audio requests can decode
more than once but share the resulting cached clip. The callback runs on the
main thread, including cache hits, and receives an exception on failure or if
`Release` cancels an in-flight request. `PlayOneShot` uses Unity `AudioSource`,
not the game's FMOD event system. Bundles must match the game's Unity version
and target platform. `Release` destroys cached sprites, their textures and clips,
and unloads bundles with `Unload(true)`; users must stop using all references
first. KAnim assets belong to the game's animation system and are not released
by this method.

KAnim requests are queued before animation loading and registered during the
game's normal animation load, before the animation table is finalized. This
supports arbitrary Mod-relative input paths, not hot registration after startup.
Names must be globally unique and end in `_kanim`. Errors encountered during
registration are reported to `Player.log`; check the log and game behavior.
Standard `anim/<category>/<name>/` content is already loaded by the game and
should not also be registered through this API. The caller supplies texture paths
in the order required by its exported animation.

## Consumer resources & options

`Jlib.Runtime.ModResources` resolves files from the owning Mod's `ContentPath`,
including implementations under `implementation/`. Pass the consumer assembly
explicitly; the bootstrap must call `JlibApi.RegisterConsumer` before use. Assemblies
in ONI's native loaded-Mod list also work. Missing ownership throws
`InvalidOperationException` rather than guessing from an assembly location. Each
call looks up ownership again, so an earlier failure does not hide late registration.

```csharp
using System.Text;
using Jlib.Runtime;

var consumer = typeof(MyMod).Assembly;
string path = ModResources.ResolvePath(consumer, "assets/icon.png");
byte[] image = ModResources.ReadAllBytes(consumer, "assets/icon.png");
string text = ModResources.ReadAllText(consumer, "assets/help.txt");
string legacy = ModResources.ReadAllText(consumer, "assets/legacy.txt", Encoding.Unicode);
```

Both `/` and `\\` are accepted as separators. Empty paths, absolute paths,
drive-relative paths, colon-containing paths, and normalized paths outside the Mod
root or equal to the root are rejected with `ArgumentException`. The owning Mod
must have an absolute content path. `ResolvePath` does not require a file to exist;
reads propagate normal file system exceptions, including missing files and access
failures. Text reads use UTF-8 by default or the supplied `Encoding`, with BOM
detection in either case, matching `File.ReadAllText`.

Containment checks compare normalized path strings. Symbolic links and Windows
junctions can lead outside the Mod directory; this API is for trusted packaged
resources and does not provide a file system security boundary. It neither caches
file contents nor chooses configuration or writable-data locations.

`PeterHan.PLib.Core.PUtil.DependentMods()` returns the sorted list of consumer mods
registered through `JlibApi.RegisterConsumer` or referencing `Jlib.dll` (excluding
Jlib itself), and `PeterHan.PLib.Options.POptions.RefreshRegistered()` refreshes the
Mods screen options table for late-registered consumer assemblies.

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

Game and Unity reference assemblies are not stored in this repository. Both target
frameworks use your installed game's `OxygenNotIncluded_Data/Managed` directory.
A desktop .NET Framework `mscorlib` is not a substitute for the game's copy:
Jlib's UI code uses the game's `ReadOnlySpan`.

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
in the Jlib packages or stored in this repository. Git source archives exclude
compiled assemblies; build Jlib against your own installed game.
