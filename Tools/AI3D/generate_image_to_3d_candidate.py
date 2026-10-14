#!/usr/bin/env python3
"""Generate one versioned Meshy or Tripo image-to-3D candidate.

Credentials are read only from the provider-specific process environment:

  * MESHY_API_KEY
  * TRIPO_API_KEY

The script never writes credentials or authorization headers. Persisted metadata
contains provenance and task identifiers but excludes signed download URLs and
base64 image payloads.
"""

from __future__ import annotations

import argparse
import base64
import hashlib
import io
import ipaddress
import json
import mimetypes
import os
import re
import socket
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid
from pathlib import Path
from typing import Any

from PIL import Image


MESHY_BASE = "https://api.meshy.ai/openapi/v1/image-to-3d"
TRIPO_BASE = os.environ.get("TRIPO_BASE_URL", "https://api.tripo3d.com/v2/openapi").rstrip("/")
TRIPO_UPLOAD = f"{TRIPO_BASE}/upload/sts"
TRIPO_TASK = f"{TRIPO_BASE}/task"
TERMINAL_MESHY = {"SUCCEEDED", "FAILED", "CANCELED", "EXPIRED"}
TERMINAL_TRIPO = {"success", "failed", "banned", "expired", "cancelled", "unknown"}
ORIGINAL_GETADDRINFO = socket.getaddrinfo


def redact_error_text(text: str) -> str:
    text = re.sub(r"data:image/[^;]+;base64,[A-Za-z0-9+/=]+", "<redacted-image-data>", text)
    text = re.sub(r"(Authorization[^\\n]*Bearer\\s+)[^\\s\"']+", r"\1<redacted>", text, flags=re.I)
    return text[:1500]


def install_tripo_process_dns_override() -> str | None:
    """Optionally override only the configured Tripo API host in this process.

    TLS still uses and validates the configured Tripo hostname. The
    optional address is not a credential and is never installed system-wide.
    """

    forced_ip = os.environ.get("TRIPO_RESOLVE_IP", "").strip()
    if not forced_ip:
        return None
    ipaddress.ip_address(forced_ip)
    tripo_host = urllib.parse.urlparse(TRIPO_BASE).hostname
    if not tripo_host:
        raise RuntimeError(f"TRIPO_BASE_URL has no hostname: {TRIPO_BASE}")

    def getaddrinfo(host: str, port: int | str, *args: Any, **kwargs: Any):
        lookup_host = forced_ip if host == tripo_host else host
        return ORIGINAL_GETADDRINFO(lookup_host, port, *args, **kwargs)

    socket.getaddrinfo = getaddrinfo
    print(f"[Tripo] process-local DNS override installed for {tripo_host}", flush=True)
    return forced_ip


def install_meshy_process_dns_override() -> str | None:
    """Optionally override only the official Meshy API host in this process.

    This mirrors the existing Tripo recovery path. TLS continues to use and
    validate ``api.meshy.ai``; only socket address resolution is replaced.
    """

    forced_ip = os.environ.get("MESHY_RESOLVE_IP", "").strip()
    if not forced_ip:
        return None
    ipaddress.ip_address(forced_ip)
    meshy_host = urllib.parse.urlparse(MESHY_BASE).hostname
    if not meshy_host:
        raise RuntimeError(f"MESHY_BASE has no hostname: {MESHY_BASE}")

    def getaddrinfo(host: str, port: int | str, *args: Any, **kwargs: Any):
        lookup_host = forced_ip if host == meshy_host else host
        return ORIGINAL_GETADDRINFO(lookup_host, port, *args, **kwargs)

    socket.getaddrinfo = getaddrinfo
    print(f"[Meshy] process-local DNS override installed for {meshy_host}", flush=True)
    return forced_ip


