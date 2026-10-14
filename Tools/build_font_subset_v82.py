"""Build a reproducible UI font subset; keep the original font immutable."""
from pathlib import Path
import hashlib
import json
import re
from fontTools import subset
from fontTools.ttLib import TTFont

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Artifacts/Checkpoints/174_ResourceOptimization_20260910"
SOURCE = ROOT / "Assets/Fonts/OpenSource/NotoSansCJKsc-Regular.otf"
DEST = ROOT / "Assets/Optimization/V82/Fonts/NotoSansCJKsc-Subset.otf"

def main():
    files = sorted((ROOT / "Assets/Scripts").glob("*.cs"))
    settings = (ROOT / "ProjectSettings/EditorBuildSettings.asset").read_text()
    files += [ROOT / p for p in re.findall(r"^\s+path: (Assets/.*\.unity)$", settings, re.M)]
    files += sorted((ROOT / "Assets/Resources").rglob("*.json"))
    files += sorted((ROOT / "Assets/Resources").rglob("*.prefab"))
    chars = set(range(32, 0x250)) | set(range(0x2000, 0x2070))
    for file in files:
        text = file.read_text(encoding="utf-8")
        chars.update(map(ord, text))
        chars.update(int(x, 16) for x in re.findall(r"\\u([0-9a-fA-F]{4})", text))
        chars.update(int(x, 16) for x in re.findall(r"\\U([0-9a-fA-F]{8})", text))
    font = TTFont(SOURCE)
    original = font.getBestCmap()
    wanted = sorted(chars.intersection(original))
    options = subset.Options()
    options.layout_features = ["*"]
    options.name_IDs = ["*"]
    options.name_languages = ["*"]
    sub = subset.Subsetter(options=options)
    sub.populate(unicodes=wanted)
    sub.subset(font)
    DEST.parent.mkdir(parents=True, exist_ok=True)
    font.save(DEST)
    result = TTFont(DEST)
    missing = sorted(set(wanted) - result.getBestCmap().keys())
    assert not missing, missing
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / "font-coverage.json").write_text(json.dumps({
        "source": str(SOURCE), "output": str(DEST),
        "source_bytes": SOURCE.stat().st_size, "subset_bytes": DEST.stat().st_size,
        "retained_codepoints": len(wanted), "missing_from_subset": missing,
        "source_files": [str(p) for p in files],
        "source_sha256": hashlib.sha256(SOURCE.read_bytes()).hexdigest(),
        "subset_sha256": hashlib.sha256(DEST.read_bytes()).hexdigest(),
        "note": "Covers existing source/scene text plus Latin and punctuation; future text requires regeneration. No reduction in character coverage relative to the original font for the scanned corpus."
    }, indent=2), encoding="utf-8")
    print(f"FONT COVERAGE PASS: {len(wanted)} codepoints; {SOURCE.stat().st_size} -> {DEST.stat().st_size} bytes")

if __name__ == "__main__":
    main()
