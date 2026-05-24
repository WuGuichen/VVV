#!/usr/bin/env python3
"""External Story authoring CLI for Markdown Story Outline v1 drafts."""

from __future__ import annotations

import argparse
import json
import pathlib
import re
import sys
from dataclasses import dataclass
from typing import Any, Dict, List, Optional, Sequence, Tuple


SCHEMA = "mx.story.config.draft.v1"
GRAPH_VERSION = 1

SUPPORTED_STEP_KINDS = {"Line", "Presentation", "SetFact", "Wait"}
SUPPORTED_WAIT_POLICIES = {"NoWait", "WaitForCommand", "WaitWithFrameTimeout"}
SUPPORTED_VALUE_KINDS = {"Bool", "Int32", "Int64", "Fix64", "StringRef"}

STEP_KIND_BY_VALUE = {
    1: "Line",
    2: "Presentation",
    3: "SetFact",
    4: "Wait",
}

WAIT_POLICY_BY_VALUE = {
    0: "NoWait",
    1: "WaitForCommand",
    2: "WaitWithFrameTimeout",
}

VALUE_KIND_BY_VALUE = {
    1: "Bool",
    2: "Int32",
    3: "Int64",
    4: "Fix64",
    5: "StringRef",
}

BEAT_HEADING_RE = re.compile(r"^##\s+Beat\s+([A-Za-z0-9_.-]+)\s*$")
DIRECTIVE_RE = re.compile(r"^([A-Za-z][A-Za-z0-9_-]*)\s*:\s*(.*)$")


@dataclass(frozen=True)
class Diagnostic:
    code: str
    severity: str
    message: str
    path: str
    source_path: str = ""
    line: int = 0

    def to_json(self) -> Dict[str, Any]:
        payload: Dict[str, Any] = {
            "code": self.code,
            "severity": self.severity,
            "message": self.message,
            "path": self.path,
        }
        if self.source_path:
            payload["sourcePath"] = self.source_path
        if self.line > 0:
            payload["line"] = self.line
        return payload


@dataclass
class StoryText:
    text_key: int
    text: str
    usage: str
    source_line: int
    beat_id: int
    row_id: int


@dataclass
class StoryBeatDraft:
    slug: str
    source_line: int
    beat_id: int = 0
    trigger_ids: Optional[List[int]] = None
    operations: Optional[List[Dict[str, Any]]] = None

    def __post_init__(self) -> None:
        if self.trigger_ids is None:
            self.trigger_ids = []
        if self.operations is None:
            self.operations = []


@dataclass
class MarkdownOutline:
    graph_id: int
    entry_slug: str
    source_name: str
    beats: List[StoryBeatDraft]


def display_path(path: pathlib.Path) -> str:
    resolved = path.resolve()
    try:
        return resolved.relative_to(pathlib.Path.cwd().resolve()).as_posix()
    except ValueError:
        return path.as_posix()


def import_markdown_file(path: pathlib.Path) -> Tuple[Optional[Dict[str, Any]], List[Diagnostic]]:
    source_path = display_path(path)
    try:
        text = path.read_text(encoding="utf-8")
    except OSError as exc:
        return None, [
            Diagnostic(
                "InputReadFailed",
                "Error",
                f"Could not read Markdown story outline: {exc}.",
                "",
                source_path,
            )
        ]

    outline, diagnostics = parse_markdown_outline(text.splitlines(), source_path)
    if diagnostics:
        return None, diagnostics

    assert outline is not None
    draft, build_diagnostics = build_story_draft(outline, source_path)
    if build_diagnostics:
        return None, build_diagnostics

    validation_diagnostics = validate_story_draft(draft)
    if validation_diagnostics:
        return None, validation_diagnostics

    return draft, []


