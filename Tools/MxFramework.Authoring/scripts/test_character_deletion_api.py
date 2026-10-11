"""Exercise the real HTTP deletion flow on disposable roots, never project assets.

Run with CHARACTER_DELETE_TEST_CLI pointing to a built authoring.dll.
"""
import json
import os
from pathlib import Path
import socket
import subprocess
import tempfile
import time
import unittest
from urllib.request import Request, urlopen
from urllib.error import HTTPError, URLError


class CharacterDeletionApiTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='character-delete-api-')
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.catalog = self.root/'Authoring/CharacterPackages/character_id_catalog.json'
        self.catalog.parent.mkdir(parents=True)
        self.catalog.write_text(json.dumps({'schemaVersion':'1.0','width':6,'revision':1,'nextCandidate':3,
            'entries':[{'id':'000001','status':'active'}],'retiredIds':['000002']}))
        self.source = self.root/'ArtSource/Characters/000001/work.blend'
        self.source.parent.mkdir(parents=True);self.source.write_bytes(b'fixture')
        archived = self.root/'Authoring/Archive/CharacterPackages/character-000002/manifest.json'
        archived.parent.mkdir(parents=True);archived.write_text('{}')
        library = self.root/'Assets/MxFrameworkGenerated/CharacterActions/Libraries/000001/actions.json'
        library.parent.mkdir(parents=True);library.write_text('{"id":"char.000001"}')
        with socket.socket() as sock:
            sock.bind(('127.0.0.1',0));port=sock.getsockname()[1]
        self.url=f'http://127.0.0.1:{port}'
        tools=Path(__file__).resolve().parents[3]
        self.server=subprocess.Popen(['dotnet',os.environ['CHARACTER_DELETE_TEST_CLI'],'editor','serve',
            '--tools-root',str(tools),'--project-root',str(self.root),'--port',str(port)],
            stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,creationflags=getattr(subprocess,'CREATE_NO_WINDOW',0))
        self.addCleanup(self.stop_server)
        for _ in range(100):
            try:
                with socket.create_connection(('127.0.0.1',port),.1):break
            except OSError:time.sleep(.05)
        else:self.fail('Fixture server did not start')

    def stop_server(self):
        self.server.terminate();self.server.wait(timeout=10)

    def post(self, route, data):
        request=Request(self.url+'/api/character/'+route,data=json.dumps(data).encode(),headers={'Content-Type':'application/json'})
        try:
            with urlopen(request,timeout=30) as response:return response.status,json.load(response)
        except HTTPError as error:return error.code,json.load(error)

    def test_preview_confirmation_staleness_delete_and_repeat(self):
        code,plan=self.post('delete-plan',{'characterId':'000001'})
        self.assertEqual((code,plan['status']),(200,'ready'))
        self.assertNotIn('files',plan)
        self.assertTrue(any('Libraries/000001' in p for p in plan['paths']))
        payload={'characterId':'000001','confirmCharacterId':'000002','editorsClosed':True,'planHash':plan['planHash']}
        self.assertNotEqual(self.post('delete',payload)[0],200)
        self.assertTrue(self.source.exists())
        payload['confirmCharacterId']='000001';payload['editorsClosed']=False
        self.assertNotEqual(self.post('delete',payload)[0],200)
        payload['editorsClosed']=True;self.source.write_bytes(b'new work')
        self.assertEqual(self.post('delete',payload)[0],409)
        self.assertTrue(self.source.exists())
        _,plan=self.post('delete-plan',{'characterId':'000001'});payload['planHash']=plan['planHash']
        self.assertEqual(self.post('delete',payload)[1]['status'],'deleted')
        self.assertFalse(self.source.exists())
        _,plan=self.post('delete-plan',{'characterId':'000001'});payload['planHash']=plan['planHash']
        self.assertEqual(self.post('delete',payload)[1]['status'],'already_deleted')
        self.assertEqual(json.loads(self.catalog.read_text())['nextCandidate'],3)

    def test_external_references_and_invalid_ids_block(self):
        (self.root/'Assets/consumer.json').write_text('{"characterId":"000001"}')
        _,plan=self.post('delete-plan',{'characterId':'000001'})
        self.assertEqual(plan['status'],'blocked')
        self.assertEqual(self.post('delete',{'characterId':'000001','confirmCharacterId':'000001','editorsClosed':True,'planHash':plan['planHash']})[0],409)
        self.assertTrue(self.source.exists())
        for value in ('../000001','humanoid','000000','999999'):
            self.assertNotEqual(self.post('delete-plan',{'characterId':value})[0],200)

    def test_archived_character_can_be_deleted(self):
        _,plan=self.post('delete-plan',{'characterId':'000002'})
        self.assertEqual(plan['status'],'ready')
        self.assertTrue(any('Archive' in p for p in plan['paths']))
        _,result=self.post('delete',{'characterId':'000002','confirmCharacterId':'000002','editorsClosed':True,'planHash':plan['planHash']})
        self.assertEqual(result['status'],'deleted')
        self.assertTrue(self.source.exists())

    def test_shared_registration_cleanup_is_planned_and_applied(self):
        mapping=self.root/'Assets/Config/MxFramework/ResourceProfiles/global_resource_mapping_profile.v2.json'
        acquisition=self.root/'Assets/StreamingAssets/MxFramework/CharacterTest/tactical_acquisition.json'
        mapping.parent.mkdir(parents=True);acquisition.parent.mkdir(parents=True)
        other={'authoringRecordId':'keep.other','custom':{'preserve':True}}
        asset='Assets/MxFrameworkGenerated/CharacterPackages/000001/resources/body.fbx'
        mapping.write_text(json.dumps({'schema':'mx.resource-mapping-profile.v2','entries':[
            {'authoringRecordId':'charpkg.000001.resource.body',
             'resourceKey':{'id':'char.000001.model.body','packageId':'000001','typeId':'GameObject','variant':'default'},
             'sourceLocator':{'kind':'generatedUnityAsset','cachedPath':asset},'producerReceipt':{
                'producerId':'character.package.000001','producerContractVersion':'1'}},other]}))
        runtime=self.root/'Assets/StreamingAssets/MxFramework/Resources/global_runtime_catalog.json'
        runtime.parent.mkdir(parents=True)
        runtime.write_text(json.dumps({'schemaVersion':1,'catalogId':'global.runtime','packageId':'','entries':[
            {'id':'char.000001.model.body','packageId':'000001','type':'GameObject','variant':'default',
             'provider':'assetBundle','address':'bundle|'+asset,'providerData':{'assetPath':asset,'bundleName':'bundle'}}]}))
        acquisition.write_text(json.dumps({'schema':'mx.character-test.tactical-acquisition.v1','characters':[
            {'characterStableId':'char.000001','actionIds':[],'cards':[]}]}))
        before=mapping.read_bytes(),acquisition.read_bytes(),runtime.read_bytes()
        code,plan=self.post('delete-plan',{'characterId':'000001'})
        self.assertEqual((code,plan['status']),(200,'ready'))
        self.assertEqual(len(plan['sharedEdits']),3)
        self.assertEqual(plan['sharedEdits'][0]['kind'],'characterRuntimeRegistration')
        self.assertEqual(before,(mapping.read_bytes(),acquisition.read_bytes(),runtime.read_bytes()))
        code,result=self.post('delete',{'characterId':'000001','confirmCharacterId':'000001',
            'editorsClosed':True,'planHash':plan['planHash']})
        self.assertEqual((code,result['status']),(200,'deleted'))
        self.assertEqual(len(result['cleanedSharedRecords']),3)
        self.assertEqual(json.loads(mapping.read_text())['entries'],[other])
        self.assertEqual(json.loads(acquisition.read_text())['characters'],[])
        self.assertEqual(json.loads(runtime.read_text())['entries'],[])


if __name__=='__main__':unittest.main()
