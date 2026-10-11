"""Shared weapon export contract; source preparation never imports into Unity."""
from __future__ import annotations

import copy
import json
import math
from pathlib import Path
from urllib.request import Request, urlopen


def collect_weapon_meshes(context):
    # Active/selected objects are editing state, not the asset's export boundary.
    return sorted((obj for obj in context.scene.objects
                   if obj.type == 'MESH' and not obj.hide_get()
                   and not obj.hide_viewport
                   and (not hasattr(obj, 'visible_get') or obj.visible_get(view_layer=context.view_layer))
                   and not obj.name.startswith('cs_')
                   and not obj.name.upper().endswith(
                       ('__TOPO_BACKUP', '_TOPO_BACKUP', '__BACKUP', '_BACKUP'))),
                  key=lambda obj: obj.name)


def authored_world_matrix(obj):
    """Read authored transforms even when a disabled collection skips evaluation."""
    if obj.constraints or obj.parent_type != 'OBJECT':
        raise ValueError(f'{obj.name}: 武器原点与 Trace 只支持无约束的 Object 父子变换')
    if obj.parent is None:
        return obj.matrix_basis.copy()
    return authored_world_matrix(obj.parent) @ obj.matrix_parent_inverse @ obj.matrix_basis


def trace_points(scene, weapon_mesh, require_explicit=False):
    from mathutils import Vector
    root = scene.objects.get('trace_blade_root')
    tip = scene.objects.get('trace_blade_tip')
    if root is not None and tip is not None:
        if root.type != 'EMPTY' or tip.type != 'EMPTY':
            raise ValueError('trace_blade_root / trace_blade_tip 必须是 Empty')
        inverse = authored_world_matrix(weapon_mesh).inverted() if weapon_mesh else None
        positions = [authored_world_matrix(obj).translation for obj in (root, tip)]
        start, end = [inverse @ pos if inverse is not None else pos.copy() for pos in positions]
        reach = (end - start).length
        if not math.isfinite(reach) or reach <= 1e-5:
            raise ValueError('Trace 判定线段长度必须大于零')
        return start, end, reach
    if require_explicit or root is not None or tip is not None:
        raise ValueError('正式武器导出需要配对的 trace_blade_root / trace_blade_tip')
    if weapon_mesh is None:
        return Vector((0, 0, .15)), Vector((0, 0, 1.2)), 1.05
    corners = weapon_mesh.bound_box
    minimum = Vector(tuple(min(c[i] for c in corners) for i in range(3)))
    maximum = Vector(tuple(max(c[i] for c in corners) for i in range(3)))
    axis = 2 if maximum.z - minimum.z >= maximum.y - minimum.y else 1
    start, end = Vector((0, 0, 0)), Vector((0, 0, 0))
    start[axis] = minimum[axis] + (maximum[axis] - minimum[axis]) * .15
    end[axis] = maximum[axis]
    return start, end, (end - start).length


def catalog_identity(repo, weapon_id):
    catalog = json.loads((Path(repo) / 'Authoring/WeaponPackages/weapon_id_catalog.json').read_text(encoding='utf-8-sig'))
    matches = [entry for entry in catalog['entries'] if entry['id'] == weapon_id and entry.get('status') == 'active']
    if len(matches) != 1 or matches[0].get('isVirtual') or not matches[0].get('category'):
        raise ValueError(f'武器 {weapon_id} 没有唯一的有效编号池类型')
    identity = matches[0]
    source = json.loads((Path(repo) / 'WeaponSource/global_weapon_library.json').read_text(encoding='utf-8-sig'))
    types = [item for item in source.get('weaponTypes', []) if item.get('typeId') == identity.get('weaponTypeId')]
    if len(types) != 1 or types[0].get('equipmentCategory') != identity['category']:
        raise ValueError(f"WeaponTypeBindingInvalid:{weapon_id}")
    return copy.deepcopy(identity)


def unity_position(position):
    return {'x': round(position[0], 4), 'y': round(position[2], 4), 'z': round(-position[1], 4)}


def model_pose(position=(0, 0, 0)):
    return {'parentKind':'ModelRoot', 'parentPath':'', 'position':unity_position(position),
            'rotation':{'x':0,'y':0,'z':0,'w':1}, 'scale':{'x':1,'y':1,'z':1}}


