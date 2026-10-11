"""Run in Blender --background --factory-startup --python-exit-code 1."""
import importlib.util
from pathlib import Path

import bpy


def main():
    spec = importlib.util.spec_from_file_location('detached_child_test', Path(__file__).with_name('__init__.py'))
    addon = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(addon)
    addon.register()
    try:
        data = bpy.data.armatures.new('rig')
        rig = bpy.data.objects.new('rig', data)
        bpy.context.collection.objects.link(rig)
        rig['wgame_rig_kind'] = 'deform_skeleton'
        addon._select_only(bpy.context, rig)
        assert not bpy.ops.wgame_rigging.add_detached_child.poll()
        bpy.ops.object.mode_set(mode='EDIT')
        parent = data.edit_bones.new('upper_chest.x')
        parent.head, parent.tail = (0, 0, 1), (0, 0, 1.2)
        parent.roll = .3
        collection = data.collections.new('VisibleBones')
        collection.assign(parent)
        data.edit_bones.active = parent
        parent.select = True
        before = (tuple(parent.head), tuple(parent.tail), parent.roll, parent.parent, parent.use_connect)
        assert bpy.ops.wgame_rigging.add_detached_child() == {'FINISHED'}
        child = data.edit_bones.active
        first_name = child.name
        assert child.parent == parent and not child.use_connect and child.use_deform
        assert child.length > 0 and (child.head-parent.tail).length > 0
        assert child.select and not parent.select
        assert collection.name in child.collections
        assert (tuple(parent.head), tuple(parent.tail), parent.roll, parent.parent, parent.use_connect) == before
        child.head.x += .1
        child.tail.x += .1
        assert tuple(parent.head) == before[0] and tuple(parent.tail) == before[1]
        data.edit_bones.active = parent
        parent.select = True
        assert bpy.ops.wgame_rigging.add_detached_child() == {'FINISHED'}
        assert data.edit_bones.active.name != first_name and len(data.edit_bones) == 3
        bpy.ops.object.mode_set(mode='OBJECT')
        assert not bpy.ops.wgame_rigging.add_detached_child.poll()
        print('WG_DETACHED_CHILD_OK parenting disconnected independent_move unique_name collection selection')
    finally:
        addon.unregister()


if __name__ == '__main__':
    main()
