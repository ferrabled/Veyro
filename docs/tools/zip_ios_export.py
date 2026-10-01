#!/usr/bin/env python3
"""Zip a Veyro Run iOS Xcode export for the Mac, keeping the exec bits Windows cannot store.

Usage:
    python docs/tools/zip_ios_export.py <export-dir> <out.zip>

A zip made by Explorer / Compress-Archive has no Unix modes, so on the Mac every script and
tool in the export unpacks as non-executable. This one marks the entries Unix-made, with 0755
for *.sh and everything under Il2CppOutputProject/IL2CPP/build/deploy_*/ (the il2cpp and bee
tools the GameAssembly target runs) and 0644 for the rest, so `unzip` and Archive Utility
restore them. The runbook's chmod step stays as a belt-and-braces check.

Leaves out `*_BurstDebugInformation_DoNotShip` (Burst's debug symbols: Unity's own name says
it; not needed to build). Entries are sorted and time-stamped from the files, so re-zipping an
unchanged export gives the same bytes. Prints the zip's size and SHA-256. Standard library only.
"""
import hashlib
import os
import sys
import zipfile

EXCLUDED_DIR_SUFFIX = "_BurstDebugInformation_DoNotShip"


def executable(rel):
    return rel.endswith(".sh") or rel.startswith("Il2CppOutputProject/IL2CPP/build/deploy_")


def main():
    if len(sys.argv) != 3:
        print(__doc__, file=sys.stderr)
        return 2
    root, out = os.path.abspath(sys.argv[1]), os.path.abspath(sys.argv[2])
    top = os.path.basename(root.rstrip("\\/"))
    if not os.path.isfile(os.path.join(root, "Unity-iPhone.xcodeproj", "project.pbxproj")):
        print(f"{root} is not an Xcode export (no Unity-iPhone.xcodeproj/project.pbxproj)", file=sys.stderr)
        return 1
    os.makedirs(os.path.dirname(out), exist_ok=True)

    files, skipped = [], []
    for dirpath, dirnames, filenames in os.walk(root):
        for d in list(dirnames):
            if d.endswith(EXCLUDED_DIR_SUFFIX):
                skipped.append(os.path.relpath(os.path.join(dirpath, d), root))
                dirnames.remove(d)
        for f in filenames:
            full = os.path.join(dirpath, f)
            files.append((os.path.relpath(full, root).replace("\\", "/"), full))
    files.sort()

    execs = 0
    with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as z:
        for rel, full in files:
            info = zipfile.ZipInfo.from_file(full, top + "/" + rel)
            info.create_system = 3  # Unix: unzip honours the mode bits below
            mode = 0o755 if executable(rel) else 0o644
            execs += mode == 0o755
            info.external_attr = (0o100000 | mode) << 16
            info.compress_type = zipfile.ZIP_DEFLATED
            with open(full, "rb") as fh:
                z.writestr(info, fh.read(), compresslevel=6)

    digest = hashlib.sha256()
    with open(out, "rb") as fh:
        for chunk in iter(lambda: fh.read(1 << 20), b""):
            digest.update(chunk)
    print(f"{out}\n  {len(files)} files ({execs} marked executable), skipped: {', '.join(skipped) or 'none'}")
    print(f"  size   {os.path.getsize(out):,} bytes\n  sha256 {digest.hexdigest()}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
