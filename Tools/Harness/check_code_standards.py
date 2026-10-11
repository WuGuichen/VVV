#!/usr/bin/env python3
"""Report likely MxFramework C# code-standard and unsafe-boundary issues.

The first pass is intentionally report-only. It gives agents and reviewers a
stable view of likely violations without turning existing historical debt into
an immediate hard gate.
"""

from __future__ import annotations

import argparse
import os
import pathlib
import re
import subprocess
import sys
from dataclasses import dataclass
from typing import Iterable


FIRST_PARTY_ROOTS = [
    pathlib.Path("Packages/com.vincent.wgameframework"),
    pathlib.Path("Assets/Scripts/WGameFrameworkProject"),
    pathlib.Path("Tools"),
]
SCANNED_SUFFIXES = {".cs", ".asmdef"}

ALLOWED_UNSAFE_ASMDEFS = {
    pathlib.Path("Packages/com.vincent.wgameframework/Core/MxFramework.Core.asmdef"),
}

SKIPPED_PARTS = {
    ".git",
    "Library",
    "Temp",
    "Logs",
    "UserSettings",
    "Assets/Plugins",
    "Assets/FImpossible Creations",
}

HOT_METHOD_NAMES = {
    "Tick",
    "Update",
    "LateUpdate",
    "FixedUpdate",
    "Execute",
    "Publish",
}
UNITY_TIME_BOUNDARY_METHODS = {"Update", "LateUpdate", "FixedUpdate"}

METHOD_RE = re.compile(
    r"^\s*"
    r"(?:(?:public|private|protected|internal|static|sealed|override|virtual|async|partial|new)\s+)*"
    r"[\w<>\[\],\s\.\?]+?\s+"
    r"(?P<name>Tick|Update|LateUpdate|FixedUpdate|Execute|Publish)\s*"
    r"\(",
)

OBSOLETE_UNITY_API_PATTERNS = [
    (
        re.compile(r"\bGetInstanceID\s*\("),
        "unity-obsolete-object-id",
        "Unity 6000 object identity should use GetEntityId/EntityId.",
    ),
    (
        re.compile(r"\bInstanceIDToObject\s*\("),
        "unity-obsolete-object-id",
        "Unity 6000 object lookup should use EntityIdToObject with EntityId.",
    ),
    (
        re.compile(r"\bFindObjectOfType\s*(?:<|\()"),
        "unity-obsolete-find",
        "Use FindAnyObjectByType or an explicit reference/cache.",
    ),
    (
        re.compile(r"\bFindObjectsOfType\s*(?:<|\()"),
        "unity-obsolete-find",
        "Use FindObjectsByType with an explicit inactive policy.",
    ),
    (
        re.compile(r"\bFindFirstObjectByType\s*<"),
        "unity-hot-find",
        "Prefer cached references or FindAnyObjectByType when ordering is not required.",
    ),
    (
        re.compile(r"\bFindObjectsByType\s*\([^)]*FindObjectsSortMode\.None"),
        "unity-find-sortmode",
        "Prefer the Unity 6000 inactive-policy overload when sorting is not required.",
    ),
]

HOT_PATH_RISK_PATTERNS = [
    (re.compile(r"\.ToList\s*\("), "hot-linq-allocation", "ToList allocates in hot paths."),
    (re.compile(r"\.ToArray\s*\("), "hot-linq-allocation", "ToArray allocates in hot paths."),
    (re.compile(r"\.(Select|Where|OrderBy|ThenBy|GroupBy|Distinct)\s*\("), "hot-linq", "LINQ in hot paths can allocate or hide enumeration cost."),
    (re.compile(r"\bnew\s+(List|Dictionary|HashSet|Queue|Stack|StringBuilder|Regex)\s*<"), "hot-collection-allocation", "Temporary collections should be pooled or preallocated in hot paths."),
    (
        re.compile(r"\bnew\s+(?!(?:ArgumentException|ArgumentNullException|ArgumentOutOfRangeException)\b)\w*Exception\s*\("),
        "hot-exception",
        "Exceptions must not be used for hot-path flow control.",
    ),
    (re.compile(r"\bstring\.Format\s*\("), "hot-string-format", "String formatting allocates in hot paths."),
    (re.compile(r"\$\""), "hot-string-interpolation", "String interpolation allocates in hot paths."),
    (re.compile(r"\bFind(Any|First)?Object[s]?ByType\s*<"), "hot-unity-search", "Scene-wide object search is not acceptable in hot paths."),
    (re.compile(r"\bGetComponent[s]?\s*<"), "hot-unity-component-search", "Component lookup in hot paths should be cached unless documented."),
    (re.compile(r"\bResources\.Load\s*<"), "hot-resource-load", "Resource loading is not a hot-path operation."),
]

