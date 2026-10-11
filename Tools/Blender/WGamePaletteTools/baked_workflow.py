"""Bake -> inspect temporary result -> explicitly publish the inspected bytes."""
import json
import os
from pathlib import Path
import runpy
import subprocess
import tempfile
import time
import uuid

import bpy
from .baked_progress import BakeProgress, PublishProgress, duration
from . import baked_task_state as task_state

_candidates = globals().get('_candidates', {})
_status = globals().get('_status', {})
_active_job = None
_feedback = globals().get('_feedback', {})
_restored = globals().get('_restored', set())
_context_notice = globals().get('_context_notice', None)
_status_watch = globals().get('_status_watch', None)


def identity(context):
    from . import _find_repo_source_file, baked_preview
    scene = context.scene
    if baked_preview.active(context.window):scene = baked_preview._sessions[context.window.as_pointer()]['scene']
    source = _find_repo_source_file()
    if not source:raise ValueError('找不到仓库工具')
    root = Path(source).resolve().parents[3]
    path = Path(bpy.data.filepath).resolve() if bpy.data.filepath else None
    if path is None or not path.is_file():raise ValueError('请先将角色登记并保存为 Work Blend')
    cid = str(getattr(getattr(scene, 'wgame_rigging_assistant', None), 'character_id', '')).strip()
    if not cid:
        parts = path.parts
        for i in range(len(parts) - 2):
            if parts[i:i+2] == ('ArtSource', 'Characters'):cid = parts[i+2];break
    if len(cid) != 6 or not cid.isdecimal() or cid == '000000':raise ValueError('请选择有效的六位角色编号')
    manifest = root / 'ArtSource/Characters' / cid / 'authoring_manifest.json'
    if manifest.is_file():
        authority = (json.loads(manifest.read_text(encoding='utf-8-sig')).get('authority') or {}).get('blenderPath')
        if authority and (manifest.parent / authority).resolve() != path:raise ValueError('请打开角色的正式 Work Blend')
    return root, cid, path, scene


def key_for(root, cid, path):return str(root), cid, str(path)


def fingerprint(scene, root):
    helper = runpy.run_path(str(root / 'Tools/Blender/WGameRiggingAssistant/baked_preview_inputs.py'))
    return helper['source_fingerprint'](scene, root)


def checked_candidate(context, require_viewed=False):
    root, cid, path, scene = identity(context)
    _feedback_for(root, cid, path)
    candidate = _candidates.get(key_for(root, cid, path))
    if candidate is None:raise ValueError('请先点击“烘焙并预览”，查看临时结果后再发布')
    if not (Path(candidate['stage'])/'bake_preview_receipt.json').is_file():
        raise ValueError('本次临时烘焙结果已被清理，请重新“烘焙并预览”；已发布的正式资产不受影响')
    helper = runpy.run_path(str(root / 'Tools/Blender/WGameRiggingAssistant/baked_preview_candidate.py'))
    receipt = helper['validate_candidate'](candidate['stage'], root, cid, fingerprint(scene, root))
    if Path(receipt['sourcePath']).resolve() != path:raise ValueError('烘焙结果不属于当前制作源')
    if require_viewed and not candidate.get('viewed'):raise ValueError('请先“查看本次烘焙”，再确认发布')
    return root, cid, path, scene, candidate, receipt


def open_candidate(context):
    from . import baked_preview
    root, cid, path, scene, candidate, receipt = checked_candidate(context)
    baked_preview.open_preview(candidate['stage'], root, context.window)
    candidate['viewed'] = True
    _record(root,cid,path,'ready','候选已查看，等待确认发布',stage=candidate['stage'],viewed=True,detail='')


def _redraw(context):
    for window in context.window_manager.windows:
        for area in window.screen.areas:area.tag_redraw()


