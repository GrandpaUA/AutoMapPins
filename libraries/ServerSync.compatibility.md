# ServerSync compatibility patch provenance

`libraries/ServerSync.dll` is a vendored ServerSync assembly with one narrow IL compatibility patch for current Valheim assemblies.

## Hashes

- Source-controlled original SHA-256: `8857A54A2E3C1E1FC7BF255799745C1D53F6A7090120D5EAF5A54F6A4B2C19BA`
- Patched `libraries/ServerSync.dll` SHA-256: `9A4EA6F34FDFC63FDEEFAC75520C2C5330C1CFCD9DD0709352A627B46B18E6BB`

## Rationale

Valheim now exposes `assembly_valheim` `ZRoutedRpc::Everybody` as an `Int64` literal constant (`0`). Older ServerSync IL still contains three `ldsfld System.Int64 ZRoutedRpc::Everybody` instructions. Loading a literal field with `ldsfld` is invalid for this runtime shape, so the vendored DLL replaces exactly those three field loads with `ldc.i8 0`.

## Reproduction

From the repository root, after restoring the original DLL from source control and with local Valheim/BepInEx build references available:

```powershell
git checkout -- libraries/ServerSync.dll
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Patch-ServerSyncCompatibility.ps1 -AssemblyPath .\libraries\ServerSync.dll -ValheimAssemblyPath .\bin\Release\assembly_valheim.dll -CecilPath .\bin\Release\Mono.Cecil.dll
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-ServerSyncCompatibility.ps1 -AssemblyPath .\libraries\ServerSync.dll -ValheimAssemblyPath .\bin\Release\assembly_valheim.dll -CecilPath .\bin\Release\Mono.Cecil.dll
```

## Recovery

To recover the unpatched vendored binary, restore it from source control:

```powershell
git checkout -- libraries/ServerSync.dll
```

Then rerun the patch command above if the current Valheim `assembly_valheim.dll` still has `ZRoutedRpc::Everybody` as the exact `Int64` literal constant `0`.

## Script safety limits

`Patch-ServerSyncCompatibility.ps1` fails closed unless all of the following are true: the target assembly name is `ServerSync`; the reference assembly name is `assembly_valheim`; `ZRoutedRpc::Everybody` is an `Int64` literal constant `0`; and the three known ServerSync methods still have the expected instruction counts and exact patch-site instruction indexes. It is idempotent for an already corrected DLL, refuses mixed partial state, and does not rewrite arbitrary literal fields or arbitrary integer constants.

`Test-ServerSyncCompatibility.ps1` treats missing input files as failures and rejects remaining `ldsfld`, `ldsflda`, or `stsfld` access to the exact Valheim `ZRoutedRpc::Everybody` field. It intentionally does not attempt broad field-hierarchy repair beyond this known compatibility issue.
