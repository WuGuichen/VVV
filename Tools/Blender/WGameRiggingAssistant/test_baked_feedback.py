"""Disposable Blender fixtures: feedback gates and rejected actions have no publish side effects."""
import importlib
import importlib.util
import json
from pathlib import Path
import runpy
import sys
import unittest
from unittest.mock import patch
import bpy

fixture=runpy.run_path(str(Path(__file__).with_name('test_baked_preview_workflow.py')))
Base=fixture['BakePreviewTests']

class FeedbackTests(Base):
    def setUp(self):
        super().setUp()
        folder=Path(__file__).parent.parent/'WGamePaletteTools'
        spec=importlib.util.spec_from_file_location('feedback_test_palette',folder/'__init__.py',submodule_search_locations=[str(folder)])
        package=importlib.util.module_from_spec(spec);sys.modules[spec.name]=package;spec.loader.exec_module(package)
        self.flow=importlib.import_module('feedback_test_palette.baked_workflow')
        self.flow._candidates.clear();self.flow._feedback.clear();self.flow._restored.clear();self.flow._context_notice=None
        self.identity=patch.object(self.flow,'identity',return_value=(self.root,'000001',self.source,bpy.context.scene));self.identity.start();self.addCleanup(self.identity.stop)

    def test_feedback_gate_missing_unviewed_and_incomplete(self):
        self.assertIn('尚无',self.flow.publish_gate(bpy.context))
        self.ready();key=self.flow.key_for(self.root,'000001',self.source)
        self.flow._candidates[key]={'stage':str(self.stage),'viewed':False}
        self.assertIn('查看',self.flow.publish_gate(bpy.context))
        self.flow._candidates[key]['viewed']=True;self.assertEqual(self.flow.publish_gate(bpy.context),'')
        (self.stage/'BaseColor.png').unlink();self.assertIn('不完整',self.flow.publish_gate(bpy.context))

    def test_feedback_preflight_rejection_persists_without_saving(self):
        before=self.source.read_bytes();mode=bpy.context.mode;selected=list(bpy.context.selected_objects)
        messages=[];owner=type('Owner',(),{'report':lambda self,*args:messages.append(args)})()
        with patch('subprocess.Popen') as process:
            self.assertEqual(self.flow.WG_OT_BakedPublish.execute(owner,bpy.context),{'CANCELLED'})
            process.assert_not_called()
        saved=self.flow.task_state.load(self.root,'000001',self.source)
        self.assertEqual(saved['state'],'rejected');self.assertIn('烘焙',saved['message'])
        self.assertEqual(before,self.source.read_bytes());self.assertEqual(mode,bpy.context.mode);self.assertEqual(selected,list(bpy.context.selected_objects))

    def test_missing_runtime_identity_rejects_before_saving_or_closing_preview(self):
        from feedback_test_palette import baked_preview
        self.ready();key=self.flow.key_for(self.root,'000001',self.source)
        self.flow._candidates[key]={'stage':str(self.stage),'viewed':True}
        ids=runpy.run_path(str(self.root/'Tools/RuntimeHash/definition_ids.py'))
        (self.root/'Tools/RuntimeHash/character_definition_ids.json').write_text(json.dumps(ids['create']([413])),encoding='utf-8')
        before=self.source.read_bytes();mode=bpy.context.mode;selected=list(bpy.context.selected_objects)
        owner=type('Owner',(),{'report':lambda self,*args:None})()
        with patch('subprocess.Popen') as process,patch.object(baked_preview,'close_preview') as close:
            self.assertEqual(self.flow.WG_OT_BakedPublish.execute(owner,bpy.context),{'CANCELLED'})
            process.assert_not_called();close.assert_not_called()
        saved=self.flow.task_state.load(self.root,'000001',self.source)
        self.assertEqual(saved['errorCode'],'CHAR_RUNTIME_ID_MISSING');self.assertEqual(saved['kind'],'publish')
        self.assertIsNone(saved['requestId']);self.assertFalse(saved['unityQueued']);self.assertIn('未登记',saved['message'])
        self.assertEqual(before,self.source.read_bytes());self.assertEqual(mode,bpy.context.mode);self.assertEqual(selected,list(bpy.context.selected_objects))

    def test_failure_ui_links_child_and_outer_logs_and_says_not_queued(self):
        from types import SimpleNamespace
        calls=[]
        class Layout:
            enabled=True
            def box(self):return self
            def row(self):return Layout()
            def label(self,**kwargs):calls.append(('label',kwargs.get('text','')))
            def operator(self,name,**kwargs):
                value=SimpleNamespace();calls.append(('operator',kwargs.get('text'),value));return value
        child=self.stage/'import.log';child.write_text('actual child stderr')
        outer=self.stage/'publish.log';outer.write_text('outer stderr')
        key=self.flow.key_for(self.root,'000001',self.source);self.flow._restored.add(key)
        self.flow._feedback[key]={'kind':'publish','state':'failed','message':'角色 000001 未登记','detail':'CHAR_RUNTIME_ID_MISSING','log':str(child),'taskLog':str(outer),'requestId':None,'unityQueued':False}
        self.flow.draw_controls(Layout(),bpy.context)
        buttons={row[1]:row[2] for row in calls if row[0]=='operator'}
        self.assertEqual(buttons['打开失败明细日志'].filepath,str(child));self.assertEqual(buttons['打开发布主日志'].filepath,str(outer))
        self.assertTrue(any(row[0]=='label' and '本次未进入 Unity' in row[1] for row in calls))

    def test_feedback_ui_retains_failure_log_and_disabled_publish_reason(self):
        from types import SimpleNamespace
        calls=[]
        class Layout:
            enabled=True
            def box(self):return self
            def row(self):return Layout()
            def label(self,**kwargs):calls.append(('label',kwargs.get('text','')))
            def operator(self,name,**kwargs):
                calls.append(('operator',name,self.enabled));return SimpleNamespace()
        log=self.stage/'preview.log';log.write_text('ValueError: invalid tangent flow at corner 16659')
        key=self.flow.key_for(self.root,'000001',self.source);self.flow._restored.add(key)
        self.flow._feedback[key]={'state':'failed','message':'头发方向无法投影','detail':'corner 16659','log':str(log)}
        self.flow.draw_controls(Layout(),bpy.context)
        self.assertIn(('operator','wm.path_open',True),calls)
        self.assertIn(('operator','wgame.baked_publish',False),calls)
        self.assertTrue(any(row[0]=='label' and '尚无可用候选' in row[1] for row in calls))
        self.assertTrue(any(row[0]=='label' and '头发方向' in row[1] for row in calls))

    def test_feedback_passive_receipt_finishes_without_starting_any_process(self):
        key=self.flow.key_for(self.root,'000001',self.source)
        self.flow._feedback[key]={'state':'pendingUnity','requestId':'r1','stage':str(self.stage)}
        target=self.root/'Temp/MxFrameworkAuthoring/CharacterImport/status/r1.json';target.parent.mkdir(parents=True)
        target.write_text('{"status":"completed"}')
        with patch('subprocess.Popen') as process:
            self.assertIsNone(self.flow._watch_unity_receipts());process.assert_not_called()
        self.assertEqual(self.flow._feedback[key]['state'],'succeeded')
        self.assertEqual(self.flow.task_state.load(self.root,'000001',self.source)['state'],'succeeded')

    def test_feedback_restored_candidate_needs_viewing_and_honest_pending_state(self):
        self.ready();self.flow._restored.clear()
        # Fixture stage has a different prefix, so use persisted index.
        self.flow.task_state.save(self.root,'000001',self.source,{'state':'ready','stage':str(self.stage),'viewed':True})
        self.assertIn('查看',self.flow.publish_gate(bpy.context))
        key=self.flow.key_for(self.root,'000001',self.source)
        self.flow._feedback[key]['state']='pendingUnity'
        self.assertIn('等待 Unity',self.flow.publish_gate(bpy.context))

if __name__=='__main__':
    result=unittest.TextTestRunner(verbosity=2).run(unittest.defaultTestLoader.loadTestsFromTestCase(FeedbackTests))
    if not result.wasSuccessful():raise RuntimeError('Feedback tests failed')
