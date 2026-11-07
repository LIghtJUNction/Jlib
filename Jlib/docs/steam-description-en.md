Welcome to Jlib! The high-performance shared prerequisite mod for **Oxygen Not Included**.
[b]Downstream mods share one unified, PLib-compatible provider instead of packaging duplicate copies and version conflicts.[/b]
Subscribe once, place it above dependent mods, and leave colony rules alone. Jlib does not alter vanilla gameplay mechanics.

[h2]Current Stage: v0.3.0[/h2]
[i]Public ABI: 1. Retained PLib component protocol: 4.25.0.0.[/i]
[i]Enable one copy of static ID LIghtJUNction.Jlib.[/i]

[h3]✨ Complete Features Enumeration[/h3]
* [b]PLibCore Reflection & Tools[/b]: Mod ownership binding ([code]PUtil.DependentMods[/code]), PDetours fast reflection delegates, PPatchTools/PTranspilerTools bytecode utilities, PThreadLocalPool object pooling, Gettext PO localization, and TMP font patching.
* [b]PLibOptions Mod Configuration[/b]: Native-styled settings UI (POptions) with full widget catalog (sliders, colors, select-one, password), dynamic options, and runtime hot-refresh ([code]POptions.RefreshRegistered[/code]).
* [b]PLibUI Modern Declarative UI[/b]: Lightweight Unity UI engine with 4 responsive layouts (BoxLayout/GridLayout/RelativeLayout/CardLayout) and draggable modal dialogs.
* [b]PLibActions / Buildings / Lighting / Database / AVC[/b]: Keybindings with safe dispatch ([code]KButtonEventSafety[/code]), building/conduit registration, polygon lighting, Codex entries, and multi-source update checks.
* [b]Jlib.Runtime Modern Prerequisite[/b]: Strict ABI 1 verification, zero-dependency contract ([code]JlibContract[/code]), native Mono P/Invoke redirection ([code]ReplaceNativeLibrary[/code]/[code]ReplaceSimDll[/code]) for C++/Rust engines like OniSim, sandboxed asset loading ([code]ModResources[/code]), and runtime component replacement.

[h3]🌟 Exclusive: Dynamic Interface Profiler & Harmony Hook Debugger[/h3]
* [b]Dynamic Discovery (No Hardcoded Enums)[/b]: Dynamically enumerates all Jlib modules, public classes, and methods via runtime reflection. Any new APIs appear automatically!
* [b]API Call Counts[/b]: Accurately tracks call frequencies and timestamps for each interface.
* [b]Caller Mod Attribution[/b]: Automatically walks call stacks to attribute invocations directly to the calling mod!
* [b]Execution & Override Order (Full Pipeline)[/b]: Dynamically discovers all methods hooked by Harmony, listing Prefix -> Transpiler -> Postfix execution chains and priorities.
* [b]All Listed & Interactive Reordering[/b]: Open a draggable floating window. Use [b][▲ Move Up][/b] or [b][▼ Move Down][/b] to adjust patch priorities live and resolve mod collisions without restarting ONI!
* [b]Live Debug Mode[/b]: Enable verbose call logging in [code]Player.log[/code] or dump a full markdown diagnostic report at any time!

[h3]MODDING API & NuGet[/h3]
NuGet: [url=https://www.nuget.org/packages/LIghtJUNction.Jlib]LIghtJUNction.Jlib[/url] (net48/netstandard2.1). Set [code]ExcludeAssets="runtime"[/code], do NOT bundle Jlib.dll. Call [code]JlibApi.RegisterConsumer[/code] before type enumeration.

[h3]Who needs it[/h3]
[url=https://steamcommunity.com/sharedfiles/filedetails/?id=3807166922]ONI MCP Server[/url] and [url=https://steamcommunity.com/sharedfiles/filedetails/?id=3807166689]CycleTrim[/url] both require this Workshop item.

[h3]Contributions[/h3]
Source: [url=https://github.com/LIghtJUNction/OniMods]GitHub Repository[/url] | Issues: [url=https://github.com/LIghtJUNction/OniMods/issues]Issues[/url].
