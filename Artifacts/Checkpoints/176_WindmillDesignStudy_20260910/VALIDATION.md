# Windkeeper level-design study — 2026-09-10

Status: design study and isolated desktop prototype COMPLETE. New art/asset-family acceptance, human fun/usability testing, VR integration and worn-device acceptance PENDING. This is not a replacement release or a claim of final-game quality.

User requested a candid score, better level design, concrete work/design drawings and asset research, explicitly deferring publishing discussion. The current design is assessed at 5/10 as a subjective product-design judgment, with the scoring rubric and proposals in /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Docs/WINDKEEPER_LEVEL_DESIGN_STUDY_20260910_V01.md.

## Delivered scope

- Independent playable scene: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Assets/Scenes/WindmillStudy_20260910_V01_LivingWorkshop.unity.
- Runtime: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Assets/Scripts/WindmillStudyDirector.cs. Reuses the eight existing semantic parts. Branching dependency graph, only available pieces presented, explicit chapters, free hints, pointer pickup/drag/release and manual yaw, independent per-piece progress, confirmed restart and manually activated rotor. A half-circle in either direction activates the collected model; small reversing motions do not accumulate a win.
- Versioned material/profile dependencies: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Assets/DesignStudies/WindmillV01. Source materials/profiles are copied before mutation. Rotor axis and hub pose are aligned to the actual sail plane, and the assembly faces the camera. Old source geometry is not replaced or claimed production-ready.
- Builder/Play menu: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Assets/Editor/WindmillStudyBuilder.cs. Build refuses an existing scene path; the build pipeline includes the final staging and mechanical-axis refinement. Play opens this scene independently. No Android build entry point was added.
- New concept board from built-in imagegen: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/176_WindmillDesignStudy_20260910/WINDKEEPER_CONCEPT_V01.png. Original output was copied into the workspace. Actual prompt: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/176_WindmillDesignStudy_20260910/IMAGE_PROMPT.txt. Original-resolution review: useful overall material and three-beat direction; concept only, not an exact eight-part production blueprint, new model, or in-game render. User approval pending.
- Official source/license research for Kenney Nature Kit, Quaternius Medieval Village and Poly Haven Wood Floor. Three actual 1K Poly Haven candidate maps downloaded, 1,685,550 bytes total; source/hash record: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/176_WindmillDesignStudy_20260910/MaterialCandidates/provenance.json. Not imported into Unity, not purchased. No provider model generation or paid asset purchase.

## Saved-scene and final-state images

Final saved start state front: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/176_WindmillDesignStudy_20260910/STUDY_START_FRONT.png.

Final saved start state left eye: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/176_WindmillDesignStudy_20260910/STUDY_START_LEFT.png.

Final earned-state screenshot: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/176_WindmillDesignStudy_20260910/STUDY_ALIVE_FRONT.png.

Final earned-state left eye: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/176_WindmillDesignStudy_20260910/STUDY_ALIVE_LEFT.png.

Assembled-but-not-yet-awakened front/left are also recorded in this directory. All six engine renders are 1920×1080. Capture poses are scripted views of the saved study's start/assembled/earned states. They do not themselves prove pointer or hand input. Capture-only pose changes are discarded after rendering, so opening the scene starts unsolved unless a user's separate study save exists.

Overlay of prior V82 versus the study start: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/176_WindmillDesignStudy_20260910/OVERLAY.png.

Difference image: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/176_WindmillDesignStudy_20260910/DIFFERENCE.png.

These compare intentionally different FOV, lighting and staging, not a similarity target. Image sizes, source/settings integrity and APK hash: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/176_WindmillDesignStudy_20260910/integrity-and-images.json.

## Independent validation

Final GPU capture: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/176_WindmillDesignStudy_20260910/capture-front-final.log. Exit 0 and explicit CAPTURE PASSED. Final original-resolution front/left inspected: full sail silhouette, visible front hub and door, supported base, no cropped chapter text. Legacy high-detail/material and incomplete loose-foundation issues remain visible; the report calls for new production assets rather than treating this study as their acceptance.

Final later independent Play/static validation: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/176_WindmillDesignStudy_20260910/validate-final-r02.log. Exit 0, STATIC VALIDATION PASSED and PLAY VALIDATION PASSED. Validated screen-space pointer adapter selection/drag/release, actual held-piece manual rotation, locked-piece rejection, eight real external-grab/release snap operations, wrong-release return, alternate branch order, partial save reload, completed save reload, manual rotor activation in either direction and clean restart. The pointer adapter functions are the same functions called by Mouse.current; this is automated coordinate/input-path verification, not a physical OS mouse or human playtest. Study input/save testing is isolated to a checkpoint saveOverride file.

Original release scene/font validation: /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/176_WindmillDesignStudy_20260910/release-validation.log. Exit 0, eleven scenes/zero missing scripts/references/active materials and FONT COVERAGE PASSED. Later runtime changes only alter English pointer feedback/control, with no new non-ASCII glyph requirement. Final study compilation and Play validation also passed.

Audio evidence: existing sources and cues are reused; snap/miss/complete cue calls exercised by the play regression, but no acoustic recording or new listening-quality assertion. Prior release audio evidence remains /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/174_ResourceOptimization_20260910/validate-flow.log. No worn-device performance, hand tracking, stereo perception or input claim for the new scene.

## Rejected trials and corrections

1. Initial title was too large and outside frame; staged foundation clipped. Corrected by using the real source sign position, smaller type, wider FOV and bounds-based placement.
2. Initial material/light combination overemphasized metal glare. Reduced copied material smoothness and key intensity; full asset redesign remains pending.
3. A 90-degree orientation trial made sails edge-on. Rejected. Actual sail bounds determined a Z-axis plane; the final assembly faces the viewer with a 180-degree heading, and the hub/socket is centered and oriented consistently with that plane.
4. First refinement assumed the old progressLabel reference existed; it did not. Corrected to find the actual diegetic sign. The saved V82 source was not edited.
5. An editor-folder MonoBehaviour smoke runner did not start after Play reload. Moved to an editor-guarded runtime initializer. The stalled owned process was stopped.
6. Headless movement test originally waited a fixed frame count, which was too little elapsed time; corrected to 1.25 seconds without widening snap thresholds.
7. OS-style queued mouse events were not a reliable batch focus test. Final verification directly exercises the screen-space pointer adapter, then the real external grab/release path; physical desktop interaction is not claimed automated.
8. First capture wrote a newly introduced study-only completed save because capture used AddTurn before SaveProgress guarded against Edit mode. That capture-only study file was copied to evidence and removed. Final code does not save in Edit mode and routine smoke testing no longer clears any user's study save. Release progress was never targeted.
9. Manual Q/E rotation initially competed with held-orientation assistance. A manual rotation action now disables that assistance for the current grab and restores it on release/restart; final regression verifies actual rotation.

Known limitations: existing mesh quality, camera-fixed desktop study, mouse plane/rotation assistance, no integration into release selection/collection, no fresh production semantic asset family, no full keyboard-only or controller accessibility acceptance, no human enjoyment test. These are explicit prototype boundaries.

## Continuity

Baseline V82 scene/metadata and ProjectSettings/EditorBuildSettings hashes match. Existing APK remains 230,554,388 bytes, SHA-256 eee4e98e5b39f7b7906ca83bfe9d01657edba097f8a003228125dadd67c7172a. No new APK, release build-list change, commit, push or store-related work. New independent scene and its copied material/profile support rollback. User acceptance is pending for the concept and study.
