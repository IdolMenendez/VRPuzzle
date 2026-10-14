# PPVR workshop visual bible

## Splash composition

Treat the frame as a near-frontal workshop view with a slight 4–8 degree yaw. Depth comes from overlap and staged distance rather than a strongly oblique camera.

Approximate screen-space allocation:

- Left window: left 0–26%, extending high into the frame
- Ship and shipwright bench: left 5–33%, in front of the window
- MODEL BUILDER sculpture: center 30–70%, dominant hero
- Workshop wall storage: right 65–100%, behind the hand and hero
- Assembly workbench: lower 20–30% of the frame, read from above as a standing work surface
- Hands: enter from lower left and lower right; right index reaches the cyan core

## Depth order

Use this order as a starting point, then tune from screenshots:

1. foreground assembly bench
2. hands
3. Logo
4. ship and shipwright bench
5. right storage
6. rear-left window and rear wall

For the accepted Splash V03, the useful world-space pattern was approximately:

- foreground bench near `z = -0.3`
- Logo near `z = 0.6`
- ship near `z = 1.1`
- storage near `z = 2.6`
- rear wall/window near `z = 3.2`

Do not copy these values blindly into another scene; preserve the ordering and tune the projection.

## Prop semantics

### Left

Use a broad shipwright workbench with drawers, tool chest, cradle supports, bottles, brushes, timber strips, and a small plant. The ship must rest on the cradle. Do not use a narrow display plinth or let the ship float.

### Foreground

Use a standing-height maker bench with a flat walnut top, green gridded cutting mat, drawers, vise, ruler, tools, and restrained clutter. Keep the central working area open. Avoid gantries, robotic arms, oversized turntables, or a low tatami-like platform.

### Right

Use a wide horizontal ensemble: shelves, books and boxes, model biplane, jars, parts organizer, blueprint, oil lamp, brush pot, and drawer cabinet. Avoid one monolithic arched fantasy cabinet.

## Window and sunlight

Mount the window close to the rear-left plane and keep it nearly frontal. Use multiple mullions, bright foliage outside, and a single diagonal sun volume aimed toward the center/front work surface.

The atmosphere should be visible but translucent. Preserve ship rigging, Logo lettering, and furniture texture through the haze.

## Palette

- aged walnut and honey timber
- restrained antique brass and blackened iron
- dark green cutting mat
- golden-white sunlight
- localized cyan interaction energy

Avoid global red grading. Use warm key light with more neutral ambient and fill.

## Known regressions

- Excessive yaw creates a corridor-like side-wall view.
- Perfectly frontal staging looks flat when every object shares the same depth.
- Giant foreground furniture blocks the Logo and reads as a wall.
- Wrong FBX up-axis shows cutting mats or shelves vertically.
- Back-facing generated assets appear as blank wooden rectangles.
- Multiple light ribbons look synthetic and hard-edged.
- Large stretched dust particles read as sparks rather than Tyndall haze.
- Shared mutable materials break rollback fidelity across scene versions.
