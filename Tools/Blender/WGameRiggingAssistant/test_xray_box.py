"""Blender --background --factory-startup --python-exit-code 1 --python this_file."""
import importlib.util
import inspect
from pathlib import Path
from types import MethodType, SimpleNamespace

import bpy
import bmesh
from mathutils import Matrix


def main():
    spec = importlib.util.spec_from_file_location("rig_box_test", Path(__file__).with_name("__init__.py"))
    addon = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(addon)
    mesh = bpy.data.meshes.new("BoxLayers")
    mesh.from_pydata([(-.2, -.2, -.4), (-.2, -.2, .4), (.8, .8, 0),
                      (0, 0, 0), (.1, .1, 0), (0, 0, 2)], [], [])
    obj = bpy.data.objects.new("BoxLayers", mesh)
    bpy.context.collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bm = bmesh.from_edit_mesh(mesh)
    bm.verts.ensure_lookup_table()
    bm.verts[3].hide_set(True)
    projection = SimpleNamespace(perspective_matrix=Matrix.Identity(4))
    area = SimpleNamespace(spaces=SimpleNamespace(active=SimpleNamespace(region_3d=projection)))
    region = SimpleNamespace(x=10, y=20, width=100, height=100)
    sample = addon._vertices_in_xray_box
    assert sample(obj, area, region, (30, 30), (60, 60)) == {0, 1, 4}
    assert sample(obj, area, region, (60, 60), (30, 30), {0, 1, 2, 3}) == {0, 1}
    assert not sample(obj, area, region, (-50, -50), (-10, -10))
    # Perspective projection must reject points behind the eye and outside depth clipping.
    projection.perspective_matrix = Matrix(((1, 0, 0, 0), (0, 1, 0, 0), (0, 0, -1.02, -.202), (0, 0, -1, 0)))
    assert sample(obj, area, region, (0, 0), (100, 100)) == {0}
    projection.perspective_matrix = Matrix.Identity(4)

    cls = addon.WGAME_RIGGING_OT_brush_custom_vertices
    brush = SimpleNamespace()
    for name, method in cls.__dict__.items():
        if inspect.isfunction(method):
            setattr(brush, name, MethodType(method, brush))
    brush._mesh_obj = obj
    brush._area = area
    brush._region = region
    brush._allowed_vertex_ids = {0, 1, 2, 3}
    brush.trim_current_component = True
    brush.base_region = ""
    brush._active_ids_cache = set()
    brush._drawing = False
    brush._brush_cache = None
    brush._history = []
    brush._redo_history = []
    brush._update_header = lambda: None
    brush._sync_mask_state = lambda context: None
    brush.report = lambda *args: None
    brush._set_active_mask_vertices({0, 1, 2, 3, 4}, True)

    def event(kind, value="PRESS", x=40, y=50, ctrl=False):
        return SimpleNamespace(type=kind, value=value, mouse_x=x, mouse_y=y, ctrl=ctrl, shift=False)

    def box(ctrl=False):
        brush.modal(bpy.context, event("B"))
        brush.modal(bpy.context, event("LEFTMOUSE", ctrl=ctrl))
        brush.modal(bpy.context, event("LEFTMOUSE", "RELEASE", 70, 80, ctrl))

    box()
    assert brush._active_mask_ids() == {2, 3, 4}
    assert len(brush._history) == 1
    brush._undo_history(bpy.context)
    assert brush._active_mask_ids() == {0, 1, 2, 3, 4}
    brush._redo_history_action(bpy.context)
    assert brush._active_mask_ids() == {2, 3, 4}
    box(ctrl=True)
    assert brush._active_mask_ids() == {0, 1, 2, 3, 4}
    before = brush._mask_snapshot()
    history_count = len(brush._history)
    brush.modal(bpy.context, event("B"))
    brush.modal(bpy.context, event("LEFTMOUSE"))
    brush.modal(bpy.context, event("ESC"))
    assert brush._mask_snapshot() == before and len(brush._history) == history_count
    brush.trim_current_component = False
    box(ctrl=True)
    assert brush._active_mask_ids() == {2, 3}
    box()
    assert brush._active_mask_ids() == {0, 1, 2, 3, 4}
    assert addon._mesh_vertex_group_ids(obj, addon.CUSTOM_BIND_MASK_GROUP) == {0, 1, 2, 3, 4}
    bpy.ops.object.mode_set(mode="OBJECT")
    print("WG_XRAY_BOX_OK projection front_back hidden scope cancel undo redo free_brush")


if __name__ == "__main__":
    main()
