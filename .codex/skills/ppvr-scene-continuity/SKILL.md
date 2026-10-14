---
name: ppvr-scene-continuity
description: Preserve rollback-safe continuity for the PersonallyPuzzledVR Unity project. Use whenever creating, rebuilding, or materially changing a Unity scene, scene builder, volume profile, scene-specific shader/material, build-setting entry, or visual checkpoint in `/Volumes/APFS/编辑器/VRPuzzle`.
---

# PPVR Scene Continuity

Create every accepted job as a new, independently recoverable scene checkpoint. Never overwrite the last accepted scene.

## Start a job

1. Treat `/Volumes/APFS/编辑器/VRPuzzle` as `PROJECT_ROOT`.
2. Inspect `PROJECT_ROOT/Assets/Scenes`, the current builder, existing artifacts, and user changes before editing.
3. Read `references/versioning-contract.md` before choosing paths.
4. Run `scripts/next_scene_version.py` to calculate the next scene and screenshot names.
5. State the new version before building and preserve every earlier `.unity` scene.

## Isolate the checkpoint

- Name scenes `<Scene>_<YYYYMMDD>_V<NN>_<Descriptor>.unity`.
- Give mutable scene-specific Volume profiles, generated meshes, shaders, and materials the same version suffix.
- Reuse an old dependency only when it is immutable and its serialized values will not be changed.
- Point the editor builder at the new paths; do not duplicate a scene and then mutate assets still shared with an earlier scene.
- Keep old scenes enabled or disabled as appropriate, but put the new accepted entry first in Build Settings only when it is the requested launch scene.
- Never delete an old checkpoint unless the user explicitly requests deletion.

## Preserve user work

- Inspect overlapping files before patching.
- Do not reset, revert, or rewrite unrelated dirty files.
- Use `apply_patch` for source edits.
- Keep API credentials outside scenes, scripts, logs, task JSON, and console output.

## Complete the checkpoint

1. Save the new scene and all new versioned dependencies.
2. Use `$ppvr-unity-visual-qa` to render, inspect, and validate it.
3. Confirm physical support, depth staging, camera framing, and required interactions before acceptance.
4. Return every local path as a complete absolute path.
5. Always return the final screenshot's complete absolute path.

## Resources

- `scripts/next_scene_version.py`: calculate collision-free scene, screenshot, and artifact paths without modifying the project.
- `references/versioning-contract.md`: naming, isolation, rollback, and handoff rules.
