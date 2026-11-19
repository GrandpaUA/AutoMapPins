"""Run full bounded suite and local scanner failure recovery using a fresh directory."""
from pathlib import Path
import subprocess
import uuid
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parent.parent
out = root / 'artifacts' / ('local-scan-recovery-' + uuid.uuid4().hex)
out.mkdir(parents=True, exist_ok=False)
print('Output:', out, flush=True)
subprocess.run(['powershell.exe', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File',
                str(root / 'scripts/Run-BoundedGameplayFixTests.ps1'), '-OutDir', str(out / 'bounded')], check=True)
probe = out / 'recovery'
probe.mkdir()
doc = ET.Element('Project')
ET.SubElement(doc, 'Import', Project=str(out / 'bounded/BoundedGameplayFixTests.csproj'))
props = ET.SubElement(doc, 'PropertyGroup')
ET.SubElement(props, 'StartupObject').text = 'LocalScanRecoveryTests'
items = ET.SubElement(doc, 'ItemGroup')
ET.SubElement(items, 'Compile', Include=str(root / 'Tests/LocalScanRecoveryTests.cs'))
project = probe / 'Recovery.csproj'
with project.open('xb') as stream:
    ET.ElementTree(doc).write(stream, encoding='utf-8', xml_declaration=True)
subprocess.run(['dotnet', 'run', '--project', str(project), '--verbosity', 'quiet'], check=True)
