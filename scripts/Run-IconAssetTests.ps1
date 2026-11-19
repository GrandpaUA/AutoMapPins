param(
    [string]$OutDir = (Join-Path $PSScriptRoot '..\artifacts\test-out-icon-assets')
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$project = Join-Path $OutDir 'IconAssetTests.csproj'
$projectXml = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <AssemblyName>UnityEngine.ImageConversionModule</AssemblyName>
    <LangVersion>10</LangVersion>
    <Nullable>enable</Nullable>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="$root\Tests\IconAssetTests.cs" />
    <Compile Include="$root\Icons\Assets.cs" />
    <Compile Include="$root\Icons\IconReloadThrottle.cs" />
    <Compile Include="$root\Data\IconConfigStorage.cs" />
    <Reference Include="YamlDotNet"><HintPath>$root\packages\YamlDotNet.15.1.4\lib\net47\YamlDotNet.dll</HintPath></Reference>
  </ItemGroup>
</Project>
"@
Set-Content -Path $project -Value $projectXml -Encoding UTF8

dotnet build $project --nologo /p:RestoreIgnoreFailedSources=true
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$out = Join-Path $OutDir 'bin\Debug\net8.0\UnityEngine.ImageConversionModule.dll'
dotnet $out
exit $LASTEXITCODE
