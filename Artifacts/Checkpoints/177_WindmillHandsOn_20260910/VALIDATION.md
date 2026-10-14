# CP177 — Background input playthrough, 2026-09-10

Status: background input playthrough and focused fixes COMPLETE / PASS. User visual acceptance and worn-headset acceptance PENDING; this is not an accepted production-art stage.

User explicitly rejected foreground Unity because it steals focus. The short native-window attempt was abandoned without a completed physical-input playthrough. All subsequent runs used `-batchmode`, with GPU enabled only for renders. Do not reopen foreground Unity for future testing unless the user changes this preference. Deliver screenshots, not a Unity-file handoff or instructions for the user to test.

## Scene and scope

Independent study scene reused without scene/material/mesh edits:
/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Assets/Scenes/WindmillStudy_20260910_V01_LivingWorkshop.unity

Runtime fixes:
- The study now exclusively owns keyboard shortcuts. An opt-out flag on the shared level controller defaults to true for preserved release scenes; only the study disables it. This prevents duplicate H handling and R bypassing restart confirmation.
- Hide old world labels after legacy Awake methods have run, preventing the old instructions from overlapping the study's chapter text.
- Assisted desktop drag maps the pointer to the assembled visual center. Previously one asymmetric sail remained 0.1618 m from the socket with a 180-degree rotation difference, outside the 0.15 m orientation-assist radius; dragging to the visible outline could never connect it. Free manual yaw still uses the current piece bounds.

Test-only virtual devices are injected before the ordinary director Update. Actions use keyboard/mouse states; the runner does not call BeginPointerGrab, MovePointerGrab, EndPointerGrab, AddTurn, ResetStudy or ForceSnap. Normal disk-load restoration internally restores saved snapped poses, as the game itself does. Isolated test save:
/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/177_WindmillHandsOn_20260910/input-progress.json

## Final verification

- /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/177_WindmillHandsOn_20260910/background-start-final.log — exit 0; START INPUT PLAYTEST PASSED. Virtual H, visible ghost, no legacy text, mouse-down selection, E rotation, rejected placement/return, pointer release assembly of parts 0/2/3, automatic mask 13 disk save.
- /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/177_WindmillHandsOn_20260910/background-resume-final.log — separate process, exit 0; RESUME INPUT PLAYTEST PASSED. Three parts restored from disk; pointer assembly of 4/7/6/5/1; all eight assembled without activation; circular mouse motion activates windmill; sails continue rotating; first R preserves completion and second R resets.
- /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/177_WindmillHandsOn_20260910/independent-regression.log — separate no-graphics process, exit 0; INDEPENDENT REGRESSION PASSED. Eleven-scene references/materials/scripts, font coverage, existing release keyboard defaults, isolated study structure.
- /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/177_WindmillHandsOn_20260910/integrity-and-images.json — all 11 PNGs 1920×1080; meaningful pixel variation; CP176 release scene/settings hashes unchanged; APK SHA-256 unchanged (`eee4e98e5b39f7b7906ca83bfe9d01657edba097f8a003228125dadd67c7172a`).

Final front screenshot:
/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/177_WindmillHandsOn_20260910/06_ALIVE_FRONT.png

Left-eye-offset screenshot:
/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/177_WindmillHandsOn_20260910/06_ALIVE_LEFT.png

Intermediate screenshot:
/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/177_WindmillHandsOn_20260910/04_MIDPOINT_SAVED_FRONT.png

Start, hint, held tower, assembled, later rotating pose and restarted state are also captured in this checkpoint. These are actual Play-mode camera renders at reached input-driven states, not concept art or force-completed renders. Camera-only captures omit the desktop IMGUI overlay. Batch input coordinates were 640×480; camera exports were 1920×1080. Left-eye evidence is a 32 mm camera offset, not PICO stereo capture.

Overlay and difference against the CP176 start camera render:
/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/177_WindmillHandsOn_20260910/OVERLAY.png
/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/177_WindmillHandsOn_20260910/DIFFERENCE.png
These document rendered-state differences, not a perceptual quality acceptance score.

## Manual visual review and limits

Original-resolution hint, held-piece, midpoint, final front and final left images inspected. New chapter text no longer overlaps old guidance. Final windmill is supported by its landscape base; hub and sail plane are readable and all eight assembled modules remain present. No missing/pink material visible.

Remaining art issues: the standalone foundation is visibly incomplete-looking; leaving the landscape branch until last makes intermediate structural support look weak; repetitive brown materials and the blank backing plaque reduce the finished presentation. These are not claimed fixed by input testing. The generated CP176 concept remains a target, not the current in-engine look.

Audio evidence: no new audio recording or listening assessment in this checkpoint. Prior audio regression remains at /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/174_ResourceOptimization_20260910/validate-flow.log. It does not prove this study's acoustic quality. No physical mouse completion, worn comfort, controller ergonomics or device FPS claim. No APK rebuild, scene overwrite, commit or push.

## Rejected attempts

- gui-play.log: foreground attempt, explicitly rejected by user; process exited, no completed native playthrough claimed.
- background-start-r01.log and background-start-r02.log: pointer edge was consumed before the normal game Update; rejected. Corrected test-runner timing and background editor input routing.
- background-start-r03.log: partial input playthrough passed; screenshots exposed overlapping legacy guidance. Superseded by final captures.
- background-resume-r01.log: independently reproduced sail 5 drag failure; rejected. Corrected assisted cursor/visual-center mapping, then reran start and resume from scratch.

User acceptance: not requested again and not inferred from permission to continue; pending review of delivered screenshots.
