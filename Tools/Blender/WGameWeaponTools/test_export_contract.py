import copy
import json
from pathlib import Path
from types import SimpleNamespace
import unittest
import export_contract as contract

class ContractTests(unittest.TestCase):
    def setUp(self):
        self.identity={'id':'000002','displayName':'C006_1 魔导法杖','category':'Catalyst','weaponTypeId':'staff'}
    def test_new_staff_has_no_sword_behavior(self):
        item=contract.single_configuration(self.identity,(0,0,0),(0,0,.93),.93)
        self.assertEqual(item['category'],'Catalyst')
        self.assertEqual(item['weaponTypeId'],'staff')
        self.assertEqual(item['tags'],[])
        self.assertEqual(item['combatConfiguration']['combatTags'],[])
        self.assertEqual(item['traceProfiles'][0]['actionKeys'],[])
        self.assertEqual(item['traceProfiles'][0]['endPose']['position'],{'x':0,'y':.93,'z':0})
    def test_existing_vfx_and_other_traces_survive(self):
        item=contract.single_configuration(self.identity,(0,0,0),(0,0,.93),.93)
        item['presentationConfiguration']['bladeTrailResourceKey']='vfx.weapon.katana_trail:GameObject@mxframework.samples'
        item['traceProfiles'][0].update(radius=.027,actionKeys=['existing'])
        item['traceProfiles'].append({'traceId':'trace.secondary','radius':.02})
        before=copy.deepcopy(item)
        updated=contract.single_configuration(self.identity,(0,0,.1),(0,0,1),.9,item)
        self.assertEqual(item,before)
        self.assertEqual(updated['presentationConfiguration'],before['presentationConfiguration'])
        self.assertEqual(updated['traceProfiles'][0]['radius'],.027)
        self.assertEqual(updated['traceProfiles'][0]['actionKeys'],['existing'])
        self.assertEqual(updated['traceProfiles'][1],before['traceProfiles'][1])
    def test_manifest_keeps_authoring_and_texture(self):
        previous={'stage':'source','authoring':{'blenderWorkPath':'Work'},'items':[{'id':'albedo','target':{'resourceKey':'weapon.000002.texture.albedo'}}]}
        manifest=contract.weapon_manifest(self.identity,(0,0,0),(0,0,.93),.93,True,'now',previous)
        self.assertEqual(manifest['authoring'],previous['authoring'])
        self.assertEqual(len(manifest['items']),2)
        self.assertEqual(manifest['items'][0]['target']['importHint']['metadata']['traceBladeTip'],'0.0000,0.9300,0.0000')
        self.assertEqual(previous['stage'],'source')
    def test_active_mesh_does_not_limit_parts(self):
        def mesh(name,hidden=False):
            return SimpleNamespace(name=name,type='MESH',hide_get=lambda:hidden,hide_viewport=False)
        objects=[mesh('shaft'),mesh('cage'),mesh('shaft__BACKUP'),mesh('hidden',True)]
        context=SimpleNamespace(scene=SimpleNamespace(objects=objects),active_object=objects[0])
        self.assertEqual([obj.name for obj in contract.collect_weapon_meshes(context)],['cage','shaft'])
    def test_registry_identity_requires_active_allocation(self):
        import tempfile
        with tempfile.TemporaryDirectory() as directory:
            path=Path(directory)/'Authoring/WeaponPackages';path.mkdir(parents=True)
            (path/'weapon_id_catalog.json').write_text(json.dumps({'entries':[dict(self.identity,status='active')]}),encoding='utf-8')
            source=Path(directory)/'WeaponSource';source.mkdir()
            (source/'global_weapon_library.json').write_text(json.dumps({'weaponTypes':[{'typeId':'staff','equipmentCategory':'Catalyst'}]}),encoding='utf-8')
            self.assertEqual(contract.catalog_identity(directory,'000002')['category'],'Catalyst')
            self.assertEqual(contract.catalog_identity(directory,'000002')['weaponTypeId'],'staff')
            with self.assertRaises(ValueError):contract.catalog_identity(directory,'000003')
    def test_existing_type_mismatch_refuses_export(self):
        previous=contract.single_configuration(self.identity,(0,0,0),(0,0,.93),.93)
        previous['weaponTypeId']='sword'
        with self.assertRaisesRegex(ValueError,'WeaponIdentityTypeProjectionMismatch'):
            contract.single_configuration(self.identity,(0,0,0),(0,0,1),1,previous)
    def test_numeric_catalog_cannot_define_another_mapping(self):
        import tempfile
        with tempfile.TemporaryDirectory() as directory:
            path=Path(directory)/'Authoring/WeaponPackages';path.mkdir(parents=True)
            (path/'weapon_id_catalog.json').write_text(json.dumps({'entries':[dict(self.identity,status='active',category='OneHandMelee')]}),encoding='utf-8')
            source=Path(directory)/'WeaponSource';source.mkdir()
            (source/'global_weapon_library.json').write_text(json.dumps({'weaponTypes':[{'typeId':'staff','equipmentCategory':'Catalyst'}]}),encoding='utf-8')
            with self.assertRaisesRegex(ValueError,'WeaponTypeBindingInvalid'):
                contract.catalog_identity(directory,'000002')
    def test_stale_service_does_not_drop_trail(self):
        with self.assertRaisesRegex(ValueError,'bladeTrailResourceKey'):
            contract.assert_preserved({'presentationConfiguration':{'bladeTrailResourceKey':'katana'}}, {'presentationConfiguration':{}})

if __name__=='__main__':unittest.main()
