[img]https://media.githubusercontent.com/media/LIghtJUNction/OniMods/main/.agents/skills/oni-workshop-description/assets/ai-generated-congratulations.png[/img]
[h1]CONGRATULATIONS![/h1]
[b]THIS MOD IS ENTIRELY AI-GENERATED[/b]
This statement describes Jlib's AI development. [b]Inherited Peter Han / PLib code and third-party material retain their original authorship and licenses; they are not original AI-generated content of this project.[/b]

[img]https://raw.githubusercontent.com/LIghtJUNction/Jlib/main/Jlib/preview.png[/img]

[h1]Jlib · One shared prerequisite for your mods[/h1]
Jlib supplies shared configuration, UI, key bindings and runtime interfaces to Oxygen Not Included mods. Install one provider and dependent mods use that same Jlib assembly.

[b]This page describes source version 0.3.10; the public ABI is 1.[/b] Check the game's Mods menu for the actual downloaded package. Jlib itself does not add colony gameplay or promise an FPS increase.

[h2]Installation[/h2]
[list]
[*]Subscribe to Jlib and enable it in the Mods menu.
[*]Place it above dependent mods and restart the game.
[*]Enable only one provider with the same static ID. Do not enable both local and Workshop copies.
[*]Use Jlib's options to inspect dependent mods. For version errors, check the assembly the game actually loaded.
[/list]
The NuGet package is a developer reference, not a replacement for the in-game prerequisite. Workshop, source and NuGet versions may differ.

[h2]What this iteration changes[/h2]
[list]
[*]Multicomponent data distinguishes a known zero from an unknown quantity. Pressure, partial pressure and volume fractions have availability flags; uncomputed values must not be treated as a vacuum.
[*]Without liquid occupancy data, the affected Vent decision falls back to the original game path. Current OniSim pressure and partial pressure remain unavailable.
[*]Reviewed U60 texture callback layouts are supported through IL inspection without executing type initializers. An unknown layout skips only the affected patch and reports a reason.
[*]Main-thread dispatch takes at most 128 callbacks per frame and checks an approximately 4 ms budget between callbacks. One callback can still exceed that budget. This is not an FPS guarantee.
[/list]

[h2]Validation scope[/h2]
This iteration passed 2,379 assertions in a standalone host, with net48/netstandard2.1 builds and metadata inspection. The host does not execute game DLLs. These checks do not replace game testing, coverage of every mod combination or long save/load sessions.

[h2]For mod authors[/h2]
Jlib retains the PeterHan.PLib.* namespace and PLib component protocol 4.25.0.0. Do not merge or copy Jlib.dll / PLib*.dll into a consumer release. Validate the loaded provider's ABI and minimum version before the game enumerates implementation types.

The availability fields require Jlib 0.3.10. Native-library mapping is restricted to supported Unity Mono processes before Game.Instance exists; changing it requires a restart. It is not an in-game hot swap. Check NuGet for the latest actually published package.

[url=https://github.com/LIghtJUNction/Jlib/blob/main/Jlib/README.md]Consumer contracts and build instructions[/url] · [url=https://www.nuget.org/packages/LIghtJUNction.Jlib]NuGet developer reference[/url]

[h2]Development workflow[/h2]
The hand-drawn illustration explains the toolchain. It is not a game screenshot or performance measurement.
[img]https://raw.githubusercontent.com/LIghtJUNction/OniMods/main/.agents/skills/oni-workshop-description/assets/onim-illustrated-jlib.gif[/img]

[h2]Sources and feedback[/h2]
Based on [url=https://github.com/peterhaneve/ONIMods]Peter Han's PLib[/url], with its MIT copyright and license retained. Jlib additions also use MIT.
[url=https://github.com/LIghtJUNction/Jlib]Source[/url] · [url=https://github.com/LIghtJUNction/Jlib/issues]Report an issue[/url] · [url=https://github.com/LIghtJUNction/Jlib/blob/main/Jlib/THIRD-PARTY-NOTICES.md]Third-party notices[/url]
Include game/Jlib versions, dependent mods and a minimal reproduction. Remove private account details.
