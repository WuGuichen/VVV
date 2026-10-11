"""Background regression against an exported mesh/hands JSON fixture.

Run Blender --background --factory-startup --python-exit-code 1 --python this_file -- fixture.json
Fixture fields: vertices, edges, faces, meshMatrix, rigMatrix, hands[l/r][head/tail].
"""
import importlib.util
import json
from pathlib import Path
import sys
import tempfile

import bpy
from mathutils import Matrix


def main():
    assert bpy.app.background, 'Use a separate background Blender'
    fixture = json.loads(Path(sys.argv[sys.argv.index('--') + 1]).read_text(encoding='utf-8'))
    spec = importlib.util.spec_from_file_location('finger_chain_test', Path(__file__).with_name('__init__.py'))
    addon = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(addon)
    addon.register()
    try:
        mesh = bpy.data.meshes.new('FingerFixture')
        mesh.from_pydata(fixture['vertices'], fixture['edges'], fixture['faces'])
        obj = bpy.data.objects.new('FingerFixture', mesh)
        bpy.context.collection.objects.link(obj)
        obj.matrix_world = Matrix(fixture['meshMatrix'])
        armature = bpy.data.armatures.new('rig')
        rig = bpy.data.objects.new('rig', armature)
        bpy.context.collection.objects.link(rig)
        rig['wgame_rig_kind'] = 'deform_skeleton'
        rig.matrix_world = Matrix(fixture['rigMatrix'])
        addon._select_only(bpy.context, rig)
        bpy.ops.object.mode_set(mode='EDIT')
        for side, data in fixture['hands'].items():
            bone = armature.edit_bones.new('hand.' + side)
            bone.head, bone.tail = data['head'], data['tail']
        bpy.ops.object.mode_set(mode='OBJECT')
        settings = bpy.context.scene.wgame_rigging_assistant
        settings.mesh_name = obj.name
        geometry = addon._hand_hint_fingerprint(obj)
        for side in ('l', 'r'):
            analysis = addon._hand_geodesic_analysis(obj, rig, side)
            chains, owners, _counts = addon._fit_finger_chains(analysis)
            addon._validate_finger_chain_fit(obj, analysis, chains, owners)
            # The old failure: a thumb chain fitted onto another digit must be rejected.
            crossed = dict(chains)
            crossed['thumb'] = chains['index']
            try:
                addon._validate_finger_chain_fit(obj, analysis, crossed, owners)
                raise AssertionError('cross-finger chain passed validation')
            except ValueError:
                pass
        with tempfile.TemporaryDirectory(prefix='finger-chain-test-') as temporary:
            addon._save_backup_copy = lambda: str(Path(temporary) / 'backup.blend')
            bpy.ops.wm.save_as_mainfile(filepath=addon._save_backup_copy())
            assert bpy.ops.wgame_rigging.generate_finger_bones() == {'FINISHED'}
            first = {b.name: addon._bone_snapshot_entry(b) for b in rig.data.bones}
            assert len(first) == 32
            assert not addon._finger_structure_errors(rig, settings.wgame_humanoid_mapping_json)
            assert bpy.ops.wgame_rigging.generate_finger_bones() == {'FINISHED'}
            assert first == {b.name: addon._bone_snapshot_entry(b) for b in rig.data.bones}
            # Rejecting a plan before commit must preserve all previous finger bones.
            original_validator = addon._validate_finger_chain_fit
            addon._validate_finger_chain_fit = lambda *_args: (_ for _ in ()).throw(ValueError('injected fit failure'))
            try:
                bpy.ops.wgame_rigging.generate_finger_bones()
            except RuntimeError:
                pass
            finally:
                addon._validate_finger_chain_fit = original_validator
            assert first == {b.name: addon._bone_snapshot_entry(b) for b in rig.data.bones}
            assert addon._hand_hint_fingerprint(obj) == geometry
        print('WG_FINGER_CHAINS_OK both_hands branch_containment cross_finger_rejection repeat preservation')
    finally:
        addon.unregister()


if __name__ == '__main__':
    main()
