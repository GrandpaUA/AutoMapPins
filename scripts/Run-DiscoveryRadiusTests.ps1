param([string]$OutDir = (Join-Path $PSScriptRoot '..\artifacts\test-discovery-radius'))
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$project = Join-Path $OutDir 'DiscoveryRadiusTests.csproj'
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework>
    <LangVersion>10</LangVersion><Nullable>enable</Nullable>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="$root\Tests\DiscoveryRadiusTests.cs" />
    <Compile Include="$root\Discovery\DiscoveryRadiusService.cs" />
  </ItemGroup>
</Project>
"@ | Set-Content -Path $project -Encoding UTF8
dotnet build $project --nologo /p:RestoreIgnoreFailedSources=true
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet (Join-Path $OutDir 'bin\Debug\net8.0\DiscoveryRadiusTests.dll')
exit $LASTEXITCODE
