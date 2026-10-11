#!/usr/bin/env python3
"""Summarize Unity Profile Analyzer frame/marker CSV exports."""

from __future__ import annotations

import argparse
import csv
import statistics
import struct
from dataclasses import dataclass
from collections import defaultdict
from typing import BinaryIO
from pathlib import Path


EDITOR_NOISE_MARKERS = (
    "EditorLoop",
    "GUI.Label",
    "GUI.Repaint",
    "RenderPlayModeViewCameras",
    "DrawScreenSpaceUI",
    "FMODUnity.RuntimeManager.DrawDebugOverlay",
)


@dataclass(frozen=True)
class FrameRow:
    frame_offset: int
    frame_index: int
    frame_time_ms: float
    gc_alloc_bytes: int


@dataclass(frozen=True)
class MarkerRow:
    name: str
    gc_total_bytes: int
    gc_count: int
    gc_mean_bytes: int
    gc_median_bytes: int
    gc_max_bytes: int
    max_frame_index: int


@dataclass(frozen=True)
class MarkerTimingRow:
    name: str
    median_time_ms: float
    min_time_ms: float
    max_time_ms: float
    median_frame_index: int
    min_frame_index: int
    max_frame_index: int
    min_depth: int
    max_depth: int
    total_time_ms: float
    mean_time_ms: float
    lower_quartile_time_ms: float
    upper_quartile_time_ms: float
    count_total: int
    count_median: int
    count_min: int
    count_max: int
    frame_count: int
    first_frame_index: int
    min_individual_time_ms: float
    max_individual_time_ms: float
    min_individual_frame_index: int
    max_individual_frame_index: int
    time_at_median_frame_ms: float
    gc_total_bytes: int
    gc_count: int
    gc_mean_bytes: int
    gc_median_bytes: int
    gc_min_bytes: int
    gc_max_bytes: int
    gc_lower_quartile_bytes: int
    gc_upper_quartile_bytes: int
    gc_median_frame_index: int
    gc_min_frame_index: int
    gc_max_frame_index: int


@dataclass(frozen=True)
class PDataMetadata:
    version: int
    frame_index_offset: int
    frame_count: int
    marker_names: list[str]
    thread_names: list[str]


