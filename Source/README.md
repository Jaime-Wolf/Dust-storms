# Current source - ApocaDustStorm 0.1.27

These are the current original mod sources. `Plugin.cs` defines the identity, settings and version; `StormRunner.cs` coordinates weather and runtime effects. See the root [compatibility guide](../COMPATIBILITY.md) for the module map and integration boundaries, and [permissions](../LICENSE.md) for reuse rules.

Build from the repository root with `Source/build.ps1 -GameDir <your game folder>`. Add `-Development` to retain F8/N testing controls. Dependencies must come from your local game/BepInEx/Apocasetter installation; none are bundled. Release/dev output folders are ignored by Git.

Version 0.1.27 reduces shelter queries, recurring scene searches and runtime allocations. Storm appearance and hazard tuning are retained. Automated checks passed; live performance testing is pending.
