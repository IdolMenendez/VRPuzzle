#!/usr/bin/env python3
"""Build Garden Cottage V76 Part_06 V04 with the frozen V03 recipe at depth .0131.

The V03 Unity front/left audit missed the strict projected side-wall p90 target
by less than 0.1 pixel.  V04 preserves V03 source pixels, UVs, topology, alpha,
lambda=.25 boundary smoothing and output contract; only normalized closed-mesh
depth changes from .0135 to .0131.  No network or credential access occurs.
"""

from __future__ import annotations

import dataclasses
import hashlib
import importlib.util
import json
import os
import sys
import uuid
from datetime import datetime, timezone
from pathlib import Path

import numpy as np
from PIL import Image


PROJECT_ROOT = Path("/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR")
PARENT_GENERATOR = (
    PROJECT_ROOT / "Tools/AI3D/build_garden_cottage_v76_part06_smoothed_relief_v03.py"
)
EXPECTED_PARENT_SHA256 = "6fcb3b3e43744053211505d081b52610086118d7797f7205811f630cec3a5a63"
DEPTH_RATIO = 0.0131


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def sha256_bytes(payload: bytes) -> str:
    return hashlib.sha256(payload).hexdigest()


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def load_parent_module():
    if sha256_file(PARENT_GENERATOR) != EXPECTED_PARENT_SHA256:
        raise RuntimeError("Frozen Part_06 V03 generator SHA changed")
    spec = importlib.util.spec_from_file_location(
        "garden_cottage_v76_part06_relief_v03", PARENT_GENERATOR
    )
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Unable to load {PARENT_GENERATOR}")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


def main() -> int:
    v03 = load_parent_module()
    v02 = v03.load_parent_module()
    base = v02.load_base_module()
    original = next(spec for spec in base.PARTS if spec.part_id == "Part_06")
    part_spec = dataclasses.replace(original, depth_ratio=DEPTH_RATIO)
    source = (base.SOURCE_ROOT / part_spec.source_name).resolve()
    if sha256_file(source) != part_spec.source_sha256:
        raise RuntimeError("Part_06 source SHA mismatch")

    with Image.open(source) as image:
        rgb = np.asarray(image.convert("RGB"), dtype=np.uint8)
    mask, relief, rgba, segmentation = base.segment_foreground(rgb, part_spec)
    positions, normals, uvs, indices, mesh_stats = base.build_mesh(mask, relief, part_spec)
    smoothing = v02.smooth_part06_positions(positions, normals, mask)

    triangles = positions[indices.reshape((-1, 3))]
    twice_area = np.linalg.norm(
        np.cross(triangles[:, 1] - triangles[:, 0], triangles[:, 2] - triangles[:, 0]),
        axis=1,
    )
    minimum_twice_area = float(twice_area.min())
    if minimum_twice_area <= 1.0e-10:
        raise RuntimeError("Smoothed Part_06 V04 contains a degenerate triangle")

    mesh_stats["depth_ratio"] = float(positions[:, 2].max() - positions[:, 2].min())
    mesh_stats["bounds_min"] = positions.min(axis=0).astype(float).tolist()
    mesh_stats["bounds_max"] = positions.max(axis=0).astype(float).tolist()
    mesh_stats["smoothing"] = smoothing
    mesh_stats["minimum_twice_triangle_area"] = minimum_twice_area
    mesh_stats["uv_sha256"] = sha256_bytes(uvs.tobytes())
    mesh_stats["index_sha256"] = sha256_bytes(indices.tobytes())

    cutout_path = (base.OUTPUT_ROOT / f"{part_spec.output_stem}_Cutout.png").resolve()
    glb_path = (base.OUTPUT_ROOT / f"{part_spec.output_stem}_Hero.glb").resolve()
    metadata_path = (base.METADATA_ROOT / part_spec.metadata_name).resolve()
    base.OUTPUT_ROOT.mkdir(parents=True, exist_ok=True)
    base.METADATA_ROOT.mkdir(parents=True, exist_ok=True)
    texture_payload = base.png_bytes(rgba)
    if sha256_bytes(texture_payload) != v02.EXPECTED_CUTOUT_SHA256:
        raise RuntimeError("Part_06 cutout/alpha bytes changed in the geometry-only V04 recipe")
    cutout_path.write_bytes(texture_payload)

    unique_suffix = uuid.uuid4().hex
    build_id = f"localrelief-part_06-v04-{unique_suffix}"
    task_id = f"local-v04-{unique_suffix}"
    base.write_glb(
        glb_path, positions, normals, uvs, indices,
        texture_payload, part_spec, build_id,
    )
    validation = base.validate_glb(glb_path)
    if validation["vertices"] != mesh_stats["vertices"] or validation["triangles"] != mesh_stats["triangles"]:
        raise RuntimeError("Part_06 GLB serialization changed the V04 mesh counts")

    generator_path = Path(__file__).resolve()
    record = {
        "schema": "PPVRLocalReliefBuildV1",
        "provider": "LocalRelief",
        "status": "SUCCEEDED",
        "progress": 100,
        "build_id": build_id,
        "task_id": task_id,
        "created_utc": utc_now(),
        "source": str(source),
        "source_sha256": part_spec.source_sha256,
        "models": [str(glb_path)],
        "output_sha256": sha256_file(glb_path),
        "cutout_preview": str(cutout_path),
        "cutout_sha256": sha256_file(cutout_path),
        "vertices": mesh_stats["vertices"],
        "triangles": mesh_stats["triangles"],
        "depth_ratio": mesh_stats["depth_ratio"],
        "indexed_contact_vertices": mesh_stats["indexed_contact_vertices"],
        "texture_embedded": True,
        "credentials_persisted": False,
        "network_used": False,
        "generator": str(generator_path),
        "generator_sha256": sha256_file(generator_path),
        "parent_generator": str(PARENT_GENERATOR),
        "parent_generator_sha256": EXPECTED_PARENT_SHA256,
        "base_generator": str(v02.BASE_GENERATOR),
        "base_generator_sha256": v02.EXPECTED_BASE_SHA256,
        "recipe": "Part06SmoothedThinReliefV04",
        "segmentation": segmentation,
        "mesh": mesh_stats,
        "validation": validation,
    }
    temporary = metadata_path.with_suffix(metadata_path.suffix + ".tmp")
    temporary.write_text(json.dumps(record, indent=2) + "\n", encoding="utf-8")
    os.replace(temporary, metadata_path)
    print(json.dumps(record, indent=2))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as error:
        print(f"[Part06 LocalRelief V04] ERROR: {error}", file=sys.stderr)
        raise
