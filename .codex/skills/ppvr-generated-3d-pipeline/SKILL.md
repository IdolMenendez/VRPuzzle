---
name: ppvr-generated-3d-pipeline
description: Create, convert, import, and validate versioned high-detail PBR 3D assets for PersonallyPuzzledVR. Use when a Unity scene needs furniture or props beyond visible primitives, when generating a concept/source image, when using Meshy or Tripo image-to-3D, when downloading FBX/GLB and texture maps, or when configuring generated models and URP materials.
---

# PPVR Generated 3D Pipeline

Produce an isolated, semantically correct asset bundle that survives Unity import and supports scene rollback.

## Decide whether to generate

- Generate when visible hero furniture or story props would otherwise read as primitives or repeated kitbash geometry.
- Keep stable high-quality project assets when they already match the needed silhouette and material response.
- Do not generate invisible colliders, light volumes, or trivial structural blocking.

## Create the source image

1. Use the `imagegen` skill for a new design source or reference-guided edit.
2. Show one isolated object or connected furniture ensemble on a neutral studio background.
3. Specify real dimensions, camera angle, functional parts, material families, required negative space, and explicit exclusions.
4. Exclude the room, people, hands, floating pieces, dramatic background lighting, and unwanted machinery.
5. Save the source as a versioned PNG under `/Volumes/APFS/编辑器/VRPuzzle/Assets/Splash/Generated3D` or the relevant scene asset folder.

## Convert and download

Read `references/provider-and-unity-import.md` before calling a provider.

- Prefer Meshy for the established PPVR workflow; use Tripo when it produces a materially better silhouette or Meshy is unsuitable.
- Read API keys interactively or from process environment. Never place a key in source, command history, task JSON, logs, scenes, materials, or the final response.
- Request textured PBR output and both FBX and GLB when available.
- Store provider task metadata under `/Volumes/APFS/编辑器/VRPuzzle/Artifacts` without credentials.
- Download base color, normal, metallic, roughness, and emission maps. Build a Unity metallic-smoothness map with smoothness in alpha when necessary.

## Inspect before Unity

1. Inspect the provider preview at original resolution.
2. Reject backboards, fused clutter, missing supports, wrong orientation, sealed shelves, or unintended room fragments.
3. Run `scripts/check_pbr_bundle.py` against the versioned bundle.
4. Regenerate rather than hiding a fundamentally wrong silhouette with scene placement.

## Import into Unity

- Disable imported cameras, lights, animation, and embedded materials.
- Import normal maps as normal maps with sRGB disabled.
- Keep base color and emission in sRGB; keep packed metallic-smoothness linear.
- Use a new version-specific URP Lit material with base, normal, metallic-smoothness, and restrained emission.
- Determine FBX up-axis and front direction from an actual Unity screenshot. Do not assume every provider export needs a `-90` degree X rotation.
- Validate renderer bounds, vertex count, physical support, and shadow behavior.

## Complete

Use `$ppvr-reference-art-direction` for placement and `$ppvr-unity-visual-qa` for the final engine render. Return complete absolute paths for the source PNG, model, texture bundle, Unity material, accepted scene, and screenshot.

## Resources

- `scripts/check_pbr_bundle.py`: read-only validation of a generated model and PBR map bundle.
- `references/provider-and-unity-import.md`: credential, generation, naming, and Unity import contract.