def parse_markdown_outline(lines: Sequence[str], source_path: str) -> Tuple[Optional[MarkdownOutline], List[Diagnostic]]:
    diagnostics: List[Diagnostic] = []
    header, body_start = parse_front_matter(lines, source_path, diagnostics)
    graph_id = parse_positive_int(header.get("graph"), "frontMatter.graph", source_path, 0, diagnostics)
    entry_slug = header.get("entry", "").strip()
    source_name = header.get("source", "").strip()

    if not entry_slug:
        diagnostics.append(
            Diagnostic(
                "MissingEntryBeat",
                "Error",
                "Markdown Story Outline front matter must declare an entry beat slug.",
                "frontMatter.entry",
                source_path,
            )
        )

    beats: List[StoryBeatDraft] = []
    current_beat: Optional[StoryBeatDraft] = None
    seen_slugs: Dict[str, int] = {}

    for index in range(body_start, len(lines)):
        line_no = index + 1
        raw_line = lines[index]
        stripped = raw_line.strip()
        if not stripped or stripped.startswith("# ") or stripped.startswith("<!--"):
            continue

        heading = BEAT_HEADING_RE.match(stripped)
        if heading:
            slug = heading.group(1)
            if slug in seen_slugs:
                diagnostics.append(
                    Diagnostic(
                        "DuplicateId",
                        "Error",
                        f"Beat slug '{slug}' is declared more than once.",
                        f"beats[{len(beats)}].slug",
                        source_path,
                        line_no,
                    )
                )
            seen_slugs[slug] = line_no
            current_beat = StoryBeatDraft(slug=slug, source_line=line_no)
            beats.append(current_beat)
            continue

        match = DIRECTIVE_RE.match(stripped)
        if match is None:
            diagnostics.append(
                Diagnostic(
                    "UnsupportedDirective",
                    "Error",
                    f"Unsupported Markdown Story Outline line: {stripped}",
                    f"line[{line_no}]",
                    source_path,
                    line_no,
                )
            )
            continue

        directive = match.group(1)
        value = match.group(2).strip()
        normalized = directive.lower()
        if current_beat is None:
            diagnostics.append(
                Diagnostic(
                    "UnsupportedDirective",
                    "Error",
                    f"Directive '{directive}' must be inside a beat.",
                    f"line[{line_no}]",
                    source_path,
                    line_no,
                )
            )
            continue

        if normalized == "id":
            current_beat.beat_id = parse_positive_int(
                value,
                f"beats[{len(beats) - 1}].Id",
                source_path,
                line_no,
                diagnostics,
            )
        elif normalized == "trigger":
            for trigger_id in parse_id_list(value, source_path, line_no, f"beats[{len(beats) - 1}].TriggerIds", diagnostics):
                current_beat.trigger_ids.append(trigger_id)
        elif normalized == "line":
            operation = parse_line_operation(value, source_path, line_no, diagnostics)
            if operation is not None:
                current_beat.operations.append(operation)
        elif normalized == "choice":
            operation = parse_choice_operation(value, source_path, line_no, diagnostics)
            if operation is not None:
                current_beat.operations.append(operation)
        elif normalized == "set-fact":
            operation = parse_set_fact_operation(value, source_path, line_no, diagnostics)
            if operation is not None:
                current_beat.operations.append(operation)
        else:
            diagnostics.append(
                Diagnostic(
                    "UnsupportedDirective",
                    "Error",
                    f"Unsupported Markdown Story Outline directive '{directive}'.",
                    f"line[{line_no}]",
                    source_path,
                    line_no,
                )
            )

    if not beats:
        diagnostics.append(
            Diagnostic(
                "MissingEntryBeat",
                "Error",
                "Markdown Story Outline must declare at least one beat.",
                "beats",
                source_path,
            )
        )

    for index, beat in enumerate(beats):
        if beat.beat_id <= 0:
            diagnostics.append(
                Diagnostic(
                    "DuplicateId",
                    "Error",
                    f"Beat '{beat.slug}' is missing a positive numeric id.",
                    f"beats[{index}].Id",
                    source_path,
                    beat.source_line,
                )
            )

    if diagnostics:
        return None, diagnostics

    return MarkdownOutline(graph_id=graph_id, entry_slug=entry_slug, source_name=source_name, beats=beats), []


def parse_front_matter(lines: Sequence[str], source_path: str, diagnostics: List[Diagnostic]) -> Tuple[Dict[str, str], int]:
    header: Dict[str, str] = {}
    if not lines or lines[0].strip() != "---":
        diagnostics.append(
            Diagnostic(
                "UnsupportedDirective",
                "Error",
                "Markdown Story Outline v1 must start with YAML-style front matter.",
                "frontMatter",
                source_path,
                1,
            )
        )
        return header, 0

    index = 1
    while index < len(lines):
        line_no = index + 1
        stripped = lines[index].strip()
        if stripped == "---":
            return header, index + 1
        match = DIRECTIVE_RE.match(stripped)
        if match is None:
            diagnostics.append(
                Diagnostic(
                    "UnsupportedDirective",
                    "Error",
                    f"Unsupported front matter line: {stripped}",
                    f"frontMatter.line[{line_no}]",
                    source_path,
                    line_no,
                )
            )
        else:
            header[match.group(1).lower()] = match.group(2).strip()
        index += 1

    diagnostics.append(
        Diagnostic(
            "UnsupportedDirective",
            "Error",
            "Markdown Story Outline front matter is not closed.",
            "frontMatter",
            source_path,
            1,
        )
    )
    return header, len(lines)


