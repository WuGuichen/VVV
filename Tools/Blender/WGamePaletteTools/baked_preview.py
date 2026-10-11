"""Transient baked UV inspection in the current window; never edits the Work mesh."""
import json
from pathlib import Path

import bpy
from bpy.app.handlers import persistent

TAG = 'wgame_baked_preview'
_sessions = {}
_kinds = ('objects', 'collections', 'meshes', 'armatures', 'materials', 'node_groups', 'actions', 'images', 'cameras', 'lights', 'curves', 'worlds')


def _hair_module():
    import importlib.util
    path=Path(__file__).with_name('baked_hair_preview.py')
    spec=importlib.util.spec_from_file_location('wgame_baked_hair_preview',path)
    module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
    return module


def active(window=None):
    window = window or bpy.context.window
    return window is not None and window.as_pointer() in _sessions


def _mode(window, mode):
    with bpy.context.temp_override(window=window):
        obj = window.view_layer.objects.active
        if obj and obj.mode != mode:
            bpy.ops.object.mode_set(mode=mode)


def _ui_state(window):
    spaces = [];views=[]
    shading_keys=('use_scene_lights','use_scene_world','studio_light','studiolight_rotate_z','studiolight_intensity','studiolight_background_alpha','studiolight_background_blur')
    for workspace in bpy.data.workspaces:
        for screen in workspace.screens:
            for area in screen.areas:
                if area.type == 'IMAGE_EDITOR':
                    spaces.append((area, area.ui_type, area.spaces.active.image))
                if area.type == 'VIEW_3D':
                    space=area.spaces.active
                    views.append((space,{key:getattr(space.shading,key) for key in ('type',)+shading_keys if hasattr(space.shading,key)},space.overlay.show_extras))
    active_view=next((a.spaces.active for a in window.screen.areas if a.type=='VIEW_3D'),None)
    return {'workspace': window.workspace, 'scene': window.scene, 'viewLayer': window.view_layer,
            'sourceShading':{key:getattr(active_view.shading,key) for key in shading_keys if active_view and hasattr(active_view.shading,key)},
            'sourceColor':{key:getattr(window.scene.view_settings,key) for key in ('view_transform','look','exposure','gamma')},
            'boneVisibility':[(space,space.overlay.show_bones) for space,_,_ in views],
            'overlayVisibility':[(space,space.overlay.show_overlays) for space,_,_ in views],
            'object': window.view_layer.objects.active,
            'mode': window.view_layer.objects.active.mode if window.view_layer.objects.active else 'OBJECT',
            'spaces': spaces, 'views':views, 'fallbackArea': None}


def _copy_material_shading(shading, values):
    # SOLID/MATCAP and MATERIAL use different studio_light enum choices.
    # Keep the viewport's material HDRI when the source uses a studio light.
    world_lights={light.name for light in bpy.context.preferences.studio_lights if light.type=='WORLD'}
    for key,value in values.items():
        if key=='studio_light' and value not in world_lights:
            continue
        setattr(shading,key,value)


def configure_viewport(window):
    state=_sessions.get(window.as_pointer(),{})
    runtime=bool(window.scene.get('wgame_baked_hair_preview_enabled',False))
    for area in window.screen.areas:
        if area.type=='VIEW_3D':
            shading=area.spaces.active.shading
            shading.type='MATERIAL'
            _copy_material_shading(shading,state.get('sourceShading',{}))
            if runtime:shading.use_scene_lights=True;shading.use_scene_world=True
            area.spaces.active.overlay.show_extras=False
            area.spaces.active.overlay.show_bones=False
            area.spaces.active.overlay.show_overlays=False


def set_style_mode(window, enabled):
    state=_sessions[window.as_pointer()];scene=window.scene
    scene['wgame_baked_hair_preview_enabled']=enabled
    for obj in state.get('runtimeLights',[]):obj.hide_set(not enabled);obj.hide_render=not enabled
    for obj in state.get('sourceLights',[]):obj.hide_set(enabled);obj.hide_render=enabled
    scene.world=state.get('runtimeWorld') if enabled else state.get('sourceWorld')
    if enabled:
        scene.view_settings.view_transform='AgX';scene.view_settings.look='AgX - Medium High Contrast'
    else:
        for key,value in state.get('sourceColor',{}).items():setattr(scene.view_settings,key,value)
    for obj in scene.objects:
        if obj.type!='MESH':continue
        for slot in obj.material_slots:
            node=slot.material.node_tree.nodes.get('WGameHairPreviewEnabled') if slot.material and slot.material.use_nodes else None
            if node:node.outputs[0].default_value=float(enabled)
    configure_viewport(window)


