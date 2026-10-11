"""Publish a repeatable single-post wall from the reviewed baked wall FBX."""

import hashlib
import json
import shutil
import subprocess
import tempfile
from pathlib import Path


SOURCE_ID = 'Sec_Wall_Straight_Long'
VARIANT_ID = 'Sec_Wall_Straight_Long_SinglePost'
SOURCE_PATH = 'Models/Security/' + SOURCE_ID + '.fbx'
VARIANT_PATH = 'Models/Security/' + VARIANT_ID + '.fbx'
DERIVATION_VERSION = 3


def _hash(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def publish_variant(package_root, blender_executable):
    package_root = Path(package_root).resolve()
    manifest_path = package_root / 'environment_build.json'
    document = json.loads(manifest_path.read_text(encoding='utf-8'))
    if document.get('format') != 'mx.environmentSharedBakedSurface.v2':
        raise ValueError('Unsupported environment publication')
    source_asset = next((item for item in document['assets'] if item['id'] == SOURCE_ID), None)
    source_file = next((item for item in document['files'] if item['path'] == SOURCE_PATH), None)
    source = package_root / SOURCE_PATH
    if source_asset is None or source_file is None or not source.is_file():
        raise ValueError('Reviewed baked wall is missing')
    source_hash = _hash(source)
    if source_hash != source_file['sha256']:
        raise ValueError('Reviewed baked wall has changed since publication')

    existing = next((item for item in document['assets'] if item['id'] == VARIANT_ID), None)
    target = package_root / VARIANT_PATH
    if (existing and existing.get('derivationVersion') == DERIVATION_VERSION
            and existing.get('derivedFromSha256') == source_hash
            and target.is_file()
            and any(item['path'] == VARIANT_PATH and item['sha256'] == _hash(target)
                    for item in document['files'])):
        return target, False

    script = Path(__file__).with_name('export_wall_variant.py')
    with tempfile.TemporaryDirectory(prefix='wall-assembly-') as directory:
        candidate = Path(directory) / (VARIANT_ID + '.fbx')
        command = [str(blender_executable), '--factory-startup', '--background',
                   '--disable-autoexec', '--python-exit-code', '1', '--python',
                   str(script), '--', '--source', str(source), '--output', str(candidate)]
        result = subprocess.run(command, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                                text=True, creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
        if result.returncode != 0:
            raise RuntimeError('Wall derivation failed:\n' + result.stdout[-4000:])
        report = json.loads(candidate.with_suffix('.report.json').read_text(encoding='utf-8'))
        if report['sourceSha256'] != source_hash or report['outputSha256'] != _hash(candidate):
            raise ValueError('Wall derivation hash mismatch')
        changed = not target.is_file() or _hash(target) != report['outputSha256']
        if changed:
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(candidate, target)

    bounds = report['boundsMetersBlenderXYZ']
    minimum, maximum = bounds['min'], bounds['max']
    size = [round(maximum[axis] - minimum[axis], 4) for axis in range(3)]
    variant = {
        'id': VARIANT_ID,
        'group': source_asset['group'],
        'model': VARIANT_PATH,
        'uv': 'UV0',
        'derivedFrom': SOURCE_ID,
        'derivedFromSha256': source_hash,
        'derivationVersion': DERIVATION_VERSION,
        'assembly': {
            'category': 'Security',
            'boundsMetersBlenderXYZ': bounds,
            'dimensionsMetersUnityXYZ': [size[0], size[2], size[1]],
            'triangleCount': report['triangleCount'],
            'pivot': 'bottom',
            'snap': {'lengthMeters': 8, 'axis': 'X'},
            'warnings': [],
        },
    }
    document['assets'] = [item for item in document['assets'] if item['id'] != VARIANT_ID]
    document['assets'].append(variant)
    document['files'] = [item for item in document['files'] if item['path'] != VARIANT_PATH]
    document['files'].append({'path': VARIANT_PATH, 'sha256': _hash(target)})
    for group in document['groups']:
        if group['id'] == source_asset['group'] and VARIANT_ID not in group['models']:
            group['models'].append(VARIANT_ID)
    temporary = manifest_path.with_suffix('.json.tmp')
    temporary.write_text(json.dumps(document, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    temporary.replace(manifest_path)
    return target, changed


def add_candidate_variant(candidate_root, document, blender_executable):
    """Add the derived wall before the reviewed candidate becomes authoritative."""
    candidate_root = Path(candidate_root).resolve()
    source_asset = next((item for item in document['assets'] if item['id'] == SOURCE_ID), None)
    if source_asset is None:
        raise ValueError('The baked candidate is missing the source wall')
    source_relative = source_asset['model']
    source = (candidate_root / source_relative).resolve()
    if candidate_root not in source.parents:
        raise ValueError('Invalid candidate wall path')
    source_file = next((item for item in document['files'] if item['path'] == source_relative), None)
    if source_file is None or _hash(source) != source_file['sha256']:
        raise ValueError('Candidate wall hash mismatch')

    with tempfile.TemporaryDirectory(prefix='wall-candidate-') as directory:
        package = Path(directory)
        staged_source = package / SOURCE_PATH
        staged_source.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, staged_source)
        staged_manifest = {
            'format': document['format'],
            'groups': [{'id': source_asset['group'], 'models': [SOURCE_ID]}],
            'assets': [{'id': SOURCE_ID, 'group': source_asset['group'],
                        'model': SOURCE_PATH, 'uv': 'UV0'}],
            'files': [{'path': SOURCE_PATH, 'sha256': source_file['sha256']}],
        }
        (package / 'environment_build.json').write_text(
            json.dumps(staged_manifest, indent=2) + '\n', encoding='utf-8')
        derivative, _ = publish_variant(package, blender_executable)
        staged_manifest = json.loads((package / 'environment_build.json').read_text(encoding='utf-8'))
        variant = next(item for item in staged_manifest['assets'] if item['id'] == VARIANT_ID)
        relative = str(Path(source_relative).parent / (VARIANT_ID + '.fbx')).replace('\\', '/')
        destination = candidate_root / relative
        shutil.copy2(derivative, destination)
        variant['model'] = relative
        document['assets'].append(variant)
        document['files'].append({'path': relative, 'sha256': _hash(destination)})
        for group in document['groups']:
            if group['id'] == source_asset['group']:
                group['models'].append(VARIANT_ID)
        return destination