def request_json(
    url: str,
    api_key: str,
    *,
    method: str = "GET",
    payload: dict[str, Any] | None = None,
    body: bytes | None = None,
    content_type: str = "application/json",
    timeout: int = 300,
) -> dict[str, Any]:
    if payload is not None and body is not None:
        raise ValueError("payload and body are mutually exclusive")
    data = json.dumps(payload).encode("utf-8") if payload is not None else body
    headers = {
        "Authorization": f"Bearer {api_key}",
        "User-Agent": "PersonallyPuzzledVR-AI3D/1.0",
    }
    if data is not None:
        headers["Content-Type"] = content_type
    request = urllib.request.Request(url, data=data, method=method, headers=headers)
    try:
        with urllib.request.urlopen(request, timeout=timeout) as response:
            return json.loads(response.read().decode("utf-8"))
    except urllib.error.HTTPError as error:
        response_body = error.read().decode("utf-8", errors="replace")
        raise RuntimeError(f"HTTP {error.code}: {redact_error_text(response_body)}") from error


def download_bytes(url: str, timeout: int = 600) -> tuple[bytes, str]:
    request = urllib.request.Request(
        url,
        headers={"User-Agent": "PersonallyPuzzledVR-AI3D/1.0"},
    )
    with urllib.request.urlopen(request, timeout=timeout) as response:
        return response.read(), response.headers.get_content_type()


def download_file(url: str, path: Path) -> None:
    data, _ = download_bytes(url)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(data)
    print(f"[download] {path} ({len(data)} bytes)", flush=True)


def download_preview(url: str, path: Path) -> None:
    data, _ = download_bytes(url)
    with Image.open(io.BytesIO(data)) as image:
        image.convert("RGBA").save(path)
    print(f"[download] {path} ({path.stat().st_size} bytes)", flush=True)


def file_sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def image_data_uri(path: Path) -> str:
    mime = mimetypes.guess_type(path.name)[0] or "image/png"
    return f"data:{mime};base64," + base64.b64encode(path.read_bytes()).decode("ascii")


def make_metallic_smoothness(metallic_path: Path, roughness_path: Path, output_path: Path) -> None:
    metallic = Image.open(metallic_path).convert("RGB")
    roughness = Image.open(roughness_path).convert("L")
    if roughness.size != metallic.size:
        roughness = roughness.resize(metallic.size, Image.Resampling.LANCZOS)
    smoothness = roughness.point(lambda value: 255 - value)
    rgba = metallic.convert("RGBA")
    rgba.putalpha(smoothness)
    rgba.save(output_path)
    print(f"[pack] {output_path}", flush=True)


def write_metadata(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2, ensure_ascii=False), encoding="utf-8")
    print(f"[metadata] {path}", flush=True)


def write_attempt_journal(
    args: argparse.Namespace,
    provider: str,
    state: str,
    *,
    task_id: str = "",
    detail: str = "",
) -> None:
    """Atomically persist a credential-free provider submission receipt.

    The journal is deliberately written before a billed create-task POST and
    immediately after a task id is returned.  An ambiguous transport failure
    therefore becomes UNKNOWN_COMMIT instead of silently permitting a second
    billed submission.
    """

    path = args.attempt_journal
    if path is None:
        return
    payload = {
        "schema": "PPVRAI3DProviderAttemptV1",
        "provider": provider,
        "state": state,
        "updated_unix_ms": int(time.time() * 1000),
        "source": str(args.source),
        "source_sha256": file_sha256(args.source),
        "output_prefix": str(args.output_prefix),
        "metadata": str(args.metadata),
        "task_id": task_id,
        "resumed_existing_task": bool(args.existing_task_id),
        "detail": redact_error_text(detail),
        "credentials_persisted": False,
    }
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".tmp")
    with temporary.open("w", encoding="utf-8") as stream:
        stream.write(json.dumps(payload, indent=2, ensure_ascii=False) + "\n")
        stream.flush()
        os.fsync(stream.fileno())
    os.replace(temporary, path)
    try:
        directory_fd = os.open(path.parent, os.O_RDONLY)
        try:
            os.fsync(directory_fd)
        finally:
            os.close(directory_fd)
    except OSError:
        # Some filesystems reject directory fsync.  The file itself is already
        # fsynced and atomically renamed, so retain the receipt instead of
        # turning a successful provider response into an artificial failure.
        pass
    print(f"[{provider}] attempt journal: {state}", flush=True)


