"""Background checks for cross-island, bounded, non-cascading weight repair."""
import importlib.util
from pathlib import Path

import bpy


def main():
    spec = importlib.util.spec_from_file_location('spatial_zero_test', Path(__file__).with_name('__init__.py'))
    a = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(a)
    a.register()
    try:
        data = bpy.data.armatures.new('rig')
        rig = bpy.data.objects.new('rig',data)
        bpy.context.collection.objects.link(rig)
        rig['wgame_rig_kind']='deform_skeleton'
        a._select_only(bpy.context,rig)
        bpy.ops.object.mode_set(mode='EDIT')
        for i,name in enumerate(('pelvis.x','extra_a','extra_b')):
            bone=data.edit_bones.new(name)
            bone.head,bone.tail=(0,0,i),(0,0,i+1)
        bpy.ops.object.mode_set(mode='OBJECT')
        mesh=bpy.data.meshes.new('Pieces')
        mesh.from_pydata([(0,0,0),(.024,0,0),(.025,0,0),(.15,0,0),(.02,0,0),(.026,0,0)],[(2,3)],[])
        obj=bpy.data.objects.new('Pieces',mesh)
        bpy.context.collection.objects.link(obj)
        obj.vertex_groups.new(name='extra_a').add([0],.75,'REPLACE')
        obj.vertex_groups.new(name='extra_b').add([0],.25,'REPLACE')
        obj.vertex_groups.new(name='pelvis.x').add([1],1,'REPLACE')
        obj.vertex_groups['extra_a'].add([5],.5,'REPLACE')
        obj.vertex_groups.new(name=a.RIGID_BIND_MASK_GROUP).add([4],1,'REPLACE')
        a._scope_vertex_ids_by_object=lambda *_args:{obj.name:set(range(6))}
        before=[[(g.group,g.weight) for g in v.groups] for v in mesh.vertices]
        plan,stats=a._build_spatial_zero_weight_plan(bpy.context,bpy.context.scene.wgame_rigging_assistant,rig,.03)
        assert len(plan)==1 and plan[0][1]==2
        assert dict(plan[0][2])=={'extra_a':.75,'extra_b':.25}
        assert stats['zero']==3 and stats['too_far']==1 and stats['rigid_skipped']==1
        assert before==[[(g.group,g.weight) for g in v.groups] for v in mesh.vertices]
        a._custom_mask_vertex_ids_by_object=lambda *_args:{obj.name:set(range(6))}
        a._unrigged_components=lambda *_args:[{'object':obj.name, 'vertex_indices':[2,3]}]
        a._component_vertex_ids_for_scope=lambda *_args:{2,3}
        combined,combined_stats=a._build_zero_weight_repair_plan(bpy.context,bpy.context.scene.wgame_rigging_assistant,rig,.03)
        assert {i for _,i,_ in combined}=={2,3}
        assert combined_stats['spatial_filled']==1 and combined_stats['topology_filled']==1
        assert before==[[(g.group,g.weight) for g in v.groups] for v in mesh.vertices], 'combined planning must not write intermediate weights'
        a._apply_deform_weight_plan(bpy.context,rig,plan)
        for i in (0,1,3,4,5):
            assert before[i]==[(g.group,g.weight) for g in mesh.vertices[i].groups]
        assert abs(sum(g.weight for g in mesh.vertices[2].groups)-1)<1e-6
        print('WG_SPATIAL_ZERO_OK disconnected radius no_cascade custom_donors rigid_guard existing_preserved')
    finally:
        a.unregister()


if __name__=='__main__':
    main()
