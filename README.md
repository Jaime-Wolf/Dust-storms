# ApocaDustStorm 0.1.44

Worldwide dust storms for **Apocalypter**, inspired by **Mad Max (2015)**. A rolling wall of brown dust approaches from a north, south, east or west wind, sweeps over the terrain and transitions into a low-visibility brownout.

## Changes since 0.1.29

- A visible, animated haboob front replaces the gradual fog-only arrival. Layered dust billows blend against terrain, adapt to hills and valleys, and hand off to nearby blowing dust and brownout haze as the wall reaches you.
- Separate gust-front, entry and blanket phases coordinate wind, the wall, earth-tone grading and near-camera dust. A gentle, adjustable entry shake fades over five seconds and is reduced inside vehicles.
- Night haze uses darker brown tones. Native day/night ambient lighting recovers after clearing, with a limited set of sky-light refreshes so shaded areas return to their normal brightness.
- Dust, placement queries, fog/depth work and lightning audio preparation have been optimized. Sticks are now cosmetic and have no collision. No measured FPS gain is claimed.
- Adds a **Damage grace period** slider from 0 to 300 seconds. Windblown lizard prefab discovery and event scheduling have been improved.

## Weather and hazards

Chance-based storms can occur during day or night and last a random 5-15 minutes by default, including approach and clearing. Wind slowly varies around its prevailing compass direction. Blowing dust, wind and sand-on-metal audio, branching dust lightning and gentle vehicle buffeting accompany the storm. Occasional windblown lizards use the game's native edible food item. No additional cloud system is included.

- Strong wind slows the player and supported AI movement. AI never take storm health damage; the optional NPCAI adapter has a known compatibility limitation described below.
- Exposed players take storm damage after the chosen grace period, which defaults to 0 seconds. Buildings, caves, conex containers and covered boat hulls provide shelter; open decks and uncovered ground remain exposed.
- Vehicles initially provide 90% protection, decreasing to 25% after five minutes in hazardous dust. Proper shelter gradually restores it. Getting out and back in does not reset protection or damage grace.
- Dusty screen edges and a shelter/vehicle/wind badge above the compass show exposure. Actual storm health loss briefly pulses the existing HEALTH label and number yellow.
- Storms interrupt exposed sleep, including sleep in vehicles, without an added sleep or wake health penalty. Sleeping in proper shelter works normally.
- Wind does not lift vehicles, pull off parts or directly damage vehicle parts. Dust lightning adds light and sound without strike damage.

## Installation and updating

Requires **BepInEx 5 x64** and **Apocasetter**. Built against the installed Apocasetter 2.1.1; dependencies are not bundled. ApocaChaseCamera and ApocaGearbox remain separate, optional mods.

1. Close Apocalypter and download **ApocaDustStorm-0.1.44.zip** from [Releases](https://github.com/Jaime-Wolf/Dust-storms/releases/tag/v0.1.44).
2. Copy the ZIP's **ApocaDustStorm** folder into the game's **BepInEx/plugins** folder, replacing the previous mod files. The DLL belongs at `BepInEx/plugins/ApocaDustStorm/ApocaDustStorm.dll`.
3. Keep only one storm DLL. Start the game and adjust the mod through Apocasetter's **MODS** menu.

Existing choices remain in `BepInEx/config/local.apocalypter.duststorm.cfg`. The ZIP includes the 128x128 in-game menu icon. Source and verification files stay separate from the installation ZIP.

## Settings

Storm chance, duration, approach/clearing time, visibility, wall strength, brownout grading, near dust, entry shake, light scattering, wind, audio, lightning, headlights, vehicle buffeting, movement resistance, damage and exposure indicators are adjustable. Effect-strength sliders default to 1.00; Damage grace period defaults to 0. Set Player exposure damage to 0 to disable health damage; exposed sleep interruption remains active.

After five minutes of calm weather, each eligible minute has a 6% default chance of starting a storm. An arrival is not guaranteed immediately after loading. Automatic storms operate independently of the vanilla **Dust Storm Off** switch by default. Enable **Follow vanilla storm switch** if you want that switch to disable this mod's automatic storms too.

## Release and developer files

- **ApocaDustStorm-0.1.44.zip:** normal installation; no storm or lightning testing hotkeys.
- **ApocaDustStorm-0.1.44-Dev.zip:** optional installable test variant; F8 starts/clears a test storm and N previews lightning, or use your saved bindings. Install this instead of the normal DLL, never alongside it.
- **ApocaDustStorm-0.1.44-Developer.zip:** source and verification material only; not an installation package.

Both installable variants use the same plugin identity and settings file. The public source and developer archive contain original mod code and checks, without game assemblies, extracted game assets, saves or decompiled game code.

## Compatibility, verification and permissions

The existing optional NPCAI movement adapter has a known layout mismatch with the installed version used for inspection. It is unchanged in this update; compatibility with that version is not claimed. Other weather, camera or rendering mods may require testing.

The author confirmed the latest build working in game. 1128 automated assertions pass, native inspection passes with the optional NPCAI adapter excluded, and 474 shared gameplay methods match between normal/dev variants. Automated checks do not simulate Unity rendering or PhysX, and no benchmarked FPS improvement is claimed. See [Verification.md](Verification.md) and [COMPATIBILITY.md](COMPATIBILITY.md).

Build with `Source/build.ps1`, or add `-Development` for the test variant, using your installed game dependencies. See [Source/README.md](Source/README.md) for commands.

Separate compatibility patches are allowed. Copying, bundling, reuploading or distributing modified versions requires permission from **Jaime-Wolf**; see [LICENSE.md](LICENSE.md). Public source visibility does not grant general reuse permission.

Storm atmosphere inspired by **Mad Max (2015)**. Thanks to **Sawyer**, creator of **Apocalypter**. Native wind and food assets are loaded from the installed game and are not redistributed. This mod adds no telemetry or network requests.
