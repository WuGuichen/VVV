"""Background regression: each component pass reads the Custom mask once."""
import importlib.util
from pathlib import Path
from types import SimpleNamespace

import bpy


def main():
    spec = importlib.util.spec_from_file_location('base_scan_test', Path(__file__).with_name('__init__.py'))
    a = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(a)
    mesh = bpy.data.meshes.new('ManyComponents')
    mesh.from_pydata([(i*.001,0,0) for i in range(200)], [], [])
    obj = bpy.data.objects.new('ManyComponents',mesh)
    bpy.context.collection.objects.link(obj)
    obj.vertex_groups.new(name='head.x').add(list(range(200)),1,'REPLACE')
    data = bpy.data.armatures.new('rig')
    rig = bpy.data.objects.new('rig',data)
    bpy.context.collection.objects.link(rig)
    a._select_only(bpy.context,rig)
    bpy.ops.object.mode_set(mode='EDIT')
    bone=data.edit_bones.new('head.x')
    bone.head,bone.tail=(0,0,-.1),(0,0,.1)
    bpy.ops.object.mode_set(mode='OBJECT')
    components=[{'object':obj.name,'vertex_indices':[i],'binding_scope':'BASE_SKIN'} for i in range(200)]
    a._unrigged_components=lambda *_args:components
    reads=[]
    def mask(*_args):
        reads.append(1)
        return {}
    a._custom_mask_vertex_ids_by_object=mask
    a._base_region_assignments_by_object=lambda *_args:{}
    settings=SimpleNamespace()
    assert a._existing_rigid_head_component_indices(bpy.context,settings,rig)==list(range(200))
    assert len(reads)==1, 'head adoption must not rescan the entire mask for each island'
    reads.clear()
    a._base_region_assignments_by_object=lambda *_args:{obj.name:{i:'HEAD' for i in range(200)}}
    assert a._head_override_mismatch_count(bpy.context,settings,rig)==(200,0)
    assert len(reads)==1, 'HEAD validation must share its mask snapshot across components'
    reads.clear()
    a._base_region_assignments_by_object=lambda *_args:{}
    assert a._automatic_head_boundary_stats(bpy.context,settings,rig)==(200,0)
    assert len(reads)==1, 'automatic HEAD validation must share its mask snapshot'
    print('WG_BASE_SCAN_BUDGET_OK 200_components one_mask_read_per_pass unchanged_results')


if __name__=='__main__':
    main()