def _uv_workspace(window, image, state=None):
    workspace = bpy.data.workspaces.get('UV Editing')
    if workspace is None:
        workspace = next((w for w in bpy.data.workspaces if any(a.type == 'IMAGE_EDITOR' and a.ui_type == 'UV' for s in w.screens for a in s.areas)), None)
    scene = window.scene
    if workspace:
        window.workspace = workspace
        window.scene = scene
    editors = [a for a in window.screen.areas if a.type == 'IMAGE_EDITOR']
    if not editors:
        area = next((a for a in window.screen.areas if a.type == 'VIEW_3D'), None)
        if area is None:
            raise ValueError('当前窗口没有可用的 UV 编辑区域')
        if state is not None:
            state['fallbackArea'] = (area, area.type, area.ui_type)
        area.type = 'IMAGE_EDITOR'
        editors = [area]
    for area in editors:
        area.ui_type = 'UV'
        area.spaces.active.image = image
        area.spaces.active.show_region_ui = True
        region = next((r for r in area.regions if r.type == 'WINDOW'), None)
        if region:
            with bpy.context.temp_override(window=window, area=area, region=region):
                bpy.ops.image.view_all(fit_view=True)
        area.tag_redraw()


def _remove_owned(state):
    owned_images=state.get('owned',{}).get('images',set())
    # Activating a workspace can create a window-specific screen not present in
    # the original UI snapshot. Release its image references as well.
    for screen in bpy.data.screens:
        for area in screen.areas:
            for space in area.spaces:
                if space.type=='IMAGE_EDITOR' and space.image in owned_images:
                    space.image=None
    for kind in _kinds:
        collection = getattr(bpy.data, kind)
        for item in state.get('owned', {}).get(kind, ()):
            try:
                if kind in {'objects', 'collections', 'images'} or item.users == 0:
                    collection.remove(item, do_unlink=True)
            except ReferenceError:
                pass


def close_preview(window=None):
    window = window or bpy.context.window
    if window is None:
        return
    state = _sessions.pop(window.as_pointer(), None)
    if state is None:
        return
    _mode(window, 'OBJECT')
    window.workspace = state['workspace']
    window.scene = state['scene']
    if state['object']:
        window.view_layer.objects.active = state['object']
    for area, ui_type, image in state['spaces']:
        try:
            area.ui_type = ui_type
            area.spaces.active.image = image
        except (ReferenceError, TypeError):
            pass
    for space,values,extras in state.get('views',[]):
        try:
            for key,value in values.items():setattr(space.shading,key,value)
            space.overlay.show_extras=extras
        except ReferenceError:pass
    for space,visible in state.get('boneVisibility',[]):
        try:space.overlay.show_bones=visible
        except ReferenceError:pass
    for space,visible in state.get('overlayVisibility',[]):
        try:space.overlay.show_overlays=visible
        except ReferenceError:pass
    if state['fallbackArea']:
        area, area_type, ui_type = state['fallbackArea']
        area.type = area_type
        area.ui_type = ui_type
    preview_scene = state.get('previewScene')
    if preview_scene:
        bpy.data.scenes.remove(preview_scene, do_unlink=True)
    _remove_owned(state)
    if state['object'] and state['mode'] != 'OBJECT':
        _mode(window, state['mode'])


