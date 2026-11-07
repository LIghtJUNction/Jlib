[img]https://raw.githubusercontent.com/LIghtJUNction/Jlib/release/jlib-0.1.0/Jlib/preview.png[/img]

Welcome to Jlib! The high-performance shared library mod for **Oxygen Not Included**.
With this mod, [b]downstream mods share one unified, PLib-compatible provider instead of packaging duplicate copies and version conflicts.[/b]

Subscribe once, place it above the mods that need it, and leave colony rules alone. Jlib does not alter vanilla gameplay mechanics, but provides a rock-solid runtime foundation.

[h2]Current Stage: v0.3.0[/h2]
[i]Public ABI: 1. Retained PLib component protocol: 4.25.0.0.[/i]
[i]Enable one copy of static ID LIghtJUNction.Jlib. A second provider will fight the first.[/i]

[h3]Supported DLC:[/h3]
[img]https://raw.githubusercontent.com/Sgt-Imalas/Sgt_Imalas-Oni-Mods/refs/heads/master/Compat_All.png[/img]

[h3]✨ Complete Features Enumeration[/h3]
Jlib inherits and modernizes Peter Han's PLib architecture, delivering a modern mod toolkit:

[b]1. PLibCore Reflection & Utilities[/b]
* [b]PUtil[/b]: Mod initialization, version verification, ownership binding, and [code]PUtil.DependentMods[/code].
* [b]PPatchTools & PTranspilerTools[/b]: Safe Harmony patch utilities, opcode pattern matching, and IL transpiler tools.
* [b]PDetours Fast Reflection[/b]: Dynamic delegate-based DetouredField, DetouredMethod, and LazyDetouredField with near-native speed.
* [b]PStateMachines[/b]: Inspection and lifecycle hooks for ONI's native StateMachine system.
* [b]PPatchManager[/b]: Declarative annotations ([code][PLibPatch][/code], [code][PLibMethod][/code], [code]RunAt[/code]).
* [b]PThreadLocalPool[/b]: Thread-local object pooling to eliminate GC pauses.
* [b]PLibStrings & PLocalization[/b]: Automatic Gettext PO loading and safe string table registration.
* [b]TextMeshProPatcher[/b]: Fallback fonts, dynamic font patching, and CJK support for TMP.
* [b]PRegistry[/b]: Cross-mod dynamic service bus ([code]IPLibRegistry[/code]).

[b]2. PLibOptions Mod Configuration[/b]
* Full options screen (POptions) matching ONI's UI style without boilerplate code.
* Widgets: Checkbox, Slider, Color picker, SelectOne enum, String/TextBlock, Password/MaskedString, and custom Buttons.
* Advanced: Dynamic options ([code]DynamicOptionAttribute[/code]), categories, restart alerts, and DLC/mod filters.
* Hot-reload: [code]POptions.RefreshRegistered()[/code] updates options for delayed modules.

[b]3. PLibUI Modern Declarative UI[/b]
* Declarative Unity UI framework designed specifically for ONI.
* Components: [code]PButton[/code], [code]PCheckBox[/code], [code]PComboBox[/code], [code]PDialog[/code], [code]PGridPanel[/code], [code]PLabel[/code], [code]PPanel[/code], [code]PRelativePanel[/code], [code]PScrollPane[/code], [code]PSlider[/code], [code]PSpacer[/code], [code]PTextArea[/code], [code]PTextField[/code], and [code]PToggle[/code].
* Layouts: [code]BoxLayout[/code], [code]GridLayout[/code], [code]RelativeLayout[/code], and [code]CardLayout[/code].
* Draggable modal dialogs ([code]PDialogDragHandle[/code]), auto font scaling, and tooltips.

[b]4. PLibActions Input & Keybindings[/b]
* In-game custom keybindings and actions ([code]PAction[/code], [code]PActionManager[/code], [code]PKeyBinding[/code]).
* Action bar menu button injections ([code]PToolMode[/code], [code]ToolMenuDictionary[/code]).
* Safe button dispatch with exception isolation ([code]KButtonEventSafety[/code]).

[b]5. PLibBuildings & Lighting[/b]
* Building registration ([code]PBuildingManager[/code]), conduits, and colored range visualizers.
* Polygon lighting with custom light meshes ([code]PLightManager[/code], [code]OctantBuilder[/code]) and ray modes.

