"""Offline regression for release license payloads; not legal clearance.

Usage: python scripts/Test-LicensePayload.py path/to/audit-package.zip
"""
from pathlib import Path
import hashlib
import sys
import zipfile

ROOT = Path(__file__).resolve().parents[1]
NOTICES = ('GPL-3.0.txt', 'YamlDotNet.txt', 'ServerSync-upstream.txt', 'THIRD-PARTY.md', 'CREDITS.md')
PREFIX = 'plugins/AutoMapPins/licenses/'


def check_payload(payload):
    expected = {'LICENSE': ROOT / 'LICENSE'}
    expected.update({PREFIX + name: ROOT / 'ThunderStorePackage' / PREFIX / name for name in NOTICES})
    for name, source in expected.items():
        if name not in payload:
            raise ValueError('Missing required license entry: ' + name)
        if payload[name] != source.read_bytes():
            raise ValueError('License entry differs from source: ' + name)
    if b'GNU LESSER GENERAL PUBLIC LICENSE' not in payload['LICENSE']:
        raise ValueError('Unexpected root license')
    if b'GNU GENERAL PUBLIC LICENSE' not in payload[PREFIX + 'GPL-3.0.txt']:
        raise ValueError('Missing incorporated GPL text')
    if b'Copyright 2021 Tykea' not in payload[PREFIX + 'ServerSync-upstream.txt']:
        raise ValueError('Unexpected ServerSync upstream notice')


def expect_failure(payload):
    try:
        check_payload(payload)
    except ValueError:
        return
    raise AssertionError('Corrupt/missing license payload was accepted')


def main():
    archive = Path(sys.argv[1])
    with zipfile.ZipFile(archive) as z:
        names = z.namelist()
        if len(names) != len({n.casefold() for n in names}):
            raise ValueError('Duplicate package entry')
        if z.testzip() is not None:
            raise ValueError('ZIP CRC failure')
        payload = {n: z.read(n) for n in names}
    check_payload(payload)
    required = ['LICENSE', *(PREFIX + name for name in NOTICES)]
    for name in required:
        missing = dict(payload)
        del missing[name]
        expect_failure(missing)
        changed = dict(payload)
        changed[name] += b' altered'
        expect_failure(changed)
    print(f'PASS: exact packaged notices; {len(required)} missing and {len(required)} modified negative controls; ZIP CRC')
    print('ZIP SHA256:', hashlib.sha256(archive.read_bytes()).hexdigest())


if __name__ == '__main__':
    main()
