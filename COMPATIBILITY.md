# Compatibility guide - ApocaDustStorm 0.1.27

This guide describes the current source, not a stable public integration API. Most implementation types are internal. Build a separate compatibility plugin against the installed mod where appropriate, or use guarded reflection; check the loaded version and fail safely if expected members or native layouts differ. Do not bundle a second ApocaDustStorm DLL. Read [LICENSE.md](LICENSE.md) for source reuse and redistribution rules.

## Identity, state and dependencies

- Plugin GUID / Harmony owner: `local.apocalypter.duststorm`.
- Entry point: `ApocaDustStorm.Plugin`; public constants `GUID` and `VERSION`.
- Required runtime dependency: Apocasetter. Optional soft dependency: `com.denis.apocalypter.npcai`.
- Current internal state: `StormRunner.Model`, `StormRunner.Strength`, `StormRunner.Gust`, `StormRunner.View`, `StormRunner.CanRender`. Strength/Gust are smoothed runtime values; `Model.Active` and the remaining visual fade are distinct. Treat these as read-only observations for integrations.
- Config is stored at `BepInEx/config/local.apocalypter.duststorm.cfg`. Both compiled variants share identity/config. Preview bindings and input calls exist only with `APOCA_DEV`.
- Declare an optional BepInEx dependency on the storm GUID if your plugin should also work without it. Use a required dependency when your patch cannot function without the original mod.

## Source map

| Area | Files |
| --- | --- |
| Settings, lifecycle and scheduling | `Plugin.cs`, `StormRunner.cs`, `StormDiscovery.cs`, `StormModel.cs`, `StormClock.cs`, `NativeStormClock.cs` |
| Fog, camera matrices and lighting | `StormCameraCache.cs`, `StormFog.cs`, `StormView.cs`, `StormDistanceFog.cs`, `AzureAtmosphere.cs`, `StormLighting.cs`, `StormHorizon.cs` |
| Dust and colliding sticks | `StormVisuals.cs`, `StormDebris.cs`, `DebrisMotion.cs`, `DustMotion.cs` |
| Wind/audio/lightning | `WindMath.cs`, `VehicleWind.cs`, `WindAudio.cs`, `SandAudio.cs`, `SandSound.cs`, `DustLightning.cs`, `DustDischargeModel.cs`, `DischargeSound.cs` |
| Shelter, player hazards, protection and HUD | `StormShelter.cs`, `PlayerStormHazards.cs`, `StormHazardModel.cs`, `VehicleProtectionModel.cs`, `ExposureHud.cs`, `ExposureHudMath.cs` |
| Movement and sleep | `StormMovement.cs`, `StormAIMovement.cs`, `StormSleep.cs` |
| Native-storm suppression / edible loot | `NativeStormGuard.cs`, `WindblownLizards.cs`, `WindLootSchedule.cs`, `StormAssets.cs` |

## Weather, rendering and camera mods

The mod suppresses recognized native SandStorm prefabs while enabled: recognition requires a SandStorm ancestor and the `SandPlayer` FSM marker. It disables only tracked native storm renderers/colliders/behaviours, and guards native storm force/damage paths. Other weather mods that depend on those native storm components need explicit coordination.

`StormDiscovery` discovers native weather assets once per scene and refreshes lighting/Azure/clock references at storm start. The native enable hook observes newly enabled storm/switch FSMs. Vehicle entry refreshes that vehicle's local headlights. If a compatibility patch replaces weather objects during an active storm, coordinate discovery invalidation rather than adding recurring full-scene searches. `StormCameraCache` retains first/third-person camera references and invalidates them on player/parent changes and cleanup; it does not write camera transforms or matrices.

Fog and lighting changes are scoped to the selected gameplay camera. Global/material/light snapshots restore after drawing; an interrupted camera scope is unwound at the next Update. Avoid permanent overwrites of the same RenderSettings/material parameters inside that scope, and investigate Harmony/camera callback order if your weather mod also owns them.

`StormView` captures the geometry view/projection before drawing and retains it through same-frame post-render restoration. A camera can override matrices without moving its Transform: using Transform position alone is insufficient for integrations. `StormDistanceFog` runs at `ImageEffectOpaque`, uses the original color source and native simple-fog pass 0, and leaves the image callback's destination active. Preserve its native texture-sign handling; do not reintroduce a preparation blit or rotate the camera to solve image orientation.

`DustLightning.AlignView` optionally discovers `ApocaChaseCamera.ChaseView.Apply(Camera)` by reflection. The bridge is intended to make rendered-camera alignment independent of PreCull subscription order. A changed camera type/method or a different camera mod may require a dedicated adapter. This is the integration point; it is not a required separate camera patch.

`StormLighting` leaves the generated ambient probe native and uses one guarded `DynamicGI.UpdateEnvironment` call after full clearing, in LateUpdate with no open camera overrides. Do not add per-draw ambient-probe assignment or repeated calm-frame GI refreshes.

## Movement and AI mods