def parse_line_operation(
    value: str,
    source_path: str,
    line_no: int,
    diagnostics: List[Diagnostic],
) -> Optional[Dict[str, Any]]:
    parts = split_pipe(value)
    if len(parts) < 3:
        diagnostics.append(
            Diagnostic(
                "UnsupportedDirective",
                "Error",
                "Line directive must be: line: <textKey> | <waitPolicy> | <text>.",
                f"line[{line_no}]",
                source_path,
                line_no,
            )
        )
        return None

    text_key = parse_positive_int(parts[0], f"line[{line_no}].TextKey", source_path, line_no, diagnostics)
    wait_policy = parts[1]
    text = " | ".join(parts[2:]).strip()
    return {
        "kind": "Line",
        "textKey": text_key,
        "waitPolicy": wait_policy,
        "text": text,
        "line": line_no,
    }


def parse_choice_operation(
    value: str,
    source_path: str,
    line_no: int,
    diagnostics: List[Diagnostic],
) -> Optional[Dict[str, Any]]:
    parts = split_pipe(value)
    if len(parts) < 5:
        diagnostics.append(
            Diagnostic(
                "UnsupportedDirective",
                "Error",
                "Choice directive must be: choice: <choiceId> | <labelTextKey> | <targetBeatSlug> | effect <id> | <label>.",
                f"line[{line_no}]",
                source_path,
                line_no,
            )
        )
        return None

    choice_id = parse_positive_int(parts[0], f"line[{line_no}].ChoiceId", source_path, line_no, diagnostics)
    label_text_key = parse_positive_int(parts[1], f"line[{line_no}].LabelTextKey", source_path, line_no, diagnostics)
    target_slug = parts[2].strip()
    effect_ids = parse_effect_ids(parts[3], source_path, line_no, diagnostics)
    label = " | ".join(parts[4:]).strip()
    return {
        "kind": "Choice",
        "choiceId": choice_id,
        "labelTextKey": label_text_key,
        "targetSlug": target_slug,
        "effectIds": effect_ids,
        "label": label,
        "line": line_no,
    }


def parse_set_fact_operation(
    value: str,
    source_path: str,
    line_no: int,
    diagnostics: List[Diagnostic],
) -> Optional[Dict[str, Any]]:
    parts = split_pipe(value)
    if len(parts) != 3:
        diagnostics.append(
            Diagnostic(
                "UnsupportedDirective",
                "Error",
                "Set-fact directive must be: set-fact: <factId> | <valueKind> | <rawValue>.",
                f"line[{line_no}]",
                source_path,
                line_no,
            )
        )
        return None

    fact_id = parse_positive_int(parts[0], f"line[{line_no}].FactId", source_path, line_no, diagnostics)
    value_kind = parts[1].strip()
    raw_value = parse_fact_raw_value(value_kind, parts[2].strip(), source_path, line_no, diagnostics)
    return {
        "kind": "SetFact",
        "factId": fact_id,
        "valueKind": value_kind,
        "rawValue": raw_value,
        "line": line_no,
    }


def split_pipe(value: str) -> List[str]:
    return [part.strip() for part in value.split("|")]


def parse_id_list(
    value: str,
    source_path: str,
    line_no: int,
    path: str,
    diagnostics: List[Diagnostic],
) -> List[int]:
    ids: List[int] = []
    for token in re.split(r"[\s,]+", value.strip()):
        if not token:
            continue
        ids.append(parse_positive_int(token, path, source_path, line_no, diagnostics))
    return ids


def parse_effect_ids(value: str, source_path: str, line_no: int, diagnostics: List[Diagnostic]) -> List[int]:
    stripped = value.strip()
    if stripped.lower().startswith("effect "):
        stripped = stripped[7:].strip()
    elif stripped.lower().startswith("effects "):
        stripped = stripped[8:].strip()
    else:
        diagnostics.append(
            Diagnostic(
                "UnsupportedDirective",
                "Error",
                "Choice effect segment must start with 'effect' or 'effects'.",
                f"line[{line_no}].EffectIds",
                source_path,
                line_no,
            )
        )
        return []
    return parse_id_list(stripped, source_path, line_no, f"line[{line_no}].EffectIds", diagnostics)


