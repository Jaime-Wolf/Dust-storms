# ApocaDustStorm 0.1.28 verification

Prepared October 8, 2026. Adds only a short yellow pulse to the native player health label/number after actual storm health loss. It lasts 0.6 seconds, has a two-second trigger interval and uses the existing Exposure indicators setting. Native health values, exposure rate, shelter, sleep and protection rules are retained.

## 0.1.28 validation

- Release and dev compile successfully against installed game/loader/dependency libraries, with no compiler warnings.
- 234 model, 421 simulated runtime and 25 installed-Harmony controlled-fixture assertions passed: **680 total**, including 19 new health-pulse assertions.
- Health-pulse cases cover actual outdoor/cab health loss, fade/cooldown, cached HUD binding, native transparency, other-mod color ownership, pause/disable/reset, hidden HUD, shelter, zero damage/indicators and time jumps.
- Both native inspections pass. Release testing controls remain compiled out; F8/N remain in dev. The 316 shared gameplay method bodies and literal data match between variants.
- Read-only inspection of the current installed game bundle confirms the two native Text paths: Canvas/SurvivalCanvas/HealthLable and its HealthText child. No screen rectangle guess or replacement UI is used. HUD lookups happen only while a pulse is active and missing references retry at most once per second.
- StormRunner's existing LateUpdate lighting recovery is retained; the pulse runs afterward. Actual damage triggers it from PlayerStormHazards.Tick only after a real health decrease. Pause, player/session reset and disabled indicators restore only the mod's own tint.

Evidence is in Verification/0.1.28-check-results.txt, 0.1.28-harmony.txt, 0.1.28-native-release.txt, 0.1.28-native-dev.txt and 0.1.28-variants.txt. Source and verification are in a separate Developer archive; the release/dev install ZIPs contain only the mod DLL and README. The 0.1.27 source/build baseline is retained under Backups/ApocaDustStorm-0.1.27-before-health-flash-0.1.28.

This update is prepared locally, without game installation or external publishing. The latest previously published GitHub version remains 0.1.27. In-game test: compare the pulse during actual outdoor and cab damage, then enter shelter and pause/disable; confirm native text remains readable and the pulse restores its normal color. No damage should occur from testing sleep or clock jumps.

## Previous 0.1.27 performance verification

Prepared October 8, 2026. This update addresses repeated shelter raycasts, scene searches, camera discovery, NPC hierarchy work and per-render allocations. The previously tested storm appearance, sounds, hazard values and native lighting recovery remain the baseline. In-game performance and visual testing of 0.1.27 is still pending.

## Performance changes

Player shelter queries run at four Hz during storms; normal movement does not bypass that limit. Cab entry/exit, pause resumption, clock jumps and teleports over 100 m force a fresh result. Crossing ordinary shelter geometry is detected on the next scheduled query, within 0.25 seconds. Full-protection calm weather needs no shelter queries; worn vehicle protection uses one Hz so proper-shelter recovery still works after a storm.

Collider classification is cached for five seconds, invalidated immediately on direct reparenting and bounded to 2048 entries. Live trigger, enabled, dynamic-rigidbody and actor-descendant exclusions precede the cache. Scene cleanup and storm start clear it. Probe lengths, recognized shelter names and unnamed roof/wall rules are retained.

Full-scene weather/FSM/light discovery occurs once per scene. Storm starts refresh late-created lighting/Azure assets; entering a different vehicle refreshes its local headlights. The FSM enable hook observes the native storm switch. Camera discovery is cached, with live enabled-state checks for view switching, owner/parent/scene invalidation and throttled missing-camera retries.

NPC movement hooks return before name/eligibility work in calm weather. During a storm they cache eligibility and share a FIFO budget of two shelter queries per rendered frame. Recurring checks have staggered deadlines. Newly queued actors retain native movement until their first result, and no AI health writes were added.

Fog snapshots, lighting arrays and Azure material/global buffers are reused while capturing current native values on every draw. Nested render passes retain their outer ownership and restoration contract. The notice style and compass Canvas are cached; HUD repaint no longer searches for the Canvas.

## Automated validation

- 234 pure model assertions, 402 simulated runtime assertions and 25 installed-Harmony controlled fixture assertions passed: 661 total.
- Runtime checks include 38 new call-count, cache invalidation and buffer-reuse assertions.
- Both release and dev compile against installed game/loader/dependency references and pass native target/rendering/hazard inspection, including the installed NPCAI movement layouts.
- Release/dev inspection confirms no test-key fields/bindings/handlers in release, F8/N in dev, and 305 matching shared gameplay method bodies plus matching literal arrays.
- All eleven native Harmony targets and both optional NPCAI targets remain valid. Native ambient-probe ownership, one guarded GI recovery, interrupted render cleanup and ImageEffectOpaque pass 0 are retained.

See Verification/0.1.27-check-results.txt and PerformanceRuntimeChecks.cs for measured simulation counters. These tests do not simulate Unity rendering, PhysX or live frame times.

## Packaging and saved state

Release: Releases/PluginFolder/ApocaDustStorm-0.1.27.zip. Installable dev: Releases/PluginFolder/ApocaDustStorm-0.1.27-Dev.zip. Each ZIP contains exactly ApocaDustStorm/ApocaDustStorm.dll and ApocaDustStorm/README.md. Copy the folder into BepInEx/plugins, replacing the previous variant; install only one variant. Source and original verification fixtures are in a separate Developer archive; no game binaries/assets or native inspection dumps are included there.

The saved 0.1.26 project baseline is backed up before promotion. Previous release archives are retained. Version 0.1.27 is prepared locally only: the existing GitHub 0.1.26 release has not been changed. No game install, live configuration edit, camera/gearbox modification or external publication was performed.

## In-game retest

Check clear-weather movement/driving, storm driving in first/chase view, view switching and vehicle transitions, shelter entry/exit, protection recovery after clearing, exposed/sheltered sleep, a group of NPCs, pause/scene reload, and day/night lighting after the storm. Compare frame times in the same scene and camera, with the same other mods, against 0.1.26.
