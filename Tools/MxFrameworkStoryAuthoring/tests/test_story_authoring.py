from __future__ import annotations

import copy
import contextlib
import io
import json
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

    def test_validate_rejects_non_object_draft_without_traceback(self) -> None:
        diagnostics = story_authoring.validate_story_draft([])

        self.assertEqual({"InvalidDraftShape"}, {diagnostic.code for diagnostic in diagnostics})

    def test_validate_rejects_missing_or_non_list_top_level_tables(self) -> None:
        diagnostics = story_authoring.validate_story_draft(
            {
                "schema": "mx.story.config.draft.v1",
                "graphs": "bad",
            }
        )

        codes = {diagnostic.code for diagnostic in diagnostics}
        paths = {diagnostic.path for diagnostic in diagnostics}
        self.assertIn("InvalidDraftShape", codes)
        self.assertIn("graphs", paths)
        self.assertIn("beats", paths)

    def test_command_validate_returns_failure_for_malformed_json_draft(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            path = pathlib.Path(tmpdir) / "bad.story.json"
            path.write_text(json.dumps(["bad"]), encoding="utf-8")

            stderr = io.StringIO()
            with contextlib.redirect_stderr(stderr):
                exit_code = story_authoring.command_validate(type("Args", (), {"story_json": path})())

        self.assertEqual(1, exit_code)
        payload = json.loads(stderr.getvalue())
        self.assertEqual("InvalidDraftShape", payload[0]["code"])

    def test_validate_duplicate_ids_reports_diagnostic(self) -> None:
        draft = self.load_fixture()
        draft["beats"].append(copy.deepcopy(draft["beats"][0]))

        self.assertIn("DuplicateId", self.diagnostic_codes(draft))

    def test_validate_duplicate_step_branch_choice_ids_are_scoped_by_beat(self) -> None:
        draft = self.load_fixture()

        duplicate_step = copy.deepcopy(draft["steps"][0])
        duplicate_step["BeatId"] = 442102
        duplicate_step["TextKey"] = 442303
        draft["steps"].append(duplicate_step)
        draft["branches"].extend(
            [
                {
                    "Id": 442701,
                    "GraphId": 442001,
                    "BeatId": 442101,
                    "TargetBeatId": 0,
                    "ConditionFactId": 0,
                    "Priority": 10,
                    "IsFallback": True,
                },
                {
                    "Id": 442701,
                    "GraphId": 442001,
                    "BeatId": 442102,
                    "TargetBeatId": 0,
                    "ConditionFactId": 0,
                    "Priority": 10,
                    "IsFallback": True,
                },
            ]
        )
        duplicate_choice = copy.deepcopy(draft["choices"][0])
        duplicate_choice["BeatId"] = 442102
        duplicate_choice["TargetBeatId"] = 0
        draft["choices"].append(duplicate_choice)

        self.assertNotIn("DuplicateId", self.diagnostic_codes(draft))

    def test_validate_duplicate_step_id_in_same_beat_reports_diagnostic(self) -> None:
        draft = self.load_fixture()
        duplicate_step = copy.deepcopy(draft["steps"][0])
        duplicate_step["TextKey"] = 442303
        draft["steps"].append(duplicate_step)

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

    def test_validate_invalid_branch_condition_fact_reports_diagnostic(self) -> None:
        draft = self.load_fixture()
        draft["branches"].append(
            {
                "Id": 442701,
                "GraphId": 442001,
                "BeatId": 442102,
                "TargetBeatId": 0,
                "ConditionFactId": -1,
                "Priority": 10,
                "IsFallback": True,
            }
        )

        self.assertIn("InvalidFactReference", self.diagnostic_codes(draft))

    def test_validate_invalid_choice_target_reports_diagnostic(self) -> None:
        draft = self.load_fixture()
        draft["choices"][0]["TargetBeatId"] = 999999

        self.assertIn("InvalidChoiceTarget", self.diagnostic_codes(draft))

    def test_validate_choice_condition_fact_must_exist_and_be_bool(self) -> None:
        missing_fact_draft = self.load_fixture()
        missing_fact_draft["choices"][0]["ConditionFactId"] = 999999

        non_bool_draft = self.load_fixture()
        non_bool_draft["facts"].append(
            {
                "Id": 442602,
                "Namespace": 442001,
                "ValueKind": "Int32",
            }
        )
        non_bool_draft["choices"][0]["ConditionFactId"] = 442602

        self.assertIn("InvalidFactReference", self.diagnostic_codes(missing_fact_draft))
        self.assertIn("InvalidFactReference", self.diagnostic_codes(non_bool_draft))

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