[b]6. PLibDatabase & AVC[/b]
* Custom Codex entries ([code]PCodexManager[/code]) and colony achievements ([code]PColonyAchievement[/code]).
* Multi-source update checks (Steam, JSON, YAML) with outdated notices ([code]ModOutdatedWarning[/code]).

[b]7. Jlib.Runtime Modern Prerequisite Engine[/b]
* Strict ABI 1 verification ([code]JlibApi.CheckCompatibility[/code], [code]RequireCompatibility[/code]).
* Zero-dependency contract helper ([code]JlibContract[/code]) for consumer bootstraps.
* Native library redirection ([code]ReplaceNativeLibrary[/code], [code]ReplaceSimDll[/code]): Mono P/Invoke mapping intercepts for high-performance C++/Rust engines (OniSim).
* Sandboxed asset loader ([code]ModResources[/code]) with path traversal protection.
* Runtime game component replacement and multicomponent simulation shims.

[h3]🌟 Exclusive New Feature: Dynamic Interface Profiler & Harmony Hook Debugger[/h3]
Stop guessing what is happening under the hood! Jlib includes a complete runtime diagnostic suite:
* [b]Dynamic Discovery (No Hardcoded Enums)[/b]: Dynamically enumerates all Jlib modules, public classes, and methods via runtime reflection. Any new APIs added in future updates appear automatically!
* [b]API Call Counts[/b]: Accurately tracks call frequencies and timestamps for each interface.
* [b]Caller Mod Attribution[/b]: Automatically walks call stacks to attribute invocations directly to the calling mod!
* [b]Execution & Override Order (Full Pipeline)[/b]: Dynamically discovers all methods hooked by Harmony, listing Prefix -> Transpiler -> Postfix execution chains and priorities.
* [b]All Listed & Interactive Reordering[/b]: Open a draggable floating debugger window. Use [b][▲ Move Up][/b] or [b][▼ Move Down][/b] to adjust patch priorities live and resolve mod collisions without restarting ONI!
* [b]Live Debug Mode[/b]: Enable verbose call logging in [code]Player.log[/code] or dump a full markdown diagnostic report at any time!

[h3]MODDING API & NuGet[/h3]
NuGet: [url=https://www.nuget.org/packages/LIghtJUNction.Jlib]LIghtJUNction.Jlib[/url], targets [code]net48[/code] and [code]netstandard2.1[/code].
Reference with [code]ExcludeAssets="runtime"[/code], or set [code]Private="false"[/code].
[b]Do NOT merge Jlib.dll or PLib assemblies into consumer mod distribution packages![/b]
Call [code]JlibApi.RegisterConsumer[/code] before ONI enumerates implementation types.

[h3]Who needs it[/h3]
[url=https://steamcommunity.com/sharedfiles/filedetails/?id=3807166922]ONI MCP Server[/url] and [url=https://steamcommunity.com/sharedfiles/filedetails/?id=3807166689]CycleTrim[/url] both require this Workshop item.

[h3]Controls and Quality of Life[/h3]
* Subscribe to Jlib and enable it in the game's Mods menu.
* Place Jlib above OniMcp, CycleTrim, and other consumer mods.
* Restart ONI. All dependent mods share the single enabled provider.
* In Jlib's Options screen, view active dependent mods, check dynamic API call counts, toggle debug logs, or drag to reorder Harmony hooks live!

[h3]Built with onim[/h3]
Part of [url=https://github.com/LIghtJUNction/OniMods]OniMods[/url]. onim links development, host contract testing, and Steam Workshop publishing.

[h3]Contributions and Feedback[/h3]
Contributions are welcome!
Source code: [url=https://github.com/LIghtJUNction/OniMods/tree/main/mods/Jlib]GitHub Repository[/url]
Bug reports: [url=https://github.com/LIghtJUNction/OniMods/issues]GitHub Issues[/url]

[h3]Disclaimer[/h3]
Jlib is under active maintenance. It does not alter duplicant stats or vanilla simulation rules. If a consumer mod misbehaves, please mention which mod and the loaded Jlib version in your report.

Have fun out there! One shared library, infinite possibilities, zero extra germs!
[img]https://i.ibb.co/LXdJf9K9/I3P8A6f.gif[/img]