UNSAFE_RISK_PATTERNS = [
    (
        re.compile(r"\bunsafe\b"),
        "unsafe-keyword",
        "Unsafe code requires documented bottleneck evidence and a narrow internal boundary.",
    ),
    (
        re.compile(r"\bfixed\s*\("),
        "unsafe-fixed",
        "Pinned memory or fixed buffers require an unsafe boundary review.",
    ),
    (
        re.compile(r"\bstackalloc\b"),
        "unsafe-stackalloc",
        "Stack allocation requires size bounds and platform review.",
    ),
    (
        re.compile(r"\bMemoryMarshal\b"),
        "unsafe-memorymarshal",
        "MemoryMarshal use requires layout and platform review.",
    ),
    (
        re.compile(r"\bUnsafeUtility\b"),
        "unsafe-utility",
        "UnsafeUtility use requires Unity platform review.",
    ),
    (
        re.compile(r"\bSystem\.Runtime\.CompilerServices\.Unsafe\b|\bUnsafe\."),
        "unsafe-compiler-services",
        "CompilerServices.Unsafe use requires a narrow internal boundary.",
    ),
    (
        re.compile(r"\b(?:void|byte|sbyte|short|ushort|int|uint|long|ulong|char|float|double|decimal|bool)\s*\*\s*(?:[A-Za-z_]|\))"),
        "unsafe-pointer-type",
        "Pointer types must not leak into public API and require unsafe boundary review.",
    ),
]

PUBLIC_UNSAFE_RE = re.compile(r"^\s*(?:\[.*\]\s*)*(?=.*\bpublic\b)(?=.*\bunsafe\b)")
ASMDEF_ALLOW_UNSAFE_RE = re.compile(r'^\s*"allowUnsafeCode"\s*:\s*true\b', re.IGNORECASE)
ALLOC_MARKER_RE = re.compile(r"\b(NoAlloc|LowFreqAlloc|AllocByDesign|alloc|allocation)\b", re.IGNORECASE)
TIME_BOUNDARY_MARKER_RE = re.compile(r"\b(UnityTimeBoundary|TimeBoundary)\b")
LINQ_USING_RE = re.compile(r"^\s*using\s+System\.Linq\s*;")
UNITY_EDITOR_RE = re.compile(r"\b(UnityEditor\.|using\s+UnityEditor\s*;)")
TIME_DELTA_RE = re.compile(r"\bTime\.(deltaTime|fixedDeltaTime|time|unscaledDeltaTime)\b")
UNITY_EDITOR_DIRECTIVE_RE = re.compile(r"\bUNITY_EDITOR\b")


@dataclass(frozen=True)
class Finding:
    path: pathlib.Path
    line: int
    code: str
    message: str


def run_git_root() -> pathlib.Path:
    result = subprocess.run(
        ["git", "rev-parse", "--show-toplevel"],
        check=True,
        capture_output=True,
        text=True,
    )
    return pathlib.Path(result.stdout.strip())


def normalize(path: pathlib.Path) -> pathlib.Path:
    return pathlib.Path(path.as_posix())


def has_skipped_part(path: pathlib.Path) -> bool:
    normalized = path.as_posix()
    return any(normalized == part or normalized.startswith(part + "/") for part in SKIPPED_PARTS)


def is_first_party_code_file(path: pathlib.Path) -> bool:
    path = normalize(path)
    if path.suffix not in SCANNED_SUFFIXES or has_skipped_part(path):
        return False
    return any(path == root or path.is_relative_to(root) for root in FIRST_PARTY_ROOTS)


def is_editor_path(path: pathlib.Path) -> bool:
    parts = path.parts
    return "Editor" in parts or any(part.endswith(".Editor") for part in parts)


def is_test_path(path: pathlib.Path) -> bool:
    return any(part.lower() == "tests" for part in path.parts)


def is_runtime_policy_path(path: pathlib.Path) -> bool:
    if path.parts and path.parts[0] == "Tools":
        return False
    return not is_editor_path(path) and not is_test_path(path)


def changed_paths(changed_paths_file: pathlib.Path) -> list[pathlib.Path]:
    return [
        normalize(pathlib.Path(raw.strip()))
        for raw in changed_paths_file.read_text(encoding="utf-8").splitlines()
        if raw.strip()
    ]


def iter_all_first_party_code_files(repo_root: pathlib.Path) -> Iterable[pathlib.Path]:
    for root in FIRST_PARTY_ROOTS:
        absolute_root = repo_root / root
        if not absolute_root.exists():
            continue
        for path in absolute_root.rglob("*"):
            if path.suffix not in SCANNED_SUFFIXES:
                continue
            relative = normalize(path.relative_to(repo_root))
            if is_first_party_code_file(relative):
                yield relative


