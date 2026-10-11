"""Bake/audit a snapshot into Temp only. Never publishes or starts Unity import."""
import argparse
import json
from pathlib import Path
import runpy
import subprocess
import sys
import bpy


def main():
    print('WGAME_BAKE_PROGRESS '+json.dumps({'stage':1,'message':'正在读取烘焙请求'}),flush=True)
    parser = argparse.ArgumentParser()
    parser.add_argument('--repo', required=True)
    parser.add_argument('--character', required=True)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    root = Path(args.repo).resolve();stage = Path(bpy.data.filepath).parent.resolve()
    if root / 'Temp' not in stage.parents:raise ValueError('Preview stage must be under repository Temp')
    request = json.loads((stage / 'preview_request.json').read_text(encoding='utf-8'))
    if request['packageId'] != args.character:raise ValueError('Preview request belongs to another character')
    tool = Path(__file__).parent;fbx = stage / (args.character + '_LOD0.fbx');audit = stage / 'fbx_export_audit.json'
    sys.argv = ['blender', '--', str(fbx), '--baked-surface']
    print('WGAME_BAKE_PROGRESS '+json.dumps({'stage':2,'message':'正在检查材质、模型与 UV'}),flush=True)
    runpy.run_path(str(tool / 'export_unity_fbx.py'), run_name='__main__')
    print('WGAME_BAKE_PROGRESS '+json.dumps({'stage':6,'message':'正在重新导入 FBX 并检查导出结果'}),flush=True)
    subprocess.run([bpy.app.binary_path, '--background', '--factory-startup', '--python-exit-code', '17', '--python',
                    str(tool / 'audit_unity_fbx.py'), '--', str(fbx), str(audit), args.character, '--baked-surface'],
                   check=True, creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
    candidate = runpy.run_path(str(tool / 'baked_preview_candidate.py'))
    print('WGAME_BAKE_PROGRESS '+json.dumps({'stage':6,'message':'正在校验预览文件与哈希'}),flush=True)
    candidate['finish_candidate'](stage, root, args.character, request['inputHash'], request['sourcePath'])
    print('BAKED_PREVIEW_READY', args.character, flush=True)


if __name__ == '__main__':main()
