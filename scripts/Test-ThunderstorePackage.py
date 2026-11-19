"""Offline regression checks against the real PowerShell packager; fixtures retained."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import uuid
import zipfile

root = Path(__file__).resolve().parent.parent
parser = argparse.ArgumentParser()
parser.add_argument('--dll', type=Path, required=True)
parser.add_argument('--version', default=json.loads((root / 'ThunderStorePackage/manifest.json').read_text(encoding='utf-8-sig'))['version_number'])
args = parser.parse_args()
dll = args.dll.resolve()
packager = root / 'scripts/New-ThunderstorePackage.ps1'
work = root / 'artifacts' / ('package-tests-' + uuid.uuid4().hex)
work.mkdir(parents=True)

def fixture(name, version=args.version, duplicate=False):
    base = work / name
    package = base / 'ThunderStorePackage'
    package.mkdir(parents=True)
    for path, content in {
        'README.md': b'Extended GitHub documentation: must not appear on the mod site',
        'ThunderStorePackage/README.md': b'Short Thunderstore overview',
        'CHANGELOG.md': b'changes', 'LICENSE': b'license',
        'ThunderStorePackage/icon.png': b'fixture icon',
        'ThunderStorePackage/config/a/icon.png': b'FIRST',
        'ThunderStorePackage/config/b/icon.png': b'SECOND',
        'ThunderStorePackage/config/FixItFelix.AutoMapPins.categories.yaml': b'current',
        'ThunderStorePackage/config/FixItFelix.AutoMapPins.categories.vanilla.yaml': b'legacy',
        'TempPackage/stale.txt': b'MUST NOT SHIP',
    }.items():
        p = base / path
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_bytes(content)
    (package / 'manifest.json').write_text(json.dumps({'version_number': version}), encoding='utf-8')
    if duplicate:
        (package / 'plugins').mkdir()
        shutil.copyfile(dll, package / 'plugins/AutoMapPins.dll')
    return base

def snapshot(base):
    return {str(p.relative_to(base)): hashlib.sha256(p.read_bytes()).hexdigest()
            for p in base.rglob('*') if p.is_file()}

def run(base, output):
    # A Python parent launched by PowerShell 7 can pass its module paths into
    # Windows PowerShell 5.1. Prefer the child's own built-in modules so that
    # standard commands such as Get-FileHash resolve in that process.
    child_env = dict(os.environ)
    system_modules = str(Path(os.environ['SystemRoot']) / 'System32/WindowsPowerShell/v1.0/Modules')
    child_env['PSModulePath'] = system_modules + os.pathsep + child_env.get('PSModulePath', '')
    result = subprocess.run(['powershell', '-NoProfile', '-ExecutionPolicy', 'Bypass',
        '-File', str(packager), '-ProjectRoot', str(base), '-DllPath', str(dll),
        '-OutputZip', str(output)], capture_output=True, text=True, env=child_env)
    return result.returncode, result.stdout + result.stderr

base = fixture('valid')
before = snapshot(base)
out = work / 'valid.zip'
code, log = run(base, out)
assert code == 0, log
with zipfile.ZipFile(out) as archive:
    names = archive.namelist()
    assert archive.read('config/a/icon.png') == b'FIRST'
    assert archive.read('config/b/icon.png') == b'SECOND'
    assert archive.read('plugins/AutoMapPins.dll') == dll.read_bytes()
    assert archive.read('README.md') == (base / 'ThunderStorePackage/README.md').read_bytes()
    assert archive.read('README.md') != (base / 'README.md').read_bytes()
    assert len(names) == 9, names
    assert not any('stale' in n or 'vanilla' in n for n in names), names
assert snapshot(base) == before
print('PASS nested paths, DLL identity, no stale files, legacy exclusion, source immutability')
original = out.read_bytes()
code, log = run(base, out)
assert code != 0 and 'Output already exists' in log, log
assert out.read_bytes() == original
print('PASS existing output rejected without mutation')
second = work / 'repeat.zip'
code, log = run(base, second)
assert code == 0, log
assert second.read_bytes() == original
print('PASS byte-reproducible repeated package')
github_changed = fixture('github-readme-changed')
(github_changed / 'README.md').write_bytes(b'Different extended GitHub-only guide')
prior = snapshot(github_changed)
unchanged_site = work / 'github-readme-changed.zip'
code, log = run(github_changed, unchanged_site)
assert code == 0, log
assert unchanged_site.read_bytes() == original
assert snapshot(github_changed) == prior
print('PASS GitHub-only README changes do not change the mod package')
missing_site = fixture('missing-site-readme')
(missing_site / 'ThunderStorePackage/README.md').unlink()
prior = snapshot(missing_site)
missing_target = work / 'missing-site-readme.zip'
code, log = run(missing_site, missing_target)
assert code != 0 and 'Thunderstore README not found' in log, log
assert not missing_target.exists()
assert snapshot(missing_site) == prior
print('PASS missing site README rejected without falling back to GitHub documentation')
for name, version, duplicate, expected in [
    ('version-mismatch', '99.0.0', False, 'version mismatch'),
    ('collision', args.version, True, 'Duplicate package destination'),
]:
    case = fixture(name, version, duplicate)
    prior = snapshot(case)
    target = work / (name + '.zip')
    code, log = run(case, target)
    assert code != 0 and expected in log, log
    assert not target.exists()
    assert snapshot(case) == prior
    print('PASS ' + name + ' rejected before writing archive')
print('SUMMARY package regressions PASS; retained fixtures:', work)
