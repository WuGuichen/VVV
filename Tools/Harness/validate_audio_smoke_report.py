#!/usr/bin/env python3
"""Validate the deterministic Audio Validation Player report."""

from __future__ import annotations

import argparse
from pathlib import Path


def parse_report(path: Path) -> dict[str, str]:
    values: dict[str, str] = {}
    for raw_line in path.read_text(encoding="utf-8").splitlines():
        key, separator, value = raw_line.partition(":")
        if separator:
            values[key.strip()] = value.strip()
    return values


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--report", type=Path, required=True)
    args = parser.parse_args()

    if not args.report.is_file():
        print(f"audio smoke report missing: {args.report}")
        return 2

    values = parse_report(args.report)
    failures: list[str] = []
    if values.get("status") != "Passed":
        failures.append(f"status={values.get('status', '<missing>')}")
    if values.get("activeEventCount") != "0":
        failures.append(f"activeEventCount={values.get('activeEventCount', '<missing>')}")
    if values.get("recentErrorCount") != "0":
        failures.append(f"recentErrorCount={values.get('recentErrorCount', '<missing>')}")
    try:
        if int(values.get("channels", "0")) <= 0:
            failures.append(f"channels={values.get('channels', '<missing>')}")
        if int(values.get("memoryBytes", "0")) <= 0:
            failures.append(f"memoryBytes={values.get('memoryBytes', '<missing>')}")
    except ValueError as exception:
        failures.append(str(exception))

    if failures:
        print("audio smoke report failed: " + ", ".join(failures))
        return 1
    print(
        "audio smoke report passed: "
        f"plays={values.get('totalPlayRequests')} "
        f"channels={values.get('channels')} "
        f"memoryBytes={values.get('memoryBytes')}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