def parse_positive_int(
    value: Optional[str],
    path: str,
    source_path: str,
    line_no: int,
    diagnostics: List[Diagnostic],
) -> int:
    parsed = coerce_int(value)
    if parsed is None or parsed <= 0:
        diagnostics.append(
            Diagnostic(
                "DuplicateId",
                "Error",
                f"Expected a positive integer id at {path}.",
                path,
                source_path,
                line_no,
            )
        )
        return 0
    return parsed


def parse_fact_raw_value(
    value_kind: str,
    raw_value: str,
    source_path: str,
    line_no: int,
    diagnostics: List[Diagnostic],
) -> int:
    normalized = normalize_enum(value_kind, VALUE_KIND_BY_VALUE)
    if normalized == "Bool":
        lowered = raw_value.lower()
        if lowered in {"true", "1"}:
            return 1
        if lowered in {"false", "0"}:
            return 0
        diagnostics.append(
            Diagnostic(
                "InvalidFactValue",
                "Error",
                "Bool fact raw value must be true, false, 1, or 0.",
                f"line[{line_no}].FactValueRaw",
                source_path,
                line_no,
            )
        )
        return 0

    parsed = coerce_int(raw_value)
    if parsed is None:
        diagnostics.append(
            Diagnostic(
                "InvalidFactValue",
                "Error",
                "Fact raw value must be an integer for this value kind.",
                f"line[{line_no}].FactValueRaw",
                source_path,
                line_no,
            )
        )
        return 0
    return parsed


def build_story_draft(outline: MarkdownOutline, source_path: str) -> Tuple[Dict[str, Any], List[Diagnostic]]:
    diagnostics: List[Diagnostic] = []
    slug_to_id: Dict[str, int] = {}
    for beat in outline.beats:
        slug_to_id[beat.slug] = beat.beat_id

    entry_beat_id = slug_to_id.get(outline.entry_slug, 0)
    if entry_beat_id <= 0:
        diagnostics.append(
            Diagnostic(
                "MissingEntryBeat",
                "Error",
                f"Entry beat slug '{outline.entry_slug}' does not resolve to a beat id.",
                "graphs[0].EntryBeatId",
                source_path,
            )
        )

    text_keys: List[int] = []
    texts: List[StoryText] = []
    steps: List[Dict[str, Any]] = []
    choices: List[Dict[str, Any]] = []
    facts: List[Dict[str, Any]] = []
    fact_keys: set[Tuple[int, int]] = set()

    beats: List[Dict[str, Any]] = []
    for beat_index, beat in enumerate(outline.beats):
        beat_id = beat.beat_id
        choice_count = sum(1 for operation in beat.operations or [] if operation["kind"] == "Choice")
        beats.append(
            {
                "Id": beat_id,
                "GraphId": outline.graph_id,
                "SortOrder": (beat_index + 1) * 10,
                "ChoiceSetId": beat_id if choice_count else 0,
                "TriggerIds": sorted(beat.trigger_ids or []),
            }
        )

        step_sort = 10
        choice_sort = 10
        for operation in beat.operations or []:
            if operation["kind"] == "Line":
                text_key = int(operation["textKey"])
                steps.append(
                    {
                        "Id": text_key,
                        "GraphId": outline.graph_id,
                        "BeatId": beat_id,
                        "SortOrder": step_sort,
                        "Kind": "Line",
                        "TextKey": text_key,
                        "SpeakerId": 0,
                        "ResourceId": 0,
                        "WaitPolicy": operation["waitPolicy"],
                        "FactNamespace": 0,
                        "FactId": 0,
                        "FactValueKind": "None",
                        "FactValueRaw": 0,
                        "AuxId": 0,
                    }
                )
                step_sort += 10
                text_keys.append(text_key)
                texts.append(
                    StoryText(
                        text_key=text_key,
                        text=operation["text"],
                        usage="Line",
                        source_line=int(operation["line"]),
                        beat_id=beat_id,
                        row_id=text_key,
                    )
                )
            elif operation["kind"] == "SetFact":
                fact_id = int(operation["factId"])
                fact_namespace = outline.graph_id
                value_kind = str(operation["valueKind"])
                steps.append(
                    {
                        "Id": fact_id,
                        "GraphId": outline.graph_id,
                        "BeatId": beat_id,
                        "SortOrder": step_sort,
                        "Kind": "SetFact",
                        "TextKey": 0,
                        "SpeakerId": 0,
                        "ResourceId": 0,
                        "WaitPolicy": "NoWait",
                        "FactNamespace": fact_namespace,
                        "FactId": fact_id,
                        "FactValueKind": value_kind,
                        "FactValueRaw": int(operation["rawValue"]),
                        "AuxId": 0,
                    }
                )
                step_sort += 10
                fact_key = (fact_namespace, fact_id)
                if fact_key not in fact_keys:
                    fact_keys.add(fact_key)
                    facts.append(
                        {
                            "Id": fact_id,
                            "Namespace": fact_namespace,
                            "ValueKind": value_kind,
                        }
                    )
            elif operation["kind"] == "Choice":
                choice_id = int(operation["choiceId"])
                label_text_key = int(operation["labelTextKey"])
                target_slug = str(operation["targetSlug"]).strip()
                target_beat_id = resolve_target_beat_id(target_slug, slug_to_id)
                if target_beat_id is None:
                    diagnostics.append(
                        Diagnostic(
                            "InvalidChoiceTarget",
                            "Error",
                            f"Choice target beat slug '{target_slug}' does not resolve to a beat id.",
                            f"choices[{len(choices)}].TargetBeatId",
                            source_path,
                            int(operation["line"]),
                        )
                    )
                    target_beat_id = -1
                choices.append(
                    {
                        "Id": choice_id,
                        "GraphId": outline.graph_id,
                        "BeatId": beat_id,
                        "SortOrder": choice_sort,
                        "LabelTextKey": label_text_key,
                        "TargetBeatId": target_beat_id,
                        "ConditionFactId": 0,
                        "EffectIds": sorted(int(effect_id) for effect_id in operation["effectIds"]),
                    }
                )
                choice_sort += 10
                text_keys.append(label_text_key)
                texts.append(
                    StoryText(
                        text_key=label_text_key,
                        text=operation["label"],
                        usage="ChoiceLabel",
                        source_line=int(operation["line"]),
                        beat_id=beat_id,
                        row_id=choice_id,
                    )
                )

    draft = {
        "schema": SCHEMA,
        "sourcePath": source_path,
        "graphs": [
            {
                "Id": outline.graph_id,
                "Version": GRAPH_VERSION,
                "EntryBeatId": entry_beat_id,
                "SourcePath": source_path,
            }
        ],
        "beats": beats,
        "steps": steps,
        "branches": [],
        "choices": choices,
        "facts": sorted(facts, key=lambda item: (item["Namespace"], item["Id"])),
        "textKeys": sorted(set(text_keys)),
        "texts": [
            {
                "TextKey": text.text_key,
                "Text": text.text,
                "Usage": text.usage,
                "SourcePath": source_path,
                "SourceLine": text.source_line,
                "GraphId": outline.graph_id,
                "BeatId": text.beat_id,
                "RowId": text.row_id,
            }
            for text in texts
        ],
    }
    return draft, diagnostics


