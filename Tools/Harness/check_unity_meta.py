#!/usr/bin/env python3
"""Check that changed Unity assets under Assets/ have matching .meta files."""

from __future__ import annotations

import pathlib
import sys


def is_unity_asset(path: pathlib.Path) -> bool:
    if not path.parts or path.parts[0] != "Assets":
        return False
    if path.suffix == ".meta":
        return False
    if path.name.startswith("."):
        return False
    return True


def main() -> int:
    if len(sys.argv) != 2:
        print("usage: check_unity_meta.py <changed-paths-file>", file=sys.stderr)
        return 2

    changed_paths_file = pathlib.Path(sys.argv[1])
    failed = False

    for raw_path in changed_paths_file.read_text(encoding="utf-8").splitlines():
        path = pathlib.Path(raw_path)
        if not is_unity_asset(path):
            continue

        if not path.exists():
            continue

        meta_path = pathlib.Path(f"{raw_path}.meta")
        if not meta_path.exists():
            print(f"Unity asset is missing .meta file: {raw_path}", file=sys.stderr)
            failed = True

    if failed:
        return 1

    print("Unity .meta pairing check passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
