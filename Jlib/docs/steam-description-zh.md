[img]https://media.githubusercontent.com/media/LIghtJUNction/OniMods/main/.agents/skills/oni-workshop-description/assets/ai-generated-congratulations.png[/img]
[h1]恭喜！[/h1]
[b]这个 Mod 完全由 AI 生成！[/b]
该声明指 Jlib 项目的 AI 开发方式。[b]继承的 Peter Han / PLib 代码及第三方素材属于原作者贡献，继续适用原作者署名与许可。[/b]

[img]https://raw.githubusercontent.com/LIghtJUNction/Jlib/main/Jlib/preview.png[/img]

[h1]Jlib · 多个 Mod 共用的一份前置库[/h1]
Jlib 为《缺氧》Mod 提供共享的配置、界面、按键和运行时接口。玩家安装一份，依赖它的 Mod 使用同一个 Jlib 提供者。

[b]本页对应源码版本 0.3.10；公共 ABI 为 1。[/b] 实际下载包请在游戏 Mod 菜单核对。Jlib 提供共享接口与兼容性校验；帧率收益需在具体游戏场景测量。

[h2]怎样安装[/h2]
[list]
[*]订阅并在 Mods 菜单启用 Jlib。
[*]把它排在依赖它的 Mod 前面，然后重启游戏。
[*]同一 static ID 只启用一份提供者，不要同时启用本地与工坊副本。
[*]在 Jlib 选项界面查看依赖它的 Mod；版本报错时核对实际加载的 DLL。
[/list]
NuGet 包供开发者引用，玩家仍需安装游戏里的前置 Mod。工坊、源码和 NuGet 版本可能不同。

[h2]0.3.10 这一轮解决什么[/h2]
[list]
[*]多组分数据明确区分“零”和“未知”。压力、分压、体积分数各有可用性字段；消费者按可用性标记读取这些字段。
[*]液体占格数据不可用时，相关 Vent 判断回到原版路径。当前 OniSim 压力与分压仍标为不可用。
[*]支持受审查的 U60 纹理回调布局；通过读取 IL 定位目标，不触发类型静态初始化。未知布局仅跳过对应补丁并给出诊断。
[*]主线程派发一帧至多取 128 个回调，在回调之间检查约 4 ms 的预算；单个回调仍可能超时。整帧耗时仍需实测。
[/list]

[h2]验证到哪里[/h2]
本轮独立宿主通过 2,379 个断言，net48 与 netstandard2.1 构建及元数据检查通过。宿主不运行游戏 DLL；真实游戏、其他 Mod 组合和长期存读档仍需分别验收。

[h2]给 Mod 作者[/h2]
保留 PeterHan.PLib.* 命名空间与 PLib 4.25.0.0 组件协议。引用共享提供者时不要把 Jlib.dll / PLib*.dll 再打进发行包；必须在游戏枚举实现类型前检查已加载提供者的 ABI 和最低版本。

0.3.10 可用性字段需要最低 Jlib 0.3.10。原生库映射仅限受支持的 Unity Mono、Game.Instance 创建之前，变更需要重启；请在重启后的初始化阶段设置映射。NuGet 最新已发布版本请以包页为准。

[url=https://github.com/LIghtJUNction/Jlib/blob/main/Jlib/README.md]完整消费者接口与构建文档[/url] · [url=https://www.nuget.org/packages/LIghtJUNction.Jlib]NuGet 开发引用[/url]

[h2]开发流程示意[/h2]
手绘图说明工具链分工；测量结果另见验证记录。
[img]https://raw.githubusercontent.com/LIghtJUNction/OniMods/main/.agents/skills/oni-workshop-description/assets/onim-illustrated-jlib.gif[/img]

[h2]来源与反馈[/h2]
基于 [url=https://github.com/peterhaneve/ONIMods]Peter Han 的 PLib[/url]，保留其 MIT 版权和许可。Jlib 新增代码亦使用 MIT。
[url=https://github.com/LIghtJUNction/Jlib]源码[/url] · [url=https://github.com/LIghtJUNction/Jlib/issues]问题反馈[/url] · [url=https://github.com/LIghtJUNction/Jlib/blob/main/Jlib/THIRD-PARTY-NOTICES.md]第三方声明[/url]
反馈请提供游戏与 Jlib 版本、依赖 Mod 列表和最小复现；移除私人账号信息。
