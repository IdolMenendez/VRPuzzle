#!/usr/bin/env python3
"""Build the two Garden Cottage V76 semantic fallback parts entirely locally.

The output is not a rectangular billboard.  A foreground mask is sampled into an
indexed grid, then extruded into a closed, shallow mesh containing independently
indexed front, back, and silhouette side-wall triangles.  The RGBA cutout is
embedded in the GLB buffer as a PNG texture.  No network or credential access is
performed by this program.
"""

from __future__ import annotations

import argparse
import hashlib
import io
import json
import math
import os
import struct
import sys
import uuid
from collections import deque
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

import numpy as np
from PIL import Image


PROJECT_ROOT = Path("/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR")
SOURCE_ROOT = PROJECT_ROOT / "Assets/GardenCottage/V76/PartSources"
OUTPUT_ROOT = PROJECT_ROOT / "Assets/GardenCottage/V76/Parts3D/DerivedLocalRelief"
METADATA_ROOT = (
    PROJECT_ROOT
    / "Artifacts/Checkpoints/169_GardenCottage_V76_ProviderSemanticReplacement_20260824/Provider"
)


@dataclass(frozen=True)
class PartSpec:
    part_id: str
    semantic_name: str
    source_name: str
    source_sha256: str
    output_stem: str
    metadata_name: str
    depth_ratio: float
    mask_threshold: float
    grid_long_side: int
    contact_band_ratio: float = 0.0
    minimum_contact_vertices: int = 0


PARTS = (
    PartSpec(
        part_id="Part_06",
        semantic_name="CurvedFloweringVine",
        source_name=(
            "GardenCottage_Part_06_CurvedFloweringVine_"
            "SourceEnvelope_V76_R03_CompactWallRelief.png"
        ),
        source_sha256="ae1d7acffdd978088714e3593b39032e6f982d38e35f8f7a307c5ac5bcfd4417",
        output_stem=(
            "GardenCottage_Part_06_CurvedFloweringVine_"
            "V76_LocalReliefFallback"
        ),
        metadata_name="Part_06_CurvedFloweringVine_V76_LOCAL_RELIEF_BUILD.json",
        depth_ratio=0.050,
        mask_threshold=18.0,
        grid_long_side=320,
    ),
    PartSpec(
        part_id="Part_07",
        semantic_name="SignFenceFlowerbed",
        source_name=(
            "GardenCottage_Part_07_SignFenceFlowerbed_"
            "SourceEnvelope_V76_R05_XCompressed.png"
        ),
        source_sha256="715fb897c5fc4e79a7da1950a16851b178f4e3478fd0217261add1355f8def85",
        output_stem=(
            "GardenCottage_Part_07_SignFenceFlowerbed_"
            "V76_LocalReliefFallback"
        ),
        metadata_name="Part_07_SignFenceFlowerbed_V76_LOCAL_RELIEF_BUILD.json",
        depth_ratio=0.080,
        mask_threshold=18.0,
        grid_long_side=320,
        # Preserve the organic lower silhouette.  The source already supplies
        # enough indexed vertices on its true minimum-Y boundary for the strict
        # contact gate; flattening an interior band would create degenerate
        # triangles and an artificial shelf.
        contact_band_ratio=0.0,
        minimum_contact_vertices=48,
    ),
)


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def shifted(mask: np.ndarray, dy: int, dx: int) -> np.ndarray:
    result = np.zeros_like(mask, dtype=bool)
    height, width = mask.shape
    src_y0 = max(0, -dy)
    src_y1 = min(height, height - dy)
    src_x0 = max(0, -dx)
    src_x1 = min(width, width - dx)
    dst_y0 = src_y0 + dy
    dst_y1 = src_y1 + dy
    dst_x0 = src_x0 + dx
    dst_x1 = src_x1 + dx
    if src_y1 > src_y0 and src_x1 > src_x0:
        result[dst_y0:dst_y1, dst_x0:dst_x1] = mask[
            src_y0:src_y1, src_x0:src_x1
        ]
    return result


def dilate(mask: np.ndarray) -> np.ndarray:
    result = np.zeros_like(mask, dtype=bool)
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            result |= shifted(mask, dy, dx)
    return result