def _preview_material(final, root, document=None, meshes=None, light=None):
    material = bpy.data.materials.new('WGame_Baked_UV_Preview')
    tree = material.node_tree
    bsdf = next(n for n in tree.nodes if n.type == 'BSDF_PRINCIPLED')
    base = bpy.data.images.load(str(final / 'BaseColor.png'), check_existing=False)
    base.colorspace_settings.name = 'sRGB'
    base_node = tree.nodes.new('ShaderNodeTexImage')
    base_node.image = base
    base_node.interpolation = 'Linear'
    tree.links.new(base_node.outputs['Color'], bsdf.inputs['Base Color'])
    tree.nodes.active = base_node
    data_path = final / 'SurfaceData.png'
    catalog_path = root / 'ArtSource/Common/Palettes/WGameSurfacePalette/catalog.json'
    if data_path.exists() and catalog_path.exists():
        data = bpy.data.images.load(str(data_path), check_existing=False)
        data.colorspace_settings.name = 'Non-Color'
        data.alpha_mode = 'CHANNEL_PACKED'
        data_node = tree.nodes.new('ShaderNodeTexImage')
        data_node.image = data
        data_node.interpolation = 'Closest'
        catalog = json.loads(catalog_path.read_text(encoding='utf-8-sig'))
        specular_profile=None
        for field, socket in (('roughness', 'Roughness'), ('metallic', 'Metallic'), ('specular', 'Specular IOR Level'), ('subsurface', 'Subsurface Weight')):
            ramp = tree.nodes.new('ShaderNodeValToRGB')
            ramp.name='WGame Surface '+field
            ramp.color_ramp.interpolation = 'CONSTANT'
            while len(ramp.color_ramp.elements) > 1:
                ramp.color_ramp.elements.remove(ramp.color_ramp.elements[-1])
            for i, profile in enumerate(catalog['surfaceProfiles']):
                element = ramp.color_ramp.elements[0] if i == 0 else ramp.color_ramp.elements.new(i * 16 / 255)
                value = (.35 if i==0 else .28 if i==1 else 0.) if field=='subsurface' else float(profile[field])
                element.color = (value, value, value, 1)
            tree.links.new(data_node.outputs['Alpha'], ramp.inputs[0])
            tree.links.new(ramp.outputs['Color'], bsdf.inputs[socket])
            if field=='specular':specular_profile=ramp.outputs['Color']
        bsdf.inputs['Subsurface Scale'].default_value=.015
        if document and meshes and light:
            # Continuous AO must not inherit the point sampler used for classes.
            continuous=tree.nodes.new('ShaderNodeTexImage');continuous.image=data;continuous.interpolation='Linear'
            hair=_hair_module()
            for obj in meshes:hair.prepare_mesh(obj)
            hair.build(material,base_node.outputs['Color'],continuous.outputs['Color'],document['hair'],
                       [u.name for u in meshes[0].data.uv_layers],light,profile=data_node.outputs['Alpha'],profile_specular=specular_profile)
    return material, base


