# ApocaDustStorm 0.1.44 - development build

This optional installable variant has the same weather and gameplay as the normal release, with testing controls retained:

- F8 starts a gradually approaching test storm; press again to fade it out over 30 seconds.
- N previews cosmetic dust lightning during visible storm dust.
- Saved custom bindings remain supported through the MODS menu. None disables a key.

Close the game and copy the ZIP's ApocaDustStorm folder into BepInEx/plugins, replacing the normal DLL. Install one variant only: normal/dev share the same plugin identity, installation path and configuration. The normal release has no testing hotkeys. The separate Developer ZIP is source/verification only.

Test storms include normal exposure damage and sleep interruption. Set Player exposure damage to 0 for harmless visual testing; exposed sleep is still interrupted. Settings and ordinary random storms work in both variants. The five-second entry shake, rolling dust front, terrain blending and lighting recovery are identical to the normal build.

Requires BepInEx 5 x64 and Apocasetter. Native assets and dependencies are not bundled. No telemetry or networking. See README.md, COMPATIBILITY.md and LICENSE.md in the source repository for details.