def main() -> int:
    parser = argparse.ArgumentParser(
        description=(
            "Summarize Profile Analyzer frameTime.csv, markerTable.csv, "
            "and .pdata GC/timing data."
        )
    )
    parser.add_argument("--frame-time", type=Path, help="Profile Analyzer frameTime.csv path.")
    parser.add_argument("--marker-table", type=Path, help="Profile Analyzer markerTable.csv path.")
    parser.add_argument(
        "--pdata",
        type=Path,
        help="Unity Profile Analyzer .pdata path. Parsed directly for repeatable exports.",
    )
    parser.add_argument(
        "--pdata-thread",
        default="1:Main Thread",
        help="Thread name to aggregate marker data from when reading .pdata. Use 'all' for all threads.",
    )
    parser.add_argument(
        "--frame-time-out",
        type=Path,
        help="Write a frameTime.csv-compatible export when --pdata is used.",
    )
    parser.add_argument(
        "--marker-table-out",
        type=Path,
        help="Write a markerTable.csv-compatible export when --pdata is used.",
    )
    parser.add_argument(
        "--marker-timing-out",
        type=Path,
        help="Write a full Profile Analyzer marker timing table export when --pdata is used.",
    )
    parser.add_argument(
        "--frame-start",
        type=int,
        help="Inclusive frame index filter. Also filters .pdata marker aggregation.",
    )
    parser.add_argument(
        "--frame-end",
        type=int,
        help="Inclusive frame index filter. Also filters .pdata marker aggregation.",
    )
    parser.add_argument(
        "--drop-last-frame",
        action="store_true",
        help="Drop the last frame from frame-time summary, useful for capture-stop artifacts.",
    )
    parser.add_argument(
        "--exclude-frame",
        type=int,
        action="append",
        default=[],
        help="Exclude a display frame index from summaries and .pdata CSV exports. Can be repeated.",
    )
    parser.add_argument(
        "--top-markers",
        type=int,
        default=20,
        help="Number of GC-heavy markers to print. Defaults to 20.",
    )
    parser.add_argument(
        "--top-time-markers",
        type=int,
        default=20,
        help="Number of time-heavy markers to print for .pdata timing exports. Defaults to 20.",
    )
    parser.add_argument(
        "--exclude-editor-noise",
        action="store_true",
        help="Hide known Editor/debug-noise markers from marker ranking.",
    )
    args = parser.parse_args()
    exclude_frames = set(args.exclude_frame or [])

    if not args.frame_time and not args.marker_table and not args.pdata:
        parser.error("at least one of --frame-time, --marker-table, or --pdata is required")

    if (args.frame_time_out or args.marker_table_out or args.marker_timing_out) and not args.pdata:
        parser.error("--frame-time-out, --marker-table-out, and --marker-timing-out require --pdata")

    if args.pdata:
        metadata = load_pdata_metadata(args.pdata)
        frame_rows, marker_rows, marker_timing_rows = load_pdata_rows(
            args.pdata,
            metadata=metadata,
            marker_thread=args.pdata_thread,
            frame_start=args.frame_start,
            frame_end=args.frame_end,
            drop_last_frame=args.drop_last_frame,
            exclude_frames=exclude_frames,
        )
        print("PData summary")
        print(f"  file: {args.pdata}")
        print(f"  version: {metadata.version}")
        print(f"  frames: {metadata.frame_count}")
        print(f"  markers: {len(metadata.marker_names)}")
        print(f"  threads: {len(metadata.thread_names)}")
        print(f"  marker thread filter: {args.pdata_thread}")
        if args.frame_time_out:
            write_frame_rows(args.frame_time_out, frame_rows)
            print(f"  wrote frameTime export: {args.frame_time_out}")
        if args.marker_table_out:
            write_marker_rows(args.marker_table_out, marker_rows)
            print(f"  wrote markerTable export: {args.marker_table_out}")
        if args.marker_timing_out:
            write_marker_timing_rows(args.marker_timing_out, marker_timing_rows)
            print(f"  wrote marker timing export: {args.marker_timing_out}")
        summarize_frames(
            frame_rows,
            frame_start=args.frame_start,
            frame_end=args.frame_end,
            drop_last_frame=args.drop_last_frame,
            exclude_frames=exclude_frames,
        )
        summarize_markers(
            marker_rows,
            top_markers=args.top_markers,
            exclude_editor_noise=args.exclude_editor_noise,
        )
        summarize_marker_timing(
            marker_timing_rows,
            top_markers=args.top_time_markers,
            exclude_editor_noise=args.exclude_editor_noise,
        )

    if args.frame_time:
        summarize_frames(
            load_frame_rows(args.frame_time),
            frame_start=args.frame_start,
            frame_end=args.frame_end,
            drop_last_frame=args.drop_last_frame,
            exclude_frames=exclude_frames,
        )

    if args.marker_table:
        summarize_markers(
            load_marker_rows(args.marker_table),
            top_markers=args.top_markers,
            exclude_editor_noise=args.exclude_editor_noise,
        )

    return 0


def summarize_frames(
    rows: list[FrameRow],
    *,
    frame_start: int | None,
    frame_end: int | None,
    drop_last_frame: bool,
    exclude_frames: set[int],
) -> None:
    if frame_start is not None:
        rows = [row for row in rows if row.frame_index >= frame_start]
    if frame_end is not None:
        rows = [row for row in rows if row.frame_index <= frame_end]
    if exclude_frames:
        rows = [row for row in rows if row.frame_index not in exclude_frames]
    if drop_last_frame and rows:
        rows = rows[:-1]

    print("Frame GC summary")
    if not rows:
        print("  no frame rows after filters")
        return

    gc_values = [row.gc_alloc_bytes for row in rows]
    nonzero = [value for value in gc_values if value > 0]
    max_row = max(rows, key=lambda row: row.gc_alloc_bytes)
    print(f"  frames: {len(rows)}")
    print(f"  frame range: {rows[0].frame_index}-{rows[-1].frame_index}")
    if exclude_frames:
        excluded = ", ".join(str(frame) for frame in sorted(exclude_frames))
        print(f"  excluded frames: {excluded}")
    print(f"  total GC: {sum(gc_values)} bytes")
    print(f"  mean GC/frame: {round(statistics.fmean(gc_values), 2)} bytes")
    print(f"  median GC/frame: {round(statistics.median(gc_values), 2)} bytes")
    print(f"  nonzero frames: {len(nonzero)}")
    print(
        "  max GC frame: "
        f"frame {max_row.frame_index}, {max_row.gc_alloc_bytes} bytes, "
        f"{round(max_row.frame_time_ms, 4)} ms"
    )