def resolve_target_beat_id(target: str, slug_to_id: Dict[str, int]) -> Optional[int]:
    normalized = target.strip().lower()
    if normalized in {"0", "complete", "end-graph", "graph-complete"}:
        return 0
    parsed = coerce_int(target)
    if parsed is not None:
        return parsed
    return slug_to_id.get(target)


def validate_story_draft(draft: Dict[str, Any]) -> List[Diagnostic]:
    diagnostics: List[Diagnostic] = []
    source_path = str(draft.get("sourcePath", ""))

    graphs = get_list(draft, "graphs")
    beats = get_list(draft, "beats")
    steps = get_list(draft, "steps")
    branches = get_list(draft, "branches")
    choices = get_list(draft, "choices")
    facts = get_list(draft, "facts")

    report_duplicate_ids(graphs, "graphs", "Id", source_path, diagnostics)
    report_duplicate_ids(beats, "beats", "Id", source_path, diagnostics)
    report_duplicate_ids(steps, "steps", "Id", source_path, diagnostics)
    report_duplicate_ids(branches, "branches", "Id", source_path, diagnostics)
    report_duplicate_ids(choices, "choices", "Id", source_path, diagnostics)
    report_duplicate_fact_keys(facts, source_path, diagnostics)

    text_keys = collect_text_keys(draft)
    beats_by_graph: Dict[int, set[int]] = {}
    for beat in beats:
        graph_id = coerce_int(beat.get("GraphId"))
        beat_id = coerce_int(beat.get("Id"))
        if graph_id is None or beat_id is None:
            continue
        beats_by_graph.setdefault(graph_id, set()).add(beat_id)

    for graph_index, graph in enumerate(graphs):
        graph_id = coerce_int(graph.get("Id"))
        entry_beat_id = coerce_int(graph.get("EntryBeatId"))
        if graph_id is None:
            continue
        if entry_beat_id is None or entry_beat_id not in beats_by_graph.get(graph_id, set()):
            diagnostics.append(
                Diagnostic(
                    "MissingEntryBeat",
                    "Error",
                    f"Story graph {graph_id} entry beat {entry_beat_id} is missing.",
                    f"graphs[{graph_index}].EntryBeatId",
                    source_path,
                )
            )

    fact_kind_by_key = collect_fact_kinds(facts)

    for beat_index, beat in enumerate(beats):
        for trigger_index, trigger_id in enumerate(as_int_list(beat.get("TriggerIds"))):
            if trigger_id <= 0:
                diagnostics.append(
                    Diagnostic(
                        "InvalidTriggerId",
                        "Error",
                        "Story beat trigger id must be positive.",
                        f"beats[{beat_index}].TriggerIds[{trigger_index}]",
                        source_path,
                    )
                )

    for step_index, step in enumerate(steps):
        kind = normalize_enum(step.get("Kind"), STEP_KIND_BY_VALUE)
        wait_policy = normalize_enum(step.get("WaitPolicy", "NoWait"), WAIT_POLICY_BY_VALUE)
        if kind not in SUPPORTED_STEP_KINDS:
            diagnostics.append(
                Diagnostic(
                    "UnsupportedStepKind",
                    "Error",
                    f"Story step kind '{step.get('Kind')}' is not supported.",
                    f"steps[{step_index}].Kind",
                    source_path,
                )
            )
        if wait_policy not in SUPPORTED_WAIT_POLICIES:
            diagnostics.append(
                Diagnostic(
                    "UnsupportedWaitPolicy",
                    "Error",
                    f"Story wait policy '{step.get('WaitPolicy')}' is not supported.",
                    f"steps[{step_index}].WaitPolicy",
                    source_path,
                )
            )

        if kind in {"Line", "Presentation", "Wait"}:
            text_key = coerce_int(step.get("TextKey"))
            if text_key is None or text_key <= 0 or text_key not in text_keys:
                diagnostics.append(
                    Diagnostic(
                        "MissingTextKey",
                        "Error",
                        f"Story step references missing text key {text_key}.",
                        f"steps[{step_index}].TextKey",
                        source_path,
                    )
                )

        if kind == "SetFact":
            fact_namespace = coerce_int(step.get("FactNamespace"))
            fact_id = coerce_int(step.get("FactId"))
            value_kind = normalize_enum(step.get("FactValueKind"), VALUE_KIND_BY_VALUE)
            raw_value = coerce_int(step.get("FactValueRaw"))
            fact_key = (fact_namespace or 0, fact_id or 0)
            declared_kind = fact_kind_by_key.get(fact_key)
            if fact_namespace is None or fact_namespace < 0 or fact_id is None or fact_id <= 0 or declared_kind is None:
                diagnostics.append(
                    Diagnostic(
                        "InvalidFactReference",
                        "Error",
                        f"SetFact step references missing Story fact {fact_namespace}:{fact_id}.",
                        f"steps[{step_index}].FactId",
                        source_path,
                    )
                )
            elif declared_kind != value_kind:
                diagnostics.append(
                    Diagnostic(
                        "InvalidFactReference",
                        "Error",
                        f"SetFact value kind {value_kind} does not match declared fact kind {declared_kind}.",
                        f"steps[{step_index}].FactValueKind",
                        source_path,
                    )
                )
            validate_fact_raw_value(value_kind, raw_value, f"steps[{step_index}].FactValueRaw", source_path, diagnostics)

    for branch_index, branch in enumerate(branches):
        graph_id = coerce_int(branch.get("GraphId"))
        target_beat_id = coerce_int(branch.get("TargetBeatId"))
        if target_beat_id is None or target_beat_id < 0 or (
            target_beat_id != 0 and target_beat_id not in beats_by_graph.get(graph_id or 0, set())
        ):
            diagnostics.append(
                Diagnostic(
                    "InvalidBranchTarget",
                    "Error",
                    f"Story branch target beat {target_beat_id} is missing.",
                    f"branches[{branch_index}].TargetBeatId",
                    source_path,
                )
            )

    for choice_index, choice in enumerate(choices):
        graph_id = coerce_int(choice.get("GraphId"))
        target_beat_id = coerce_int(choice.get("TargetBeatId"))
        if target_beat_id is None or target_beat_id < 0 or (
            target_beat_id != 0 and target_beat_id not in beats_by_graph.get(graph_id or 0, set())
        ):
            diagnostics.append(
                Diagnostic(
                    "InvalidChoiceTarget",
                    "Error",
                    f"Story choice target beat {target_beat_id} is missing.",
                    f"choices[{choice_index}].TargetBeatId",
                    source_path,
                )
            )

        label_text_key = coerce_int(choice.get("LabelTextKey"))
        if label_text_key is None or label_text_key <= 0 or label_text_key not in text_keys:
            diagnostics.append(
                Diagnostic(
                    "MissingTextKey",
                    "Error",
                    f"Story choice references missing label text key {label_text_key}.",
                    f"choices[{choice_index}].LabelTextKey",
                    source_path,
                )
            )

        for effect_index, effect_id in enumerate(as_int_list(choice.get("EffectIds"))):
            if effect_id <= 0:
                diagnostics.append(
                    Diagnostic(
                        "InvalidEffectId",
                        "Error",
                        "Story choice effect id must be positive.",
                        f"choices[{choice_index}].EffectIds[{effect_index}]",
                        source_path,
                    )
                )

    return diagnostics


