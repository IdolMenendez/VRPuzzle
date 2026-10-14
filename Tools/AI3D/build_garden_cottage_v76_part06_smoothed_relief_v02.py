#!/usr/bin/env python3
"""Build only Garden Cottage V76 Part_06 with a thin, smoothed relief contour.

This versioned recipe imports the frozen V76 LocalRelief V1 implementation but
does not modify it.  It keeps the source segmentation, texture, UVs, indices,
alpha mode, and output paths unchanged.  Only POSITION/NORMAL data changes:

* total normalized depth is reduced from 0.050 to 0.020;
* degree-two silhouette corners receive one lambda=0.25 Laplacian pass;
* every corner displacement is capped at 0.35 grid cell;
* duplicated side-wall normals are recomputed from the smoothed edge.

No network or credential access is performed.
"""

from __future__ import annotations

import dataclasses
import hashlib
import importlib.util
import json
import math
import os
import sys
import uuid
from collections import defaultdict
from datetime import datetime, timezone
from pathlib import Path

import numpy as np
from PIL import Image


PROJECT_ROOT = Path("/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR")
BASE_GENERATOR = PROJECT_ROOT / "Tools/AI3D/build_garden_cottage_v76_local_relief_fallback.py"
EXPECTED_BASE_SHA256 = "269290c738dcc5b16003eb62f113cb5fd58b00c8949d492053442070f0e10855"
EXPECTED_CUTOUT_SHA256 = "178243f565fbccef8e14bf3fc3ab75ab76014b5c6258f9f73390d1f3af5efb36"
DEPTH_RATIO = 0.020
SMOOTHING_LAMBDA = 0.25
MAX_DISPLACEMENT_CELLS = 0.35


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


def load_base_module():
    if sha256_file(BASE_GENERATOR) != EXPECTED_BASE_SHA256:
        raise RuntimeError("Frozen LocalRelief V1 generator SHA changed")
    module_spec = importlib.util.spec_from_file_location(
        "garden_cottage_v76_local_relief_v1", BASE_GENERATOR
    )
    if module_spec is None or module_spec.loader is None:
        raise RuntimeError(f"Unable to load {BASE_GENERATOR}")
    module = importlib.util.module_from_spec(module_spec)
    sys.modules[module_spec.name] = module
    module_spec.loader.exec_module(module)
    return module


def boundary_adjacency(mask: np.ndarray) -> dict[tuple[int, int], set[tuple[int, int]]]:
    height, width = mask.shape
    graph: dict[tuple[int, int], set[tuple[int, int]]] = defaultdict(set)

    def add_edge(a: tuple[int, int], b: tuple[int, int]) -> None:
        graph[a].add(b)
        graph[b].add(a)

    ys, xs = np.nonzero(mask)
    for row, col in zip(ys.tolist(), xs.tolist()):
        tl, tr = (row, col), (row, col + 1)
        br, bl = (row + 1, col + 1), (row + 1, col)
        if col == 0 or not mask[row, col - 1]:
            add_edge(tl, bl)
        if row == height - 1 or not mask[row + 1, col]:
            add_edge(bl, br)
        if col == width - 1 or not mask[row, col + 1]:
            add_edge(br, tr)
        if row == 0 or not mask[row - 1, col]:
            add_edge(tr, tl)
    return graph