def summarize_markers(
    rows: list[MarkerRow],
    *,
    top_markers: int,
    exclude_editor_noise: bool,
) -> None:
    if exclude_editor_noise:
        rows = [
            row
            for row in rows
            if not any(marker in row.name for marker in EDITOR_NOISE_MARKERS)
        ]
    rows = [row for row in rows if row.gc_total_bytes > 0 or row.gc_count > 0]
    rows.sort(key=lambda row: (row.gc_total_bytes, row.gc_max_bytes, row.gc_count), reverse=True)

    print("Marker GC summary")
    if not rows:
        print("  no marker rows with GC data after filters")
        return

    print(f"  markers with GC: {len(rows)}")
    print("  top markers by GC total:")
    for row in rows[: max(0, top_markers)]:
        print(
            "  - "
            f"{row.gc_total_bytes} bytes, count {row.gc_count}, "
            f"max {row.gc_max_bytes} bytes @ frame {row.max_frame_index}: "
            f"{row.name}"
        )


def summarize_marker_timing(
    rows: list[MarkerTimingRow],
    *,
    top_markers: int,
    exclude_editor_noise: bool,
) -> None:
    if exclude_editor_noise:
        rows = [
            row
            for row in rows
            if not any(marker in row.name for marker in EDITOR_NOISE_MARKERS)
        ]
    rows = [row for row in rows if row.total_time_ms > 0 or row.count_total > 0]
    rows.sort(
        key=lambda row: (
            row.median_time_ms,
            row.max_time_ms,
            row.total_time_ms,
            row.count_total,
        ),
        reverse=True,
    )

    print("Marker timing summary")
    if not rows:
        print("  no marker timing rows after filters")
        return

    print(f"  markers with timing: {len(rows)}")
    print("  top markers by median time:")
    for row in rows[: max(0, top_markers)]:
        print(
            "  - "
            f"median {round(row.median_time_ms, 4)} ms, "
            f"max {round(row.max_time_ms, 4)} ms @ frame {row.max_frame_index}, "
            f"count {row.count_total}: {row.name}"
        )


def load_frame_rows(path: Path) -> list[FrameRow]:
    rows: list[FrameRow] = []
    for row in read_csv_rows(path):
        rows.append(
            FrameRow(
                frame_offset=parse_int(row.get("Frame Offset")),
                frame_index=parse_int(row.get("Frame Index")),
                frame_time_ms=parse_float(row.get("Frame Time (ms)")),
                gc_alloc_bytes=parse_int(row.get("GC Alloc (bytes)")),
            )
        )
    return rows


def load_marker_rows(path: Path) -> list[MarkerRow]:
    rows: list[MarkerRow] = []
    for row in read_csv_rows(path):
        rows.append(
            MarkerRow(
                name=(row.get("Name") or "").strip().strip('"'),
                gc_total_bytes=parse_int(row.get("GC Alloc Total (bytes)")),
                gc_count=parse_int(row.get("GC Alloc Count")),
                gc_mean_bytes=parse_int(row.get("GC Alloc Mean (bytes)")),
                gc_median_bytes=parse_int(row.get("GC Alloc Median (bytes)")),
                gc_max_bytes=parse_int(row.get("GC Alloc Max (bytes)")),
                max_frame_index=parse_int(row.get("GC Alloc Max Frame Index")),
            )
        )
    return rows


def load_pdata_metadata(path: Path) -> PDataMetadata:
    with path.open("rb") as handle:
        version = read_int32(handle)
        frame_index_offset = read_int32(handle)
        frame_count = read_int32(handle)
        marker_record_size = get_marker_record_size(version)
        for _ in range(frame_count):
            skip_profile_frame(handle, version, marker_record_size)

        marker_count = read_int32(handle)
        marker_names = [read_dotnet_string(handle) for _ in range(marker_count)]

        thread_count = read_int32(handle)
        thread_names = [read_dotnet_string(handle) for _ in range(thread_count)]

    return PDataMetadata(
        version=version,
        frame_index_offset=frame_index_offset,
        frame_count=frame_count,
        marker_names=marker_names,
        thread_names=thread_names,
    )