def read_lines(repo_root: pathlib.Path, path: pathlib.Path) -> list[str]:
    return (repo_root / path).read_text(encoding="utf-8-sig", errors="replace").splitlines()


def brace_delta(line: str) -> int:
    return line.count("{") - line.count("}")


def previous_text(lines: list[str], index: int, count: int = 6) -> str:
    start = max(0, index - count)
    return "\n".join(lines[start:index])


def is_comment_line(line: str) -> bool:
    stripped = line.lstrip()
    return stripped.startswith("//") or stripped.startswith("///") or stripped.startswith("*")


def is_unity_behaviour_file(lines: list[str]) -> bool:
    return any(": MonoBehaviour" in line or ": UnityEngine.MonoBehaviour" in line for line in lines)


def is_unity_editor_directive(line: str) -> bool:
    return bool(UNITY_EDITOR_DIRECTIVE_RE.search(line)) and "!UNITY_EDITOR" not in line


def scan_asmdef(repo_root: pathlib.Path, path: pathlib.Path) -> list[Finding]:
    lines = read_lines(repo_root, path)
    findings: list[Finding] = []
    if normalize(path) in ALLOWED_UNSAFE_ASMDEFS:
        return findings

    for index, line in enumerate(lines):
        if ASMDEF_ALLOW_UNSAFE_RE.search(line):
            findings.append(
                Finding(
                    path,
                    index + 1,
                    "asmdef-allow-unsafe",
                    "First-party assemblies should not enable allowUnsafeCode without an explicit whitelist entry.",
                )
            )
    return findings


def scan_cs_file(repo_root: pathlib.Path, path: pathlib.Path) -> list[Finding]:
    lines = read_lines(repo_root, path)
    findings: list[Finding] = []
    hot_method: str | None = None
    hot_braces = 0
    pending_hot_method: str | None = None
    unity_behaviour_file = is_unity_behaviour_file(lines)
    editor_guard_stack: list[bool] = []

    for index, line in enumerate(lines):
        line_number = index + 1
        runtime_policy_path = is_runtime_policy_path(path)
        started_hot_method = False
        stripped = line.strip()
        if stripped.startswith("#if"):
            editor_guard_stack.append(is_unity_editor_directive(stripped))
            continue
        if stripped.startswith("#elif"):
            if editor_guard_stack:
                editor_guard_stack[-1] = is_unity_editor_directive(stripped)
            continue
        if stripped.startswith("#else"):
            if editor_guard_stack:
                editor_guard_stack[-1] = False
            continue
        if stripped.startswith("#endif"):
            if editor_guard_stack:
                editor_guard_stack.pop()
            continue
        if is_comment_line(line):
            continue

        inside_editor_guard = any(editor_guard_stack)
        method_match = METHOD_RE.match(line)
        matched_method = method_match.group("name") if method_match else None

        for pattern, code, message in UNSAFE_RISK_PATTERNS:
            if pattern.search(line):
                findings.append(Finding(path, line_number, code, message))

        if PUBLIC_UNSAFE_RE.search(line):
            findings.append(
                Finding(
                    path,
                    line_number,
                    "public-unsafe-surface",
                    "Public API must not expose unsafe methods or pointer ownership.",
                )
            )

        for pattern, code, message in OBSOLETE_UNITY_API_PATTERNS:
            if pattern.search(line):
                findings.append(Finding(path, line_number, code, message))

        if LINQ_USING_RE.search(line) and runtime_policy_path:
            findings.append(
                Finding(
                    path,
                    line_number,
                    "runtime-linq-namespace",
                    "System.Linq in first-party runtime code requires a low-frequency boundary or review.",
                )
            )

        if UNITY_EDITOR_RE.search(line) and runtime_policy_path and not inside_editor_guard:
            findings.append(
                Finding(
                    path,
                    line_number,
                    "runtime-unityeditor-reference",
                    "Runtime-facing files must not reference UnityEditor.",
                )
            )

        unity_time_boundary = unity_behaviour_file and (
            hot_method in UNITY_TIME_BOUNDARY_METHODS
            or matched_method in UNITY_TIME_BOUNDARY_METHODS
        )
        explicit_time_boundary = TIME_BOUNDARY_MARKER_RE.search(previous_text(lines, index, count=3))
        if (
            TIME_DELTA_RE.search(line)
            and runtime_policy_path
            and not unity_time_boundary
            and not explicit_time_boundary
        ):
            findings.append(
                Finding(
                    path,
                    line_number,
                    "framework-time-read",
                    "Framework code should receive time from its caller instead of reading UnityEngine.Time.",
                )
            )

        if runtime_policy_path and method_match and method_match.group("name") in HOT_METHOD_NAMES:
            method_name = method_match.group("name")
            if re.search(r"\bpublic\b", line) and not ALLOC_MARKER_RE.search(previous_text(lines, index)):
                findings.append(
                    Finding(
                        path,
                        line_number,
                        "public-hot-api-missing-alloc-doc",
                        f"Public hot-path method '{method_name}' should document NoAlloc/LowFreqAlloc/AllocByDesign.",
                    )
                )

            delta = brace_delta(line)
            if "{" in line:
                hot_method = method_name
                hot_braces = delta
                started_hot_method = True
            else:
                pending_hot_method = None if ";" in line else method_name

        elif pending_hot_method is not None and "{" in line:
            hot_method = pending_hot_method
            pending_hot_method = None
            hot_braces = brace_delta(line)
            started_hot_method = True
        elif pending_hot_method is not None and ";" in line:
            pending_hot_method = None

        if hot_method is not None:
            for pattern, code, message in HOT_PATH_RISK_PATTERNS:
                if pattern.search(line):
                    if code == "hot-exception" and line.lstrip().startswith("throw new "):
                        continue
                    findings.append(Finding(path, line_number, code, f"{message} Method: {hot_method}."))

            if not started_hot_method:
                hot_braces += brace_delta(line)
            if hot_braces <= 0:
                hot_method = None
                hot_braces = 0

    return findings


