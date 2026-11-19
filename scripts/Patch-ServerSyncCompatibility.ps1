param(
    [Parameter(Mandatory = $true)]
    [string]$AssemblyPath,

    [Parameter(Mandatory = $true)]
    [string]$ValheimAssemblyPath,

    [string]$CecilPath = (Join-Path $PSScriptRoot '..\bin\Release\Mono.Cecil.dll')
)

$ErrorActionPreference = 'Stop'

$ExpectedAssemblyName = 'ServerSync'
$ExpectedValheimAssemblyName = 'assembly_valheim'
$ExpectedFieldDeclaringType = 'ZRoutedRpc'
$ExpectedFieldName = 'Everybody'
$ExpectedFieldFullName = 'System.Int64 ZRoutedRpc::Everybody'
$ExpectedLiteral = [int64]0
$ExpectedSites = @(
    @{ Method = 'System.Collections.IEnumerator ServerSync.ConfigSync::sendZPackage(System.Int64,ZPackage)'; InstructionIndex = 28; InstructionCount = 52 },
    @{ Method = 'System.Void ServerSync.ConfigSync/<>c__DisplayClass34_0`1::<AddConfigEntry>b__0(System.Object,System.EventArgs)'; InstructionIndex = 14; InstructionCount = 26 },
    @{ Method = 'System.Void ServerSync.ConfigSync/<>c__DisplayClass36_0::<AddCustomValue>b__1()'; InstructionIndex = 10; InstructionCount = 22 }
)

function Resolve-ExistingPath([string]$PathValue, [string]$Label) {
    if (-not (Test-Path -LiteralPath $PathValue -PathType Leaf)) {
        throw "$Label not found: $PathValue"
    }
    (Resolve-Path -LiteralPath $PathValue).Path
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

$assemblyFullPath = Resolve-ExistingPath $AssemblyPath 'Assembly'
$valheimFullPath = Resolve-ExistingPath $ValheimAssemblyPath 'Valheim assembly'
$cecilFullPath = Resolve-ExistingPath $CecilPath 'Mono.Cecil'

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

$readerParameters = [Mono.Cecil.ReaderParameters]::new()
$readerParameters.ReadWrite = $true
$module = [Mono.Cecil.ModuleDefinition]::ReadModule($assemblyFullPath, $readerParameters)
$patched = 0
try {
    if ($module.Assembly.Name.Name -ne $ExpectedAssemblyName) {
        throw "Patch is only valid for $ExpectedAssemblyName.dll; got $($module.Assembly.Name.Name) from $assemblyFullPath"
    }

    $methodsByFullName = @{}
    foreach ($type in Get-AllTypes $module.Types) {
        foreach ($method in $type.Methods) {
            if ($method.HasBody) { $methodsByFullName[$method.FullName] = $method }
        }
    }

    $originalSites = 0
    $correctedSites = 0
    foreach ($site in $ExpectedSites) {
        if (-not $methodsByFullName.ContainsKey($site.Method)) {
            throw "Expected method not found: $($site.Method)"
        }
        $method = $methodsByFullName[$site.Method]
        if ($method.Body.Instructions.Count -ne $site.InstructionCount) {
            throw "Unexpected instruction count for $($site.Method): $($method.Body.Instructions.Count), expected $($site.InstructionCount)"
        }
        $instruction = $method.Body.Instructions[$site.InstructionIndex]
        $field = $instruction.Operand -as [Mono.Cecil.FieldReference]
        if ($instruction.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldsfld -and (Test-ExpectedEverybodyReference $field)) {
            $originalSites++
            continue
        }
        if ($instruction.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldc_I8 -and [int64]$instruction.Operand -eq $ExpectedLiteral) {
            $correctedSites++
            continue
        }
        throw "Unexpected instruction at $($site.Method)[$($site.InstructionIndex)]: $($instruction.OpCode) $($instruction.Operand)"
    }

    if ($originalSites -eq $ExpectedSites.Count -and $correctedSites -eq 0) {
        foreach ($site in $ExpectedSites) {
            $instruction = $methodsByFullName[$site.Method].Body.Instructions[$site.InstructionIndex]
            $instruction.OpCode = [Mono.Cecil.Cil.OpCodes]::Ldc_I8
            $instruction.Operand = $ExpectedLiteral
            $patched++
        }
    } elseif ($originalSites -eq 0 -and $correctedSites -eq $ExpectedSites.Count) {
        Write-Host "ServerSync compatibility patch already present in $assemblyFullPath"
        return
    } else {
        throw "Mixed ServerSync compatibility patch state: $originalSites original site(s), $correctedSites corrected site(s); refusing partial rewrite"
    }

    $unexpectedReferences = New-Object System.Collections.Generic.List[string]
    foreach ($type in Get-AllTypes $module.Types) {
        foreach ($method in $type.Methods) {
            if (-not $method.HasBody) { continue }
            for ($i = 0; $i -lt $method.Body.Instructions.Count; $i++) {
                $instruction = $method.Body.Instructions[$i]
                $field = $instruction.Operand -as [Mono.Cecil.FieldReference]
                if (-not (Test-ExpectedEverybodyReference $field)) { continue }
                if ($instruction.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldsfld -or
                    $instruction.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldsflda -or
                    $instruction.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Stsfld) {
                    $unexpectedReferences.Add("$($method.FullName)[$i] $($instruction.OpCode) $($field.FullName)")
                }
            }
        }
    }
    if ($unexpectedReferences.Count -gt 0) {
        $unexpectedReferences | ForEach-Object { [Console]::Error.WriteLine($_) }
        throw "Unexpected remaining access to $ExpectedFieldFullName after patch"
    }

    $module.Write()
} finally {
    $module.Dispose()
}

Write-Host "Patched $patched exact $ExpectedFieldFullName load(s) in $assemblyFullPath"