def load_pdata_rows(
    path: Path,
    *,
    metadata: PDataMetadata,
    marker_thread: str,
    frame_start: int | None,
    frame_end: int | None,
    drop_last_frame: bool,
    exclude_frames: set[int],
) -> tuple[list[FrameRow], list[MarkerRow], list[MarkerTimingRow]]:
    selected_thread_indices = get_selected_thread_indices(metadata.thread_names, marker_thread)
    selected_marker_frames = get_selected_frame_indices(
        metadata,
        frame_start=frame_start,
        frame_end=frame_end,
        drop_last_frame=drop_last_frame,
        exclude_frames=exclude_frames,
    )
    aggregate: dict[int, dict[str, object]] = defaultdict(
        lambda: {"total": 0, "count": 0, "frames": []}
    )
    timing_aggregate: dict[int, dict[str, object]] = defaultdict(create_marker_timing_aggregate)
    frame_rows: list[FrameRow] = []

    with path.open("rb") as handle:
        version = read_int32(handle)
        if version != metadata.version:
            raise ValueError(f".pdata version changed while reading: {version} != {metadata.version}")
        frame_index_offset = read_int32(handle)
        if frame_index_offset != metadata.frame_index_offset:
            raise ValueError(
                ".pdata frame index offset changed while reading: "
                f"{frame_index_offset} != {metadata.frame_index_offset}"
            )
        frame_count = read_int32(handle)
        if frame_count != metadata.frame_count:
            raise ValueError(
                f".pdata frame count changed while reading: {frame_count} != {metadata.frame_count}"
            )

        marker_record_size = get_marker_record_size(version)
        for frame_offset in range(frame_count):
            frame_index = frame_offset + 1 + frame_index_offset
            if frame_index not in selected_marker_frames:
                skip_profile_frame(handle, version, marker_record_size)
                continue

            ms_frame, frame_gc_bytes = read_profile_frame(
                handle,
                version,
                selected_thread_indices=selected_thread_indices,
                frame_index=frame_index,
                aggregate=aggregate if frame_index in selected_marker_frames else None,
                timing_aggregate=timing_aggregate if frame_index in selected_marker_frames else None,
            )
            if frame_index in selected_marker_frames:
                frame_rows.append(
                    FrameRow(
                        frame_offset=frame_offset,
                        frame_index=frame_index,
                        frame_time_ms=ms_frame,
                        gc_alloc_bytes=frame_gc_bytes,
                    )
                )

    marker_rows = build_marker_rows(metadata.marker_names, aggregate)
    marker_timing_rows = build_marker_timing_rows(metadata.marker_names, timing_aggregate)
    return frame_rows, marker_rows, marker_timing_rows


def get_selected_frame_indices(
    metadata: PDataMetadata,
    *,
    frame_start: int | None,
    frame_end: int | None,
    drop_last_frame: bool,
    exclude_frames: set[int],
) -> set[int]:
    frame_indices = [
        frame_offset + 1 + metadata.frame_index_offset
        for frame_offset in range(metadata.frame_count)
    ]
    if frame_start is not None:
        frame_indices = [frame_index for frame_index in frame_indices if frame_index >= frame_start]
    if frame_end is not None:
        frame_indices = [frame_index for frame_index in frame_indices if frame_index <= frame_end]
    if exclude_frames:
        frame_indices = [frame_index for frame_index in frame_indices if frame_index not in exclude_frames]
    if drop_last_frame and frame_indices:
        frame_indices = frame_indices[:-1]
    return set(frame_indices)


def get_selected_thread_indices(thread_names: list[str], marker_thread: str) -> set[int]:
    if marker_thread.lower() == "all":
        return set(range(len(thread_names)))

    selected = {index for index, name in enumerate(thread_names) if name == marker_thread}
    if not selected:
        known = ", ".join(thread_names[:10])
        raise ValueError(
            f"thread '{marker_thread}' not found in .pdata. First threads: {known}"
        )
    return selected


