"""Run with Blender --background --factory-startup --python-exit-code 17."""

from pathlib import Path
import runpy
import sys
import tempfile

import bpy


FOLDER = Path(__file__).resolve().parent
CHECKS = runpy.run_path(str(FOLDER / "palette_export_preflight.py"))
EXPORT = runpy.run_path(str(FOLDER / "export_unity_fbx.py"))


def fails(action, *expected):
    try:
        action()
    except RuntimeError as error:
        assert all(value in str(error) for value in expected), str(error)
    else:
        raise AssertionError(f"Expected failure containing {expected}")


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    rig = bpy.data.objects.new("Rig", bpy.data.armatures.new("Rig"))
    bpy.context.collection.objects.link(rig)
    rig["wgame_rig_kind"] = "deform_skeleton"
    objects = []
    for name in ("Body", "part_0000001.001"):
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata([(0, 0, 0), (1, 0, 0), (0, 1, 0)], [], [(0, 1, 2)])
        mesh.uv_layers.new(name="UV0")
        mesh.uv_layers.new(name="PaletteUV")
        mesh.color_attributes.new(name="PaletteData", type="FLOAT_COLOR", domain="CORNER")
        obj = bpy.data.objects.new(name, mesh)
        bpy.context.collection.objects.link(obj)
        obj.modifiers.new("Skin", "ARMATURE").object = rig
        objects.append(obj)
    CHECKS["validate_sources"](objects)
    obj = objects[1]
    original = obj.data

    def reset():
        previous = obj.data
        obj.data = original.copy()
        if previous != original:
            bpy.data.meshes.remove(previous)

    reset()
    obj.data.uv_layers.remove(obj.data.uv_layers["PaletteUV"])
    fails(lambda: CHECKS["validate_sources"](objects), obj.name, "缺少 PaletteUV")
    reset()
    obj.data.color_attributes.remove(obj.data.color_attributes["PaletteData"])
    fails(lambda: CHECKS["validate_sources"](objects), obj.name, "缺少 RGBA PaletteData")
    with tempfile.TemporaryDirectory() as directory:
        target = Path(directory) / "blocked.fbx"
        sys.argv = ["export", "--", str(target), "--require-palette"]
        fails(EXPORT["main"], obj.name, "缺少 RGBA PaletteData")
        assert not target.exists(), "Source failure must precede FBX writes"
    reset()
    obj.data.uv_layers["PaletteUV"].name = "Other"
    obj.data.uv_layers.new(name="PaletteUV")
    fails(lambda: CHECKS["validate_sources"](objects), "位于 UV2", "要求 UV1")
    reset()
    obj.data.vertices.add(1)
    fails(lambda: CHECKS["validate_sources"](objects), "1 个顶点不属于任何面", "[3]")
    reset()
    obj.data.color_attributes["PaletteData"].name = "OtherColor"
    obj.data.color_attributes.new(name="PaletteData", type="FLOAT_COLOR", domain="CORNER")
    fails(lambda: CHECKS["validate_sources"](objects), "首个颜色层", "OtherColor")

    reset()
    modifier = obj.modifiers.new("RemovePalette", "NODES")
    tree = bpy.data.node_groups.new("RemovePalette", "GeometryNodeTree")
    tree.interface.new_socket(name="Geometry", in_out="INPUT", socket_type="NodeSocketGeometry")
    tree.interface.new_socket(name="Geometry", in_out="OUTPUT", socket_type="NodeSocketGeometry")
    input_node = tree.nodes.new("NodeGroupInput")
    output_node = tree.nodes.new("NodeGroupOutput")
    remove = tree.nodes.new("GeometryNodeRemoveAttribute")
    remove.inputs["Name"].default_value = "PaletteData"
    tree.links.new(input_node.outputs["Geometry"], remove.inputs["Geometry"])
    tree.links.new(remove.outputs["Geometry"], output_node.inputs["Geometry"])
    modifier.node_group = tree
    CHECKS["validate_sources"](objects)
    fails(lambda: CHECKS["validate_evaluated"](objects, rig), "修改器求值后", "RemovePalette", "缺少 RGBA PaletteData")
    assert rig.data.pose_position == "POSE"
    assert original.color_attributes.get("PaletteData") is not None
    obj.modifiers.remove(modifier)
    CHECKS["validate_evaluated"](objects, rig)
    print("WGame Palette export preflight: PASS (missing channels, UV order, loose vertices, color order, modifiers, no FBX write)")


if __name__ == "__main__":
    main()