def scan_file(repo_root: pathlib.Path, path: pathlib.Path) -> list[Finding]:
    if path.suffix == ".asmdef":
        return scan_asmdef(repo_root, path)
    if path.suffix == ".cs":
        return scan_cs_file(repo_root, path)
    return []


def print_report(findings: list[Finding], files_scanned: int, max_findings: int, max_files: int, summary_only: bool) -> None:
    print(f"Code standards report: scanned {files_scanned} first-party code file(s).")
    if not findings:
        print("Code standards report: no findings.")
        return

    counts: dict[str, int] = {}
    file_counts: dict[pathlib.Path, int] = {}
    for finding in findings:
        counts[finding.code] = counts.get(finding.code, 0) + 1
        file_counts[finding.path] = file_counts.get(finding.path, 0) + 1

    print(f"Code standards report: {len(findings)} finding(s), report-only.")
    for code, count in sorted(counts.items()):
        print(f"  {code}: {count}")

    print("Code standards report: top files by finding count:")
    for path, count in sorted(file_counts.items(), key=lambda item: (-item[1], item[0].as_posix()))[:max_files]:
        print(f"  {path}: {count}")

    if summary_only:
        return

    print("Code standards report: sample findings:")
    for finding in findings[:max_findings]:
        print(f"  {finding.path}:{finding.line}: {finding.code}: {finding.message}")

    hidden = len(findings) - max_findings
    if hidden > 0:
        print(f"  ... {hidden} more finding(s) omitted; raise CODE_STANDARDS_MAX_FINDINGS to inspect more.")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("changed_paths_file", nargs="?", help="newline-delimited changed paths file")
    parser.add_argument("--all", action="store_true", help="scan all first-party code files")
    parser.add_argument(
        "--fail-on-findings",
        action="store_true",
        default=os.environ.get("CODE_STANDARDS_FAIL_ON_FINDINGS") == "1",
        help="return non-zero when findings are present",
    )
    parser.add_argument(
        "--max-findings",
        type=int,
        default=int(os.environ.get("CODE_STANDARDS_MAX_FINDINGS", "80")),
        help="maximum sample findings to print",
    )
    parser.add_argument(
        "--max-files",
        type=int,
        default=int(os.environ.get("CODE_STANDARDS_MAX_FILES", "10")),
        help="maximum top finding files to print",
    )
    parser.add_argument(
        "--summary-only",
        action="store_true",
        help="print counts and top files without sample findings",
    )
    args = parser.parse_args()

    if not args.all and not args.changed_paths_file:
        parser.error("changed_paths_file is required unless --all is set")

    repo_root = run_git_root()
    if args.all:
        paths = sorted(set(iter_all_first_party_code_files(repo_root)))
    else:
        paths = sorted(
            {
                path
                for path in changed_paths(pathlib.Path(args.changed_paths_file))
                if is_first_party_code_file(path) and (repo_root / path).exists()
            }
        )

    findings: list[Finding] = []
    for path in paths:
        findings.extend(scan_file(repo_root, path))

    print_report(
        findings,
        len(paths),
        max(0, args.max_findings),
        max(0, args.max_files),
        args.summary_only,
    )

    if findings and args.fail_on_findings:
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