def build_marker_rows(
    marker_names: list[str],
    aggregate: dict[int, dict[str, object]],
) -> list[MarkerRow]:
    rows: list[MarkerRow] = []
    for marker_index, data in aggregate.items():
        frames = data["frames"]
        if not isinstance(frames, list) or not frames:
            continue

        frame_bytes = [item[1] for item in frames]
        max_frame_index, max_bytes = max(frames, key=lambda item: item[1])
        total = int(data["total"])
        count = int(data["count"])
        rows.append(
            MarkerRow(
                name=marker_names[marker_index] if marker_index < len(marker_names) else "",
                gc_total_bytes=total,
                gc_count=count,
                gc_mean_bytes=round(total / len(frame_bytes)),
                gc_median_bytes=round(statistics.median(frame_bytes)),
                gc_max_bytes=max_bytes,
                max_frame_index=max_frame_index,
            )
        )
    return rows


def create_marker_timing_aggregate() -> dict[str, object]:
    return {
        "frames": {},
        "min_depth": None,
        "max_depth": None,
        "min_individual_time": None,
        "min_individual_frame": 0,
        "max_individual_time": None,
        "max_individual_frame": 0,
    }


def build_marker_timing_rows(
    marker_names: list[str],
    aggregate: dict[int, dict[str, object]],
) -> list[MarkerTimingRow]:
    rows: list[MarkerTimingRow] = []
    for marker_index, data in aggregate.items():
        frames = data["frames"]
        if not isinstance(frames, dict) or not frames:
            continue

        frame_items = sorted(
            (frame_index, value)
            for frame_index, value in frames.items()
            if isinstance(value, list) and len(value) == 4
        )
        if not frame_items:
            continue

        time_values = [float(value[0]) for _, value in frame_items]
        count_values = [int(value[1]) for _, value in frame_items]
        gc_values = [int(value[2]) for _, value in frame_items]
        total_time = sum(time_values)
        median_time = statistics.median(time_values)
        median_frame, time_at_median_frame = choose_nearest_frame_value(frame_items, 0, median_time)
        min_frame, min_time = choose_min_frame_value(frame_items, 0)
        max_frame, max_time = choose_max_frame_value(frame_items, 0)
        gc_median = statistics.median(gc_values)
        gc_median_frame, _ = choose_nearest_frame_value(frame_items, 2, gc_median)
        gc_min_frame, gc_min = choose_min_frame_value(frame_items, 2)
        gc_max_frame, gc_max = choose_max_frame_value(frame_items, 2)

        rows.append(
            MarkerTimingRow(
                name=marker_names[marker_index] if marker_index < len(marker_names) else "",
                median_time_ms=median_time,
                min_time_ms=min_time,
                max_time_ms=max_time,
                median_frame_index=median_frame,
                min_frame_index=min_frame,
                max_frame_index=max_frame,
                min_depth=int(data["min_depth"]) if data["min_depth"] is not None else 0,
                max_depth=int(data["max_depth"]) if data["max_depth"] is not None else 0,
                total_time_ms=total_time,
                mean_time_ms=statistics.fmean(time_values),
                lower_quartile_time_ms=percentile(time_values, 0.25),
                upper_quartile_time_ms=percentile(time_values, 0.75),
                count_total=sum(count_values),
                count_median=round(statistics.median(count_values)),
                count_min=min(count_values),
                count_max=max(count_values),
                frame_count=len(frame_items),
                first_frame_index=frame_items[0][0],
                min_individual_time_ms=float(data["min_individual_time"])
                if data["min_individual_time"] is not None
                else 0.0,
                max_individual_time_ms=float(data["max_individual_time"])
                if data["max_individual_time"] is not None
                else 0.0,
                min_individual_frame_index=int(data["min_individual_frame"]),
                max_individual_frame_index=int(data["max_individual_frame"]),
                time_at_median_frame_ms=time_at_median_frame,
                gc_total_bytes=sum(gc_values),
                gc_count=sum(int(value[3]) for _, value in frame_items),
                gc_mean_bytes=round(statistics.fmean(gc_values)),
                gc_median_bytes=round(gc_median),
                gc_min_bytes=gc_min,
                gc_max_bytes=gc_max,
                gc_lower_quartile_bytes=round(percentile(gc_values, 0.25)),
                gc_upper_quartile_bytes=round(percentile(gc_values, 0.75)),
                gc_median_frame_index=gc_median_frame,
                gc_min_frame_index=gc_min_frame,
                gc_max_frame_index=gc_max_frame,
            )
        )

    rows.sort(
        key=lambda row: (
            row.median_time_ms,
            row.max_time_ms,
            row.total_time_ms,
            row.count_total,
            row.name,
        ),
        reverse=True,
    )
    return rows


