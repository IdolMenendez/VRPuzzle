# Public README and promotional media — 2026-10-14

README now uses repository-relative/public links with no workstation paths. Chinese title follows the user-provided work page: 拼接世界. Docs/Media contains separate generated promotional artwork and actual V82 screenshots, with source mapping and prompt. Full historical V80 video converted to 720p for GitHub Releases; existing V82 APK reused without rebuild. CP184 records documentation/media validation. Game code/scenes unchanged. Poster user review pending; PICO form must remain draft with no submit-review click.

# GitHub dev snapshot migration — 2026-10-14

User requested switching source to dev and copying all code into /Volumes/APFS/编辑器/VRPuzzle, then pushing its existing GitHub repository. This commit imports the complete tracked dev snapshot 436e5c520355fca50967b3861fcb314f640ae5b5 into https://github.com/IdolMenendez/VRPuzzle on main. Source checkout remains dev; codex/scene-redesign stays preserved in the original repository and is not part of this import. Git push completion is verified against remote main.

4061 source files / 3099058070 bytes copied and hash-verified. Runtime/editor code, models, materials, scenes, packages and project settings unchanged; local workflow paths and README adapted to this checkout. Target LICENSE preserved. No old .git history or .git/lfs cache migrated. Git LFS required for clone. Existing tests and screenshots reused for this byte-identical code/asset transfer, no Unity foreground launch. Historical absolute paths below describe the original workspace; resolve equivalent project-relative files under this new checkout for continued work.

Checkpoint: /Volumes/APFS/编辑器/VRPuzzle/Artifacts/Checkpoints/183_GitHubDevMigration_20261014/VALIDATION.md
Final unchanged dev screenshot: /Volumes/APFS/编辑器/VRPuzzle/Artifacts/Checkpoints/178_PreRedesignBaseline_20260910/BASELINE_FRONT.png

# Preserve existing version before actual scene redesign — 2026-09-10

User explicitly said the latest work missed the intended outcome: a better 3D scene, including a visibly changed background, rather than more interaction testing over the same old workshop. Do not present CP176/177 as having achieved the requested art redesign. User requests pushing the existing version first, then doing the new work on a separate branch.

Baseline branch: dev; remote: origin (existing VRModelBuilder repository). This checkpoint preserves the complete current tree: prior authorized cleanup, V82 resource optimization, Garden study, Windkeeper study and verification evidence. V82 remains the enabled release scene set. New visual development belongs on codex/scene-redesign, branched after the baseline push; preserve dev as the pre-redesign reference. Continue background-only Unity rendering and screenshot delivery. No foreground editor or user playtest handoff.

Git-only baseline checkpoint: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/178_PreRedesignBaseline_20260910/VALIDATION.md. Baseline screenshot: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/178_PreRedesignBaseline_20260910/BASELINE_FRONT.png. This is a byte-for-byte copy of the validated V82 screenshot, explicitly showing the old scene; it is not a new scene render or a visual redesign claim.

# Background playthrough and screenshot delivery — 2026-09-10

Persistent user preference: user does not want to operate the game or receive a Unity file as the deliverable. Run and inspect the flow ourselves and deliver screenshots. User explicitly rejected foreground Unity because it steals focus; do not use foreground Unity/CUA for subsequent game testing. Routine continuation actions are authorized. Use background batch Play mode with virtual input and GPU captures, clearly distinguishing this from human or worn-headset experience.

Checkpoint COMPLETE for background input verification and fixes; user/VR acceptance pending: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/177_WindmillHandsOn_20260910/VALIDATION.md. Foreground attempt exited without completing playthrough. First two background trials failed input-edge timing; fixed in the editor-only virtual-input runner. Final start and separate-process resume each exit 0 / INPUT PLAYTEST PASSED. Normal Update consumed all virtual mouse/keyboard input: eight real drag/releases, E yaw, wrong placement return, H hint, fresh-process partial save reload, circular sail activation, continuous turning, double-confirm restart. Independent eleven-scene/font/study/default-keyboard regression exit 0. Runtime fixes: study owns shortcuts to avoid duplicate H and unconfirmed R; hide legacy labels after Awake; map assisted drag to solved visual center to prevent an asymmetric sail getting stuck outside orientation assist. No scene/model/material edits; V82 scene/settings hashes and APK unchanged.

