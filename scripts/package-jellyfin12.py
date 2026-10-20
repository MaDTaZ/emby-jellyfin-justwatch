"""Package the dedicated Jellyfin 12.0-rc7 build, preserving older server builds."""
from pathlib import Path
import hashlib
import json
import zipfile

root = Path(__file__).resolve().parents[1]
name = 'JustWatch-Jellyfin-12.0-rc7.zip'
package = root / 'dist' / name
with zipfile.ZipFile(package, 'w', zipfile.ZIP_DEFLATED) as archive:
    for dll in ['Jellyfin.Plugin.JustWatch.dll', 'JustWatch.Plugin.Core.dll']:
        entry = zipfile.ZipInfo(dll, (2026, 10, 19, 0, 0, 0))
        entry.compress_type = zipfile.ZIP_DEFLATED
        archive.writestr(entry, (root / 'artifacts/jellyfin-12.0-rc7' / dll).read_bytes())
manifest_path = root / 'manifest.json'
manifest = json.loads(manifest_path.read_text())
versions = manifest[0]['versions']
versions[:] = [v for v in versions if v['version'] != '0.1.2.0']
versions.insert(0, {
    'version': '0.1.2.0',
    'changelog': 'Dedicated Jellyfin 12.0-rc7 / .NET 10 build, including dashboard Save and Search initialization fixes.',
    'targetAbi': '12.0.0.0',
    'sourceUrl': 'https://github.com/MaDTaZ/emby-jellyfin-justwatch/releases/download/v0.1.2/' + name,
    'checksum': hashlib.md5(package.read_bytes()).hexdigest(),
    'timestamp': '2026-10-20T00:00:00Z',
})
manifest_path.write_text(json.dumps(manifest, indent=2) + '\n')
(root / 'dist/SHA256SUMS-Jellyfin12.txt').write_text(hashlib.sha256(package.read_bytes()).hexdigest() + '  ' + name + '\n')
print('Packaged Jellyfin 12.0-rc7 as plugin 0.1.2.0; preserved 10.10.7 manifest entry.')
