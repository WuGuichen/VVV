#!/usr/bin/env python3
"""Compare Profile Analyzer-like marker timing CSV exports."""

from __future__ import annotations

import argparse
import csv
import fnmatch
from dataclasses import dataclass
from pathlib import Path


DEFAULT_MARKERS = (
    "*CharacterTest.GameManager.Simulation",
    "*CharacterTest.GameSlice.HostTick",
    "*CharacterTest.RuntimeModule.CharacterTest.CharacterRuntime",
    "*CharacterTest.CharacterRuntime.Motion",
    "*CombatKinematicMotor.Step()",
    "*CombatPhysicsBroadphase.CollectCandidatesAabb()",
    "*CombatPhysicsWorld.QueryCapsule()",
    "*CharacterTest.CharacterRuntime.InteractionConsumers",
    "*CharacterTestInteractionPhysicsSystem.QueryField()",
    "*CharacterTest.InteractionPhysics.TornadoIndexRebuild",
    "*CharacterTest.InteractionPhysics.TornadoFieldQuery",
    "*CharacterTest.InteractionPhysics.FallbackFieldQuery",
)


@dataclass(frozen=True)
class MarkerTiming:
    name: str
    median_time_ms: float
    mean_time_ms: float
    max_time_ms: float
    total_time_ms: float
    count_total: int
    frame_count: int
    gc_total_bytes: int
    gc_count: int
    gc_median_bytes: int
    gc_max_bytes: int


@dataclass(frozen=True)
class ComparisonRow:
    marker: str
    before: MarkerTiming | None
    after: MarkerTiming | None


def main() -> int:
    parser = argparse.ArgumentParser(
        description=(
            "Compare two Profile Analyzer-like marker timing CSV exports. "
            "The input is the semicolon-delimited table written by "
            "profile_analyzer_gc_summary.py --marker-timing-out."
        )
    )
    parser.add_argument("--before", type=Path, required=True, help="Before marker timing CSV.")
    parser.add_argument("--after", type=Path, required=True, help="After marker timing CSV.")
    parser.add_argument("--before-label", default="before", help="Label for before columns.")
    parser.add_argument("--after-label", default="after", help="Label for after columns.")
    parser.add_argument(
        "--marker",
        action="append",
        default=[],
        help=(
            "Marker name or glob to compare. Repeat for multiple markers. "
            "Defaults to the CharacterTest performance marker set."
        ),
    )
    parser.add_argument("--out-csv", type=Path, help="Write comparison CSV.")
    parser.add_argument("--out-md", type=Path, help="Write comparison Markdown.")
    parser.add_argument(
        "--note",
        default="",
        help="Optional one-line note to include in Markdown output.",
    )
    args = parser.parse_args()

    markers = args.marker or list(DEFAULT_MARKERS)
    before_rows = load_marker_timing(args.before)
    after_rows = load_marker_timing(args.after)
    comparisons = [
        ComparisonRow(
            marker=marker,
            before=find_marker(before_rows, marker),
            after=find_marker(after_rows, marker),
        )
        for marker in markers
    ]
    validity_notes = build_validity_notes(comparisons)

    if args.out_csv:
        write_csv(args.out_csv, comparisons, args.before_label, args.after_label)
        print(f"wrote CSV comparison: {args.out_csv}")
    if args.out_md:
        write_markdown(
            args.out_md,
            comparisons,
            args.before_label,
            args.after_label,
            args.note,
            validity_notes,
        )
        print(f"wrote Markdown comparison: {args.out_md}")

    print_summary(comparisons, args.before_label, args.after_label, validity_notes)
    return 0


def load_marker_timing(path: Path) -> list[MarkerTiming]:
    rows: list[MarkerTiming] = []
    with path.open(newline="", encoding="utf-8-sig") as handle:
        reader = csv.DictReader(handle, delimiter=";")
        for row in reader:
            name = (row.get("Name") or "").strip()
            if not name:
                continue
            rows.append(
                MarkerTiming(
                    name=name,
                    median_time_ms=parse_float(row, "Median Time"),
                    mean_time_ms=parse_float(row, "Mean Time"),
                    max_time_ms=parse_float(row, "Max Time"),
                    total_time_ms=parse_float(row, "Total Time"),
                    count_total=parse_int(row, "Count Total"),
                    frame_count=parse_int(row, "Number of frames containing Marker"),
                    gc_total_bytes=parse_int(row, "GC Alloc Total (bytes)"),
                    gc_count=parse_int(row, "GC Alloc Count"),
                    gc_median_bytes=parse_int(row, "GC Alloc Median (bytes)"),
                    gc_max_bytes=parse_int(row, "GC Alloc Max (bytes)"),
                )
            )
    return rows


