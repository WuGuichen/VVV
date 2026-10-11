"""Task feedback and receipt recovery; never runs a worker or edits Blender data."""
import hashlib
import json
import os
from pathlib import Path
import re
import runpy
import time

TERMINAL = {'succeeded', 'failed', 'rejected', 'interrupted'}
_identity_cache = {}


class PublishIdentityError(ValueError):
    def __init__(self, code, cid, detail):
        self.issue = {'code': code, 'gate': 'ExportBlocked',
                      'sourcePath': 'Tools/RuntimeHash/character_definition_ids.json',
                      'sourceObjectPath': 'domains/413/' + cid,
                      'field': 'runtimeCharacterId', 'message': detail}
        super().__init__(code + ': ' + detail)


def require_runtime_identity(root, cid):
    """Read the formal catalog; never allocates an identity or writes any file."""
    path = Path(root) / 'Tools/RuntimeHash/character_definition_ids.json'
    validator = path.with_name('definition_ids.py')
    try:
        signature = (str(path.resolve()), path.stat().st_mtime_ns, path.stat().st_size,
                     validator.stat().st_mtime_ns, validator.stat().st_size)
        document = _identity_cache.get(signature)
        if document is None:
            document = json.loads(path.read_text(encoding='utf-8-sig'))
            runpy.run_path(str(validator))['validate'](document)
            _identity_cache.clear();_identity_cache[signature] = document
        domain = next(d for d in document['domains'] if d['domainId'] == 413)
    except (OSError, ValueError, KeyError, TypeError, AttributeError, StopIteration) as error:
        raise PublishIdentityError('CHAR_RUNTIME_ID_CATALOG_INVALID', cid, str(error)) from error
    entry = next((e for e in domain['entries'] if e['sourceKey'] == cid), None)
    if entry is None:
        raise PublishIdentityError('CHAR_RUNTIME_ID_MISSING', cid,
            f"Source key '{cid}' is not registered in identity catalog domain 413.")
    if entry['status'] != 'active':
        raise PublishIdentityError('CHAR_RUNTIME_ID_RETIRED', cid,
            f"Source key '{cid}' is retired in identity catalog domain 413.")
    return {'id': entry['id'], 'domain': 413, 'sourceKey': cid,
            'revision': document['revision'], 'catalogHash': document['catalogHash']}


def state_path(root, cid, source):
    identity = str(Path(source).resolve())
    digest = hashlib.sha256(identity.encode('utf-8')).hexdigest()[:16]
    return Path(root) / 'Temp/BlenderPublishFeedback' / (cid + '-' + digest + '.json')


def load(root, cid, source):
    try:
        value = json.loads(state_path(root, cid, source).read_text(encoding='utf-8'))
        if not isinstance(value, dict) or value.get('version') != 1:return None
        if value.get('character') != cid or Path(value.get('source', '')).resolve() != Path(source).resolve():return None
        return value
    except (OSError, ValueError, TypeError):return None


def save(root, cid, source, value):
    target = state_path(root, cid, source)
    target.parent.mkdir(parents=True, exist_ok=True)
    document = {**value, 'version': 1, 'character': cid, 'source': str(Path(source).resolve())}
    temporary = target.with_suffix('.tmp')
    temporary.write_text(json.dumps(document, ensure_ascii=False, indent=2), encoding='utf-8')
    os.replace(temporary, target)


def _tail(path):
    try:
        with Path(path).open('rb') as stream:
            stream.seek(max(0, Path(path).stat().st_size - 32000))
            return stream.read().decode('utf-8', errors='replace')
    except OSError:return ''


