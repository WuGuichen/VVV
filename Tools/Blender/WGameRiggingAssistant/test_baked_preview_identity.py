"""Run in Blender background: preview identity, UI routing and operator guards."""
import hashlib
import importlib.util
from pathlib import Path
import sys
from types import SimpleNamespace
import bpy

root=Path(__file__).resolve().parents[3]


def load(name,path):
    spec=importlib.util.spec_from_file_location(name,path)
    module=importlib.util.module_from_spec(spec);sys.modules[name]=module;spec.loader.exec_module(module)
    return module


rig=load('wgame_rigging_identity_test',Path(__file__).with_name('__init__.py'))
preview=load('wgame_preview_identity_test',root/'Tools/Blender/WGamePaletteTools/baked_preview.py')
source=root/'ArtSource/Characters/000009/Blender_Work/000009_work.blend'
before_hash=hashlib.sha256(source.read_bytes()).hexdigest()
rig.register();preview.register()
try:
    bpy.ops.wm.open_mainfile(filepath=str(source))
    if bpy.context.object and bpy.context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
    scene=bpy.context.scene;settings=scene.wgame_rigging_assistant
    identity={key:getattr(settings,key) for key in ('character_id','character_display_name','repository_root')}
    assert identity['character_id']=='000009'
    assert bpy.ops.wgame_rigging.bootstrap_current_character_source.poll()
    custom_poll_before=rig.WGAME_RIGGING_OT_add_detached_child.poll(bpy.context)
    preview.open_preview(root/'ArtSource/Characters/000009/Final_Source',root)
    assert bpy.context.scene['wgame_baked_preview_package']=='000009'
    assert bpy.context.scene['wgame_baked_preview_display_name']=='C001_1'
    assert Path(bpy.context.scene['wgame_baked_preview_source']).resolve()==source.resolve()
    # Preview identity is read-only metadata, not a fake registered source.
    assert not bpy.context.scene.wgame_rigging_assistant.character_id

    class Layout:
        labels=[];operators=[]
        def box(self):return self
        def label(self,**kw):self.labels.append(kw.get('text',''))
        def operator(self,name,**kw):self.operators.append(name);return SimpleNamespace()

    layout=Layout();rig.WGAME_RIGGING_PT_panel.draw(SimpleNamespace(layout=layout),bpy.context)
    assert layout.operators==['wgame.baked_uv_view'],layout.operators
    assert any('000009' in text and 'C001_1' in text and '烘焙预览' in text for text in layout.labels)
    assert '000009_work.blend' in layout.labels
    assert not any('尚未纳入' in text for text in layout.labels)
    guarded=[cls for cls in rig.classes if issubclass(cls,bpy.types.Operator) and cls.bl_idname!='wgame_rigging.monitor_unity_publish']
    for cls in guarded:assert not cls.poll(bpy.context),cls.bl_idname
    dummy=SimpleNamespace(report=lambda *_:None)
    for cls in (rig.WGAME_RIGGING_OT_bootstrap_current_character_source,
                rig.WGAME_RIGGING_OT_publish_fbx_and_update_unity,
                rig.WGAME_RIGGING_OT_save_versioned_work):
        assert cls.execute(dummy,bpy.context)=={'CANCELLED'}
        wrapped=cls.execute;rig._guard_authoring_operator(cls);assert cls.execute is wrapped
    try:
        bpy.ops.wgame_rigging.bootstrap_current_character_source('INVOKE_DEFAULT')
        raise AssertionError('Preview opened a registration dialog')
    except RuntimeError as error:assert 'poll' in str(error)
    bpy.ops.wgame.baked_uv_view(action='RETURN')
    assert bpy.context.scene==scene
    assert {key:getattr(settings,key) for key in identity}==identity
    assert bpy.ops.wgame_rigging.bootstrap_current_character_source.poll()
    assert bpy.ops.wgame_rigging.publish_fbx_and_update_unity.poll()
    assert rig.WGAME_RIGGING_OT_add_detached_child.poll(bpy.context)==custom_poll_before
    assert hashlib.sha256(source.read_bytes()).hexdigest()==before_hash
    print('PREVIEW_IDENTITY_PASS',len(guarded),'operators blocked; source identity, existing polls and Work file preserved')
finally:
    preview.unregister();rig.unregister()
