"""Verify exact production Explore postfix binding using installed Harmony on Windows net48.
No game launch/download/deployment. Output is a fresh retained directory.
"""
import argparse
import hashlib
from pathlib import Path
import subprocess
import uuid
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parent.parent
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--harmony', required=True, type=Path)
parser.add_argument('--out', type=Path)
args = parser.parse_args()
harmony = args.harmony.resolve(strict=True)
refs = root / 'packages/Microsoft.NETFramework.ReferenceAssemblies.net48.1.0.3/build/.NETFramework/v4.8'
if not (refs / 'mscorlib.dll').is_file():
    parser.error('Local net48 reference assemblies missing')
source = (root / 'Patches/MinimapPatch.cs').read_text(encoding='utf-8-sig')
start = '    [UsedImplicitly]\n    [HarmonyPatch("Explore", typeof(Vector3), typeof(float))]'
end = '    [UsedImplicitly]\n    [HarmonyPatch("UpdatePins")]'
if source.count(start) != 1 or source.count(end) != 1:
    raise RuntimeError('Explore extraction boundaries changed; review adapter')
block = source[source.index(start):source.index(end, source.index(start))]
if block.count('ExplorePostfix') != 1:
    raise RuntimeError('Expected exactly one production Explore hook')
out = (args.out or root / 'artifacts' / ('explore-harmony-' + uuid.uuid4().hex)).resolve()
out.mkdir(parents=True, exist_ok=False)
print('Output:', out, flush=True)
print('Harmony SHA256:', hashlib.sha256(harmony.read_bytes()).hexdigest(), flush=True)
print('Production Explore block SHA256:', hashlib.sha256(block.encode()).hexdigest(), flush=True)
hook = out / 'ExploreHook.cs'
with hook.open('x', encoding='utf-8') as f:
    f.write('using HarmonyLib; using UnityEngine; using JetBrains.Annotations; using AutoMapPins.Discovery;\nnamespace AutoMapPins.Patches { internal class MinimapPatch {\n' + block + '\n}}')
doc = ET.Element('Project', Sdk='Microsoft.NET.Sdk')
props = ET.SubElement(doc, 'PropertyGroup')
for key, value in dict(OutputType='Exe', TargetFramework='net48', LangVersion='10', Nullable='annotations',
                      EnableDefaultCompileItems='false', AutomaticallyUseReferenceAssemblyPackages='false',
                      FrameworkPathOverride=str(refs)).items():
    ET.SubElement(props, key).text = value
items = ET.SubElement(doc, 'ItemGroup')
for path in (hook, root / 'Tests/ExploreHarmonyIntegration.cs'):
    ET.SubElement(items, 'Compile', Include=str(path))
ref = ET.SubElement(items, 'Reference', Include='0Harmony')
ET.SubElement(ref, 'HintPath').text = str(harmony)
project = out / 'Explore.csproj'
with project.open('xb') as f:
    ET.ElementTree(doc).write(f, encoding='utf-8', xml_declaration=True)
subprocess.run(['dotnet', 'build', str(project), '--nologo', '--verbosity', 'quiet'], check=True)
subprocess.run([str(out / 'bin/Debug/net48/Explore.exe')], check=True)
