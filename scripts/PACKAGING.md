# Local package verification

Release packaging runs `New-ThunderstorePackage.ps1` after ILRepack through the project target when `BuildThunderstorePackage=true`. It reads the actual DLL assembly version and requires the source manifest version to match. It does not rewrite the manifest. Update version and changelog deliberately before a candidate build; matching version numbers alone do not establish readiness.

The archive is named `AutoMapPins_v<major>.<minor>.<patch>.zip` in `PackageOutputDir`. Existing archives are **never overwritten**. Choose a fresh output directory for another audit run. Nothing is deployed or published by the packaging script. Disable deployment explicitly in build commands.

```powershell
dotnet build AutoMapPins.csproj -p:Configuration=Release -p:BuildThunderstorePackage=true -p:DeployToGame=false -p:PackageOutputDir=artifacts/package-check-unique -p:BepInExPath="<installed BepInEx directory>"
```

To package an already merged DLL without rebuilding:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/New-ThunderstorePackage.ps1 -DllPath "<merged DLL>" -OutputZip "<new archive path>"
```

The output parent must exist for direct script invocation. The project target creates `PackageOutputDir` when needed. Package sources are README, changelog, license, manifest, icon, the merged DLL, and the `ThunderStorePackage/config` and `ThunderStorePackage/plugins` trees. Relative paths are preserved. The legacy root `categories.vanilla.yaml` template remains excluded. Linked payload files/directories and duplicate case-insensitive destinations are rejected. Existing `TempPackage` content is neither read nor deleted.

Regression tests use fixture files and the real assembly identity of the supplied DLL. They exercise the production script, not a duplicated implementation:

```powershell
python scripts/Test-ThunderstorePackage.py --dll "<merged DLL>"
```

`--version` optionally supplies the expected fixture version; otherwise it is read from the source manifest. Fixtures remain in a uniquely named artifacts directory for inspection. Tests require Python, Windows PowerShell, and a compiled AutoMapPins DLL. They do not contact services or use live game configuration.

Validation runs before archive creation. A later I/O failure can leave a partial archive; the command fails and will not replace that file on retry. Inspect it and choose a new output path rather than treating existence as success. Entries use fixed timestamps and sorted paths for byte reproducibility on the same compression/runtime implementation; cross-runtime identical bytes are not guaranteed.

Packaging does not prove game compatibility or validate Thunderstore image dimensions, dependency availability, third-party rights, or gameplay. Before any delivery, verify the exact archive entries, embedded DLL hash/version, release notes, and scenario-specific gameplay evidence. Audit-only archives under the existing tester version must not be distributed as new builds.
