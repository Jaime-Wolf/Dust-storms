# ApocaDustStorm 0.1.44 source

These are the original mod sources. Plugin.cs defines identity, settings and version; StormRunner.cs coordinates weather and effects. See the root compatibility guide and license for integration boundaries and reuse permissions.

On Windows, use the .NET Framework compiler and dependencies from your local Apocalypter/BepInEx/Apocasetter installation:

```powershell
$game = 'D:\Steam\steamapps\common\Apocalypter'
& .\Source\build.ps1 -GameDir $game
& .\Source\build.ps1 -GameDir $game -Development
& .\Verification\test.ps1
& .\Verification\harmony-compat.ps1 -GameDir $game
& .\Verification\inspect-build.ps1 -GameDir $game -SkipOptionalNpc
& .\Verification\check-variants.ps1 -GameDir $game
```

The normal DLL is written to Source/build and the optional F8/N development DLL to Source/build-dev. Install one variant only. No dependencies are bundled. Both build directories are excluded from the source repository. See ../Verification.md for results and limits; the optional NPCAI layout mismatch remains outside this update.
