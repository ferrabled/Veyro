"""Devpost marketing assets: the 1024 icon, a 3:2 gallery cover, and 1179x2556 screenshots.

    python docs/submission/devpost_assets.py                      # icon + cover
    python docs/submission/devpost_assets.py shot1.png shot2.png  # + screenshots

Screenshots are scaled to cover 1179x2556 and cropped. The crop comes off the top (status bar,
sky), never the bottom, where the HUD and buttons sit. An iPhone 14 Pro/15/15 Pro/16 capture is
already 1179x2556 and passes through untouched. Output goes to builds/submission/ (gitignored).
Devpost filters any screenshot that is not exactly 1179x2556, so every file is re-opened and checked.
"""
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "builds" / "submission"
ICON = ROOT / "game" / "Assets" / "Art" / "Icon" / "AppIcon.png"
COVER = ROOT / "screenshots" / "graphic.png"
SHOT = (1179, 2556)


def icon():
    img = Image.open(ICON).convert("RGB")  # the asset is palette mode; stores and Devpost want RGB
    if img.size != (1024, 1024):
        img = img.resize((1024, 1024), Image.LANCZOS)
    path = OUT / "icon-1024.png"
    img.save(path)
    return path


def cover():
    # The feature graphic is ~2.05:1; Devpost's gallery shows 3:2. Pad top and bottom with the
    # graphic's own background colour instead of cropping into the lettering.
    src = Image.open(COVER).convert("RGB")
    w, h = src.size
    target_h = round(w * 2 / 3)
    canvas = Image.new("RGB", (w, target_h), src.getpixel((4, 4)))
    canvas.paste(src, (0, (target_h - h) // 2))
    path = OUT / "gallery-cover-3x2.png"
    canvas.save(path)
    return path


def shot(src_path):
    src = Image.open(src_path).convert("RGB")
    w, h = src.size
    scale = max(SHOT[0] / w, SHOT[1] / h)
    scaled = src.resize((round(w * scale), round(h * scale)), Image.LANCZOS)
    sw, sh = scaled.size
    left = (sw - SHOT[0]) // 2
    top = sh - SHOT[1]  # keep the bottom edge
    out = scaled.crop((left, top, left + SHOT[0], top + SHOT[1]))
    path = OUT / f"{Path(src_path).stem}-1179x2556.png"
    out.save(path)
    return path


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    made = [icon(), cover()] + [shot(p) for p in sys.argv[1:]]
    for path in made:
        size = Image.open(path).size
        if "1179x2556" in path.name and size != SHOT:
            sys.exit(f"FAIL {path}: {size}")
        print(f"{path}  {size[0]}x{size[1]}")


if __name__ == "__main__":
    main()
