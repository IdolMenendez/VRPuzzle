# Personally Puzzled VR — Unified Art Bible V01

## 1. Non-negotiable visual priority

Every stage is judged in this order:

1. Match the supplied design image from the fixed front reference camera.
2. Read as a polished, authored miniature-craft experience rather than a blockout.
3. Preserve one coherent visual language across all eight stages.
4. Keep the scene physically credible and comfortable in VR.
5. Optimize only after the hero silhouette, material response, lighting hierarchy, and effects pass visual review.

A scene is rejected when any hero object, cabinet, hand, furniture ensemble, puzzle model, or major background prop visibly reads as exposed cubes, cylinders, spheres, repeated primitives, or a generic AI asset that does not match the reference silhouette.

## 2. Unified world language

### Narrative

The whole game takes place in one enchanted collector's model workshop. Selection, assembly, completion, and collection are different ceremonial zones of the same building, not unrelated demo scenes.

### Architecture

- Circular or gently curved rooms with dark walnut wall panels.
- Shallow Art Deco arches, inset brass rails, carved wood moulding, and warm plaster only as a secondary material.
- Tall arched windows reveal a quiet blue-violet evening outside.
- Furniture has believable joinery, feet, trim, bevels, hardware, contact shadows, and age variation.
- Repeated displays share a design family but vary subtly in wear, wood grain, and internal illumination.

### Materials

- Primary wood: aged walnut, readable grain, satin varnish, roughness variation, darkened creases.
- Secondary wood: honey oak for work surfaces and selected trim.
- Metal: restrained antique brass with darker patina in recesses; no flat yellow metal.
- Glass: optically clear, slightly warm, physically thin, subtle edge tint, controlled reflections, no opaque blue shell.
- Iron: dark burnished iron for miniature structures and mechanical parts.
- Interaction energy: localized cyan only around hands, labels, and active affordances.
- Celebration energy: warm gold only for trails, star lights, completion dust, and ritual illumination.

### Lighting

- One dominant warm key or spotlight per composition.
- Neutral-warm fill preserves material detail instead of flooding the frame orange.
- Cool evening windows provide separation in the background.
- Bloom is restrained; highlights may glow but may not erase wood grain, brass patina, glass edges, or model detail.
- Volumetric light must read as a continuous feathered field, never stacked planes or solid cones.

### Effects

- Gold effects use layered motion: one readable ribbon or arc, sparse bright motes, and a softer residual dust field.
- Cyan effects are thin and functional: hand rim, active base ring, label border, snap outline.
- No large generic particle spheres, firework fountains, opaque fog cards, or thick neon tubes.

### Audio identity

- Original chamber-light-music palette: felt piano, celesta, soft strings, wooden percussion, subtle room tone.
- Every stage has its own musical layer while preserving common motifs and instrumentation.
- Physical actions use tactile wood, brass, glass, paper, and miniature-mechanism sounds.
- Snap, hint, completion, flight, and archive sounds receive distinct melodic signatures.

## 3. Asset production rule

- Meshy and Tripo3D are A/B quality sources, not two art styles to mix inside a finished stage.
- Calibrate both providers on the same isolated reference asset, then lock the winning provider, prompt structure, scale language, and material language for the stage.
- Stage 2 V04 is locked to Meshy after the windmill calibration because it matches the already accepted Eiffel, pagoda, pedestal, and curved wall asset family. Tripo is fallback-only when a Meshy result fails its standalone quality gate.
- A clean isolated reference image is created before image-to-3D when the supplied design frame contains hands, rooms, or several objects.
- Keep only the provider result that best matches the design silhouette. Attractive texture alone is not acceptance.
- Blender is used for cleanup, separation, retopology, UV/PBR repair, pivots, scale, collision, and LOD. It is not the default tool for constructing hero assets from visible primitives.
- Every provider asset keeps a versioned source image, task metadata without credentials, preview, GLB/FBX, texture maps, cleanup file, Unity material, and in-engine screenshot.
- Generated background fragments, fused room geometry, unwanted backboards, sealed openings, malformed supports, and wrong-view assets are rejected and regenerated.
- Every puzzle-capable exhibit follows `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Docs/PUZZLE_ASSET_DECOMPOSITION_STANDARD_20260716_V01.md`.
- The full-detail AI output is a visual master, not a playable monolith. Solved display, completion, and archive prefabs are assembled from the same semantic parts used by gameplay.
- Do not accept provider-created fragment islands as puzzle design. Part boundaries follow real construction seams, comfortable VR grab sizes, dependency order, and keyed hidden sockets.