def open_preview(final, root, window=None):
    window = window or bpy.context.window
    if window is None:
        raise ValueError('需要 Blender 窗口才能查看 UV')
    if active(window):
        close_preview(window)
    final, root = Path(final), Path(root)
    receipt = final / 'character_baked_surface_binding.json'
    if not receipt.is_file():
        raise ValueError('还没有烘焙结果，请先烘焙导出')
    document = json.loads(receipt.read_text(encoding='utf-8-sig'))
    fbx = final / Path(document.get('modelFile', '')).name
    if not fbx.is_file() or not (final / 'BaseColor.png').is_file():
        raise ValueError('烘焙模型或底色图缺失，请重新导出')
    state = _ui_state(window)
    before = {kind: set(getattr(bpy.data, kind)) for kind in _kinds}
    _sessions[window.as_pointer()] = state
    try:
        _mode(window, 'OBJECT')
        scene = bpy.data.scenes.new('WGame 烘焙结果预览')
        scene[TAG] = True
        if (final/'bake_preview_receipt.json').is_file():scene['wgame_baked_candidate_path']=str(final)
        scene['wgame_baked_preview_package'] = document.get('packageId', '')
        source_path=document.get('authoringSourcePath')
        scene['wgame_baked_preview_source']=str((root/source_path).resolve()) if source_path else bpy.data.filepath
        source_settings=getattr(state['scene'],'wgame_rigging_assistant',None)
        display_name=getattr(source_settings,'character_display_name','')
        manifest=final.parent/'authoring_manifest.json'
        if manifest.is_file():
            display_name=json.loads(manifest.read_text(encoding='utf-8-sig')).get('displayName',display_name)
        scene['wgame_baked_preview_display_name']=display_name
        state['previewScene'] = scene
        window.scene = scene
        with bpy.context.temp_override(window=window):
            bpy.ops.import_scene.fbx(filepath=str(fbx))
        meshes = [o for o in scene.objects if o.type == 'MESH']
        if not meshes:
            raise ValueError('烘焙 FBX 没有网格')
        light=_hair_module().lighting(scene)
        state['runtimeWorld']=scene.world
        state['runtimeLights']=[o for o in scene.objects if o.type=='LIGHT']
        state['sourceWorld']=state['scene'].world.copy() if state['scene'].world else None
        state['sourceLights']=[]
        for original in state['scene'].objects:
            if original.type!='LIGHT' or original.hide_render or not original.visible_get(view_layer=state['viewLayer']):continue
            copy=original.copy();copy.data=original.data.copy();copy.parent=None;scene.collection.objects.link(copy)
            copy.matrix_world=original.matrix_world;state['sourceLights'].append(copy)
        bpy.context.view_layer.update()
        material, image = _preview_material(final, root,document,meshes,light)
        scene['wgame_baked_hair_preview_enabled']=False
        for obj in meshes:
            obj.data.materials.clear()
            obj.data.materials.append(material)
            for face in obj.data.polygons:
                face.material_index = 0
            obj.data.uv_layers.active_index = 0
            obj.data.uv_layers[0].active_render = True
        state['owned'] = {kind: set(getattr(bpy.data, kind)) - before[kind] for kind in _kinds}
        _uv_workspace(window, image, state)
        set_style_mode(window,False)
        with bpy.context.temp_override(window=window):
            bpy.ops.object.select_all(action='DESELECT')
            for obj in meshes:
                obj.select_set(True)
            window.view_layer.objects.active = meshes[0]
            bpy.ops.object.mode_set(mode='EDIT')
            bpy.ops.mesh.select_all(action='SELECT')
        return {'meshes': len(meshes), 'textureSize': tuple(image.size), 'uv': meshes[0].data.uv_layers[0].name}
    except Exception:
        state['owned'] = {kind: set(getattr(bpy.data, kind)) - before[kind] for kind in _kinds}
        close_preview(window)
        raise


def show_source_uv(window=None):
    window = window or bpy.context.window
    close_preview(window)
    obj = window.view_layer.objects.active
    if obj is None or obj.type != 'MESH':
        obj = next((o for o in window.scene.objects if o.type == 'MESH' and o.data.uv_layers.get('PaletteUV')), None)
    if obj is None:
        raise ValueError('请先选择角色制作网格')
    _mode(window, 'OBJECT')
    uv = obj.data.uv_layers.get('DetailUV') or obj.data.uv_layers.get('FaceDetailUV') or obj.data.uv_layers.get('PaletteUV')
    if uv is None:
        raise ValueError('制作网格没有 DetailUV 或 PaletteUV')
    obj.data.uv_layers.active = uv
    image = next((n.image for s in obj.material_slots if s.material and s.material.use_nodes
                  for n in s.material.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image
                  and ('detail' in n.name.lower() if 'Detail' in uv.name else 'palette' in n.image.name.lower())), None)
    _uv_workspace(window, image)
    with bpy.context.temp_override(window=window):
        bpy.ops.object.select_all(action='DESELECT')
        obj.select_set(True)
        window.view_layer.objects.active = obj
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.mesh.select_all(action='SELECT')


def _paths(context):
    from . import _find_repo_source_file
    source = _find_repo_source_file()
    if not source:
        raise ValueError('找不到仓库路径')
    root = Path(source).resolve().parents[3]
    cid = str(getattr(getattr(context.scene, 'wgame_rigging_assistant', None), 'character_id', '')).strip()
    if not cid:
        parts = Path(bpy.data.filepath).parts
        for i in range(len(parts)-2):
            if parts[i:i+2] == ('ArtSource', 'Characters'):
                cid = parts[i+2]
                break
    if len(cid) != 6 or not cid.isdecimal():
        raise ValueError('请打开角色 Work Blend 或选择角色编号')
    return root / 'ArtSource/Characters' / cid / 'Final_Source', root


