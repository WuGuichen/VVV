from __future__ import annotations

import copy
import pathlib
import sys
import unittest


TOOL_ROOT = pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TOOL_ROOT))

import story_authoring  # noqa: E402


class StoryTargetResolutionTests(unittest.TestCase):
    def build_outline(
        self,
        slug: str = "middle",
        target: str | None = None,
        entry: str = "intro",
        graph_id: int = 101,
        destination_id: int = 2,
        source_path: str = "synthetic/targets.md",
    ) -> tuple[dict, list]:
        markdown = f"""---
graph: {graph_id}
entry: {entry}
source: target_resolution
---
## Beat intro
id: 1
choice: 11 | 3001 | {slug if target is None else target} | effect 4001 | Continue.
## Beat {slug}
id: {destination_id}
line: 1001 | NoWait | Destination.
"""
        outline, diagnostics = story_authoring.parse_markdown_outline(markdown.splitlines(), source_path)
        self.assertEqual([], diagnostics)
        return story_authoring.build_story_draft(outline, source_path)

    def assert_valid(self, draft: dict, diagnostics: list) -> None:
        self.assertEqual([], diagnostics)
        self.assertEqual([], story_authoring.validate_story_draft(draft))

    def combine_graphs(self, first: dict, second: dict) -> dict:
        combined = copy.deepcopy(first)
        for table in ("graphs", "beats", "steps", "branches", "choices", "facts", "texts"):
            combined[table].extend(copy.deepcopy(second[table]))
        combined["textKeys"] = sorted(set(first["textKeys"] + second["textKeys"]))
        return combined

    def test_numeric_choice_slug_uses_explicit_id(self) -> None:
        for slug in ("1", "123", "001"):
            with self.subTest(slug=slug):
                draft, diagnostics = self.build_outline(slug=slug)
                self.assertEqual(2, draft["choices"][0]["TargetBeatId"])
                self.assert_valid(draft, diagnostics)

    def test_numeric_entry_and_choice_use_same_explicit_id(self) -> None:
        for slug in ("1", "123"):
            with self.subTest(slug=slug):
                draft, diagnostics = self.build_outline(slug=slug, entry=slug)
                self.assertEqual(2, draft["graphs"][0]["EntryBeatId"])
                self.assertEqual(2, draft["choices"][0]["TargetBeatId"])
                self.assert_valid(draft, diagnostics)

    def test_numeric_slug_is_matched_before_integer_normalization(self) -> None:
        draft, diagnostics = self.build_outline(slug="01")
        self.assert_valid(draft, diagnostics)
        other, other_diagnostics = self.build_outline(slug="01", target="1")
        self.assert_valid(other, other_diagnostics)
        self.assertEqual(2, draft["choices"][0]["TargetBeatId"])
        self.assertEqual(1, other["choices"][0]["TargetBeatId"])

    def test_completion_targets_keep_precedence_over_declared_slugs(self) -> None:
        for target in ("0", "complete", "COMPLETE", "end-graph", "graph-complete"):
            with self.subTest(target=target):
                draft, diagnostics = self.build_outline(slug=target)
                self.assertEqual(0, draft["choices"][0]["TargetBeatId"])
                self.assert_valid(draft, diagnostics)

    def test_direct_numeric_id_without_matching_slug_remains_supported(self) -> None:
        draft, diagnostics = self.build_outline(target="2")
        self.assertEqual(2, draft["choices"][0]["TargetBeatId"])
        self.assert_valid(draft, diagnostics)

    def test_same_numeric_slug_is_local_to_each_document(self) -> None:
        first, first_diagnostics = self.build_outline(slug="1", source_path="synthetic/first.md")
        second, second_diagnostics = self.build_outline(
            slug="1", graph_id=202, destination_id=3, source_path="synthetic/second.md"
        )
        self.assert_valid(first, first_diagnostics)
        self.assert_valid(second, second_diagnostics)
        self.assertEqual(2, first["choices"][0]["TargetBeatId"])
        self.assertEqual(3, second["choices"][0]["TargetBeatId"])
        self.assertEqual([], story_authoring.validate_story_draft(self.combine_graphs(first, second)))

    def test_missing_slug_is_not_resolved_from_another_document(self) -> None:
        foreign, foreign_diagnostics = self.build_outline(slug="absent", graph_id=202)
        self.assert_valid(foreign, foreign_diagnostics)
        draft, diagnostics = self.build_outline(target="absent", source_path="synthetic/local.md")
        self.assertEqual(-1, draft["choices"][0]["TargetBeatId"])
        self.assertEqual(
            [{
                "code": "InvalidChoiceTarget",
                "severity": "Error",
                "message": "Choice target beat slug 'absent' does not resolve to a beat id.",
                "path": "choices[0].TargetBeatId",
                "sourcePath": "synthetic/local.md",
                "line": 8,
            }],
            [diagnostic.to_json() for diagnostic in diagnostics],
        )

    def test_slug_matching_remains_case_sensitive(self) -> None:
        draft, diagnostics = self.build_outline(slug="Middle", target="middle")
        self.assertEqual(-1, draft["choices"][0]["TargetBeatId"])
        self.assertEqual(["InvalidChoiceTarget"], [diagnostic.code for diagnostic in diagnostics])

    def test_missing_direct_numeric_id_is_not_satisfied_by_foreign_graph(self) -> None:
        local, diagnostics = self.build_outline(target="2", destination_id=3)
        self.assertEqual([], diagnostics)
        foreign, foreign_diagnostics = self.build_outline(graph_id=202)
        self.assert_valid(foreign, foreign_diagnostics)
        combined = self.combine_graphs(local, foreign)
        before = copy.deepcopy(combined)
        first = [diagnostic.to_json() for diagnostic in story_authoring.validate_story_draft(combined)]
        self.assertEqual(
            [{
                "code": "InvalidChoiceTarget",
                "severity": "Error",
                "message": "Story choice target beat 2 is missing.",
                "path": "choices[0].TargetBeatId",
                "sourcePath": "synthetic/targets.md",
            }],
            first,
        )
        self.assertEqual(
            first,
            [diagnostic.to_json() for diagnostic in story_authoring.validate_story_draft(combined)],
        )
        self.assertEqual(before, combined)


if __name__ == "__main__":
    unittest.main()