def choose_nearest_frame_value(
    frame_items: list[tuple[int, list[float | int]]],
    value_index: int,
    target: float,
) -> tuple[int, float]:
    frame_index, values = min(
        frame_items,
        key=lambda item: (abs(float(item[1][value_index]) - target), item[0]),
    )
    return frame_index, float(values[value_index])


def choose_min_frame_value(
    frame_items: list[tuple[int, list[float | int]]],
    value_index: int,
) -> tuple[int, float]:
    frame_index, values = min(frame_items, key=lambda item: (float(item[1][value_index]), item[0]))
    return frame_index, float(values[value_index])


def choose_max_frame_value(
    frame_items: list[tuple[int, list[float | int]]],
    value_index: int,
) -> tuple[int, float]:
    frame_index, values = max(frame_items, key=lambda item: (float(item[1][value_index]), -item[0]))
    return frame_index, float(values[value_index])


def percentile(values: list[float] | list[int], fraction: float) -> float:
    if not values:
        return 0.0
    ordered = sorted(float(value) for value in values)
    if len(ordered) == 1:
        return ordered[0]

    position = (len(ordered) - 1) * fraction
    lower_index = int(position)
    upper_index = min(lower_index + 1, len(ordered) - 1)
    weight = position - lower_index
    return ordered[lower_index] + (ordered[upper_index] - ordered[lower_index]) * weight


def read_profile_frame(
    handle: BinaryIO,
    version: int,
    *,
    selected_thread_indices: set[int],
    frame_index: int,
    aggregate: dict[int, dict[str, object]] | None,
    timing_aggregate: dict[int, dict[str, object]] | None,
) -> tuple[float, int]:
    if version > 1:
        handle.read(8)
    ms_frame = read_float32(handle)
    thread_count = read_int32(handle)
    frame_gc_bytes = 0
    selected_frame_totals: dict[int, list[int]] = {}

    for _ in range(thread_count):
        thread_index = read_int32(handle)
        marker_count = read_int32(handle)
        markers = [read_profile_marker(handle, version) for _ in range(marker_count)]
        frame_gc_bytes += sum(marker[3] for marker in markers)

        if aggregate is None or thread_index not in selected_thread_indices:
            if timing_aggregate is not None and thread_index in selected_thread_indices:
                record_marker_timing(markers, frame_index, timing_aggregate)
            continue

        bytes_with_children, allocation_counts = calculate_marker_child_allocations(markers)
        for marker, gc_bytes, allocation_count in zip(
            markers,
            bytes_with_children,
            allocation_counts,
            strict=False,
        ):
            if gc_bytes <= 0 and allocation_count <= 0:
                continue
            totals = selected_frame_totals.setdefault(marker[0], [0, 0])
            totals[0] += gc_bytes
            totals[1] += allocation_count

        if timing_aggregate is not None:
            record_marker_timing(
                markers,
                frame_index,
                timing_aggregate,
                bytes_with_children=bytes_with_children,
                allocation_counts=allocation_counts,
            )

    for marker_index, totals in selected_frame_totals.items():
        data = aggregate[marker_index]
        data["total"] = int(data["total"]) + totals[0]
        data["count"] = int(data["count"]) + totals[1]
        frames = data["frames"]
        if isinstance(frames, list):
            frames.append((frame_index, totals[0]))

    return ms_frame, frame_gc_bytes


