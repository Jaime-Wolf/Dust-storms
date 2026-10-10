# ApocaDustStorm 0.1.29

Thick worldwide dust storms inspired by Mad Max (2015), with a gradual approach and clearing, changing directional wind, low visibility and overcast lighting. Includes blowing ground dust, colliding twigs and sticks, gentle vehicle buffeting, sand-on-metal audio, branching dust lightning and occasional edible windblown lizards.

## Changes in 0.1.29

Boat POI hulls now provide storm shelter when their collision geometry covers you. Native shipwreck hulls and their Wreck POI wrappers are recognized without granting protection to the whole location. Open decks and uncovered ground remain exposed; car wrecks, props, vegetation and driveable vehicle roofs remain excluded. Boat cover uses the same damage, sleep, movement and protection-recovery rules as other structural shelter.

234 model checks, 470 simulated runtime checks and 25 controlled Harmony compatibility checks passed (729 total). The 49 new boat checks include 28 native collider hierarchy fixtures. Release and dev compile successfully with matching gameplay methods. In-game boat coverage needs testing. The native compatibility inspection also found an existing mismatch with the currently installed optional NPCAI movement layout, reproduced against the previous 0.1.28 build; that adapter is unchanged in this update.

## Changes in 0.1.28

A brief yellow pulse on the game's existing HEALTH label and number makes actual storm health loss easier to notice. The pulse lasts 0.6 seconds and repeats at most once every two seconds during sustained damage, then restores native colors. It uses the existing Exposure indicators setting; setting it to zero hides the pulse along with the other indicators. No new HUD panel or sound. Damage, shelter, vehicle protection and storm tuning are unchanged.

Both variants compile against the installed game libraries; 680 automated assertions and native/variant inspections pass. The new pulse's visibility needs an in-game test.

## Changes in 0.1.27

Performance update: player shelter checks now keep their four-per-second cadence while driving, collider classification is cached, and full-protection calm weather skips shelter queries. Worn vehicle protection still recovers only in proper shelter, using one check per second after clearing. Ordinary shelter entry/exit is detected within 0.25 seconds; cab transitions, pause/clock jumps and large teleports refresh immediately.

Weather assets are discovered once per scene and refreshed at storm start. Camera references, NPC eligibility, rendering snapshots and the compass Canvas are cached. NPC shelter checks share a small per-frame budget. Storm appearance, audio, damage values, protection settings and other effect tuning are retained.

Both variants build and pass 661 automated regression assertions, native compatibility inspection and release/dev checks. Live performance and visual testing of this update is still pending.

## Storm hazards

- Strong wind slows the player and AI. AI never receive storm health damage.
- Exposed players take damage immediately. Caves, buildings, conex containers and covered boat hulls provide shelter and stop damage.
- Actual storm health loss briefly flashes the existing health label and number yellow, no more than once every two seconds.
- A vehicle initially provides 90% protection, falling gradually to 25% after five minutes in hazardous dust. Proper shelter restores protection gradually; exiting and re-entering a vehicle does not reset it.
- Dusty screen edges and a small icon above the compass show exposure, vehicle cover or shelter. Active storms interrupt exposed sleep, including in vehicles, without a sleep or wake health penalty. Sleeping in proper shelter works normally.
- Gusts apply modest horizontal pressure to the driven vehicle. The storm does not lift vehicles, suck off parts or directly damage vehicle parts. Dust lightning adds light and sound without strike damage.

## Installation and updating

Requires BepInEx 5 x64 and Apocasetter. Built against the installed Apocasetter 2.0.9; the dependency is not bundled.

1. Close Apocalypter.
2. Copy the ZIP's ApocaDustStorm folder into BepInEx/plugins in the Apocalypter game folder, merging the existing mod folder. The DLL belongs at BepInEx/plugins/ApocaDustStorm/ApocaDustStorm.dll.
3. Start the game and adjust the mod through Apocasetter's MODS menu.

Existing settings are retained in BepInEx/config/local.apocalypter.duststorm.cfg. Replace the previous storm DLL when updating; keep only one copy. The gearbox and chase-camera mods remain separate.

## Settings and timing

Automatic storms can arrive in daytime or at night. Defaults are a random 5-15 minutes of normal play, with a 90-second build-up and 75-second clearing, adjusted for shorter storms. After five minutes of calm weather, each eligible minute has a 6% chance of starting a storm. An arrival is not guaranteed immediately after loading.

Storms use a random north, south, east or west wind with directional variation. Storm appearance, chance, duration, wind, debris, audio, lightning, headlights, vehicle buffeting, movement resistance, player damage and indicators are adjustable. All thirteen Effects sliders default to 1.00; your saved choices are preserved. Set Player exposure damage to 0 for no health damage; exposed sleep is still interrupted.

The mod's automatic storms operate independently of the vanilla Dust Storm Off switch by default, so vanilla storms may remain off. Enable Follow vanilla storm switch if you want that option to also disable this mod's automatic storms.

## Release and development builds

This release has no storm or lightning testing hotkeys. Normal automatic weather remains available.

ApocaDustStorm-0.1.29-Dev.zip is a separate installable development variant with the testing controls. Install either the release or dev variant, replacing the same DLL; never install both together. Both use the same plugin identity and settings file. Old preview-key values may remain in the config but cannot trigger the release.

ApocaDustStorm-0.1.29-Developer.zip contains source and verification material only. It is not an installation package. The release installation ZIP contains the DLL, README and in-game MODS menu icon. Source and verification remain separate.

## Compatibility and credits

ApocaChaseCamera remains a separate mod. The optional NPCAI movement adapter has a known layout mismatch with the installed version used for testing; this also occurs with the previous 0.1.28 build. AI health remains untouched. Other weather/rendering mods may need a compatibility test.

Storm atmosphere inspired by Mad Max (2015). Thanks to Sawyer, creator of Apocalypter. Native wind and food assets are loaded from the installed game and are not redistributed. This mod adds no telemetry or network requests.

## Changes in 0.1.26

The release compiles out the storm/lightning testing key handlers and their settings. The dev build retains F8 and N. Show test status becomes Show storm notices, preserving the previous preference. Gameplay and effect tuning from the user-tested 0.1.25 build are retained, including first-person fog and post-storm lighting recovery.

Archive layout: the repackaged installation ZIP opens directly to the mod's folder. Copy that folder into BepInEx/plugins. For an older ZIP that opens to a BepInEx folder, merge that folder into the game folder instead. Source and verification files are supplied separately.


## For other modders

This repository shares the 0.1.29 original mod source and focused verification fixtures. Download the installation ZIP from [Releases](https://github.com/Jaime-Wolf/Dust-storms/releases/tag/v0.1.29).

See [COMPATIBILITY.md](COMPATIBILITY.md) for integration details and [LICENSE.md](LICENSE.md) for reuse permissions. Separate compatibility patches are allowed; copying code into another mod, bundling or reuploading this mod, or distributing modified versions requires permission from Jaime-Wolf. Source visibility does not grant general reuse permission.

Build with `Source/build.ps1` (release) or `Source/build.ps1 -Development` (dev), using your installed game dependencies. Run `Verification/test.ps1` and `Verification/harmony-compat.ps1` for regressions; `Verification/inspect-build.ps1` and `Verification/check-variants.ps1` inspect compiled variants. See [verification notes](Verification/README.md) for results and limitations. No game binaries, decompiled game classes, extracted assets or native inspection dumps are included in this repository.

## MODS menu icon

The installation ZIP now includes `icon.png` beside the DLL for the in-game MODS menu. Keep it in the mod folder. This packaging refresh leaves the gameplay code and version unchanged.

