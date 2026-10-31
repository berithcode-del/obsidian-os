"""Freeze the known 1.0 launcher and selected local assets for laboratory use.

This does not build a disk image, contact a device, or approve distribution.
"""
from __future__ import annotations
import hashlib
import json
import shutil
from pathlib import Path

PROJECT = Path(__file__).resolve().parents[1]
WORKSPACE = PROJECT.parent
OUTPUT = PROJECT / 'artifacts' / 'releases' / 'obsidian-os-1.0-lab-r2'
EXPECTED_LAUNCHER = 'de6fa0a3ac98c529c429554755cab021a5a7c85197acf8b12d4bef420165c018'

def digest(path):
    h = hashlib.sha256()
    with path.open('rb') as f:
        for part in iter(lambda: f.read(1024 * 1024), b''):
            h.update(part)
    return h.hexdigest()

def main():
    launcher = WORKSPACE / '01-projeto/gameman-preview-a7'
    if digest(launcher) != EXPECTED_LAUNCHER:
        raise SystemExit('O launcher mudou: recusar a substituição do congelamento 1.0.')
    if OUTPUT.exists():
        raise SystemExit('O congelamento já existe; não será sobrescrito.')
    ui = WORKSPACE / '01-projeto/gameman-ui'
    mapping = [(launcher, 'overlay/home/player/gameman-launcher'),
               (ui / 'build-preview.ps1', 'source/build-preview.ps1'),
               (ui / 'dist/videos-20261030/LICENCA-FFMPEG.txt', 'licenses/LICENCA-FFMPEG.txt')]
    mapping += [(p, 'source/src/' + p.name) for p in sorted((ui / 'src').glob('*')) if p.is_file()]
    for rel in ['fonts/DejaVuSerif.ttf', 'fonts/DejaVuSerif-LICENSE.txt',
                'library/opening.png', 'library/shelf.png', 'library/voyage.png', 'library/winter.png']:
        asset = (ui / 'assets' / rel) if rel == 'library/opening.png' else (ui / 'dist/library-reader-20261030/gameman-assets' / rel)
        mapping.append((asset,
                        'overlay/home/player/gameman-assets/' + rel))
    for name in ['ffplay-obsidian', 'reproduzir.sh']:
        mapping.append((ui / 'dist/videos-20261030/gameman-assets/video' / name,
                        'overlay/home/player/gameman-assets/video/' + name))
    for src, _ in mapping:
        if not src.is_file(): raise SystemExit('Entrada ausente: ' + str(src))
    OUTPUT.mkdir(parents=True)
    entries = []
    for src, name in mapping:
        target = OUTPUT / name
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(src, target)
        entries.append({'path': name, 'bytes': target.stat().st_size, 'sha256': digest(target),
                        'source': src.relative_to(WORKSPACE).as_posix()})
    manifest = {'release': '1.0', 'classification': 'development-freeze',
                'hardware_profile': 'ga36-a33-rhododendron-panel1-lab',
                'customer_installation_allowed': False, 'signed': False,
                'image_included': False, 'hardware_boot_test_of_image': False,
                'launcher_installed_hash_observed_2026_10_30': EXPECTED_LAUNCHER,
                'limitations': ['Partial overlay; not a bootable image.',
                                'Assets and system dependencies require reconciliation with the running console.',
                                'Licensing and distribution rights not reviewed.',
                                'No personal books, ROMs, saves, or settings included.'],
                'files': entries}
    (OUTPUT / 'freeze-manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2)+'\n',encoding='utf-8')
    (OUTPUT / 'SHA256SUMS.txt').write_text(''.join(f"{e['sha256']}  {e['path']}\n" for e in entries),encoding='utf-8')
    print(f'Congelados {len(entries)} arquivos em {OUTPUT}; imagem não incluída.')

if __name__ == '__main__': main()