def erode(mask: np.ndarray) -> np.ndarray:
    result = np.ones_like(mask, dtype=bool)
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            result &= shifted(mask, dy, dx)
    return result


def largest_component(mask: np.ndarray) -> np.ndarray:
    height, width = mask.shape
    visited = np.zeros_like(mask, dtype=bool)
    best: list[tuple[int, int]] = []
    for y, x in np.argwhere(mask):
        y = int(y)
        x = int(x)
        if visited[y, x]:
            continue
        visited[y, x] = True
        queue: deque[tuple[int, int]] = deque([(y, x)])
        component: list[tuple[int, int]] = []
        while queue:
            cy, cx = queue.popleft()
            component.append((cy, cx))
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    if dy == 0 and dx == 0:
                        continue
                    ny, nx = cy + dy, cx + dx
                    if (
                        0 <= ny < height
                        and 0 <= nx < width
                        and mask[ny, nx]
                        and not visited[ny, nx]
                    ):
                        visited[ny, nx] = True
                        queue.append((ny, nx))
        if len(component) > len(best):
            best = component
    result = np.zeros_like(mask, dtype=bool)
    if best:
        ys, xs = zip(*best)
        result[np.asarray(ys), np.asarray(xs)] = True
    return result


def segment_foreground(
    rgb: np.ndarray, spec: PartSpec
) -> tuple[np.ndarray, np.ndarray, np.ndarray, dict[str, Any]]:
    height, width, _ = rgb.shape
    border_width = max(8, int(round(min(height, width) * 0.035)))
    border = np.concatenate(
        (
            rgb[:border_width].reshape(-1, 3),
            rgb[-border_width:].reshape(-1, 3),
            rgb[border_width:-border_width, :border_width].reshape(-1, 3),
            rgb[border_width:-border_width, -border_width:].reshape(-1, 3),
        ),
        axis=0,
    ).astype(np.float32)
    background = np.median(border, axis=0)
    delta_rgb = rgb.astype(np.float32) - background[None, None, :]
    score = np.sqrt(np.sum(delta_rgb * delta_rgb, axis=2))

    # Soft generated-image shadows are nearly neutral translations of the local
    # background.  Suppressing only that narrow color direction removes the cast
    # shadow while retaining brown wood, pale flowers, and the cream sign face.
    channel_spread = np.ptp(delta_rgb, axis=2)
    neutral_shadow = (delta_rgb.mean(axis=2) < -4.0) & (channel_spread < 10.0)
    score = np.where(neutral_shadow, score * 0.25, score)

    if height >= width:
        grid_height = spec.grid_long_side
        grid_width = max(8, int(round(width * grid_height / height)))
    else:
        grid_width = spec.grid_long_side
        grid_height = max(8, int(round(height * grid_width / width)))

    score_grid = np.asarray(
        Image.fromarray(score.astype(np.float32), mode="F").resize(
            (grid_width, grid_height), Image.Resampling.BILINEAR
        ),
        dtype=np.float32,
    )
    mask = score_grid >= spec.mask_threshold
    mask = erode(dilate(mask))
    mask = largest_component(mask)
    if int(mask.sum()) < 64:
        raise RuntimeError(f"{spec.part_id}: foreground mask is unexpectedly small")

    # Re-close after component selection without swallowing disconnected shadows.
    mask = largest_component(erode(dilate(mask)))
    ys, xs = np.nonzero(mask)
    if not len(xs):
        raise RuntimeError(f"{spec.part_id}: no foreground after cleanup")

    foreground_scores = score_grid[mask]
    relief_scale = max(float(np.percentile(foreground_scores, 95.0)), 1.0)
    relief_cells = np.clip(score_grid / relief_scale, 0.0, 1.0)
    relief_cells[~mask] = 0.0

    alpha_small = Image.fromarray(mask.astype(np.uint8) * 255, mode="L")
    alpha = np.asarray(
        alpha_small.resize((width, height), Image.Resampling.LANCZOS),
        dtype=np.uint8,
    )
    rgba = np.empty((height, width, 4), dtype=np.uint8)
    rgba[:, :, :3] = rgb
    rgba[:, :, 3] = alpha

    stats = {
        "source_width": width,
        "source_height": height,
        "grid_width": grid_width,
        "grid_height": grid_height,
        "background_rgb": [int(round(value)) for value in background],
        "mask_threshold": spec.mask_threshold,
        "foreground_cells": int(mask.sum()),
        "foreground_bbox_grid": {
            "min_x": int(xs.min()),
            "min_y": int(ys.min()),
            "max_x": int(xs.max()),
            "max_y": int(ys.max()),
        },
        "foreground_fill_ratio": float(mask.mean()),
    }
    return mask, relief_cells, rgba, stats


