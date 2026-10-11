"""Blender regression for publish cleanup and attachment index remapping."""
import importlib.util
import json
from pathlib import Path

import bpy


def main():
    spec = importlib.util.spec_from_file_location("cleanup_test", Path(__file__).with_name("__init__.py"))
    addon = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(addon)
    addon.register()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    rig = bpy.data.objects.new("Rig", bpy.data.armatures.new("Rig"))
    bpy.context.collection.objects.link(rig)
    rig["wgame_rig_kind"] = "deform_skeleton"
    mesh = bpy.data.meshes.new("CleanupMesh")
    mesh.from_pydata([(9, 0, 0), (0, 0, 0), (9, 1, 0), (1, 0, 0), (0, 1, 0), (9, 2, 0)], [(0, 2)], [(1, 3, 4)])
    obj = bpy.data.objects.new("Body", mesh)
    bpy.context.collection.objects.link(obj)
    obj.modifiers.new("Armature", "ARMATURE").object = rig
    obj.vertex_groups.new(name="head.x").add([1, 3, 4], 0.75, "REPLACE")
    uv = mesh.uv_layers.new(name="PaletteUV")
    color = mesh.color_attributes.new(name="PaletteData", domain="CORNER", type="FLOAT_COLOR")
    for index in range(3):
        uv.data[index].uv = (0.25, 0.5)
        color.data[index].color = (0.1, 0.2, 0.3, 0.5)
    copy, mapping, count = addon._clean_faceless_mesh_copy(mesh)
    assert count == 3 and mapping == {1: 0, 3: 1, 4: 2}, (count, mapping)
    assert len(mesh.vertices) == 6 and len(copy.vertices) == 3 and len(copy.polygons) == 1
    assert all(abs(vertex.groups[0].weight - 0.75) < 1e-6 for vertex in copy.vertices)
    assert all(tuple(item.uv) == (0.25, 0.5) for item in copy.uv_layers["PaletteUV"].data)
    assert all(abs(item.color[3] - 0.5) < 1e-6 for item in copy.color_attributes["PaletteData"].data)
    assert addon._clean_faceless_mesh_copy(copy) == (None, {}, 0)
    bpy.data.meshes.remove(copy)
    settings = bpy.context.scene.wgame_rigging_assistant
    components = addon._scan_unrigged_components(bpy.context)
    for component in components:
        component["binding_scope"] = "BASE_SKIN"
    settings.unrigged_components_json = json.dumps(components)
    settings.unrigged_component_scan_fingerprint = addon._unrigged_topology_fingerprint(bpy.context)
    addon._save_rigid_bind_payload(rig, {"attachments": [{"object": obj.name, "vertex_indices": [1, 3, 4], "target_bone": "head.x", "id": "old"}]})
    result = bpy.ops.wgame_rigging.clean_publish_faceless_vertices()
    assert result == {"FINISHED"}, result
    assert len(obj.data.vertices) == 3
    record = addon._rigid_bind_payload(rig)["attachments"][0]
    assert record["vertex_indices"] == [0, 1, 2], record
    assert record["id"] == addon._rigid_bind_island_id(obj.name, [0, 1, 2])
    assert Path(settings.backup_filepath).is_file()
    assert addon._component_scan_is_current(bpy.context, settings)
    assert all(index < 3 for component in addon._unrigged_components(settings) for index in component["vertex_indices"])
    assert not settings.wgame_component_skin_complete
    print("Faceless cleanup: PASS (isolated and wire vertices, face/UV/RGBA/weights preserved, backup, record remap)")


if __name__ == "__main__":
    main()