Final screenshot: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/177_WindmillHandsOn_20260910/06_ALIVE_FRONT.png. Final left and midpoint also inspected at original resolution. Camera-only Play-mode captures omit desktop IMGUI; 1920×1080 images, input coordinate surface 640×480. No OS-input completion, new audio capture or worn-device claim. Existing art still has incomplete-looking foundation/weak intermediate support, brown repetition and blank final plaque; do not describe it as the generated concept implemented. Background entry points: WindmillBackgroundPlaytest.Run, Resume, ValidateRegression; require -batchmode, no -quit for Run/Resume because runners exit themselves, -nographics only for independent static regression.

# Windkeeper level-design study — 2026-09-10

Status: concrete design study and independent desktop prototype COMPLETE; human fun/usability, production asset and worn-device acceptance PENDING. Latest user explicitly deferred publishing discussion and asked for a candid design score, better levels, concrete implementation/concepts and asset research. Current design assessed subjectively at 5/10; focus now is gameplay and assets, not store work.

Report: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Docs/WINDKEEPER_LEVEL_DESIGN_STUDY_20260910_V01.md.
Independent scene: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Assets/Scenes/WindmillStudy_20260910_V01_LivingWorkshop.unity. Unity menu Personally Puzzled XR > Design Study > Play Windmill Study. Desktop mouse drag/release, Q/E yaw, H free hint, R double-confirm restart. Assemble eight existing semantic parts, then manually turn sails a half-circle to activate the collectible. Branches, staged parts, independent mid-level save/restore, same-parts completion and corrected real sail-plane rotor/hub added. Mouse assistance is a study control scheme, not a claim of VR hand readiness. Not integrated into current APK/build selection. Original V82 scene/build settings hashes and APK hash match.

Concept image: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/176_WindmillDesignStudy_20260910/WINDKEEPER_CONCEPT_V01.png. Built-in imagegen, copied into workspace; prompt stored alongside. Design target only; no new 3D hero generated. User concept/asset acceptance pending. Kenney Nature Kit and Quaternius Medieval Village researched; three Poly Haven Wood Floor 1K maps downloaded as candidates with CC0/source/hash record, not imported.

Checkpoint: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/176_WindmillDesignStudy_20260910/VALIDATION.md.
Final screenshot: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/176_WindmillDesignStudy_20260910/STUDY_ALIVE_FRONT.png. Final left-eye and saved-start captures also retained and inspected at original resolution. Final GPU capture exit 0; independent static + pointer adapter + manual yaw + eight-piece real grab/release + invalid return + branch/save/activation/reset regression exit 0 in validate-final-r02.log. Original eleven-scene/font validation also passes. No new acoustic recording, worn test, physical OS mouse playtest, new APK or commit/push.

Important prototype limitations: old models still need complete-module visual quality work; concept look is NOT in-engine. First capture-only study save was cleaned up and Edit-mode saving guarded; smoke uses its own saveOverride and does not erase user saves. Old V82 release progress untouched. Failed title/orientation/test-start trials and their fixes documented in checkpoint. Reuse current builder/runtime rather than recreating from old scene-version builders.

# PICO product review and rebuild proposal — 2026-09-10

Status: review COMPLETE; proposed redesign NOT IMPLEMENTED or user-approved. User asked how to improve/rebuild the game including level selection and monetization for PICO publishing. Report: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Docs/PICO_PRODUCT_REVIEW_AND_REBUILD_PLAN_20260910_V01.md.

Current APK/scenes remain V82 as recorded below. No new APK, gameplay change, platform payment integration or store submission. Region clarification unanswered; proposal assumes mainland-first and distinguishes overseas requirements. Prioritize one complete polished sample (tutorial → explicit selection → differentiated assembly → living-model reward → accessible collection → resume saved assembly) before expanding content or paid DLC.

Confirmed product gaps: inaccessible ordinary-player Collection route; progress schema lacks per-piece mid-level save; current Bootstrap unlockAllLevelsV57=1; hint/time star penalties conflict with a relaxed positioning; Eiffel scene tolerances 14.5–18 cm / 135–180° warrant worn usability review; display/puzzle model inconsistency and high-poly native assets remain. Existing input/flow automated PASS does not prove fun, accessibility or store readiness.

New twenty-view GPU capture and independent eleven-scene references/font validation exit 0, explicit PASS. Original-resolution Cottage front and Selection front/left inspected. Review checkpoint: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/175_PicoProductReview_20260910/VALIDATION.md. Final current-state screenshot: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/175_PicoProductReview_20260910/Final/Runtime_Selection_20260910_V82_ResourceOptimization_FRONT.png. No new audio, 53-part input, real-device or payment test claimed; prior CP174 evidence retained. User product/visual acceptance pending.

# Resource optimization V82 — 2026-09-10