class WG_OT_BakedUV(bpy.types.Operator):
    bl_idname = 'wgame.baked_uv_view'
    bl_label = '查看角色 UV'
    action: bpy.props.EnumProperty(items=[('BEFORE', '制作 UV', ''), ('AFTER', '烘焙 UV', ''), ('RETURN', '返回制作', '')])

    def execute(self, context):
        try:
            if self.action == 'RETURN':
                close_preview(context.window)
            elif self.action == 'BEFORE':
                show_source_uv(context.window)
            else:
                if active(context.window):
                    close_preview(context.window)
                final, root = _paths(bpy.context)
                open_preview(final, root, context.window)
        except (ValueError, RuntimeError, OSError) as e:
            self.report({'ERROR'}, str(e))
            return {'CANCELLED'}
        return {'FINISHED'}


def draw_buttons(layout, context):
    row = layout.row(align=True)
    row.operator('wgame.baked_uv_view', text='制作 UV').action = 'BEFORE'
    row.operator('wgame.baked_uv_view', text='上次发布 UV').action = 'AFTER'
    if active(context.window):
        layout.operator('wgame.baked_uv_view', text='返回制作').action = 'RETURN'
        enabled=context.scene.get('wgame_baked_hair_preview_enabled',False)
        layout.label(text='运行时风格预览（近似）' if enabled else '按制作材质对照')
        layout.operator('wgame.baked_hair_compare',text='返回制作材质对照' if enabled else '查看运行时风格（近似）',depress=enabled)
        if enabled:layout.operator('wgame.baked_hair_light',text='转动预览灯光')
        view=next((a.spaces.active for a in context.window.screen.areas if a.type=='VIEW_3D'),None)
        if view:layout.prop(view.overlay,'show_overlays',text='显示网格叠加（检查 UV）')
        layout.label(text='烘焙结果预览，保存时自动返回制作')
        from . import baked_workflow
        baked_workflow.draw_controls(layout,context)


class WG_OT_BakedHairCompare(bpy.types.Operator):
    bl_idname='wgame.baked_hair_compare'
    bl_label='对比头发光照效果'
    def execute(self,context):
        if not active(context.window):return {'CANCELLED'}
        enabled=not context.scene.get('wgame_baked_hair_preview_enabled',False)
        set_style_mode(context.window,enabled)
        return {'FINISHED'}


class WG_OT_BakedHairLight(bpy.types.Operator):
    bl_idname='wgame.baked_hair_light'
    bl_label='转动头发预览灯光'
    def execute(self,context):
        if not active(context.window):return {'CANCELLED'}
        import math
        key=next((o for o in context.scene.objects if o.type=='LIGHT' and o.name.startswith('WGame Preview Key')),None)
        if key is None:return {'CANCELLED'}
        key.rotation_euler.z+=math.radians(45)
        context.view_layer.update()
        hair=_hair_module()
        for obj in context.scene.objects:
            if obj.type!='MESH':continue
            for slot in obj.material_slots:
                if slot.material and slot.material.use_nodes:hair.update_light(slot.material,key)
        return {'FINISHED'}


class WG_PT_BakedUV(bpy.types.Panel):
    bl_label = '制作与烘焙 UV'
    bl_idname = 'WG_PT_baked_uv'
    bl_space_type = 'IMAGE_EDITOR'
    bl_region_type = 'UI'
    bl_category = 'WGame'

    def draw(self, context):
        draw_buttons(self.layout, context)


@persistent
def close_all(_=None):
    for window in list(bpy.context.window_manager.windows):
        if active(window):
            close_preview(window)


CLASSES = (WG_OT_BakedUV, WG_OT_BakedHairCompare, WG_OT_BakedHairLight, WG_PT_BakedUV)


def register():
    for cls in CLASSES:
        bpy.utils.register_class(cls)
    for handlers in (bpy.app.handlers.save_pre, bpy.app.handlers.load_pre):
        if close_all not in handlers:
            handlers.append(close_all)


def unregister():
    close_all()
    for handlers in (bpy.app.handlers.save_pre, bpy.app.handlers.load_pre):
        if close_all in handlers:
            handlers.remove(close_all)
    for cls in reversed(CLASSES):
        try:
            bpy.utils.unregister_class(cls)
        except RuntimeError:
            pass
