"""Export weapon source FBX and its manifest; never publish or refresh Unity.

Usage: blender -b <weapon.blend> -P Tools/Blender/export_weapon_fbx.py -- <output.fbx>
"""
from __future__ import annotations
import datetime
import json
from pathlib import Path
import runpy
import sys
import bpy

def main():
    source=Path(bpy.data.filepath).resolve()
    root=next((parent for parent in source.parents if (parent/'Authoring/WeaponPackages/weapon_id_catalog.json').is_file()),None)
    if root is None:raise RuntimeError('请打开仓库中的六位数字武器工程')
    parts=source.parts
    weapon_id=parts[parts.index('Weapons')+1] if 'Weapons' in parts else ''
    helper=runpy.run_path(str(root/'Tools/Blender/WGameWeaponTools/export_contract.py'))
    identity=helper['catalog_identity'](root,weapon_id)
    args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
    args=[arg for arg in args if arg!='--auto-trace']
    if len(args)>1:raise RuntimeError('Expected at most one output FBX path')
    weapon_root=root/'ArtSource/Common/Weapons'/weapon_id
    canonical=weapon_root/'model'/f'{weapon_id}.fbx'
    output=Path(args[0]).resolve() if args else canonical
    meshes,start,end,reach,palette=helper['export_fbx'](bpy.context,output)
    # An isolated validation export does not alter the production manifest.
    if output.resolve()==canonical.resolve():
        manifest_path=weapon_root/'weapon_sources.json'
        previous=json.loads(manifest_path.read_text(encoding='utf-8-sig')) if manifest_path.is_file() else None
        document=helper['weapon_manifest'](identity,start,end,reach,palette,datetime.datetime.now(datetime.timezone.utc).isoformat(),previous)
        manifest_path.write_text(json.dumps(document,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('WGAME_WEAPON_FBX_EXPORT '+json.dumps({'output':str(output),'bytes':output.stat().st_size,
        'meshes':[mesh.name for mesh in meshes],'reachMeters':reach,'traceStart':list(start),'traceEnd':list(end),
        'unityQueued':False},ensure_ascii=False))

if __name__=='__main__':main()