def _watch_unity_receipts():
    global _status_watch
    pending=False;changed=False
    for key,value in list(_feedback.items()):
        if value.get('state')!='pendingUnity':continue
        before=dict(value);task_state.refresh_publish(key[0],value)
        pending=pending or value.get('state')=='pendingUnity'
        if before!=value:
            changed=True
            try:task_state.save(key[0],key[1],key[2],value)
            except OSError as error:value['persistenceError']='反馈状态未写入：'+str(error)
    if changed:
        for window in bpy.context.window_manager.windows:
            for area in window.screen.areas:area.tag_redraw()
    if pending:return 1.0
    _status_watch=None;return None


def _ensure_receipt_watch():
    global _status_watch
    if _status_watch is None:
        _status_watch=_watch_unity_receipts
        bpy.app.timers.register(_status_watch,first_interval=1.0)


def _feedback_for(root, cid, path):
    key = key_for(root, cid, path)
    if key not in _restored:
        _restored.add(key)
        value = task_state.load(root, cid, path) or task_state.recover_stage(root, cid, path)
        if value:
            if value.get('state') in {'running', 'saving'} and _active_job is None:
                recovered = task_state.recover_stage(root, cid, path, attempt_id=value.get('attemptId'))
                value = recovered or {**value, 'state': 'interrupted', 'message': '上次任务未连接，请查看日志；未自动重新发布'}
            _feedback[key] = value
            if value.get('stage') and value.get('state') in {'ready','rejected','failed','interrupted'} and (Path(value['stage'])/'bake_preview_receipt.json').is_file():
                _candidates.setdefault(key, {'stage': value['stage'], 'viewed': False})
    value = _feedback.get(key, {})
    if value.get('state') == 'pendingUnity':
        task_state.refresh_publish(root, value)
        if value.get('state') == 'pendingUnity':_ensure_receipt_watch()
    return value


def _record(root, cid, path, state, message, **fields):
    global _context_notice
    key = key_for(root, cid, path)
    _context_notice = None
    value = {**_feedback.get(key, {}), **fields, 'state': state, 'message': message}
    _feedback[key] = value;_status[key] = message;_restored.add(key)
    if state=='pendingUnity':_ensure_receipt_watch()
    try:task_state.save(root, cid, path, value)
    except OSError as error:value['persistenceError'] = '反馈状态未写入：' + str(error)
    return value


def record_rejection(context, message, log=None, kind=None):
    """Keep preflight rejection visible, even when no subprocess was started."""
    global _context_notice
    failure = task_state.failure_feedback(message, log)
    summary, detail = failure['message'], failure['detail']
    try:root,cid,path,_ = identity(context)
    except (ValueError, OSError):
        _context_notice = {'source': bpy.data.filepath, 'state': 'rejected', 'message': summary, 'detail': detail, 'log': str(log) if log else ''}
    else:
        previous=_feedback_for(root,cid,path)
        _record(root,cid,path,'rejected',summary,detail=detail,
                kind=kind or previous.get('kind','bake'),
                log=failure['log'] or (previous.get('log','') if kind!='publish' else ''),taskLog=failure['taskLog'],
                stderr=failure['stderr'],errorCode=failure.get('errorCode'),
                errorGate=failure.get('errorGate'),errorIssue=failure.get('errorIssue'),
                requestId=None,unityQueued=False,finished=time.time())
    _redraw(context)


def _wrapped_label(layout, context, message, icon='NONE'):
    import unicodedata
    width = max(18, int(getattr(getattr(context,'region',None),'width',300) / 8) - 5)
    for paragraph in str(message).splitlines():
        line='';used=0
        for char in paragraph[:800]:
            size=2 if unicodedata.east_asian_width(char) in {'W','F'} else 1
            if used + size > width:
                layout.label(text=line,icon=icon);line='';used=0;icon='NONE'
            line+=char;used+=size
        if line:layout.label(text=line,icon=icon);icon='NONE'


