# VRPuzzle — Personally Puzzled XR

Unity 6000.0.77f1 / URP / PICO OpenXR puzzle project. Imported from the delivered `dev` snapshot `436e5c520355fca50967b3861fcb314f640ae5b5` on 2026-10-14. This GitHub repository uses `main` for the imported project. The separate Coastal Atelier redesign is not included in this dev snapshot.

## Open the project

Install Git LFS before cloning, then run `git lfs pull` in the cloned repository to retrieve models, textures, fonts and native libraries. Open the repository root in Unity 6000.0.77f1. The local checkout for this delivery is `/Volumes/APFS/编辑器/VRPuzzle`.

Enabled Build Settings use the ten V82 resource-optimized scenes, starting with Bootstrap. The latest independent Garden Cottage V76 and Windmill design study are also retained. All tracked source, Assets, Packages, ProjectSettings, Tools, Docs and validation artifacts from dev are included. Unity caches, build output, credentials and the old repository history/LFS cache are excluded.

## Validate and build

Editor entry points: `LatestProjectTools.Validate`, `LatestProjectTools.CaptureFinal`, `LatestProjectTools.RunInteractionSmoke` and `LatestProjectTools.BuildPico`. Use background batch mode for unattended work. Screenshot capture requires graphics; Play-mode interaction runs exit themselves and omit `-quit`.

Android toolchain policy is centralized in `/Volumes/APFS/编辑器/VRPuzzle/Assets/Editor/CurrentAndroidToolchain.cs` and requires the configured external SDK `/Users/bytedance/Library/Android/sdk`, JDK `/Library/Java/JavaVirtualMachines/jdk-17.jdk/Contents/Home`, and NDK `/Users/bytedance/Library/Android/sdk/ndk/27.2.12479018`. Unity bundled toolchains are not used. The latest previous APK was V82, 230,554,388 bytes; APK output is not checked into Git.

## Evidence and continuity

Read `/Volumes/APFS/编辑器/VRPuzzle/Docs/CONTINUATION_STATE.md` and `/Volumes/APFS/编辑器/VRPuzzle/Docs/STAGE_VALIDATION_INDEX.md`. Migration evidence is under `/Volumes/APFS/编辑器/VRPuzzle/Artifacts/Checkpoints/183_GitHubDevMigration_20261014`. Historical records retain original source-workspace paths; matching project-relative files are now present in this checkout. Migration does not change prior visual/headset acceptance status.

The destination repository's existing LICENSE is preserved. Third-party package and asset license files remain with their sources.
