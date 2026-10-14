# Garden Cottage V76 tree/roof and Part_06 actual-triangle clearance audit

## Part_05 behind Part_02 roof ownership

| Reference roof pixel (top-left) | nearest Part_02/Part_05 hit | depth | pass |
|---|---|---:|---|
| (635, 455) | Part_02 HouseRoof | 7.319 | PASS |
| (660, 500) | Part_02 HouseRoof | 7.017 | PASS |
| (690, 545) | Part_02 HouseRoof | 6.725 | PASS |

## Part_06 shallow-relief and roof/body penetration gate

All spans below come only from readable, non-degenerate triangle-referenced provider vertices transformed to world space; no Renderer/AABB extent is used.

- Part_02 facade-plane sampler: `schema=V76_PART02_FACADE_ROI_GRID_V1 | roi=[1070.0000,611.0000..1118.0000,659.0000] | grid=5x5 | rayHits=25 | accepted=23 | rejectedNormals=2 | rows=5 | columns=5 | median=0.7358 | MAD=0.0005 | p90Residual=0.0032 | p90NormalAngleDeg=5.0932`.
- Actual vertex count: `21492`.
- Facade-normal projection min/max/span: `0.7398 / 0.7612 / 0.0214 m`.
- World-up projection min/max/span: `-0.0256 / 1.6079 / 1.6335 m`.
- Thickness / height: `0.0131` (hard range `0.0120..0.0300`; absolute thickness range `0.0180..0.0450 m`).
- Actual Part_02 facade clearance origin / Part_06 back / outward gap: `0.7398 / 0.7398 / 0.0000 m` (hard range `-0.0005..0.0080 m`; the local attachment and in-volume blocker gates below remain mandatory).
- Facade/roof ROI: complete top-left envelope expanded from `(x:1008.00, y:368.00, width:171.00, height:534.00)`; probes are 5 px cells represented by the backmost actual Part_06 vertex with a per-cell actual depth span.
- Per-view hard gates: at least 16 silhouette cells and 24 actual Part_02 attachments spanning at least three of four vertical bands; at least 100 vertical body-carrier cells with 60 actual hits and >=0.15 coverage; body-carrier median gap 0.002..0.015 m and p90 <=0.025 m; full local p95 and closest-triangle p95 <=0.050 m; true Part_02 blocker ratios measured inside the relief thickness + 0.002 m must each be <=0.02 for both the full silhouette and the vertical body carrier. The ray origin/contact tolerance intentionally ignores at most 0.002 m immediately at the back surface. At least 6/7 fixed canonical front-reference rays, reprojected from their current Part_06 world hits for stereo consistency, remain Part_06-owned and all four independent roof-negative probes remain Part_02-owned in each eye. Full self-derived silhouette ownership is diagnostic because the reference vine intentionally passes behind the eave; it cannot force the root to float away from the facade.

| view | vertex cells | local attachments / coverage / bands | min / median / p95 local gap (m) | min / p95 local normal-ray triangle (m) | in-volume blockers / ratio | ownership rays | Part_06 owned | Part_02 owned | fixed canonical front-pixel anchors Part_06-owned | roof guards Part_02-owned | pass |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|
| front | 1420 | 273 / 0.1923 / 3 | 0.0032 / 0.0075 / 0.0316 | 0.0000 / 0.0259 | 10 / 0.0070 | 1350 | 0.8556 | 0.1444 | 6 / 7 | 4 / 4 | PASS |
| left-eye | 1431 | 271 / 0.1894 / 3 | 0.0012 / 0.0076 / 0.0287 | 0.0000 / 0.0281 | 8 / 0.0056 | 1332 | 0.8483 | 0.1517 | 6 / 7 | 4 / 4 | PASS |

### Body-carrier hard geometry and diagnostic ownership (vertical facade band)

| view | body cells | actual Part_02 hits / coverage | median / p90 gap (m) | in-volume blockers / ratio | Part_06 / Part_02 ownership | 
|---|---:|---:|---:|---:|---:|
| front | 1051 | 271 / 0.2578 | 0.0075 / 0.0215 | 10 / 0.0095 | 0.8100 / 0.1900 |
| left-eye | 1066 | 269 / 0.2523 | 0.0076 / 0.0186 | 8 / 0.0075 | 0.8024 / 0.1976 |

Automated result: `PASS`. Original-resolution manual review remains additive and cannot override a failed geometric gate.
