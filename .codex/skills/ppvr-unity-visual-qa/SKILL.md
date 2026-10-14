---
name: ppvr-unity-visual-qa
description: Render, inspect, iterate, and validate visual jobs in the PersonallyPuzzledVR Unity project. Use after any scene, model, material, lighting, camera, interaction-presentation, or art change; whenever the user asks for a screenshot; and before declaring any job in `/Volumes/APFS/编辑器/VRPuzzle` complete.
---

# PPVR Unity Visual QA

Treat the Unity-rendered screenshot as the acceptance artifact. A successful compile or saved scene is not visual completion.

## Prepare

1. Read `references/render-validation-contract.md`.
2. Confirm no other Unity Editor process has the project open before starting batch mode.
3. Identify the exact builder capture method, validation method, scene path, screenshot path, and log path.
4. Keep all paths absolute in commands and user-facing output.

## Render

- Use `scripts/run_unity_job.py` or an equivalent direct Unity batch command.
- Do not pass `-nographics` when producing the final image.
- Capture from the target scene camera at 1920×1080 unless the job specifies another resolution.
- Save screenshots under `/Volumes/APFS/编辑器/VRPuzzle/Artifacts` with the same checkpoint stem as the scene.
- Preserve the builder log for every accepted checkpoint.

## Inspect

1. Open the screenshot with the image viewer at original detail.
2. Compare it to the full reference and important crops when references exist.
3. Check composition, camera yaw, hero scale, support/contact, depth order, asset orientation, PBR readability, lighting direction, haze edges, hand silhouette, and unintended primitives.
4. Fix the largest visible failure and render again. Do not rely on hierarchy or numeric transforms as proof of appearance.

## Validate

- Run a scene-specific validation method in a separate Unity batch process after the final render.
- Use `-nographics` for validation when it does not render.
- Require exit code zero and an explicit validation-passed line in the log.
- Validate screenshot existence, dimensions, and meaningful file size.
- Validate generated asset presence, material maps, support bounds, staging, camera configuration, interactions, and Build Settings when relevant.
- Scan build and validation logs for C# errors, shader errors, exceptions, and failed requirements.

## Complete every job

1. Ensure the final screenshot reflects the final saved scene, not a diagnostic or earlier iteration.
2. Inspect the final screenshot once more after the last change.
3. Return the accepted scene, screenshot, and validation log as complete absolute paths.
4. Always include the final screenshot's complete absolute path, even when the user did not ask again.
5. Use `$ppvr-scene-continuity` to preserve the accepted checkpoint.

## Resources

- `scripts/run_unity_job.py`: discover the project Unity version and run one editor method with an absolute log path.
- `references/render-validation-contract.md`: PPVR editor paths, capture methods, inspection checklist, and completion contract.
