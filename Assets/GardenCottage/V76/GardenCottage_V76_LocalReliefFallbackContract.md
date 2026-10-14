# Garden Cottage V76 — local relief fallback contract

This is a no-network fallback for the two source-shape failures that cannot be repaired by root T×R×S. It is not claimed to be exact hidden-surface reconstruction and it does not impersonate Meshy or Tripo. The two accepted source images remain the complete subject boundaries; their smooth warm backgrounds are excluded by deterministic foreground masks.

## Suitability gate

Scale: `0=unsuitable`, `1=weak`, `2=usable`, `3=strong`.

| criterion | Part_06 | Part_07 | reason |
|---|---:|---:|---|
| isolation | 3 | 3 | one centered semantic object on a smooth background |
| silhouette | 3 | 3 | complete outer contour and internal openings are visible |
| depth inference | 1 | 2 | hidden depth is ambiguous; shallow relief is an explicit fallback assumption |
| primitive decomposition | 2 | 3 | vine is organic; sign/fence/flowerbed has clear systems |
| material procedurality | 2 | 2 | baked clay colour is reusable, but source lighting remains visible |
| occlusion risk | 2 | 2 | thin geometry limits roof/body intrusion; raster ownership gates remain mandatory |
| interaction fit | 3 | 3 | each output remains one independent semantic root with one uniform T×R×S |

Decision: `conditional pass`. Preserve the reference-camera read first; accept only if front and 32 mm left-eye renders pass the existing V76 ownership, contact, roof-negative and footprint gates.

## Style and quality contract

- Style: `soft-clay`; high roughness, rounded baked shading, restrained specular response.
- Visible silhouette: Part_06 must retain the curved top arm and vertical flowering fall; Part_07 must retain one left post, hanging board, four broad blue pickets and the full dense lower flowerbed.
- Macro layout: no root/stump/ground contact on Part_06; no diagonal brace, stone rim, continuous slab or added step on Part_07.
- Identity systems: woody vine + green leaf mass + peach/cream flowers; brown post/beam + cream board; four blue pickets with gaps; dense multicolour garden bed.
- Material palette: embedded source colour texture, opaque alpha-cut front/back, high roughness, no metallic response except baked ring highlights already present in the source.
- Reference camera: frozen Garden Cottage V75 `1450×1498` front camera plus the existing `+0.032 m` left-eye audit.
- Geometry: indexed foreground cells only, front/back surfaces and real boundary sidewalls; no full rectangular collider behind transparent pixels.
- Failure modes: baked-light sticker appearance, coarse silhouette cells, false shadow inclusion, left-eye flatness, alpha/triangle ownership mismatch, wall/roof penetration, or a broad artificial contact slab.

## Pass sequence

1. `blockout`: foreground mask/crop and silhouette coverage.
2. `structure`: indexed front/back/side topology and real lower support samples.
3. `form`: shallow thickness and boundary cleanup.
4. `material`: embedded RGBA source texture with alpha cutoff and high roughness.
5. `lighting`: Unity V76 seamless studio only; no lighting baked by the fallback generator beyond the source pixels.
6. `interaction`: unchanged semantic roots, uniform T×R×S and existing puzzle-family ownership.
7. `optimization`: readable non-degenerate indexed triangles; no unused full-canvas collision geometry.

Every applicable pass requires a three-quarter or final reference-camera capture. A local GLB is only a candidate until Unity cardinal yaw, final-front, left-eye, overlay, difference and validation artifacts close the stage.
