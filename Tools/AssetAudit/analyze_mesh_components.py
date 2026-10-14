import json
import os
import sys

import bpy
from mathutils import Vector


def reset_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)


def import_model(path):
    suffix = os.path.splitext(path)[1].lower()
    if suffix in {".glb", ".gltf"}:
        bpy.ops.import_scene.gltf(filepath=path)
    elif suffix == ".fbx":
        bpy.ops.import_scene.fbx(filepath=path)
    else:
        raise ValueError(f"Unsupported model format: {suffix}")


class UnionFind:
    def __init__(self, size):
        self.parent = list(range(size))
        self.rank = [0] * size

    def find(self, value):
        while self.parent[value] != value:
            self.parent[value] = self.parent[self.parent[value]]
            value = self.parent[value]
        return value

    def union(self, left, right):
        left_root = self.find(left)
        right_root = self.find(right)
        if left_root == right_root:
            return
        if self.rank[left_root] < self.rank[right_root]:
            left_root, right_root = right_root, left_root
        self.parent[right_root] = left_root
        if self.rank[left_root] == self.rank[right_root]:
            self.rank[left_root] += 1


def analyze_object(obj):
    mesh = obj.data
    union = UnionFind(len(mesh.vertices))
    for edge in mesh.edges:
        union.union(edge.vertices[0], edge.vertices[1])
    raw_roots = {union.find(vertex.index) for vertex in mesh.vertices}

    # glTF/FBX exporters may duplicate vertices along every triangle, UV seam, or
    # hard-normal boundary.  Weld index connectivity virtually by local-space
    # position before judging whether the asset contains useful object islands.
    extent = max((obj.dimensions.x, obj.dimensions.y, obj.dimensions.z, 1.0))
    tolerance = extent * 0.00001
    coincident = {}
    for vertex in mesh.vertices:
        key = tuple(round(value / tolerance) for value in vertex.co)
        if key in coincident:
            union.union(vertex.index, coincident[key])
        else:
            coincident[key] = vertex.index

    components = {}
    for vertex in mesh.vertices:
        root = union.find(vertex.index)
        component = components.setdefault(
            root,
            {
                "vertex_indices": [],
                "polygon_count": 0,
                "minimum": Vector((float("inf"),) * 3),
                "maximum": Vector((float("-inf"),) * 3),
            },
        )
        component["vertex_indices"].append(vertex.index)
        world = obj.matrix_world @ vertex.co
        component["minimum"].x = min(component["minimum"].x, world.x)
        component["minimum"].y = min(component["minimum"].y, world.y)
        component["minimum"].z = min(component["minimum"].z, world.z)
        component["maximum"].x = max(component["maximum"].x, world.x)
        component["maximum"].y = max(component["maximum"].y, world.y)
        component["maximum"].z = max(component["maximum"].z, world.z)

    for polygon in mesh.polygons:
        if not polygon.vertices:
            continue
        root = union.find(polygon.vertices[0])
        components[root]["polygon_count"] += 1

    records = []
    for component in components.values():
        size = component["maximum"] - component["minimum"]
        records.append(
            {
                "vertices": len(component["vertex_indices"]),
                "polygons": component["polygon_count"],
                "minimum": [round(value, 6) for value in component["minimum"]],
                "maximum": [round(value, 6) for value in component["maximum"]],
                "size": [round(value, 6) for value in size],
            }
        )
    records.sort(key=lambda item: (item["polygons"], item["vertices"]), reverse=True)
    return {
        "name": obj.name,
        "vertices": len(mesh.vertices),
        "polygons": len(mesh.polygons),
        "materials": len(mesh.materials),
        "raw_index_connected_components": len(raw_roots),
        "virtual_weld_tolerance": tolerance,
        "connected_components": len(records),
        "components": records[:256],
        "components_truncated": len(records) > 256,
    }


def main():
    argv = sys.argv[sys.argv.index("--") + 1 :]
    if len(argv) != 2:
        raise SystemExit("usage: blender -b --python analyze_mesh_components.py -- MODEL OUTPUT_JSON")
    model, output = map(os.path.abspath, argv)
    os.makedirs(os.path.dirname(output), exist_ok=True)
    reset_scene()
    import_model(model)
    objects = [obj for obj in bpy.context.scene.objects if obj.type == "MESH" and not obj.hide_render]
    if not objects:
        raise RuntimeError(f"No renderable meshes found in {model}")
    analyses = [analyze_object(obj) for obj in objects]
    result = {
        "model": model,
        "mesh_objects": len(analyses),
        "vertices": sum(item["vertices"] for item in analyses),
        "polygons": sum(item["polygons"] for item in analyses),
        "raw_index_connected_components": sum(
            item["raw_index_connected_components"] for item in analyses
        ),
        "connected_components": sum(item["connected_components"] for item in analyses),
        "objects": analyses,
    }
    with open(output, "w", encoding="utf-8") as stream:
        json.dump(result, stream, indent=2, ensure_ascii=False)
    print(
        "COMPONENT_AUDIT_OK "
        f"model={model} output={output} mesh_objects={result['mesh_objects']} "
        f"vertices={result['vertices']} polygons={result['polygons']} "
        f"connected_components={result['connected_components']}"
    )


if __name__ == "__main__":
    main()
