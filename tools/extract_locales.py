"""Carve the game's 12 locale dictionaries (TextAssets) out of resources.assets.

A Unity 5.3 TextAsset serializes as: int32 name length, the name bytes, padding
to 4-byte alignment, int32 data length, the data bytes. The locale assets are
named with the game's language codes and their data is NGUI's plain-text
"KEY = value" format (see ByteReader.ReadDictionary in the decompiled source),
so each can be found by its name header and carved by the declared length.

Output: one <code>.txt per locale in the directory given as argv[2]
(default game-locales/ beside this script). The output directory is
gitignored: the text is the game's copyrighted content, extracted locally
as a translation reference only.
"""
import struct
import sys
from pathlib import Path

CODES = ["en", "fr", "it", "de", "es", "pt-br", "ru", "hu", "uk", "ja", "ko", "zh"]

def find_text_asset(blob: bytes, name: str):
    """Yield (offset, data) for every plausible TextAsset named `name`."""
    name_bytes = name.encode("ascii")
    pad = (4 - len(name_bytes) % 4) % 4
    header = struct.pack("<i", len(name_bytes)) + name_bytes + b"\x00" * pad
    start = 0
    while True:
        at = blob.find(header, start)
        if at < 0:
            return
        start = at + 1
        size_at = at + len(header)
        (size,) = struct.unpack_from("<i", blob, size_at)
        # A locale file is tens of KB to a few MB; anything else is a
        # different object that happens to share the name pattern.
        if not (10_000 <= size <= 5_000_000):
            continue
        data = blob[size_at + 4 : size_at + 4 + size]
        # The real locale text is "KEY = value" lines, possibly behind a UTF-8
        # BOM or // comment lines; require an early " = " to accept.
        probe = data[:2000]
        if b" = " in probe or b"=" in probe[:200]:
            yield at, data

def main():
    assets = Path(sys.argv[1])
    out_dir = Path(sys.argv[2]) if len(sys.argv) > 2 else Path(__file__).parent.parent / "game-locales"
    out_dir.mkdir(exist_ok=True)
    blob = assets.read_bytes()
    for code in CODES:
        hits = list(find_text_asset(blob, code))
        if len(hits) != 1:
            print(f"{code}: {len(hits)} candidates at {[hex(h[0]) for h in hits]}", file=sys.stderr)
            if not hits:
                continue
        _, data = hits[0]
        text = data.decode("utf-8-sig", errors="strict")
        lines = [l for l in text.splitlines() if "=" in l and not l.startswith("//")]
        (out_dir / f"{code}.txt").write_text(text, encoding="utf-8")
        print(f"{code}: {len(data)} bytes, {len(lines)} key lines")

if __name__ == "__main__":
    main()
