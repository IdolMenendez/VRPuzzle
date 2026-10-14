#!/usr/bin/env python3
"""Run one PPVR Unity editor method with deterministic absolute paths."""

from __future__ import annotations

import argparse
import datetime as dt
import json
import re
import subprocess
import sys
from pathlib import Path


DEFAULT_PROJECT = Path(__file__).resolve().parents[4]


def unity_version(project: Path) -> str:
    version_file = project / "ProjectSettings" / "ProjectVersion.txt"
    match = re.search(r"^m_EditorVersion:\s*(\S+)", version_file.read_text(encoding="utf-8"), re.MULTILINE)
    if not match:
        raise RuntimeError(f"Could not read Unity version from {version_file}")
    return match.group(1)


def discover_unity(project: Path) -> Path:
    version = unity_version(project)
    candidates = [
        Path("/Volumes/APFS/Unity/Hub/Editor") / version / "Unity.app/Contents/MacOS/Unity",
        Path("/Applications/Unity/Hub/Editor") / version / "Unity.app/Contents/MacOS/Unity",
    ]
    for candidate in candidates:
        if candidate.is_file():
            return candidate.resolve()
    raise FileNotFoundError("Unity executable not found; checked:\n" + "\n".join(map(str, candidates)))


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--method", required=True, help="Fully-qualified static editor method")
    parser.add_argument("--project-root", type=Path, default=DEFAULT_PROJECT)
    parser.add_argument("--unity", type=Path)
    parser.add_argument("--log", type=Path)
    parser.add_argument("--nographics", action="store_true")
    parser.add_argument(
        "--no-quit",
        action="store_true",
        help="Let Play Mode automation exit Unity after its coroutine completes",
    )
    parser.add_argument("--timeout", type=int, default=1800)
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    project = args.project_root.expanduser().resolve()
    unity = args.unity.expanduser().resolve() if args.unity else discover_unity(project)
    safe_method = re.sub(r"[^A-Za-z0-9_.-]", "_", args.method)
    timestamp = dt.datetime.now().strftime("%Y%m%d_%H%M%S")
    log = (args.log.expanduser().resolve() if args.log else
           project / "Artifacts" / "Jobs" / f"{timestamp}_{safe_method}.log")
    log.parent.mkdir(parents=True, exist_ok=True)

    command = [str(unity), "-batchmode"]
    if args.nographics:
        command.append("-nographics")
    command.extend([
        "-projectPath", str(project),
        "-executeMethod", args.method,
    ])
    if not args.no_quit:
        command.append("-quit")
    command.extend(["-logFile", str(log)])
    print(json.dumps({"command": command, "log": str(log)}, ensure_ascii=False, indent=2))
    if args.dry_run:
        return

    result = subprocess.run(command, timeout=args.timeout, check=False)
    if result.returncode:
        print(f"Unity failed with exit code {result.returncode}; log: {log}", file=sys.stderr)
    raise SystemExit(result.returncode)


if __name__ == "__main__":
    main()
