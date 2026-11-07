[img]https://raw.githubusercontent.com/LIghtJUNction/Jlib/release/jlib-0.1.0/Jlib/preview.png[/img]

欢迎来到 Jlib！《缺氧》（Oxygen Not Included）下一代高性能共享基础前置库。
有了这个模组，[b]所有下游模组共用一份高性能、标准兼容的提供者，彻底告别重复打包与版本冲突！[/b]

订阅一次，把它排在需要它的模组前面。殖民地规则不动，Jlib 不改变游戏原生玩法，但为整个 Mod 生态提供最坚实的底层底座。

[h2]当前阶段: v0.3.0[/h2]
[i]公开稳定 ABI: 1。保留的 PLib 组件协议: 4.25.0.0。[/i]
[i]static ID LIghtJUNction.Jlib 只启用一份。同一 ID 的第二份提供者会和第一份打架。[/i]

[h3]支持的 DLC:[/h3]
[img]https://raw.githubusercontent.com/Sgt-Imalas/Sgt_Imalas-Oni-Mods/refs/heads/master/Compat_All.png[/img]

[h3]✨ 完整功能特性枚举 (Features Overview)[/h3]
Jlib 继承并深度演进了 Peter Han 的经典 PLib 架构，提供开箱即用、现代化的全栈模组开发套件：

[b]1. PLibCore 核心工具箱与反射增强[/b]
* [b]PUtil[/b]：模组初始化、版本校验、所有权显式绑定与跨模组依赖感知（[code]PUtil.DependentMods[/code]）。
* [b]PPatchTools & PTranspilerTools[/b]：Harmony 补丁安全辅助、IL 字节码指令匹配与安全改写工具集。
* [b]PDetours 高性能快速反射[/b]：基于动态委托的 DetouredField、DetouredMethod 与 LazyDetouredField，调用开销接近原生，彻底告别常规反射的性能瓶颈。
* [b]PStateMachines[/b]：针对游戏原生有限状态机（StateMachine）的深度反射与行为扩展。
* [b]PPatchManager[/b]：声明式注解补丁系统（[code][PLibPatch][/code]、[code][PLibMethod][/code]、[code]RunAt[/code]），让补丁逻辑井井有条。
* [b]PThreadLocalPool[/b]：线程本地高性能对象池，杜绝多线程高频调度中的内存抖动与 GC 停顿。
* [b]PLibStrings & PLocalization[/b]：Gettext PO 语言包自动加载注入与游戏内置字符串安全注册。
* [b]TextMeshProPatcher[/b]：TMP 字体后备、动态字形补丁与多语言字符集缺失修复。
* [b]PRegistry[/b]：跨 Mod 动态服务总线与远程组件解耦（[code]IPLibRegistry[/code]）。

[b]2. PLibOptions 模组配置与可视化设置[/b]
* 专为《缺氧》风格定制的完整选项配置面板（POptions），无需手写复杂 UI 即可自动生成配置页。
* 全类型控件支持：Checkbox（复选框）、Slider（整数/浮点滑动条）、Color（颜色拾取器）、SelectOne（下拉单选枚举）、String/TextBlock（单行/多行长文本说明）、Password/MaskedString（掩码密码输入）、自定义 Button（动作按钮）。
* 高级配置特性：动态选项（[code]DynamicOptionAttribute[/code]）、分类折叠展开、重启游戏提醒（[code]RestartRequiredAttribute[/code]）、DLC 与前置 Mod 条件依赖过滤。
* 运行时热刷新：[code]POptions.RefreshRegistered()[/code]，延迟注册或动态装载模块的选项随调随刷。

