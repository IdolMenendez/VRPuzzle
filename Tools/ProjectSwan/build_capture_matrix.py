#!/usr/bin/env python3
"""Build a labelled Project Swan screenshot matrix without altering sources."""

from __future__ import annotations

import argparse
import math
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


def load_font(size: int) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    candidates = (
        "/System/Library/Fonts/Supplemental/Arial Bold.ttf",
        "/System/Library/Fonts/Supplemental/Arial.ttf",
    )
    for candidate in candidates:
        if Path(candidate).is_file():
            return ImageFont.truetype(candidate, size)
    return ImageFont.load_default()


def parse_item(value: str) -> tuple[str, Path]:
    if "=" not in value:
        raise argparse.ArgumentTypeError("item must be LABEL=/absolute/path.png")
    label, raw_path = value.split("=", 1)
    path = Path(raw_path)
    if not label or not path.is_absolute() or not path.is_file():
        raise argparse.ArgumentTypeError(
            "item label must be non-empty and path must be an existing absolute file"
        )
    return label, path


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--columns", type=int, default=3)
    parser.add_argument("--cell-width", type=int, default=600)
    parser.add_argument("--gap", type=int, default=12)
    parser.add_argument("items", nargs="+", type=parse_item)
    args = parser.parse_args()

    if not args.output.is_absolute() or args.columns < 1 or args.cell_width < 160:
        parser.error("output must be absolute and dimensions must be valid")

    opened: list[tuple[str, Image.Image]] = []
    for label, path in args.items:
        image = Image.open(path).convert("RGB")
        height = round(args.cell_width * image.height / image.width)
        opened.append((label, image.resize((args.cell_width, height), Image.Resampling.LANCZOS)))

    cell_height = max(image.height for _, image in opened)
    rows = math.ceil(len(opened) / args.columns)
    width = args.columns * args.cell_width + (args.columns - 1) * args.gap
    height = rows * cell_height + (rows - 1) * args.gap
    canvas = Image.new("RGB", (width, height), (17, 14, 12))
    draw = ImageDraw.Draw(canvas)
    font = load_font(max(18, args.cell_width // 22))

    for index, (label, image) in enumerate(opened):
        column = index % args.columns
        row = index // args.columns
        x = column * (args.cell_width + args.gap)
        y = row * (cell_height + args.gap)
        canvas.paste(image, (x, y))
        bounds = draw.textbbox((0, 0), label, font=font)
        label_width = bounds[2] - bounds[0]
        label_height = bounds[3] - bounds[1]
        padding_x, padding_y = 13, 9
        box = (
            x + 14,
            y + 14,
            x + 14 + label_width + 2 * padding_x,
            y + 14 + label_height + 2 * padding_y,
        )
        draw.rounded_rectangle(box, radius=9, fill=(13, 13, 13, 220), outline=(218, 166, 78))
        draw.text(
            (box[0] + padding_x, box[1] + padding_y - bounds[1]),
            label,
            font=font,
            fill=(255, 246, 227),
        )

    args.output.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(args.output, format="PNG", optimize=True)
    print(f"matrix={args.output} size={canvas.width}x{canvas.height} items={len(opened)}")


if __name__ == "__main__":
    main()
