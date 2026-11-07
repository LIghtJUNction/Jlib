[img]https://raw.githubusercontent.com/LIghtJUNction/Jlib/release/jlib-0.1.0/Jlib/preview.png[/img]

Welcome to Jlib! The shared library mod behind the other OniMods releases.
With this mod, [b]OniMcp and CycleTrim load one PLib-compatible provider instead of packing a private copy.[/b]

Subscribe once, place it above the mods that need it, and leave the colony rules alone. Jlib does not change gameplay.

[h2]Current Stage: v0.1.0[/h2]
[i]The public ABI is 1. The retained PLib component protocol is 4.25.0.0.[/i]
[i]Enable exactly one copy of static ID LIghtJUNction.Jlib. A second provider with the same ID will fight the first.[/i]

[h3]Supported DLC:[/h3]
[img]https://raw.githubusercontent.com/Sgt-Imalas/Sgt_Imalas-Oni-Mods/refs/heads/master/Compat_All.png[/img]

[h3]MODDING API[/h3]
This is a shared-prerequisite fork of [url=https://github.com/peterhaneve/ONIMods]Peter Han's PLib[/url]. The PeterHan.PLib.* source API stays. Inherited code keeps its MIT notice.
NuGet package: [url=https://www.nuget.org/packages/LIghtJUNction.Jlib]LIghtJUNction.Jlib 0.1.0[/url], targets net48 and netstandard2.1.
Reference it with ExcludeAssets="runtime", or with a project reference whose Private is false. Do not ILRepack Jlib.dll or PLib assemblies into a consumer release.
Call JlibApi.RegisterConsumer before the game enumerates the consumer's implementation types. A late OnLoad check is too late.
The options screen lists mods currently sharing this provider. POptions.RefreshRegistered can pick up options registered after the first init.

[h3]Who needs it[/h3]
[url=https://steamcommunity.com/sharedfiles/filedetails/?id=3807166922]ONI MCP Server[/url] and [url=https://steamcommunity.com/sharedfiles/filedetails/?id=3807166689]CycleTrim[/url] both require this Workshop item.
OniOnline bundles its own PLib and does not ask for Jlib.
Installing the NuGet package on a developer machine does not subscribe the player. The game still needs this mod enabled.

[h3]Controls and Quality of Life[/h3]
There is no colony hotkey. The install order is the whole interface.

* Subscribe to Jlib and enable it.
* Drag it above OniMcp, CycleTrim, and any other consumer.
* Restart the game. One enabled provider is enough for every consumer in the list.
* Modders resolve files with ModResources from the owning mod root, including files under implementation/.

[h3]Built with onim[/h3]
This mod comes from [url=https://github.com/LIghtJUNction/OniMods]OniMods[/url]. onim is the toolchain in that repo. It takes an Oxygen Not Included mod straight through development, testing, and Workshop publishing.

onim dev tries it in game. onim build packs it. onim publish sends it to Steam.

[h3]Contributions[/h3]
Contributions are welcome!
If you have improvements, fixes, or new features, feel free to open a Pull Request.

Please make sure your changes are clear and well-documented where necessary.

You can find the [url=https://github.com/LIghtJUNction/OniMods/tree/main/mods/Jlib]GitHub repository here[/url]

[h3]Disclaimer[/h3]
Please remember that this mod is under active development and will receive regular updates and improvements.
Your feedback and suggestions are highly appreciated.
Jlib does not retune duplicants, buildings, or the simulation. If a consumer misbehaves, say which consumer and which Jlib version you loaded.

[h3]Contact[/h3]
Questions and bug reports go to the [url=https://github.com/LIghtJUNction/OniMods/issues]GitHub issue tracker[/url].

Have fun out there. One library, many mods, and no extra germs.
[img]https://i.ibb.co/LXdJf9K9/I3P8A6f.gif[/img]
