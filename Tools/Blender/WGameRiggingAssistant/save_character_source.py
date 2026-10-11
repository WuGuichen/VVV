"""Save the editable source before export, including unsaved image pixels."""
import hashlib
import json
import os
from pathlib import Path
import tempfile

import bpy


def save_before_publish(root, character_id):
    if len(character_id)!=6 or not character_id.isdecimal() or character_id=='000000':
        raise ValueError('保存制作源需要有效的六位角色编号')
    scene=bpy.context.scene
    if scene.get('wgame_baked_preview',False):
        raise ValueError('请先返回制作，再保存并发布角色')
    if not bpy.data.filepath:
        raise ValueError('请先将角色保存为 Work Blend，再发布')
    origin=Path(bpy.data.filepath).resolve()
    if origin.suffix.lower()!='.blend' or not origin.is_file():
        raise ValueError('当前制作源不是已保存的 Blend 文件')
    manifest_path=Path(root)/'ArtSource/Characters'/character_id/'authoring_manifest.json'
    manifest_bytes=manifest_path.read_bytes() if manifest_path.is_file() else None
    manifest=json.loads(manifest_bytes.decode('utf-8-sig')) if manifest_bytes else None
    if manifest:
        authority=(manifest.get('authority') or {}).get('blenderPath')
        if authority and (manifest_path.parent/authority).resolve()!=origin:
            raise ValueError('当前文件不是角色清单中的制作源，请打开正式 Work Blend')
    slots=[];images=[]
    try:
        # Authoring hair previews are temporary, too: save the original material
        # bindings while preserving the user's active preview in memory.
        for obj in scene.objects:
            if obj.type=='MESH' and obj.mode=='EDIT':obj.update_from_editmode()
            for slot in getattr(obj,'material_slots',()):
                if slot.material and slot.material.get('wgame_hair_preview_source'):
                    original=bpy.data.materials.get(slot.material['wgame_hair_preview_source'])
                    if original is None:raise ValueError('头发预览的原制作材质丢失，未保存或发布')
                    slots.append((slot,slot.material));slot.material=original
        for image in bpy.data.images:
            if image.source not in {'FILE','GENERATED'}:continue
            if not image.is_dirty and image.source!='GENERATED':continue
            if not all(image.size):raise ValueError('无法保存贴图：'+image.name)
            # Pack into the Work file; never overwrite a shared/external image.
            # Packing also refreshes an existing packed image after new paint.
            image.pack()
            if not image.packed_file:raise RuntimeError('贴图打包失败：'+image.name)
            images.append(image.name)
        result=bpy.ops.wm.save_as_mainfile(filepath=str(origin),copy=False,check_existing=False,relative_remap=False)
        if 'FINISHED' not in result or not origin.is_file():
            raise RuntimeError('制作源保存失败，已停止发布')
    finally:
        for slot,material in slots:slot.material=material
    content_hash='sha256:'+hashlib.sha256(origin.read_bytes()).hexdigest()
    if manifest:
        changed=False
        for item in manifest.get('items',[]):
            if item.get('path') and (manifest_path.parent/item['path']).resolve()==origin and item.get('sha256')!=content_hash:
                item['sha256']=content_hash;changed=True
        if changed:
            manifest['revision']=int(manifest.get('revision',0))+1
            temporary=None
            try:
                with tempfile.NamedTemporaryFile(mode='w',encoding='utf-8',dir=str(manifest_path.parent),prefix='.source-save-',suffix='.tmp',delete=False) as stream:
                    temporary=Path(stream.name)
                    stream.write(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')
                if manifest_path.read_bytes()!=manifest_bytes:
                    raise RuntimeError('制作清单在保存期间发生变化，请重试发布')
                os.replace(temporary,manifest_path)
            except Exception as error:
                raise RuntimeError('制作源已保存，但更新制作清单失败；未发布：'+str(error)) from error
            finally:
                if temporary and temporary.exists():temporary.unlink()
    return {'path':str(origin),'hash':content_hash,'packedImages':images}
