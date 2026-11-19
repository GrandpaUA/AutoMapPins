param(
    [string]$OutDir = (Join-Path $PSScriptRoot '..\artifacts\test-bounded-gameplay')
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$yaml = Join-Path $root 'packages\YamlDotNet.15.1.4\lib\net47\YamlDotNet.dll'
if (-not (Test-Path $yaml)) { throw "YamlDotNet not found at $yaml" }

$project = Join-Path $OutDir 'BoundedGameplayFixTests.csproj'
$projectXml = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>10</LangVersion>
    <Nullable>enable</Nullable>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="YamlDotNet"><HintPath>$yaml</HintPath></Reference>
    <Compile Include="$root\Tests\BoundedGameplayFixTests.cs" />
    <Compile Include="$root\Common\Constants.cs" />
    <Compile Include="$root\Discovery\ExplorationSkillGate.cs" />
    <Compile Include="$root\Discovery\LoadedObjectScanSummary.cs" />
    <Compile Include="$root\Discovery\LoadedObjectSceneScanner.cs" />
    <Compile Include="$root\Discovery\GeneratedZoneScanner.cs" />
    <Compile Include="$root\Discovery\GeneratedPinService.cs" />
    <Compile Include="$root\Discovery\GeneratedPinRequestPolicy.cs" />
    <Compile Include="$root\Discovery\GeneratedPinRequestLimiter.cs" />
    <Compile Include="$root\Discovery\GeneratedPinWireProtocol.cs" />
    <Compile Include="$root\Discovery\GeneratedPinResponseTracker.cs" />
    <Compile Include="$root\Discovery\GeneratedPinNetworkService.cs" />
    <Compile Include="$root\Data\Config.cs" />
    <Compile Include="$root\Data\YamlFileStorage.cs" />
    <Compile Include="$root\Data\Registry.cs" />
    <Compile Include="$root\Model\PositionedObject.cs" />
    <Compile Include="$root\Model\MapPin.cs" />
    <Compile Include="$root\Model\PinComponent.cs" />
    <Compile Include="$root\Patches\MinimapPatch.cs" />
    <Compile Include="$root\Icons\PickerIconOverrides.cs" />
  </ItemGroup>
</Project>
"@
Set-Content -Path $project -Value $projectXml -Encoding UTF8

dotnet build $project --nologo /p:RestoreIgnoreFailedSources=true
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$out = Join-Path $OutDir 'bin\Debug\net8.0\BoundedGameplayFixTests.dll'
dotnet $out
exit $LASTEXITCODE
