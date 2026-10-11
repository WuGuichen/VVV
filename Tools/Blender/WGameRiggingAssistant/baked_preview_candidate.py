"""Disk contract for temporary reviewed bakes; no Blender dependency."""
import hashlib
import json
from pathlib import Path

RECEIPT = 'bake_preview_receipt.json'
BINDING = 'character_baked_surface_binding.json'


def sha(path):
    return 'sha256:' + hashlib.sha256(Path(path).read_bytes()).hexdigest()


def validate_candidate(stage, root, character_id, input_hash=None):
    stage = Path(stage).resolve()
    if (Path(root).resolve() / 'Temp') not in stage.parents:
        raise ValueError('烘焙预览必须位于当前仓库的 Temp 目录')
    receipt = json.loads((stage / RECEIPT).read_text(encoding='utf-8'))
    if receipt.get('status') != 'ready' or receipt.get('packageId') != character_id:
        raise ValueError('烘焙预览未完成或属于其他角色')
    if input_hash is not None and receipt.get('inputHash') != input_hash:
        raise ValueError('制作内容或烘焙设置已变化，请重新“烘焙并预览”')
    expected = {character_id + '_LOD0.fbx', 'BaseColor.png', 'SurfaceData.png', BINDING, 'fbx_export_audit.json'}
    if set(receipt.get('files', {})) != expected:
        raise ValueError('烘焙预览回执不完整，请重新烘焙')
    for name, digest in receipt['files'].items():
        if not (stage / name).is_file() or sha(stage / name) != digest:
            raise ValueError('烘焙预览文件已变化或缺失：' + name)
    return receipt


def finish_candidate(stage, root, character_id, input_hash, source_path):
    stage = Path(stage)
    audit = json.loads((stage / 'fbx_export_audit.json').read_text(encoding='utf-8-sig'))
    if audit.get('status') not in {'Passed', 'PassedWithWarnings'}:
        raise ValueError('FBX 回读验收未通过')
    names = (character_id + '_LOD0.fbx', 'BaseColor.png', 'SurfaceData.png', BINDING, 'fbx_export_audit.json')
    receipt = {'status': 'ready', 'packageId': character_id, 'inputHash': input_hash,
               'sourcePath': str(Path(source_path).resolve()), 'files': {name: sha(stage / name) for name in names}}
    (stage / RECEIPT).write_text(json.dumps(receipt, ensure_ascii=False, indent=2), encoding='utf-8')
    return validate_candidate(stage, root, character_id, input_hash)
