"""Run actual radius source + exact sharing hooks with real installed Harmony.

Requires Windows, Python, .NET SDK, local net48 reference assemblies and 0Harmony.dll.
No downloads, game launches, overwrites, service calls or deletion. Outputs retained.
"""
import argparse
import hashlib
from pathlib import Path
import subprocess
import uuid
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent.parent
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--harmony', type=Path, required=True, help='Installed BepInEx/core/0Harmony.dll')
parser.add_argument('--out', type=Path, help='New output directory; existing directories are refused')
args = parser.parse_args()
harmony = args.harmony.resolve(strict=True)
references = ROOT / 'packages/Microsoft.NETFramework.ReferenceAssemblies.net48.1.0.3/build/.NETFramework/v4.8'
if not (references / 'mscorlib.dll').is_file():
    parser.error('Local net48 reference assemblies not found; restore project dependencies first')
out = args.out or ROOT / 'artifacts' / ('audit-tests-' + uuid.uuid4().hex)
out = out.resolve()
out.mkdir(parents=True, exist_ok=False)
print('Output:', out, flush=True)
print('Harmony SHA256:', hashlib.sha256(harmony.read_bytes()).hexdigest(), flush=True)


def project(folder, framework, startup, sources, real_harmony=False):
    folder.mkdir()
    doc = ET.Element('Project', Sdk='Microsoft.NET.Sdk')
    properties = ET.SubElement(doc, 'PropertyGroup')
    values = {'OutputType': 'Exe', 'TargetFramework': framework, 'LangVersion': '10',
              'EnableDefaultCompileItems': 'false', 'StartupObject': startup,
              'Nullable': 'enable' if framework == 'net8.0' else 'annotations'}
    if real_harmony:
        values.update(AutomaticallyUseReferenceAssemblyPackages='false', FrameworkPathOverride=str(references))
    for key, value in values.items():
        ET.SubElement(properties, key).text = value
    items = ET.SubElement(doc, 'ItemGroup')
    for source in sources:
        ET.SubElement(items, 'Compile', Include=str(source))
    if real_harmony:
        reference = ET.SubElement(items, 'Reference', Include='0Harmony')
        ET.SubElement(reference, 'HintPath').text = str(harmony)
    path = folder / 'Tests.csproj'
    ET.ElementTree(doc).write(path, encoding='utf-8', xml_declaration=True)
    subprocess.run(['dotnet', 'build', str(path), '--nologo', '--verbosity', 'quiet'], check=True)
    binary = folder / 'bin/Debug' / framework / ('Tests.exe' if real_harmony else 'Tests.dll')
    subprocess.run([str(binary)] if real_harmony else ['dotnet', str(binary)], check=True)


for entry in ('DiscoveryRadiusTests', 'DiscoveryRadiusExtendedTests'):
    project(out / entry, 'net8.0', entry, [ROOT / 'Tests/DiscoveryRadiusTests.cs',
            ROOT / 'Tests/DiscoveryRadiusExtendedTests.cs', ROOT / 'Discovery/DiscoveryRadiusService.cs'])

source = (ROOT / 'Patches/MinimapPatch.cs').read_text(encoding='utf-8-sig')
start = '    [UsedImplicitly]\n    [HarmonyPatch("GetSharedMapData")]'
end = '    private static void ConvertSavedAmpPins()'
# Prefix, postfix and finalizer each carry the same HarmonyPatch attribute.
if source.count(start) != 3 or source.count(end) != 1:
    raise RuntimeError('Production extraction boundaries changed; review test adapter explicitly')
block = source[source.index(start):source.index(end, source.index(start))]
for method in ('GetSharedMapDataPrefix', 'GetSharedMapDataPostfix', 'GetSharedMapDataFinalizer'):
    if block.count(method) != 1:
        raise RuntimeError('Unexpected production hook declaration: ' + method)
header = '''using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using JetBrains.Annotations;
using AutoMapPins.Model;
namespace AutoMapPins.Patches {
internal class MinimapPatch {
private static readonly AccessTools.FieldRef<Minimap,List<Minimap.PinData>> PinsRef = AccessTools.FieldRefAccess<Minimap,List<Minimap.PinData>>("m_pins");
// Identity adapter only: exact production ownership/encoding is exercised by bounded gameplay tests.
private static bool TryGetSavedAmpPinInternalName(Minimap.PinData pin,out string name) {
name="Tree"; return pin.m_type==Minimap.PinType.None && pin.m_ownerID==0 && pin.m_author=="amp"; }
'''
hooks = out / 'SharingHooks.cs'
hooks.write_text(header + block + '\n}}', encoding='utf-8')
print('Exact production sharing block SHA256:', hashlib.sha256(block.encode()).hexdigest(), flush=True)
project(out / 'Harmony', 'net48', 'HarmonyIntegration',
        [hooks, ROOT / 'Tests/HarmonyWorldFixture.cs', ROOT / 'Tests/HarmonyIntegration.cs'], True)
print('SUMMARY all audit regressions PASS (stub world; not Valheim/Mono gameplay)', flush=True)
