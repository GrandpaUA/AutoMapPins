# Build and checks

## Requirements

- Windows and .NET SDK 8; the mod targets .NET Framework 4.8.
- Local Valheim installation for the full release build. Game assemblies are not distributed in this repository.
- BepInExPack for Valheim. Pass the directory containing `core/BepInEx.dll` as `BepInExPath`.
- Python for package regression checks; Windows PowerShell for the scripts.

Restore the pinned NuGet dependencies into the ignored `packages/` directory:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/restore-packages.ps1
```

## Source regressions

These compile production sources against runtime stubs without Valheim or a live server:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Run-BoundedGameplayFixTests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Run-IconAssetTests.ps1
git diff --check
```

GitHub Actions runs these suites. They do not prove Unity UI behavior or live multiplayer compatibility.

## Release DLL and package

Replace the example BepInEx path with your installed version. For r2modman it is typically below `%APPDATA%/r2modmanPlus-local/Valheim/cache/denikson-BepInExPack_Valheim/<version>/BepInExPack_Valheim/BepInEx`. The helper `scripts/build.ps1` also accepts explicit `-GamePath` and `-BepInExPath`.

```powershell
dotnet build AutoMapPins.sln -p:Configuration=Release -p:DeployToGame=false -p:BepInExPath="C:/path/to/BepInEx"
```

The merged output is `bin/Release/AutoMapPins.dll`. ILRepack embeds ServerSync and YamlDotNet; the build checks ServerSync against local game assemblies. A normal build does not deploy to the game.

Create the Thunderstore ZIP in a fresh output directory:

```powershell
dotnet build AutoMapPins.sln -p:Configuration=Release -p:DeployToGame=false -p:BuildThunderstorePackage=true -p:PackageOutputDir=artifacts/my-candidate -p:BepInExPath="C:/path/to/BepInEx"
```

The package is `artifacts/my-candidate/AutoMapPins_v<version>.zip`. Existing archives are never overwritten. Manifest and DLL versions must match. See [scripts/PACKAGING.md](scripts/PACKAGING.md).

Validate packaging behavior and actual package notices:

```powershell
python scripts/Test-ThunderstorePackage.py --dll bin/Release/AutoMapPins.dll
python scripts/Test-LicensePayload.py artifacts/my-candidate/AutoMapPins_v2.3.17.zip
Get-FileHash bin/Release/AutoMapPins.dll -Algorithm SHA256
```

Do not distribute the build output directory: it can contain copied game/reference assemblies. Distribute the curated ZIP and provide matching source and notices. Follow [docs/RELEASE.md](docs/RELEASE.md) before calling a candidate stable.
