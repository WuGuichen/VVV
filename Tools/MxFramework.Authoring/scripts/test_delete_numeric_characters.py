import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch


spec = importlib.util.spec_from_file_location("delete_characters", Path(__file__).with_name("delete_numeric_characters.py"))
tool = importlib.util.module_from_spec(spec)
spec.loader.exec_module(tool)


class DeleteCharactersTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="character-delete-test-")
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.catalog = self.root / tool.CATALOG
        self.catalog.parent.mkdir(parents=True)
        self.catalog.write_text(json.dumps({"schemaVersion":"1.0", "width":6, "nextCandidate":8, "revision":4,
                                           "entries":[{"id":x,"status":"active"} for x in ("000005","000006","000007")], "retiredIds":[]}), encoding="utf-8")
        for value in ("000005", "000006", "000007"):
            for pattern in tool.OWNED_ROOTS[:2]:
                path = self.root / pattern.format(id=value)
                path.mkdir(parents=True)
                (path / "content.bin").write_bytes(value.encode())

    def test_preview_apply_and_idempotence(self):
        before = self.catalog.read_bytes()
        plan = tool.build_plan(self.root, ["000005", "000006"])
        self.assertEqual(plan["fileCount"], 4)
        self.assertEqual(before, self.catalog.read_bytes())
        result = tool.apply_plan(self.root, ["000005", "000006"], plan["planHash"])
        self.assertEqual(result["status"], "deleted")
        catalog = json.loads(self.catalog.read_text())
        self.assertEqual(catalog["retiredIds"], ["000005", "000006"])
        self.assertEqual(catalog["entries"], [{"id":"000007", "status":"active"}])
        self.assertEqual(catalog["nextCandidate"], 8)
        self.assertEqual((self.root / "ArtSource/Characters/000007/content.bin").read_bytes(), b"000007")
        again = tool.build_plan(self.root, ["000005", "000006"])
        self.assertEqual(tool.apply_plan(self.root, ["000005", "000006"], again["planHash"])["status"], "already_deleted")

    def test_external_reference_blocks(self):
        other = self.root / "Authoring/CharacterPackages/character-000007/reference.json"
        other.write_text('{"model":"char.000005.model.body"}')
        plan = tool.build_plan(self.root, ["000005"])
        self.assertEqual(plan["status"], "blocked")
        with self.assertRaises(ValueError):
            tool.apply_plan(self.root, ["000005"], plan["planHash"])
        self.assertTrue((self.root / "ArtSource/Characters/000005").exists())

    def test_guid_reference_blocks(self):
        target = self.root / "Assets/MxFrameworkGenerated/CharacterPackages/000005"
        target.mkdir(parents=True)
        guid = "ab" * 16
        (target / "model.fbx.meta").write_text("guid: " + guid)
        (self.root / "Assets/consumer.prefab").write_text("m_Mesh: {guid: " + guid.upper() + "}")
        self.assertEqual(tool.build_plan(self.root, ["000005"])["status"], "blocked")

    def test_changed_plan_rejected(self):
        plan = tool.build_plan(self.root, ["000005"])
        (self.root / "ArtSource/Characters/000005/new.blend").write_bytes(b"new work")
        with self.assertRaises(ValueError):
            tool.apply_plan(self.root, ["000005"], plan["planHash"])
        self.assertTrue((self.root / "ArtSource/Characters/000005/new.blend").exists())

    def test_native_prefilter_matches_fallback_and_includes_ignored_files(self):
        target=self.root/'Assets/MxFrameworkGenerated/CharacterPackages/000005'
        target.mkdir(parents=True)
        guid='abcdef0123456789'*2
        (target/'model.meta').write_text('guid: '+guid)
        (self.root/'.gitignore').write_text('Assets/hidden/\n')
        hidden=self.root/'Assets/hidden';hidden.mkdir()
        (hidden/'consumer.JSON').write_text('{"guid":"'+guid.upper()+'"}')
        (hidden/'.private.json').write_text('{"characterId":"000005"}')
        (hidden/'not-a-reference.json').write_text('{"hash":"1'+guid+'0"}')
        native=tool.build_plan(self.root,['000005'])
        with patch.object(tool.shutil,'which',return_value=None):fallback=tool.build_plan(self.root,['000005'])
        self.assertEqual(native,fallback)
        self.assertEqual({r['path'] for r in native['blockingReferences']},
                         {'Assets/hidden/consumer.JSON','Assets/hidden/.private.json'})

    def test_native_scan_error_fails_closed(self):
        with patch.object(tool.shutil,'which',return_value='rg'),patch.object(tool.subprocess,'run',
                return_value=tool.subprocess.CompletedProcess([],2,stdout='',stderr='fixture scan failure')):
            with self.assertRaisesRegex(ValueError,'Reference scan failed'):tool.build_plan(self.root,['000005'])

    def test_partial_failure_can_resume(self):
        plan = tool.build_plan(self.root, ["000005"])
        real_remove = tool.shutil.rmtree
        def fail_one(path):
            if "ArtSource" in str(path):
                raise PermissionError("fixture locked")
            real_remove(path)
        with patch.object(tool.shutil, "rmtree", side_effect=fail_one):
            result = tool.apply_plan(self.root, ["000005"], plan["planHash"])
        self.assertEqual(result["status"], "partial")
        self.assertIn("000005", json.loads(self.catalog.read_text())["retiredIds"])
        refreshed = tool.build_plan(self.root, ["000005"])
        self.assertEqual(tool.apply_plan(self.root, ["000005"], refreshed["planHash"])["status"], "deleted")

    def test_invalid_or_unknown_ids_rejected(self):
        for value in ("../000007", "000000", "999999", "０００００５"):
            with self.assertRaises(ValueError):
                tool.build_plan(self.root, [value])

    def registrations(self):
        mapping=self.root/tool.RESOURCE_MAPPING;mapping.parent.mkdir(parents=True,exist_ok=True)
        acquisition=self.root/tool.TACTICAL_ACQUISITION;acquisition.parent.mkdir(parents=True,exist_ok=True)
        entries=[{'authoringRecordId':f'charpkg.{cid}.resource.model.body',
                  'producerReceipt':{'producerId':f'character.package.{cid}','producerContractVersion':'1'},
                  'custom':{'keep':'untouched'}} for cid in ('000005','000006')]
        mapping.write_text(json.dumps({'schema':'mx.resource-mapping-profile.v2','entries':entries,'metadata':{'keep':True}}))
        acquisition.write_text(json.dumps({'schema':'mx.character-test.tactical-acquisition.v1',
            'characters':[{'characterStableId':'char.'+cid,'actionIds':[],'cards':[]} for cid in ('000005','000006')]}))
        return mapping,acquisition

    def test_owned_registrations_preview_then_cleanup_preserves_other_records(self):
        files=self.registrations();before=[p.read_bytes() for p in files]
        original=[json.loads(b) for b in before]
        plan=tool.build_plan(self.root,['000005'])
        self.assertEqual(plan['status'],'ready');self.assertEqual(len(plan['sharedEdits']),2)
        self.assertEqual(before,[p.read_bytes() for p in files])
        result=tool.apply_plan(self.root,['000005'],plan['planHash'])
        self.assertEqual(result['status'],'deleted');self.assertEqual(len(result['cleanedSharedRecords']),2)
        original[0]['entries']=original[0]['entries'][1:];original[1]['characters']=original[1]['characters'][1:]
        self.assertEqual(original,[json.loads(p.read_bytes()) for p in files])
        self.assertTrue((self.root/'ArtSource/Characters/000006/content.bin').exists())
        self.assertEqual(tool.build_plan(self.root,['000005'])['status'],'already_deleted')

    def test_nonempty_or_unknown_tactical_record_still_blocks(self):
        _,acquisition=self.registrations()
        for extra in ({'actionIds':['ta.000005.1']},{'cards':[{'cardId':'kept'}]},{'futureField':True}):
            acquisition.write_text(json.dumps({'schema':'mx.character-test.tactical-acquisition.v1',
                'characters':[{'characterStableId':'char.000005','actionIds':[],'cards':[],**extra}]}))
            self.assertEqual(tool.build_plan(self.root,['000005'])['status'],'blocked')

    def test_shared_file_other_consumers_and_unproven_owners_still_block(self):
        mapping,_=self.registrations();original=json.loads(mapping.read_text())
        for change in ('dependency','owner','version'):
            document=json.loads(json.dumps(original))
            if change=='dependency':document['entries'][1]['dependencies']=['char.000005.model.body']
            if change=='owner':document['entries'][0]['producerReceipt']['producerId']='manual'
            if change=='version':document['entries'][0]['producerReceipt']['producerContractVersion']='future'
            mapping.write_text(json.dumps(document))
            plan=tool.build_plan(self.root,['000005'])
            self.assertEqual(plan['status'],'blocked')
            self.assertIn(tool.RESOURCE_MAPPING,[b['path'] for b in plan['blockingReferences']])
            with self.assertRaises(ValueError):tool.apply_plan(self.root,['000005'],plan['planHash'])

    def test_shared_document_changes_expire_plan(self):
        mapping,_=self.registrations();plan=tool.build_plan(self.root,['000005'])
        document=json.loads(mapping.read_text());document['metadata']['changed']='other author work'
        mapping.write_text(json.dumps(document));changed=mapping.read_bytes()
        with self.assertRaisesRegex(ValueError,'Plan changed'):tool.apply_plan(self.root,['000005'],plan['planHash'])
        self.assertEqual(mapping.read_bytes(),changed)
        self.assertTrue((self.root/'ArtSource/Characters/000005/content.bin').exists())

    def test_cleanup_failure_preserves_owned_files_and_can_retry(self):
        self.registrations();plan=tool.build_plan(self.root,['000005'])
        actual=tool.write_shared_edit
        def fail_second(root,edit,content):
            if edit['path']==tool.TACTICAL_ACQUISITION:raise PermissionError('fixture locked')
            actual(root,edit,content)
        with patch.object(tool,'write_shared_edit',side_effect=fail_second):
            result=tool.apply_plan(self.root,['000005'],plan['planHash'])
        self.assertEqual(result['status'],'partial');self.assertEqual(result['deletedPaths'],[])
        self.assertIn(tool.TACTICAL_ACQUISITION,result['remainingPaths'])
        self.assertTrue((self.root/'ArtSource/Characters/000005/content.bin').exists())
        plan=tool.build_plan(self.root,['000005'])
        self.assertEqual(len(plan['sharedEdits']),1)
        self.assertEqual(tool.apply_plan(self.root,['000005'],plan['planHash'])['status'],'deleted')

    def runtime_registrations(self):
        mapping,acquisition=self.registrations()
        document=json.loads(mapping.read_text());record=document['entries'][0]
        asset='Assets/MxFrameworkGenerated/CharacterPackages/000005/resources/body.fbx'
        record['resourceKey']={'id':'char.000005.model.body','packageId':'000005','typeId':'GameObject','variant':'default'}
        record['sourceLocator']={'kind':'generatedUnityAsset','cachedPath':asset}
        mapping.write_text(json.dumps(document))
        catalog=self.root/tool.RUNTIME_CATALOG;catalog.parent.mkdir(parents=True,exist_ok=True)
        runtime={'schemaVersion':1,'catalogId':'global.runtime','packageId':'','entries':[
            {'id':'char.000005.model.body','packageId':'000005','type':'GameObject','variant':'default',
             'provider':'assetBundle','address':'bundle|'+asset,'providerData':{'bundleName':'bundle','assetPath':asset}},
            {'id':'char.000006.model.body','packageId':'000006','custom':{'keep':True}}]}
        catalog.write_text(json.dumps(runtime))
        return mapping,catalog,runtime

    def test_runtime_registration_cleanup_preserves_other_entries(self):
        _,catalog,original=self.runtime_registrations();before=catalog.read_bytes()
        plan=tool.build_plan(self.root,['000005']);self.assertEqual(plan['status'],'ready')
        self.assertEqual(plan['sharedEdits'][0]['kind'],'characterRuntimeRegistration')
        self.assertEqual(catalog.read_bytes(),before)
        self.assertEqual(tool.apply_plan(self.root,['000005'],plan['planHash'])['status'],'deleted')
        original['entries']=original['entries'][1:]
        self.assertEqual(json.loads(catalog.read_text()),original)

    def test_unproven_runtime_rows_and_other_dependencies_still_block(self):
        mapping,catalog,original=self.runtime_registrations()
        for change in ('address','type','variant','dependency','provider'):
            document=json.loads(json.dumps(original));record=document['entries'][0]
            if change=='address':record['providerData']['assetPath']='Assets/shared.fbx'
            if change=='type':record['type']='Texture2D'
            if change=='variant':record['variant']='other'
            if change=='provider':record['provider']='unknown'
            if change=='dependency':document['entries'][1]['dependencies']=['char.000005.model.body']
            catalog.write_text(json.dumps(document))
            plan=tool.build_plan(self.root,['000005'])
            self.assertEqual(plan['status'],'blocked',change)
            self.assertIn(tool.RUNTIME_CATALOG,[b['path'] for b in plan['blockingReferences']])

    def test_runtime_cleanup_keeps_ownership_proof_on_failure(self):
        mapping,catalog,_=self.runtime_registrations();before=mapping.read_bytes()
        plan=tool.build_plan(self.root,['000005']);actual=tool.write_shared_edit
        def fail_mapping(root,edit,content):
            if edit['path']==tool.RESOURCE_MAPPING:raise PermissionError('fixture locked')
            actual(root,edit,content)
        with patch.object(tool,'write_shared_edit',side_effect=fail_mapping):
            result=tool.apply_plan(self.root,['000005'],plan['planHash'])
        self.assertEqual(result['status'],'partial');self.assertEqual(result['deletedPaths'],[])
        self.assertEqual(mapping.read_bytes(),before)
        self.assertNotIn('000005',catalog.read_text())
        retry=tool.build_plan(self.root,['000005']);self.assertEqual(retry['status'],'ready')
        self.assertEqual(tool.apply_plan(self.root,['000005'],retry['planHash'])['status'],'deleted')

    def test_links_rejected(self):
        target = self.root / "ArtSource/Characters/000005/link"
        try:
            target.symlink_to(self.root / "ArtSource/Characters/000007", target_is_directory=True)
        except OSError:
            self.skipTest("Symlinks unavailable on this host")
        with self.assertRaises(ValueError):
            tool.build_plan(self.root, ["000005"])
        target.unlink()


if __name__ == "__main__":
    unittest.main()
