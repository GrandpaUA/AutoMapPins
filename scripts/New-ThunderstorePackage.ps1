param(
    [Parameter(Mandatory=$true)][string]$DllPath,
    [Parameter(Mandatory=$true)][string]$OutputZip,
    [string]$ProjectRoot
)
# Offline packager: never mutates source files, stages files, or replaces an archive.
$ErrorActionPreference = 'Stop'
if (!$ProjectRoot) { $ProjectRoot = Join-Path $PSScriptRoot '..' }
Add-Type -AssemblyName System.IO.Compression
$root = (Resolve-Path -LiteralPath $ProjectRoot).Path
$dll = (Resolve-Path -LiteralPath $DllPath).Path
$output = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputZip)
if (Test-Path -LiteralPath $output) { throw "Output already exists: $output" }
$identity = [Reflection.AssemblyName]::GetAssemblyName($dll)
if ($identity.Name -ne 'AutoMapPins') { throw 'Expected AutoMapPins assembly' }
$version = $identity.Version
$packageVersion = '{0}.{1}.{2}' -f $version.Major,$version.Minor,$version.Build
$manifestPath = Join-Path $root 'ThunderStorePackage/manifest.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.version_number -cne $packageVersion) {
    throw "Manifest/DLL version mismatch: $($manifest.version_number) vs $packageVersion"
}
# Hashtable keys are case-insensitive, matching Windows installation destinations.
$files = @{}
function Add-Payload([string]$source, [string]$entry) {
    $item = Get-Item -LiteralPath $source
    if ($item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw "Not a regular payload file: $source"
    }
    $entry = $entry.Replace('\','/')
    if ($files.ContainsKey($entry)) { throw "Duplicate package destination: $entry" }
    $files[$entry] = $item.FullName
}
foreach ($name in @('README.md','CHANGELOG.md','LICENSE')) {
    Add-Payload (Join-Path $root $name) $name
}
Add-Payload $manifestPath 'manifest.json'
Add-Payload (Join-Path $root 'ThunderStorePackage/icon.png') 'icon.png'
Add-Payload $dll 'plugins/AutoMapPins.dll'
foreach ($folder in @('config','plugins')) {
    $base = Join-Path $root "ThunderStorePackage/$folder"
    if (!(Test-Path -LiteralPath $base)) { continue }
    $base = (Get-Item -LiteralPath $base).FullName
    if ((Get-Item -LiteralPath $base).Attributes -band [IO.FileAttributes]::ReparsePoint) {
        throw "Linked payload directory is unsupported: $base"
    }
    foreach ($item in Get-ChildItem -LiteralPath $base -Recurse -Force) {
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw "Linked payload is unsupported: $($item.FullName)"
        }
        if ($item.PSIsContainer) { continue }
        $relative = $item.FullName.Substring($base.Length + 1).Replace('\','/')
        # Legacy template is intentionally not installed alongside the current default.
        if ($folder -eq 'config' -and $relative -eq 'FixItFelix.AutoMapPins.categories.vanilla.yaml') { continue }
        Add-Payload $item.FullName "$folder/$relative"
    }
}
# CreateNew closes the race after the existence check; no destructive cleanup on failure.
$stream = [IO.File]::Open($output, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
$archive = $null
try {
    $archive = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create, $true)
    foreach ($entryName in @($files.Keys | Sort-Object)) {
        $entry = $archive.CreateEntry($entryName, [IO.Compression.CompressionLevel]::Optimal)
        $entry.LastWriteTime = [DateTimeOffset]::new(2000,1,1,0,0,0,[TimeSpan]::Zero)
        $inputStream = [IO.File]::OpenRead($files[$entryName])
        $entryStream = $null
        try {
            $entryStream = $entry.Open()
            $inputStream.CopyTo($entryStream)
        } finally {
            if ($null -ne $entryStream) { $entryStream.Dispose() }
            $inputStream.Dispose()
        }
    }
} finally {
    if ($null -ne $archive) { $archive.Dispose() }
    $stream.Dispose()
}
Write-Output "Created $output (version $packageVersion; $($files.Count) files)"
Get-FileHash -LiteralPath $output -Algorithm SHA256