def find_marker(rows: list[MarkerTiming], pattern: str) -> MarkerTiming | None:
    matches = [
        row
        for row in rows
        if row.name == pattern or fnmatch.fnmatch(row.name, pattern)
    ]
    if not matches and not has_glob(pattern):
        matches = [row for row in rows if pattern in row.name]
    if not matches:
        return None
    return max(
        matches,
        key=lambda row: (
            row.frame_count,
            row.count_total,
            row.median_time_ms,
            row.total_time_ms,
        ),
    )


def has_glob(pattern: str) -> bool:
    return any(char in pattern for char in "*?[]")


def write_csv(
    path: Path,
    rows: list[ComparisonRow],
    before_label: str,
    after_label: str,
) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.writer(handle)
        writer.writerow(
            [
                "marker",
                f"{before_label}_name",
                f"{after_label}_name",
                f"{before_label}_median_ms",
                f"{after_label}_median_ms",
                "median_delta_ms",
                "median_delta_percent",
                f"{before_label}_mean_ms",
                f"{after_label}_mean_ms",
                "mean_delta_ms",
                "mean_delta_percent",
                f"{before_label}_max_ms",
                f"{after_label}_max_ms",
                f"{before_label}_gc_total_bytes",
                f"{after_label}_gc_total_bytes",
                "gc_total_delta_bytes",
                f"{before_label}_gc_median_bytes",
                f"{after_label}_gc_median_bytes",
                f"{before_label}_count_total",
                f"{after_label}_count_total",
                f"{before_label}_frame_count",
                f"{after_label}_frame_count",
            ]
        )
        for row in rows:
            before = row.before
            after = row.after
            writer.writerow(
                [
                    row.marker,
                    before.name if before else "",
                    after.name if after else "",
                    value_or_empty(before, "median_time_ms"),
                    value_or_empty(after, "median_time_ms"),
                    delta_or_empty(before, after, "median_time_ms"),
                    percent_delta_or_empty(before, after, "median_time_ms"),
                    value_or_empty(before, "mean_time_ms"),
                    value_or_empty(after, "mean_time_ms"),
                    delta_or_empty(before, after, "mean_time_ms"),
                    percent_delta_or_empty(before, after, "mean_time_ms"),
                    value_or_empty(before, "max_time_ms"),
                    value_or_empty(after, "max_time_ms"),
                    value_or_empty(before, "gc_total_bytes"),
                    value_or_empty(after, "gc_total_bytes"),
                    delta_or_empty(before, after, "gc_total_bytes"),
                    value_or_empty(before, "gc_median_bytes"),
                    value_or_empty(after, "gc_median_bytes"),
                    value_or_empty(before, "count_total"),
                    value_or_empty(after, "count_total"),
                    value_or_empty(before, "frame_count"),
                    value_or_empty(after, "frame_count"),
                ]
            )