[b]3. PLibUI 现代声明式 UI 引擎[/b]
* 为《缺氧》深度优化的轻量级声明式 Unity UI 框架，兼具极简语法与强大扩展性。
* 丰富组件库：[code]PButton[/code]、[code]PCheckBox[/code]、[code]PComboBox[/code]、[code]PDialog[/code]、[code]PGridPanel[/code]、[code]PLabel[/code]、[code]PPanel[/code]、[code]PRelativePanel[/code]、[code]PScrollPane[/code]、[code]PSlider[/code]、[code]PSpacer[/code]、[code]PTextArea[/code]、[code]PTextField[/code]、[code]PToggle[/code]。
* 灵活自适应布局：[code]BoxLayout[/code]（流式盒式布局）、[code]GridLayout[/code]（网格布局）、[code]RelativeLayout[/code]（相对约束布局）、[code]CardLayout[/code]（卡片式切换）。
* 窗口与交互：内置可拖拽浮动弹窗（[code]PDialogDragHandle[/code]）、字号自适应计算与悬停提示（Tooltips）。

[b]4. PLibActions 按键与快捷键系统[/b]
* 游戏自定义按键与输入动作注册（[code]PAction[/code]、[code]PActionManager[/code]、[code]PKeyBinding[/code]）。
* 动作栏与操作菜单动态按钮注入（[code]PToolMode[/code]、[code]ToolMenuDictionary[/code]）。
* 安全按键分发与冲突隔离机制（[code]KButtonEventSafety[/code]）。

[b]5. PLibBuildings 建筑拓展体系[/b]
* 自定义建筑安全注册与元数据注入（[code]PBuilding[/code]、[code]PBuildingManager[/code]）。
* 管道端口与连接定义（[code]ConduitConnection[/code]）、彩色范围可视化器（[code]ColoredRangeVisualizer[/code]）与供电拓扑。

[b]6. PLibLighting 多边形动态光照[/b]
* 突破原版单一圆锥光照限制，支持任意多边形与自定义光照形状（[code]PLightManager[/code]、[code]OctantBuilder[/code]）。
* 多种光线投射模式（[code]PRayMode[/code]）与动态遮挡实时计算。

[b]7. PLibDatabase 数据库与法典扩展[/b]
* 自定义法典条目与富文本注入（[code]PCodexManager[/code]）。
* 自定义殖民地成就动态挂钩（[code]PColonyAchievement[/code]）。
* 游戏原生数据库安全扩展工具（[code]PDatabaseUtils[/code]）。

[b]8. PLibAVC 自动更新检查器[/b]
* 多通道更新检查：Steam Workshop、JSON URL、YAML URL 远端版本同步。
* 过期模组视觉提醒与更新指引（[code]ModOutdatedWarning[/code]）。

[b]9. Jlib.Runtime 专属现代底层增强[/b]
* 严格 ABI 1 握手与版本兼容契约验证（[code]JlibApi.CheckCompatibility[/code]、[code]RequireCompatibility[/code]）。
* 零依赖嵌入式引导合约（[code]JlibContract[/code]），下游 Mod 的独立引导程序集无需捆绑 Jlib 即可校验环境。
* 原生动态库重定向（[code]ReplaceNativeLibrary[/code]、[code]ReplaceSimDll[/code]）：底层 Mono P/Invoke 映射拦截，为 OniSim 等高性能 C++/Rust 仿真引擎提供免重启无缝注入。
* 沙盒安全资源解析器（[code]ModResources[/code]）：安全读取 Mod 根目录或 [code]implementation/[/code] 下的资产，防止路径穿越与句柄泄漏。
* 游戏组件运行时热替换（[code]GameComponentReplacement[/code]）与多分量网格模拟兼容管线（[code]Multicomponent[/code]）。

