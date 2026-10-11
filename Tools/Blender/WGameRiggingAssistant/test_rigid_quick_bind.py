"""Background regression: apply rigid rules immediately without rebuilding other weights."""
import importlib.util
from pathlib import Path
import tempfile
from types import SimpleNamespace, MethodType

import bpy


def main():
    spec = importlib.util.spec_from_file_location('rigid_quick_test', Path(__file__).with_name('__init__.py'))
    a = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(a)
    a.register()
    try:
        armature = bpy.data.armatures.new('rig')
        rig = bpy.data.objects.new('rig', armature)
        bpy.context.collection.objects.link(rig)
        rig['wgame_rig_kind'] = 'deform_skeleton'
        a._select_only(bpy.context, rig)
        bpy.ops.object.mode_set(mode='EDIT')
        for i, name in enumerate(('chain_1','chain_2')):
            bone = armature.edit_bones.new(name)
            bone.head, bone.tail = (0,0,i), (0,0,i+1)
        bpy.ops.object.mode_set(mode='OBJECT')
        mesh = bpy.data.meshes.new('Attachments')
        mesh.from_pydata([(0,0,0),(1,0,0),(0,1,0),(2,0,0),(3,0,0),(2,1,0)], [], [(0,1,2),(3,4,5)])
        obj = bpy.data.objects.new('Attachments', mesh)
        bpy.context.collection.objects.link(obj)
        for name, weight in (('chain_1',.3),('chain_2',.7),('author_mask',.5)):
            obj.vertex_groups.new(name=name).add(list(range(6)),weight,'REPLACE')
        settings = bpy.context.scene.wgame_rigging_assistant
        island = {'object':obj.name,'mesh':obj,'component_id':'fixture','vertex_ids':[0,1,2],'vertex_set':{0,1,2}}
        a._custom_weight_subislands = lambda *_args: [island]
        a._set_focus_marker = lambda *_args, **_kwargs: None
        def assignments(i):
            return {obj.vertex_groups[g.group].name:g.weight for g in mesh.vertices[i].groups}
        outside = [assignments(i) for i in (3,4,5)]
        with tempfile.TemporaryDirectory() as directory:
            backup = Path(directory)/'backup.blend'
            bpy.ops.wm.save_as_mainfile(filepath=str(backup))
            a._save_backup_copy = lambda: str(backup)
            assert a._rigid_island_at_face(bpy.context, settings, obj, 0)==island
            try:
                a._rigid_island_at_face(bpy.context, settings, obj, 1)
                raise AssertionError('outside Custom face must not be picked')
            except ValueError:
                pass
            a._apply_rigid_attachment_now(bpy.context,settings,rig,island,'chain_1','fixture')
            for i in (0,1,2):
                values=assignments(i)
                assert values['chain_1']==1 and 'chain_2' not in values
                assert values['author_mask']==.5 and values[a.RIGID_BIND_MASK_GROUP]==1
            assert outside==[assignments(i) for i in (3,4,5)]
            overview = a._rigid_marks_overview(bpy.context)
            assert overview['count']==1 and overview['vertices']==3
            a._RIGID_PICK_FEEDBACK.clear()
            assert a._rigid_marks_overview(bpy.context)['count']==1, 'clearing hover must not clear marked regions'
            assert bpy.ops.wgame_rigging.toggle_rigid_marks()=={'FINISHED'}
            assert bpy.context.scene['wgame_show_rigid_marks'] is False
            assert a._rigid_marks_overview(bpy.context)['count']==1
            assert bpy.ops.wgame_rigging.toggle_rigid_marks()=={'FINISHED'}
            assert bpy.context.scene['wgame_show_rigid_marks'] is True
            a._apply_rigid_attachment_now(bpy.context,settings,rig,island,'chain_2','manual')
            assert len(a._rigid_bind_payload(rig)['attachments'])==1
            assert assignments(0)['chain_2']==1 and 'chain_1' not in assignments(0)
            before=[assignments(i) for i in range(6)]
            payload=rig.get(a.RIGID_BIND_ATTACHMENTS_KEY)
            original_store=a._store_rigid_attachment
            a._store_rigid_attachment=lambda *_args: (_ for _ in ()).throw(RuntimeError('injected record failure'))
            try:
                a._apply_rigid_attachment_now(bpy.context,settings,rig,island,'chain_1','fixture')
                raise AssertionError('record failure expected')
            except RuntimeError:
                pass
            finally:
                a._store_rigid_attachment=original_store
            assert before==[assignments(i) for i in range(6)]
            assert payload==rig.get(a.RIGID_BIND_ATTACHMENTS_KEY)
            a._remove_rigid_attachment_mark(bpy.context,settings,rig,island)
            assert not a._rigid_bind_payload(rig)['attachments']
            assert a._rigid_marks_overview(bpy.context)['count']==0
            for i in range(6):
                expected={name:value for name,value in before[i].items() if name!=a.RIGID_BIND_MASK_GROUP}
                actual={name:value for name,value in assignments(i).items() if name!=a.RIGID_BIND_MASK_GROUP}
                assert expected==actual, 'unmark must preserve deform weights and other masks'
            # Exercise the interaction contract without writing additional weights.
            island['id'] = 'fixture'
            calls = []
            finished = []
            marked = [False]
            def commit(*_args):
                marked[0]=True
                calls.append('commit')
            def remove(*_args):
                marked[0]=False
                calls.append('remove')
            op = SimpleNamespace(_hover_key=None, _islands=[island],
                                 _apply=commit,
                                 _finish=lambda *_args: finished.append('exit'), report=lambda *_args: None)
            op.modal = MethodType(a.WGAME_RIGGING_OT_quick_bind_rigid.modal, op)
            op._preview = MethodType(a.WGAME_RIGGING_OT_quick_bind_rigid._preview, op)
            a._rigid_attachment_record_for_island=lambda *_args: {'target_bone':'chain_1'} if marked[0] else None
            a._remove_rigid_attachment_mark=remove
            region = SimpleNamespace(x=0, y=0)
            area = SimpleNamespace(spaces=SimpleNamespace(active=SimpleNamespace(region_3d=None)))
            a._view3d_window_region_from_event = lambda *_args: (area, region)
            a.view3d_utils = SimpleNamespace(region_2d_to_origin_3d=lambda *_args: (0,0,0),
                                            region_2d_to_vector_3d=lambda *_args: (0,0,1))
            a._rigid_island_at_face = lambda *_args: island
            a._rigid_attachment_auto_target = lambda *_args: ('chain_1', 0, 'fixture')
            a._set_rigid_pick_feedback = lambda *_args: calls.append('hover')
            ctx = SimpleNamespace(scene=SimpleNamespace(wgame_rigging_assistant=settings,
                                  ray_cast=lambda *_args: (True,None,None,0,obj,None)),
                                  evaluated_depsgraph_get=lambda: None)
            a._wgame_rig_object = lambda *_args: rig
            a._RIGID_PICK_FEEDBACK.update(active=True, stop=False)
            def event(kind):
                return SimpleNamespace(type=kind, value='PRESS', mouse_x=20, mouse_y=20)
            assert op.modal(ctx,event('MOUSEMOVE'))=={'RUNNING_MODAL'} and calls==['hover']
            assert op.modal(ctx,event('LEFTMOUSE'))=={'RUNNING_MODAL'}
            assert op.modal(ctx,event('LEFTMOUSE'))=={'RUNNING_MODAL'}
            assert calls.count('commit')==1 and calls.count('remove')==1 and not marked[0] and not finished
            op._apply=lambda *_args: (_ for _ in ()).throw(ValueError('bad target'))
            assert op.modal(ctx,event('LEFTMOUSE'))=={'RUNNING_MODAL'}
            assert a._RIGID_PICK_FEEDBACK['message']=='bad target' and a._RIGID_PICK_FEEDBACK['error']
            assert op.modal(ctx,event('ESC'))=={'FINISHED'} and finished==['exit']
            print('WG_RIGID_QUICK_BIND_OK immediate_weights local_scope nondeform_preserved rebind rollback face_scope')
    finally:
        a.unregister()


if __name__=='__main__':
    main()
