#!/usr/bin/env python3
"""Documentation structure and declared code/document delivery checks.

Passing this check does not establish that documentation claims are true.
"""

from __future__ import annotations

import argparse
import fnmatch
import pathlib
import re
import subprocess
import sys


VALID_STATUS = {"Current", "Guide", "Design", "ADR", "Archive", "Draft"}
STATUS_RE = re.compile(r"^> Status: (?P<status>[A-Za-z]+)\s*$")
CODE_DOC_RE = re.compile(r"^> Code-Doc: `(?P<pattern>[^`]+)`\s*$")
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
REMOVED_WORKFLOW_DOC = pathlib.Path("Docs") / "WORKFLOW.md"


def read_text(path: pathlib.Path) -> str:
    return path.read_text(encoding="utf-8-sig")


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
    if REMOVED_WORKFLOW_DOC.exists():
        add_failure(failures, f"{REMOVED_WORKFLOW_DOC}: removed workflow document must not be restored")

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
        "## Task Routes",
        "Conflict Rule",
    ]:
        if required not in project_index:
            add_failure(failures, f"Docs/PROJECT_INDEX.md: missing required marker '{required}'")

    tasks_readme = read_text(pathlib.Path("Docs/Tasks/README.md"))
    if "Status: Archive" not in tasks_readme:
        add_failure(failures, "Docs/Tasks/README.md: task archive must stay marked Archive")

    removed_refs = ("Docs/" + "WORKFLOW.md", "`" + "WORKFLOW.md" + "`")
    for path in [*ENTRY_DOCS, pathlib.Path("AGENTS.md")]:
        text = read_text(path)
        if any(reference in text for reference in removed_refs):
            add_failure(failures, f"{path}: removed workflow entrypoint is still referenced")


def changed_paths(changed_paths_file: pathlib.Path) -> list[pathlib.Path]:
    return [
        pathlib.Path(raw_path.strip().replace("\\", "/"))
        for raw_path in changed_paths_file.read_text(encoding="utf-8-sig").splitlines()
        if raw_path.strip()
    ]


def code_patterns(text: str, path: pathlib.Path, failures: list[str]) -> tuple[str, ...]:
    patterns = []
    for number, line in enumerate(text.splitlines(), 1):
        if not line.startswith("> Code-Doc:"):
            continue
        match = CODE_DOC_RE.fullmatch(line)
        pattern = match.group("pattern") if match else ""
        parts = pathlib.PurePosixPath(pattern).parts
        if (not parts or parts[0] not in {"Assets", "Packages", "Tools", "FGUIProject"}
                or ".." in parts or "\\" in pattern or pattern.endswith("/")):
            add_failure(failures, f"{path}:{number}: invalid Code-Doc repository path/glob")
            continue
        patterns.append(pattern)
    return tuple(dict.fromkeys(patterns))


def current_authorities(failures: list[str]) -> dict[pathlib.Path, tuple[str, ...]]:
    result = {}
    for path in sorted(pathlib.Path("Docs").rglob("*.md")):
        patterns = code_patterns(read_text(path), path, failures)
        if patterns:
            result[path] = patterns
    return result


def baseline_authorities(base_ref: str, failures: list[str]) -> dict[pathlib.Path, tuple[str, ...]]:
    """Read the actual base so deleting a declaration cannot silently disable it."""
    revision = subprocess.run(["git", "rev-parse", "--verify", base_ref + "^{commit}"],
                              capture_output=True, encoding="utf-8", errors="replace")
    if revision.returncode:
        add_failure(failures, f"Code-Doc: cannot resolve base ref {base_ref!r}")
        return {}
    commit = revision.stdout.strip()
    found = subprocess.run(["git", "grep", "-l", "-z", "-F", "-e", "> Code-Doc:",
                            commit, "--", "Docs"], capture_output=True)
    if found.returncode not in (0, 1):
        add_failure(failures, "Code-Doc: cannot read baseline authority declarations")
        return {}
    result = {}
    for item in found.stdout.decode("utf-8").split("\0"):
        if not item:
            continue
        path = pathlib.Path(item.split(":", 1)[1])
        if not is_docs_markdown(path):
            continue
        source = subprocess.run(["git", "show", commit + ":" + path.as_posix()],
                                capture_output=True, encoding="utf-8-sig", errors="replace")
        if source.returncode:
            add_failure(failures, f"{path}: cannot read baseline Code-Doc declaration")
            continue
        result[path] = code_patterns(source.stdout, path, failures)
    return result


def check_code_doc(paths: set[pathlib.Path], current: dict[pathlib.Path, tuple[str, ...]],
                   previous: dict[pathlib.Path, tuple[str, ...]], failures: list[str]) -> int:
    changed = {path.as_posix() for path in paths}
    covered = set()
    # Every previous declaration must remain or be explicitly transferred to another
    # changed authority. A deleted/renamed code path still matches the old declaration.
    for old_doc, patterns in previous.items():
        for pattern in patterns:
            retained = any(pattern in values for values in current.values())
            if not retained:
                # Source retirement/movement can legitimately remove an old path,
                # but the existing explanation must be processed and the old path
                # must actually be part of the delivery. Live code cannot lose coverage.
                retired = (old_doc.is_file() and old_doc.as_posix() in changed
                           and any(fnmatch.fnmatchcase(path, pattern) for path in changed)
                           and not any(p.is_file() for p in pathlib.Path().glob(pattern)))
                if not retired:
                    add_failure(failures, f"{old_doc}: Code-Doc declaration removed without a replacement: {pattern}")
            elif pattern not in current.get(old_doc, ()):
                replacements = [doc for doc, values in current.items() if pattern in values]
                if not any(doc.as_posix() in changed for doc in replacements):
                    add_failure(failures, f"{old_doc}: replacement authority must be in this change")
    for doc, patterns in current.items():
        hits = {path for path in changed
                if any(fnmatch.fnmatchcase(path, pattern) for pattern in patterns)}
        covered.update(hits)
        for pattern in patterns:
            if not any(p.is_file() for p in pathlib.Path().glob(pattern)) and not any(
                    fnmatch.fnmatchcase(path, pattern) for path in changed):
                add_failure(failures, f"{doc}: Code-Doc pattern matches no file: {pattern}")
        if hits and (doc.as_posix() not in changed or not doc.is_file()):
            sample = ", ".join(sorted(hits)[:3])
            add_failure(failures, f"{doc}: related code changed without its authority document: {sample}")
    # Old patterns must also catch scope removal or source movement in the same change.
    for doc, patterns in previous.items():
        hits = {path for path in changed
                if any(fnmatch.fnmatchcase(path, pattern) for pattern in patterns)}
        if hits:
            covered.update(hits)
            if doc in current and doc.as_posix() not in changed:
                add_failure(failures, f"{doc}: baseline-related code changed without its authority document")
    return len(covered)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("changed_paths_file", type=pathlib.Path)
    parser.add_argument("--base-ref", default="HEAD", help="same base used to collect changed paths")
    args = parser.parse_args(argv)

    failures: list[str] = []
    paths = set(changed_paths(args.changed_paths_file))
    authorities = current_authorities(failures)
    previous = baseline_authorities(args.base_ref, failures)
    covered = check_code_doc(paths, authorities, previous, failures)
    paths_to_check = set(paths)
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

    print(f"Docs health check passed. Code-Doc: {len(authorities)} declared authorities, "
          f"{covered} related changed paths. Content truth still requires review; "
          "unmapped code is outside this check.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