def publish_gate(context, structured=False):
    if _active_job is not None:return '后台任务进行中，请等待完成'
    try:
        root,cid,path,_=identity(context);key=key_for(root,cid,path)
        state=_feedback_for(root,cid,path)
        if state.get('state')=='pendingUnity':return '已导出，正在等待 Unity；请勿重复发布'
        candidate=_candidates.get(key)
        if candidate and not (Path(candidate['stage'])/'bake_preview_receipt.json').is_file():
            return '本次临时烘焙结果已被清理，请重新“烘焙并预览”'
        if state.get('state')=='succeeded':return '此候选已发布；修改后请重新烘焙'
        if candidate is None:return '尚无可用候选：先烘焙并预览'
        receipt=Path(candidate['stage'])/'bake_preview_receipt.json'
        document=json.loads(receipt.read_text(encoding='utf-8'))
        if Path(document.get('sourcePath','')).resolve()!=path:return '候选不属于当前 Work Blend'
        if document.get('status')!='ready' or document.get('packageId')!=cid:return '候选回执未就绪，请重新烘焙'
        expected={cid+'_LOD0.fbx','BaseColor.png','SurfaceData.png','character_baked_surface_binding.json','fbx_export_audit.json'}
        if set(document.get('files',{}))!=expected or any(not (receipt.parent/name).is_file() for name in expected):return '候选文件不完整，请重新烘焙'
        if not candidate.get('viewed'):return '候选已就绪：先点击“查看本次烘焙”'
        try:task_state.require_runtime_identity(root,cid)
        except task_state.PublishIdentityError as error:return error if structured else task_state.failure_feedback(error)['message']
        return ''  # Content/image hashes are checked by checked_candidate before saving.
    except (ValueError,OSError,KeyError,TypeError) as error:return str(error)


def draw_controls(layout, context):
    from . import baked_preview
    box=layout.box()
    preview=baked_preview.active(context.window)
    if not preview:
        _wrapped_label(box,context,'1. 烘焙并预览 → 2. 检查结果 → 3. 确认发布')
        row=box.row();row.enabled=_active_job is None
        row.operator('wgame.bake_preview',text='1. 烘焙并预览',icon='RENDER_STILL')
    try:
        root,cid,path,_=identity(context);key=key_for(root,cid,path)
        value=_feedback_for(root,cid,path)
    except (ValueError,OSError) as error:
        value=_context_notice or {'state':'rejected','message':str(error)};key=None
    candidate=_candidates.get(key)
    if candidate and not preview:
        row=box.row();row.enabled=_active_job is None
        row.operator('wgame.view_bake_candidate',text='2. 查看本次烘焙',icon='VIEWZOOM')
    reason=publish_gate(context)
    row=box.row();row.enabled=not reason
    row.operator('wgame.baked_publish',text='3. 确认并发布到 Unity',icon='EXPORT')
    _wrapped_label(box,context,reason or '候选已查看；发布前复核制作内容，并保存 Work Blend',icon='INFO')
    notice=(_context_notice if _context_notice and _context_notice.get('source')==bpy.data.filepath else None) or value
    if notice:
        icon='ERROR' if notice.get('state') in {'failed','rejected','interrupted'} else 'CHECKMARK' if notice.get('state')=='succeeded' else 'TIME'
        _wrapped_label(box,context,notice.get('message',''),icon)
        if notice.get('detail') and notice['detail']!=notice.get('message'):_wrapped_label(box,context,notice['detail'])
        if notice.get('finished') and notice.get('started'):
            box.label(text='用时 '+duration(notice['finished']-notice['started']))
        if notice.get('requestId'):_wrapped_label(box,context,'Unity 请求：'+notice['requestId'])
        elif notice.get('unityQueued') is False and notice.get('kind')=='publish' and notice.get('state') in {'failed','rejected','interrupted'}:
            _wrapped_label(box,context,'本次未进入 Unity：未生成本次请求编号','INFO')
        if notice.get('persistenceError'):_wrapped_label(box,context,notice['persistenceError'],'ERROR')
        if notice.get('log') and Path(notice['log']).is_file():
            box.operator('wm.path_open',text='打开失败明细日志' if notice.get('taskLog') and notice['taskLog']!=notice['log'] else '打开任务日志',icon='TEXT').filepath=notice['log']
        if notice.get('taskLog') and notice['taskLog']!=notice.get('log') and Path(notice['taskLog']).is_file():
            box.operator('wm.path_open',text='打开发布主日志',icon='TEXT').filepath=notice['taskLog']
    if _active_job is not None and getattr(_active_job,'_key',None)==key:
        progress=getattr(_active_job,'_progress',None)
        if progress:
            for line in progress.lines():_wrapped_label(box,context,line)
    _wrapped_label(box,context,'发布成功以“Unity 重建完成”为准；贴图出现不等于成功')


