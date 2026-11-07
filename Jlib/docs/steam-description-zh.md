[img]https://raw.githubusercontent.com/LIghtJUNction/Jlib/release/jlib-0.1.0/Jlib/preview.png[/img]

欢迎来到 Jlib！OniMods 其他发行模组背后的共享库。
有了这个模组，[b]OniMcp 和 CycleTrim 共用一份兼容 PLib 的提供者，不必各自再塞一份私有副本。[/b]

订阅一次，把它排在需要它的模组前面。殖民地规则不动。Jlib 不改变玩法。

[h2]当前阶段: v0.1.0[/h2]
[i]公开 ABI 是 1。保留的 PLib 组件协议是 4.25.0.0。[/i]
[i]static ID LIghtJUNction.Jlib 只启用一份。同一 ID 的第二份提供者会和第一份打架。[/i]

[h3]支持的 DLC:[/h3]
[img]https://raw.githubusercontent.com/Sgt-Imalas/Sgt_Imalas-Oni-Mods/refs/heads/master/Compat_All.png[/img]

[h3]MOD 拓展接口 (API)[/h3]
这是 [url=https://github.com/peterhaneve/ONIMods]Peter Han 的 PLib[/url] 的共享前置分支。PeterHan.PLib.* 源码 API 保留。继承代码继续带原来的 MIT 声明。
NuGet 包：[url=https://www.nuget.org/packages/LIghtJUNction.Jlib]LIghtJUNction.Jlib 0.1.0[/url]，目标框架是 net48 和 netstandard2.1。
引用时设置 ExcludeAssets="runtime"，或把项目引用的 Private 设为 false。不要把 Jlib.dll 或 PLib 程序集 ILRepack 进消费者的发行包。
在游戏枚举消费者实现类型之前调用 JlibApi.RegisterConsumer。等到 OnLoad 再检查就晚了。
选项页会列出当前正在共用这份提供者的模组。POptions.RefreshRegistered 可以接上首次初始化之后才注册的选项。

[h3]谁需要它[/h3]
[url=https://steamcommunity.com/sharedfiles/filedetails/?id=3807166922]ONI MCP Server[/url] 和 [url=https://steamcommunity.com/sharedfiles/filedetails/?id=3807166689]CycleTrim[/url] 都需要这个创意工坊条目。
OniOnline 自带 PLib，不要求 Jlib。
开发机上安装 NuGet 包不会给玩家订阅。游戏里仍然要启用这个模组。

[h3]操作指南与体验优化 (QoL)[/h3]
没有殖民地快捷键。安装顺序就是全部界面。

* 订阅并启用 Jlib。
* 把它拖到 OniMcp、CycleTrim 以及其他消费者前面。
* 重启游戏。列表里的所有消费者共用这一份已启用的提供者即可。
* 模组作者用 ModResources 从所属模组根目录取文件，包括 implementation/ 下面的文件。

[h3]由 onim 完成[/h3]
本模组来自 [url=https://github.com/LIghtJUNction/OniMods]OniMods[/url]。onim 是这个仓库里的工具。它把缺氧 Mod 的开发、测试和创意工坊发布连成一条直线。

onim dev 放进游戏里试。onim build 打包。onim publish 发到 Steam。

[h3]开源贡献[/h3]
非常欢迎社区贡献！
如果你有优化、Bug 修复或新功能建议，欢迎提交 Pull Request。

请确保你的提交条理清晰并在必要处附带说明文档。

项目地址：[url=https://github.com/LIghtJUNction/OniMods/tree/main/mods/Jlib]GitHub 仓库传送门[/url]

[h3]免责声明[/h3]
请注意本模组仍在紧密迭代中，我们将保持频繁更新与维护。
我们非常重视大家的反馈与建议！
Jlib 不调整复制人、建筑或模拟。如果某个消费者行为不对，请说明是哪一个消费者，以及加载的 Jlib 版本。

[h3]联系我们[/h3]
提问和缺陷请送到 [url=https://github.com/LIghtJUNction/OniMods/issues]GitHub Issues[/url]。

祝游戏愉快。一个库，很多模组，不额外带细菌。
[img]https://i.ibb.co/LXdJf9K9/I3P8A6f.gif[/img]
