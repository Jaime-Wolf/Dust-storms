# ApocaDustStorm 0.1.28 - development build

Installable development variant of the same worldwide dust-storm mod. It retains the gameplay and effect tuning of the release, with testing controls enabled:

Version 0.1.28 adds a 0.6-second yellow pulse on the existing health label and number after actual storm health loss, at most once every two seconds. It uses Exposure indicators and preserves native HUD colors. Both variants have the same pulse; its visibility still needs an in-game test.

- F8 starts a gradually approaching test storm; press again to clear it over 30 seconds.
- N previews dust lightning during visible dust when Dust lightning is enabled. A short playback cooldown applies.
- Change the preview keys in the MODS menu under Controls; None disables a key. The former F9 default migrates to N.

Test storms include normal exposure and movement hazards. Set Player exposure damage to 0 if you want harmless visual testing. This does not disable exposed sleep interruption.

Close the game and copy the ZIP's ApocaDustStorm folder into BepInEx/plugins in the game folder. Replace BepInEx/plugins/ApocaDustStorm/ApocaDustStorm.dll. Requires BepInEx 5 x64 and Apocasetter; dependencies are not bundled. Never install release and dev DLLs together: they share the same identity, installation path and settings file. Switching variants preserves settings. The release ignores any saved testing keys.

The release ZIP has no testing hotkeys. The separately named -Developer.zip is source/verification only, not an installable dev build. Both installable archives contain only the DLL and README files.

To rebuild from the source archive, run Source/build.ps1 for release or Source/build.ps1 -Development for dev. Development uses the APOCA_DEV compile symbol and writes to Source/build-dev. Verification/check-variants.ps1 checks both compiled variants after building them.

Storm inspiration: Mad Max (2015). Thanks to Sawyer, creator of Apocalypter. No telemetry or network requests. Existing gearbox/camera projects remain separate; the shelved road-audio mod is not included.

Archive layout: the repackaged installation ZIP opens directly to the mod's folder. Copy that folder into BepInEx/plugins. For an older ZIP that opens to a BepInEx folder, merge that folder into the game folder instead. Source and verification files are supplied separately.

0.1.27 includes the shelter/discovery/camera/NPC/rendering performance changes. Automated checks passed; in-game performance testing is pending. Appearance, sounds and hazard tuning are retained.

