# Personally Puzzled VR — Image-First Puzzle Asset Gate V01

Last updated: 2026-07-17 11:12 CST

This gate is mandatory for every new playable model family. A provider job may not be promoted because it is merely watertight or technically importable.

## Required order

1. `MasterImage`: one approved complete solved-object image in the locked family style.
2. `SemanticManifest`: construction-based Part list, dependencies, directions, size, mass, and joinery.
3. `PartStoryboard`: isolated/multi-view images derived from the MasterImage; every cell is a complete finished module.
4. `ProviderCalibration`: the same representative cell generated on Meshy and Tripo only when a provider has not yet been locked.
5. `ProviderLock`: one winning provider, prompt recipe, material language, scale language, and detail budget for the entire family.
6. `PartImageTo3D`: parallel provider jobs for approved isolated Part images.
7. `ProviderOutputAudit`: reject fused neighbours, melted backs, open/torn shells, missing thickness, broken frames, or inconsistent style.
8. `Cleanup`: preserve the accepted surface; author pivots, capped mating faces, hidden keys, colliders, LODs, and sockets.
9. `FourAngleAudit`: front, rear, left/right three-quarter views for every loose Part.
10. `FamilyAudit`: assembled, exploded, fixed-front master overlay, and in-hand scale.
11. `UnityGate`: reachable staged layout, nonlinear dependencies, multi-angle sockets, snap/reset smoke, audio routing, independent scene backup, and screenshots.

## Hard rejection conditions

- The Part is a spatial crop, peeled skin, arbitrary shell strip, torn-looking shard, or capped surface patch.
- The Part has no coherent standalone identity or cannot be understood while held and rotated through 360 degrees.
- A provider filled the intended socket/joinery with neighbouring geometry.
- The back, underside, exposed ends, or mating boundary are visibly unfinished.
- The accepted family mixes Meshy and Tripo surface styles without an explicit fallback re-style pass.
- Fine detail is so fragile that it melts, disappears at gameplay scale, or creates unusable colliders.
- Puzzle difficulty is produced by making Parts tiny, remote, front-only, or linearly stacked.
- The Unity runtime references a rejected/historical provider output instead of the frozen selected family.

## Required persistent evidence

Each family record must contain:

- absolute path and SHA-256 for the MasterImage;
- absolute paths and SHA-256 values for the PartStoryboard and selected Part images;
- provider name, model/version, task IDs without credentials, and frozen prompt recipe;
- exact imported FBX/GLB paths and hashes;
- per-Part four-angle audit result;
- assembled/exploded screenshots;
- reach, dependency, direction, snap, reset, audio, and backup results;
- explicit list of rejected assets that the runtime is forbidden to reference.

## Current active families

- Cottage V04: thirteen complete architectural modules; active and validated.
- Ship V08: one continuous full-length hull plus twelve complete construction assemblies; active and validated. V06 torn modules and V07 split lower-hull pods are historical only and forbidden in the polished runtime.
- Eiffel V05/V06: sixteen complete lattice construction modules; active and validated. Open V04 partitions are historical only and forbidden in the polished runtime.

The visual reviewer remains the final authority. Automated topology, manifold, face-count, and manifest flags are supporting evidence, not permission to promote an unattractive Part.