def write_markdown(
    path: Path,
    rows: list[ComparisonRow],
    before_label: str,
    after_label: str,
    note: str,
    validity_notes: list[str],
) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    lines = [
        "# Profile Analyzer Marker Comparison",
        "",
        f"- Before: `{before_label}`",
        f"- After: `{after_label}`",
    ]
    if note:
        lines.append(f"- Note: {note}")
    if validity_notes:
        lines.extend(
            [
                "- Validity: report-only; one or more compared markers are not present on both sides.",
                "",
                "## Validity Notes",
                "",
            ]
        )
        lines.extend(f"- {note}" for note in validity_notes)
    lines.extend(
        [
            "",
            "| Marker | Before Median | After Median | Median Delta | Before Mean | After Mean | Mean Delta | Before Max | After Max | Before GC Total | After GC Total |",
            "| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |",
        ]
    )
    for row in rows:
        before = row.before
        after = row.after
        lines.append(
            "| "
            + " | ".join(
                [
                    markdown_marker_name(row),
                    format_ms(value_or_none(before, "median_time_ms")),
                    format_ms(value_or_none(after, "median_time_ms")),
                    format_delta_ms(delta_or_none(before, after, "median_time_ms")),
                    format_ms(value_or_none(before, "mean_time_ms")),
                    format_ms(value_or_none(after, "mean_time_ms")),
                    format_delta_ms(delta_or_none(before, after, "mean_time_ms")),
                    format_ms(value_or_none(before, "max_time_ms")),
                    format_ms(value_or_none(after, "max_time_ms")),
                    format_bytes(value_or_none(before, "gc_total_bytes")),
                    format_bytes(value_or_none(after, "gc_total_bytes")),
                ]
            )
            + " |"
        )
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def print_summary(
    rows: list[ComparisonRow],
    before_label: str,
    after_label: str,
    validity_notes: list[str],
) -> None:
    print(f"Marker comparison: {before_label} -> {after_label}")
    if validity_notes:
        print("Validity notes:")
        for note in validity_notes:
            print(f"  - {note}")
    for row in rows:
        before = row.before
        after = row.after
        if before is None and after is None:
            print(f"  - {row.marker}: missing in before and after")
            continue
        if before is None:
            print(f"  - {row.marker}: missing in before")
            continue
        if after is None:
            print(f"  - {row.marker}: missing in after")
            continue
        print(
            "  - "
            f"{row.marker}: median {round(before.median_time_ms, 4)} -> "
            f"{round(after.median_time_ms, 4)} ms "
            f"({format_delta_ms(delta_or_none(before, after, 'median_time_ms'))}), "
            f"mean {round(before.mean_time_ms, 4)} -> "
            f"{round(after.mean_time_ms, 4)} ms "
            f"({format_delta_ms(delta_or_none(before, after, 'mean_time_ms'))}), "
            f"GC total {before.gc_total_bytes} -> {after.gc_total_bytes} B"
        )


def build_validity_notes(rows: list[ComparisonRow]) -> list[str]:
    notes: list[str] = []
    for row in rows:
        before = row.before
        after = row.after
        if before is None and after is not None:
            notes.append(
                f"Before is missing marker {after.name}; this marker has no formal delta."
            )
        elif before is not None and after is None:
            notes.append(
                f"After is missing marker {before.name}; this marker has no formal delta."
            )

    return notes


def markdown_marker_name(row: ComparisonRow) -> str:
    if row.after:
        return code(row.after.name)
    if row.before:
        return code(row.before.name)
    return code(row.marker)


def value_or_empty(row: MarkerTiming | None, name: str) -> float | int | str:
    if row is None:
        return ""
    return getattr(row, name)


def value_or_none(row: MarkerTiming | None, name: str) -> float | int | None:
    if row is None:
        return None
    value = getattr(row, name)
    if isinstance(value, int | float):
        return value
    return None


def delta_or_empty(
    before: MarkerTiming | None,
    after: MarkerTiming | None,
    name: str,
) -> float | int | str:
    delta = delta_or_none(before, after, name)
    if delta is None:
        return ""
    return delta


def delta_or_none(
    before: MarkerTiming | None,
    after: MarkerTiming | None,
    name: str,
) -> float | int | None:
    if before is None or after is None:
        return None
    before_value = getattr(before, name)
    after_value = getattr(after, name)
    if isinstance(before_value, int) and isinstance(after_value, int):
        return after_value - before_value
    return float(after_value) - float(before_value)


def percent_delta_or_empty(
    before: MarkerTiming | None,
    after: MarkerTiming | None,
    name: str,
) -> float | str:
    if before is None or after is None:
        return ""
    before_value = float(getattr(before, name))
    if before_value == 0:
        return ""
    after_value = float(getattr(after, name))
    return ((after_value - before_value) / before_value) * 100.0


def parse_float(row: dict[str, str], name: str) -> float:
    value = (row.get(name) or "").strip()
    return float(value) if value else 0.0


def parse_int(row: dict[str, str], name: str) -> int:
    value = (row.get(name) or "").strip()
    return round(float(value)) if value else 0


def format_ms(value: float | int | None) -> str:
    if value is None:
        return ""
    return f"{float(value):.4f} ms"


def format_delta_ms(value: float | int | None) -> str:
    if value is None:
        return ""
    return f"{float(value):+.4f} ms"


def format_bytes(value: float | int | None) -> str:
    if value is None:
        return ""
    return f"{round(float(value))} B"


def code(value: str) -> str:
    escaped = value.replace("|", "\\|")
    return f"`{escaped}`"


if __name__ == "__main__":
    raise SystemExit(main())
