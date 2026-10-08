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

Baseline: 234 model assertions, 364 simulated runtime assertions and 25 installed-Harmony controlled fixture assertions (623 total). Both DLLs passed native target/rendering/hazard inspection. Variant inspection checks release without testing keys versus dev with F8/N; 286 shared gameplay methods and embedded literal-array data matched.

The user confirmed preceding 0.1.25 gameplay working. 0.1.26 retains that gameplay/effect tuning and separates release/dev controls. New compatibility patches still need an actual game test with the target mod.

No proprietary dependency binaries, game assets, recordings or local historical research dumps are included in this folder. See [COMPATIBILITY.md](../COMPATIBILITY.md) and [LICENSE.md](../LICENSE.md).
