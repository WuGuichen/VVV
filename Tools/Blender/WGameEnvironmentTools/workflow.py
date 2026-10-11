"""Candidate/review/publish around the existing character baker, grouped by image."""
import hashlib
import importlib.util
import json
import shutil
import subprocess
import tempfile
from pathlib import Path
import bpy
from bpy.app.handlers import persistent
from . import authoring, shared_detail, assembly, wall_assembly, emission

JOB={}
PREVIEW={}


def content_fingerprint(scene):
    root=authoring.repository();path=root/'Tools/Blender/WGameRiggingAssistant/baked_preview_inputs.py'
    spec=importlib.util.spec_from_file_location('environment_source_fingerprint',path)
    module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
    digest=hashlib.sha256(module.source_fingerprint(scene,root).encode())
    for obj in sorted(scene.objects,key=lambda o:o.name):
        digest.update(json.dumps([obj.name,{k:obj[k] for k in obj.keys() if k.startswith('wgame_environment_')}],sort_keys=True).encode())
    return digest.hexdigest()


def fingerprint(scene):
    digest=hashlib.sha256(content_fingerprint(scene).encode())
    root=authoring.repository()/'Tools/Blender/WGameEnvironmentTools'
    for name in ('authoring.py','shared_detail.py','emission.py','assembly.py','registration.py','group_bake.py',
                 'export_groups.py','wall_assembly.py','export_wall_variant.py'):
        digest.update((root/name).read_bytes())
    for name in ('baked_surface.py','baked_palette.py','baked_atlas.py','baked_atlas_packing.py'):
        digest.update((authoring.repository()/'Tools/Blender/WGameRiggingAssistant'/name).read_bytes())
    digest.update(str(getattr(scene,'wgame_environment_bake_size','AUTO')).encode())
    digest.update(str(getattr(scene,'wgame_environment_bake_device','AUTO')).encode())
    return digest.hexdigest()


def start(scene):
    if JOB.get('process') and JOB['process'].poll() is None:raise ValueError('已有烘焙正在进行')
    if scene.get('wgame_environment_pipeline')!=shared_detail.PIPELINE or PREVIEW:raise ValueError('请在共享细节制作文件中烘焙')
    shared_detail.validate(scene)
    root=authoring.repository();temp=root/'Temp/EnvironmentSurface';temp.mkdir(parents=True,exist_ok=True)
    directory=Path(tempfile.mkdtemp(prefix='group-candidate-',dir=temp))
    sig=fingerprint(scene)
    authoring.bake_module().snapshot(directory/'source.blend',getattr(scene,'wgame_environment_bake_size','AUTO'))
    log=(directory/'bake.log').open('w',encoding='utf-8')
    args=[bpy.app.binary_path,'--background','--factory-startup','--disable-autoexec','--python-exit-code','1',
          str(directory/'source.blend'),'--python',str(root/'Tools/Blender/WGameEnvironmentTools/export_groups.py'),
          '--','--output',str(directory/'build'),'--device',str(getattr(scene,'wgame_environment_bake_device','AUTO'))]
    process=subprocess.Popen(args,stdout=log,stderr=subprocess.STDOUT,creationflags=getattr(subprocess,'CREATE_NO_WINDOW',0))
    JOB.clear();JOB.update(process=process,log=log,directory=directory,scene=scene,fingerprint=sig,
                           reviewed=False,status='正在按共同细节图联合烘焙')
    bpy.app.timers.register(poll,first_interval=1)


def poll():
    if not JOB:return None
    if JOB['process'].poll() is None:
        lines=(JOB['directory']/'bake.log').read_text(encoding='utf-8',errors='replace').splitlines()
        group=next((line.split() for line in lines if line.startswith('GROUP_BAKE ')),None)
        total=int(group[2]) if group and len(group)>2 else 0
        prepared=sum(line.startswith('BAKE_UV_TILES ') for line in lines)
        exported=sum(line.startswith('FBX export starting') for line in lines)
        if exported:status=f'正在导出模型 {exported}/{total}'
        elif any(line.startswith('BAKE_ATLAS_COMPACT ') for line in lines):status='合图排布完成，正在最终烘焙'
        elif total and prepared>=total:status='模型准备完成，正在计算共享贴图'
        elif prepared:status=f'正在准备模型 {prepared}/{total}'
        else:status='正在准备烘焙快照'
        if status!=JOB['status']:
            JOB['status']=status
            for area in bpy.context.screen.areas if bpy.context.screen else []:area.tag_redraw()
        return 1
    JOB['log'].close()
    JOB['status']='烘焙完成，等待查看' if JOB['process'].returncode==0 and (JOB['directory']/'build/environment_build.json').is_file() else '烘焙失败，请查看 bake.log'
    for area in bpy.context.screen.areas if bpy.context.screen else []:area.tag_redraw()
    return None