Status: COMPLETE for this job; production/user-worn acceptance PENDING.
Current APK: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Builds/PersonallyPuzzledVR-PICO-OptimizedV82.apk.
230,554,388 bytes = 230.554388 MB = 219.873798 MiB. Down 49,561,213 bytes / 17.6931% vs V81; down 47.9746% vs original. SHA-256 eee4e98e5b39f7b7906ca83bfe9d01657edba097f8a003228125dadd67c7172a.
Old V81 APK removed after validation to retain latest APK only. Historical APK paths below are superseded.

Current scenes: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Assets/Scenes/RuntimeV82_ResourceOptimization (ten enabled V82 scenes; Bootstrap first). Independent Garden V76 remains unchanged. Old V80 scenes and source assets remain intact for rollback.
Checkpoint: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/174_ResourceOptimization_20260910/VALIDATION.md.
Final screenshot: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/174_ResourceOptimization_20260910/RESOURCE_OPTIMIZATION_FINAL.png.
Final build: build-final.log; independent scene/resource/full-flow/audio/save: validate-flow.log; 53-part input: interaction.log; GPU: capture-final.log. All exit 0 / explicit PASS. APK signature/CRC/ARM64/manifest checks pass. No device/worn test claimed.

Analysis/checklist preceded changes. 25 referenced model assets copied byte-identically to Assets/Optimization/V82; 23 use Medium, 2 workbenches retain original Low after Medium caused visible UV/grid waviness. Topology/vertex/submesh identities of all 93 meshes preserved. Mesh build report 170.3→123.5 MiB. Lossy quantization affects positions/UVs/normals; final max image mean RGB difference 0.440475/255 vs V81, original-resolution key views reviewed. Trial rejection and per-mesh differences documented. Three copied Biplane color/normal/emission textures now Android ASTC 6x6 / 1K; total textures 95.5→89.3 MiB.

Font subset 16,437,364→182,176 bytes / 544 codepoints, zero missing scanned supported characters. Tools/build_font_subset_v82.py scans configured scenes and runtime scripts. FontTools 4.59.1 used from temporary isolated environment. Build-time Unity glyph coverage guard fails when new scene/script text exceeds subset; regenerate before shipping new text. Full source font preserved for regeneration. Original full font and models absent from V82 scene dependency graph. New rollback-safe copies add ~558 MiB repository disk use; no repository-size reduction claimed. 548 baseline asset hashes unchanged.

Deferred: old Eiffel is active in Completion/Collection, not junk; semantic display unification requires separate acceptance. PICO audio libraries retained (native declarations + no device validation); Read/Write/topology/managed/shader stripping unchanged. Existing Collection player-entry gap, debug signing, airplane preview and Garden nonintegration remain. User visual/worn acceptance pending. No Git commit/push.

Editor tools: ResourceOptimizationV82.Audit / Prepare (rejects overwriting existing V82 scene checkpoint) / RefineAndCapture / ValidateAndRunFlow. LatestProjectTools.Validate supports ten consistent V80 or V82 scenes and checks V82 font coverage. BuildPico defaults to OptimizedV82.apk, accepts PPVR_APK_PATH and PPVR_EVIDENCE_DIR, exact external toolchain remains centralized. Restore checkpoint Backup/EditorBuildSettings.asset for rollback scenes. Icon and disabled Unity startup branding from V81 preserved.

# Android optimization V81 — 2026-09-10

Status: COMPLETE for optimization/build/editor QA; overall production/user-worn acceptance PENDING.
Latest APK: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Builds/PersonallyPuzzledVR-PICO-OptimizedV81.apk.
280,115,601 bytes = 280.115601 MB = 267.139054 MiB, down 163,041,657 bytes (36.7909%) from 443,157,258. SHA-256 346d6e023f6324d8af4d790ba3abcc9d5d2ea14be2d45a80b974fcf76e37c496.
Superseded V80 APK removed after verification per user only-latest request. Historical APK paths below are superseded records.

Checkpoint: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/173_AndroidOptimization_20260910/VALIDATION.md.
Final screenshot: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/173_AndroidOptimization_20260910/ANDROID_OPTIMIZATION_FINAL.png.
Final build: capture-build-r03.log; independent validation + five-level progression/archive/audio/save reload: validate-flow-final.log; 53-part input regression: interaction.log. All exit 0 / explicit PASS. APK signature v2/CRC/ARM64/manifest/icon checks pass. No connected ADB device; no install or worn test claimed.

