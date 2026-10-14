# Latest-version repository cleanup — 2026-09-08

Status: COMPLETE — COMPILE / REFERENCES / GPU / MANUAL VISUAL REVIEW /
53-PART INTERACTION REGRESSION PASS; CACHE REMOVAL COMPLETE.

Retained: ten V80 runtime scenes, independent Garden Cottage V76, actual
Unity dependencies, current source/evidence and package/project configuration.
Old editor builders were replaced with four current editor tools. Runtime
code and models were preserved. Three scenes received only removal of two
already-missing scripts and clearing one dangling material on a disabled
renderer. The original missing GUIDs have no metadata in the original Git
tree. These corrections remove inert serialization, not working gameplay.

- Manifest: `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/171_RepositoryCleanup_20260908/delete-manifest.json`.
- Independent scenes: `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/171_RepositoryCleanup_20260908/retained-scenes.txt`.
- Baseline front and left-eye: `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/171_RepositoryCleanup_20260908/Before`.
- Final screenshot: `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/171_RepositoryCleanup_20260908/REPOSITORY_CLEANUP_FINAL.png` (1200×1980, viewed at original resolution).
- Final original front and left-eye captures: `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/171_RepositoryCleanup_20260908/Final` (18 VR images at 1920×1080 and two cottage images at 1450×1498).
- Overlay: `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/171_RepositoryCleanup_20260908/Comparison/CLEANUP_OVERLAY_50_50.png`.
- Difference: `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/171_RepositoryCleanup_20260908/Comparison/CLEANUP_DIFFERENCE_X4.png`.
- GPU log: `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/171_RepositoryCleanup_20260908/capture-final.log` — exit 0, CAPTURE PASSED.
- Independent post-render validation: `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/171_RepositoryCleanup_20260908/validate-final.log` — exit 0, VALIDATION PASSED; 11 scenes, zero missing scripts/references/active materials. Disabled historical particle FX intentionally have null materials.
- Complete interaction regression: `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/171_RepositoryCleanup_20260908/interaction-smoke.log` — exit 0, COMPLETE UNITY INTERACTION SMOKE PASSED; Splash targeting, current Selection centre/lock/second-confirm gate, and all 53 parts across Cottage 13, Ship 13, Eiffel 10, Pagoda 9, Windmill 8.
- Audio: current clips/graph retained; original evidence is `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/93_ZeroLatencyAssembly_V80_20260807/V80_AUDIO_EVIDENCE.md`.
- Original-resolution manual review: PASS for final overview, cottage front/left, all five puzzle-level representatives, Splash, Selection, Completion and Collection. No missing geometry, texture loss or new magenta rendering was found. This is a cleanup comparison, not a new art-direction acceptance.
- User visual and worn-device acceptance: PENDING, unchanged.
- Git history and unrelated modified package/settings files: preserved.

## Comparison and source evidence

All 20 before/after pairs were compared. Selection is pixel-identical. The
largest mean absolute RGB channel difference is Splash front at 0.0281 on a
0–255 scale (approximately 0.011%); its left eye is 0.0000141. Other frames
are at or below 0.000212. Scattered edge/particle differences were reviewed as
capture variation; there is no visible loss of scene content.
Metrics: `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/171_RepositoryCleanup_20260908/visual-comparison.json`.

548 measured dependency files remain byte-identical. Exactly three scenes
have the documented inert-reference cleanup; no measured asset is missing.
Evidence: `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/171_RepositoryCleanup_20260908/source-integrity-final.json`.
The latest APK remains 443,233,558 bytes and SHA-256
`a13a1c5c4d99d0011341c407211b3930efd1245100a7d20c0b19ec6640ccfe7a`.
No new APK build or headset test is claimed. The desktop XR-runtime warnings
are expected without a headset; the regression exercises simulated hand input.

## Size and final state

Directory footprint: approximately 106 GiB before and 22 GiB after, including
approximately 20 GiB of preserved Git history. Assets are approximately
1.6 GiB, the retained APK 423 MiB and embedded packages 343 MiB. There are
11 scenes, 100 C# source files, four editor scripts and one APK.

The initial 16,427-file manifest removed 44.33 GiB logical content. Additional
old V15 scenes/metadata and one obsolete music builder were removed. Unity
also purged old import-cache content during refresh. After every check passed
and Unity exited, the remaining Library cache (45,922 files, 16.16 GiB logical)
was deleted. These categories are not a measurement of physical APFS free
space; the before/after directory footprint includes automatic cache purge.
Cache evidence: `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/171_RepositoryCleanup_20260908/cache-removal.json`.

Unity recreated its required CompositionLayers editor preference; its
original GUID was restored. Git diff whitespace checks pass. The existing
viewer service was kept running. Next opening Unity will regenerate Library;
no commit or push was made.
