# Personally Puzzled VR — Semantic Puzzle Asset Decomposition Standard V01

Last updated: 2026-07-17 11:12 CST

This document is mandatory for every generated exhibit that can become a puzzle. Environment assets such as the museum wall, display furniture, workbench, and archive architecture are excluded unless they are explicitly promoted to playable puzzle content.

## 1. Core rule

A beautiful monolithic AI mesh is only a visual master. It is never the final playable asset.

Every puzzle-capable exhibit must ship as one versioned family:

- `VisualMaster`: high-detail reference used to approve silhouette, materials, and surface detail.
- `SemanticParts`: authored pieces divided at meaningful construction seams.
- `AssembledPrefab`: the exact SemanticParts in their solved poses; used in selection, transition, completion, and archive scenes.
- `PuzzlePrefab`: the same SemanticParts with grab, rigidbody, collider, socket, dependency, ghost, audio, and haptic data.
- `ExplodedPrefab`: the same SemanticParts arranged on reachable trays for assembly staging and QA.

Do not use a separate one-piece display mesh that only resembles the puzzle result.

## 2. Mandatory image-first generation and cleanup pipeline

The asset family is approved in two dimensions before provider credits are spent on playable meshes. Arbitrarily cutting one generated 3D shell into pieces is not the default pipeline and may not be used to manufacture puzzle depth.

1. Generate and approve one complete-object master image in the locked stage style. It must show the final solved silhouette, material hierarchy, scale language, and intended front/three-quarter presentation.
2. Write the semantic part manifest from real construction logic while preserving the complete-object silhouette.
3. Derive a clean decomposition storyboard from that exact master image. Every proposed Part gets an isolated image or multi-view cell showing a complete, finished object with front, back, thickness, finished ends, and believable joinery. A crop that still contains neighbouring geometry or looks like a torn region fails before image-to-3D.
4. Run image-to-3D on those approved complete-part images with one selected provider and one locked style recipe. Meshy and Tripo may be compared on the same calibration object, but their styles may not be mixed inside one accepted family.
5. Generate the complete-object master in 3D as a silhouette/material reference, not as permission to slice it arbitrarily. Compare every generated Part against both the master image and complete 3D master.
6. Reject provider output with melted backs, missing thickness, fused neighbours, open shells, paper-like tears, unintentional holes, broken frames, or an isolated silhouette that does not read as the approved construction module.
7. Use a master-mesh cut only for a broad module whose seam is already a genuine architectural or mechanical boundary and whose approved exterior can be preserved without producing a surface patch. Independent part generation remains mandatory for topology-sensitive, thin, occluded, or ornament-bearing modules.
8. In Blender, perform cleanup only: repair non-manifold defects, preserve the approved surface, finish hidden mating faces, set pivots, author deterministic keys/colliders, build LODs, and export. Blender cleanup may not turn a rejected shard into an accepted Part merely by capping or thickening it.
9. In Unity, assemble the solved prefab from exactly those approved semantic Parts and verify that its fixed-front silhouette matches the complete-object master.
10. Render every loose Part through at least four inspection angles, plus assembled and exploded family views. A visual reviewer may veto any topology pass.
11. Run reach, socket, dependency, collider, progressive-difficulty, deterministic full-snap, clean-reset, audio, and scene-backup validation before promotion.

The machine-readable manifest must retain paths and hashes for the complete-object master image, decomposition storyboard, selected part images, provider outputs, four-angle audits, and final Unity prefab. The detailed gate is recorded in `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Docs/PUZZLE_ASSET_GENERATION_GATE_20260717_V01.md`.

### Detail budget

