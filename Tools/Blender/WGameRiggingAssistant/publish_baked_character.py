"""Publish an intact reviewed temporary bake. This command never rebakes."""
import argparse
import datetime
import json
import os
from pathlib import Path
import runpy
import shutil
import subprocess
import sys
import uuid


def run_logged(command, root, log_path):
    # Hidden Blender processes do not reliably pass console handles to dotnet.
    with log_path.open('w', encoding='utf-8') as output:
        try:
            subprocess.run(command, cwd=str(root), check=True, stdout=output,
                           stderr=subprocess.STDOUT,
                           creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
        except subprocess.CalledProcessError as error:
            output.flush()
            detail=log_path.read_text(encoding='utf-8', errors='replace')[-8000:]
            raise RuntimeError(f'发布工具执行失败（退出码 {error.returncode}），日志：{log_path}\n{detail}') from error


def build_import_tool(root, stage):
    cli=root/'Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj'
    output=stage/'authoring-cli'
    # CharacterStudio may be running from the project's default bin directory.
    # Isolate both intermediates and binaries, and run exactly this fresh build.
    run_logged(['dotnet','build',str(cli),'--nologo','--artifacts-path',str(stage/'build-artifacts'),
                '--output',str(output)],root,stage/'authoring-build.log')
    assembly=output/'authoring.dll'
    if not assembly.is_file():raise RuntimeError('发布工具编译后未生成 '+str(assembly))
    return assembly


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--repo',required=True);parser.add_argument('--character',required=True)
    parser.add_argument('--candidate',required=True);parser.add_argument('--expected-input-hash',required=True)
    parser.add_argument('--source-hash',required=True)
    parser.add_argument('--attempt-id')
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    root=Path(args.repo).resolve();cid=args.character
    if len(cid)!=6 or not cid.isdecimal():raise ValueError('Invalid character ID')
    candidate=Path(args.candidate).resolve()
    attempt=args.attempt_id or uuid.uuid4().hex
    if uuid.UUID(hex=attempt).hex!=attempt:raise ValueError('Invalid publish attempt ID')
    stage=candidate/('publish_'+attempt)
    package=root/'Authoring/CharacterPackages'/('character-'+cid)
    final=root/'ArtSource/Characters'/cid/'Final_Source'
    if not (package/'manifest.json').is_file():raise ValueError('Character package is missing')
    tool=Path(__file__).parent
    policy=runpy.run_path(str(tool/'character_surface_publish_policy.py'))
    policy['resolve_surface_publish_mode'](package,True)
    baker=runpy.run_path(str(tool/'baked_surface.py'))
    candidate_tools=runpy.run_path(str(tool/'baked_preview_candidate.py'))
    receipt=candidate_tools['validate_candidate'](candidate,root,cid,args.expected_input_hash)
    origin=Path(receipt['sourcePath']).resolve()
    authoring_manifest=root/'ArtSource/Characters'/cid/'authoring_manifest.json'
    if authoring_manifest.exists():
        manifest=json.loads(authoring_manifest.read_text(encoding='utf-8-sig'))
        authority=(manifest.get('authority') or {}).get('blenderPath','')
        if authority and origin!=(authoring_manifest.parent/authority).resolve():
            raise ValueError('Reviewed bake does not belong to the declared Work Blend')
    if not origin.is_file() or candidate_tools['sha'](origin)!=args.source_hash:
        raise ValueError('Saved source changed after publish was requested')
    stage.mkdir()
    marker=candidate/'publish_attempt.json'
    temporary=marker.with_suffix('.tmp')
    temporary.write_text(json.dumps({'attemptId':attempt,'packageId':cid,'sourcePath':str(origin)}),encoding='utf-8')
    os.replace(temporary,marker)
    # Read the formal identity directory before build, backup, or formal output writes.
    state=runpy.run_path(str(tool.parent/'WGamePaletteTools/baked_task_state.py'))
    state['require_runtime_identity'](root,cid)
    for name in receipt['files']:shutil.copy2(candidate/name,stage/name)
    # Only provenance changes after saving the reviewed source. Model/texture bytes stay intact.
    binding_path=stage/baker['BINDING']
    binding=json.loads(binding_path.read_text(encoding='utf-8-sig'))
    binding['authoringSourceHash']=args.source_hash
    binding_path.write_text(json.dumps(binding,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    fbx=stage/(cid+'_LOD0.fbx')
    print('WGAME_PUBLISH_PROGRESS '+json.dumps({'stage':2,'message':'正在编译发布工具'}),flush=True)
    cli=build_import_tool(root,stage)
    backup=stage/('previous_success_'+datetime.datetime.now().strftime('%Y%m%d_%H%M%S_%f'))
    shutil.copytree(package,backup/'package')
    if final.exists():shutil.copytree(final,backup/'final_source')
    catalog_path=package/'resource_catalog.json'
    catalog=json.loads(catalog_path.read_text(encoding='utf-8-sig'))
    models=[r for r in catalog['entries'] if r['resourceKey']==f'char.{cid}.model.body']
    if len(models)!=1:raise ValueError('A single body model resource is required')
    body=models[0];model_path=package/body['relativePath']
    if package.resolve() not in model_path.resolve().parents:raise ValueError('Body model path escapes package')
    try:
        print('WGAME_PUBLISH_PROGRESS '+json.dumps({'stage':3,'message':'正在更新正式产物与角色包'}),flush=True)
        final.mkdir(parents=True,exist_ok=True)
        for name in (fbx.name,'BaseColor.png','SurfaceData.png',baker['BINDING']):shutil.copy2(stage/name,final/name)
        shutil.copy2(fbx,model_path)
        body['hash']=baker['sha'](fbx)
        body['hashes']={'algorithm':'sha256','contentHash':body['hash'],'importHash':'','dependencyHash':''}
        body.setdefault('conflictPolicy',{})['allowOverwrite']=True
        catalog_path.write_text(json.dumps(catalog,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
        baker['publish_package'](stage,package,body['relativePath'])
        print('WGAME_PUBLISH_PROGRESS '+json.dumps({'stage':4,'message':'正在导入生成资源'}),flush=True)
        run_logged(['dotnet',str(cli),'character','import-unity','--package',str(package),'--project-root',str(root),
                    '--check-files','--check-hashes','--build-target','StandaloneWindows64'],root,stage/'import-unity.log')
        generated='Assets/MxFrameworkGenerated/CharacterPackages'
        imported=json.loads((root/generated/cid/'package_cache/import_report.json').read_text(encoding='utf-8-sig'))
        request_id=uuid.uuid4().hex
        request={'requestId':request_id,'createdUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),
                 'package':package.relative_to(root).as_posix(),'generatedRoot':generated,'packageId':cid}
        for key in ('sourcePackageHash','generatedConfigHash','geometryBindingHash','resourceMappingHash','writePlanHash'):
            request[key]=imported[key]
        request_path=root/'Temp/MxFrameworkAuthoring/CharacterImport/rebuild_request.json'
        request_path.parent.mkdir(parents=True,exist_ok=True)
        temp=request_path.with_suffix('.tmp');temp.write_text(json.dumps(request,indent=2),encoding='utf-8');os.replace(temp,request_path)
        result_path=candidate/'publish_result.json';result_temp=result_path.with_suffix('.tmp')
        result_temp.write_text(json.dumps({'status':'pendingUnity','attemptId':attempt,'requestId':request_id,'textureSize':json.loads((stage/baker['BINDING']).read_text())['textureSize']},indent=2),encoding='utf-8')
        os.replace(result_temp,result_path)
    except Exception:
        # Restore bytes and remove only files created within the explicit package/final roots.
        for target,previous in ((package,backup/'package'),(final,backup/'final_source')):
            if root not in target.resolve().parents or target.resolve()==root:
                raise RuntimeError('Refusing rollback outside the character workspace')
            before={p.relative_to(previous) for p in previous.rglob('*') if p.is_file()} if previous.exists() else set()
            for p in target.rglob('*'):
                if p.is_file() and p.relative_to(target) not in before:p.unlink()
            if previous.exists():shutil.copytree(previous,target,dirs_exist_ok=True)
        raise
    print('WGAME_PUBLISH_PROGRESS '+json.dumps({'stage':5,'message':'资源已导出，等待 Unity 重建'}),flush=True)
    print('BAKED_CHARACTER_PUBLISHED',cid,request_id)


if __name__=='__main__':main()
