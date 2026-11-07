[img]https://raw.githubusercontent.com/LIghtJUNction/Jlib/main/Jlib/preview.png[/img]

欢迎来到 [b]Jlib[/b]！《缺氧》（Oxygen Not Included）下一代高性能共享基础前置库。
所有下游模组共用一份高性能、标准兼容的提供者，彻底告别重复打包与版本冲突！

订阅一次，排在需要它的模组前面即可。Jlib 不改变游戏原生玩法，但为整个 Mod 生态提供最坚实的底层底座。

[h2]当前版本: v0.3.0[/h2]
[i]公开稳定 ABI: 1。兼容保留 PLib 组件协议: 4.25.0.0。[/i]
[i]static ID: LIghtJUNction.Jlib（全局只启用一份，自动多版本仲裁）。[/i]

[h3]支持的 DLC:[/h3]
[img]https://raw.githubusercontent.com/Sgt-Imalas/Sgt_Imalas-Oni-Mods/refs/heads/master/Compat_All.png[/img]

[h3]✨ 核心特性矩阵 (Core Features)[/h3]

[b]1. 原生组件热替换引擎 (Native Component Replacement)[/b]
* 支持在托管层透明替换游戏底层动态库（如 [code]SimDLL[/code] / [code]sim.dll[/code]）。
* 无缝对接 Rust / C++ 高性能仿真内核（如 [b]CycleTrim / OniSim[/b]），支持平台架构自适应加载与 Mono DLL 映射。

[b]2. 一格多物（多组分）解耦垫片体系 (Multicomponent Shims)[/b]
* 针对游戏原版“一格一物”硬编码死锁，提供 6 大系统级解耦兼容层：
  - [b]视觉渲染[/b]：气体双线性质量加权颜色混合，次级气体不再隐形。
  - [b]复制人生理[/b]：基于分压加权的可呼吸质量判定，彻底杜绝混气假死窒息。
  - [b]工业建筑[/b]：排气口液体浸没微量容差与建筑消费物相兼容判定。
  - [b]生态环境[/b]：植物微气压环境容差评估。
  - [b]导航阻尼[/b]：泳道表面液面波动的滞后滤波。
  - [b]信息展示[/b]：鼠标悬浮卡片多组分百分比精准呈现。
* 任何模组调用 [code]JlibApi.InstallMulticomponentPatches[/code] 即可一键挂载全套垫片。

[b]3. 现代全栈 PLib 架构演进[/b]
* [b]PLibCore[/b]：反射加速 Detours、字节码 Transpiler 工具、零锁高并发对象池（[code]PThreadLocalPool[/code]）、多语言 PO 本地化。
* [b]PLibOptions[/b]：现代声明式设置面板，全类型控件（滑动条/拾色器/下拉框/密码遮罩输入），支持动态选项与热刷新。
* [b]PLibUI[/b]：声明式 Unity UI 框架，自适应网格、盒式与卡片布局，可拖拽弹窗。
* [b]PLibActions & PLibBuildings[/b]：自定义快捷键安全分发（[code]KButtonEventSafety[/code]）与建筑管道拓扑。

[b]4. 跨线程安全调度与动态诊断 (Dispatcher & Diagnostics)[/b]
* 双缓冲线程安全主线程调度器（[code]JlibApi.RunOnMainThread[/code] / [code]InvokeOnMainThread[/code]）。
* 运行时 API 调用动态统计与 Harmony 补丁审查诊断。

[h3]📌 使用指南[/h3]
1. 在创意工坊点击“订阅”本模组。
2. 启动游戏，在模组管理器中勾选启用 [b]Jlib[/b]。
3. 将 [b]Jlib[/b] 拖动到需要依赖它的模组（如 OniMcp、CycleTrim 等）上方。
4. 重启游戏即可享受极速稳定的模组体验！

[h3]开源许可与贡献[/h3]
* 代码遵循 MIT / Apache-2.0 开源协议。
* 源码仓库：[url=https://github.com/LIghtJUNction/OniMods]LIghtJUNction/OniMods[/url]
* 欢迎提交 PR、报告 Issue 或参与社区交流！