- Prefer readable medium-scale construction detail: bevels, frames, coamings, rail seats, windows, lattice bays, planking groups, brackets, trim, and material variation.
- Avoid hero fidelity that depends on hair-thin filigree, loose wires, sub-millimetre engraving, or fragile isolated rods that image-to-3D providers cannot reproduce consistently.
- “Reduced micro-detail” never means blockout geometry. Silhouette, proportion, edge treatment, material response, surface continuity, and major construction detail must remain polished enough that the assembled result feels collectible and rewarding.
- At gameplay scale, keep structural visible detail large enough to survive the selected LOD and headset resolution; transfer very fine detail into normal/roughness maps where appropriate.

## 3. Semantic seam rules

- Every playable part must read as a complete manufactured or architectural module when held by itself. It needs a closed silhouette, finished exposed surfaces, intentional edge treatment, and a clear functional identity. A spatial fragment that only becomes attractive after the neighbouring fragments hide its torn boundary is rejected.
- “Complete” applies to the visible object envelope, not only to topology. The player must be able to turn the loose part through 360 degrees and still understand what whole construction unit it represents. A watertight strip cut from a hull, roof, wall, or ornament is still rejected when it visually reads as a peeled surface or torn sheet.
- Use the accepted building modules as the positive visual benchmark: each loose Part must look like a small finished collectible object before it is assembled. It must have its own coherent outer silhouette, front and back treatment, structural thickness, perimeter frame or load-bearing body, believable termination at every exposed end, usable grip volume, and designed joinery. Merely thickening, capping, or making a torn-looking surface patch manifold does not make it a complete Part.
- Completeness and assembly intent must coexist. A valid Part looks whole by itself, yet its keyed tenon, flange, coaming, rail seat, bracket, shoe, receiver, or load path makes its role in the larger construction immediately understandable. Do not create visual damage to manufacture puzzle difficulty.
- Preserve the construction language of each subject instead of imposing one universal slicing pattern. Buildings use complete bays, portals, balconies, roof/dormer assemblies, towers, and crowns; ships use hull/keel cores, decks, bow/stern assemblies, bulwark batteries, deckhouses, mast-and-sail assemblies, and rigging crowns; lattice monuments use keyed legs, arch/truss bays, platforms, taper stages, and summit assemblies.
- Fragmentation is never an art direction. Do not repeat a torn-sheet, peeled-skin, shard, or surface-patch treatment across a whole model family merely to make the object look decomposed. Most Parts must preserve a complete spatial volume or a self-supporting framed assembly; the small number of naturally thin Parts are allowed only when the real construction unit is itself thin and still has finished perimeter structure, front/back treatment, attachment hardware, and a coherent held-object silhouette.
- A part may be slender only when the real-world item is itself a complete slender assembly, such as a finished mast-and-sail unit, framed bulwark battery, bowsprit rig, or lattice bay. It must include its frame, attachment treatment, and usable grip volume; an unframed ribbon of surface detail is not a valid part.
- Prefer complete construction units such as a framed façade bay, door portal, balcony wall module, roof slope with its trim, dormer, clock tower, deck, bulwark battery, stern gallery, figurehead, mast-and-sail assembly, or rigging crown.
- A visible seam must look designed: quoin line, cornice, sill, roof flashing, deck coaming, lap joint, flange, rail, mortise-and-tenon, keyed pin, axle socket, or another believable assembly interface.
- Every hidden mating face must be capped and visually clean. Raw clipping planes, open shells, paper-thin tears, disconnected triangles, and arbitrary Boolean scars fail the asset gate even when those defects are eventually hidden after snap.
- The rendered playable mesh itself must pass this complete-module gate. A closed collision or mating proxy cannot be used to excuse an open, torn, or unfinished visible mesh.
- Every part must still read as a deliberate finished object when shown alone at inspection scale. Manifest flags and automated topology numbers are supporting evidence only; the isolated render is the visual authority and may veto a technically marked pass.
- Every candidate family requires an exploded-view audit where each part remains visually coherent by itself. For hero families, at least one isolated-part turntable/contact sheet must demonstrate finished edges and closed mating geometry.
- Separate on real architectural or mechanical joints: floor plates, roof lines, deck coamings, complete hull-core receivers, bulwark rails, tower stages, observation platforms, wing roots, hub shafts, buttress clusters, and façade modules.
- Never cut across a hero ornament, face, rose window, engraved plaque, lattice bay, door, window, sail graphic, or strong continuous wood grain unless no meaningful alternative exists.
- Thin structures are grouped into robust modules. The player assembles a lattice bay, mast-and-sail module, roof assembly, blade pair, or buttress cluster rather than loose millimetre rods.
- The solved object must have no visible light leaks, z-fighting, open backs, doubled surfaces, or texture discontinuities from the front reference view or normal head movement.
- Mating faces are hidden after snap. They may use simplified capped geometry and do not need the hero texture.
- Every part has a unique keyed orientation. Symmetric-looking pieces use subtly different keys or underside markings so wrong placement is physically rejected without relying on a UI message.

