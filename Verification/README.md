# Verification

Run these from the repository root on Windows. `test.ps1` needs the .NET Framework compiler only; it compiles original model/runtime fixtures into a temporary executable and cleans it up. The runtime stubs model contracts; they are not game assemblies and do not render Unity shaders or simulate PhysX.

```powershell
& .\Verification\test.ps1
# These checks need your installed game/loader/dependencies:
$game = 'D:\Steam\steamapps\common\Apocalypter'
& .\Verification\harmony-compat.ps1 -GameDir $game
& .\Source\build.ps1 -GameDir $game
& .\Source\build.ps1 -GameDir $game -Development
& .\Verification\inspect-build.ps1 -GameDir $game
& .\Verification\inspect-build.ps1 -GameDir $game -PluginPath .\Source\build-dev\ApocaDustStorm.dll
& .\Verification\check-variants.ps1 -GameDir $game
```

Version 0.1.27: 234 model assertions, 402 simulated runtime assertions and 25 installed-Harmony controlled fixture assertions (661 total). Both DLLs passed native target/rendering/hazard inspection. Variant inspection checks release without testing keys versus dev with F8/N; 305 shared gameplay methods and embedded literal-array data matched. See [0.1.27-check-results.txt](0.1.27-check-results.txt) for the local pre-publication check record.

The 38 new runtime assertions verify bounded shelter raycasts while driving, skipped calm queries, collider/NPC/camera caches, bounded and fair NPC checks, discovery cadence, reusable render snapshots and HUD Canvas lookup caching. These simulated counters verify the intended work reduction; they do not measure live FPS.

The preceding gameplay was confirmed working by the author. Version 0.1.27 retains the effect/hazard tuning and release/dev controls split; actual performance and visual testing of this update is pending. Test first/chase cameras, driving over bumps, shelter entry/exit and recovery, NPC movement, lighting restoration and scene/pause transitions in game. New compatibility patches also need an actual game test with their target mod.

No proprietary dependency binaries, game assets, recordings or local historical research dumps are included in this folder. See [COMPATIBILITY.md](../COMPATIBILITY.md) and [LICENSE.md](../LICENSE.md).
