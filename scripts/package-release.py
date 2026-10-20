"""Package prebuilt plugins; run from the repository root after Release builds."""
from pathlib import Path
import hashlib
import json
import zipfile

ROOT = Path(__file__).resolve().parents[1]
DIST = ROOT / 'dist'
DIST.mkdir(exist_ok=True)
VERSION = '0.1.12.0'
TAG = 'v0.1.12'
REPO = 'https://github.com/MaDTaZ/emby-jellyfin-justwatch'
builds = {
    'JustWatch-Emby-Stable-4.8.10.zip': ('artifacts/emby-stable', 'Emby.Plugin.JustWatch.dll'),
    'JustWatch-Emby-Beta-4.11.0.8.zip': ('artifacts/emby-4.11.0.8', 'Emby.Plugin.JustWatch.dll'),
    'JustWatch-Jellyfin-10.10.7.zip': ('artifacts/jellyfin', 'Jellyfin.Plugin.JustWatch.dll'),
    'JustWatch-Jellyfin-12.0-rc7.zip': ('artifacts/jellyfin-12.0-rc7', 'Jellyfin.Plugin.JustWatch.dll'),
}
for name, (folder, plugin) in builds.items():
    with zipfile.ZipFile(DIST / name, 'w', zipfile.ZIP_DEFLATED) as archive:
        for dll in [plugin, 'JustWatch.Plugin.Core.dll']:
            entry = zipfile.ZipInfo(dll, (2026, 10, 19, 0, 0, 0))
            entry.compress_type = zipfile.ZIP_DEFLATED
            archive.writestr(entry, (ROOT / folder / dll).read_bytes())
    with zipfile.ZipFile(DIST / name) as archive:
        assert archive.testzip() is None

jellyfin = 'JustWatch-Jellyfin-10.10.7.zip'
manifest = [{
    'guid': 'fd91b02d-7551-46f1-a205-2ef7607c25d8',
    'name': 'Where to Watch',
    'description': 'Look up regional streaming, rental, and purchase offers from the server dashboard.',
    'overview': 'Where to Watch: regional provider names, prices, quality, and watch links. Preview release; live server testing is pending.',
    'imageUrl': f'https://raw.githubusercontent.com/MaDTaZ/emby-jellyfin-justwatch/main/assets/where-to-watch-mascot-banner-v2.png',
    'owner': 'MaDTaZ',
    'category': 'General',
    'versions': [
        {
            'version': version,
            'changelog': 'Search region now lists All chosen countries and each configured country individually. Default to All chosen countries; selecting one country does not change saved settings.',
            'targetAbi': abi,
            'sourceUrl': f'{REPO}/releases/download/{TAG}/{name}',
            'checksum': hashlib.md5((DIST / name).read_bytes()).hexdigest(),
            'timestamp': '2026-10-20T00:00:00Z',
        }
        for version, abi, name in [
            ('0.1.12.0', '12.0.0.0', 'JustWatch-Jellyfin-12.0-rc7.zip'),
            ('0.1.11.1', '10.10.7.0', jellyfin),
        ]
    ],
}]
(ROOT / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
(DIST / 'SHA256SUMS.txt').write_text(''.join(
    f'{hashlib.sha256((DIST / name).read_bytes()).hexdigest()}  {name}\n' for name in builds
))
print('Created four packages, checksums, and manifest.json.')
