# ApocaDustStorm

Worldwide dust storms for **Apocalypter**, inspired by **Mad Max (2015)**. This repository shares the **0.1.27 source** so modders can inspect behavior, report conflicts and develop separate compatibility patches. Download the installable ZIP from [Releases](https://github.com/Jaime-Wolf/Dust-storms/releases).

## Changes in 0.1.27

Performance update: player shelter checks stay at four per second during storms, including while driving. Calm weather skips those queries unless worn vehicle protection needs shelter-only recovery, then checks once per second. Collider classification is cached, scene searches run once per scene and at storm start, and vehicle entry refreshes local headlights. Camera references, NPC eligibility, fog/lighting snapshots, the notice style and the compass Canvas are cached. NPC shelter checks are staggered with a shared limit of two checks per frame.

Storm appearance, audio, hazard values and saved settings are retained. Ordinary shelter changes are detected within 0.25 seconds; cab changes, pause/clock jumps and large teleports refresh immediately. Automated checks passed; in-game performance and visual testing of this update is still pending.

## What the mod does

Chance-based day/night storms last 5-15 minutes by default, approach and clear gradually, and use a changing compass wind direction. Dense distant haze, moving ground dust, overcast lighting, colliding sticks, wind and sand-on-metal audio, gentle vehicle buffeting, branching dust lightning and occasional edible windblown lizards build the atmosphere.

Hazardous dust damages exposed players immediately. Buildings, caves and conex containers provide shelter. Vehicles start at 90% protection and fall to 25% over five minutes in hazardous dust; proper shelter gradually restores it. Exiting/re-entering does not reset protection. Wind slows player and AI movement, but AI never receive storm health damage. Exposed sleep is interrupted without an added sleep/wake health penalty. Dusty screen edges and an icon above the compass show exposure, vehicle cover or shelter.

Settings are available through Apocasetter's MODS menu. All thirteen Effects sliders default to 1.00. Automatic storms are independent of the vanilla Dust Storm Off switch by default. There is no telemetry or networking in the mod.

## For other modders

Start with [COMPATIBILITY.md](COMPATIBILITY.md), which identifies the relevant source files, native Harmony targets, camera/rendering lifecycle and the existing optional AI/camera adapters. Read the [reuse permissions](LICENSE.md) before using or distributing code.

- Separate compatibility patches that depend on the original mod are allowed.
- Copying code into another mod, bundling this mod, redistributing a modified version or reuploading it requires permission from **Jaime-Wolf**.
- Source visibility does not grant general reuse or redistribution permission.
- Use Issues for compatibility reports and Pull requests for proposed changes to this repository.

## Requirements and build

**Runtime:** BepInEx 5 x64 and Apocasetter. The saved build was compiled against Unity 2020.3.49f1, Apocasetter 2.0.9 and the installed game assemblies. NPCAI is optional; its adapter was checked against version 1.2.0. ApocaChaseCamera 0.1.5 remains a separate optional mod.

**Build:** Windows with the .NET Framework compiler and a local Apocalypter installation containing the dependencies. Game/Unity/NWH/PlayMaker/BepInEx/Apocasetter binaries are not included in the source folders.

```powershell
# Set this to your own game installation.
$game = 'D:\Steam\steamapps\common\Apocalypter'
& .\Source\build.ps1 -GameDir $game
& .\Source\build.ps1 -GameDir $game -Development
```

Release output: `Source/build/ApocaDustStorm.dll`. Dev output: `Source/build-dev/ApocaDustStorm.dll`. The dev build defines `APOCA_DEV` and retains **F8** for storm start/clear and **N** for lightning preview. Release compiles out those testing handlers and settings. Install only one variant: they share `local.apocalypter.duststorm`, the same DLL path and config file.

## Validation

```powershell
& .\Verification\test.ps1
& .\Verification\harmony-compat.ps1 -GameDir $game
& .\Verification\inspect-build.ps1 -GameDir $game
& .\Verification\inspect-build.ps1 -GameDir $game -PluginPath .\Source\build-dev\ApocaDustStorm.dll
& .\Verification\check-variants.ps1 -GameDir $game
```

Version 0.1.27 passes **661 regression assertions**: 234 model, 402 simulated runtime and 25 controlled installed-Harmony assertions. Both variants pass native target inspections; 305 shared gameplay methods and embedded literal arrays match between release and dev. The new checks count expensive calls and verify cache/buffer reuse. They do not measure live FPS or simulate Unity GPU rendering or real PhysX. In-game performance testing is pending. See [Verification/README.md](Verification/README.md).

## Installation

Download **ApocaDustStorm-0.1.27.zip** from [Releases](https://github.com/Jaime-Wolf/Dust-storms/releases/tag/v0.1.27). Close the game, then copy the ZIP's **ApocaDustStorm** folder into `BepInEx/plugins`, replacing the previous DLL. The final path is `BepInEx/plugins/ApocaDustStorm/ApocaDustStorm.dll`. Install only one release/dev variant. Source and verification files are separate from the player installation ZIP.

The `-Development` build above creates the installable dev DLL. `Source/` contains the mod's implementation; `Verification/` contains original test simulations and checking scripts. Game method names, settings and state names are referenced for integration, but the current source tree does not include game DLLs, decompiled game classes, extracted game assets or native inspection dumps.

## Credits

Storm atmosphere inspired by **Mad Max (2015)**. Thanks to **Sawyer**, creator of Apocalypter. Native assets are loaded from the installed game; no game assets or dependency assemblies are redistributed by the source folders.