def get_list(draft: Dict[str, Any], key: str) -> List[Dict[str, Any]]:
    value = draft.get(key, [])
    if not isinstance(value, list):
        return []
    return [item for item in value if isinstance(item, dict)]


def report_duplicate_ids(
    rows: Sequence[Dict[str, Any]],
    table_name: str,
    id_field: str,
    source_path: str,
    diagnostics: List[Diagnostic],
) -> None:
    seen: Dict[int, int] = {}
    for index, row in enumerate(rows):
        row_id = coerce_int(row.get(id_field))
        if row_id is None:
            continue
        if row_id in seen:
            diagnostics.append(
                Diagnostic(
                    "DuplicateId",
                    "Error",
                    f"Duplicate Story config id {row_id} in {table_name}.",
                    f"{table_name}[{index}].{id_field}",
                    source_path,
                )
            )
        else:
            seen[row_id] = index


def report_duplicate_fact_keys(
    facts: Sequence[Dict[str, Any]],
    source_path: str,
    diagnostics: List[Diagnostic],
) -> None:
    seen: Dict[Tuple[int, int], int] = {}
    for index, fact in enumerate(facts):
        namespace = coerce_int(fact.get("Namespace"))
        fact_id = coerce_int(fact.get("Id"))
        if namespace is None or fact_id is None:
            continue
        key = (namespace, fact_id)
        if key in seen:
            diagnostics.append(
                Diagnostic(
                    "DuplicateId",
                    "Error",
                    f"Duplicate Story fact key {namespace}:{fact_id}.",
                    f"facts[{index}].Id",
                    source_path,
                )
            )
        else:
            seen[key] = index