def single_configuration(identity, start, end, reach, existing=None):
    weapon_id = identity['id']
    entry = copy.deepcopy(existing) if existing is not None else {
        'singleWeaponConfigurationId':f'weapon.{weapon_id}.main',
        'displayName':identity['displayName'], 'category':identity['category'], 'weaponTypeId':identity.get('weaponTypeId', ''),
        'tags':[], 'allowedSlots':['mainHand','offHand'], 'occupiesSlots':[],
        'weaponRef':{
            'resourceStableId':f'global.profile.common.model.weapon.{weapon_id}.model',
            'sourceProviderId':'resourceMapping', 'bindingKind':'ResourceManagerAsset',
            'expectedKind':'Model', 'expectedUsage':'weaponModel', 'expectedHash':'', 'unityGuid':'',
            'unityAssetPath':f'Assets/MxFrameworkGenerated/Common/Weapons/{weapon_id}/model/{weapon_id}.fbx',
            'runtimeResourceKey':f'weapon.{weapon_id}.model', 'providerResourceKey':f'weapon.{weapon_id}.model'},
        'modelPose':model_pose(),
        'presentationConfiguration':{'profileId':'','displayMode':'Equipped','modelScale':1},
        'combatConfiguration':{'profileId':'','impactScale':1,'combatTags':[]},
        'resourcePolicy':{'profileId':'','preloadPolicy':'EquipmentInitial','requiredForSpawn':True},
        'traceProfiles':[], 'baseModifiers':[]}
    if existing is not None and (entry.get('weaponTypeId') != identity.get('weaponTypeId')
                                 or entry.get('category') != identity.get('category')):
        raise ValueError(f"WeaponIdentityTypeProjectionMismatch:{weapon_id}")
    # Existing tags, poses, VFX, modifiers, trace radius and action bindings remain authoritative.
    entry.setdefault('combatConfiguration', {})['reachMeters'] = round(reach, 4)
    trace_id = f'trace.{weapon_id}.blade'
    traces = entry.setdefault('traceProfiles', [])
    trace = next((item for item in traces if item.get('traceId') == trace_id), None)
    if trace is None:
        trace = {'traceId':trace_id, 'startLocatorPath':'weapon/trace_blade_root',
                 'endLocatorPath':'weapon/trace_blade_tip', 'radius':.05,
                 'proximalExtension':0, 'proximalRadius':0,
                 'sampleRule':'CapsuleSweep', 'fixedSampleCount':4, 'actionKeys':[]}
        traces.append(trace)
    trace.setdefault('startPose', model_pose())['position'] = unity_position(start)
    trace.setdefault('endPose', model_pose())['position'] = unity_position(end)
    return entry


def weapon_manifest(identity, start, end, reach, has_palette, exported_utc, existing=None):
    document = copy.deepcopy(existing) if existing is not None else {}
    weapon_id = identity['id']
    document.update(resourceId=weapon_id, displayName=identity['displayName'], kind='weapon')
    if has_palette: document['paletteMode'] = 'sharedWGameAtlas'
    model = {'id':'model', 'label':identity['displayName']+' Model',
             'source':{'kind':'file','scope':'public','path':f'Weapons/{weapon_id}/model/{weapon_id}.fbx'},
             'target':{'resourceKey':f'weapon.{weapon_id}.model','usage':'weaponModel','bindTarget':'weapon.model',
                       'importHint':{'importerType':'model','preset':'weapon','metadata':{
                           'traceBladeRoot':','.join(f'{value:.4f}' for value in unity_position(start).values()),
                           'traceBladeTip':','.join(f'{value:.4f}' for value in unity_position(end).values()),
                           'reachMeters':f'{reach:.4f}','sourceExportedUtc':exported_utc}}}}
    document['items'] = [model]+[item for item in document.get('items', [])
                                 if item.get('target', {}).get('resourceKey') != f'weapon.{weapon_id}.model'
                                 and item.get('id') not in {'model','weapon-model'}]
    if document.get('stage') == 'source': document['stage'] = 'exported'
    if 'reviewStatus' in document: document['reviewStatus'] = 'pending-unity-validation'
    return document


def api_json(base_url, route, body=None):
    request = Request(base_url.rstrip('/')+route,
                      data=None if body is None else json.dumps(body).encode('utf-8'),
                      headers={'Content-Type':'application/json'},
                      method='GET' if body is None else 'POST')
    with urlopen(request, timeout=15) as response:
        return json.loads(response.read().decode('utf-8-sig'))


def assert_preserved(source, target, path=''):
    """Reject a stale service that silently drops current source-schema fields."""
    if isinstance(source, dict):
        for key,value in source.items():
            if key not in target: raise ValueError('Authoring 服务不保留字段 '+path+'/'+key+'；请更新服务，未保存公共库')
            assert_preserved(value,target[key],path+'/'+key)
    elif isinstance(source, list):
        if len(source)!=len(target): raise ValueError('Authoring 服务读取列表不一致 '+path)
        for index,(a,b) in enumerate(zip(source,target)): assert_preserved(a,b,path+f'/{index}')
    elif source!=target: raise ValueError('Authoring 服务读取值不一致 '+path)


