# Provider and Unity import contract

## Versioned bundle naming

Use a stable prefix such as `AssemblyWorkbench_V03_Hero`:

- `<Asset>_V<NN>_Source.png`
- `<Asset>_V<NN>_Hero.fbx`
- `<Asset>_V<NN>_Hero.glb`
- `<Asset>_V<NN>_Hero_base_color.png`
- `<Asset>_V<NN>_Hero_normal.png`
- `<Asset>_V<NN>_Hero_metallic.png`
- `<Asset>_V<NN>_Hero_roughness.png`
- `<Asset>_V<NN>_Hero_metallic_smoothness.png`
- `<Asset>_V<NN>_Hero_emission.png`
- `<Asset>_V<NN>_MeshyPreview.png` or provider equivalent

## Source prompt contract

Include:

1. isolated object category and real dimensions
2. functional construction and required parts
3. material list
4. neutral three-quarter studio view
5. required open spaces
6. explicit exclusions

For furniture, explicitly exclude tall backboards, arches, gantries, robotic arms, room walls, and unrelated props unless they are part of the approved design.

## Credentials

- Use `getpass`, a protected environment variable, or a secure connector.
- Do not echo a key or include it in a process argument that may appear in `ps` output.
- Do not persist authorization headers.
- Metadata JSON may contain task IDs and signed download URLs but never the API key.

## Meshy image-to-3D baseline

The established PPVR Meshy workflow requests a standard textured model, PBR maps, remeshing, triangle topology, target polycount appropriate to screen size, and FBX plus GLB output. Poll the task to a terminal state, download all outputs, and retain the task JSON for provenance.

The existing task-specific implementation is:

`/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Assets/ModelBuilder/Scripts/generate_splash_v03_meshy.py`

Use it as a reference; do not hard-code its asset list into unrelated jobs.

## Unity texture settings

| Map | Texture type | sRGB | Notes |
| --- | --- | --- | --- |
| base color | Default | On | color data |
| normal | Normal Map | Off | set bump scale in material |
| metallic-smoothness | Default | Off | metallic RGB, smoothness alpha |
| emission | Default | On | keep intensity restrained |

Use mipmaps, trilinear filtering, appropriate anisotropy, and a maximum size that preserves close-up detail without wasting VR memory.

## Acceptance checks

- correct front and up axes in Unity
- no provider background geometry
- PBR maps assigned to a version-specific URP material
- enough topology for the visible silhouette
- physically credible scale and support
- final appearance confirmed in the target Unity camera
