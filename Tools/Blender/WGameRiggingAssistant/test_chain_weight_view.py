"""Blender background checks for read-only whole-chain aggregation."""
import importlib.util
from pathlib import Path

import bpy


def main():
    spec = importlib.util.spec_from_file_location('chain_view_test', Path(__file__).with_name('__init__.py'))
    a = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(a)
    a.register()
    try:
        data = bpy.data.armatures.new('rig')
        rig = bpy.data.objects.new('rig', data)
        bpy.context.collection.objects.link(rig)
        rig['wgame_rig_kind'] = 'deform_skeleton'
        a._select_only(bpy.context, rig)
        bpy.ops.object.mode_set(mode='EDIT')
        for i, name in enumerate(('chain_1', 'chain_2', 'unused')):
            bone = data.edit_bones.new(name)
            bone.head, bone.tail = (0,0,i), (0,0,i+1)
        bpy.ops.object.mode_set(mode='OBJECT')
        mesh = bpy.data.meshes.new('Body')
        mesh.from_pydata([(0,0,0),(1,0,0),(0,1,0)], [], [(0,1,2)])
        obj = bpy.data.objects.new('Body', mesh)
        bpy.context.collection.objects.link(obj)
        for name, weights in [('chain_1',(.6,.7,0)), ('chain_2',(.3,.6,0)), ('other',(.1,0,1))]:
            group = obj.vertex_groups.new(name=name)
            for i, value in enumerate(weights):
                group.add([i], value, 'REPLACE')
        names = ['chain_1','chain_2','unused']
        values, stats, invalid = a._chain_weight_values(obj, names)
        assert abs(values[0]-.9)<1e-6 and abs(values[1]-1.3)<1e-6 and values[2]==0
        assert stats['chain_1']['vertices']==2 and stats['unused']['vertices']==0 and invalid==0
        assert a._chain_weight_color(0)[3]==0
        assert a._chain_weight_color(1)==(1,0,0,.55)
        a._participant_meshes = lambda *_args: [obj]
        a._custom_weight_chain_status = lambda *_args: {'scanned':True,'stale':False}
        a._current_custom_weight_chain = lambda *_args: {'root_bone':'chain_1','bones':names}
        a._select_only(bpy.context, obj)
        bpy.ops.object.mode_set(mode='WEIGHT_PAINT')
        before = [[(g.group,g.weight) for g in v.groups] for v in mesh.vertices]
        group_names = list(obj.vertex_groups.keys())
        assert bpy.ops.wgame_rigging.preview_chain_total()=={'FINISHED'}
        assert a._CHAIN_WEIGHT_VIEW['covered']==2 and a._CHAIN_WEIGHT_VIEW['overflow']==1
        assert len(a._CHAIN_WEIGHT_VIEW['stats'])==3
        assert bpy.context.mode=='OBJECT'
        assert bpy.ops.wgame_rigging.preview_chain_total(action='CLOSE')=={'FINISHED'}
        assert obj.mode=='WEIGHT_PAINT' and bpy.context.object==obj
        assert before==[[(g.group,g.weight) for g in v.groups] for v in mesh.vertices]
        assert group_names==list(obj.vertex_groups.keys())
        print('WG_CHAIN_WEIGHT_VIEW_OK sums per_bone overflow zero_bone no_weight_changes mode_return')
    finally:
        a.unregister()


if __name__ == '__main__':
    main()