## 4. Shared VR scale and reach

- Reference-camera eye height and the initial XR eye midpoint coincide.
- Primary puzzle manipulation remains approximately 0.35–0.60 metres from the player's chest/shoulder centre.
- Candidate displays wrap around the near field without requiring steps or torso twisting.
- Hero puzzle models are large enough for fine hand interaction and remain visually dominant.
- Two-hand parts communicate mass through scale, hand separation, inertia, damping, haptics, and sound.

## 5. Stage 2 — Selection museum V04 screen-space specification

Reference image: 1280×720.

### Camera and composition

- Near-frontal view with only enough yaw to expose room curvature.
- Central hero showcase occupies approximately x=500–795 and y=42–535.
- Central miniature occupies approximately x=570–710 and y=115–390.
- Five candidate showcases form a shallow waist-height arc from approximately x=0–1275 and y=280–525.
- Left open hand occupies approximately x=95–450 and y=195–705.
- Right hand remains a small secondary foreground silhouette around x=1020–1275 and y=535–720.
- The compass medallion is centred at the bottom and cropped by the frame.
- The top spotlight is centred over the hero cabinet and creates the strongest vertical light hierarchy.

### Central showcase

- One seamless cylindrical curved-glass enclosure with no vertical metal uprights.
- Thin polished brass rings at top and bottom.
- Floating shallow canopy with a warm internal downlight.
- Sculpted, lathed walnut plinth with stepped brass trim and a recessed cyan-edged destination label.
- Glass size is proportional to the featured miniature: enough breathing room without dwarfing it.
- The featured Eiffel miniature is roughly 45–55% of the cabinet's internal height.

### Candidate showcases

- Five smaller members of the same design family, not generic jars on cylinders.
- Each uses a seamless glass dome/cylinder, sculpted walnut plinth, brass edge, and low warm internal pool light.
- Candidate miniatures are ship, pagoda, windmill, biplane, and cathedral.
- Each miniature must remain identifiable from the reference camera.

### Hall

- Curved dark-walnut museum wall with moulded bays and a raised gallery band.
- Two large framed Art Deco travel posters flank the central cabinet.
- Tall arched blue-evening windows and warm wall lanterns create depth.
- Radial walnut floor boards converge on a brass-and-wood compass medallion.
- No repeated background cylinders, placeholder pillars, empty black voids, or oversized generic display furniture.

### Interaction and motion

- The left open hand controls selection with a gentle lateral wave.
- The active candidate enlarges and flies along a gold arc into the centre while the old candidate shrinks and returns.
- Camera and player rig remain still; perceived transition comes from models, light handoff, particles, audio, and slight showcase illumination changes.
- The active label updates without presenting a floating virtual button.

## 6. Per-stage acceptance gate

Before a checkpoint may be called complete:

- New versioned scene and mutable dependencies exist.
- Hero assets do not read as primitives at 100% screenshot zoom.
- Reference camera render, left-eye render, overlay, and difference image exist.
- Full frame and critical crops have been manually inspected.
- The largest visible mismatch has been iterated until no structural mismatch remains.
- Scene-specific validator passes with an explicit success marker.
- Audio assigned to the stage is present and audible in a desktop flow test.
- The final screenshot corresponds to the final saved scene.
- The user receives complete absolute paths for the scene, screenshot, left-eye screenshot, overlay, difference image, and validation log.
