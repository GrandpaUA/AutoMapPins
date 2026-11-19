"""Fresh generated actual-source projects for category and icon culture checks."""
from pathlib import Path
import subprocess
import uuid
import xml.etree.ElementTree as ET
root = Path(__file__).resolve().parent.parent
out = root / 'artifacts' / ('culture-order-' + uuid.uuid4().hex)
out.mkdir(parents=True, exist_ok=False)
for kind, script, project, test in [
    ('categories', 'Run-BoundedGameplayFixTests.ps1', 'BoundedGameplayFixTests.csproj', 'CategoryCultureOrderTests'),
    ('icons', 'Run-IconAssetTests.ps1', 'IconAssetTests.csproj', 'IconCultureOrderTests')]:
    baseline = out / kind
    subprocess.run(['powershell.exe', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', str(root / 'scripts' / script), '-OutDir', str(baseline)], check=True)
    probe = out / test
    probe.mkdir()
    doc = ET.Element('Project')
    ET.SubElement(doc, 'Import', Project=str(baseline / project))
    props = ET.SubElement(doc, 'PropertyGroup')
    ET.SubElement(props, 'StartupObject').text = test
    items = ET.SubElement(doc, 'ItemGroup')
    ET.SubElement(items, 'Compile', Include=str(root / 'Tests' / (test + '.cs')))
    path = probe / 'Probe.csproj'
    with path.open('xb') as stream:
        ET.ElementTree(doc).write(stream, encoding='utf-8', xml_declaration=True)
    # Category storage constructor sees only this empty probe directory, not repo fixtures.
    subprocess.run(['dotnet', 'run', '--project', str(path), '--verbosity', 'quiet'], cwd=probe, check=True)
print('PASS both source suites and culture regressions:', out)
