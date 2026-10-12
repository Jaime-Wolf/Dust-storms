# ApocaDustStorm 0.1.44 verification

The author confirmed the latest build working in game after testing storm visuals, post-storm lighting recovery and the five-second entry shake. The release packages reuse the exact tested 0.1.44 production DLLs; only packaging and documentation were promoted for publication.

- 335 model + 754 controlled runtime + 39 real-Harmony fixture assertions pass: 1128 total. Re-run during release preparation.
- Native inspection passes with -SkipOptionalNpc: all 16 Harmony targets exist. The optional NPCAI adapter is unchanged and has a known installed-layout mismatch; its compatibility is not claimed.
- Normal/dev inspection passes: 474 shared gameplay methods match. Normal has no preview fields, bindings, test HUD or test-key calls. Dev retains F8/N and saved custom bindings. Both retain the original plugin identity.
- Both production variants previously compiled against installed game dependencies without warnings. The Harmony test fixtures emit two unused-field warnings; those warnings are confined to test doubles.
- The 0.1.44 performance preservation guard passed against the preserved 0.1.39 baseline with -LightingRecovery: 71 protected root methods and local particle construction/fog palette remain unchanged, apart from the three deliberate StormLighting.Scan/Recover/Clear exceptions.
- Lighting recovery uses current native Azure ambient inputs once fully calm, with at most four clean sky-light requests around 0/1/3/6 seconds. No mod ambient-probe assignment or idle refresh loop. Current day/night, invalid/disabled controllers and active render scopes are guarded.
- Installation ZIPs contain exactly ApocaDustStorm/{ApocaDustStorm.dll,icon.png,README.md,LICENSE.md}. The icon is 128x128. Source/verification remain separate and exclude dependency DLLs, extracted assets, saves, decompiled game code and native inspection dumps.

The checks do not simulate Unity GPU rendering or PhysX. No benchmarked FPS improvement or general compatibility guarantee is claimed. The retained optional NPCAI adapter mismatch is separate from the tested native player/camera/weather behavior.

Run Verification/test.ps1, Verification/harmony-compat.ps1, Verification/inspect-build.ps1 -SkipOptionalNpc and Verification/check-variants.ps1 after building both variants. Optional compiled preservation comparison requires your own previous mod DLL via Verification/performance-build.ps1 -BaselinePath <0.1.39 mod DLL> -LightingRecovery; that historical binary is not bundled with the source.
