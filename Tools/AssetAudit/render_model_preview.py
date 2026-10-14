import math
import os
import sys

import bpy
from mathutils import Vector


def reset_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.materials, bpy.data.cameras, bpy.data.lights):
        for datablock in list(datablocks):
            if datablock.users == 0:
                datablocks.remove(datablock)


def import_model(path):
    suffix = os.path.splitext(path)[1].lower()
    if suffix in {".glb", ".gltf"}:
        bpy.ops.import_scene.gltf(filepath=path)
    elif suffix == ".fbx":
        bpy.ops.import_scene.fbx(filepath=path)
    else:
        raise ValueError(f"Unsupported model format: {suffix}")


def visible_meshes():
    return [obj for obj in bpy.context.scene.objects if obj.type == "MESH" and not obj.hide_render]


def bounds(objects):
    points = [obj.matrix_world @ Vector(corner) for obj in objects for corner in obj.bound_box]
    minimum = Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points)))
    maximum = Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points)))
    return minimum, maximum


def normalize(objects):
    minimum, maximum = bounds(objects)
    center = (minimum + maximum) * 0.5
    size = maximum - minimum
    scale = 2.4 / max(size.x, size.y, size.z, 0.0001)
    roots = [obj for obj in bpy.context.scene.objects if obj.parent is None and obj.type not in {"CAMERA", "LIGHT"}]
    for obj in roots:
        obj.location = (obj.location - center) * scale
        obj.scale *= scale
    bpy.context.view_layer.update()
    minimum, maximum = bounds(objects)
    return minimum, maximum


def add_floor(z):
    bpy.ops.mesh.primitive_plane_add(size=16.0, location=(0.0, 0.0, z - 0.015))
    floor = bpy.context.object
    material = bpy.data.materials.new("Audit Floor")
    material.diffuse_color = (0.035, 0.025, 0.022, 1.0)
    material.metallic = 0.0
    material.roughness = 0.62
    floor.data.materials.append(material)


def add_area(name, location, energy, size, color):
    data = bpy.data.lights.new(name, "AREA")
    data.energy = energy
    data.shape = "DISK"
    data.size = size
    data.color = color
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    direction = Vector((0.0, 0.0, 0.55)) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def setup_camera(minimum, maximum):
    center = (minimum + maximum) * 0.5
    size = maximum - minimum
    distance = max(size.x, size.z) * 2.05 + size.y * 0.55
    camera_data = bpy.data.cameras.new("Audit Camera")
    camera_data.lens = 58
    camera = bpy.data.objects.new("Audit Camera", camera_data)
    bpy.context.collection.objects.link(camera)
    camera.location = (center.x + distance * 0.72, center.y - distance, center.z + distance * 0.48)
    camera.rotation_euler = (center - camera.location).to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.camera = camera


def configure_render(output):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 768
    scene.render.resolution_y = 768
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.render.filepath = output
    scene.render.image_settings.color_mode = "RGBA"
    try:
        scene.view_settings.look = "AgX - Medium High Contrast"
    except TypeError:
        scene.view_settings.look = "Medium High Contrast"
    scene.world.color = (0.012, 0.009, 0.008)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    if len(argv) != 2:
        raise SystemExit("usage: blender -b --python render_model_preview.py -- MODEL OUTPUT")
    model, output = map(os.path.abspath, argv)
    os.makedirs(os.path.dirname(output), exist_ok=True)
    reset_scene()
    import_model(model)
    meshes = visible_meshes()
    if not meshes:
        raise RuntimeError(f"No renderable meshes found in {model}")
    minimum, maximum = normalize(meshes)
    add_floor(minimum.z)
    add_area("Warm Key", (3.5, -4.0, 5.5), 1050, 4.0, (1.0, 0.62, 0.36))
    add_area("Soft Fill", (-4.0, -2.0, 3.2), 760, 4.5, (0.48, 0.68, 1.0))
    add_area("Golden Rim", (1.5, 4.0, 4.5), 920, 3.2, (1.0, 0.37, 0.12))
    setup_camera(minimum, maximum)
    configure_render(output)
    bpy.ops.render.render(write_still=True)
    vertices = sum(len(obj.data.vertices) for obj in meshes)
    polygons = sum(len(obj.data.polygons) for obj in meshes)
    print(f"AUDIT_RENDER_OK model={model} output={output} meshes={len(meshes)} vertices={vertices} polygons={polygons}")


if __name__ == "__main__":
    main()