def require_safe_attempt_start(args: argparse.Namespace) -> None:
    path = args.attempt_journal
    if path is None or not path.exists():
        return
    try:
        record = json.loads(path.read_text(encoding="utf-8"))
    except Exception as error:
        raise RuntimeError(f"Provider attempt journal is unreadable: {path}: {error}") from error
    if record.get("schema") != "PPVRAI3DProviderAttemptV1":
        raise RuntimeError(f"Provider attempt journal has an unsupported schema: {path}")
    expected_source = str(args.source)
    expected_sha = file_sha256(args.source)
    if record.get("source") != expected_source or record.get("source_sha256") != expected_sha:
        raise RuntimeError(f"Provider attempt journal belongs to different source bytes: {path}")
    recorded_task = str(record.get("task_id") or "")
    if args.existing_task_id:
        if recorded_task and recorded_task != args.existing_task_id:
            raise RuntimeError(
                f"Requested resume task does not match the attempt journal task id: {path}"
            )
        return
    state = str(record.get("state") or "UNKNOWN")
    raise RuntimeError(
        "A prior provider attempt journal already exists with state "
        f"{state}; do not create another billed task. Resume its recorded task id "
        f"or audit UNKNOWN_COMMIT first: {path}"
    )


def poll_meshy(task_id: str, api_key: str, timeout_minutes: int) -> dict[str, Any]:
    deadline = time.time() + timeout_minutes * 60
    last_marker: tuple[str, int] | None = None
    while time.time() < deadline:
        task = request_json(f"{MESHY_BASE}/{task_id}", api_key)
        status = str(task.get("status", "UNKNOWN"))
        progress = int(task.get("progress") or 0)
        marker = (status, progress)
        if marker != last_marker:
            print(f"[Meshy] {task_id}: {status} {progress}%", flush=True)
            last_marker = marker
        if status in TERMINAL_MESHY:
            return task
        time.sleep(8)
    raise TimeoutError(f"Meshy task timed out after {timeout_minutes} minutes: {task_id}")