def _business_issue(text):
    for line in text.splitlines():
        if re.match(r'^\s*-?\s*Error\s+gate=', line):
            fields = dict(re.findall(r'(\w+)=(.*?)(?=\s+\w+=|$)', line.strip()))
            if fields.get('code'):return fields
    # CLI tools may serialize a report or an individual issue instead of text.
    def find(value):
        if isinstance(value, dict):
            if value.get('code') and str(value.get('severity', 'Error')).lower() == 'error':return value
            for item in value.get('issues', []):
                result = find(item)
                if result:return result
        return None
    for candidate in [text.strip(), *text.splitlines()]:
        candidate = candidate[candidate.find('{'):] if '{' in candidate else ''
        if not candidate:continue
        try:result = find(json.loads(candidate))
        except ValueError:continue
        if result:return result
    return None


def failure_feedback(message, log=None):
    """Expose the business cause and nested log, while keeping complete stderr on disk."""
    outer = Path(log) if log else None
    primary = outer
    texts = [str(message)]
    if outer:
        text = _tail(outer);texts.append(text)
        visited = {outer.resolve()}
        for _ in range(4):
            nested = None
            for name in re.findall(r'(?:日志[:：]\s*|detailLogPath[=:]\s*)([^\r\n]+?\.log)', text):
                candidate = Path(name.strip().strip('"\''))
                if not candidate.is_absolute():candidate = outer.parent / candidate
                try:
                    resolved = candidate.resolve()
                    if outer.parent.resolve() in resolved.parents and resolved not in visited and candidate.is_file():nested = candidate
                except OSError:pass
            if nested is None:break
            primary = nested;visited.add(nested.resolve());text = _tail(nested);texts.append(text)
    issue = getattr(message, 'issue', None) or next((result for text in reversed(texts) if (result := _business_issue(text))), None)
    diagnostic = texts[-1] or str(message)
    errors = [line.strip() for line in diagnostic.splitlines()
              if re.match(r'^[\w.]+(?:Error|Exception):', line.strip())
              or re.search(r'\berror\s+(?:[A-Z]+\d+)\s*:', line, re.IGNORECASE)]
    detail = errors[-1] if errors else str(message)
    result = {'log': str(primary) if primary else '', 'taskLog': str(outer) if outer else '',
              'stderr': diagnostic[-8000:], 'hasFailure': bool(issue or errors)}
    if issue:
        code = issue['code'];where = issue.get('sourceObjectPath', issue.get('object', ''))
        cid = where.rsplit('/', 1)[-1]
        known = {'CHAR_RUNTIME_ID_MISSING': f'角色 {cid} 未登记运行时编号（域 413）',
                 'CHAR_RUNTIME_ID_RETIRED': f'角色 {cid} 的运行时编号已退休',
                 'CHAR_RUNTIME_ID_CATALOG_INVALID': '运行时身份目录缺失或无效'}
        summary = known.get(code, code + ': ' + str(issue.get('message', '发布预检失败')))
        detail = '\n'.join(str(issue.get(key, '')) for key in ('code', 'sourcePath'))
        detail += '\n' + where + '\n' + str(issue.get('message', ''))
        result.update(errorCode=code, errorGate=issue.get('gate', ''), errorIssue=dict(issue))
    elif 'invalid tangent flow at corner' in detail:summary = '头发方向无法投影到烘焙 UV 切线平面'
    else:summary = (detail.splitlines() or ['发布失败'])[0][:180]
    result.update(message=summary, detail=detail[:1600])
    return result


def error_summary(message, log=None):
    value = failure_feedback(message, log)
    return value['message'], value['detail']


def current_publish_result(stage, attempt_id):
    result = json.loads((Path(stage) / 'publish_result.json').read_text(encoding='utf-8'))
    if not attempt_id or result.get('attemptId') != attempt_id:
        raise ValueError('未生成本次发布回执；未进入 Unity，旧回执不可用于本次发布')
    request = result.get('requestId')
    if result.get('status') != 'pendingUnity' or not isinstance(request, str) or not re.fullmatch(r'[A-Za-z0-9_-]{1,80}', request):
        raise ValueError('本次发布回执中的请求编号无效')
    return result