## 4. VR size, reach, and handling

- Solved hero models should generally occupy approximately 0.42–0.62 m in their longest dimension, adjusted per reference composition.
- All staged parts and sockets remain approximately 0.35–0.60 m from the player's chest/shoulder centre.
- One-hand parts should normally have a longest dimension of 0.06–0.22 m and a usable grip span of at least 0.035 m.
- Avoid functional thickness below approximately 0.012 m. Visually thin rails and lattice may remain thin, but their grab/collider proxy must be more forgiving and hidden.
- Two-hand parts should normally span 0.22–0.45 m and support hand separation of approximately 0.18–0.35 m.
- Difficulty must never come from making pieces too small, moving them beyond reach, or reducing accessibility thresholds.
- Preserve the existing interaction contract: more than 0.15 m gives no ghost, 0.05–0.15 m gives a breathing exact-silhouette ghost, and within 0.05 m the part magnetizes and assists alignment within ±30°.
- Every playable model must place sockets on at least three meaningfully different assembly surfaces or approach directions. A front-only sequence is rejected even when all parts are semantic.
- The supporting/left hand must be able to grip and rotate the assembled base in place without locomotion. Snapped parts and sockets rotate as one rigid assembly.
- A socket on the far or hidden side must not reveal its ghost through the model and must not accept magnetic alignment until the player has rotated that assembly surface toward the viewing/working side.

## 5. Socket and prefab contract

Each part package must contain:

- `Part_<nn>_<semanticName>` visual mesh.
- `Grip_<nn>_Primary` and optional secondary grip anchors.
- `Collider_<nn>_<shape>` compound convex collider proxy.
- `Socket_<nn>_<semanticName>` solved transform.
- `Ghost_<nn>_<semanticName>` exact part silhouette using the approved gold ghost material.
- `Key_<nn>` hidden orientation key or logical socket signature.
- `LOD0`, `LOD1`, and `LOD2` where the part is large enough to benefit.
- Authored mass class: `light`, `normal`, or `heavy-two-hand`.
- Dependency IDs that must already be snapped.
- Local snap axis and approved approach cone.
- Grab, hover, near-socket, snap, miss, and optional heavy-settle audio assignments.

The selection, completion, and archive scenes instantiate `AssembledPrefab`. The assembly scenes instantiate `PuzzlePrefab`. Both prefabs reference the same part meshes and materials.

## 6. Difficulty ladder

Difficulty grows through construction reasoning, not discomfort.

Every level must also contain an internal learning curve. Increasing the part count alone is insufficient.

- Begin with one or two unmistakable structural pieces and a forgiving approach.
- Introduce dependency ordering before introducing symmetry.
- Introduce a readable approach direction before tightening rotational alignment.
- Introduce keyed orientation before asking the player to distinguish repeated or mirrored modules.
- Reserve multi-branch dependencies, two-hand weight, and repeated-family discrimination for later levels.
- Keep the outer hint distance at 0.15 m and snap distance at 0.05 m. Make later steps more thoughtful through semantic prerequisites, approach cones, and keyed orientation, never by shrinking parts or moving them farther away.

