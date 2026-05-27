#!/usr/bin/env python3
"""Lightweight documentation health checks for changed project docs."""

from __future__ import annotations

import pathlib
import re
import sys


VALID_STATUS = {"Current", "Guide", "Design", "ADR", "Archive", "Draft"}
STATUS_RE = re.compile(r"^> Status: (?P<status>[A-Za-z]+)\s*$")
REMOVED_INDEX_TERMS = [
    "Docs/" + "GIT" + "NEXUS",
    "GIT" + "NEXUS",
    "Git" + "Nexus",
    "git" + "nexus",
    "Tools/" + "Git" + "Nexus",
    "." + "git" + "nexus",
]
REMOVED_INDEX_RE = re.compile("|".join(re.escape(term) for term in REMOVED_INDEX_TERMS))

ENTRY_DOCS = [
    pathlib.Path("Docs/README.md"),
    pathlib.Path("Docs/PROJECT_INDEX.md"),
    pathlib.Path("Docs/Tasks/README.md"),
]


def read_text(path: pathlib.Path) -> str:
    return path.read_text(encoding="utf-8")


def first_lines(text: str, count: int = 12) -> list[str]:
    return text.splitlines()[:count]


def status_in_header(path: pathlib.Path) -> str | None:
    for line in first_lines(read_text(path)):
        match = STATUS_RE.match(line)
        if match:
            return match.group("status")
    return None


def is_docs_markdown(path: pathlib.Path) -> bool:
    return path.suffix == ".md" and path.parts and path.parts[0] == "Docs"


def requires_status(path: pathlib.Path) -> bool:
    if not is_docs_markdown(path):
        return False
    if len(path.parts) >= 2 and path.parts[1] == "Tasks":
        return path == pathlib.Path("Docs/Tasks/README.md")
    return True


def add_failure(failures: list[str], message: str) -> None:
    failures.append(message)


def check_status(path: pathlib.Path, failures: list[str]) -> None:
    if not requires_status(path) or not path.exists():
        return

    status = status_in_header(path)
    if status is None:
        add_failure(failures, f"{path}: missing '> Status: ...' in the first 12 lines")
        return

    if status not in VALID_STATUS:
        valid = ", ".join(sorted(VALID_STATUS))
        add_failure(failures, f"{path}: invalid Status '{status}' (expected one of: {valid})")


def check_removed_index_terms(path: pathlib.Path, failures: list[str]) -> None:
    if not path.exists() or path.is_dir():
        return
    if path.suffix not in {".md", ".sh", ".py"} and path.name != "AGENTS.md":
        return

    for line_number, line in enumerate(read_text(path).splitlines(), start=1):
        if REMOVED_INDEX_RE.search(line):
            add_failure(failures, f"{path}:{line_number}: removed code-index term remains")


def check_entry_docs(failures: list[str]) -> None:
    for path in ENTRY_DOCS:
        if not path.exists():
            add_failure(failures, f"{path}: required entry document is missing")
            continue
        check_status(path, failures)

    readme = read_text(pathlib.Path("Docs/README.md"))
    for required in [
        "## 文档状态",
        "## 当前事实源",
        "## 归档和决策",
        "Current",
        "Guide",
        "Design",
        "Archive",
        "Draft",
    ]:
        if required not in readme:
            add_failure(failures, f"Docs/README.md: missing required marker '{required}'")

    project_index = read_text(pathlib.Path("Docs/PROJECT_INDEX.md"))
    for required in [
        "## Default Context Pack",
        "## Conditional Packs",
        "## Do Not Read By Default",
        "- `Docs/Tasks/`",
        "Conflict Rule",
    ]:
        if required not in project_index:
            add_failure(failures, f"Docs/PROJECT_INDEX.md: missing required marker '{required}'")

    tasks_readme = read_text(pathlib.Path("Docs/Tasks/README.md"))
    if "Status: Archive" not in tasks_readme:
        add_failure(failures, "Docs/Tasks/README.md: task archive must stay marked Archive")


def changed_paths(changed_paths_file: pathlib.Path) -> list[pathlib.Path]:
    return [
        pathlib.Path(raw_path)
        for raw_path in changed_paths_file.read_text(encoding="utf-8").splitlines()
        if raw_path.strip()
    ]


def main() -> int:
    if len(sys.argv) != 2:
        print("usage: check_docs_health.py <changed-paths-file>", file=sys.stderr)
        return 2

    failures: list[str] = []
    paths_to_check = set(changed_paths(pathlib.Path(sys.argv[1])))
    paths_to_check.update(ENTRY_DOCS)
    paths_to_check.add(pathlib.Path("AGENTS.md"))

    for path in sorted(paths_to_check):
        check_status(path, failures)
        check_removed_index_terms(path, failures)

    check_entry_docs(failures)

    if failures:
        for failure in failures:
            print(failure, file=sys.stderr)
        return 1

    print("Docs health check passed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
