"""Texture-free, selected-mountain FBX publication; separate from the palette baker."""
import hashlib
import json
import re
import shutil
import uuid
from pathlib import Path

import bpy
from mathutils import Matrix, Vector

from . import authoring

FORMAT = 'mx.distantMountainVertexColor.v1'
ATTRIBUTE = 'DistantMountainColor'
MANIFEST = 'distant_mountains_vertex_build.json'
MODES = {
    'BakedColor': 'DistantMountains',
    'AlbedoAO': 'DistantMountainsAlbedoAO',
}


def is_work_file():
    if not bpy.data.filepath:
        return False
    root = authoring.repository().resolve()
    return Path(bpy.data.filepath).resolve().parent == root / 'ArtSource/Environment/DistantMountains/Blender_Work'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def color_mode(obj):
    mode = obj.get('wgame_mountain_color_mode')
    if mode in MODES:
        return mode
    names = {mat.name for mat in obj.data.materials if mat}
    if names == {'DistantMountain_VertexColor'}:
        return 'BakedColor'
    if names == {'DistantMountain_AlbedoAO_Preview'}:
        return 'AlbedoAO'
    raise ValueError('无法确认顶点颜色内容：' + obj.name)


def _copy_changed(source, destination):
    destination.parent.mkdir(parents=True, exist_ok=True)
    if not destination.is_file() or digest(source) != digest(destination):
        shutil.copy2(source, destination)


