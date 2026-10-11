"""Binary contract tests; actual Blender/Unity imports are checked separately."""
import copy
from pathlib import Path
import runpy
import struct
import tempfile
import unittest

tool=runpy.run_path(str(Path(__file__).with_name('fbx_tangent_pruning.py')))


def node(name, value=None, children=()):
    props=b'' if value is None else b'I'+struct.pack('<i',value) if isinstance(value,int) else b'S'+struct.pack('<I',len(value))+value
    return {'name':name,'count':int(value is not None),'props':props,'children':list(children),'terminated':bool(children)}


def fixture(version=7400):
    geometry=node(b'Geometry',children=[node(b'Vertices',b'unchanged mesh payload')])
    for index,name in enumerate((b'UV0',b'HairFlow',b'HairMeta')):
        geometry['children'] += [node(b'LayerElementUV',index,[node(b'Name',name),node(b'UV',bytes([index])*7)])]
    for kind in sorted(tool['TSPACE']):
        geometry['children'] += [node(kind,index,[node(b'Payload',bytes([index])*11)]) for index in range(3)]
    for index in range(3):
        geometry['children'].append(node(b'Layer',index,[node(b'LayerElement',children=[node(b'Type',kind),node(b'TypedIndex',index)])
                                                       for kind in (b'LayerElementUV',*sorted(tool['TSPACE']))]))
    roots=[node(b'Objects',children=[geometry,node(b'Deformer',b'unchanged skinning')])]
    footer=b'footer marker!!!'+bytes(4)+bytes(16)+struct.pack('<I',version)+bytes(120)+bytes(16)
    # Footer marker is exactly 16 bytes, matching Blender's footer layout.
    assert len(footer)==176
    return tool['write_document'](roots,version,footer)


class TangentPruningTests(unittest.TestCase):
    def test_only_auxiliary_layers_and_references_change_and_repeat_is_noop(self):
        for version in (7400,7500):
            before=fixture(version);original,_,_=tool['read_document'](before)
            after,report=tool['optimize_bytes'](before)
            self.assertEqual(report['removedTangentLayers'],4)
            self.assertLess(len(after),len(before))
            expected=copy.deepcopy(original);geometry=expected[0]['children'][0]
            geometry['children']=[n for n in geometry['children'] if not(n['name'] in tool['TSPACE'] and tool['scalar'](n,b'I')>0)]
            for layer in geometry['children']:
                if layer['name']==b'Layer' and tool['scalar'](layer,b'I')>0:
                    layer['children']=[r for r in layer['children'] if tool['scalar'](tool['child'](r,b'Type'),b'S')==b'LayerElementUV']
            self.assertEqual(tool['read_document'](after)[0],expected)
            self.assertEqual(tool['optimize_bytes'](after)[0],after)

    def test_unknown_uv_contract_is_rejected_without_overwriting_file(self):
        with tempfile.TemporaryDirectory() as folder:
            path=Path(folder)/'legacy.fbx'
            before=fixture().replace(b'HairFlow',b'OtherUV1');path.write_bytes(before)
            with self.assertRaisesRegex(ValueError,'baked UV0'):tool['optimize_file'](path)
            self.assertEqual(path.read_bytes(),before)

    def test_truncation_and_missing_primary_basis_rejected(self):
        before=fixture()
        for invalid in (b'not fbx',before[:-20],before[:80]):
            with self.assertRaises((ValueError,struct.error)):tool['optimize_bytes'](invalid)
        roots,version,footer=tool['read_document'](before)
        g=roots[0]['children'][0]
        g['children']=[n for n in g['children'] if not(n['name']==b'LayerElementTangent' and tool['scalar'](n,b'I')==0)]
        with self.assertRaisesRegex(ValueError,'tangent layers'):
            tool['optimize_bytes'](tool['write_document'](roots,version,footer))


if __name__=='__main__':unittest.main()
