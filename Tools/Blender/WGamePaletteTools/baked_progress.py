"""Read worker logs incrementally; stage counts are not time percentages."""
import json
from pathlib import Path
import re
import time


STAGES = ('启动后台任务', '准备模型与 UV', '初始排布与贴图烘焙',
          '优化图集排布', '最终贴图烘焙与导出', '检查导出与预览文件')


def duration(seconds):
    seconds = max(0, int(seconds))
    minutes, seconds = divmod(seconds, 60)
    hours, minutes = divmod(minutes, 60)
    return f'{hours:02d}:{minutes:02d}:{seconds:02d}' if hours else f'{minutes:02d}:{seconds:02d}'


class BakeProgress:
    def __init__(self, path, started=None):
        self.path = Path(path)
        self.started = time.monotonic() if started is None else started
        self.offset = 0
        self.pending = b''
        self.stage = 1
        self.detail = '等待后台日志'
        self.updated = None
        self.ready = False

    def _line(self, line):
        marker = 'WGAME_BAKE_PROGRESS '
        if line.startswith(marker):
            try:
                item = json.loads(line[len(marker):])
                if not isinstance(item, dict):return
                stage = int(item.get('stage', 0))
                if 1 <= stage <= len(STAGES):
                    self.stage = stage
                    self.detail = str(item.get('message', ''))[:100]
            except (ValueError, TypeError):pass
        elif 'Read blend:' in line:
            self.stage = 2;self.detail = '制作快照已载入'
        elif line.startswith('BAKE_UV_TILES'):
            self.stage = 3;self.detail = 'UV 准备完成'
        elif line.startswith('BAKE_DEVICE '):
            self.stage = 3;self.detail = '计算设备：' + line[len('BAKE_DEVICE '):][:80]
        elif line.startswith('BAKE_CONTOUR_TRY '):
            attempt = re.match(r'BAKE_CONTOUR_TRY (\d+)\b', line)
            if attempt:
                self.stage = 4;self.detail = '正在尝试第 ' + attempt[1] + ' 种排布'
        elif line.startswith('BAKE_CONTOUR_CANDIDATE '):
            attempt = re.match(r'BAKE_CONTOUR_CANDIDATE (\d+)\b', line)
            if attempt:self.detail = '已评估第 ' + attempt[1] + ' 种排布'
        elif line.startswith('BAKE_ATLAS_COMPACT '):
            self.stage = 5;self.detail = '图集排布完成，开始最终烘焙'
        elif 'Baking map saved to internal image' in line:
            self.detail = '一张贴图已烘焙，继续处理'
        elif 'FBX export starting' in line:
            self.stage = 5;self.detail = '正在导出 FBX'
        elif line.startswith('WGAME_FBX_EXPORT '):
            self.stage = 6;self.detail = 'FBX 已导出，等待校验'
        elif line.startswith('BAKED_PREVIEW_READY '):
            self.stage = 6;self.detail = '后台结果已生成，等待主界面校验';self.ready = True

    def update(self):
        try:
            stat = self.path.stat()
            if stat.st_size < self.offset:
                self.offset = 0;self.pending = b'';self.stage = 1;self.ready = False
                self.detail = '日志重新开始'
            with self.path.open('rb') as stream:
                stream.seek(self.offset)
                chunk = stream.read(65536)
                self.offset = stream.tell()
            if chunk:
                self.updated = stat.st_mtime
                lines = (self.pending + chunk).split(b'\n')
                self.pending = lines.pop()[-65536:]
                for line in lines:self._line(line.decode('utf-8', errors='replace').strip())
        except OSError:
            pass  # A pending/temporarily locked log must not break the modal job.
        return self

    def lines(self):
        elapsed = duration(time.monotonic() - self.started)
        activity = ('等待日志输出' if self.updated is None else
                    '距日志更新 ' + duration(time.time() - self.updated))
        return (f'阶段 {self.stage}/{len(STAGES)} · {STAGES[self.stage-1]}',
                self.detail, f'已用 {elapsed} · {activity}')


class PublishProgress(BakeProgress):
    def _line(self, line):
        marker='WGAME_PUBLISH_PROGRESS '
        if line.startswith(marker):
            try:
                item=json.loads(line[len(marker):]);stage=int(item.get('stage',0))
                if 1<=stage<=5:self.stage=stage;self.detail=str(item.get('message',''))[:100]
            except (ValueError,TypeError,AttributeError):pass

    def lines(self):
        stages=('启动发布后台（制作源已保存）','编译发布工具','更新正式产物与角色包','导入生成资源','等待 Unity 重建')
        activity='等待日志输出' if self.updated is None else '距日志更新 '+duration(time.time()-self.updated)
        return (f'阶段 {self.stage}/5 · {stages[self.stage-1]}',self.detail,
                '已用 '+duration(time.monotonic()-self.started)+' · '+activity)