[h3]🌟 独家新特性：动态接口调用分析与 Harmony 补丁覆盖排序诊断器[/h3]
不再为多 Mod 冲突和黑盒调用抓狂！Jlib 现已内置全套运行时动态诊断与覆盖管理系统：
* [b]动态生成无硬编码枚举[/b]：通过运行时反射实时动态枚举 Jlib 提供的所有模块、公开类型与接口，后续 Mod 更新自动同步，绝无死板硬编码！
* [b]统计每个接口的调用次数[/b]：精准记录每个公开 API 的调用频次与最后触发时间。
* [b]被什么 Mod 调用了（调用溯源）[/b]：自动回溯调用堆栈，直接提取调用者程序集所属的 Mod 名称与 staticID，调用链一目了然！
* [b]覆盖顺序是什么（全景流水线）[/b]：自动提取当前游戏中所有被 Harmony 挂钩的核心方法，按 Prefix（前置）-> Transpiler（转译）-> Postfix（后置）完整展示执行覆盖顺序与 Priority 优先级。
* [b]全部列出来，还可拖动调整覆盖顺序！[/b]：在配置界面中一键唤出可拖动独立诊断窗口，点击 [b][▲ 提前][/b] 或 [b][▼ 延后][/b] 即可实时重排补丁优先级并即刻生效，无需重启游戏即可现场化解 Mod 补丁打架！
* [b]可以 Debug！[/b]：支持一键开启详细调用日志，在 [code]Player.log[/code] 中实时输出调用详情，并支持一键导出完整诊断报告快照！

[h3]开发者使用指引 (MODDING API)[/h3]
NuGet 官方包：[url=https://www.nuget.org/packages/LIghtJUNction.Jlib]LIghtJUNction.Jlib[/url]，全面支持 [code]net48[/code] 与 [code]netstandard2.1[/code]。
引用时设置 [code]ExcludeAssets="runtime"[/code]，或将项目引用的 [code]Private[/code] 设为 [code]false[/code]。
[b]严禁将 Jlib.dll 或 PLib 相关程序集 ILRepack 或打包进消费者的发行包！[/b]
在游戏枚举消费者实现类型之前调用 [code]JlibApi.RegisterConsumer[/code]。

[h3]谁需要它[/h3]
[url=https://steamcommunity.com/sharedfiles/filedetails/?id=3807166922]ONI MCP Server[/url] 与 [url=https://steamcommunity.com/sharedfiles/filedetails/?id=3807166689]CycleTrim[/url] 均依赖本模组。
开发机上安装 NuGet 包仅供编译，玩家在游戏内仍需订阅并启用本前置模组。

[h3]操作指南与体验优化 (QoL)[/h3]
* 订阅并启用 Jlib。
* 在 Mod 列表中将 Jlib 拖动排在 OniMcp、CycleTrim 及其他依赖它的 Mod 上方。
* 重启游戏。列表中所有依赖 Mod 即可无缝共享单例前置。
* 打开 Jlib 的 Mod 选项页，可随时查阅正在依赖本库的模组、查看动态接口调用量、开启 Debug 日志，或点击打开独立浮动窗口拖动调整 Harmony 补丁覆盖顺序！

[h3]由 onim 完成[/h3]
本模组来自 [url=https://github.com/LIghtJUNction/OniMods]OniMods[/url]。onim 是这个仓库里的现代化工具链，把缺氧 Mod 的开发、本地宿主测试与创意工坊发布连成一条直线。

[h3]开源贡献与反馈[/h3]
非常欢迎社区贡献！
如果你有优化、Bug 修复或新功能建议，欢迎提交 Pull Request。
项目源码：[url=https://github.com/LIghtJUNction/OniMods/tree/main/mods/Jlib]GitHub 仓库传送门[/url]
缺陷与建议反馈：[url=https://github.com/LIghtJUNction/OniMods/issues]GitHub Issues[/url]

[h3]免责声明[/h3]
本模组处于积极且紧密的维护中。Jlib 本身不修改游戏内任何复制人属性、建筑数值或模拟逻辑。如果某个下游模组表现异常，请在反馈时附带该模组名称与加载的 Jlib 版本号。

祝各位模组作者与殖民地厂长游戏愉快！一个共享库，百花齐放，无额外细菌！
[img]https://i.ibb.co/LXdJf9K9/I3P8A6f.gif[/img]