"""Feedback persistence and recovery must never imply or repeat a publish."""
import importlib.util
import json
from pathlib import Path
import runpy
import shutil
import sys
import tempfile
import unittest
from unittest.mock import patch

spec=importlib.util.spec_from_file_location('task_state',Path(__file__).parents[1]/'baked_task_state.py')
state=importlib.util.module_from_spec(spec);spec.loader.exec_module(state)

class TaskStateTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.addCleanup(self.temp.cleanup)
        self.root=Path(self.temp.name);self.source=self.root/'work.blend';self.cid='000010'
        self.stage=self.root/'Temp/baked-preview-000010-one';self.stage.mkdir(parents=True)
        (self.stage/'preview_request.json').write_text(json.dumps({'packageId':self.cid,'sourcePath':str(self.source)}))
        validator=Path(__file__).parents[3]/'RuntimeHash/definition_ids.py'
        self.ids=runpy.run_path(str(validator))
        self.catalog=self.root/'Tools/RuntimeHash/character_definition_ids.json';self.catalog.parent.mkdir(parents=True)
        shutil.copy2(validator,self.catalog.with_name('definition_ids.py'))
        self.write_catalog(self.ids['apply'](self.ids['create']([413]),'allocate',413,self.cid))

    def write_catalog(self,document):self.catalog.write_text(json.dumps(document),encoding='utf-8')

    def attempt(self,attempt='new',request='request1'):
        (self.stage/'publish_attempt.json').write_text(json.dumps({'attemptId':attempt}))
        (self.stage/'publish_result.json').write_text(json.dumps({'status':'pendingUnity','attemptId':attempt,'requestId':request}))

    def test_round_trip_preflight_rejection_and_identity_isolation(self):
        value={'state':'rejected','message':'缺少候选','log':'preview.log'}
        state.save(self.root,self.cid,self.source,value)
        self.assertEqual(state.load(self.root,self.cid,self.source)['message'],'缺少候选')
        self.assertIsNone(state.load(self.root,self.cid,self.root/'other.blend'))
        self.assertIsNone(state.load(self.root,'000009',self.source))

    def test_corrupt_state_does_not_block_recovery(self):
        path=state.state_path(self.root,self.cid,self.source);path.parent.mkdir();path.write_text('{bad')
        self.assertIsNone(state.load(self.root,self.cid,self.source))

    def test_ready_receipt_requires_view_again(self):
        (self.stage/'bake_preview_receipt.json').write_text(json.dumps({'status':'ready','packageId':self.cid}))
        value=state.recover_stage(self.root,self.cid,self.source)
        self.assertEqual(value['state'],'ready');self.assertFalse(value['viewed'])

    def test_real_tangent_error_summary_keeps_full_diagnostic_and_log(self):
        log=self.stage/'preview.log';log.write_text('Traceback:\nValueError: body: invalid tangent flow at corner 16659\nBlender quit\n')
        value=state.recover_stage(self.root,self.cid,self.source)
        self.assertEqual(value['state'],'failed');self.assertIn('头发方向',value['message'])
        self.assertIn('16659',value['detail']);self.assertEqual(value['log'],str(log))

    def test_incomplete_worker_is_unknown_not_success(self):
        (self.stage/'preview.log').write_text('BAKE_DEVICE CPU\n')
        self.assertEqual(state.recover_stage(self.root,self.cid,self.source)['state'],'interrupted')

    def test_foreign_work_file_is_not_recovered(self):
        self.assertIsNone(state.recover_stage(self.root,self.cid,self.root/'other.blend'))

    def test_publish_result_waits_for_unity_receipt(self):
        self.attempt()
        value=state.recover_stage(self.root,self.cid,self.source)
        self.assertEqual(value['state'],'pendingUnity')
        target=self.root/'Temp/MxFrameworkAuthoring/CharacterImport/status/request1.json';target.parent.mkdir(parents=True)
        target.write_text('{"status":"completed"}')
        self.assertEqual(state.refresh_publish(self.root,value)['state'],'succeeded')
        target.write_text('{"status":"failed","message":"avatar rejected"}')
        value=state.refresh_publish(self.root,value)
        self.assertEqual(value['state'],'failed');self.assertIn('avatar rejected',value['detail'])

    def test_invalid_request_cannot_read_outside_status_directory(self):
        value={'stage':str(self.stage),'requestId':'../../other'}
        self.assertEqual(state.refresh_publish(self.root,value)['state'],'rejected')

    def test_nested_import_error_is_the_visible_cause_and_actual_log(self):
        child=self.stage/'publish_new/import-unity.log';child.parent.mkdir()
        child.write_text("status=ExportBlocked\n- Error gate=ExportBlocked code=CHAR_RUNTIME_ID_MISSING sourcePath=Tools/RuntimeHash/character_definition_ids.json object=domains/413/000010 field=runtimeCharacterId message=Source key '000010' is not registered in identity catalog domain 413.\n",encoding='utf-8')
        outer=self.stage/'publish.log';outer.write_text('RuntimeError: 发布工具执行失败（退出码 2），日志：'+str(child),encoding='utf-8')
        result=state.failure_feedback('发布失败',outer)
        self.assertEqual(result['errorCode'],'CHAR_RUNTIME_ID_MISSING')
        self.assertIn('000010 未登记',result['message'])
        self.assertEqual(result['log'],str(child));self.assertEqual(result['taskLog'],str(outer))
        self.assertIn('status=ExportBlocked',result['stderr']);self.assertIn('domains/413/000010',result['detail'])

    def test_nested_json_issue_is_supported_and_warning_is_not_failure(self):
        child=self.stage/'child.log';outer=self.stage/'publish.log'
        issue={'severity':'Error','code':'RESOURCE_MISSING','sourcePath':'resource_catalog.json','sourceObjectPath':'entries/body','field':'relativePath','message':'model is missing'}
        child.write_text(json.dumps({'issues':[{'severity':'Warning','code':'IGNORED'},issue]}))
        outer.write_text('detailLogPath='+str(child))
        result=state.failure_feedback('exit code 2',outer)
        self.assertEqual(result['errorCode'],'RESOURCE_MISSING');self.assertIn('model is missing',result['message'])
        self.assertEqual(result['errorIssue'],issue)

    def test_external_log_reference_is_not_followed(self):
        outside=self.root/'private.log';outside.write_text('ValueError: private contents')
        outer=self.stage/'publish.log';outer.write_text('detailLogPath='+str(outside))
        result=state.failure_feedback('failed',outer)
        self.assertEqual(result['log'],str(outer));self.assertNotIn('private contents',result['stderr'])

    def test_build_stderr_is_preserved_and_visible(self):
        child=self.stage/'build.log';child.write_text('tool.cs(10,2): error CS1002: ; expected\nmore details\n')
        outer=self.stage/'publish.log';outer.write_text('detailLogPath='+str(child))
        result=state.failure_feedback('exit code 2',outer)
        self.assertTrue(result['hasFailure']);self.assertIn('CS1002',result['message']);self.assertIn('more details',result['stderr'])

    def test_failed_attempt_never_borrows_old_success_receipt(self):
        self.attempt('old','oldRequest')
        value={'stage':str(self.stage),'state':'failed','message':'this attempt failed'}
        result=state.refresh_publish(self.root,value)
        self.assertEqual(result['state'],'failed');self.assertIsNone(result['requestId']);self.assertFalse(result['unityQueued'])

    def test_current_attempt_wins_over_old_marker_and_result(self):
        self.attempt('old','oldRequest')
        value={'stage':str(self.stage),'state':'running','attemptId':'new'}
        result=state.refresh_publish(self.root,value)
        self.assertEqual(result['state'],'interrupted');self.assertEqual(result['attemptId'],'new')
        self.assertIsNone(result['requestId']);self.assertFalse(result['unityQueued'])
        with self.assertRaises(ValueError):state.current_publish_result(self.stage,'new')

    def test_current_receipt_requires_pending_status_and_safe_request(self):
        self.attempt('new','../outside')
        with self.assertRaises(ValueError):state.current_publish_result(self.stage,'new')
        self.attempt('new','valid')
        self.assertEqual(state.current_publish_result(self.stage,'new')['requestId'],'valid')

    def test_recovery_preserves_nested_publish_failure_over_ready_bake(self):
        (self.stage/'bake_preview_receipt.json').write_text(json.dumps({'status':'ready','packageId':self.cid}))
        self.attempt('old','oldRequest')
        child=self.stage/'import.log';child.write_text('- Error gate=ExportBlocked code=CHAR_RUNTIME_ID_MISSING sourcePath=catalog object=domains/413/000010 field=runtimeCharacterId message=Missing identity')
        (self.stage/'publish.log').write_text('RuntimeError: failed, 日志：'+str(child),encoding='utf-8')
        result=state.recover_stage(self.root,self.cid,self.source,attempt_id='new')
        self.assertEqual(result['state'],'failed');self.assertEqual(result['errorCode'],'CHAR_RUNTIME_ID_MISSING')
        self.assertIsNone(result['requestId']);self.assertEqual(result['log'],str(child))

    def test_identity_gate_reads_formal_catalog_without_allocating(self):
        before=self.catalog.read_bytes()
        result=state.require_runtime_identity(self.root,self.cid)
        self.assertEqual(result['id'],1);self.assertEqual(result['domain'],413)
        self.assertEqual(before,self.catalog.read_bytes());self.assertFalse(self.catalog.with_suffix('.json.lock').exists())

    def test_identity_gate_covers_missing_retired_and_corrupt_catalog(self):
        valid=json.loads(self.catalog.read_text())
        for document,code in [(self.ids['create']([413]),'CHAR_RUNTIME_ID_MISSING'),
                              (self.ids['apply'](valid,'retire',413,self.cid),'CHAR_RUNTIME_ID_RETIRED'),
                              ({**valid,'catalogHash':'invalid'},'CHAR_RUNTIME_ID_CATALOG_INVALID'),
                              (None,'CHAR_RUNTIME_ID_CATALOG_INVALID')]:
            self.write_catalog(document)
            with self.assertRaises(state.PublishIdentityError) as raised:state.require_runtime_identity(self.root,self.cid)
            self.assertEqual(raised.exception.issue['code'],code)

    def test_background_worker_missing_identity_stops_before_build_or_formal_writes(self):
        self.write_catalog(self.ids['create']([413]));self.source.write_bytes(b'work')
        package=self.root/'Authoring/CharacterPackages/character-000010';package.mkdir(parents=True)
        manifest=package/'manifest.json';manifest.write_text('{"unchanged":true}')
        worker_path=Path(__file__).parents[2]/'WGameRiggingAssistant/publish_baked_character.py'
        worker=runpy.run_path(str(worker_path));original=runpy.run_path
        def load(path,*args,**kwargs):
            name=Path(path).name
            if name=='character_surface_publish_policy.py':return {'resolve_surface_publish_mode':lambda *a:None}
            if name=='baked_surface.py':return {'BINDING':'binding.json'}
            if name=='baked_preview_candidate.py':return {'validate_candidate':lambda *a:{'sourcePath':str(self.source),'files':{}},'sha':lambda p:'sourceHash'}
            return original(path,*args,**kwargs)
        arguments=['blender','--','--repo',str(self.root),'--character',self.cid,'--candidate',str(self.stage),'--expected-input-hash','hash','--source-hash','sourceHash','--attempt-id','1'*32]
        with patch('runpy.run_path',side_effect=load),patch.object(sys,'argv',arguments),patch('subprocess.run') as process:
            with self.assertRaisesRegex(ValueError,'CHAR_RUNTIME_ID_MISSING'):worker['main']()
            process.assert_not_called()
        self.assertEqual(manifest.read_text(),'{"unchanged":true}')
        self.assertFalse((self.root/'ArtSource').exists());self.assertFalse((self.root/'Assets').exists())
        self.assertFalse((self.root/'Temp/MxFrameworkAuthoring').exists())
        self.assertEqual(json.loads((self.stage/'publish_attempt.json').read_text())['attemptId'],'1'*32)

if __name__=='__main__':unittest.main()
