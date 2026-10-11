"""Checks and stable names for the Unity assembly source package.

This module only reads authored meshes. It never moves a component in the Work
file or changes its palette/detail UVs.
"""

import hashlib
import math
from copy import deepcopy
from pathlib import Path

import bpy


CATEGORIES = {
    'Arch_': 'Architecture',
    'Ground_': 'Ground',
    'Sec_': 'Security',
    'Veh_': 'Vehicles',
    'Tool_': 'Tools',
    'Prop_': 'Props',
}


def category(identifier):
    for prefix, value in CATEGORIES.items():
        if identifier.startswith(prefix):
            return value
    raise ValueError('无法归类的场景组件：' + identifier)


def image_publish_key(image):
    """Image identity stays fixed when painted pixels change."""
    if image is None:
        return hashlib.sha256(b'mx.environment.constant-materials.v1').hexdigest()[:16]
    path = bpy.path.abspath(image.filepath) if image.filepath else ''
    identity = (str(image['wgame_detail_source_id']) if image.get('wgame_detail_source_id')
                else str(Path(path).resolve()) if path else image.name)
    return hashlib.sha256(identity.encode('utf-8')).hexdigest()[:16]


def model_publish_path(identifier):
    return 'Models/' + category(identifier) + '/' + identifier + '.fbx'


def stable_package(candidate):
    """Map a content-addressed bake candidate to stable Unity import paths."""
    document = deepcopy(candidate)
    group_ids = {}
    file_paths = {}
    for group in document['groups']:
        previous_id = group['id']
        stable_id = group.get('publishKey')
        if not stable_id or stable_id in group_ids.values():
            raise ValueError('细节图发布 ID 缺失或重复，请重新烘焙')
        group_ids[previous_id] = stable_id
        for field, filename in (('baseColor', 'BaseColor.png'),
                                ('surfaceData', 'SurfaceData.png')):
            file_paths[group[field]] = 'Textures/' + stable_id + '/' + filename
            group[field] = file_paths[group[field]]
        group['candidateGroupHash'] = previous_id
        group['id'] = stable_id
    for asset in document['assets']:
        previous = asset['model']
        asset['model'] = model_publish_path(asset['id'])
        asset['group'] = group_ids[asset['group']]
        file_paths[previous] = asset['model']
    seen = set()
    for item in document['files']:
        previous = item['path']
        if previous not in file_paths:
            raise ValueError('烘焙文件没有拼装发布路径：' + previous)
        item['path'] = file_paths[previous]
        if item['path'] in seen:
            raise ValueError('重复的 Unity 发布路径：' + item['path'])
        seen.add(item['path'])
    return document, file_paths


def record(obj, mesh=None):
    identifier = str(obj.get('wgame_environment_asset', ''))
    if not identifier or not identifier.replace('_', '').replace('-', '').isalnum():
        raise ValueError('无效的场景组件 ID：' + obj.name)
    category_name = category(identifier)
    mesh = mesh or obj.data
    if not mesh.vertices or not mesh.polygons:
        raise ValueError('场景组件没有可导出的网格：' + identifier)
    coords = [(float(v.co.x), float(v.co.y), float(v.co.z)) for v in mesh.vertices]
    if any(not math.isfinite(value) for vertex in coords for value in vertex):
        raise ValueError('场景组件含无效顶点：' + identifier)
    minimum = [min(v[axis] for v in coords) for axis in range(3)]
    maximum = [max(v[axis] for v in coords) for axis in range(3)]
    dimensions = [round(maximum[axis] - minimum[axis], 4) for axis in range(3)]
    triangles = sum(len(face.vertices) - 2 for face in mesh.polygons)
    warnings = []
    if any(abs(value - 1) > 0.0001 for value in obj.scale):
        warnings.append('旋转或缩放尚未应用：缩放')
    if any(abs(value) > 0.0001 for value in obj.rotation_euler):
        warnings.append('旋转或缩放尚未应用：旋转')
    if abs(minimum[2]) > 0.01:
        warnings.append('原点距底部超过 1 厘米')
    if identifier == 'Arch_Light_Mast_Pillar' and dimensions[2] < 3:
        warnings.append('灯杆高度不足 3 米，请按角色比例复核')
    if identifier == 'Arch_FuelStation_Canopy' and dimensions[2] < 2.5:
        warnings.append('油站顶棚高度不足 2.5 米，请按车辆比例复核')
    if identifier.startswith('Sec_Wall_Straight_'):
        warnings.append('双端柱与悬空墙板源件不可直接重复拼墙；Unity 围墙使用发布时派生的 SinglePost 模块')
    snap = None
    if identifier.startswith('Ground_Slab_'):
        snap = {'xMeters': dimensions[0], 'zMeters': dimensions[1]}
    elif identifier.startswith(('Sec_Wall_Straight_', 'Sec_Fence_Panel_')):
        snap = {'lengthMeters': dimensions[0], 'axis': 'X'}
    return {
        'category': category_name,
        'boundsMetersBlenderXYZ': {'min': [round(v, 4) for v in minimum],
                                   'max': [round(v, 4) for v in maximum]},
        'dimensionsMetersUnityXYZ': [dimensions[0], dimensions[2], dimensions[1]],
        'triangleCount': triangles,
        'pivot': 'bottom' if abs(minimum[2]) <= 0.01 else 'review',
        'snap': snap,
        'warnings': warnings,
    }


def audit_scene(scene):
    from .registration import require_registered
    require_registered(scene)
    objects = [obj for obj in scene.objects if obj.type == 'MESH'
               and obj.get('wgame_environment_asset')]
    if not objects:
        raise ValueError('没有场景模型组件')
    seen = set()
    depsgraph = bpy.context.evaluated_depsgraph_get()
    assets = []
    for obj in sorted(objects, key=lambda item: str(item['wgame_environment_asset'])):
        identifier = str(obj['wgame_environment_asset'])
        if identifier in seen:
            raise ValueError('重复的场景组件 ID：' + identifier)
        seen.add(identifier)
        evaluated = obj.evaluated_get(depsgraph)
        mesh = evaluated.to_mesh(preserve_all_data_layers=True, depsgraph=depsgraph)
        try:
            item = record(obj, mesh)
        finally:
            evaluated.to_mesh_clear()
        item['id'] = identifier
        assets.append(item)
    return {'format': 'mx.environmentUnityAssemblyAudit.v1',
            'unit': 'meter', 'sourceUp': 'Z', 'unityUp': 'Y',
            'assets': assets,
            'warningCount': sum(len(item['warnings']) for item in assets)}
