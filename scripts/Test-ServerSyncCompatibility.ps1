param(
    [string[]]$AssemblyPath = @('libraries\ServerSync.dll', 'bin\Release\AutoMapPins.dll'),
    [string]$ValheimAssemblyPath = 'bin\Release\assembly_valheim.dll',
    [string]$CecilPath = 'bin\Release\Mono.Cecil.dll'
)

$ErrorActionPreference = 'Stop'

$ExpectedValheimAssemblyName = 'assembly_valheim'
$ExpectedFieldDeclaringType = 'ZRoutedRpc'
$ExpectedFieldName = 'Everybody'
$ExpectedFieldFullName = 'System.Int64 ZRoutedRpc::Everybody'
$ExpectedLiteral = [int64]0

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
function Resolve-RepoPath([string]$PathValue, [string]$Label) {
    $candidate = if ([System.IO.Path]::IsPathRooted($PathValue)) { $PathValue } else { Join-Path $repoRoot $PathValue }
    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
        throw "$Label not found: $PathValue"
    }
    (Resolve-Path -LiteralPath $candidate).Path
}

function Get-AllTypes($Types) {
    foreach ($type in $Types) {
        $type
        if ($type.HasNestedTypes) {
            Get-AllTypes $type.NestedTypes
        }
    }
}

function Test-ExpectedEverybodyReference([Mono.Cecil.FieldReference]$Field) {
    if ($null -eq $Field) { return $false }
    if ($Field.Name -ne $ExpectedFieldName) { return $false }
    if ($Field.FullName -ne $ExpectedFieldFullName) { return $false }
    if ($Field.FieldType.FullName -ne 'System.Int64') { return $false }
    if ($Field.DeclaringType.FullName -ne $ExpectedFieldDeclaringType) { return $false }
    if ($Field.DeclaringType.Scope.Name -ne $ExpectedValheimAssemblyName) { return $false }
    return $true
}

$valheimFullPath = Resolve-RepoPath $ValheimAssemblyPath 'Valheim assembly'
$cecilFullPath = Resolve-RepoPath $CecilPath 'Mono.Cecil'

Add-Type -Path $cecilFullPath

$valheimModule = [Mono.Cecil.ModuleDefinition]::ReadModule($valheimFullPath)
try {
    if ($valheimModule.Assembly.Name.Name -ne $ExpectedValheimAssemblyName) {
        throw "Expected $ExpectedValheimAssemblyName, got $($valheimModule.Assembly.Name.Name) from $valheimFullPath"
    }
    $zroutedRpc = (Get-AllTypes $valheimModule.Types | Where-Object { $_.FullName -eq $ExpectedFieldDeclaringType } | Select-Object -First 1)
    if ($null -eq $zroutedRpc) { throw "Missing expected type $ExpectedFieldDeclaringType in $valheimFullPath" }
    $everybody = ($zroutedRpc.Fields | Where-Object { $_.Name -eq $ExpectedFieldName } | Select-Object -First 1)
    if ($null -eq $everybody) { throw "Missing expected field $ExpectedFieldFullName in $valheimFullPath" }
    if (-not $everybody.IsLiteral -or -not $everybody.HasConstant -or $everybody.FieldType.FullName -ne 'System.Int64' -or [int64]$everybody.Constant -ne $ExpectedLiteral) {
        throw "Expected $ExpectedFieldFullName to be an Int64 literal constant 0 in $valheimFullPath"
    }
} finally {
    $valheimModule.Dispose()
}

$failures = New-Object System.Collections.Generic.List[string]
foreach ($assemblyPathValue in $AssemblyPath) {
    $assemblyFullPath = Resolve-RepoPath $assemblyPathValue 'Assembly'
    $module = [Mono.Cecil.ModuleDefinition]::ReadModule($assemblyFullPath)
    try {
        foreach ($type in Get-AllTypes $module.Types) {
            foreach ($method in $type.Methods) {
                if (-not $method.HasBody) { continue }
                for ($i = 0; $i -lt $method.Body.Instructions.Count; $i++) {
                    $instruction = $method.Body.Instructions[$i]
                    $field = $instruction.Operand -as [Mono.Cecil.FieldReference]
                    if (-not (Test-ExpectedEverybodyReference $field)) { continue }

                    if ($instruction.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldsfld) {
                        $failures.Add("$assemblyPathValue :: $($method.FullName)[$i] uses ldsfld on literal field $ExpectedFieldFullName=$ExpectedLiteral")
                    } elseif ($instruction.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldsflda) {
                        $failures.Add("$assemblyPathValue :: $($method.FullName)[$i] takes address of literal field $ExpectedFieldFullName")
                    } elseif ($instruction.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Stsfld) {
                        $failures.Add("$assemblyPathValue :: $($method.FullName)[$i] stores to literal field $ExpectedFieldFullName")
                    }
                }
            }
        }
    } finally {
        $module.Dispose()
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { [Console]::Error.WriteLine($_) }
    exit 1
}

Write-Host "ServerSync compatibility check passed for $($AssemblyPath -join ', ') against $ValheimAssemblyPath"
