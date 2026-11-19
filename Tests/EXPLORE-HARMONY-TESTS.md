# Explore hook binding regression

Run from the repository root on Windows with a local .NET SDK, restored net48 reference assemblies and installed BepInEx Harmony:

```powershell
python scripts/Run-ExploreHarmonyTests.py --harmony "C:/path/to/BepInEx/core/0Harmony.dll"
```

[Runner](<../scripts/Run-ExploreHarmonyTests.py>) extracts the exact current Explore attribute/method block from [production](<../Patches/MinimapPatch.cs#L175-L181>). Extraction fails closed if boundary markers change. It emits an XML-escaped project, hashes Harmony and the extracted block, builds against local net48 references and runs the executable. No download, game launch, network, deployment or source mutation. Each invocation creates a fresh retained artifacts directory; an existing `--out` directory is refused.

[Fixture](<ExploreHarmonyIntegration.cs>) verifies:

- Parameter names `p` and `center` both bind through production `__0`/`__1`.
- Observer is installed before two radius-changing prefixes, exercising subsequent patch-chain rebuilds.
- Both high/low-priority orders capture the exact radius received by the original (50→1050 and20→420).
- Position and original call count remain correct.
- A deliberately legacy `center`-named observer is rejected on a `p` original. This negative control reproduces the historical failure rather than merely demonstrating a permissive test stub.

Five scenarios should report PASS. The fixture uses real Harmony but synthetic methods, a Vector3 stub and a capture-recorder DiscoveryRadiusService. It does not test Unity ABI, game patch discovery, actual Exploration/Sailing assemblies, providers, network, or Mono runtime. Actual radius-service behavior is covered separately by [audit regressions](<AUDIT-TESTS.md>). Prefixes multiplying commute; this test does not prove compatibility with arbitrary third-party postfixes, skipped originals or finalizers.

The installed Harmony used during development requires `UnpatchSelf()`; the obsolete `UnpatchAll(string)` is a compile error. If targeting a different Harmony release, inspect its API rather than weakening the test.
