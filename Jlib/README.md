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

- Assembly: `Jlib`, version `0.1.0.0`; mod static ID: `LIghtJUNction.Jlib`.
- `Jlib.Runtime.JlibApi.AbiVersion` is the public integer constant `1`.
- `Jlib.Runtime.JlibApi.StaticId`, `Version`, and `PLibVersion` identify the provider.
- `Jlib.Runtime.JlibApi.RegisterConsumer(Assembly implementation, KMod.Mod owner)`
  associates a dynamically loaded implementation with its Mod before calling
  its `OnLoad`. PLib then resolves options, config, translations and image paths
  from the owning Mod root, including consumers registered after options initialization.
- Retained PLib component protocol: `4.25.0.0`.
- NuGet package: `LIghtJUNction.Jlib` version `0.1.0`, targets `net48` and `netstandard2.1`.

Use `PackageReference Include="LIghtJUNction.Jlib" Version="0.1.0" ExcludeAssets="runtime"`
or a project reference with `Private="false"`. Do not merge the shared assembly
or include `Jlib.dll` / `PLib*.dll` in a consumer release. A consumer that has
Jlib-dependent types must validate and load the enabled Jlib provider **before**
ONI enumerates those implementation types; a late `OnLoad` check is insufficient.
Workshop required items help users subscribe but do not enforce ONI enablement.

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

`JlibDistPath` optionally mirrors the Mod ZIP and directory for an external build
tool. When this project is checked out at `OniMods/libs/Jlib`, it detects the
parent's `onim.toml` and Mod project and mirrors to the parent's `dist/` for `onim`.

`preview.png`, when supplied next to this README, is copied into the Mod package.
Use an original 512×512 PNG; no upstream preview image is bundled automatically.
The two `description` files contain Steam Workshop descriptions.

Build/package verification does not prove Unity/Mono or real-game compatibility.
The initial shared-library migration still requires acceptance with the enabled
provider and consumers in a real game session.

## License

Inherited PLib source and original Jlib code are MIT-licensed. See `LICENSE` and
`THIRD-PARTY-NOTICES.md`. No game assemblies or inherited artwork are distributed
in the Jlib packages.