def smooth_part06_positions(
    positions: np.ndarray,
    normals: np.ndarray,
    mask: np.ndarray,
) -> dict[str, float | int | str]:
    ys, xs = np.nonzero(mask)
    min_x, max_x = int(xs.min()), int(xs.max())
    min_y, max_y = int(ys.min()), int(ys.max())
    height_span = float(max_y + 1 - min_y)
    x_center = 0.5 * (min_x + max_x + 1)
    graph = boundary_adjacency(mask)

    def xy(corner: tuple[int, int]) -> np.ndarray:
        row, col = corner
        return np.asarray(
            ((float(col) - x_center) / height_span,
             float(max_y + 1 - row) / height_span),
            dtype=np.float64,
        )

    def float32_key(point: np.ndarray) -> tuple[bytes, bytes]:
        return np.float32(point[0]).tobytes(), np.float32(point[1]).tobytes()

    replacements: dict[tuple[bytes, bytes], np.ndarray] = {}
    maximum_displacement = 0.0
    moved_corners = 0
    for corner, neighbours in graph.items():
        if len(neighbours) != 2:
            continue
        current = xy(corner)
        previous, following = (xy(item) for item in sorted(neighbours))
        delta = SMOOTHING_LAMBDA * (0.5 * (previous + following) - current)
        displacement_cells = float(np.linalg.norm(delta) * height_span)
        if displacement_cells > MAX_DISPLACEMENT_CELLS:
            delta *= MAX_DISPLACEMENT_CELLS / displacement_cells
            displacement_cells = MAX_DISPLACEMENT_CELLS
        if displacement_cells <= 1.0e-9:
            continue
        replacements[float32_key(current)] = current + delta
        maximum_displacement = max(maximum_displacement, displacement_cells)
        moved_corners += 1

    moved_vertices = 0
    for index in range(len(positions)):
        key = (positions[index, 0].tobytes(), positions[index, 1].tobytes())
        replacement = replacements.get(key)
        if replacement is None:
            continue
        positions[index, 0:2] = replacement.astype(np.float32)
        moved_vertices += 1

    side_groups = 0
    index = 0
    while index < len(normals):
        if abs(float(normals[index, 2])) >= 0.5:
            index += 1
            continue
        if index + 3 >= len(normals) or not np.all(np.abs(normals[index:index + 4, 2]) < 0.5):
            raise RuntimeError("Side-wall vertices are not grouped in canonical quads")
        edge = positions[index + 3, 0:2].astype(np.float64) - positions[index, 0:2].astype(np.float64)
        length = float(np.linalg.norm(edge))
        if length <= 1.0e-9:
            raise RuntimeError("Smoothing collapsed a side-wall edge")
        side_normal = np.asarray((edge[1] / length, -edge[0] / length, 0.0), dtype=np.float32)
        normals[index:index + 4] = side_normal
        side_groups += 1
        index += 4

    return {
        "schema": "PART06_BOUNDARY_LAPLACIAN_V02",
        "lambda": SMOOTHING_LAMBDA,
        "passes": 1,
        "max_displacement_cells": maximum_displacement,
        "cap_cells": MAX_DISPLACEMENT_CELLS,
        "boundary_corners": len(graph),
        "moved_boundary_corners": moved_corners,
        "moved_vertices": moved_vertices,
        "side_quads_reoriented": side_groups,
    }


def main() -> int:
    base = load_base_module()
    original = next(spec for spec in base.PARTS if spec.part_id == "Part_06")
    part_spec = dataclasses.replace(original, depth_ratio=DEPTH_RATIO)
    source = (base.SOURCE_ROOT / part_spec.source_name).resolve()
    if sha256_file(source) != part_spec.source_sha256:
        raise RuntimeError("Part_06 source SHA mismatch")

    with Image.open(source) as image:
        rgb = np.asarray(image.convert("RGB"), dtype=np.uint8)
    mask, relief, rgba, segmentation = base.segment_foreground(rgb, part_spec)
    positions, normals, uvs, indices, mesh_stats = base.build_mesh(mask, relief, part_spec)
    smoothing = smooth_part06_positions(positions, normals, mask)

    triangles = positions[indices.reshape((-1, 3))]
    twice_area = np.linalg.norm(
        np.cross(triangles[:, 1] - triangles[:, 0], triangles[:, 2] - triangles[:, 0]),
        axis=1,
    )
    minimum_twice_area = float(twice_area.min())
    if minimum_twice_area <= 1.0e-10:
        raise RuntimeError("Smoothed Part_06 contains a degenerate triangle")

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
    if sha256_bytes(texture_payload) != EXPECTED_CUTOUT_SHA256:
        raise RuntimeError("Part_06 cutout/alpha bytes changed in the geometry-only V02 recipe")
    cutout_path.write_bytes(texture_payload)

    unique_suffix = uuid.uuid4().hex
    build_id = f"localrelief-part_06-v02-{unique_suffix}"
    task_id = f"local-v02-{unique_suffix}"
    base.write_glb(
        glb_path, positions, normals, uvs, indices,
        texture_payload, part_spec, build_id,
    )
    validation = base.validate_glb(glb_path)
    if validation["vertices"] != mesh_stats["vertices"] or validation["triangles"] != mesh_stats["triangles"]:
        raise RuntimeError("Part_06 GLB serialization changed the smoothed mesh counts")

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
        "base_generator": str(BASE_GENERATOR),
        "base_generator_sha256": EXPECTED_BASE_SHA256,
        "recipe": "Part06SmoothedThinReliefV02",
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
        print(f"[Part06 LocalRelief V02] ERROR: {error}", file=sys.stderr)
        raise
