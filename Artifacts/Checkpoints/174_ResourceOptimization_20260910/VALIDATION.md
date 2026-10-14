# V82 resource optimization — validation

Status: COMPLETE for this optimization job. User visual/worn-device/production acceptance remains PENDING.

## Result

Final APK: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Builds/PersonallyPuzzledVR-PICO-OptimizedV82.apk

| Measure | Before V81 | Final V82 |
| --- | ---: | ---: |
| Actual APK bytes | 280115601 | 230554388 |
| Actual APK decimal MB | 280.115601 | 230.554388 |
| Meshes in uncompressed build report | 170.3 MiB | 123.5 MiB |
| Textures in uncompressed build report | 95.5 MiB | 89.3 MiB |
| Original font source / subset file | 16437364 bytes | 182176 bytes |

Saved 49,561,213 APK bytes (17.6931%) this round; down 47.9746% from the original 443,157,258-byte APK. Per-resource uncompressed figures are not additive estimates of APK savings. SHA-256: `eee4e98e5b39f7b7906ca83bfe9d01657edba097f8a003228125dadd67c7172a`.

## Analysis and execution checklist

The user requested analysis, a list, then implementation. The list was presented before mutations and recorded in /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/174_ResourceOptimization_20260910/OPTIMIZATION_CHECKLIST.md.

Audit found 25 model assets and 5,970 serialized scene references. The old 16-part Eiffel has 1,749,210 vertices / 2,566,698 triangles and 32 MeshFilter references in active Completion/Collection objects. It is not unused garbage. The Ship has 1,492,093 vertices / 2,051,530 triangles and is used across gameplay and displays. Evidence: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/174_ResourceOptimization_20260910/models.tsv and /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/174_ResourceOptimization_20260910/scene-references.tsv.

Executed:

1. Independent V82 scenes and model copies. 23 models / 91 meshes use Medium compression; two workbench models / two meshes preserve original Low. No decimation, part merging, collider change, Read/Write change, or interaction threshold change. All 25 copied FBX source bytes equal their originals; 548 original baseline asset hashes remain unchanged. Source integrity: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/174_ResourceOptimization_20260910/source-integrity.json.
2. Font subset retains 544 codepoints covering supported characters in the scanned scene/runtime-code corpus, plus Latin and punctuation. Missing required codepoints: zero. The font is 182,176 bytes. Source font and naming/license metadata remain preserved. Build validation also checks scene/script glyph coverage with Unity Font.HasCharacter and fails if new supported text is missing from the subset. Regenerate with /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Tools/build_font_subset_v82.py when adding text; this run used FontTools 4.59.1 in a temporary isolated Python environment. Coverage manifest: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/174_ResourceOptimization_20260910/font-coverage.json. External/user-generated arbitrary Chinese text is not claimed covered.
3. Three Biplane textures are color (_BaseMap/_MainTex), normal (_BumpMap) and emission (_EmissionMap). Copied to V82 and set Android ASTC 6x6, maximum 1024; original import defaults and images untouched. New external material remaps the embedded material to these exact copies. Bindings: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/174_ResourceOptimization_20260910/biplane-texture-bindings.tsv.

Deferred with evidence: replacing active old Eiffel/showcase models with a unified semantic assembly changes presentation and requires its own acceptance; PICO audio plugin libraries still have native entry-point declarations and cannot be removed solely because scene AudioSources have spatialize=false; managed/shader stripping, Read/Write removal and topology simplification remain unchanged pending dedicated runtime verification. No speculative savings are claimed for these items.

## Rejected trial and corrections

Initial Medium-all trial APK: 229,957,392 bytes. Numerical position comparison passed, but original-resolution inspection found warped regular grid lines on workbenches due to quantized mesh/UV data. Trial rejected for those two assets. Restoring their original Low costs 596,996 APK bytes; final retained as 230,554,388 bytes. Evidence: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/174_ResourceOptimization_20260910/TrialMedium/REJECTED_WORKBENCH_UV.png. Final image mean differences fell from a maximum 2.8719/255 to 0.440475/255 across twenty views.

First prepare validation found a missing Biplane material because a reference from an imported FBX material (type 3) had been changed to a native .mat without changing its reference type (2). The V82-only scene override and builder replacement logic were corrected before capture/build. Final validation contains no missing material/reference.

## Scene, visual and geometry evidence

