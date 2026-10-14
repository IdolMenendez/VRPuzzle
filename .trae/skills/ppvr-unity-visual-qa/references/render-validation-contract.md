# PPVR render and validation contract

## Project and editor

- Project: `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR`
- Unity version file: `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/ProjectSettings/ProjectVersion.txt`
- Current editor executable: `/Volumes/APFS/Unity/Hub/Editor/6000.0.77f1/Unity.app/Contents/MacOS/Unity`
- Splash builder: `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Assets/Editor/SplashSceneBuilder.cs`

Discover the editor from the version file instead of assuming the current version forever.

## Established Splash methods

- Build and capture: `SplashSceneBuilder.BuildSplashSceneAndCapture`
- Capture only: `SplashSceneBuilder.CaptureSplashPreview`
- Validate: `SplashSceneBuilder.ValidateSplashScene`

Other scenes should expose equivalent deterministic editor methods.

## Batch rules

- Use `-batchmode`, `-projectPath`, `-executeMethod`, `-quit`, and an absolute `-logFile`.
- Do not use `-nographics` for a GPU screenshot.
- Use `-nographics` for a read-only validator when supported.
- Do not launch a second Unity process while the project is locked.
- Poll running sessions without leaving the user without a progress update.

## Screenshot inspection

Check at original resolution:

- framing and negative space
- reference-image screen percentages
- near/mid/far overlap
- floating or penetrating props
- FBX front/up orientation
- visible primitive placeholders
- overly red grade or clipped highlights
- hard volumetric boundaries
- stretched dust streaks
- hands occluding the hero incorrectly
- text or virtual buttons that contradict the design

## Log acceptance

The render log must contain the final screenshot path. The validation log must contain a positive pass marker and no unhandled exception. Exit code zero alone is insufficient when the expected method did not run.

## Final response

Always paste the full absolute paths for:

1. final scene
2. final screenshot
3. validation log

Never shorten them to project-relative paths or filenames.
