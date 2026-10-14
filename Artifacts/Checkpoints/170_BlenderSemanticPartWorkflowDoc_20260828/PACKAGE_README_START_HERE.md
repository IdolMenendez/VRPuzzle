# Blender Semantic Part Workflow Reference Pack V01

Date: `2026-08-28`

## What this package is

This is a self-contained reference bundle for the pipeline:

```text
complete generated model
  -> Blender topology repair
  -> model-specific semantic face classification
  -> generic extraction/capping/export/audit framework
  -> frozen solved T/R/S matrices
  -> exact Unity reassembly
```

The current implementation is **a generic execution framework plus per-model semantic configuration**. It does not hand-select every triangle, but it is also not a zero-configuration universal semantic splitter. Pagoda, Windmill, Ship, and Eiffel reuse the same pipeline skeleton while defining different semantic classifiers.

## Package layout

```text
BlenderSemanticPartWorkflowReferencePack_V01_20260828/
├── README_START_HERE.md
├── Docs/
│   ├── BLENDER_SEMANTIC_PART_DECOMPOSITION_AND_EXACT_REASSEMBLY_WORKFLOW_V01.md
│   └── GARDEN_COTTAGE_NO_BLENDER_IMAGE_TO_PARTS_ASSEMBLY_V01.md
├── Scripts/
│   ├── AI3D/
│   │   ├── repair_v09_pagoda_master.py
│   │   ├── build_v09_pagoda_cut_master.py
│   │   ├── build_v09_windmill_cut_master.py
│   │   ├── audit_v09_fbx_objects.py
│   │   └── audit_v09_topology_defects.py
│   ├── Stage5/build_meshy_ship_semantic10_v03.py
│   └── Stage7/build_meshy_eiffel_semantic16_v04.py
├── Assets/Pagoda/
│   ├── RepairedMaster/SelectionPagodaMiniature_V09_LocalTopologyRepairCandidateG_Embedded.fbx
│   └── CutMaster/SelectionPagoda_CompleteModules9_RepairedCutMasterCandidateL_Embedded_20260721_V09.fbx
├── Audit/Pagoda/CandidateL/
│   ├── PAGODA_V09_ASSET_GATE.md
│   ├── PagodaV09_Assembled.png
│   ├── PagodaV09_Exploded.png
│   └── Part_*.png
└── Preview/BLENDER_SEMANTIC_PART_DECOMPOSITION_AND_EXACT_REASSEMBLY_WORKFLOW_V01_PREVIEW.png
```

The Pagoda pair is the authoritative runnable asset example: one locally repaired complete master and one nine-object semantic cut master. The audit folder contains assembled, exploded, and six-direction evidence for all nine Parts. Windmill, Ship, and Eiffel scripts are included as classifier strategy references and may contain project-specific semantic constants.

## Critical source hashes

| File | SHA-256 |
|---|---|
| Workflow MD | `b9c8b2109c9104d435390cecea4cd996eb948fc191bbbf45bd1e38c42ab3899f` |
| No-Blender comparison MD | `83aebc45bce62a3661a60f230e340af16bcb0476ab691df987ac568eee184428` |
| `repair_v09_pagoda_master.py` | `89db9d1934de0b44ad513a0299c57357bb17f02c8bae9df044f9d5a911574b7f` |
| `build_v09_pagoda_cut_master.py` | `6e42f61599d4ca9508408eb7f9ad86a374c71f83bf3799be6b8d34e741355812` |
| `build_v09_windmill_cut_master.py` | `0e9b54422dfb85383070511b688288d38596a0138984fa073fb8d0a446cd2bbc` |
| `audit_v09_fbx_objects.py` | `86e6bf1d7c3ed7d7b4ac927c207cd0f00aa68043f094b2ac45bd221e9b485780` |
| `audit_v09_topology_defects.py` | `84e6602e3bbdbb7a093df3e7c64a761cf9faf0a48ebd01965775601843b7509d` |
| `build_meshy_ship_semantic10_v03.py` | `062d61042d277fbdbff40a74ad2d54b6f195470b938cb95a314a5555f7631d16` |
| `build_meshy_eiffel_semantic16_v04.py` | `560f22307c4a762392116d901cb666b7f188b7a8688cdebcf4eb9f418cb1eba6` |
| Repaired Pagoda master FBX | `cf0d60abe783fc51253a1c0f36c0f061b4df7dd3a78adcac2e2a824117ec764a` |
| Nine-Part Pagoda cut master FBX | `b8629fcffca72bacc1ef7714bd7c6aa120c351dfb23016a34ab96224d788d0a1` |
| Pagoda asset gate | `1751f03aa61cbbab5d0fcc1ed9cda4446891932cf440bc766c0400bbdc53bf06` |

## Running the packaged Pagoda example

From the extracted package root:

```bash
"/Applications/Blender.app/Contents/MacOS/Blender" \
  --background \
  --python "Scripts/AI3D/build_v09_pagoda_cut_master.py" \
  -- \
  "Assets/Pagoda/RepairedMaster/SelectionPagodaMiniature_V09_LocalTopologyRepairCandidateG_Embedded.fbx" \
  "/absolute/output/path/Pagoda_SemanticParts_Combined.fbx" \
  "/absolute/output/path/Pagoda_Audit"
```

Do not overwrite the bundled source or accepted output. Run into a new output directory and compare the new topology report and six-direction renders with `Audit/Pagoda/CandidateL/`.
