"""Delivery checks, not assertions about Unity behavior or prose correctness."""
import contextlib
import importlib.util
import io
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

SPEC = importlib.util.spec_from_file_location("docs_health", Path(__file__).parents[1] / "check_docs_health.py")
health = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(health)


class CodeDocumentDeliveryTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.old_cwd = Path.cwd()
        os.chdir(self.temp.name)
        self.doc = Path("Docs/Guides/Feature.md")
        self.source = Path("Packages/example/Feature.cs")
        self.write(self.doc, "> Status: Guide\n> Code-Doc: `Packages/example/Feature*.cs`\n\nCurrent behavior.\n")
        self.write(self.source, "changed implementation\n")

    def tearDown(self):
        os.chdir(self.old_cwd)
        self.temp.cleanup()

    def write(self, path, text):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8")

    def run_check(self, paths, previous=None):
        failures = []
        current = health.current_authorities(failures)
        count = health.check_code_doc(set(paths), current, previous or {}, failures)
        return count, failures

    def test_related_code_without_authority_fails(self):
        count, failures = self.run_check([self.source])
        self.assertEqual(1, count)
        self.assertTrue(any("without its authority document" in f for f in failures))

    def test_unrelated_code_does_not_require_this_document(self):
        count, failures = self.run_check([Path("Packages/other/Independent.cs")])
        self.assertEqual((0, []), (count, failures))

    def test_related_code_and_corrected_authority_pass(self):
        self.write(self.doc, self.doc.read_text() + "The new failure behavior is documented.\n")
        self.assertEqual((1, []), self.run_check([self.source, self.doc]))

    def test_only_updating_an_index_cannot_satisfy_authority(self):
        _, failures = self.run_check([self.source, Path("Docs/README.md")])
        self.assertTrue(failures)

    def test_deleted_code_still_requires_authority(self):
        self.source.unlink()
        _, failures = self.run_check([self.source])
        self.assertTrue(any("without its authority document" in f for f in failures))

    def test_deleted_authority_cannot_disable_baseline_mapping(self):
        baseline = {self.doc: ("Packages/example/Feature*.cs",)}
        self.doc.unlink()
        _, failures = self.run_check([self.source, self.doc], baseline)
        self.assertTrue(any("removed without a replacement" in f for f in failures))

    def test_renamed_authority_must_be_changed_and_keep_mapping(self):
        baseline = {self.doc: ("Packages/example/Feature*.cs",)}
        replacement = Path("Docs/Guides/Renamed.md")
        self.doc.rename(replacement)
        self.assertEqual((1, []), self.run_check([self.source, self.doc, replacement], baseline))

    def test_moved_source_requires_the_updated_existing_authority(self):
        baseline = {self.doc: ("Packages/example/Feature*.cs",)}
        self.source.unlink()
        moved = Path("Packages/example/Renamed.cs")
        self.write(moved, "moved implementation\n")
        self.write(self.doc, "> Code-Doc: `Packages/example/Renamed.cs`\nThe source moved.\n")
        self.assertEqual((2, []), self.run_check([self.source, moved, self.doc], baseline))

    def test_removing_mapping_while_source_remains_cannot_disable_check(self):
        baseline = {self.doc: ("Packages/example/Feature*.cs",)}
        self.write(self.doc, "No declaration.\n")
        _, failures = self.run_check([self.source, self.doc], baseline)
        self.assertTrue(any("removed without a replacement" in f for f in failures))

    def test_whitespace_edit_can_pass_so_content_still_needs_review(self):
        self.write(self.doc, self.doc.read_text() + "\n")
        self.assertEqual((1, []), self.run_check([self.source, self.doc]))

    def test_multiple_related_authorities_each_need_processing(self):
        second = Path("Docs/Guides/Second.md")
        self.write(second, "> Code-Doc: `Packages/example/Feature.cs`\n")
        _, failures = self.run_check([self.source, self.doc])
        self.assertTrue(any("Second.md" in f for f in failures))

    def test_bad_declaration_or_typo_fails(self):
        for declaration in ["> Code-Doc: `../outside/*`", "> Code-Doc: missing-quotes",
                            "> Code-Doc: `Packages/typo/*.cs`"]:
            with self.subTest(declaration=declaration):
                self.write(self.doc, declaration + "\n")
                _, failures = self.run_check([])
                self.assertTrue(failures)

    def test_changed_paths_support_bom_crlf_and_windows_separators(self):
        path = Path("changed.txt")
        path.write_bytes(b"\xef\xbb\xbfPackages\\example\\Feature.cs\r\n\r\n")
        self.assertEqual([self.source], health.changed_paths(path))

    def test_main_returns_real_exit_codes_for_three_delivery_cases(self):
        # Entry fixtures exercise the existing structural check too. No Git writes
        # or Unity are needed; baseline retrieval alone is isolated from the fixture.
        self.write(Path("AGENTS.md"), "Repository rules.\n")
        self.write(Path("Docs/README.md"), "> Status: Current\n## 文档状态\n## 当前事实源\n## 归档和决策\nCurrent Guide Design Archive Draft\n")
        self.write(Path("Docs/PROJECT_INDEX.md"), "> Status: Current\n## Task Routes\nConflict Rule\n")
        self.write(Path("Docs/Tasks/README.md"), "> Status: Archive\n")
        for paths, expected in [([self.source], 1), ([Path("Tools/unrelated.py")], 0),
                                ([self.source, self.doc], 0)]:
            with self.subTest(paths=paths):
                listing = Path("changed.txt")
                listing.write_text("\n".join(p.as_posix() for p in paths), encoding="utf-8")
                with patch.object(health, "baseline_authorities", return_value={}), \
                        contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
                    self.assertEqual(expected, health.main([str(listing)]))


if __name__ == "__main__":
    unittest.main()
