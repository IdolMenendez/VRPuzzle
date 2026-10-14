#!/usr/bin/env python3
"""Validate the files and texture dimensions of a PPVR generated PBR bundle."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from PIL import Image


def inspect_image(path: Path) -> dict[str, object]:
    with Image.open(path) as image:
        return {"path": str(path), "width": image.width, "height": image.height, "mode": image.mode}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("prefix", type=Path, help="Absolute bundle prefix ending in _Hero")
    parser.add_argument("--require-emission", action="store_true")
    args = parser.parse_args()

    prefix = args.prefix.expanduser().resolve()
    models = [prefix.with_suffix(".fbx"), prefix.with_suffix(".glb")]
    maps = {
        "base_color": Path(str(prefix) + "_base_color.png"),
        "normal": Path(str(prefix) + "_normal.png"),
        "metallic_smoothness": Path(str(prefix) + "_metallic_smoothness.png"),
    }
    if args.require_emission:
        maps["emission"] = Path(str(prefix) + "_emission.png")

    missing = [str(path) for path in models if not path.is_file()]
    missing += [str(path) for path in maps.values() if not path.is_file()]
    if missing:
        raise SystemExit("Missing required bundle files:\n" + "\n".join(missing))

    payload = {
        "prefix": str(prefix),
        "models": [{"path": str(path), "bytes": path.stat().st_size} for path in models],
        "textures": {name: inspect_image(path) for name, path in maps.items()},
        "status": "valid",
    }
    dimensions = {(item["width"], item["height"]) for item in payload["textures"].values()}
    payload["texture_dimensions_match"] = len(dimensions) == 1
    print(json.dumps(payload, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
