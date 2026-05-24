from __future__ import annotations

import copy
import pathlib
import sys
import tempfile
import unittest


ROOT = pathlib.Path(__file__).resolve().parents[3]
TOOL_ROOT = ROOT / "Tools" / "MxFrameworkStoryAuthoring"
sys.path.insert(0, str(TOOL_ROOT))

import story_authoring  # noqa: E402


FIXTURE_MARKDOWN = TOOL_ROOT / "fixtures" / "markdown" / "basic_choice_story.md"
FIXTURE_JSON = TOOL_ROOT / "fixtures" / "generated" / "basic_choice_story.story.json"


class StoryAuthoringTests(unittest.TestCase):
    def load_fixture(self) -> dict:
        return story_authoring.load_story_draft(FIXTURE_JSON)

    def diagnostic_codes(self, draft: dict) -> set[str]:
        return {diagnostic.code for diagnostic in story_authoring.validate_story_draft(draft)}

    def test_import_markdown_matches_generated_fixture(self) -> None:
        draft, diagnostics = story_authoring.import_markdown_file(FIXTURE_MARKDOWN)

        self.assertEqual([], diagnostics)
        self.assertEqual(self.load_fixture(), draft)

    def test_validate_generated_fixture_succeeds(self) -> None:
        diagnostics = story_authoring.validate_story_draft(self.load_fixture())

        self.assertEqual([], diagnostics)

    def test_validate_duplicate_ids_reports_diagnostic(self) -> None:
        draft = self.load_fixture()
        draft["beats"].append(copy.deepcopy(draft["beats"][0]))

        self.assertIn("DuplicateId", self.diagnostic_codes(draft))

    def test_validate_missing_entry_beat_reports_diagnostic(self) -> None:
        draft = self.load_fixture()
        draft["graphs"][0]["EntryBeatId"] = 999999

        self.assertIn("MissingEntryBeat", self.diagnostic_codes(draft))

    def test_validate_missing_text_key_reports_diagnostic(self) -> None:
        draft = self.load_fixture()
        draft["textKeys"] = [key for key in draft["textKeys"] if key != 442301]

        self.assertIn("MissingTextKey", self.diagnostic_codes(draft))

    def test_validate_invalid_branch_target_reports_diagnostic(self) -> None:
        draft = self.load_fixture()
        draft["branches"].append(
            {
                "Id": 442701,
                "GraphId": 442001,
                "BeatId": 442102,
                "TargetBeatId": 999999,
                "ConditionFactId": 0,
                "Priority": 10,
                "IsFallback": True,
            }
        )

        self.assertIn("InvalidBranchTarget", self.diagnostic_codes(draft))

    def test_validate_invalid_choice_target_reports_diagnostic(self) -> None:
        draft = self.load_fixture()
        draft["choices"][0]["TargetBeatId"] = 999999

        self.assertIn("InvalidChoiceTarget", self.diagnostic_codes(draft))

    def test_validate_invalid_trigger_and_effect_ids_report_diagnostics(self) -> None:
        draft = self.load_fixture()
        draft["beats"][0]["TriggerIds"] = [0]
        draft["choices"][0]["EffectIds"] = [0]

        codes = self.diagnostic_codes(draft)
        self.assertIn("InvalidTriggerId", codes)
        self.assertIn("InvalidEffectId", codes)

    def test_import_unsupported_directive_reports_diagnostic(self) -> None:
        markdown = """---
graph: 442001
entry: intro
source: unsupported_directive
---

# Unsupported Directive Story

## Beat intro
id: 442101
camera: 442901
"""
        with tempfile.TemporaryDirectory() as tmpdir:
            path = pathlib.Path(tmpdir) / "unsupported.md"
            path.write_text(markdown, encoding="utf-8")

            draft, diagnostics = story_authoring.import_markdown_file(path)

        self.assertIsNone(draft)
        self.assertIn("UnsupportedDirective", {diagnostic.code for diagnostic in diagnostics})

    def test_validate_unsupported_step_kind_and_wait_policy_report_diagnostics(self) -> None:
        draft = self.load_fixture()
        draft["steps"][0]["Kind"] = "Cutscene"
        draft["steps"][0]["WaitPolicy"] = "WaitForMoonPhase"

        codes = self.diagnostic_codes(draft)
        self.assertIn("UnsupportedStepKind", codes)
        self.assertIn("UnsupportedWaitPolicy", codes)


if __name__ == "__main__":
    unittest.main()
