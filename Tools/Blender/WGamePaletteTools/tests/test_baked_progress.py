"""Progress stays truthful across buffered, partial and restarted worker logs."""
import importlib.util
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

SOURCE = Path(__file__).resolve().parents[1] / 'baked_progress.py'
spec = importlib.util.spec_from_file_location('baked_progress', SOURCE)
progress = importlib.util.module_from_spec(spec)
spec.loader.exec_module(progress)


class BakeProgressTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.path = Path(self.temp.name) / 'preview.log'
        self.reader = progress.BakeProgress(self.path, started=10)

    def append(self, data):
        with self.path.open('ab') as output:output.write(data)
        self.reader.update()

    def test_missing_log_keeps_elapsed_without_inventing_progress(self):
        self.reader.update()
        with patch.object(progress.time, 'monotonic', return_value=135):
            lines = self.reader.lines()
        self.assertIn('02:05', lines[-1])
        self.assertIn('等待日志', lines[-1])
        self.assertFalse(self.reader.ready)
        self.assertEqual(self.reader.stage, 1)

    def test_partial_utf8_and_json_wait_for_complete_line(self):
        line = 'WGAME_BAKE_PROGRESS {"stage":6,"message":"检查导出"}\n'.encode()
        cut = line.index('检'.encode()) + 1
        self.append(line[:cut])
        self.assertEqual(self.reader.stage, 1)
        self.append(line[cut:])
        self.assertEqual(self.reader.detail, '检查导出')
        self.assertEqual(self.reader.stage, 6)
        self.assertFalse(self.reader.ready)

    def test_real_legacy_worker_markers_and_retry(self):
        self.append(b'BAKE_UV_TILES body constantDuplicates 100 detailFaces 10\nBAKE_DEVICE CPU\n')
        self.assertEqual(self.reader.stage, 3)
        self.append(b'BAKE_CONTOUR_TRY 12 ANY angle 0 focused False\n')
        self.assertIn('12', self.reader.detail)
        self.assertEqual(self.reader.stage, 4)
        self.append(b'BAKE_ATLAS_COMPACT {}\n')
        self.assertEqual(self.reader.stage, 5)
        self.append(b'BAKE_CONTOUR_TRY 1 CARDINAL angle 0 focused False\n')
        self.assertEqual(self.reader.stage, 4)
        self.append(b'WGAME_FBX_EXPORT {}\nBAKED_PREVIEW_READY 000009\n')
        self.assertTrue(self.reader.ready)
        self.assertIn('等待主界面校验', self.reader.detail)

    def test_idle_poll_does_not_reset_last_log_update(self):
        self.append(b'BAKE_DEVICE CPU\n')
        updated = self.reader.updated
        self.reader.update()
        self.assertEqual(self.reader.updated, updated)
        with patch.object(progress.time, 'time', return_value=updated + 125):
            self.assertIn('距日志更新 02:05', self.reader.lines()[-1])

    def test_truncation_and_bad_markers_are_safe(self):
        self.append(b'BAKED_PREVIEW_READY 000009\n')
        self.path.write_bytes(b'new\n')
        self.reader.update()
        self.assertFalse(self.reader.ready)
        self.assertEqual(self.reader.stage, 1)
        self.append(b'WGAME_BAKE_PROGRESS {bad}\nWGAME_BAKE_PROGRESS []\nWGAME_BAKE_PROGRESS {"stage":99}\n')
        self.assertEqual(self.reader.stage, 1)

    def test_large_log_is_read_incrementally(self):
        self.path.write_bytes(b'noise\n' * 20000 + b'BAKE_ATLAS_COMPACT {}\n')
        self.reader.update()
        self.assertLessEqual(self.reader.offset, 65536)
        self.reader.update()
        self.assertEqual(self.reader.stage, 5)


if __name__ == '__main__':unittest.main()
