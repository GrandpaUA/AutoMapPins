"""Real Windows filesystem notification coverage using actual icon storage."""
from pathlib import Path
import subprocess
import uuid
import xml.etree.ElementTree as ET
root = Path(__file__).resolve().parent.parent
out = root / 'artifacts' / ('icon-watcher-' + uuid.uuid4().hex)
out.mkdir(parents=True, exist_ok=False)
baseline = out / 'icons'
subprocess.run(['powershell.exe', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', str(root / 'scripts/Run-IconAssetTests.ps1'), '-OutDir', str(baseline)], check=True)
for test in ['IconWatcherCoverageTests', 'IconWatcherExtendedTests']:
    probe = out / test
    probe.mkdir()
    doc = ET.Element('Project')
    ET.SubElement(doc, 'Import', Project=str(baseline / 'IconAssetTests.csproj'))
    ET.SubElement(ET.SubElement(doc, 'PropertyGroup'), 'StartupObject').text = test
    ET.SubElement(ET.SubElement(doc, 'ItemGroup'), 'Compile', Include=str(root / 'Tests' / (test + '.cs')))
    project = probe / 'Probe.csproj'
    with project.open('xb') as stream:
        ET.ElementTree(doc).write(stream, encoding='utf-8', xml_declaration=True)
    subprocess.run(['dotnet', 'run', '--project', str(project), '--verbosity', 'quiet'], cwd=probe, check=True)
print('PASS icon baseline and OS watcher suites:', out)