def generate_meshy(args: argparse.Namespace) -> None:
    api_key = os.environ.get("MESHY_API_KEY", "").strip()
    if not api_key:
        raise RuntimeError("MESHY_API_KEY is not available in the process environment")
    resolver_override = install_meshy_process_dns_override()
    ai_model = args.meshy_ai_model or (
        "meshy-t2" if args.meshy_model_type == "smart-topology" else "meshy-6"
    )
    payload = {
        "image_url": image_data_uri(args.source),
        "model_type": args.meshy_model_type,
        "ai_model": ai_model,
        "should_texture": True,
        "enable_pbr": True,
        "target_polycount": args.polycount,
        "target_formats": ["glb", "fbx"],
        "texture_prompt": args.texture_prompt,
    }
    if args.meshy_model_type == "standard":
        payload.update(
            {
                "should_remesh": True,
                "topology": "triangle",
                "save_pre_remeshed_model": False,
            }
        )
    if args.existing_task_id:
        task_id = args.existing_task_id
        write_attempt_journal(args, "Meshy", "RESUMING_TASK", task_id=task_id)
        print(f"[Meshy] resuming task {task_id}", flush=True)
    else:
        write_attempt_journal(args, "Meshy", "SUBMITTING_TASK")
        try:
            created = request_json(MESHY_BASE, api_key, method="POST", payload=payload)
        except Exception as error:
            write_attempt_journal(args, "Meshy", "UNKNOWN_COMMIT", detail=str(error))
            raise
        task_id = str(created.get("result") or "")
        if not task_id:
            write_attempt_journal(args, "Meshy", "UNKNOWN_COMMIT", detail="create response omitted task id")
            raise RuntimeError(f"Meshy did not return a task id: {created}")
        write_attempt_journal(args, "Meshy", "TASK_CREATED", task_id=task_id)
        print(f"[Meshy] created task {task_id}", flush=True)
    write_attempt_journal(args, "Meshy", "POLLING", task_id=task_id)
    task = poll_meshy(task_id, api_key, args.timeout_minutes)
    if task.get("status") != "SUCCEEDED":
        error = task.get("task_error") or task.get("error_message") or "unknown error"
        write_attempt_journal(
            args, "Meshy", "PROVIDER_FAILED", task_id=task_id, detail=str(error)
        )
        raise RuntimeError(f"Meshy task {task_id} ended as {task.get('status')}: {error}")

    model_urls = task.get("model_urls") or {}
    downloaded_models: list[str] = []
    for extension in ("glb", "fbx"):
        url = model_urls.get(extension)
        if not url:
            continue
        destination = Path(f"{args.output_prefix}_Hero.{extension}")
        download_file(url, destination)
        downloaded_models.append(str(destination))

    texture_sets = task.get("texture_urls") or []
    downloaded_textures: dict[str, str] = {}
    if texture_sets:
        for channel in ("base_color", "metallic", "normal", "roughness", "emission"):
            url = (texture_sets[0] or {}).get(channel)
            if not url:
                continue
            destination = Path(f"{args.output_prefix}_Hero_{channel}.png")
            download_file(url, destination)
            downloaded_textures[channel] = str(destination)
    if "metallic" in downloaded_textures and "roughness" in downloaded_textures:
        packed = Path(f"{args.output_prefix}_Hero_metallic_smoothness.png")
        make_metallic_smoothness(
            Path(downloaded_textures["metallic"]),
            Path(downloaded_textures["roughness"]),
            packed,
        )
        downloaded_textures["metallic_smoothness"] = str(packed)

    preview_path = None
    if task.get("thumbnail_url"):
        preview_path = Path(f"{args.output_prefix}_MeshyPreview.png")
        download_preview(task["thumbnail_url"], preview_path)

    write_metadata(
        args.metadata,
        {
            "provider": "Meshy",
            "task_id": task_id,
            "resumed_existing_task": bool(args.existing_task_id),
            "status": task.get("status"),
            "progress": task.get("progress"),
            "source": str(args.source),
            "source_sha256": file_sha256(args.source),
            "model_type": args.meshy_model_type,
            "ai_model": ai_model,
            "process_local_dns_override_used": bool(resolver_override),
            "requested_polycount": args.polycount,
            "models": downloaded_models,
            "textures": downloaded_textures,
            "preview": str(preview_path) if preview_path else None,
            "provider_output_keys": sorted(task.keys()),
            "credentials_persisted": False,
        },
    )
    write_attempt_journal(args, "Meshy", "SUCCEEDED", task_id=task_id)


def multipart_file_body(path: Path) -> tuple[bytes, str]:
    boundary = f"----PPVR-{uuid.uuid4().hex}"
    mime = mimetypes.guess_type(path.name)[0] or "application/octet-stream"
    chunks = [
        f"--{boundary}\r\n".encode(),
        (
            f'Content-Disposition: form-data; name="file"; filename="{path.name}"\r\n'
            f"Content-Type: {mime}\r\n\r\n"
        ).encode(),
        path.read_bytes(),
        f"\r\n--{boundary}--\r\n".encode(),
    ]
    return b"".join(chunks), f"multipart/form-data; boundary={boundary}"


def poll_tripo(task_id: str, api_key: str, timeout_minutes: int) -> dict[str, Any]:
    deadline = time.time() + timeout_minutes * 60
    last_marker: tuple[str, int] | None = None
    while time.time() < deadline:
        response = request_json(f"{TRIPO_TASK}/{task_id}", api_key)
        if response.get("code") not in (0, None):
            raise RuntimeError(f"Tripo status request failed: {response}")
        task = response.get("data") or {}
        status = str(task.get("status", "unknown"))
        progress = int(task.get("progress") or 0)
        marker = (status, progress)
        if marker != last_marker:
            print(f"[Tripo] {task_id}: {status} {progress}%", flush=True)
            last_marker = marker
        if status in TERMINAL_TRIPO:
            return task
        time.sleep(8)
    raise TimeoutError(f"Tripo task timed out after {timeout_minutes} minutes: {task_id}")