def record_marker_timing(
    markers: list[tuple[int, float, int, int]],
    frame_index: int,
    aggregate: dict[int, dict[str, object]],
    *,
    bytes_with_children: list[int] | None = None,
    allocation_counts: list[int] | None = None,
) -> None:
    if bytes_with_children is None or allocation_counts is None:
        bytes_with_children, allocation_counts = calculate_marker_child_allocations(markers)

    for marker, gc_bytes, allocation_count in zip(
        markers,
        bytes_with_children,
        allocation_counts,
        strict=False,
    ):
        marker_index = marker[0]
        marker_time = marker[1]
        marker_depth = marker[2]
        data = aggregate[marker_index]
        frames = data["frames"]
        if not isinstance(frames, dict):
            continue

        frame_values = frames.setdefault(frame_index, [0.0, 0, 0, 0])
        frame_values[0] = float(frame_values[0]) + marker_time
        frame_values[1] = int(frame_values[1]) + 1
        frame_values[2] = int(frame_values[2]) + gc_bytes
        frame_values[3] = int(frame_values[3]) + allocation_count

        if data["min_depth"] is None or marker_depth < int(data["min_depth"]):
            data["min_depth"] = marker_depth
        if data["max_depth"] is None or marker_depth > int(data["max_depth"]):
            data["max_depth"] = marker_depth
        if data["min_individual_time"] is None or marker_time < float(data["min_individual_time"]):
            data["min_individual_time"] = marker_time
            data["min_individual_frame"] = frame_index
        if data["max_individual_time"] is None or marker_time > float(data["max_individual_time"]):
            data["max_individual_time"] = marker_time
            data["max_individual_frame"] = frame_index


def read_profile_marker(handle: BinaryIO, version: int) -> tuple[int, float, int, int]:
    name_index = read_int32(handle)
    ms_marker_total = read_float32(handle)
    depth = read_int32(handle)
    if version == 3:
        handle.read(4)
    bytes_allocated = read_int64(handle) if version >= 8 else 0
    return name_index, ms_marker_total, depth, bytes_allocated


def calculate_marker_child_allocations(
    markers: list[tuple[int, float, int, int]]
) -> tuple[list[int], list[int]]:
    bytes_with_children = [marker[3] for marker in markers]
    allocation_counts = [1 if marker[3] > 0 else 0 for marker in markers]
    stack: list[int] = []

    def pop_and_record() -> None:
        child_index = stack.pop()
        if stack:
            parent_index = stack[-1]
            bytes_with_children[parent_index] += bytes_with_children[child_index]
            allocation_counts[parent_index] += allocation_counts[child_index]

    for marker_index, marker in enumerate(markers):
        depth = marker[2]
        if depth >= len(stack):
            if depth == len(stack):
                pop_and_record()
        else:
            while len(stack) >= depth:
                pop_and_record()
        stack.append(marker_index)

    while stack:
        pop_and_record()

    return bytes_with_children, allocation_counts


def skip_profile_frame(handle: BinaryIO, version: int, marker_record_size: int) -> None:
    if version > 1:
        handle.seek(8, 1)
    handle.seek(4, 1)
    thread_count = read_int32(handle)
    for _ in range(thread_count):
        handle.seek(4, 1)
        marker_count = read_int32(handle)
        handle.seek(marker_record_size * marker_count, 1)


def get_marker_record_size(version: int) -> int:
    size = 4 + 4 + 4
    if version == 3:
        size += 4
    if version >= 8:
        size += 8
    return size


def write_frame_rows(path: Path, rows: list[FrameRow]) -> None:
    with path.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.writer(handle, delimiter=";")
        writer.writerow(
            [
                "Frame Offset",
                " Frame Index",
                " Frame Time (ms)",
                " Time from first frame (ms)",
                " GC Alloc (bytes)",
            ]
        )
        for row in rows:
            writer.writerow(
                [
                    row.frame_offset,
                    row.frame_index,
                    row.frame_time_ms,
                    "",
                    row.gc_alloc_bytes,
                ]
            )


def write_marker_rows(path: Path, rows: list[MarkerRow]) -> None:
    with path.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.writer(handle, delimiter=";")
        writer.writerow(
            [
                "Name",
                "GC Alloc Total (bytes)",
                "GC Alloc Count",
                "GC Alloc Mean (bytes)",
                "GC Alloc Median (bytes)",
                "GC Alloc Max (bytes)",
                "GC Alloc Max Frame Index",
            ]
        )
        for row in rows:
            writer.writerow(
                [
                    row.name,
                    row.gc_total_bytes,
                    row.gc_count,
                    row.gc_mean_bytes,
                    row.gc_median_bytes,
                    row.gc_max_bytes,
                    row.max_frame_index,
                ]
            )


