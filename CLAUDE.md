# PersonallyPuzzledVR project workflow

Use the matching project-local Skill before acting on tasks in this repository:

- Scene creation, scene edits, rollback, and version paths: `/Volumes/APFS/编辑器/VRPuzzle/.codex/skills/ppvr-scene-continuity/SKILL.md`
- Reference-image composition, workshop staging, camera, lighting, and visual fidelity: `/Volumes/APFS/编辑器/VRPuzzle/.codex/skills/ppvr-reference-art-direction/SKILL.md`
- AI concept images, Meshy/Tripo conversion, generated FBX/GLB, PBR maps, and Unity import: `/Volumes/APFS/编辑器/VRPuzzle/.codex/skills/ppvr-generated-3d-pipeline/SKILL.md`
- Final Unity rendering, screenshot inspection, iteration, and automated validation: `/Volumes/APFS/编辑器/VRPuzzle/.codex/skills/ppvr-unity-visual-qa/SKILL.md`

For a visual Unity job, apply the Skills in this order when relevant:

1. `ppvr-scene-continuity`
2. `ppvr-reference-art-direction`
3. `ppvr-generated-3d-pipeline`
4. `ppvr-unity-visual-qa`

Persistent continuation contract:

- At the start of every resumed or new session, read `/Volumes/APFS/编辑器/VRPuzzle/Docs/CONTINUATION_STATE.md` completely before changing the project.
- Then read `/Volumes/APFS/编辑器/VRPuzzle/Docs/PUZZLE_ASSET_DECOMPOSITION_STANDARD_20260716_V01.md` before generating, splitting, importing, or wiring any puzzle-capable exhibit model.
- Then read `/Volumes/APFS/编辑器/VRPuzzle/Docs/STAGE_VALIDATION_INDEX.md` and the active stage's `VALIDATION.md`.
- Treat those files, the current filesystem, Git status, running processes, and current artifacts as authoritative over compressed conversation memory.
- Update `/Volumes/APFS/编辑器/VRPuzzle/Docs/CONTINUATION_STATE.md` after every accepted or rejected generated asset, material change, scene build, capture, validation, or material blocker.
- Every stage must have a versioned `VALIDATION.md` under `/Volumes/APFS/编辑器/VRPuzzle/Artifacts/Checkpoints`. Do not mark a stage accepted until that file records the independent scene, final front screenshot, left-eye screenshot, overlay, difference image, validation log, audio evidence, manual visual review, and user acceptance state.
- Meshy and Tripo are A/B provider candidates, not two styles to mix. Calibrate on the same source, lock one provider/style recipe for the stage, and use the other only when the locked provider fails a quality gate.
- Stage 2 V04 is locked to Meshy; Tripo is fallback-only.
- Run independent assets concurrently on the locked provider rather than serially.

Mandatory project rules:

- Preserve every accepted scene as an independent version; never overwrite the last accepted scene.
- Every puzzle-capable generated exhibit must be delivered as semantic assembly parts. Selection, completion, and archive displays must instantiate the assembled prefab made from those same parts, not a separate monolithic look-alike.
- Preserve unrelated user changes.
- Do not expose or persist provider API keys.
- Inspect final screenshots at original resolution before completion.
- After every completed job, provide the final screenshot's complete absolute path.
- Whenever providing any local path, write the complete absolute path verbatim; do not shorten it or hide it behind a different link label.
