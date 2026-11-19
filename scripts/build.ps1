param(
    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Release",

    [string] $GamePath = $env:ValheimGamePath,

    [string] $BepInExPath = $env:BepInExPath,

    [switch] $Package,

    [switch] $DeployToGame
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$solution = Join-Path $repoRoot "AutoMapPins.sln"

if (!$BepInExPath) {
    $r2modmanBepInEx = Join-Path $env:APPDATA "r2modmanPlus-local\Valheim\cache\denikson-BepInExPack_Valheim\5.4.2202\BepInExPack_Valheim\BepInEx"
    if (Test-Path (Join-Path $r2modmanBepInEx "core\BepInEx.dll")) {
        $BepInExPath = $r2modmanBepInEx
    }
}

& (Join-Path $PSScriptRoot "restore-packages.ps1")
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$msbuildArgs = @(
    "msbuild",
    $solution,
    "/p:Configuration=$Configuration",
    "/p:Platform=Any CPU"
)

if ($GamePath) {
    $msbuildArgs += "/p:GamePath=$GamePath"
}

if ($BepInExPath) {
    $msbuildArgs += "/p:BepInExPath=$BepInExPath"
}

if ($Package) {
    $msbuildArgs += "/p:BuildThunderstorePackage=true"
}

if ($DeployToGame) {
    $msbuildArgs += "/p:DeployToGame=true"
}

& dotnet @msbuildArgs
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