class WG_OT_BakePreview(bpy.types.Operator):
    bl_idname='wgame.bake_preview'
    bl_label='烘焙并预览'
    bl_description='在后台烘焙当前制作快照，完成后打开临时预览；不覆盖正式产物、不导入 Unity'
    _active=False

    @classmethod
    def poll(cls,context):return _active_job is None and not context.scene.get('wgame_baked_preview',False)

    def execute(self,context):
        global _active_job
        if not self.poll(context):return {'CANCELLED'}
        self._log=None
        self._started_wall=time.time()
        try:
            root,cid,path,scene=identity(context)
            self._key=key_for(root,cid,path);self._root=root;self._cid=cid;self._source=path;self._scene=scene;self._window=context.window
            input_hash=fingerprint(scene,root)
            (root/'Temp').mkdir(exist_ok=True)
            self._stage=Path(tempfile.mkdtemp(prefix='baked-preview-'+cid+'-',dir=str(root/'Temp')))
            baker=runpy.run_path(str(root/'Tools/Blender/WGameRiggingAssistant/baked_surface.py'))
            baker['snapshot'](self._stage/'source.blend')
            (self._stage/'preview_request.json').write_text(json.dumps({'packageId':cid,'sourcePath':str(path),'inputHash':input_hash}),encoding='utf-8')
            self._log=open(self._stage/'preview.log','w',encoding='utf-8')
            self._progress=BakeProgress(self._stage/'preview.log')
            self._process=subprocess.Popen([bpy.app.binary_path,'--background','--factory-startup',str(self._stage/'source.blend'),
                '--python-exit-code','17','--python',str(root/'Tools/Blender/WGameRiggingAssistant/bake_character_preview.py'),
                '--','--repo',str(root),'--character',cid],stdout=self._log,stderr=subprocess.STDOUT,
                env={**os.environ,'PYTHONUNBUFFERED':'1','PYTHONIOENCODING':'utf-8'},
                creationflags=getattr(subprocess,'CREATE_NO_WINDOW',0))
            if hasattr(scene,'wgame_baked_surface_enabled'):scene.wgame_baked_surface_enabled=True
        except Exception as error:
            if self._log:self._log.close()
            record_rejection(context,error)
            self.report({'ERROR'},str(error));return {'CANCELLED'}
        _active_job=self;WG_OT_BakePreview._active=True
        _candidates.pop(self._key,None)
        _record(root,cid,path,'running','正在后台烘焙',kind='bake',stage=str(self._stage),log=str(self._stage/'preview.log'),started=self._started_wall,finished=None,detail='',requestId=None,attemptId=None,unityQueued=None,taskLog='',errorCode=None,errorGate=None,errorIssue=None,pid=self._process.pid)
        self._timer=context.window_manager.event_timer_add(.5,window=context.window)
        context.window_manager.modal_handler_add(self)
        return {'RUNNING_MODAL'}

    def modal(self,context,event):
        if event.type!='TIMER':return {'PASS_THROUGH'}
        self._progress.update()
        for window in context.window_manager.windows:
            for area in window.screen.areas:area.tag_redraw()
        code=self._process.poll()
        if code is None:return {'PASS_THROUGH'}
        self._log.close()
        try:
            if code!=0:raise ValueError('烘焙失败，日志：'+str(self._stage/'preview.log'))
            helper=runpy.run_path(str(self._root/'Tools/Blender/WGameRiggingAssistant/baked_preview_candidate.py'))
            helper['validate_candidate'](self._stage,self._root,self._cid)
            _candidates[self._key]={'stage':str(self._stage),'viewed':False}
            _record(self._root,self._cid,self._source,'ready','烘焙完成，等待查看与确认发布',finished=time.time(),detail='')
            if context.window==self._window and context.scene==self._scene and Path(bpy.data.filepath).resolve()==self._source:
                open_candidate(context)
                self.report({'INFO'},'已打开临时烘焙预览；满意后点击“确认并发布到 Unity”')
        except Exception as error:
            summary,detail=task_state.error_summary(error,self._stage/'preview.log')
            _record(self._root,self._cid,self._source,'failed',summary,detail=detail,finished=time.time())
            self.report({'WARNING'},summary)
            return self.finish(context,False)
        return self.finish(context,True)

    def finish(self,context,success):
        global _active_job
        context.window_manager.event_timer_remove(self._timer)
        _active_job=None;WG_OT_BakePreview._active=False
        for window in context.window_manager.windows:
            for area in window.screen.areas:area.tag_redraw()
        return {'FINISHED'} if success else {'CANCELLED'}


