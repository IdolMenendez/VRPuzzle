---
name: ppvr-reference-art-direction
description: Translate reference images and crops into immersive, high-fidelity Unity composition for PersonallyPuzzledVR. Use when the user asks to match a visual reference, correct camera staging, rebuild workshop furniture, improve realism, remove primitive-looking geometry, tune sunlight or Tyndall scattering, or compare a Unity render against supplied images.
---

# PPVR Reference Art Direction

Reconstruct the reference's visual relationships before refining individual props. Judge success from the final Unity render, not from hierarchy completeness.

## Analyze before editing

1. Inspect the full reference and every crop at original detail.
2. Inspect the current Unity screenshot at original detail.
3. Read `references/workshop-visual-bible.md` for the established Splash composition and failure modes.
4. Write a screen-space map: subject bounds, negative space, horizon/table line, light origin, overlap, and depth order.
5. Separate evidence from inference. Use the reference for visible facts and use world-space estimates only to reproduce those facts.

## Build in visual priority order

1. Camera projection and slight yaw
2. Foreground/midground/background distances
3. Hero silhouette and scale
4. Window, ship, right storage, and workbench masses
5. Key sunlight and continuous atmosphere
6. Secondary props and micro-detail

Do not polish a prop while the camera or large masses are still wrong.

## Geometry quality

- Do not expose primitive cubes, cylinders, or spheres as hero furniture, shelving, hands, or decorative props.
- Allow primitives for invisible colliders, light volumes, blocking, or architecture only when the final render does not read as placeholder geometry.
- Prefer authored or generated high-detail PBR assets for furniture and story props.
- Reject a generated asset if its silhouette, orientation, support, or semantic function is wrong even when its texture is attractive.
- Seat every object on a credible support surface; verify renderer bounds for floating or deep intersection.

## Lighting quality

- Keep the workshop honey-gold and sunlit, not uniformly red or orange.
- Establish one directional key from the visible window and neutral-warm fill for legibility.
- Use one soft continuous volumetric field with depth occlusion. Avoid stacked additive planes and stretched streak particles.
- Let the beam emerge from the window, travel diagonally through depth, feather at its boundary, and remain subordinate to the Logo.
- Reduce bloom, density, or emission when highlights erase PBR detail.

## Iterate from screenshots

1. Render with `$ppvr-unity-visual-qa`.
2. Compare full frame and left/right crops against the reference.
3. Fix the largest mismatch first.
4. Repeat until composition, support, lighting direction, and atmosphere hold together.
5. Use `$ppvr-scene-continuity` before accepting the iteration.

## Resource

- `references/workshop-visual-bible.md`: Splash screen-space composition, world staging, palette, and known regressions.
