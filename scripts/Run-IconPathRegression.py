"""Run the icon suite and real-source path regression in fresh artifact directories.
Requires Windows symlink creation permission (Developer Mode or elevated token).
Never silently skip link tests. No native Unity/game acceptance is implied.
"""
from pathlib import Path
import subprocess
import uuid
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parent.parent
out = root / 'artifacts' / ('icon-path-regression-' + uuid.uuid4().hex)
out.mkdir(parents=True, exist_ok=False)
print('Output:', out, flush=True)
subprocess.run(['powershell.exe', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File',
                str(root / 'scripts/Run-IconAssetTests.ps1'), '-OutDir', str(out / 'icons')], check=True)
probe = out / 'paths'
probe.mkdir()
doc = ET.Element('Project')
ET.SubElement(doc, 'Import', Project=str(out / 'icons/IconAssetTests.csproj'))
props = ET.SubElement(doc, 'PropertyGroup')
ET.SubElement(props, 'StartupObject').text = 'IconPathRegression'
items = ET.SubElement(doc, 'ItemGroup')
ET.SubElement(items, 'Compile', Include=str(root / 'Tests/IconPathRegression.cs'))
project = probe / 'Paths.csproj'
with project.open('xb') as stream:
    ET.ElementTree(doc).write(stream, encoding='utf-8', xml_declaration=True)
subprocess.run(['dotnet', 'run', '--project', str(project), '--verbosity', 'quiet'], check=True)