106 Android-only material-map overrides: ASTC 6x6 colors/normals/emission, ASTC 8x8 masks. Ordinary maps 1K; title, hand and workbench colors 2K. Round 2 276.8 MB rejected for soft workbench grid; final restores its clarity for 3.36 MB. User assets textures 440.2→95.5 MiB; meshes unchanged 170.3 MiB. 548 original asset hashes and metadata outside Android remain identical. Restore importer metadata/PlayerSettings from checkpoint Backup to roll back. Desktop defaults, scene geometry and source images unchanged.

New icon: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Assets/Branding/V81/PersonallyPuzzledIcon.png; all 18 Android slots filled. Unity default splash/logo disabled, game Splash intact. Original-resolution front/left and key scene review pass; 20 comparisons maximum mean RGB 1.934/255. Noncritical texture detail reduced; no lossless claim.

Remaining release gaps: player Collection entry disabled (Completion active button returns Selection; programmatic archive test does not prove reachability); no PICO sustained-performance/thermal/cold-start/resume/user-worn test; APK uses Android Debug certificate and versionCode 1 / versionName 1.0. Airplane is preview only, Garden V76 remains independent and unintegrated. Detailed findings in checkpoint VALIDATION. User visual/production acceptance pending. No Git commit/push.

Editor tools: AndroidSizeOptimization.Apply / CaptureAndBuild / ValidateAndRunFlow. LatestProjectTools.BuildPico accepts PPVR_APK_PATH, and defaults to the latest OptimizedV81 APK name. PPVR_EVIDENCE_DIR sets new evidence. Toolchain remains centralized CurrentAndroidToolchain exact external SDK/JDK/NDK. Editor-only current flow smoke complements hand-input regression; its final version restores original in-memory save before quit-time save.


# PersonallyPuzzledVR — current continuation state

## APK rebuild — 2026-09-09

User requested a fresh APK from the cleaned repository and its measured size.
Status: COMPLETE — APK BUILD / SIGNATURE / ZIP CRC / GPU / INDEPENDENT
11-SCENE VALIDATION PASS. Final front/left-eye screenshots were inspected at
original resolution. Twenty captures match the cleanup baseline with maximum
mean RGB channel difference 0.000453 on the 0–255 scale. Unity has regenerated
Library for this build; caches are retained for subsequent incremental builds.
Final screenshot: `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/172_CleanRepositoryApkRebuild_20260909/APK_REBUILD_FINAL.png`.
Fresh APK: 443,157,258 bytes = 443.157258 MB = 422.627695 MiB, down 76,300
bytes (0.0172144%) from the previous APK. SHA-256:
`9a21da11258a881c4dcb626cfd3e53a29e36cfec450c1eea8d0351693d1b463f`.
ARM64, minimum SDK 29, target SDK 35, APK v2 signature verified; manifest
retains PICO hand tracking and OpenXR permissions. No device install requested.

First cold import caused the PICO SDK's
GetProjectConfig to recreate its Resources asset with default hand tracking
disabled. That attempt was stopped before APK completion. The original
eight settings were restored and the current builder now reapplies and
verifies the V80 hand/OpenXR profile after import, before player serialization.
Unity starts from the deleted Library cache and uses
the exact required external Android SDK/JDK/NDK and environment variables.
The shared toolchain policy applies before BuildPipeline.BuildPlayer.
The existing APK measured 443,233,558 bytes before this run.
Checkpoint: `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/172_CleanRepositoryApkRebuild_20260909`.
Successful log: `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/172_CleanRepositoryApkRebuild_20260909/build-apk-r02.log`.
Current builder accepts PPVR_EVIDENCE_DIR to keep new evidence separate from
the completed cleanup record. No runtime scenes or assets were edited.

Updated 2026-09-08 for the user's instruction: “精简代码仓库，只保留最后版本，删除一切无用内容。”

## Scope and current cleanup status

Retain the complete VR game V80 and independent Garden Cottage V76. There is
no newer complete VR release. This explicit deletion request overrides the
usual preserve-every-checkpoint policy for this cleanup. Older names on
retained dependencies do not mean they are unused releases.

16,427 enumerated obsolete files were deleted (47,596,745,494 logical bytes):
old scenes/builders, unused assets, old APKs, duplicate exports, intermediate
evidence and scratch output. The retained set follows Unity dependencies,
C# type references, Resources/configuration roots and current source inputs.

Status: CLEANUP COMPLETE — COMPILE / 11-SCENE REFERENCES / 20 GPU CAPTURES /
53-PART INTERACTION REGRESSION / MANUAL VISUAL REVIEW PASSED. The directory
footprint fell from approximately 106 GiB to 22 GiB, including approximately
20 GiB of preserved Git history. Unity purged obsolete imported assets during
refresh; the remaining 16.16 GiB logical Library cache (45,922 files) was
removed after Unity exited. The next Unity open will regenerate its cache.