def write_marker_timing_rows(path: Path, rows: list[MarkerTimingRow]) -> None:
    with path.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.writer(handle, delimiter=";")
        writer.writerow(
            [
                "Name",
                "Median Time",
                "Min Time",
                "Max Time",
                "Median Frame Index",
                "Min Frame Index",
                "Max Frame Index",
                "Min Depth",
                "Max Depth",
                "Total Time",
                "Mean Time",
                "Time Lower Quartile",
                "Time Upper Quartile",
                "Count Total",
                "Count Median",
                "Count Min",
                "Count Max",
                "Number of frames containing Marker",
                "First Frame Index",
                "Time Min Individual",
                "Time Max Individual",
                "Min Individual Frame",
                "Max Individual Frame",
                "Time at Median Frame",
                "GC Alloc Total (bytes)",
                "GC Alloc Count",
                "GC Alloc Mean (bytes)",
                "GC Alloc Median (bytes)",
                "GC Alloc Min (bytes)",
                "GC Alloc Max (bytes)",
                "GC Alloc Lower Quartile (bytes)",
                "GC Alloc Upper Quartile (bytes)",
                "GC Alloc Median Frame Index",
                "GC Alloc Min Frame Index",
                "GC Alloc Max Frame Index",
            ]
        )
        for row in rows:
            writer.writerow(
                [
                    row.name,
                    row.median_time_ms,
                    row.min_time_ms,
                    row.max_time_ms,
                    row.median_frame_index,
                    row.min_frame_index,
                    row.max_frame_index,
                    row.min_depth,
                    row.max_depth,
                    row.total_time_ms,
                    row.mean_time_ms,
                    row.lower_quartile_time_ms,
                    row.upper_quartile_time_ms,
                    row.count_total,
                    row.count_median,
                    row.count_min,
                    row.count_max,
                    row.frame_count,
                    row.first_frame_index,
                    row.min_individual_time_ms,
                    row.max_individual_time_ms,
                    row.min_individual_frame_index,
                    row.max_individual_frame_index,
                    row.time_at_median_frame_ms,
                    row.gc_total_bytes,
                    row.gc_count,
                    row.gc_mean_bytes,
                    row.gc_median_bytes,
                    row.gc_min_bytes,
                    row.gc_max_bytes,
                    row.gc_lower_quartile_bytes,
                    row.gc_upper_quartile_bytes,
                    row.gc_median_frame_index,
                    row.gc_min_frame_index,
                    row.gc_max_frame_index,
                ]
            )


def read_int32(handle: BinaryIO) -> int:
    return struct.unpack("<i", handle.read(4))[0]


def read_int64(handle: BinaryIO) -> int:
    return struct.unpack("<q", handle.read(8))[0]


def read_float32(handle: BinaryIO) -> float:
    return struct.unpack("<f", handle.read(4))[0]


def read_dotnet_string(handle: BinaryIO) -> str:
    byte_count = read_7bit_encoded_int(handle)
    return handle.read(byte_count).decode("utf-8", errors="replace")


def read_7bit_encoded_int(handle: BinaryIO) -> int:
    count = 0
    shift = 0
    while True:
        raw = handle.read(1)
        if not raw:
            raise EOFError("unexpected end of .pdata while reading string length")
        value = raw[0]
        count |= (value & 0x7F) << shift
        if value & 0x80 == 0:
            return count
        shift += 7


def read_csv_rows(path: Path) -> list[dict[str, str]]:
    if not path.exists():
        raise FileNotFoundError(path)
    with path.open(newline="", encoding="utf-8-sig") as handle:
        return [normalize_row(row) for row in csv.DictReader(handle, delimiter=";")]


def normalize_row(row: dict[str, str]) -> dict[str, str]:
    return {(key or "").strip(): (value or "").strip() for key, value in row.items()}


def parse_int(value: str | None) -> int:
    if not value:
        return 0
    try:
        return int(float(value.strip()))
    except ValueError:
        return 0


def parse_float(value: str | None) -> float:
    if not value:
        return 0.0
    try:
        return float(value.strip())
    except ValueError:
        return 0.0


if __name__ == "__main__":
    raise SystemExit(main())
