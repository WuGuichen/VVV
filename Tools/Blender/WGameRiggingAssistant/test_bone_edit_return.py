"""Blender background regression for temporary bone editing."""
import importlib.util
from pathlib import Path

import bpy
import bmesh


def main():
    spec = importlib.util.spec_from_file_location('mode_return_test', Path(__file__).with_name('__init__.py'))
    addon = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(addon)
    addon.register()
    try:
        armature = bpy.data.armatures.new('rig')
        rig = bpy.data.objects.new('rig', armature)
        bpy.context.collection.objects.link(rig)
        rig['wgame_rig_kind'] = 'deform_skeleton'
        addon._select_only(bpy.context, rig)
        bpy.ops.object.mode_set(mode='EDIT')
        bone = armature.edit_bones.new('upper_chest.x')
        bone.head, bone.tail = (0, 0, 0), (0, 0, 1)
        bpy.ops.object.mode_set(mode='OBJECT')
        mesh = bpy.data.meshes.new('Body')
        mesh.from_pydata([(0,0,0),(1,0,0),(0,1,0)], [], [(0,1,2)])
        obj = bpy.data.objects.new('Body', mesh)
        bpy.context.collection.objects.link(obj)
        group = obj.vertex_groups.new(name='upper_chest.x')
        group.add([0,1,2], 1.0, 'REPLACE')
        addon._select_only(bpy.context, obj)
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.mesh.select_all(action='DESELECT')
        bm = bmesh.from_edit_mesh(mesh)
        bm.verts.ensure_lookup_table()
        for v in bm.verts:
            v.select_set(v.index == 1)
        bmesh.update_edit_mesh(mesh)
        assert bpy.ops.wgame_rigging.toggle_bone_edit() == {'FINISHED'}
        assert bpy.context.object == rig and rig.mode == 'EDIT'
        assert bpy.ops.wgame_rigging.toggle_bone_edit() == {'FINISHED'}
        assert bpy.context.object == obj and obj.mode == 'EDIT'
        assert {i for i,v in enumerate(bmesh.from_edit_mesh(mesh).verts) if v.select} == {1}
        bpy.ops.object.mode_set(mode='OBJECT')
        # Pose rig plus active weight-painted mesh is a common combined context.
        rig.select_set(True)
        bpy.context.view_layer.objects.active = rig
        bpy.ops.object.mode_set(mode='POSE')
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.mode_set(mode='WEIGHT_PAINT')
        settings = bpy.context.scene.wgame_rigging_assistant
        settings.wgame_custom_weight_edit_active = True
        assert bpy.ops.wgame_rigging.toggle_bone_edit() == {'FINISHED'}
        assert not settings.wgame_custom_weight_edit_active
        assert bpy.ops.wgame_rigging.toggle_bone_edit() == {'FINISHED'}
        assert obj.mode == 'WEIGHT_PAINT' and rig.mode == 'POSE'
        assert settings.wgame_custom_weight_edit_active
        assert obj.vertex_groups.active.name == group.name
        assert bpy.context.view_layer.objects.active == obj
        assert bpy.ops.wgame_rigging.toggle_bone_edit() == {'FINISHED'}
        rig.data.edit_bones['upper_chest.x'].tail.z += .1
        settings.wgame_skeleton_accepted = True
        settings.wgame_base_skin_complete = True
        assert bpy.ops.wgame_rigging.toggle_bone_edit() == {'FINISHED'}
        assert not settings.wgame_skeleton_accepted and not settings.wgame_base_skin_complete
        assert obj.mode == 'WEIGHT_PAINT'
        assert not addon._BONE_EDIT_RETURN
        print('WG_BONE_EDIT_RETURN_OK mesh_selection weight_paint pose_context active_group invalidation')
    finally:
        if bpy.context.object and bpy.context.object.mode != 'OBJECT':
            bpy.ops.object.mode_set(mode='OBJECT')
        addon.unregister()


if __name__ == '__main__':
    main()
