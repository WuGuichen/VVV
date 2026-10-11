"""Explicit, non-destructive intake of newly authored environment meshes."""
import re

import bpy

from . import assembly, authoring, color_profile

ASSET_KEY = 'wgame_environment_asset'
EXCLUDE_KEY = 'wgame_environment_exclude'


def pending(scene):
    return sorted((obj for obj in scene.objects if obj.type == 'MESH'
                   and not obj.get(ASSET_KEY) and not obj.get(EXCLUDE_KEY)), key=lambda obj: obj.name)


def require_registered(scene):
    missing = pending(scene)
    if missing:
        names = '、'.join(obj.name for obj in missing[:8])
        if len(missing) > 8:
            names += '…'
        raise ValueError(f'存在 {len(missing)} 个未登记模型：{names}。请先「登记选中模型」，辅助网格可「设为辅助模型」。')
    seen = set()
    for obj in scene.objects:
        if obj.type != 'MESH' or not obj.get(ASSET_KEY):
            continue
        identifier = str(obj[ASSET_KEY])
        _validate_id(identifier)
        if identifier in seen:
            raise ValueError('重复的场景组件 ID：' + identifier)
        seen.add(identifier)


def _validate_id(identifier):
    if not identifier or not identifier.replace('_', '').replace('-', '').isalnum():
        raise ValueError('组件 ID 只能包含字母、数字、下划线或短横线：' + identifier)
    assembly.category(identifier)


def suggest_id(name, prefix='Prop_'):
    value = re.sub(r'[^A-Za-z0-9_-]+', '_', name).strip('_') or 'Asset'
    if not any(value.startswith(part) for part in assembly.CATEGORIES):
        value = prefix + value
    return value


def _material_profile(material):
    """Existing surface authoring wins; plain industrial materials seed profiles 10-13."""
    return color_profile.material_profile(material)


def _face_pairs(obj):
    mesh = obj.data
    if not mesh.vertices or not mesh.polygons:
        raise ValueError('模型没有可登记的网格面：' + obj.name)
    for name, domain, kind in (('PaletteToken', 'FACE', 'INT'), ('SurfaceProfile', 'FACE', 'INT'),
                               ('PaletteData', 'CORNER', 'FLOAT_COLOR')):
        attr = mesh.attributes.get(name)
        if attr and (attr.domain != domain or attr.data_type != kind):
            raise ValueError(f'{obj.name} 的 {name} 类型不符合场景制作约定，请先整理；登记不会覆盖已有数据')
    pairs = []
    cache = {}
    profiles = mesh.attributes.get('SurfaceProfile')
    if profiles and any(not 0 <= item.value < 16 for item in profiles.data):
        raise ValueError('已有 SurfaceProfile 含无效表面类型：' + obj.name)
    for face in mesh.polygons:
        color, profile = color_profile.face_pair(obj, face.index, cache)
        if profiles is None:
            profile = _material_profile(mesh.materials[face.material_index])
        pairs.append((color, profile))
    return pairs


def _prepare_mesh(mesh, pairs):
    """Only create missing layers. Never reinterpret or clear existing UVs or face data."""
    palette = authoring.palette_module()
    active_uv = mesh.uv_layers.active.name if mesh.uv_layers.active else None
    render_uv = next((layer.name for layer in mesh.uv_layers if layer.active_render), None)
    token = mesh.attributes.get('PaletteToken')
    profile = mesh.attributes.get('SurfaceProfile')
    data = mesh.color_attributes.get('PaletteData')
    uv = mesh.uv_layers.get('PaletteUV')
    create_token, create_profile, create_data, create_uv = token is None, profile is None, data is None, uv is None
    if create_token:
        token = mesh.attributes.new(name='PaletteToken', type='INT', domain='FACE')
    if create_profile:
        profile = mesh.attributes.new(name='SurfaceProfile', type='INT', domain='FACE')
    if create_data:
        data = mesh.color_attributes.new(name='PaletteData', type='FLOAT_COLOR', domain='CORNER')
    if create_uv:
        uv = mesh.uv_layers.new(name='PaletteUV', do_init=False)
    if mesh.uv_layers.get('DetailUV') is None:
        detail = mesh.uv_layers.new(name='DetailUV', do_init=False)
        for loop in detail.data:
            loop.uv = (-1, -1)
    # Resolve handles after adding layers: Blender can reallocate CustomData storage.
    token, profile = mesh.attributes['PaletteToken'], mesh.attributes['SurfaceProfile']
    data, uv = mesh.color_attributes['PaletteData'], mesh.uv_layers['PaletteUV']
    for face, (color, surface) in zip(mesh.polygons, pairs):
        if create_token:
            token.data[face.index].value = palette.logical_token(color, surface)
        if create_profile:
            profile.data[face.index].value = surface
        x, y = palette.color_coordinates(color)
        coordinates = ((x + .5) / palette.WIDTH, (y + .5) / palette.HEIGHT)
        alpha = palette.STATE.catalog['surfaceProfiles'][surface]['alphaByte'] / 255.0
        for index in face.loop_indices:
            if create_uv:
                uv.data[index].uv = coordinates
            if create_data:
                data.data[index].color = (1, 0, 0, alpha)
    mesh.update()
    if active_uv:
        mesh.uv_layers.active = mesh.uv_layers[active_uv]
    if render_uv:
        mesh.uv_layers[render_uv].active_render = True