### Level 1 — Workshop Cottage, 13 complete modules

- Thirteen finished architectural modules replace the rejected five-large-layer prototype.
- Step 1 is a forgiving foundation tutorial, followed by one complete structural core.
- The dependency graph then opens into independent front, left, right, and rear branches. The player may solve available branches in more than one order.
- Side and rear modules require in-place base rotation; no fixed front view exposes every valid socket.
- Roof work is divided into complete trimmed roof modules, dormer/clock ornaments, ridge cap, and clock tower rather than horizontal sliced shells.
- The final clock tower/crown step revisits the right and top surfaces with the narrowest keyed approach.
- Parts use two shallow reachable tiers: structural modules nearest and roof/ornament modules behind, all within the same 0.35–0.60 m reach envelope.
- Goal: teach hold-base, rotate, inspect construction interfaces, choose between branches, approach the exact ghost, and snap.

Semantic parts:

1. Stone foundation, plinth, and pavement module.
2. Complete lower front shopfront module.
3. Complete lower left wall module.
4. Complete lower right wall module.
5. Complete lower rear service-wall module.
6. Complete upper front façade module.
7. Complete upper left wall and balcony module.
8. Complete upper right wall and balcony module.
9. Complete upper rear wall module.
10. Front-left roof and dormer module.
11. Front-right roof and dormer module.
12. Rear mansard-roof module.
13. Right-side clock chimney tower module.

### Level 2 — Classical Sailing Ship, 12 complete modules

- The previous split-hull candidate is rejected because its staged pieces read as torn shell fragments.
- A complete keel/hull core is the main two-hand weight module; surrounding pieces are finished shipbuilding assemblies with capped, designed interfaces.
- The hull core retains a coherent boat-shaped volume with keel, ribs, framing, and finished gunwale/receiver treatment. It must never resemble left/right paper skins peeled from an otherwise missing vessel.
- Long side pieces are accepted only as complete framed bulwark-and-cannon-battery assemblies with rails, posts, battery openings, lower sill, end caps, and attachment keys. Bare decorative hull strips or texture bands are rejected even if manifold.
- The main deck must be a finished deck assembly, not a flat board: it needs visible plank structure plus coaming/perimeter frame, underside beams or a closed structural back, at least one complete hatch/capstan/receiver feature, finished bow/stern terminations, and keyed seating into the hull core.
- Bow and stern Parts must retain their own enclosed spatial volume and complete identity when separated. A figurehead stuck to a triangular hull skin or a gallery façade without cabin depth is rejected.
- Every mast Part must be a complete mast-and-sail construction unit with mast shoe, yard/boom, sail or furled-sail body, necessary local rigging, and a stable hand-scale grip zone. Loose rigging sheets, decorative line webs, or unsupported sail surfaces are rejected.
- The final rigging/crown Part may be visually light only when it is a framed, self-supporting assembly with rails, cross-members, end anchors, and a finished central grip/attachment body. A long flat strip or floating rope drawing is rejected.
- Port/starboard symmetry is introduced through complete bulwark/cannon-battery modules rather than ripped half-hulls.
- Required search surfaces include port, starboard, deck, bow, and stern; the player must rotate the hull assembly to reach the relevant side.
- Deck, bow, stern, side batteries, cabin, and mast modules create a branching dependency graph.
- Internal ramp: two-hand hull core, deck lock, bow/stern distinction, keyed side batteries, cabin/deckhouse, three mast-and-sail modules, final rigging crown.
- Parts use two low stepped trays: structural modules nearest, upper rig modules behind, all within reach.

Semantic parts:

1. Complete keel, lower hull, and internal-frame core.
2. Main deck and coaming module.
3. Complete bow and figurehead module.
4. Complete stern gallery and lantern module.
5. Port bulwark and cannon-battery module.
6. Starboard bulwark and cannon-battery module.
7. Cabin, deckhouse, and hatch module.
8. Foremast and sail module.
9. Mainmast and sail module.
10. Mizzen mast and sail module.
11. Bowsprit, anchors, and forward rig module.
12. Final standing-rigging, rail-crown, and flag module.