An additional 20 old V15 scenes and their metadata were removed after the
initial manifest, along with one obsolete music builder. Two missing script
components and one dangling material reference on a disabled renderer were
already absent from the original asset/Git metadata; those inert references
were removed from three V80 scenes. All other 548 measured scene/asset
dependency files remain byte-identical, including every model and texture.

Git history, embedded PICO package, package lock, active viewer service and
unrelated package/settings changes remain intact. Unity's regenerated
CompositionLayers editor preference received its original meta GUID back.
No commit, push, history rewrite, APK rebuild or device install was performed.
The existing latest APK retains SHA-256
`a13a1c5c4d99d0011341c407211b3930efd1245100a7d20c0b19ec6640ccfe7a`.

Current checkpoint:
`/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/171_RepositoryCleanup_20260908`.
This contains the deletion manifest, original dependency hashes, prior Git
status and 20 front/left-eye baseline renders. It is the authority for the
current working tree; historical backup/builder references in retained
delivery records describe their original deliveries and are superseded.

Final screenshot:
`/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/171_RepositoryCleanup_20260908/REPOSITORY_CLEANUP_FINAL.png`.
Final validator:
`/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/171_RepositoryCleanup_20260908/validate-final.log`.
Interaction regression:
`/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/171_RepositoryCleanup_20260908/interaction-smoke.log`.

## VR V80

Scenes:
`/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Assets/Scenes/RuntimeV80_ZeroLatencyAssembly`.
Build Settings contain ten V80 scenes: Bootstrap, Splash, Selection, five
puzzle levels, Completion and Collection. PlayerManual is the release
default. The puzzles contain 13/13/10/9/8 semantic parts (53 total).

V80 retains the second-pinch Selection confirmation, physical Home/Reset/Hint
console, movable/scalable Completion displays, and suppressed in-level
completion UI. Eiffel Part 02 uses a 0.180 m / 180-degree capture profile.
Blueprints show one module on demand. Steady lights are 4/4/5/4/4 with no
realtime shadows. The V80 URP asset uses 2x MSAA and four additional lights.
PICO uses low fixed foveation.

Original validation:
`/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/93_ZeroLatencyAssembly_V80_20260807/VALIDATION.md`.
Latest APK:
`/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Builds/PersonallyPuzzledVR-PICO-ZeroLatencyAssemblyV80.apk`.
Original device install/hash and complete regression passed; user-worn
responsiveness is PENDING. Editor timing is not headset acceptance.

## Garden Cottage V76

Scene:
`/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Assets/Scenes/GardenCottage_20260824_V02_ProviderSemanticReplacement.unity`.
Validation:
`/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/169_GardenCottage_V76_ProviderSemanticReplacement_20260824/VALIDATION.md`.

The final study uses deterministic LocalRelief GLBs for Part_06/Part_07,
rendered Part_01 bench support, Part_08 four-foot support, and the reviewed
Part_10 text decal. Final front/left-eye, overlay/difference, source/matrix
hashes and manual-review evidence are retained. Original-resolution manual
review passed; user acceptance is PENDING. This study does not replace V80.

The relief generator depends on its v04/v03/v02/base Python module chain;
those are dependencies of one current generator. Current viewer source:
`/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Tools/AI3D/GardenCottageLocalReliefViewer`.
Its existing active service is left running.

## Tooling and persistent contracts

Current menu/batch entry points are in
`/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Assets/Editor/LatestProjectTools.cs`.
The Play Mode window resolves V80. Historical generation builders are removed;
use the saved final scenes as the authoritative source for future work.

Android policy is centralized in
`/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Assets/Editor/CurrentAndroidToolchain.cs`:
SDK `/Users/bytedance/Library/Android/sdk`,
JDK `/Library/Java/JavaVirtualMachines/jdk-17.jdk/Contents/Home`,
NDK `/Users/bytedance/Library/Android/sdk/ndk/27.2.12479018`.
It fails on absent directories, disables embedded toolchains, and applies API
properties, EditorPrefs and environment variables. The Android manifest and
Gradle postprocessor remains intact.

Continue honoring semantic puzzle decomposition, no provider keys in output,
original-resolution screenshot inspection, full absolute paths, and pending
user acceptance. Read the decomposition standard before any new exhibit
generation/import/assembly. Future visual changes still use independent
versions unless the user requests otherwise.
