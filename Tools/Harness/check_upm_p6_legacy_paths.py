#!/usr/bin/env python3
"""Reject executable references to the retired pre-P6 package root."""

from __future__ import annotations

import argparse
import json
import pathlib
import subprocess


ROOT = pathlib.Path(__file__).resolve().parents[2]
LEGACY_MARKERS = ("Assets/Scripts/MxFramework", "Assets\\Scripts\\MxFramework")
ALLOWED_PREFIXES = (
    "Docs/Tasks/",
    "Docs/Evidence/",
    "Docs/UPM/Evidence/",
    "Docs/UPM/Inventories/",
    "Docs/UPM/Migrations/",
    "Docs/UPM/Phases/",
    "Docs/Decisions/",
)
ALLOWED_FILES = {
    "Docs/UPM_EXTERNAL_CONSUMPTION_IMPLEMENTATION_PLAN.md",
    "Docs/UPM_OPTIMIZATION_ROADMAP.md",
    "Docs/UPM_PACKAGE_SURFACE_LAYOUT_OPTIMIZATION.md",
    "Docs/UPM_PHASE_TASKS.md",
    "Tools/Harness/apply_upm_p2_move_manifest.py",
    "Tools/Harness/build_upm_p2_move_manifest.py",
    "Tools/Harness/build_upm_p3_test_surface_manifest.py",
    "Tools/Harness/build_upm_p4_demo_inventory.py",
    "Tools/Harness/build_upm_p5_editor_inventory.py",
    "Tools/Harness/build_upm_p5_editor_move_manifest.py",
    "Tools/Harness/build_upm_p6_cutover_manifest.py",
    "Tools/Harness/check_upm_p6_legacy_paths.py",
}


def tracked_and_untracked_files() -> list[pathlib.PurePosixPath]:
    output = subprocess.run(
        ["git", "ls-files", "--cached", "--others", "--exclude-standard", "-z"],
        cwd=ROOT,
        check=True,
        stdout=subprocess.PIPE,
    ).stdout.decode("utf-8", errors="surrogateescape")
    return [pathlib.PurePosixPath(path) for path in output.split("\0") if path]


def is_allowed(path: str) -> bool:
    return path in ALLOWED_FILES or any(path.startswith(prefix) for prefix in ALLOWED_PREFIXES)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--report-json", type=pathlib.Path)
    args = parser.parse_args()

    allowed_hits: list[dict[str, object]] = []
    failures: list[dict[str, object]] = []
    for relative in tracked_and_untracked_files():
        path = ROOT.joinpath(*relative.parts)
        if not path.is_file():
            continue
        try:
            text = path.read_text(encoding="utf-8-sig")
        except (UnicodeDecodeError, OSError):
            continue
        normalized = relative.as_posix()
        for line_number, line in enumerate(text.splitlines(), start=1):
            if not any(marker in line for marker in LEGACY_MARKERS):
                continue
            hit = {"path": normalized, "line": line_number}
            if is_allowed(normalized):
                allowed_hits.append(hit)
            else:
                failures.append(hit)

    report = {
        "schemaVersion": 1,
        "phase": "UPM-P6",
        "legacyRoot": "Assets/Scripts/MxFramework",
        "result": "passed" if not failures else "failed",
        "productionOrCurrentExecutableHitCount": len(failures),
        "historicalOrMigrationHitCount": len(allowed_hits),
        "allowedPrefixes": list(ALLOWED_PREFIXES),
        "allowedFiles": sorted(ALLOWED_FILES),
        "failures": failures,
    }
    if args.report_json:
        output = args.report_json if args.report_json.is_absolute() else ROOT / args.report_json
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(
        f"P6 legacy path audit {report['result']}: "
        f"{len(failures)} executable/current hits, {len(allowed_hits)} allowed historical hits."
    )
    for failure in failures[:20]:
        print(f"FAIL {failure['path']}:{failure['line']}")
    return 0 if not failures else 1


if __name__ == "__main__":
    raise SystemExit(main())
