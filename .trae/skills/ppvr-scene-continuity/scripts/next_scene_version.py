#!/usr/bin/env python3
"""Calculate the next PPVR scene checkpoint paths without changing files."""

from __future__ import annotations

import argparse
import datetime as dt
import json
import re
from pathlib import Path


DEFAULT_PROJECT = Path(__file__).resolve().parents[4]


def safe_token(value: str, label: str) -> str:
    token = re.sub(r"[^A-Za-z0-9]", "", value)
    if not token:
        raise ValueError(f"{label} must contain at least one letter or digit")
    return token


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--scene", required=True, help="Logical scene name, for example Splash")
    parser.add_argument("--descriptor", required=True, help="PascalCase checkpoint descriptor")
    parser.add_argument("--date", default=dt.date.today().strftime("%Y%m%d"), help="YYYYMMDD")
    parser.add_argument("--project-root", type=Path, default=DEFAULT_PROJECT)
    args = parser.parse_args()

    scene = safe_token(args.scene, "scene")
    descriptor = safe_token(args.descriptor, "descriptor")
    if not re.fullmatch(r"\d{8}", args.date):
        raise ValueError("date must use YYYYMMDD")

    project = args.project_root.expanduser().resolve()
    scenes = project / "Assets" / "Scenes"
    pattern = re.compile(rf"^{re.escape(scene)}_{args.date}_V(\d+)_")
    used = []
    if scenes.is_dir():
        for path in scenes.glob(f"{scene}_{args.date}_V*.unity"):
            match = pattern.match(path.stem)
            if match:
                used.append(int(match.group(1)))
    version = max(used, default=0) + 1
    stem = f"{scene}_{args.date}_V{version:02d}_{descriptor}"
    payload = {
        "project_root": str(project),
        "version": f"V{version:02d}",
        "stem": stem,
        "scene_path": str(scenes / f"{stem}.unity"),
        "screenshot_path": str(project / "Artifacts" / scene / f"{stem}.png"),
        "job_artifacts_directory": str(project / "Artifacts" / scene / f"{stem}_Artifacts"),
    }
    print(json.dumps(payload, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
