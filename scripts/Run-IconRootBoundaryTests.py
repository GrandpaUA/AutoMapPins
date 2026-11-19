"""Run all icon/path checks, then trailing-separator and drive-root regressions."""
from pathlib import Path
import subprocess
import sys
import uuid
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parent.parent
subprocess.run([sys.executable, str(root / 'scripts/Run-IconPathRegression.py')], check=True)
out = root / 'artifacts' / ('icon-root-boundary-' + uuid.uuid4().hex)
out.mkdir(parents=True, exist_ok=False)
doc = ET.Element('Project', Sdk='Microsoft.NET.Sdk')
props = ET.SubElement(doc, 'PropertyGroup')
for name, value in {'OutputType': 'Exe', 'TargetFramework': 'net8.0',
                    'AssemblyName': 'UnityEngine.ImageConversionModule', 'LangVersion': '10',
                    'Nullable': 'enable', 'EnableDefaultCompileItems': 'false',
                    'StartupObject': 'IconRootBoundaryTests'}.items():
    ET.SubElement(props, name).text = value
items = ET.SubElement(doc, 'ItemGroup')
for name in ['Tests/IconAssetTests.cs', 'Tests/IconRootBoundaryTests.cs', 'Icons/Assets.cs',
             'Icons/IconReloadThrottle.cs', 'Data/IconConfigStorage.cs']:
    ET.SubElement(items, 'Compile', Include=str(root / name))
reference = ET.SubElement(items, 'Reference', Include='YamlDotNet')
ET.SubElement(reference, 'HintPath').text = str(root / 'packages/YamlDotNet.15.1.4/lib/net47/YamlDotNet.dll')
project = out / 'RootBoundary.csproj'
with project.open('xb') as stream:
    ET.ElementTree(doc).write(stream, encoding='utf-8', xml_declaration=True)
print('Root boundary output:', out, flush=True)
subprocess.run(['dotnet', 'run', '--project', str(project), '--verbosity', 'quiet'], check=True)
