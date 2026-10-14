# Garden Cottage V76 Part_08 four-foot actual-triangle contact audit

Status: `PASS`

- Contract: exactly four substantial lowest-band foot clusters; each foot must hit an actual composite Part_01 triangle with `normal.y >= 0.90`, gap in `[-0.008m, +0.025m]`, and all four support surfaces must remain within `0.030m` height range. Provider versus versioned-extension provenance is disclosed per foot; a separate provider-only overlap gate proves that the extension joins the immutable provider lawn. The isolated Unity raster must remain within 15 px of the reference-envelope centre, within ±15% width/height, and begin at x >= 1105 so contact cannot be purchased by dragging the bench into the door.
- Retained Part_08 source: `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Assets/GardenCottage/V57/Parts3D/GardenCottage_Part_08_CompactPottingBench_V57_Tripo_ReferenceEnvelope_Hero.glb`.
- Retained Part_08 source SHA-256: `ecc980595cb296cd8aef251bcddcb61f911e29c3696cf27b62774d8c851f0225`.
- Frozen V75 baseline matrix: `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Assets/GardenCottage/V75/GardenCottage_V75_SemanticDetailCorrectionMatrices.json`.
- Frozen V75 baseline matrix SHA-256: `8879a7925fd475ebf517bac1a527a5bc24716782dbe472b035e84271d49c408e`.
- Live local T / R / S: `(0.929459200, 0.107153000, -1.178925000)` / `(0.000000000, 0.000000000, 0.000000000, 1.000000000)` / `(0.716548400, 0.716548400, 0.716548400)`.
- Semantic reference hull pixel / solved pixel / error: `(1250.00, 1040.00)` / `(1300.36, 1062.16)` / `55.018px`.
- Reference / Unity isolated visible envelope / centre error: `(x:1105.00, y:815.00, width:280.00, height:250.00)` / `(x:1113.00, y:807.00, width:264.00, height:266.00)` / `0.000px`.
- Versioned visible support extension: `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Assets/GardenCottage/V76/DerivedMeshes/GardenCottage_Part_01_BenchGrassExtension_V76.asset` (a rendered Part_01 child, not an invisible collider or new semantic root).
- Four-foot hits / contacts: `4/4` / `4/4`.
- Provider / versioned-extension support hits: `1` / `3`.
- Gap range / minimum normal.y / support-surface height range: `0.007487..0.009513m` / `0.999539` / `0.002152m`.

| Foot cluster | Actual mesh foot point | Screen pixel | Part_01 triangle point | Triangle normal | Support kind | Collider | Mesh asset | Triangle | Gap (m) | Gate |
|---:|---|---|---|---|---|---|---|---:|---:|---|
| 1 | (0.786238, -0.174006, -1.457348) | (1257.64, 1070.33) | (0.786238, -0.182931, -1.457349) | (0.001755, 0.999934, -0.011400) | extension | Part_01_BenchGrassExtension · V76 actual-triangle support | Assets/GardenCottage/V76/DerivedMeshes/GardenCottage_Part_01_BenchGrassExtension_V76.asset | 3930 | 0.008925 | PASS |
| 2 | (0.803130, -0.174054, -0.952261) | (1137.95, 1011.74) | (0.803130, -0.183567, -0.952262) | (0.006585, 0.999973, -0.003426) | provider | Part_01 · GardenFoundation · V48 ultra-elongated direct GLB · V49 independently generated | Assets/GardenCottage/V48/Parts3D/GardenCottage_Part_01_UltraElongatedGardenFoundation_V48_Tripo_ReferenceEnvelope_Hero.glb | 25884 | 0.009513 | PASS |
| 3 | (1.053216, -0.173928, -0.963494) | (1214.36, 998.05) | (1.053216, -0.181416, -0.963494) | (-0.014330, 0.999539, 0.026760) | extension | Part_01_BenchGrassExtension · V76 actual-triangle support | Assets/GardenCottage/V76/DerivedMeshes/GardenCottage_Part_01_BenchGrassExtension_V76.asset | 13555 | 0.007487 | PASS |
| 4 | (1.052231, -0.174072, -1.470867) | (1342.36, 1054.13) | (1.052231, -0.181563, -1.470867) | (-0.018263, 0.999668, -0.018205) | extension | Part_01_BenchGrassExtension · V76 actual-triangle support | Assets/GardenCottage/V76/DerivedMeshes/GardenCottage_Part_01_BenchGrassExtension_V76.asset | 2909 | 0.007491 | PASS |