def export_selected(context):
    if not is_work_file() or context.mode != 'OBJECT':
        raise ValueError('请在远山制作文件的物体模式下导出')
    objects = sorted((o for o in context.selected_objects if o.type == 'MESH'), key=lambda o: o.name)
    if not objects:
        raise ValueError('请选择需要导出的顶点色山体')
    root = authoring.repository().resolve()
    work = Path(bpy.data.filepath).resolve()
    published = work.parent.parent / 'Final_Source'
    unity = root / 'Assets/Art/WGame/Environment/DistantMountains'
    manifest_path = published / MANIFEST
    previous = json.loads(manifest_path.read_text(encoding='utf-8')) if manifest_path.is_file() else {}
    if previous and previous.get('format') != FORMAT:
        raise ValueError('不支持的远山导出清单格式')
    old = {item['id']: item for item in previous.get('assets', [])}
    used = {p.stem for folder in (published / 'Pieces', unity / 'Pieces') for p in folder.glob('Mountain_*.fbx')}
    used.update(str(o.get('wgame_mountain_export_id')) for o in context.scene.objects if o.get('wgame_mountain_export_id'))
    plans = []
    for obj in objects:
        color = obj.data.color_attributes.get(ATTRIBUTE)
        if color is None or color.domain != 'POINT' or color.data_type != 'BYTE_COLOR':
            raise ValueError(obj.name + ' 需要 DistantMountainColor / POINT / BYTE_COLOR')
        if obj.data.uv_layers or obj.modifiers or obj.data.shape_keys:
            raise ValueError(obj.name + ' 必须为无 UV、无未应用修改器、无形态键的纯顶点色网格')
        if not obj.data.polygons:
            raise ValueError('网格没有面：' + obj.name)
        mode = color_mode(obj)
        identifier = obj.get('wgame_mountain_export_id')
        if identifier is None:
            number = 1
            while f'Mountain_{number:02d}' in used:
                number += 1
            identifier = f'Mountain_{number:02d}'
            used.add(identifier)
        if not re.fullmatch(r'Mountain_\d{2,}', identifier):
            raise ValueError('无效的远山导出编号：' + str(identifier))
        if any(p[1] == identifier for p in plans):
            raise ValueError('选中模型的导出编号重复：' + identifier)
        relative = 'Pieces/' + identifier + '.fbx'
        # Never overwrite an unowned or externally edited FBX.
        for folder in (published, unity):
            path = folder / relative
            if path.exists() and (identifier not in old or digest(path) != old[identifier]['sha256']):
                raise ValueError('目标已有非本次清单管理或被外部修改的模型：' + str(path))
        plans.append((obj, identifier, relative, mode))

    staging = root / 'Temp/DistantMountainVertexExport' / uuid.uuid4().hex
    staging.mkdir(parents=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(staging / 'before_export.blend'), copy=True)
    source_scene = context.window.scene
    export_scene = bpy.data.scenes.new('WGame_Vertex_Export_Temporary')
    records = []
    try:
        context.window.scene = export_scene
        for obj, identifier, relative, mode in plans:
            mesh = obj.data.copy()
            clone = bpy.data.objects.new(identifier, mesh)
            export_scene.collection.objects.link(clone)
            material = bpy.data.materials.new(MODES[mode])
            try:
                transform = obj.matrix_world.copy()
                transform.translation = Vector((0, 0, 0))
                mesh.transform(transform)
                low = Vector(tuple(min(v.co[i] for v in mesh.vertices) for i in range(3)))
                high = Vector(tuple(max(v.co[i] for v in mesh.vertices) for i in range(3)))
                origin = Vector(((low.x + high.x) * .5, (low.y + high.y) * .5, low.z))
                mesh.transform(Matrix.Translation(-origin))
                mesh.materials.clear()
                mesh.materials.append(material)
                for face in mesh.polygons:
                    face.material_index = 0
                for attr in list(mesh.color_attributes):
                    if attr.name != ATTRIBUTE:
                        mesh.color_attributes.remove(attr)
                mesh.color_attributes.active_color = mesh.color_attributes[ATTRIBUTE]
                mesh.color_attributes.render_color_index = 0
                mesh.update()
                clone.select_set(True)
                context.view_layer.objects.active = clone
                path = staging / relative
                path.parent.mkdir(parents=True, exist_ok=True)
                result = bpy.ops.export_scene.fbx(
                    filepath=str(path), use_selection=True, object_types={'MESH'},
                    axis_forward='-Z', axis_up='Y', apply_unit_scale=True,
                    apply_scale_options='FBX_SCALE_UNITS', use_space_transform=True,
                    bake_space_transform=True, use_mesh_modifiers=False,
                    use_triangles=True, use_tspace=False, bake_anim=False,
                    use_custom_props=False, colors_type='LINEAR', prioritize_active_color=True,
                    path_mode='AUTO', embed_textures=False)
                if 'FINISHED' not in result or not path.is_file():
                    raise RuntimeError('FBX 导出失败：' + obj.name)
                records.append(dict(id=identifier, sourceObject=obj.name, model=relative,
                                    sha256=digest(path), colorMode=mode, material=MODES[mode],
                                    vertexCount=len(mesh.vertices),
                                    triangleCount=sum(len(p.vertices) - 2 for p in mesh.polygons),
                                    dimensions=list(high - low), origin='bottomCenter',
                                    uvChannels=0, colorAttribute=ATTRIBUTE, colorSpace='linear'))
            finally:
                bpy.data.objects.remove(clone, do_unlink=True)
                bpy.data.meshes.remove(mesh)
                bpy.data.materials.remove(material)
    finally:
        context.window.scene = source_scene
        bpy.data.scenes.remove(export_scene)

    for obj, identifier, _, mode in plans:
        obj['wgame_mountain_export_id'] = identifier
        obj['wgame_mountain_color_mode'] = mode
    versions = context.preferences.filepaths.save_version
    try:
        context.preferences.filepaths.save_version = 0
        bpy.ops.wm.save_as_mainfile(filepath=str(work))
    finally:
        context.preferences.filepaths.save_version = versions
    for record in records:
        _copy_changed(staging / record['model'], published / record['model'])
        _copy_changed(staging / record['model'], unity / record['model'])
        old[record['id']] = record
    data = dict(format=FORMAT, source=work.relative_to(root).as_posix(),
                sourceSha256=digest(work), shader='WGame/DistantMountainVertexColor',
                assets=sorted(old.values(), key=lambda r: r['id']))
    manifest_path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    _copy_changed(manifest_path, unity / MANIFEST)
    return records, unity
