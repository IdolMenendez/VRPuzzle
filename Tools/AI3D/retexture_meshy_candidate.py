#!/usr/bin/env python3
"""Retexture an existing successful Meshy task without persisting credentials."""

import argparse
import os
import time
from pathlib import Path

import generate_image_to_3d_candidate as core


RETEXTURE_BASE = "https://api.meshy.ai/openapi/v1/retexture"


def poll(task_id: str, api_key: str, timeout_minutes: int) -> dict:
    deadline = time.time() + timeout_minutes * 60
    last_marker = None
    while time.time() < deadline:
        task = core.request_json(f"{RETEXTURE_BASE}/{task_id}", api_key)
        marker = (str(task.get("status", "UNKNOWN")), int(task.get("progress") or 0))
        if marker != last_marker:
            print(f"[Meshy Retexture] {task_id}: {marker[0]} {marker[1]}%", flush=True)
            last_marker = marker
        if marker[0] in core.TERMINAL_MESHY:
            return task
        time.sleep(8)
    raise TimeoutError(f"Meshy retexture task timed out after {timeout_minutes} minutes: {task_id}")


def parse_args():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input-task-id", required=True)
    parser.add_argument("--output-prefix", type=Path, required=True)
    parser.add_argument("--metadata", type=Path, required=True)
    parser.add_argument("--style-prompt", required=True)
    parser.add_argument("--existing-task-id", default="")
    parser.add_argument("--timeout-minutes", type=int, default=35)
    args = parser.parse_args()
    args.output_prefix = args.output_prefix.expanduser().resolve()
    args.metadata = args.metadata.expanduser().resolve()
    args.output_prefix.parent.mkdir(parents=True, exist_ok=True)
    args.metadata.parent.mkdir(parents=True, exist_ok=True)
    return args


def main():
    args = parse_args()
    api_key = os.environ.get("MESHY_API_KEY", "").strip()
    if not api_key:
        raise RuntimeError("MESHY_API_KEY is not available in the process environment")

    if args.existing_task_id:
        task_id = args.existing_task_id
        print(f"[Meshy Retexture] resuming task {task_id}", flush=True)
    else:
        payload = {
            "input_task_id": args.input_task_id,
            "text_style_prompt": args.style_prompt,
            "ai_model": "meshy-6",
            "enable_original_uv": True,
            "enable_pbr": True,
            "hd_texture": True,
            "remove_lighting": True,
            "target_formats": ["glb", "fbx"],
            "alpha_thumbnail": True,
        }
        created = core.request_json(RETEXTURE_BASE, api_key, method="POST", payload=payload)
        task_id = str(created.get("result") or "")
        if not task_id:
            raise RuntimeError(f"Meshy did not return a retexture task id: {created}")
        print(f"[Meshy Retexture] created task {task_id}", flush=True)

    task = poll(task_id, api_key, args.timeout_minutes)
    if task.get("status") != "SUCCEEDED":
        error = task.get("task_error") or task.get("error_message") or "unknown error"
        raise RuntimeError(f"Meshy retexture task {task_id} ended as {task.get('status')}: {error}")

    downloaded_models = []
    for extension in ("glb", "fbx"):
        url = (task.get("model_urls") or {}).get(extension)
        if not url:
            continue
        destination = Path(f"{args.output_prefix}_Hero.{extension}")
        core.download_file(url, destination)
        downloaded_models.append(str(destination))

    downloaded_textures = {}
    texture_sets = task.get("texture_urls") or []
    if texture_sets:
        for channel in ("base_color", "metallic", "normal", "roughness", "emission"):
            url = (texture_sets[0] or {}).get(channel)
            if not url:
                continue
            destination = Path(f"{args.output_prefix}_Hero_{channel}.png")
            core.download_file(url, destination)
            downloaded_textures[channel] = str(destination)
    if "metallic" in downloaded_textures and "roughness" in downloaded_textures:
        packed = Path(f"{args.output_prefix}_Hero_metallic_smoothness.png")
        core.make_metallic_smoothness(
            Path(downloaded_textures["metallic"]),
            Path(downloaded_textures["roughness"]),
            packed,
        )
        downloaded_textures["metallic_smoothness"] = str(packed)

    preview_url = task.get("alpha_thumbnail_url") or task.get("thumbnail_url")
    preview_path = None
    if preview_url:
        preview_path = Path(f"{args.output_prefix}_MeshyPreview.png")
        core.download_preview(preview_url, preview_path)

    core.write_metadata(
        args.metadata,
        {
            "provider": "Meshy Retexture",
            "task_id": task_id,
            "input_task_id": args.input_task_id,
            "resumed_existing_task": bool(args.existing_task_id),
            "status": task.get("status"),
            "progress": task.get("progress"),
            "consumed_credits": task.get("consumed_credits"),
            "style_prompt": args.style_prompt,
            "enable_original_uv": True,
            "enable_pbr": True,
            "hd_texture": True,
            "remove_lighting": True,
            "models": downloaded_models,
            "textures": downloaded_textures,
            "preview": str(preview_path) if preview_path else None,
            "provider_output_keys": sorted(task.keys()),
            "credentials_persisted": False,
        },
    )
    print(f"[AI3D] Meshy retexture complete: {args.output_prefix}", flush=True)


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        print(f"[AI3D] ERROR: {core.redact_error_text(str(error))}", flush=True)
        raise
