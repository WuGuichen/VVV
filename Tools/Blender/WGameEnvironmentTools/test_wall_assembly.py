"""Exercise the reviewed-wall derivative without editing Work or Final_Source."""

import hashlib
import json
import shutil
import sys
import tempfile
from pathlib import Path

import bpy

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from WGameEnvironmentTools import assembly, authoring, wall_assembly


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


published = authoring.repository() / 'ArtSource/Environment/Base/Final_Source'
manifest = json.loads((published / 'environment_build.json').read_text(encoding='utf-8'))
source = published / wall_assembly.SOURCE_PATH
group = next(item for item in manifest['groups'] if wall_assembly.SOURCE_ID in item['models'])
key = 'wall-test-group'
with tempfile.TemporaryDirectory(prefix='wall-assembly-test-') as directory:
    root = Path(directory)
    part = root / key
    part.mkdir()
    for name, original in ((wall_assembly.SOURCE_ID + '.fbx', source),
                           ('BaseColor.png', published / group['baseColor']),
                           ('SurfaceData.png', published / group['surfaceData'])):
        shutil.copy2(original, part / name)
    files = [{'path': key + '/' + path.name, 'sha256': sha(path)} for path in part.iterdir()]
    candidate = {
        'format': manifest['format'],
        'groups': [{'id': key, 'publishKey': group['id'],
                    'models': [wall_assembly.SOURCE_ID],
                    'baseColor': key + '/BaseColor.png',
                    'surfaceData': key + '/SurfaceData.png'}],
        'assets': [{'id': wall_assembly.SOURCE_ID, 'group': key,
                    'model': key + '/' + wall_assembly.SOURCE_ID + '.fbx', 'uv': 'UV0'}],
        'files': files,
    }
    variant = wall_assembly.add_candidate_variant(root, candidate, bpy.app.binary_path)
    assert variant.is_file() and sha(variant) == next(item['sha256'] for item in candidate['files']
                                                     if item['path'] == variant.relative_to(root).as_posix())
    bpy.ops.import_scene.fbx(filepath=str(variant))
    wall = bpy.data.objects[wall_assembly.VARIANT_ID].data
    broad_faces = []
    for face in wall.polygons:
        vertices = [wall.vertices[index].co for index in face.vertices]
        if (abs(face.normal.y) > 0.8 and max(vertex.x for vertex in vertices)
                - min(vertex.x for vertex in vertices) > 2
                and max(vertex.z for vertex in vertices) > 1):
            broad_faces.append(min(vertex.z for vertex in vertices))
    assert broad_faces and min(broad_faces) <= 0.05, 'Main wall panel still floats above grade'
    assert len(candidate['assets']) == 2 and len(candidate['files']) == 4
    assert candidate['assets'][1]['derivationVersion'] == wall_assembly.DERIVATION_VERSION
    document, paths = assembly.stable_package(candidate)
    assert document['assets'][1]['model'] == wall_assembly.VARIANT_PATH
    assert len(paths) == len(set(paths.values())) == 4
    assert sha(source) == next(item['sha256'] for item in manifest['files']
                               if item['path'] == wall_assembly.SOURCE_PATH)
print('PASS reviewed wall derivative and stable Unity path', flush=True)