def candidate():
    if not JOB or JOB['process'].poll() is None:raise ValueError('请先完成烘焙')
    path=JOB['directory']/'build/environment_build.json'
    if JOB['process'].returncode!=0 or not path.is_file():raise ValueError('烘焙失败，请查看 '+str(JOB['directory']/'bake.log'))
    if fingerprint(JOB['scene'])!=JOB['fingerprint']:raise ValueError('制作内容已改变，请重新烘焙')
    data=json.loads(path.read_text(encoding='utf-8'))
    if data['format']!='mx.environmentSharedBakedSurface.v2':raise ValueError('不是当前联合烘焙格式')
    for item in data['files']:
        source=(path.parent/item['path']).resolve()
        if path.parent.resolve() not in source.parents or hashlib.sha256(source.read_bytes()).hexdigest()!=item['sha256']:
            raise ValueError('候选文件已变化：'+item['path'])
    return path,data


def baked_material(group,root):
    module=authoring.palette_module();material=bpy.data.materials.new('Preview_'+group['id']);material.use_nodes=True
    tree=material.node_tree;nodes=tree.nodes;bsdf=nodes.get('Principled BSDF')
    uv=nodes.new('ShaderNodeUVMap');uv.uv_map='UV0'
    base=nodes.new('ShaderNodeTexImage');base.image=bpy.data.images.load(str(root/group['baseColor']),check_existing=False);base.extension='EXTEND'
    control=nodes.new('ShaderNodeTexImage');control.image=bpy.data.images.load(str(root/group['surfaceData']),check_existing=False)
    control.image.colorspace_settings.name='Non-Color';control.interpolation='Closest';control.extension='EXTEND'
    control.image.alpha_mode='CHANNEL_PACKED'
    tree.links.new(uv.outputs['UV'],base.inputs['Vector']);tree.links.new(uv.outputs['UV'],control.inputs['Vector'])
    tree.links.new(base.outputs['Color'],bsdf.inputs['Base Color'])
    for field,socket in (('roughness','Roughness'),('metallic','Metallic'),('specular','Specular IOR Level')):
        ramp=module._build_profile_ramp(nodes,'Baked '+field,field)
        tree.links.new(control.outputs['Alpha'],ramp.inputs['Fac']);tree.links.new(ramp.outputs['Color'],bsdf.inputs[socket])
    emission.connect_baked(tree,bsdf,base,control,float(group.get('emissionIntensity',1.0)))
    return material


