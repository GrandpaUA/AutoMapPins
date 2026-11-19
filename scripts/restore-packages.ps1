param(
    [string] $NuGetVersion = "6.10.0"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$toolsDir = Join-Path $repoRoot ".tools"
$nugetExe = Join-Path $toolsDir "nuget.exe"
$packagesConfig = Join-Path $repoRoot "packages.config"
$packagesDir = Join-Path $repoRoot "packages"

if (!(Test-Path $nugetExe)) {
    New-Item -ItemType Directory -Force -Path $toolsDir | Out-Null
    $url = "https://dist.nuget.org/win-x86-commandline/v$NuGetVersion/nuget.exe"
    Invoke-WebRequest -Uri $url -OutFile $nugetExe
}

& $nugetExe restore $packagesConfig -PackagesDirectory $packagesDir -NonInteractive
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
