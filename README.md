# Jlib

Jlib 是《缺氧》（Oxygen Not Included）的共享前置库，基于
[Peter Han 的 PLib](https://github.com/peterhaneve/ONIMods)，保留
`PeterHan.PLib.*` 命名空间。多个 Mod 共用一份启用的 `Jlib.dll`。

本仓库只包含 Jlib、它需要的 PLib 库模块、测试和打包配置。

## 玩家安装

安装并启用 Jlib 前置 Mod，将它排在依赖它的 Mod 前面，然后重启游戏。
本地安装时，将构建得到的 `artifacts/mod/Jlib/` 放入游戏用户目录的
`mods/Local/`。同一 static ID 只启用一份提供者。

**NuGet 包是给开发者使用的引用，不能代替游戏中的 Jlib 前置 Mod。**

## Mod 开发引用

NuGet 包：[LIghtJUNction.Jlib](https://www.nuget.org/packages/LIghtJUNction.Jlib)。
当前分支的源码版本为 `0.2.0`；已发布版本以 NuGet 包页为准。
下面使用已发布的 `0.1.0` 作为引用示例：

```xml
<PackageReference Include="LIghtJUNction.Jlib" Version="0.1.0" ExcludeAssets="runtime" />
```

直接引用源码时，将 `ProjectReference` 的 `Private` 设为 `false`。
不要把 `Jlib.dll` 或 `PLib*.dll` 合并、复制进消费者 Mod 的发行包。
消费者必须在枚举依赖 Jlib 的实现类型之前，确认游戏实际加载的提供者及其 ABI。

程序集名称为 `Jlib`，Mod static ID 为 `LIghtJUNction.Jlib`，PLib 组件协议为
`4.25.0.0`。完整 API、版本兼容规则和消费者引导方式见
[Jlib 开发指南](Jlib/README.md)。

## 从源码构建

需要 .NET 10 SDK（测试工程使用 `net10.0`）和本机合法安装的《缺氧》。
将 `OniManagedPath` 指向游戏的 `OxygenNotIncluded_Data/Managed` 目录：

```sh
dotnet build Jlib/Jlib.csproj -c Release -p:OniManagedPath="/path/to/OxygenNotIncluded_Data/Managed"
dotnet pack Jlib/Jlib.csproj -c Release --no-build -p:OniManagedPath="/path/to/OxygenNotIncluded_Data/Managed"
```

也可以通过环境变量 `ONI_MANAGED_PATH` 提供同一路径。
仓库不保存游戏引用 DLL，构建和打包也不会分发这些 DLL。

构建目标为 `net48` 和 `netstandard2.1`。游戏前置 Mod 使用 `net48`，输出为：

```text
artifacts/Jlib.zip          游戏 Mod 压缩包
artifacts/mod/Jlib/         解压后的游戏 Mod
artifacts/nuget/*.nupkg     开发引用包
```

## 本地验证

```sh
dotnet run --project Jlib.Tests/Jlib.Tests.csproj -c Release
# 构建后再检查两个目标程序集的元数据：
dotnet run --project Jlib.Tests/Jlib.Tests.csproj -c Release -- --inspect Jlib/bin/Release/net48/Jlib.dll Jlib/bin/Release/netstandard2.1/Jlib.dll
```

宿主测试不启动游戏，也不执行游戏 DLL。Unity/Mono、Harmony 补丁、存读档、UI 和
实际加载顺序仍需在真实游戏会话中验证。

## 目录

| 路径 | 内容 |
| --- | --- |
| `Jlib/` | 共享库入口、公开 API、消费者打包规则和 Mod 元数据 |
| `Jlib.Tests/` | 不依赖运行游戏的宿主测试 |
| `PLibCore/` | Jlib 使用的核心库和翻译 |
| `PLibActions/`、`PLibAVC/`、`PLibBuildings/` | 操作、版本检查和建筑 API |
| `PLibDatabase/`、`PLibLighting/`、`PLibOptions/`、`PLibUI/` | 数据库、光照、配置和 UI API |

这些 PLib 模块直接编译进 `Jlib.dll`，不是额外发行的 Mod。

## 许可证与来源

继承的 PLib 代码保留 Peter Han 的 MIT 版权和许可声明；Jlib 新增代码也使用 MIT。
详情见 [LICENSE](LICENSE)、[第三方声明](Jlib/THIRD-PARTY-NOTICES.md) 和
[上游来源声明](UPSTREAM.toml)。
