"""Blender background tests for overlay picking and selection routing."""
import importlib.util
from pathlib import Path
from types import SimpleNamespace

import bpy
from mathutils import Vector


def main():
    spec = importlib.util.spec_from_file_location("joint_pick_test", Path(__file__).with_name("__init__.py"))
    addon = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(addon)
    addon.register()
    project = addon.view3d_utils.location_3d_to_region_2d
    try:
        collection = bpy.data.collections.new('WGame_RigGuides')
        bpy.context.scene.collection.children.link(collection)
        for name, x in [('wrist.l', 1), ('wrist.r', -1), ('pelvis.x', 0)]:
            obj = bpy.data.objects.new(addon._joint_guide_object_name(name), None)
            collection.objects.link(obj)
            obj.location = (x, 0, 0)
        bpy.context.view_layer.update()
        addon.view3d_utils.location_3d_to_region_2d = lambda r, s, p: Vector((400+p.x*100, 300))
        context = SimpleNamespace(scene=bpy.context.scene, view_layer=bpy.context.view_layer,
                                  object=None, region=SimpleNamespace(type='WINDOW'),
                                  space_data=SimpleNamespace(region_3d=None),
                                  area=SimpleNamespace(type='VIEW_3D'), mode='OBJECT')
        settings = context.scene.wgame_rigging_assistant
        hit = addon._joint_guide_hit_test
        assert hit(context, (500, 300)) == 'wrist.l'
        assert hit(context, (526, 298)) == 'wrist.l', 'label is clickable'
        assert hit(context, (300, 300)) == 'wrist.r'
        assert hit(context, (900, 700)) is None
        op = addon.WGAME_RIGGING_OT_pick_joint_guide
        assert op.poll(context)
        settings.unrigged_joint_mirror = True
        event = SimpleNamespace(mouse_region_x=300, mouse_region_y=300)
        assert op.invoke(None, context, event) == {'FINISHED'}
        assert bpy.context.view_layer.objects.active == addon._joint_guide_object('wrist.l')
        assert addon.JOINT_GUIDE_DEFINITIONS[settings.unrigged_joint_guide_index][0] == 'wrist.l'
        settings.unrigged_joint_mirror = False
        assert op.invoke(None, context, event) == {'FINISHED'}
        assert bpy.context.view_layer.objects.active == addon._joint_guide_object('wrist.r')
        assert op.invoke(None, context, SimpleNamespace(mouse_region_x=900, mouse_region_y=700)) == {'PASS_THROUGH'}
        assert bpy.context.view_layer.objects.active == addon._joint_guide_object('wrist.r')
        addon._joint_guide_object('wrist.r').hide_set(True)
        assert hit(context, (300, 300)) is None
        context.mode = 'EDIT_MESH'
        assert not op.poll(context)
        context.mode = 'OBJECT'
        collection.hide_select = True
        assert not op.poll(context)
        addon._register_joint_guide_keymap()
        assert len(addon._JOINT_GUIDE_KEYMAPS) <= 1, 'keymap installation is idempotent'
    finally:
        addon.view3d_utils.location_3d_to_region_2d = project
        addon.unregister()
    assert not addon._JOINT_GUIDE_KEYMAPS
    print('WG_JOINT_PICK_OK circle label mirror independent_side pass_through hidden mode keymap_cleanup')


if __name__ == '__main__':
    main()