### Level 3 — Eiffel Tower, 16 parts

- Four-leg spatial reasoning, repeated visual language, and multi-stage vertical construction.
- Required search surfaces include all four cardinal sides plus multiple vertical tiers; no single camera-facing sequence may expose every socket.
- Similar modules remain comfortably sized and use keyed underside geometry.
- Three dependency tiers prevent impossible floating placements.
- Internal ramp: four keyed feet, orthogonal arch pairs, first platform lock, repeated middle bays, upper deck, tapered crown and summit.
- Parts use three shallow height tiers on a waist-level arc; no tier is beyond 0.60 m reach.

Semantic parts:

1. Northwest foot and lower leg module.
2. Northeast foot and lower leg module.
3. Southwest foot and lower leg module.
4. Southeast foot and lower leg module.
5. North lower arch/truss bay.
6. South lower arch/truss bay.
7. West lower arch/truss bay.
8. East lower arch/truss bay.
9. First observation platform.
10. North middle taper bay.
11. South middle taper bay.
12. West middle taper bay.
13. East middle taper bay.
14. Second observation platform and upper deck.
15. Upper taper tower module.
16. Summit room, beacon, and antenna module.

## 7. Selection-carousel exhibit decomposition targets

These are production targets for the five surrounding Stage 2 exhibits. They ensure every displayed collectible can later become a real puzzle without replacing its visible model.

- Sailing ship: 10 parts, as defined above.
- Pagoda: 9 parts — landscape plinth, three storey bodies, three roof/eave modules, and finial/crown.
- Windmill: 8 parts — landscape plinth, lower tower, upper tower, balcony ring, roof cap, drive hub, and two keyed crossed-blade pairs.
- Biplane: 8 parts — fuselage/engine, lower wing, upper wing, left strut/gear module, right strut/gear module, tailplane, rudder, and propeller/cowling.
- Twin-spire cathedral: 12 parts — plinth/floor, nave, transept/crossing, choir/apse, central façade, left tower, right tower, left spire, right spire, left buttress cluster, right buttress cluster, and central roof/crown.
- Eiffel hero: 16 parts, as defined above.

Stage 2 may initially display optimized assembled versions of these part families, but acceptance requires a recorded decomposition manifest and a path to the exact part-based prefab. Before a model becomes playable, every listed part must exist and pass the full puzzle validator.

### Generation-feasibility gate

Before spending provider credits, every proposed puzzle model must be classified by semantic part family:

- `cut-master`: broad structural shells whose approved visible surface can be preserved by cutting the high-detail visual master along real seams.
- `independent-generate`: thin, mechanically separate, deeply occluded, or topology-sensitive modules that are safer to generate/model separately and mate to the master.
- `reuse-mirrored-source`: visually related left/right modules that may share a clean source mesh, but must receive different hidden keys, pivots, colliders, and socket signatures.
- `manual-author`: mating caps, hidden keys, collider proxies, grip volumes, ghost meshes, and small structural connectors that should be authored deterministically rather than hallucinated by an image-to-3D provider.

The machine-readable catalog records the intended strategy and known risk for every model. A model fails the gate if its hero detail depends on cuts across ornaments, if most playable pieces would be fragile slivers, or if no reachable grip can be placed without obscuring the assembly task.

Current production decisions:

