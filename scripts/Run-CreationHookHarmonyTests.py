"""Compile the full production creation-hook file against stubs and real Harmony.
No game launch, download, deployment or overwrite; fresh retained artifacts only.
"""
import argparse
import hashlib
from pathlib import Path
import subprocess
import uuid
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parent.parent
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--harmony', type=Path, required=True)
args = parser.parse_args()
harmony = args.harmony.resolve(strict=True)
refs = root / 'packages/Microsoft.NETFramework.ReferenceAssemblies.net48.1.0.3/build/.NETFramework/v4.8'
if not (refs / 'mscorlib.dll').is_file():
    parser.error('Missing local net48 references')
sources = [root / 'Patches/AllCreatePinPatches.cs', root / 'Tests/CreationHookHarmonyTests.cs']
out = root / 'artifacts' / ('creation-harmony-' + uuid.uuid4().hex)
out.mkdir(parents=True, exist_ok=False)
print('Output:', out, flush=True)
for path in [harmony, *sources]:
    print(path.name, 'SHA256:', hashlib.sha256(path.read_bytes()).hexdigest(), flush=True)
doc = ET.Element('Project', Sdk='Microsoft.NET.Sdk')
props = ET.SubElement(doc, 'PropertyGroup')
for key, value in dict(OutputType='Exe', TargetFramework='net48', LangVersion='10', Nullable='annotations',
                       EnableDefaultCompileItems='false', AutomaticallyUseReferenceAssemblyPackages='false',
                       FrameworkPathOverride=str(refs)).items():
    ET.SubElement(props, key).text = value
items = ET.SubElement(doc, 'ItemGroup')
for source in sources:
    ET.SubElement(items, 'Compile', Include=str(source))
reference = ET.SubElement(items, 'Reference', Include='0Harmony')
ET.SubElement(reference, 'HintPath').text = str(harmony)
project = out / 'Creation.csproj'
with project.open('xb') as stream:
    ET.ElementTree(doc).write(stream, encoding='utf-8', xml_declaration=True)
subprocess.run(['dotnet', 'build', str(project), '--nologo', '--verbosity', 'quiet'], check=True)
subprocess.run([str(out / 'bin/Debug/net48/Creation.exe')], check=True)