def build_mesh(
    mask: np.ndarray,
    relief_cells: np.ndarray,
    spec: PartSpec,
) -> tuple[np.ndarray, np.ndarray, np.ndarray, np.ndarray, dict[str, Any]]:
    grid_height, grid_width = mask.shape
    ys, xs = np.nonzero(mask)
    min_x, max_x = int(xs.min()), int(xs.max())
    min_y, max_y = int(ys.min()), int(ys.max())
    height_span = float(max_y + 1 - min_y)
    x_center = 0.5 * (min_x + max_x + 1)

    relief_sum = np.zeros((grid_height + 1, grid_width + 1), dtype=np.float32)
    relief_count = np.zeros_like(relief_sum)
    weighted = relief_cells * mask.astype(np.float32)
    for oy, ox in ((0, 0), (1, 0), (0, 1), (1, 1)):
        relief_sum[oy : oy + grid_height, ox : ox + grid_width] += weighted
        relief_count[oy : oy + grid_height, ox : ox + grid_width] += mask
    relief_vertex = np.divide(
        relief_sum,
        np.maximum(relief_count, 1.0),
        out=np.zeros_like(relief_sum),
        where=relief_count > 0.0,
    )

    positions: list[tuple[float, float, float]] = []
    normals: list[tuple[float, float, float]] = []
    uvs: list[tuple[float, float]] = []
    indices: list[int] = []
    front_vertices: dict[tuple[int, int], int] = {}
    back_vertices: dict[tuple[int, int], int] = {}

    depth = spec.depth_ratio

    def xy_for_corner(row: int, col: int) -> tuple[float, float]:
        x = (float(col) - x_center) / height_span
        y = float(max_y + 1 - row) / height_span
        if spec.contact_band_ratio > 0.0 and y <= spec.contact_band_ratio:
            y = 0.0
        return x, y

    def uv_for_corner(row: int, col: int) -> tuple[float, float]:
        # glTF texture coordinates use the image's top-left convention.  The
        # embedded PNG is stored top-to-bottom, so V must follow the source row
        # directly.  Flipping V here would map the flowerbed to the top and the
        # hanging sign to the bottom even though the relief geometry is upright.
        return float(col) / grid_width, float(row) / grid_height

    def front_z(row: int, col: int) -> float:
        # The back is planar.  The front varies subtly with source contrast but
        # remains inside the declared total depth ratio.
        return depth * (0.15 + 0.35 * float(relief_vertex[row, col]))

    def side_uv_for_cell(row: int, col: int) -> tuple[float, float]:
        """Choose a nearby, confidently foreground texel for an extrusion wall.

        A boundary cell can be selected by the downsampled silhouette even when
        its exact centre still contains the pale antialiased source matte.  Using
        that centre on every side-wall vertex creates white paper-like ribs in
        Unity at a three-quarter camera angle.  Search only a small local radius
        and prefer the strongest foreground response while mildly penalising
        distance, preserving local semantic colour without sampling the matte.
        """
        best_row, best_col = row, col
        best_score = -1.0
        radius = 3
        for candidate_row in range(max(0, row - radius), min(grid_height, row + radius + 1)):
            for candidate_col in range(max(0, col - radius), min(grid_width, col + radius + 1)):
                if not mask[candidate_row, candidate_col]:
                    continue
                distance = math.hypot(candidate_row - row, candidate_col - col)
                score = float(relief_cells[candidate_row, candidate_col]) - 0.025 * distance
                if score > best_score:
                    best_score = score
                    best_row, best_col = candidate_row, candidate_col
        return (
            (float(best_col) + 0.5) / grid_width,
            (float(best_row) + 0.5) / grid_height,
        )

    def add_vertex(
        position: tuple[float, float, float],
        normal: tuple[float, float, float],
        uv: tuple[float, float],
    ) -> int:
        index = len(positions)
        positions.append(position)
        normals.append(normal)
        uvs.append(uv)
        return index

    def surface_vertex(row: int, col: int, front: bool) -> int:
        table = front_vertices if front else back_vertices
        key = (row, col)
        if key in table:
            return table[key]
        x, y = xy_for_corner(row, col)
        z = front_z(row, col) if front else -0.5 * depth
        normal = (0.0, 0.0, 1.0) if front else (0.0, 0.0, -1.0)
        table[key] = add_vertex((x, y, z), normal, uv_for_corner(row, col))
        return table[key]

    boundary_edges = 0
    for row, col in zip(ys.tolist(), xs.tolist()):
        tl = (row, col)
        tr = (row, col + 1)
        br = (row + 1, col + 1)
        bl = (row + 1, col)
        f_tl, f_tr, f_br, f_bl = (
            surface_vertex(*tl, True),
            surface_vertex(*tr, True),
            surface_vertex(*br, True),
            surface_vertex(*bl, True),
        )
        b_tl, b_tr, b_br, b_bl = (
            surface_vertex(*tl, False),
            surface_vertex(*tr, False),
            surface_vertex(*br, False),
            surface_vertex(*bl, False),
        )
        indices.extend((f_tl, f_bl, f_br, f_tl, f_br, f_tr))
        indices.extend((b_tl, b_tr, b_br, b_tl, b_br, b_bl))

        exposed: list[tuple[tuple[int, int], tuple[int, int]]] = []
        if col == 0 or not mask[row, col - 1]:
            exposed.append((tl, bl))
        if row == grid_height - 1 or not mask[row + 1, col]:
            exposed.append((bl, br))
        if col == grid_width - 1 or not mask[row, col + 1]:
            exposed.append((br, tr))
        if row == 0 or not mask[row - 1, col]:
            exposed.append((tr, tl))

        for a, b in exposed:
            boundary_edges += 1
            ax, ay = xy_for_corner(*a)
            bx, by = xy_for_corner(*b)
            dx, dy = bx - ax, by - ay
            edge_length = math.hypot(dx, dy)
            if edge_length <= 1.0e-9:
                # Contact-band flattening can collapse a tiny vertical edge.
                continue
            side_normal = (dy / edge_length, -dx / edge_length, 0.0)
            # Sample a locally confident foreground texel for the entire
            # silhouette wall. Boundary cells frequently retain pale source-matte
            # antialiasing even though the downsampled mask classifies them as
            # foreground; the local search prevents white extrusion ribs in Unity.
            side_uv = side_uv_for_cell(row, col)
            af = add_vertex(
                (ax, ay, front_z(*a)), side_normal, side_uv
            )
            ab = add_vertex((ax, ay, -0.5 * depth), side_normal, side_uv)
            bb = add_vertex((bx, by, -0.5 * depth), side_normal, side_uv)
            bf = add_vertex(
                (bx, by, front_z(*b)), side_normal, side_uv
            )
            indices.extend((af, ab, bb, af, bb, bf))

    position_array = np.asarray(positions, dtype="<f4")
    normal_array = np.asarray(normals, dtype="<f4")
    uv_array = np.asarray(uvs, dtype="<f4")
    index_array = np.asarray(indices, dtype="<u4")
    referenced = np.unique(index_array)
    if len(referenced) != len(position_array):
        raise RuntimeError(
            f"{spec.part_id}: mesh contains unreferenced vertices "
            f"({len(position_array) - len(referenced)})"
        )
    contact_vertices = int(
        np.count_nonzero(np.abs(position_array[referenced, 1]) <= 1.0e-7)
    )
    if contact_vertices < spec.minimum_contact_vertices:
        raise RuntimeError(
            f"{spec.part_id}: only {contact_vertices} indexed contact vertices; "
            f"requires {spec.minimum_contact_vertices}"
        )
    actual_depth_ratio = float(
        position_array[:, 2].max() - position_array[:, 2].min()
    )
    stats = {
        "vertices": int(len(position_array)),
        "triangles": int(len(index_array) // 3),
        "indices": int(len(index_array)),
        "boundary_edges": int(boundary_edges),
        "indexed_contact_vertices": contact_vertices,
        "depth_ratio": actual_depth_ratio,
        "bounds_min": position_array.min(axis=0).astype(float).tolist(),
        "bounds_max": position_array.max(axis=0).astype(float).tolist(),
    }
    return position_array, normal_array, uv_array, index_array, stats


def png_bytes(rgba: np.ndarray) -> bytes:
    payload = io.BytesIO()
    Image.fromarray(rgba, mode="RGBA").save(
        payload, format="PNG", optimize=True, compress_level=9
    )
    return payload.getvalue()


def write_glb(
    path: Path,
    positions: np.ndarray,
    normals: np.ndarray,
    uvs: np.ndarray,
    indices: np.ndarray,
    texture_png: bytes,
    spec: PartSpec,
    build_id: str,
) -> None:
    binary = bytearray()
    views: list[dict[str, Any]] = []

    def add_view(data: bytes, target: int | None = None) -> int:
        while len(binary) % 4:
            binary.append(0)
        offset = len(binary)
        binary.extend(data)
        view: dict[str, Any] = {
            "buffer": 0,
            "byteOffset": offset,
            "byteLength": len(data),
        }
        if target is not None:
            view["target"] = target
        views.append(view)
        return len(views) - 1

    position_view = add_view(positions.tobytes(order="C"), 34962)
    normal_view = add_view(normals.tobytes(order="C"), 34962)
    uv_view = add_view(uvs.tobytes(order="C"), 34962)
    index_view = add_view(indices.tobytes(order="C"), 34963)
    image_view = add_view(texture_png)
    while len(binary) % 4:
        binary.append(0)

    position_min = positions.min(axis=0).astype(float).tolist()
    position_max = positions.max(axis=0).astype(float).tolist()
    document: dict[str, Any] = {
        "asset": {
            "version": "2.0",
            "generator": "PPVR Garden Cottage V76 LocalReliefFallback",
            "extras": {
                "provider": "LocalRelief",
                "build_id": build_id,
                "network_used": False,
            },
        },
        "scene": 0,
        "scenes": [{"nodes": [0], "name": f"{spec.part_id}_{spec.semantic_name}"}],
        "nodes": [{"mesh": 0, "name": f"{spec.part_id}_{spec.semantic_name}"}],
        "meshes": [
            {
                "name": f"{spec.output_stem}_Hero",
                "primitives": [
                    {
                        "attributes": {
                            "POSITION": 0,
                            "NORMAL": 1,
                            "TEXCOORD_0": 2,
                        },
                        "indices": 3,
                        "material": 0,
                        "mode": 4,
                    }
                ],
            }
        ],
        "materials": [
            {
                "name": f"{spec.output_stem}_RGBA",
                "pbrMetallicRoughness": {
                    "baseColorTexture": {"index": 0},
                    "baseColorFactor": [1.0, 1.0, 1.0, 1.0],
                    "metallicFactor": 0.0,
                    "roughnessFactor": 0.78,
                },
                "alphaMode": "MASK",
                "alphaCutoff": 0.20,
                "doubleSided": False,
            }
        ],
        "textures": [{"sampler": 0, "source": 0}],
        "samplers": [
            {
                "magFilter": 9729,
                "minFilter": 9987,
                "wrapS": 33071,
                "wrapT": 33071,
            }
        ],
        "images": [
            {
                "name": f"{spec.output_stem}_Cutout",
                "bufferView": image_view,
                "mimeType": "image/png",
            }
        ],
        "accessors": [
            {
                "bufferView": position_view,
                "componentType": 5126,
                "count": int(len(positions)),
                "type": "VEC3",
                "min": position_min,
                "max": position_max,
            },
            {
                "bufferView": normal_view,
                "componentType": 5126,
                "count": int(len(normals)),
                "type": "VEC3",
            },
            {
                "bufferView": uv_view,
                "componentType": 5126,
                "count": int(len(uvs)),
                "type": "VEC2",
            },
            {
                "bufferView": index_view,
                "componentType": 5125,
                "count": int(len(indices)),
                "type": "SCALAR",
                "min": [int(indices.min())],
                "max": [int(indices.max())],
            },
        ],
        "bufferViews": views,
        "buffers": [{"byteLength": len(binary)}],
    }

    json_chunk = json.dumps(document, separators=(",", ":"), ensure_ascii=True).encode(
        "utf-8"
    )
    json_chunk += b" " * ((-len(json_chunk)) % 4)
    total_length = 12 + 8 + len(json_chunk) + 8 + len(binary)
    payload = bytearray(struct.pack("<4sII", b"glTF", 2, total_length))
    payload.extend(struct.pack("<II", len(json_chunk), 0x4E4F534A))
    payload.extend(json_chunk)
    payload.extend(struct.pack("<II", len(binary), 0x004E4942))
    payload.extend(binary)
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_bytes(payload)
    os.replace(temporary, path)


def validate_glb(path: Path) -> dict[str, Any]:
    payload = path.read_bytes()
    if len(payload) < 28:
        raise RuntimeError(f"{path}: GLB is too small")
    magic, version, declared_length = struct.unpack_from("<4sII", payload, 0)
    if magic != b"glTF" or version != 2 or declared_length != len(payload):
        raise RuntimeError(f"{path}: invalid GLB 2.0 header")
    offset = 12
    chunks: dict[int, bytes] = {}
    while offset < len(payload):
        chunk_length, chunk_type = struct.unpack_from("<II", payload, offset)
        offset += 8
        chunk = payload[offset : offset + chunk_length]
        if len(chunk) != chunk_length:
            raise RuntimeError(f"{path}: truncated GLB chunk")
        chunks[chunk_type] = chunk
        offset += chunk_length
    if offset != len(payload) or 0x4E4F534A not in chunks or 0x004E4942 not in chunks:
        raise RuntimeError(f"{path}: missing JSON or BIN chunk")
    document = json.loads(chunks[0x4E4F534A].rstrip(b" \x00"))
    binary = chunks[0x004E4942]
    if int(document["buffers"][0]["byteLength"]) != len(binary):
        raise RuntimeError(f"{path}: BIN length does not match buffer declaration")
    views = document.get("bufferViews", [])
    for index, view in enumerate(views):
        start = int(view.get("byteOffset", 0))
        end = start + int(view["byteLength"])
        if start < 0 or end > len(binary):
            raise RuntimeError(f"{path}: bufferView {index} is out of range")

    component_sizes = {5126: 4, 5125: 4}
    component_counts = {"SCALAR": 1, "VEC2": 2, "VEC3": 3}
    for index, accessor in enumerate(document.get("accessors", [])):
        count = int(accessor["count"])
        if count <= 0:
            raise RuntimeError(f"{path}: accessor {index} is empty")
        view = views[int(accessor["bufferView"])]
        required = (
            int(accessor.get("byteOffset", 0))
            + count
            * component_sizes[int(accessor["componentType"])]
            * component_counts[str(accessor["type"])]
        )
        if required > int(view["byteLength"]):
            raise RuntimeError(f"{path}: accessor {index} exceeds its bufferView")

    position_accessor = document["accessors"][0]
    index_accessor = document["accessors"][3]
    position_view = views[int(position_accessor["bufferView"])]
    index_view = views[int(index_accessor["bufferView"])]
    positions = np.frombuffer(
        binary,
        dtype="<f4",
        count=int(position_accessor["count"]) * 3,
        offset=int(position_view.get("byteOffset", 0)),
    ).reshape(-1, 3)
    indices = np.frombuffer(
        binary,
        dtype="<u4",
        count=int(index_accessor["count"]),
        offset=int(index_view.get("byteOffset", 0)),
    )
    if len(indices) % 3 or int(indices.max()) >= len(positions):
        raise RuntimeError(f"{path}: invalid triangle index accessor")
    if len(np.unique(indices)) != len(positions):
        raise RuntimeError(f"{path}: validation found unreferenced vertices")

    image = document["images"][0]
    image_view = views[int(image["bufferView"])]
    image_start = int(image_view.get("byteOffset", 0))
    image_end = image_start + int(image_view["byteLength"])
    embedded_png = binary[image_start:image_end]
    if not embedded_png.startswith(b"\x89PNG\r\n\x1a\n"):
        raise RuntimeError(f"{path}: embedded image is not a PNG")
    with Image.open(io.BytesIO(embedded_png)) as decoded:
        decoded.load()
        if decoded.mode != "RGBA" or decoded.width <= 0 or decoded.height <= 0:
            raise RuntimeError(f"{path}: embedded PNG is not non-empty RGBA")
        alpha = np.asarray(decoded.getchannel("A"), dtype=np.uint8)
        if int(alpha.max()) == 0 or int(np.count_nonzero(alpha)) == alpha.size:
            raise RuntimeError(f"{path}: embedded PNG lacks a cutout alpha channel")
        texture_size = [decoded.width, decoded.height]

    return {
        "glb_version": version,
        "glb_bytes": len(payload),
        "json_parsed": True,
        "buffer_bytes": len(binary),
        "buffer_views": len(views),
        "accessors": len(document["accessors"]),
        "vertices": int(len(positions)),
        "triangles": int(len(indices) // 3),
        "index_max": int(indices.max()),
        "embedded_png_bytes": len(embedded_png),
        "embedded_png_rgba": True,
        "embedded_texture_size": texture_size,
    }


def build_part(spec: PartSpec, source_override: Path | None = None) -> dict[str, Any]:
    source = (source_override or (SOURCE_ROOT / spec.source_name)).resolve()
    if not source.is_file():
        raise FileNotFoundError(source)
    source_sha = sha256_file(source)
    if source_sha != spec.source_sha256:
        raise RuntimeError(
            f"{spec.part_id}: source SHA mismatch: {source_sha} != {spec.source_sha256}"
        )

    with Image.open(source) as image:
        rgb = np.asarray(image.convert("RGB"), dtype=np.uint8)
    mask, relief, rgba, segmentation = segment_foreground(rgb, spec)
    positions, normals, uvs, indices, mesh_stats = build_mesh(mask, relief, spec)

    cutout_path = (OUTPUT_ROOT / f"{spec.output_stem}_Cutout.png").resolve()
    glb_path = (OUTPUT_ROOT / f"{spec.output_stem}_Hero.glb").resolve()
    metadata_path = (METADATA_ROOT / spec.metadata_name).resolve()
    OUTPUT_ROOT.mkdir(parents=True, exist_ok=True)
    METADATA_ROOT.mkdir(parents=True, exist_ok=True)
    texture_payload = png_bytes(rgba)
    cutout_path.write_bytes(texture_payload)

    unique_suffix = uuid.uuid4().hex
    build_id = f"localrelief-{spec.part_id.lower()}-{unique_suffix}"
    task_id = f"local-{unique_suffix}"
    write_glb(
        glb_path,
        positions,
        normals,
        uvs,
        indices,
        texture_payload,
        spec,
        build_id,
    )
    validation = validate_glb(glb_path)
    if validation["vertices"] != mesh_stats["vertices"]:
        raise RuntimeError(f"{spec.part_id}: vertex count changed during serialization")
    if validation["triangles"] != mesh_stats["triangles"]:
        raise RuntimeError(f"{spec.part_id}: triangle count changed during serialization")

    record: dict[str, Any] = {
        "schema": "PPVRLocalReliefBuildV1",
        "provider": "LocalRelief",
        "status": "SUCCEEDED",
        "progress": 100,
        "build_id": build_id,
        "task_id": task_id,
        "created_utc": utc_now(),
        "source": str(source),
        "source_sha256": source_sha,
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
        "generator": str(Path(__file__).resolve()),
        "segmentation": segmentation,
        "mesh": mesh_stats,
        "validation": validation,
    }
    temporary = metadata_path.with_suffix(metadata_path.suffix + ".tmp")
    temporary.write_text(json.dumps(record, indent=2) + "\n", encoding="utf-8")
    os.replace(temporary, metadata_path)
    record["metadata"] = str(metadata_path)
    return record


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--part",
        choices=("06", "07", "all"),
        default="all",
        help="Build one fallback part or both (default: all).",
    )
    parser.add_argument("--part06-source", type=Path)
    parser.add_argument("--part07-source", type=Path)
    arguments = parser.parse_args()
    overrides = {"Part_06": arguments.part06_source, "Part_07": arguments.part07_source}
    selected = [
        spec
        for spec in PARTS
        if arguments.part == "all" or spec.part_id.endswith(arguments.part)
    ]
    results = [build_part(spec, overrides[spec.part_id]) for spec in selected]
    print(json.dumps(results, indent=2))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as error:
        print(f"[LocalRelief] ERROR: {error}", file=sys.stderr)
        raise
