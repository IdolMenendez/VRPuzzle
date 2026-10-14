import argparse
import json
import os

import numpy as np
from PIL import Image, ImageEnhance


def global_ssim(a, b):
    a = a.astype(np.float64) / 255.0
    b = b.astype(np.float64) / 255.0
    mu_a = a.mean()
    mu_b = b.mean()
    var_a = a.var()
    var_b = b.var()
    covariance = ((a - mu_a) * (b - mu_b)).mean()
    c1 = 0.01 ** 2
    c2 = 0.03 ** 2
    return ((2 * mu_a * mu_b + c1) * (2 * covariance + c2)) / (
        (mu_a * mu_a + mu_b * mu_b + c1) * (var_a + var_b + c2)
    )


def luma(rgb):
    return rgb[..., 0] * 0.2126 + rgb[..., 1] * 0.7152 + rgb[..., 2] * 0.0722


def edge_map(gray):
    gy, gx = np.gradient(gray.astype(np.float32))
    return np.sqrt(gx * gx + gy * gy)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("reference")
    parser.add_argument("current")
    parser.add_argument("output_dir")
    args = parser.parse_args()
    os.makedirs(args.output_dir, exist_ok=True)

    current_image = Image.open(args.current).convert("RGB")
    reference_image = Image.open(args.reference).convert("RGB").resize(current_image.size, Image.Resampling.LANCZOS)
    reference = np.asarray(reference_image, dtype=np.float32)
    current = np.asarray(current_image, dtype=np.float32)
    absolute = np.abs(reference - current)
    magnitude = np.sqrt(np.mean(absolute * absolute, axis=2))
    normalized = np.clip(magnitude / 150.0, 0.0, 1.0)
    heat = np.zeros_like(current, dtype=np.uint8)
    heat[..., 0] = np.clip(normalized * 255.0, 0, 255).astype(np.uint8)
    heat[..., 1] = np.clip((normalized - 0.28) * 300.0, 0, 255).astype(np.uint8)
    heat[..., 2] = np.clip((0.22 - normalized) * 210.0, 0, 255).astype(np.uint8)

    overlay = Image.blend(reference_image, current_image, 0.5)
    overlay = ImageEnhance.Contrast(overlay).enhance(1.04)
    overlay.save(os.path.join(args.output_dir, "Front_Reference_Overlay_FINAL.png"))
    Image.fromarray(heat, "RGB").save(os.path.join(args.output_dir, "Front_Reference_Difference_FINAL.png"))

    reference_luma = luma(reference)
    current_luma = luma(current)
    reference_edges = edge_map(reference_luma)
    current_edges = edge_map(current_luma)
    metrics = {
        "reference": os.path.abspath(args.reference),
        "current": os.path.abspath(args.current),
        "resolution": list(current_image.size),
        "mean_absolute_rgb_0_to_255": float(absolute.mean()),
        "root_mean_square_rgb_0_to_255": float(np.sqrt(np.mean((reference - current) ** 2))),
        "global_luma_ssim": float(global_ssim(reference_luma, current_luma)),
        "reference_mean_luma_0_to_255": float(reference_luma.mean()),
        "current_mean_luma_0_to_255": float(current_luma.mean()),
        "mean_absolute_edge_energy": float(np.abs(reference_edges - current_edges).mean()),
        "pixels_with_rgb_error_below_32_ratio": float((magnitude < 32.0).mean()),
    }
    with open(os.path.join(args.output_dir, "Front_Reference_Metrics_FINAL.json"), "w", encoding="utf-8") as handle:
        json.dump(metrics, handle, ensure_ascii=False, indent=2)
    print(json.dumps(metrics, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