def generate_tripo(args: argparse.Namespace) -> None:
    api_key = os.environ.get("TRIPO_API_KEY", "").strip()
    if not api_key:
        raise RuntimeError("TRIPO_API_KEY is not available in the process environment")
    resolver_override = install_tripo_process_dns_override()
    if args.existing_task_id:
        task_id = args.existing_task_id
        write_attempt_journal(args, "Tripo3D", "RESUMING_TASK", task_id=task_id)
        print(f"[Tripo] resuming task {task_id}", flush=True)
    else:
        upload_body, upload_content_type = multipart_file_body(args.source)
        write_attempt_journal(args, "Tripo3D", "SUBMITTING_SOURCE_UPLOAD")
        try:
            upload_response = request_json(
                TRIPO_UPLOAD,
                api_key,
                method="POST",
                body=upload_body,
                content_type=upload_content_type,
            )
        except Exception as error:
            write_attempt_journal(args, "Tripo3D", "SOURCE_UPLOAD_UNKNOWN", detail=str(error))
            raise
        if upload_response.get("code") not in (0, None):
            write_attempt_journal(
                args, "Tripo3D", "SOURCE_UPLOAD_FAILED", detail=str(upload_response)
            )
            raise RuntimeError(f"Tripo upload failed: {upload_response}")
        image_token = str((upload_response.get("data") or {}).get("image_token") or "")
        if not image_token:
            write_attempt_journal(
                args, "Tripo3D", "SOURCE_UPLOAD_UNKNOWN", detail="upload response omitted image token"
            )
            raise RuntimeError(f"Tripo did not return an image token: {upload_response}")
        write_attempt_journal(args, "Tripo3D", "SOURCE_UPLOADED")
        print("[Tripo] source upload complete", flush=True)

        source_type = args.source.suffix.lower().lstrip(".")
        if source_type == "jpg":
            source_type = "jpeg"
        payload = {
            "type": "image_to_model",
            "model_version": args.tripo_model,
            "file": {"type": source_type, "file_token": image_token},
            "face_limit": args.polycount,
            "pbr": True,
            "texture": True,
            "texture_quality": args.tripo_texture_quality,
            "enable_image_autofix": True,
        }
        write_attempt_journal(args, "Tripo3D", "SUBMITTING_TASK")
        try:
            created = request_json(TRIPO_TASK, api_key, method="POST", payload=payload)
        except Exception as error:
            write_attempt_journal(args, "Tripo3D", "UNKNOWN_COMMIT", detail=str(error))
            raise
        if created.get("code") not in (0, None):
            write_attempt_journal(args, "Tripo3D", "UNKNOWN_COMMIT", detail=str(created))
            raise RuntimeError(f"Tripo task creation failed: {created}")
        task_id = str((created.get("data") or {}).get("task_id") or "")
        if not task_id:
            write_attempt_journal(
                args, "Tripo3D", "UNKNOWN_COMMIT", detail="create response omitted task id"
            )
            raise RuntimeError(f"Tripo did not return a task id: {created}")
        write_attempt_journal(args, "Tripo3D", "TASK_CREATED", task_id=task_id)
        print(f"[Tripo] created task {task_id}", flush=True)
    write_attempt_journal(args, "Tripo3D", "POLLING", task_id=task_id)
    task = poll_tripo(task_id, api_key, args.timeout_minutes)
    if task.get("status") != "success":
        write_attempt_journal(
            args, "Tripo3D", "PROVIDER_FAILED", task_id=task_id,
            detail=f"terminal status {task.get('status')}",
        )
        raise RuntimeError(f"Tripo task {task_id} ended as {task.get('status')}")

    output = task.get("output") or {}
    downloaded_models: list[str] = []
    primary_url = output.get("pbr_model") or output.get("model")
    if not primary_url:
        raise RuntimeError(f"Tripo task {task_id} returned no model URL")
    primary_path = Path(f"{args.output_prefix}_Hero.glb")
    download_file(primary_url, primary_path)
    downloaded_models.append(str(primary_path))
    if output.get("base_model") and output.get("base_model") != primary_url:
        base_path = Path(f"{args.output_prefix}_Base.glb")
        download_file(output["base_model"], base_path)
        downloaded_models.append(str(base_path))

    preview_path = None
    preview_url = output.get("rendered_image") or output.get("generated_image")
    if preview_url:
        preview_path = Path(f"{args.output_prefix}_TripoPreview.png")
        download_preview(preview_url, preview_path)

    write_metadata(
        args.metadata,
        {
            "provider": "Tripo3D",
            "task_id": task_id,
            "resumed_existing_task": bool(args.existing_task_id),
            "status": task.get("status"),
            "progress": task.get("progress"),
            "consumed_credit": task.get("consumed_credit"),
            "source": str(args.source),
            "source_sha256": file_sha256(args.source),
            "model_version": args.tripo_model,
            "texture_quality": args.tripo_texture_quality,
            "requested_face_limit": args.polycount,
            "base_url": TRIPO_BASE,
            "models": downloaded_models,
            "preview": str(preview_path) if preview_path else None,
            "provider_output_keys": sorted(output.keys()),
            "process_local_dns_override_used": bool(resolver_override),
            "credentials_persisted": False,
        },
    )
    write_attempt_journal(args, "Tripo3D", "SUCCEEDED", task_id=task_id)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--provider", choices=("meshy", "tripo"), required=True)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--output-prefix", type=Path, required=True)
    parser.add_argument("--metadata", type=Path, required=True)
    parser.add_argument(
        "--attempt-journal",
        type=Path,
        default=None,
        help=(
            "Credential-free atomic submission receipt. Existing receipts fail closed unless "
            "--existing-task-id resumes the same task."
        ),
    )
    parser.add_argument("--polycount", type=int, default=160_000)
    parser.add_argument("--timeout-minutes", type=int, default=35)
    parser.add_argument(
        "--existing-task-id",
        default="",
        help="Resume polling/downloading an already-created provider task without creating a new one.",
    )
    parser.add_argument(
        "--meshy-model-type",
        choices=("standard", "smart-topology"),
        default="standard",
        help=(
            "Use standard for the visual master, or smart-topology for the same-source "
            "native separated-parts candidate."
        ),
    )
    parser.add_argument(
        "--meshy-ai-model",
        default="",
        help="Optional explicit Meshy model; defaults to meshy-6 or meshy-t2 by model type.",
    )
    parser.add_argument(
        "--texture-prompt",
        default=(
            "premium aged dark walnut with readable grain and satin varnish; restrained "
            "antique brass with darker patina in recesses; optically clear warm glass; "
            "subtle cyan edge light only in the blank front label recess"
        ),
    )
    parser.add_argument("--tripo-model", default="v3.1-20260211")
    parser.add_argument(
        "--tripo-texture-quality",
        choices=("standard", "detailed", "extreme"),
        default="extreme",
    )
    args = parser.parse_args()
    args.source = args.source.expanduser().resolve()
    args.output_prefix = args.output_prefix.expanduser().resolve()
    args.metadata = args.metadata.expanduser().resolve()
    if args.attempt_journal is not None:
        args.attempt_journal = args.attempt_journal.expanduser().resolve()
    if not args.source.is_file():
        parser.error(f"source image does not exist: {args.source}")
    if args.meshy_model_type == "smart-topology" and not 100 <= args.polycount <= 15_000:
        parser.error("Meshy meshy-t2 target_polycount must be between 100 and 15000")
    args.output_prefix.parent.mkdir(parents=True, exist_ok=True)
    args.metadata.parent.mkdir(parents=True, exist_ok=True)
    return args


def main() -> None:
    args = parse_args()
    require_safe_attempt_start(args)
    if args.provider == "meshy":
        generate_meshy(args)
    else:
        generate_tripo(args)
    print(f"[AI3D] {args.provider} candidate complete: {args.output_prefix}", flush=True)


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        print(f"[AI3D] ERROR: {redact_error_text(str(error))}", file=sys.stderr, flush=True)
        raise