- Independent runtime scenes: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Assets/Scenes/RuntimeV82_ResourceOptimization.
- Main scene: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Assets/Scenes/RuntimeV82_ResourceOptimization/Runtime_Splash_20260910_V82_ResourceOptimization.unity.
- Final front screenshot: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/174_ResourceOptimization_20260910/RESOURCE_OPTIMIZATION_FINAL.png.
- Final left-eye screenshot: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/174_ResourceOptimization_20260910/Final/Runtime_Splash_20260910_V82_ResourceOptimization_LEFT.png.
- Overlay: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/174_ResourceOptimization_20260910/Comparison/OVERLAY.png.
- Difference x4: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/174_ResourceOptimization_20260910/Comparison/DIFFERENCE_X4.png.
- All front views: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/174_ResourceOptimization_20260910/Comparison/ALL_FRONTS.png.
- Capture log: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/174_ResourceOptimization_20260910/capture-final.log, exit 0 and CAPTURE PASSED; twenty GPU renders, runtime 1920×1080 and independent Garden 1450×1498.
- Numeric comparisons: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/174_ResourceOptimization_20260910/render-comparison.json, max mean RGB 0.440475/255 vs V81. Geometry/rasterization changes are lossy; this number is supporting evidence, not proof of identical images.
- Manual original-resolution review: final Splash front/left, Eiffel left, Cottage front, Selection front; Ship left inspected during trial, and final overall contact sheet checked. Text remains legible and workbench grid recovered. No new missing geometry, missing materials or gross silhouette failure observed. Headset perception remains untested.
- 93 meshes retain vertex count, submesh count and triangle index arrays. Largest initial positional error relative to a mesh bounds diagonal: 0.008872%. Final per-mesh position/UV/normal differences are in /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/174_ResourceOptimization_20260910/final-mesh-integrity.tsv. Quantization also changes normals (largest recorded angle 6.6462 degrees) and UVs; it is not lossless. The two workbench exceptions have original geometry compression.

## Independent validation, interaction, audio and APK

- /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/174_ResourceOptimization_20260910/validate-flow.log: separate Unity process after final capture, exit 0; eleven scenes (ten V82 plus independent Garden) / zero missing scripts, references or active materials. RESOURCE VALIDATION PASSED confirms original 25 model assets and full font absent from V82 scene dependency graph.
- Same log: five playable levels and 53 parts complete, Completion/Collection API routes, return to Selection, music playing and non-repeating puzzle pool, disk save write/reload PASS. Archive API tests do not prove an accessible player entry.
- /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/174_ResourceOptimization_20260910/interaction.log: exit 0, actual shared-provider input path, title boundaries, V78 two-pinch selection, all 53 grab/move/bad-release-return/valid-release-snap operations, physical Completion return PASS.
- Audio evidence is playback/volume/pool assertions in full-flow logs; audio source files and configuration were not altered. No acoustic recording or headset listening test is claimed.
- /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/174_ResourceOptimization_20260910/build-final.log: exit 0, FONT COVERAGE PASSED, eleven-scene validation PASS, Build Finished Success and PICO BUILD PASSED 230554388 bytes. Exact required external SDK/JDK/NDK preflight, environment, API and EditorPrefs configuration used via CurrentAndroidToolchain.Apply before BuildPlayer.
- /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/174_ResourceOptimization_20260910/apk-signature.txt: v2 signature PASS, Android Debug certificate preserved.
- /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/174_ResourceOptimization_20260910/apk-badging.txt and /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/174_ResourceOptimization_20260910/apk-manifest.txt: ARM64, min SDK 29, target SDK 35, OpenXR and PICO high-frequency hand tracking retained.
- /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/174_ResourceOptimization_20260910/apk-size-and-hash.json: actual file measurement, SHA-256, ZIP CRC PASS.

No PICO device was connected in the current session baseline; no install, real-device cold start, resume, sustained FPS/thermal or worn acceptance is claimed. Existing release gaps remain: Collection player entry disabled, debug signing/versionCode 1, airplane preview only, Garden V76 independent. These are not introduced or fixed by this compression pass.

## Continuity and rollback

Original V80 scenes and source model/import metadata are intact. V82 changes only its copied model import settings, copied plane textures/material, subset font and independently copied scenes with updated bootstrap names/GUIDs. New source copies consume approximately 558 MiB on disk to preserve rollback; they do not cause both old and new models to be included in the APK. This job shrinks delivery size, not repository size. Old V81 APK was removed after V82 verification to retain only the latest deliverable APK.

Rollback build settings are in /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/174_ResourceOptimization_20260910/Backup/EditorBuildSettings.asset. Restore that file to build the preserved V80/V81 scene set, and set PPVR_APK_PATH explicitly for rollback output. LatestProjectTools.BuildPico now defaults to V82. No Git commit, push, provider requests or model-generation charges.
