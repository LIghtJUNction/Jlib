[img]https://raw.githubusercontent.com/LIghtJUNction/Jlib/main/Jlib/preview.png[/img]

Welcome to [b]Jlib[/b]! The next-generation high-performance shared framework and prerequisite library for Oxygen Not Included.
All downstream mods share a single, high-performance, standards-compliant provider, eliminating duplicate packaging and version conflicts forever!

Subscribe once and arrange it above dependent mods. Jlib preserves vanilla colony gameplay while providing a robust infrastructural foundation for the entire modding ecosystem.

[h2]Current Version: v0.3.0[/h2]
[i]Public Stable ABI: 1. Retained PLib Protocol: 4.25.0.0.[/i]
[i]static ID: LIghtJUNction.Jlib (single global provider with multi-version arbitration).[/i]

[h3]Supported DLC:[/h3]
[img]https://raw.githubusercontent.com/Sgt-Imalas/Sgt_Imalas-Oni-Mods/refs/heads/master/Compat_All.png[/img]

[h3]✨ Core Features[/h3]

[b]1. Native Component Replacement Engine[/b]
* Transparently replace game native dynamic libraries (such as [code]SimDLL[/code] / [code]sim.dll[/code]) at the managed boundary.
* Seamless integration with high-performance Rust/C++ simulation engines (like [b]CycleTrim / OniSim[/b]), supporting platform-adaptive resolution and Mono DLL mapping.

[b]2. Multicomponent Coexistence Shims[/b]
* Unlocks ONI's hardcoded "one-element-per-cell" assumptions with 6 system-wide compatibility shims:
  - [b]Rendering[/b]: Mass-weighted gas and liquid color blending. Secondary species never turn invisible.
  - [b]Physiology[/b]: Respiration resolved by partial breathable mass. Prevents false suffocation in mixed atmospheres.
  - [b]Buildings[/b]: Flooded-liquid tolerance for gas vents and multicomponent consumption checks.
  - [b]Plants[/b]: Partial safe-element pressure evaluation for plant vitality.
  - [b]Navigation[/b]: Swim entry/exit hysteresis preventing surface wave oscillation.
  - [b]Tooltips[/b]: Accurate multicomponent mass and percentage breakdown on hover cards.
* One-click installation via [code]JlibApi.InstallMulticomponentPatches[/code].

[b]3. Modern Full-Stack PLib Evolution[/b]
* [b]PLibCore[/b]: Accelerated Detours, bytecode transpilers, lock-free object pooling ([code]PThreadLocalPool[/code]), and gettext PO localization.
* [b]PLibOptions[/b]: Rich declarative settings UI (sliders, color pickers, dropdowns, password masking), dynamic options, and hot refresh.
* [b]PLibUI[/b]: Declarative Unity UI framework with adaptive grid, box, and card layouts plus draggable dialogs.
* [b]PLibActions & PLibBuildings[/b]: Safe custom key dispatch ([code]KButtonEventSafety[/code]) and building conduit topologies.

[b]4. Cross-Thread Dispatcher & Dynamic Diagnostics[/b]
* Double-buffered thread-safe main thread dispatcher ([code]JlibApi.RunOnMainThread[/code] / [code]InvokeOnMainThread[/code]).
* Real-time API invocation tracking and Harmony patch inspection report.

[h3]📌 Installation & Ordering[/h3]
1. Subscribe to [b]Jlib[/b] on Steam Workshop.
2. In the ONI Mods menu, enable [b]Jlib[/b].
3. Drag [b]Jlib[/b] above dependent mods (e.g. OniMcp, CycleTrim).
4. Restart the game and enjoy!

[h3]Open Source & Contributions[/h3]
* Licensed under MIT / Apache-2.0.
* Source repository: [url=https://github.com/LIghtJUNction/OniMods]LIghtJUNction/OniMods[/url]