- Workshop cottage: high feasibility. Cut foundation, wall shell, roof, and tower from one master; author the front façade bay as a clean keyed insert.
- Sailing ship: medium feasibility. Cut keel, hull shells, deck, bow, and stern from the master; independently generate/model mast-and-sail modules and final rigging crown.
- Eiffel tower: medium feasibility. Cut robust lower-leg, arch, platform, middle-bay, and upper-tower modules from the master; manually author hidden caps/keys and treat the antenna as part of the summit module.
- Pagoda: high feasibility. Storeys and roof/eave modules follow natural horizontal seams; finial and rail cluster may be independently cleaned.
- Windmill: high feasibility. Cut plinth/tower/balcony/roof from the master; independently author the axle and two robust keyed blade-pair modules.
- Biplane: medium feasibility. Cut fuselage and broad wing/tail surfaces from the master; independently author strut/gear modules and propeller/cowling.
- Twin-spire cathedral: medium-high feasibility. Cut major architectural masses on floor/roof/tower seams; use robust buttress clusters instead of individual thin buttresses.

### Difficulty and tray progression

- Five-part tutorial: one shallow tray, unique silhouettes, no mirrored ambiguity, one dependency branch at a time.
- Eight-to-ten-part intermediate exhibits: two shallow stepped trays, nearest tier for structural parts and rear tier for upper/mechanical modules; introduce one mirrored/keyed family and one two-hand part.
- Twelve-to-sixteen-part advanced exhibits: three shallow stepped trays, dependency tiers encoded by placement height and light state; introduce repeated visual language and keyed spatial reasoning without shrinking parts or exceeding the reach envelope.

Tray tiering changes cognitive order, not physical reach. Every tier remains inside the same approximate 0.35–0.60 m chest-centred interaction envelope.

## 8. Visual and technical acceptance

An exhibit family is rejected if:

- Its solved prefab uses a monolithic substitute rather than the real parts.
- A staged part reads as a torn fragment, open shell, arbitrary slice, or unfinished Boolean cut rather than a complete construction module.
- A staged part has been made technically manifold but still reads visually as a thickened skin, capped shard, flat decal board, unsupported façade, or ripped slice instead of a finished collectible construction object.
- A family uses torn fragments, peeled skins, or arbitrary patches as a repeated visual language instead of subject-specific complete construction modules.
- A visible mesh fails the complete-module test even if its simplified collider or hidden mating proxy is watertight.
- A manifest claims `complete_modules_pass` while the isolated-part contact sheet still shows paper-like edges, raw cut planes, missing backs, damaged silhouettes, or components that only become attractive when neighbouring pieces conceal them.
- An exposed mating boundary lacks deliberate trim, cap, flange, key, rail, flashing, sill, or another credible assembly treatment.
- Part cuts look arbitrary or visibly damage the approved silhouette.
- A piece is too small or thin for reliable hand interaction.
- A collider is concave, unstable, or substantially larger than the visible object in a way the player can feel.
- A sequence allows pieces to float without structural support.
- Symmetric pieces can snap into the wrong side.
- Ghost geometry is a sphere, box, arrow, or generic glow instead of the exact next part/socket silhouette.
- All sockets can be completed from one fixed front angle, or a hidden-side ghost/assist leaks through the model.
- The assembled base cannot be rotated in place with the supporting hand while already-snapped parts remain attached.
- The solved part assembly visibly differs from the approved visual master.

Required evidence per playable model:

- Visual master audit render.
- Smart Topology/native separation audit.
- Semantic exploded-view render with numbered pieces.
- Isolated-parts/contact-sheet render proving each part has a complete silhouette, finished exposed faces, and capped mating geometry.
- Solved-prefab render.
- Part manifest and dependency graph.
- Reach report.
- Collider report.
- Deterministic full-snap log.
- Manual VR-scale review.

## 9. Current implementation consequence

`/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Assets/Editor/FullGameBuilder.cs` currently creates puzzle visuals using `PrimitiveType`. That geometry is functional prototype data only and is visually rejected.

Retain its proven 5/10/16 progression, 0.15 m hint distance, 0.05 m snap distance, ±30° assist, reachable staging, two-hand constraint, save flow, and deterministic smoke tests. Replace every visible primitive piece and primitive ghost with the semantic generated part prefabs defined here.
