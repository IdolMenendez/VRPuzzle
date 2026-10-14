# Clean repository APK rebuild — 2026-09-09

Status: COMPLETE — APK BUILD / SIGNATURE / CRC / GPU / INDEPENDENT SCENE
VALIDATION / ORIGINAL-RESOLUTION VISUAL REVIEW PASS.

APK file size: **443,157,258 bytes = 443.157258 MB = 422.627695 MiB**.
Previous size: 443,233,558 bytes. Reduction: 76,300 bytes (0.0172144%).
SHA-256: `9a21da11258a881c4dcb626cfd3e53a29e36cfec450c1eea8d0351693d1b463f`.
Unity's BuildReport totalSize includes build data and is not the final APK
file length. The builder now logs FileInfo.Length to prevent confusion.

- Source: the ten retained V80 runtime scenes in `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Assets/Scenes/RuntimeV80_ZeroLatencyAssembly`.
- Output: `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Builds/PersonallyPuzzledVR-PICO-ZeroLatencyAssemblyV80.apk`.
- Successful build log: `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/172_CleanRepositoryApkRebuild_20260909/build-apk-r02.log` — exit 0, Build Finished Success and PICO BUILD PASSED.
- Previous APK: 443,233,558 bytes; hash recorded in `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/172_CleanRepositoryApkRebuild_20260909/previous-apk.json`.
- Required external SDK, JDK and NDK directories passed shell preflight; API/EditorPrefs configuration is centralized in CurrentAndroidToolchain.Apply.
- Final front screenshot: `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/172_CleanRepositoryApkRebuild_20260909/APK_REBUILD_FINAL.png`.
- Left-eye screenshot: `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/172_CleanRepositoryApkRebuild_20260909/Final/Runtime_Splash_20260807_V80_ZeroLatencyAssembly_LEFT.png`.
- Overlay/difference: `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/172_CleanRepositoryApkRebuild_20260909/Comparison/OVERLAY.png` and `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/172_CleanRepositoryApkRebuild_20260909/Comparison/DIFFERENCE_X4.png`.
- Capture log: `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/172_CleanRepositoryApkRebuild_20260909/capture-final.log` — exit 0; 20 captures, 1920×1080 VR and 1450×1498 cottage.
- Independent post-capture validation: `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/172_CleanRepositoryApkRebuild_20260909/validate-final.log` — exit 0, 11 scenes and zero missing scripts/references/active materials.
- Original-resolution front/left-eye manual review: PASS, no missing content or new rendering corruption. All 20 image comparisons have mean RGB channel difference ≤0.000453/255 against checkpoint 171.
- Audio/runtime behavior: unchanged sources; previous complete regression remains recorded in cleanup checkpoint 171.
- User visual and worn-device acceptance: pending; no new device test is claimed.

APK checks: ZIP CRC PASS, APK Signature Scheme v2 PASS, ARM64 only, package
com.bytedance.personallypuzzled.xr, minimum SDK 29, target SDK 35. Manifest
contains OpenXR permissions, PICO HAND_TRACKING permission, handtracking=1
and Hand_Tracking_HighFrequency=1. Size, signature, badging and manifest
evidence are stored beside this validation record.

The first cold import exposed a PICO SDK initialization issue: GetProjectConfig
recreated its Resources asset with default hand/OpenXR configuration before
imports finished. That attempt was terminated (143), the original settings
were restored, and BuildPico now reapplies and validates the original V80
profile after import and before player serialization. The settings asset is
byte-identical to its previous tracked content. Runtime scenes and models
were not changed; existing unrelated modifications remain intact.

This build regenerates Library after the prior cleanup. Cache files and the
latest Burst debug symbols remain available for incremental builds/debugging.
No headset install, Git commit or push was requested or performed.