class WG_OT_ViewBakeCandidate(bpy.types.Operator):
    bl_idname='wgame.view_bake_candidate'
    bl_label='查看本次烘焙'
    @classmethod
    def poll(cls,context):return _active_job is None
    def execute(self,context):
        try:open_candidate(context)
        except Exception as error:
            record_rejection(context,error)
            self.report({'WARNING'},str(error));return {'CANCELLED'}
        return {'FINISHED'}


class WG_OT_BakedPublish(bpy.types.Operator):
    bl_idname='wgame.baked_publish'
    bl_label='确认并发布到 Unity'
    bl_description='发布已查看且与当前制作源一致的烘焙结果；不会重新烘焙'
    _active=False

    @classmethod
    def poll(cls,context):
        reason=publish_gate(context)
        if reason:cls.poll_message_set(reason)
        return not reason

    def execute(self,context):
        global _active_job
        from . import baked_preview
        if _active_job is not None:
            record_rejection(context,'已有后台任务正在运行',kind='publish')
            self.report({'WARNING'},'已有后台任务正在运行');return {'CANCELLED'}
        reason=publish_gate(context,structured=True)
        if reason:
            record_rejection(context,reason,kind='publish')
            self.report({'WARNING'},task_state.failure_feedback(reason)['message']);return {'CANCELLED'}
        self._log=None
        try:
            root,cid,_,_=identity(context)
            task_state.require_runtime_identity(root,cid)
            if baked_preview.active(context.window):baked_preview.close_preview(context.window)
            root,cid,path,scene,candidate,receipt=checked_candidate(context,require_viewed=True)
            self._root=root;self._cid=cid;self._source=path;self._stage=Path(candidate['stage']);self._key=key_for(root,cid,path)
            self._started_wall=time.time()
            self._attempt=uuid.uuid4().hex
            _record(root,cid,path,'saving','正在保存已确认的制作源',kind='publish',stage=str(self._stage),started=self._started_wall,finished=None,detail='',requestId=None,attemptId=self._attempt,unityQueued=False,taskLog='',errorCode=None,errorGate=None,errorIssue=None,stderr='')
            saver=runpy.run_path(str(root/'Tools/Blender/WGameRiggingAssistant/save_character_source.py'))
            saved=saver['save_before_publish'](root,cid)
            if fingerprint(scene,root)!=receipt['inputHash']:raise ValueError('保存后的制作内容有变化，请重新烘焙并预览')
            self._log=open(self._stage/'publish.log','w',encoding='utf-8')
            self._process=subprocess.Popen([bpy.app.binary_path,'--background','--factory-startup','--python-exit-code','17',
                '--python',str(root/'Tools/Blender/WGameRiggingAssistant/publish_baked_character.py'),'--','--repo',str(root),
                '--character',cid,'--candidate',str(self._stage),'--expected-input-hash',receipt['inputHash'],'--source-hash',saved['hash'],'--attempt-id',self._attempt],
                stdout=self._log,stderr=subprocess.STDOUT,env={**os.environ,'PYTHONUNBUFFERED':'1','PYTHONIOENCODING':'utf-8'},creationflags=getattr(subprocess,'CREATE_NO_WINDOW',0))
        except Exception as error:
            if self._log:self._log.close()
            record_rejection(context,error,kind='publish')
            self.report({'WARNING'},str(error));return {'CANCELLED'}
        self._request=None;self._started=time.monotonic()
        _active_job=self;WG_OT_BakedPublish._active=True
        self._progress=PublishProgress(self._stage/'publish.log')
        _record(root,cid,path,'running','正在发布已确认的烘焙结果',log=str(self._stage/'publish.log'),pid=self._process.pid)
        _redraw(context)
        self._timer=context.window_manager.event_timer_add(.5,window=context.window)
        context.window_manager.modal_handler_add(self)
        return {'RUNNING_MODAL'}

    def modal(self,context,event):
        if event.type!='TIMER':return {'PASS_THROUGH'}
        self._progress.update();_redraw(context)
        code=self._process.poll()
        if code is None:return {'PASS_THROUGH'}
        try:
            if self._request is None:
                self._log.close()
                if code!=0:raise ValueError('发布失败，查看 '+str(self._stage/'publish.log'))
                self._request=task_state.current_publish_result(self._stage,self._attempt)['requestId']
                self._started=time.monotonic()
                _record(self._root,self._cid,self._source,'pendingUnity','资源已导出，等待 Unity 重建',requestId=self._request,unityQueued=True)
            status_path=self._root/'Temp/MxFrameworkAuthoring/CharacterImport/status'/(self._request+'.json')
            if status_path.exists():
                try:status=json.loads(status_path.read_text(encoding='utf-8-sig'))
                except (OSError,ValueError):status={}
                state=str(status.get('status','')).lower()
                if state in {'ready','succeeded','completed','success'}:
                    _record(self._root,self._cid,self._source,'succeeded','已发布，Unity 重建完成',finished=time.time(),detail='')
                    self.report({'INFO'},'已发布，Unity 重建完成');return self.finish(context,True)
                if state in {'failed','error','blocked'}:raise ValueError(str(status.get('message','Unity 重建失败')))
            if time.monotonic()-self._started>300:
                _record(self._root,self._cid,self._source,'pendingUnity','已导出，Unity 尚未完成重建；保留请求继续核对',finished=time.time())
                self.report({'WARNING'},'已导出，Unity 尚未完成重建');return self.finish(context,False)
        except Exception as error:
            failure=task_state.failure_feedback(error,self._stage/'publish.log')
            summary=failure.pop('message');failure.pop('hasFailure',None)
            _record(self._root,self._cid,self._source,'failed',summary,**failure,
                    requestId=self._request,unityQueued=bool(self._request),finished=time.time())
            self.report({'WARNING'},summary);return self.finish(context,False)
        return {'PASS_THROUGH'}

    def finish(self,context,success):
        global _active_job
        context.window_manager.event_timer_remove(self._timer)
        _active_job=None;WG_OT_BakedPublish._active=False
        _redraw(context)
        return {'FINISHED'} if success else {'CANCELLED'}
