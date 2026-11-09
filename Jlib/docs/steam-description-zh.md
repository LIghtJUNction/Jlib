欢迎来到 Jlib！《缺氧》下一代高性能共享前置库。
[b]所有下游模组共用一份高性能、标准兼容的提供者，彻底告别重复打包与版本冲突！[/b]
订阅一次排在依赖 Mod 前面。殖民地规则不动，Jlib 不改变游戏原生玩法，但为整个 Mod 生态提供坚实底座。

[h2]当前阶段: v0.3.2[/h2]
[i]公开稳定 ABI: 1。保留的 PLib 组件协议: 4.25.0.0。[/i]
[i]static ID LIghtJUNction.Jlib 只启用一份。[/i]

[h3]✨ 核心功能全集枚举[/h3]
* [b]PLibCore 核心工具[/b]：模组所有权绑定（[code]PUtil.DependentMods[/code]）、PDetours 快速反射委托、PPatchTools/PTranspilerTools 字节码辅助、PThreadLocalPool 线程对象池、PO 语言包加载与 TMP 字体补丁。
* [b]PLibOptions 配置体系[/b]：游戏原生风格配置页（POptions），支持全类型控件（滑动条、颜色拾取、枚举单选、密码掩码等）、动态选项与运行时热刷新（[code]POptions.RefreshRegistered[/code]）。
* [b]PLibUI 声明式 UI 引擎[/b]：轻量级 Unity UI 框架，内置 4 大布局引擎（BoxLayout/GridLayout/RelativeLayout/CardLayout）与可拖拽浮动弹窗。
* [b]PLibActions / Buildings / Lighting / Database / AVC[/b]：自定义按键映射与安全分发、建筑管线注册、多边形光照、法典成就注入与多源自动版本检查。
* [b]Jlib.Runtime 现代前置底座[/b]：严格 ABI 1 握手、零依赖引导合约（[code]JlibContract[/code]）、原生动态库 Mono P/Invoke 重定向（[code]ReplaceNativeLibrary[/code]/[code]ReplaceSimDll[/code]）为 OniSim 等 C++/Rust 引擎提供免重启注入、沙盒安全资源解析器（[code]ModResources[/code]）与游戏组件热替换。

[h3]🌟 独家：动态接口调用分析与 Harmony 补丁排序诊断器[/h3]
* [b]零硬编码动态枚举[/b]：运行时反射自动扫描所有模块与公开 API，随 Mod 更新自动同步！
* [b]接口调用次数统计[/b]：精准记录每个公开 API 的调用频次与最后触发时间。
* [b]调用来源 Mod 溯源[/b]：自动回溯调用栈，直接提取调用者 Mod 名称与 staticID！
* [b]补丁覆盖全景流水线[/b]：按 Prefix -> Transpiler -> Postfix 完整展示执行覆盖顺序与优先级。
* [b]可拖动交互重排覆盖顺序[/b]：配置页一键唤出悬浮窗口，点击 [b][▲ 提前][/b] 或 [b][▼ 延后][/b] 实时重排优先级并即刻生效，无需重启游戏现场化解 Mod 冲突！
* [b]支持 Debug 诊断[/b]：一键开启详细日志追踪并支持导出完整快照至 [code]Player.log[/code]！

[h3]开发者指南 (MODDING API)[/h3]
NuGet: [url=https://www.nuget.org/packages/LIghtJUNction.Jlib]LIghtJUNction.Jlib[/url] (net48/netstandard2.1)。设置 [code]ExcludeAssets="runtime"[/code]，严禁将 Jlib.dll 打包进消费者发行包。在枚举类型前调用 [code]JlibApi.RegisterConsumer[/code]。

[h3]谁需要它[/h3]
[url=https://steamcommunity.com/sharedfiles/filedetails/?id=3807166922]ONI MCP Server[/url] 与 [url=https://steamcommunity.com/sharedfiles/filedetails/?id=3807166689]CycleTrim[/url] 均依赖本模组。

[h3]开源贡献[/h3]
源码与反馈：[url=https://github.com/LIghtJUNction/OniMods]GitHub 仓库[/url] 与 [url=https://github.com/LIghtJUNction/OniMods/issues]Issues[/url]。
