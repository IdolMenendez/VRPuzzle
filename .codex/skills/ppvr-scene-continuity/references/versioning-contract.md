# PPVR versioning contract

## Stable project locations

- Project root: `/Volumes/APFS/编辑器/VRPuzzle`
- Scenes: `/Volumes/APFS/编辑器/VRPuzzle/Assets/Scenes`
- Visual artifacts: `/Volumes/APFS/编辑器/VRPuzzle/Artifacts`
- Splash builder: `/Volumes/APFS/编辑器/VRPuzzle/Assets/Editor/SplashSceneBuilder.cs`

## Naming

Use `<Scene>_<YYYYMMDD>_V<NN>_<Descriptor>` for the checkpoint stem. Keep `NN` two digits and increase it monotonically for that scene and date. Use a short PascalCase descriptor that states the art or interaction change.

Examples:

- `Splash_20260715_V03_ReferenceWorkshop.unity`
- `Puzzle_20260716_V01_FirstAssembly.unity`

## Isolation

Version anything whose serialized values are changed for the checkpoint:

- Unity scene
- Volume profile
- custom shader or shader variant
- generated mesh
- material whose tuning differs from earlier accepted scenes
- generated source image and model bundle
- screenshot and validation log

Shared packages, fonts, unmodified textures, and stable runtime scripts may remain shared.

## Rollback test

A checkpoint is independently recoverable when opening the old scene does not pick up changed scene-specific lighting, materials, generated meshes, or Volume values from the new job.

## Final handoff

Report these as full absolute paths:

1. accepted `.unity` scene
2. final screenshot
3. validation log
4. important new generated assets

Do not hide an absolute path behind a shortened Markdown label.
