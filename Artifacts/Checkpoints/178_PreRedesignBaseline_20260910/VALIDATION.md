# CP178 — Pre-redesign Git baseline, 2026-09-10

Scope: preserve and push current complete version on dev before creating codex/scene-redesign. No scene, camera, light, shader, material, mesh, APK or gameplay edit in this job. The current art direction has not met the user's request for a visibly better 3D environment; new work must actually change that environment on the separate branch.

Baseline independent scene:
/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Assets/Scenes/RuntimeV82_ResourceOptimization/Runtime_Level_Windmill_20260910_V82_ResourceOptimization.unity

Final baseline front screenshot:
/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/178_PreRedesignBaseline_20260910/BASELINE_FRONT.png

Baseline left screenshot:
/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/178_PreRedesignBaseline_20260910/BASELINE_LEFT.png

Images are unmodified copies of CP175 V82 camera captures, retained as the pre-redesign reference. No new render is needed for Git-only changes. Original-resolution front review: old dark timber workshop, green cutting mat and supplied windmill modules; explicitly not redesigned.

Validation log reused from the unchanged code:
/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/177_WindmillHandsOn_20260910/independent-regression.log
This already passes eleven-scene references, font coverage, release keyboard defaults and isolated study structure. No extra Unity process is launched for the Git operation.

Overlay/difference: not applicable to Git-only changes. Latest visual comparison is recorded in /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/177_WindmillHandsOn_20260910/VALIDATION.md.

Audio: unchanged, no new recording; previous audio evidence is documented in CP174. Manual visual review: baseline reference inspected. User acceptance: user authorized preserving/pushing this version, but rejected the claim that the existing study sufficiently redesigns the 3D scene. Do not mark the new visual design accepted.

Git safeguards: fetched origin/dev before staging; it matched local HEAD 7c75cbcf. Existing 2026-08-10 zero-byte index.lock had no owning process and was moved to /private/tmp/ppvr-stale-index-lock-20260910. Current changes staged without resetting or rewriting history. Existing Git LFS rules cover binary assets; generated APKs, Unity caches and local credentials remain ignored by the existing repository rules.