def collect_text_keys(draft: Dict[str, Any]) -> set[int]:
    text_keys: set[int] = set()
    for value in draft.get("textKeys", []):
        parsed = coerce_int(value)
        if parsed is not None:
            text_keys.add(parsed)
    return text_keys


def collect_fact_kinds(facts: Sequence[Dict[str, Any]]) -> Dict[Tuple[int, int], str]:
    result: Dict[Tuple[int, int], str] = {}
    for fact in facts:
        namespace = coerce_int(fact.get("Namespace"))
        fact_id = coerce_int(fact.get("Id"))
        value_kind = normalize_enum(fact.get("ValueKind"), VALUE_KIND_BY_VALUE)
        if namespace is None or fact_id is None:
            continue
        result[(namespace, fact_id)] = value_kind
    return result


def validate_fact_raw_value(
    value_kind: str,
    raw_value: Optional[int],
    path: str,
    source_path: str,
    diagnostics: List[Diagnostic],
) -> None:
    if value_kind not in SUPPORTED_VALUE_KINDS:
        diagnostics.append(
            Diagnostic(
                "InvalidFactValue",
                "Error",
                f"Story fact value kind '{value_kind}' is not supported.",
                path,
                source_path,
            )
        )
        return
    if raw_value is None:
        diagnostics.append(
            Diagnostic(
                "InvalidFactValue",
                "Error",
                "Story fact raw value must be an integer.",
                path,
                source_path,
            )
        )
        return
    if value_kind == "Bool" and raw_value not in {0, 1}:
        diagnostics.append(
            Diagnostic(
                "InvalidFactValue",
                "Error",
                "Bool Story fact raw value must be 0 or 1.",
                path,
                source_path,
            )
        )
    elif value_kind == "Int32" and not -(2**31) <= raw_value <= 2**31 - 1:
        diagnostics.append(
            Diagnostic(
                "InvalidFactValue",
                "Error",
                "Int32 Story fact raw value is outside 32-bit signed integer range.",
                path,
                source_path,
            )
        )
    elif value_kind == "StringRef" and raw_value <= 0:
        diagnostics.append(
            Diagnostic(
                "InvalidFactValue",
                "Error",
                "StringRef Story fact raw value must be a positive text key id.",
                path,
                source_path,
            )
        )


