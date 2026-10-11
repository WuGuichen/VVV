#!/usr/bin/env python3
"""Rewrite current Markdown links using the reviewed UPM-P2 move manifest."""

from __future__ import annotations

import argparse
import json
import pathlib


REPOSITORY_ROOT = pathlib.Path(__file__).resolve().parents[2]
DEFAULT_MANIFEST = (
    REPOSITORY_ROOT
    / "Docs/UPM/Evidence/UPM-P2/character-test-move-manifest.json"
)


def is_current_markdown(path: pathlib.Path, text: str) -> bool:
    relative = path.relative_to(REPOSITORY_ROOT).as_posix()
    if relative.startswith("Docs/Tasks/"):
        return False
    if relative.startswith("Docs/Evidence/"):
        return False
    if relative.startswith("Docs/UPM/Evidence/UPM-P2/"):
        return False
    return "> Status: Archive" not in text


def replacements_from_manifest(path: pathlib.Path) -> list[tuple[str, str]]:
    raw = json.loads(path.read_text(encoding="utf-8-sig"))
    if raw.get("schemaVersion") != 1 or raw.get("phase") != "UPM-P2":
        raise RuntimeError("unsupported UPM-P2 move manifest")
    replacements = {
        str(entry["source"]): str(entry["target"])
        for entry in raw.get("moves", [])
    }
    return sorted(
        replacements.items(),
        key=lambda item: (len(item[0]), item[0]),
        reverse=True,
    )


def rewrite_text(text: str, replacements: list[tuple[str, str]]) -> tuple[str, int]:
    count = 0
    for source, target in replacements:
        matches = text.count(source)
        if not matches:
            continue
        text = text.replace(source, target)
        count += matches
    return text, count


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", type=pathlib.Path, default=DEFAULT_MANIFEST)
    parser.add_argument("--apply", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    replacements = replacements_from_manifest(args.manifest.resolve())
    changed: list[tuple[str, int]] = []
    for path in sorted(REPOSITORY_ROOT.rglob("*.md")):
        if ".git" in path.parts or "Library" in path.parts or "Temp" in path.parts:
            continue
        text = path.read_text(encoding="utf-8-sig")
        if not is_current_markdown(path, text):
            continue
        rewritten, count = rewrite_text(text, replacements)
        if not count:
            continue
        relative = path.relative_to(REPOSITORY_ROOT).as_posix()
        changed.append((relative, count))
        if args.apply:
            path.write_text(rewritten, encoding="utf-8", newline="")

    mode = "updated" if args.apply else "would update"
    print(
        f"UPM-P2 document path rewrite {mode} {len(changed)} file(s), "
        f"{sum(count for _, count in changed)} occurrence(s)."
    )
    for relative, count in changed:
        print(f"{count:4d} {relative}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
