# Creation-hook Harmony integration tests

```powershell
python scripts/Run-CreationHookHarmonyTests.py --harmony C:/path/to/BepInEx/core/0Harmony.dll
```

The runner compiles the entire unmodified production [creation-hook source](<../Patches/AllCreatePinPatches.cs>) with [test fixtures](<CreationHookHarmonyTests.cs>) on net48 and uses the supplied real Harmony assembly. Local framework references must already exist. No game is launched, package installed, or deployed DLL modified. Each run creates a new retained artifact directory and prints source/dependency SHA256 hashes.

45 checks cover:

- All12Harmony class attributes register successfully against the fixture types and invoke both original and postfix.
- Repeated callbacks and overlapping TreeBase/Destructible hooks add one PinComponent.
- Height just below/exactly at/above limit, NaN and positive infinity.
- Destructible defers when Pickable is present; Beehive hook excludes Piece.
- Removing this Harmony owner makes a subsequent TreeBase callback stop adding PinComponent (negative control).

Limitations: GameObject/component APIs, PinComponent and lifecycle originals are stubs. The test does not establish real Unity AddComponent/Awake semantics, original exceptions, actual prefab component composition, hook timing, object destruction, Mono behavior, or visual pin correctness. Piece exclusion applies only to Beehive's hook, not to other hooks that might run on the same object. No claim of player-built hive opt-in is justified by this test.
