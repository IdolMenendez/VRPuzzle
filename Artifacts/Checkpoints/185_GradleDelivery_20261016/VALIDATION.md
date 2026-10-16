# CP185 — PICO Gradle delivery repair, 2026-10-16

Status: LOCAL EXPORT / STANDALONE GRADLE / APK VALIDATION PASSED. PICO server build, final certificate selection and user acceptance PENDING. No PICO submission performed.

GitHub original main d408a7fc305e2df96fc560462c892d1df70b5e4a had Unity source but no root Gradle project. 3340 tracked Assets/Packages/ProjectSettings files matched the local dev baseline before work. Added shared ExportPicoGradle entry and portable delivery preparation. No gameplay, scene, asset or material changes. Runtime stays IL2CPP / ARM64.

User explicitly selected preserving com.bytedance.personallypuzzled.xr. Final app label 拼接世界, versionName 1.0.0, versionCode 2, min SDK 29, target/compile SDK 35, custom V81 icon retained. Intermediate alternate-package exports are rejected and are NOT delivered. Target API is now explicitly pinned to the same 35 used by the export.

Exact external SDK/JDK17/NDK27.2 policy used in Unity and local Gradle builds. Unity export-final.log: PICO configuration PASS, 11-scene/font validation PASS, Gradle export PASS, exit 0. gradle-final-raw.log: BUILD SUCCESSFUL, 116 tasks. The final export's 2619 IL2CPP/compiler/source files and one static library match the preceding fully compiled export exactly; only the compilation-generated compile-data.json was excluded. Native binary safely reused; matching evidence native-source-equality.json.

Portable project removes workstation Gradle paths, host IL2CPP compiler/source, CMake, cache and symbols; uses precompiled ELF runtime from that same raw APK. Includes official Gradle 8.13 Wrapper and official distribution checksum. No Git LFS in Gradle branch, so standard source checkout has real build inputs. No keystore or password included.

gradle-portable.log: clean standalone :launcher:assembleRelease and :launcher:assembleDebug PASS, 183 tasks, 25 seconds. Both APK CRC checks PASS; 542 assets/native files in each are byte-identical to the raw-export APK. aapt confirms package/version/label/ARM64/29/35; icon resources and hand-tracking/OpenXR permissions retained. Release APK is deliberately unsigned for platform signing. Debug cert SHA256 995ebc4021ef79d3299421be132127bd410e59d0ddb1901520e63285aad0f801 matches previous V82 APK. Platform signing must use a consistent certificate for future updates. No real-device installation or PICO-server build claim.

## Visual and stage evidence

Independent unchanged scene: Assets/Scenes/RuntimeV82_ResourceOptimization/Runtime_Level_Windmill_20260910_V82_ResourceOptimization.unity.
Front: Final/Runtime_Level_Windmill_20260910_V82_ResourceOptimization_FRONT.png.
Left: Final/Runtime_Level_Windmill_20260910_V82_ResourceOptimization_LEFT.png.
New background GPU capture log: capture.log, CAPTURE PASSED, exit 0. Both full-resolution images manually reviewed: original workshop, cutting mat and eight windmill parts intact. Twenty views compared with CP175; maximum mean RGB channel difference 0.006142779 / 255. No art redesign claim. Overlay WINDMILL_OVERLAY.png and difference WINDMILL_DIFF.png retained in the source checkpoint. Audio unchanged; CP174 audio evidence reused, no new acoustic capture. User visual and worn-device acceptance remains pending.

APK sizes/hashes: apk-validation.json. Public build instructions: Docs/PICO_GRADLE_DELIVERY.md. Source checkpoints contain full raw export/build logs; GitHub carries the summary, metadata, hashes and final front/left captures.
