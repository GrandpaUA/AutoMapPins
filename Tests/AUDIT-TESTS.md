# Additional release-audit regressions

From the repository root on Windows, with the .NET SDK and existing net48 reference assemblies restored:

```powershell
python scripts/Run-AuditRegressionTests.py --harmony "<BepInEx>/core/0Harmony.dll"
```

Use the installed BepInEx Harmony library, not a freshly substituted package. The runner logs its SHA-256. `--out` optionally names a **new** directory; existing directories are refused. Default output is a unique directory under artifacts. There is no game launch, network service use, deployment or cleanup.

The runner executes three isolated processes:

1. `DiscoveryRadiusTests`: 8 capture, expiry, remote-location and nonfinite-input cases against the complete actual radius service, with world/plugin stubs.
2. `DiscoveryRadiusExtendedTests`: 14 multiplier, integration fallback/recovery, cache-expiry and diagnostic cases against that same source.
3. `HarmonyIntegration`: 8 nested success/exception combinations using real Harmony on .NET Framework 4.8. Exact sharing hook text is extracted from production at execution time, hashed and compiled. Explicit extraction guards fail if the production layout changes. Model/identity/world adapters are deliberately narrow; the full ownership/metadata algorithm is tested separately by bounded gameplay tests. The fixture rejects both live AMP instances and serialized AMP metadata in the shared collection, including exceptional nested calls.

This establishes local Windows CLR hook invocation and state restoration, **not** Valheim Mono compatibility or real gameplay. Keep running the existing bounded gameplay and icon asset suites as well. Generated projects are disposable evidence, not maintained copies of production logic. The checked-in runner and test sources do not depend on the original ignored release-audit generators.

Packaging is independently exercised by `scripts/Test-ThunderstorePackage.py`; see `scripts/PACKAGING.md` for the command and artifact immutability contract.