def register_models(scene, objects, prefix='Prop_', identifier=''):
    """Preflight the entire selection and stage mesh copies before committing any object."""
    if prefix not in assembly.CATEGORIES:
        raise ValueError('未知组件类别：' + prefix)
    objects = list(dict.fromkeys(obj for obj in objects if obj.type == 'MESH'))
    if not objects:
        raise ValueError('请选中需要登记的网格模型')
    if any(obj.mode != 'OBJECT' for obj in objects):
        raise ValueError('请先切换到物体模式再登记')
    if any(obj.name not in scene.objects for obj in objects):
        raise ValueError('只能登记当前制作场景中的模型')
    if any(obj.library is not None and obj.override_library is None for obj in objects):
        raise ValueError('外部链接对象不可直接登记，请先制作本地对象或库覆盖')
    if identifier and len(objects) != 1:
        raise ValueError('自定义组件 ID 只适用于单个模型')
    owners = {}
    for obj in scene.objects:
        if obj.type == 'MESH' and obj.get(ASSET_KEY):
            asset_id = str(obj[ASSET_KEY])
            _validate_id(asset_id)
            if asset_id in owners:
                raise ValueError('重复的场景组件 ID：' + asset_id)
            owners[asset_id] = obj
    plans = []
    for obj in objects:
        if obj.get(ASSET_KEY):
            if identifier and identifier != obj[ASSET_KEY]:
                raise ValueError('已登记模型的稳定 ID 不通过重新登记修改：' + obj.name)
            continue
        asset_id = identifier.strip() or suggest_id(obj.name, prefix)
        _validate_id(asset_id)
        if asset_id in owners:
            raise ValueError('组件 ID 已被占用：' + asset_id)
        owners[asset_id] = obj
        plans.append((obj, asset_id, _face_pairs(obj)))
    staged = []
    try:
        for obj, asset_id, pairs in plans:
            mesh = obj.data.copy()
            staged.append((obj, asset_id, mesh))
            _prepare_mesh(mesh, pairs)
    except Exception:
        for _, _, mesh in staged:
            bpy.data.meshes.remove(mesh)
        raise
    for obj, asset_id, mesh in staged:
        obj.data = mesh
        obj[ASSET_KEY] = asset_id
        obj['wgame_environment_mode'] = 'shared_detail_bake'
        if EXCLUDE_KEY in obj:
            del obj[EXCLUDE_KEY]
    return {'registered': len(staged), 'unchanged': len(objects) - len(staged),
            'assets': [{'object': obj.name, 'id': asset_id} for obj, asset_id, _ in staged]}


def set_auxiliary(objects, excluded=True):
    objects = [obj for obj in objects if obj.type == 'MESH']
    if not objects:
        raise ValueError('请选中辅助网格')
    if any(obj.get(ASSET_KEY) for obj in objects):
        raise ValueError('已登记组件不能设为辅助模型，以免从发布包中意外删除')
    for obj in objects:
        if excluded:
            obj[EXCLUDE_KEY] = True
        elif EXCLUDE_KEY in obj:
            del obj[EXCLUDE_KEY]
    return len(objects)