Vanilla movement interception filters horizontal X/Z velocity in the native Movement FSM; gravity/jump vertical velocity is retained. Player input filtering is scoped to native player movement rather than vehicle throttle.

The optional NPCAI 1.2.0 adapter locates `NPCAI.Brain.BeforeSetVelocity(SetVelocity)` and `NPCAI.Idle.Drive(Ctl, float)` and rewrites exactly one Rigidbody velocity assignment in each expected path. It checks the controller/agent/Transform layout. A changed layout or velocity assignment count disables that optional adapter with a warning instead of guessing. If another transpiler changes these assignments, inspect combined patch output and order under the storm Harmony owner.

AI receive movement resistance only. Preserve the player-only health target; adding storm damage to AI would change the intended gameplay and can disrupt persistent NPCs.

The strength/pause checks precede NPC identity/component checks. Eligibility is cached per actor. Shelter checks share a FIFO budget of two per frame, with staggered recurring deadlines of at least 0.75 seconds. An actor awaiting its first check keeps native speed. Do not bypass that budget from a velocity hook.

## Shelter, sleep, vehicles and HUD

Shelter detection uses recognized building/cave/conex geometry and overhead/side collider probes. New shelter names or unusual collider layouts should be tested through `StormShelter` on foot and in vehicles. Vehicle protection is accumulated during hazardous driving and recovered only in proper shelter; entering/exiting a vehicle must not reset it.

Player shelter queries run every 0.25 seconds during storms; ordinary movement cannot trigger additional queries. Cab transitions, pause/clock jumps and large teleports force refresh. In calm weather, only worn vehicle protection needs shelter queries, at one-second intervals, to preserve shelter-only recovery. Collider classification is cached for five seconds, invalidated by immediate-parent changes and cleared at scene/storm boundaries; trigger, rigidbody and own-actor exclusions stay live. The classification cache is capped at 2048 entries. A patch that changes shelter names/components can call `StormShelter.Reset` through guarded reflection after its change, or allow cache expiry.

Sleep interception is scoped to the root Player Sleep FSM and its native SetTimeline call. It stops exposed sleep including in vehicles without applying health/fatigue/blur penalties. Proper shelter retains native healing and clock advancement. New sleep systems may bypass these native paths and require a separate adapter.

Vehicle buffeting applies capped horizontal force to the current grounded driver vehicle. It does not write native velocity/RPM or apply explosion force. Preserve those limits when integrating different vehicle layouts.

The exposure badge anchors above `Canvas/Compass` using its native RectTransform. A HUD mod that replaces/moves that hierarchy should adapt the anchor instead of adding a second exposure HUD.

The Canvas lookup is cached outside OnGUI and refreshed when the compass parent changes. Rendering snapshots reuse storage but recapture current native fog/material/light values before every camera scope; avoid retaining their internal buffers as an integration contract.

## Native Harmony targets

| Native target | Purpose |
| --- | --- |
| `PlayMakerFSM.OnEnable` | Recognized native storm suppression |
| `Explosion.DoExplosion` | Stop recognized native storm force |
| `Tornado.OnTriggerStay` | Stop recognized native storm suction |
| `SendEvent.OnEnter` / `SendEvent.OnUpdate` | Stop recognized TornadoDamage events |
| `EnviroSkyRenderingLW.RenderFog` | Scoped native fog/view correction |
| `UnityEngine.AzureSky.AzureFogScattering.OnRenderImage` | Scoped Azure atmosphere/view correction |
| `GetAxisKeyAxis.DoGetAxis` | Player movement resistance |
| `SetVelocity.DoSetVelocity` | Vanilla AI horizontal velocity filtering |
| `Fsm.SwitchState` | Exposed player sleep-entry guard |
| `CallMethod.DoMethodCall` | Exact player-sleep clock-advance guard |

The two optional NPCAI methods above are discovered separately. `Verification/inspect-build.ps1` checks the installed native signatures and NPCAI assignment layout; a matching signature does not prove two mods work together in game.

## Compatibility test/report checklist

Use the dev build for controlled F8/N previews. Test clear weather, approach, peak and full clearing; first/third-person switching; day/night; pause and scene changes; shelter entry/exit; five minutes of vehicle protection wear and shelter recovery; exposed versus sheltered sleep; NPC movement without health damage. Check clean lighting and upright first-person rendering after clearing.

Version 0.1.27 passed 234 model, 402 simulated runtime and 25 controlled installed-Harmony assertions (661 total), native target inspections and matching bodies/literal data for 305 shared release/dev gameplay methods. Call-count checks cover the performance changes, but actual FPS, Unity rendering and PhysX require an in-game test. Live performance testing of this update is pending.

Report both mod versions, game build, exact reproduction steps and relevant BepInEx log excerpts. Include screenshots/video for rendering issues and the affected source/native method for patch conflicts. Remove personal paths or other private details from shared logs. Keep proposed changes focused on the compatibility issue and preserve release/dev separation.
