"""Make website-only derivatives of the approved artwork. Requires Pillow.

Usage: python site/tools/prepare_assets.py [reference-repo]
Never writes to the reference repo or the PNG masters.
"""
from pathlib import Path
import base64
import hashlib
import json
import sys
from PIL import Image

site = Path(__file__).resolve().parents[1]
reference = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else site.parent
output = site / 'public' / 'art'
output.mkdir(parents=True, exist_ok=True)
manifest = []
for source in sorted((reference / 'screenshots' / 'guide').glob('*.png')):
    original = Image.open(source).convert('RGB')
    widths = [793] if source.stem == 'camera_01' else [793, 1586]
    for width in widths:
        target = output / f'{source.stem}-{width}.webp'
        size = (width, round(original.height * width / original.width))
        original.resize(size, Image.Resampling.LANCZOS).save(target, 'WEBP', quality=84, method=6)
        manifest.append({'file': target.name, 'source': source.relative_to(reference).as_posix(),
                         'source_sha256': hashlib.sha256(source.read_bytes()).hexdigest(),
                         'width': size[0], 'height': size[1], 'bytes': target.stat().st_size})
for skin in ('ember', 'frost', 'prism'):
    source = reference / 'game' / 'Assets' / 'Resources' / 'Art' / 'CosmeticThumbnails' / f'{skin}.png'
    original = Image.open(source).convert('RGBA')
    target = output / f'skin-{skin}.webp'
    # Preserve the existing render, native resolution and transparent background exactly.
    original.save(target, 'WEBP', lossless=True, exact=True, method=6)
    manifest.append({'file': target.name, 'source': source.relative_to(reference).as_posix(),
                     'source_sha256': hashlib.sha256(source.read_bytes()).hexdigest(),
                     'width': original.width, 'height': original.height, 'bytes': target.stat().st_size})
icon = Image.open(reference / 'screenshots' / 'icon.png').convert('RGB')
for size, name in [(32, 'favicon-32.png'), (64, 'favicon-64.png'), (180, 'apple-touch-icon.png')]:
    icon.resize((size, size), Image.Resampling.LANCZOS).save(site / 'public' / name, optimize=True)
encoded = base64.b64encode((site / 'public' / 'favicon-64.png').read_bytes()).decode('ascii')
(site / 'public' / 'favicon.svg').write_text(
    '<svg xmlns="http://www.w3.org/2000/svg" width="64" height="64" viewBox="0 0 64 64">'
    '<title>Veyro Run</title><image width="64" height="64" href="data:image/png;base64,'
    + encoded + '"/></svg>\n', encoding='utf-8')
(site / 'art-manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
print(f'{len(manifest)} WebP derivatives, {sum(item["bytes"] for item in manifest):,} bytes total; icons generated.')