def sync_single_configuration(base_url, identity, start, end, reach, repository_root):
    # Atomic single-item mutation with revision protection preserves concurrent VFX registrations.
    state = api_json(base_url, '/api/weapon-library/state')
    source=json.loads((Path(repository_root)/'WeaponSource/global_weapon_library.json').read_text(encoding='utf-8-sig'))
    assert_preserved(source,state['library'])
    entry_id = f"weapon.{identity['id']}.main"
    existing = next((item for item in state['library'].get('singleWeaponConfigurations', [])
                     if item.get('singleWeaponConfigurationId') == entry_id), None)
    entry = single_configuration(identity, start, end, reach, existing)
    return api_json(base_url, '/api/weapon-library/configuration/save', {
        'configurationType':'singleWeapon', 'operation':'create' if existing is None else 'update',
        'configurationId':entry_id, 'configuration':entry, 'expectedRevision':state['revision']})


def export_fbx(context, output, apply_transforms=True):
    """Export just the weapon source; no global configuration, Assets or Unity writes."""
    import bpy
    import runpy
    meshes = collect_weapon_meshes(context)
    if not meshes: raise ValueError('没有可导出的武器网格')
    if context.mode != 'OBJECT': raise ValueError('请先切换到物体模式再导出武器')
    if abs(context.scene.unit_settings.scale_length-1) > 1e-6:
        raise ValueError('武器要求 1 Blender Unit = 1 米')
    original_active = context.view_layer.objects.active
    selected = list(context.selected_objects)
    helpers = [context.scene.objects.get(name) for name in ('weapon','trace_blade_root','trace_blade_tip')]
    helpers = [obj for obj in helpers if obj is not None and obj.type == 'EMPTY']
    visibility = {obj:obj.hide_get() for obj in helpers}
    collections = {col:col.hide_viewport for obj in helpers for col in obj.users_collection}
    try:
        for obj in context.view_layer.objects: obj.select_set(False)
        if apply_transforms:
            for mesh in meshes:
                mesh.select_set(True); context.view_layer.objects.active=mesh
                bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
                mesh.select_set(False)
        for mesh in meshes:
            matrix = authored_world_matrix(mesh)
            if matrix.translation.length > 1e-5 or any(abs(v-1) > 1e-5 for v in matrix.to_scale()) or matrix.to_quaternion().angle > 1e-5:
                raise ValueError(f'{mesh.name}: 握持原点必须为零、旋转为零、缩放为一')
        start,end,reach = trace_points(context.scene, meshes[0], require_explicit=True)
        palette_flags = [('PaletteUV' in mesh.data.uv_layers and 'PaletteData' in mesh.data.color_attributes) for mesh in meshes]
        if any(palette_flags) and not all(palette_flags): raise ValueError('色板武器的所有分件必须带 PaletteUV 与 PaletteData')
        if all(palette_flags):
            preflight_path = Path(__file__).resolve().parent.parent/'WGameRiggingAssistant/palette_export_preflight.py'
            preflight = runpy.run_path(str(preflight_path))
            preflight['validate_sources'](meshes)
            graph = context.evaluated_depsgraph_get(); graph.update()
            for mesh in meshes:
                evaluated = mesh.evaluated_get(graph)
                evaluated_mesh = evaluated.to_mesh(preserve_all_data_layers=True, depsgraph=graph)
                try: preflight['validate_mesh'](mesh,evaluated_mesh,'武器修改器求值后检查')
                finally: evaluated.to_mesh_clear()
                mesh.data.color_attributes.active_color=mesh.data.color_attributes['PaletteData']
                mesh.data.color_attributes.render_color_index=mesh.data.color_attributes.find('PaletteData')
        for col in collections: col.hide_viewport=False
        for obj in helpers: obj.hide_set(False)
        context.view_layer.update()
        for obj in meshes+helpers: obj.select_set(True)
        context.view_layer.objects.active=meshes[0]
        output=Path(output);output.parent.mkdir(parents=True,exist_ok=True)
        result=bpy.ops.export_scene.fbx(filepath=str(output),check_existing=False,use_selection=True,
            object_types={'MESH','EMPTY'},global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',
            use_space_transform=True,bake_space_transform=True,axis_forward='-Z',axis_up='Y',
            use_mesh_modifiers=True,use_mesh_modifiers_render=True,mesh_smooth_type='FACE',colors_type='SRGB',
            prioritize_active_color=True,use_mesh_edges=False,use_tspace=True,use_triangles=True,
            use_custom_props=True,add_leaf_bones=False,bake_anim=False,path_mode='STRIP',embed_textures=False,use_metadata=True)
        if 'FINISHED' not in result or not output.is_file(): raise RuntimeError('武器 FBX 导出失败')
        return meshes,start,end,reach,all(palette_flags)
    finally:
        for obj in context.view_layer.objects: obj.select_set(False)
        for obj in helpers: obj.hide_set(visibility[obj])
        for col,value in collections.items(): col.hide_viewport=value
        for obj in selected:
            if not obj.hide_get(): obj.select_set(True)
        context.view_layer.objects.active=original_active