def recover_stage(root, cid, source, attempt_id=None):
    """Recover only this Work file's newest receipt/log; a new session must view again."""
    matches = []
    for stage in (Path(root) / 'Temp').glob('baked-preview-' + cid + '-*'):
        try:
            request = json.loads((stage / 'preview_request.json').read_text(encoding='utf-8'))
            if request.get('packageId') != cid or Path(request['sourcePath']).resolve() != Path(source).resolve():continue
            matches.append((stage.stat().st_mtime_ns, stage))
        except (OSError, ValueError, KeyError, TypeError):continue
    if not matches:return None
    stage = max(matches)[1]
    value = {'kind': 'bake', 'stage': str(stage), 'log': str(stage / 'preview.log'), 'viewed': False}
    if attempt_id:value['attemptId'] = attempt_id
    try:
        receipt = json.loads((stage / 'bake_preview_receipt.json').read_text(encoding='utf-8'))
        if receipt.get('status') == 'ready' and receipt.get('packageId') == cid:
            value.update(state='ready', message='已恢复烘焙回执，请重新查看候选；发布前复核内容')
        else:raise ValueError('回执未就绪')
    except (OSError, ValueError):
        summary, detail = error_summary('上次后台任务未生成完整候选，请核对日志', value['log'])
        value.update(state='failed' if detail != '上次后台任务未生成完整候选，请核对日志' else 'interrupted', message=summary, detail=detail)
    publish_log = stage / 'publish.log'
    if publish_log.is_file():
        failure = failure_feedback('上次发布未完成，请核对日志', publish_log)
        if failure['hasFailure']:
            value.update(failure, kind='publish', state='failed', requestId=None, unityQueued=False)
    return refresh_publish(root, value)


def refresh_publish(root, value):
    """Read disk completion only; no export/import/rebuild side effects."""
    stage = Path(value.get('stage', ''))
    request = value.get('requestId')
    attempt = value.get('attemptId')
    if value.get('state') == 'failed' and not request:
        value.update(requestId=None, unityQueued=False)
        return value  # A failed attempt cannot become successful through an older receipt.
    try:
        marker = json.loads((stage / 'publish_attempt.json').read_text(encoding='utf-8'))
        if not attempt and marker.get('attemptId'):attempt = marker['attemptId']
    except (OSError, ValueError, TypeError):pass
    if attempt:
        value['attemptId'] = attempt
        try:request = current_publish_result(stage, attempt)['requestId']
        except (OSError, ValueError, KeyError, TypeError):
            value.update(requestId=None, unityQueued=False)
            if value.get('state') not in {'failed', 'rejected'}:
                value.update(kind='publish', state='interrupted', message='未生成本次发布回执；未进入 Unity，请查看日志')
            return value
    elif value.get('state') == 'failed' or not request:
        value.update(requestId=None, unityQueued=False)
        return value  # No current attempt identity: never borrow a disk receipt.
    if not isinstance(request,str) or not re.fullmatch(r'[A-Za-z0-9_-]{1,80}',request):
        value.update(state='rejected',message='发布回执中的请求编号无效');return value
    value.update(kind='publish', requestId=request, unityQueued=True, log=str(stage / 'publish.log'))
    try:
        status = json.loads((Path(root) / 'Temp/MxFrameworkAuthoring/CharacterImport/status' / (request + '.json')).read_text(encoding='utf-8-sig'))
        state = str(status.get('status', '')).lower()
    except (OSError, ValueError, TypeError):status = {};state = ''
    if state in {'ready', 'succeeded', 'completed', 'success'}:
        value.update(state='succeeded', message='已发布，Unity 重建完成')
    elif state in {'failed', 'error', 'blocked'}:
        value.update(state='failed', message='资源已导出；Unity 重建失败', detail=str(status.get('message', '查看 Unity 状态回执')))
    else:value.update(state='pendingUnity', message='资源已导出，等待 Unity 重建回执')
    return value