def normalize_enum(value: Any, value_map: Dict[int, str]) -> str:
    parsed = coerce_int(value)
    if parsed is not None:
        return value_map.get(parsed, str(parsed))
    if value is None:
        return ""
    return str(value).strip()


def coerce_int(value: Any) -> Optional[int]:
    if isinstance(value, bool):
        return None
    if isinstance(value, int):
        return value
    if isinstance(value, str):
        stripped = value.strip()
        if not stripped:
            return None
        try:
            return int(stripped, 10)
        except ValueError:
            return None
    return None


def as_int_list(value: Any) -> List[int]:
    if not isinstance(value, list):
        return []
    result: List[int] = []
    for item in value:
        parsed = coerce_int(item)
        if parsed is not None:
            result.append(parsed)
    return result


def write_story_draft(path: pathlib.Path, draft: Dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(draft, indent=2, ensure_ascii=True) + "\n", encoding="utf-8")


def load_story_draft(path: pathlib.Path) -> Dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8"))


def print_diagnostics(diagnostics: Sequence[Diagnostic], stream: Any) -> None:
    print(json.dumps([diagnostic.to_json() for diagnostic in diagnostics], indent=2, ensure_ascii=True), file=stream)


def command_import_markdown(args: argparse.Namespace) -> int:
    markdown_path = pathlib.Path(args.markdown)
    output_path = pathlib.Path(args.out)
    draft, diagnostics = import_markdown_file(markdown_path)
    if diagnostics:
        print_diagnostics(diagnostics, sys.stderr)
        return 1

    assert draft is not None
    write_story_draft(output_path, draft)
    print(
        "Imported {0}: {1} graph(s), {2} beat(s), {3} step(s), {4} choice(s), {5} fact(s) -> {6}".format(
            draft["sourcePath"],
            len(draft["graphs"]),
            len(draft["beats"]),
            len(draft["steps"]),
            len(draft["choices"]),
            len(draft["facts"]),
            output_path.as_posix(),
        )
    )
    return 0


def command_validate(args: argparse.Namespace) -> int:
    draft_path = pathlib.Path(args.story_json)
    try:
        draft = load_story_draft(draft_path)
    except (OSError, json.JSONDecodeError) as exc:
        print_diagnostics(
            [
                Diagnostic(
                    "InputReadFailed",
                    "Error",
                    f"Could not load Story config draft: {exc}.",
                    draft_path.as_posix(),
                )
            ],
            sys.stderr,
        )
        return 1

    diagnostics = validate_story_draft(draft)
    if diagnostics:
        print_diagnostics(diagnostics, sys.stderr)
        return 1

    print(
        "Validation passed: {0} graph(s), {1} beat(s), {2} step(s), {3} choice(s), {4} fact(s).".format(
            len(get_list(draft, "graphs")),
            len(get_list(draft, "beats")),
            len(get_list(draft, "steps")),
            len(get_list(draft, "choices")),
            len(get_list(draft, "facts")),
        )
    )
    return 0


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="MxFramework Story authoring tools.")
    subparsers = parser.add_subparsers(dest="command", required=True)

    import_parser = subparsers.add_parser(
        "import-markdown",
        help="Import Markdown Story Outline v1 into deterministic .story.json draft output.",
    )
    import_parser.add_argument("markdown", help="Markdown Story Outline v1 input path.")
    import_parser.add_argument("--out", required=True, help="Output .story.json draft path.")
    import_parser.set_defaults(func=command_import_markdown)

    validate_parser = subparsers.add_parser(
        "validate",
        help="Validate a generated .story.json draft against the external Story.Config handoff contract.",
    )
    validate_parser.add_argument("story_json", help=".story.json draft path.")
    validate_parser.set_defaults(func=command_validate)

    return parser


def main(argv: Optional[Sequence[str]] = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    return int(args.func(args))


if __name__ == "__main__":
    sys.exit(main())