def preview():
    path,data=candidate()
    if PREVIEW:return
    before={kind:set(getattr(bpy.data,kind)) for kind in ('objects','meshes','materials','images')}
    source=bpy.context.window.scene;views=[]
    for area in bpy.context.screen.areas:
        if area.type!='VIEW_3D':continue
        rv=area.spaces.active.region_3d
        views.append(dict(area=area,location=rv.view_location.copy(),rotation=rv.view_rotation.copy(),distance=rv.view_distance,perspective=rv.view_perspective,local=bool(area.spaces.active.local_view)))
        if area.spaces.active.local_view:
            with bpy.context.temp_override(area=area,region=next(r for r in area.regions if r.type=='WINDOW')):bpy.ops.view3d.localview(frame_selected=False)
    active=getattr(bpy.context,'object',None)
    source_mode=active.mode if active else 'OBJECT'
    if active and active.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
    scene=bpy.data.scenes.new('共享贴图联合烘焙预览 · 尚未发布')
    PREVIEW.update(source=source,scene=scene,before=before,views=views,selected=[o.name for o in bpy.context.selected_objects],active=active.name if active else '',mode=source_mode)
    scene.world=source.world;scene.display_settings.display_device=source.display_settings.display_device
    for field in ('view_transform','look','exposure','gamma'):setattr(scene.view_settings,field,getattr(source.view_settings,field))
    bpy.context.window.scene=scene
    try:
        materials={g['id']:baked_material(g,path.parent) for g in data['groups']}
        for index,asset in enumerate(data['assets']):
            bpy.ops.import_scene.fbx(filepath=str(path.parent/asset['model']))
            for obj in list(bpy.context.selected_objects):
                if obj.parent is None:obj.location.x+=(index%10)*26;obj.location.y+=(index//10)*26
                if obj.type=='MESH':obj.data.materials.clear();obj.data.materials.append(materials[asset['group']])
        for area in bpy.context.screen.areas:
            if area.type=='VIEW_3D':
                area.spaces.active.shading.type='MATERIAL'
                with bpy.context.temp_override(area=area,region=next(r for r in area.regions if r.type=='WINDOW')):bpy.ops.view3d.view_all(center=False)
        JOB['reviewed']=True;JOB['status']='已查看联合烘焙结果'
    except Exception:return_to_source();raise


@persistent
def return_to_source(*_):
    if not PREVIEW:return
    if bpy.context.window:bpy.context.window.scene=PREVIEW['source']
    bpy.data.scenes.remove(PREVIEW['scene'])
    for kind in ('objects','meshes','materials','images'):
        data=getattr(bpy.data,kind)
        for item in list(data):
            if item not in PREVIEW['before'][kind] and (kind=='objects' or item.users==0):data.remove(item,do_unlink=True)
    if bpy.context.window:
        for obj in bpy.context.scene.objects:obj.select_set(obj.name in PREVIEW.get('selected',[]))
        bpy.context.view_layer.objects.active=bpy.context.scene.objects.get(PREVIEW.get('active',''))
        for view in PREVIEW.get('views',[]):
            try:
                area=view['area'];rv=area.spaces.active.region_3d
                if view['local'] and not area.spaces.active.local_view:
                    with bpy.context.temp_override(area=area,region=next(r for r in area.regions if r.type=='WINDOW')):bpy.ops.view3d.localview(frame_selected=False)
                rv.view_location=view['location'];rv.view_rotation=view['rotation'];rv.view_distance=view['distance'];rv.view_perspective=view['perspective']
            except (ReferenceError,RuntimeError,AttributeError):pass
        if bpy.context.object and PREVIEW.get('mode','OBJECT')!='OBJECT':
            bpy.ops.object.mode_set(mode=PREVIEW['mode'])
    PREVIEW.clear()


@persistent
def before_save(*_):
    return_to_source()
    scene=bpy.context.scene
    if scene.get('wgame_environment_pipeline')==shared_detail.PIPELINE:
        for image in shared_detail.images_for_save(scene):image.pack()


def save_source():
    scene=bpy.context.scene;work=Path(bpy.data.filepath).resolve()
    if not scene.get('wgame_environment_work') or 'Blender_Work' not in work.parts:raise ValueError('先在 Blender_Work 中保存场景制作文件')
    images=shared_detail.images_for_save(scene)
    directory=work.parent.parent/'Shared';directory.mkdir(parents=True,exist_ok=True)
    for image in images:
        if not image.filepath:
            identifier=str(image.get('wgame_detail_source_id',hashlib.sha256(image.name.encode()).hexdigest()[:12]))
            prefix='emission_' if emission.is_mask(image) else 'detail_'
            image.filepath_raw=str(directory/(prefix+identifier+'.png'));image.file_format='PNG'
        image.save();image.pack()
    previous=bpy.context.preferences.filepaths.save_version
    try:
        bpy.context.preferences.filepaths.save_version=0
        result=bpy.ops.wm.save_as_mainfile(filepath=str(work),compress=True)
        if 'FINISHED' not in result:raise ValueError('制作文件保存失败')
    finally:bpy.context.preferences.filepaths.save_version=previous


def audit_for_unity(scene):
    if PREVIEW:raise ValueError('请先返回制作场景')
    if scene.get('wgame_environment_pipeline')!=shared_detail.PIPELINE:
        raise ValueError('请打开共享细节场景制作文件')
    report=assembly.audit_scene(scene)
    output=authoring.repository()/'Temp/EnvironmentSurface/assembly_audit.json'
    output.parent.mkdir(parents=True,exist_ok=True)
    output.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    return report,output


def _published_path(root,relative):
    path=(root/relative).resolve()
    if root.resolve() not in path.parents:raise ValueError('Invalid export path: '+relative)
    return path


def _write_package(source_root,source_document,target,work):
    document,path_map=assembly.stable_package(source_document)
    target.mkdir(parents=True,exist_ok=True)
    previous_manifest=target/'environment_build.json'
    previous=json.loads(previous_manifest.read_text(encoding='utf-8')) if previous_manifest.is_file() else {}
    old_files={item['path']:item['sha256'] for item in previous.get('files',[])}
    source_hashes={item['path']:item['sha256'] for item in source_document['files']}
    for old_relative in path_map:
        source=_published_path(source_root,old_relative)
        if not source.is_file() or hashlib.sha256(source.read_bytes()).hexdigest()!=source_hashes[old_relative]:
            raise ValueError('发布源文件已变化：'+old_relative)
    changed=0
    for old_relative,new_relative in path_map.items():
        destination=_published_path(target,new_relative)
        if destination.is_file() and hashlib.sha256(destination.read_bytes()).hexdigest()==source_hashes[old_relative]:continue
        destination.parent.mkdir(parents=True,exist_ok=True)
        shutil.copy2(_published_path(source_root,old_relative),destination);changed+=1
    document['source']=str(work);document['sourceSha256']=hashlib.sha256(work.read_bytes()).hexdigest()
    temporary=target/'environment_build.json.tmp'
    temporary.write_text(json.dumps(document,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    temporary.replace(previous_manifest)
    new_files={item['path'] for item in document['files']}
    removed=0
    for relative,old_hash in old_files.items():
        if relative in new_files:continue
        stale=_published_path(target,relative)
        if not stale.is_file() or hashlib.sha256(stale.read_bytes()).hexdigest()!=old_hash:continue
        stale.unlink();removed+=1
        parent=stale.parent
        while parent!=target and parent.is_dir():
            try:parent.rmdir()
            except OSError:break
            parent=parent.parent
    return changed,removed


def publish():
    path,data=candidate()
    if not JOB['reviewed']:raise ValueError('请先查看本次联合烘焙')
    wall_assembly.add_candidate_variant(path.parent,data,bpy.app.binary_path)
    return_to_source();save_source()
    work=Path(bpy.data.filepath).resolve();target=work.parent.parent/'Final_Source';target.mkdir(parents=True,exist_ok=True)
    changed,removed=_write_package(path.parent,data,target,work)
    JOB['fingerprint']=fingerprint(JOB['scene'])
    JOB['status']=f'已发布 Unity 拼装源包：更新 {changed}，清理旧文件 {removed}'
    return target


def migrate_published(scene):
    """One-time path migration of a reviewed package; never rebakes or edits Work."""
    if scene.get('wgame_environment_pipeline')!=shared_detail.PIPELINE:
        raise ValueError('请打开共享细节场景制作文件')
    work=Path(bpy.data.filepath).resolve();target=work.parent.parent/'Final_Source'
    manifest=target/'environment_build.json'
    if not manifest.is_file():raise ValueError('没有已发布的环境源包')
    data=json.loads(manifest.read_text(encoding='utf-8'))
    if data['sourceSha256']!=hashlib.sha256(work.read_bytes()).hexdigest():
        raise ValueError('制作文件与已发布源包不一致，请重新烘焙、查看并发布')
    if all(item.get('publishKey') for item in data['groups']):return 0,0
    from .group_bake import detail_image,groups_for
    objects=[obj for obj in scene.objects if obj.type=='MESH' and obj.get('wgame_environment_asset')]
    groups=groups_for(objects)
    keys={key:assembly.image_publish_key(detail_image(values[0])) for key,values in groups.items()}
    if set(keys)!={item['id'] for item in data['groups']}:
        raise ValueError('细节图分组与已发布源包不一致，请重新烘焙')
    audit={item['id']:item for item in assembly.audit_scene(scene)['assets']}
    for group in data['groups']:group['publishKey']=keys[group['id']]
    for asset in data['assets']:
        if asset['id'] not in audit:raise ValueError('发布组件在制作文件中已不存在：'+asset['id'])
        asset['assembly']={key:value for key,value in audit[asset['id']].items() if key!='id'}
    return _write_package(target,data,target,work)


def sync_to_unity():
    """Copy the reviewed publication into Assets without changing Unity GUIDs."""
    root=authoring.repository()
    source=root/'ArtSource/Environment/Base/Final_Source'
    manifest=source/'environment_build.json'
    if not manifest.is_file():raise ValueError('尚未发布场景源包，请先联合烘焙、查看并发布')
    data=json.loads(manifest.read_text(encoding='utf-8'))
    if data.get('format')!='mx.environmentSharedBakedSurface.v2':
        raise ValueError('不支持的场景发布格式')
    target=root/'Assets/Art/WGame/Environment/Base/Published'
    files=data.get('files',[])
    if not files or len({item['path'] for item in files})!=len(files):
        raise ValueError('发布清单缺少文件或路径重复')
    for item in files:
        path=_published_path(source,item['path'])
        if not path.is_file() or hashlib.sha256(path.read_bytes()).hexdigest()!=item['sha256']:
            raise ValueError('发布文件缺失或哈希不符：'+item['path'])
    previous_manifest=target/'environment_build.json'
    previous=json.loads(previous_manifest.read_text(encoding='utf-8')) if previous_manifest.is_file() else {}
    old_paths={item['path'] for item in previous.get('files',[])}
    new_paths={item['path'] for item in files}
    stale=sorted(old_paths-new_paths)
    changed=0
    for item in files:
        destination=_published_path(target,item['path'])
        if destination.is_file() and hashlib.sha256(destination.read_bytes()).hexdigest()==item['sha256']:
            continue
        destination.parent.mkdir(parents=True,exist_ok=True)
        shutil.copy2(_published_path(source,item['path']),destination)
        changed+=1
    target.mkdir(parents=True,exist_ok=True)
    if not previous_manifest.is_file() or previous_manifest.read_bytes()!=manifest.read_bytes():
        shutil.copy2(manifest,previous_manifest)
        changed+=1
    return target,changed,stale
