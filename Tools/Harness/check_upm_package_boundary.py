#!/usr/bin/env python3
"""Validate the MxFramework UPM package boundary without starting Unity."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import pathlib
import re
import shutil
import subprocess
import sys
from typing import Any


REPOSITORY_ROOT = pathlib.Path(__file__).resolve().parents[2]
DEFAULT_PACKAGE_ROOT = REPOSITORY_ROOT / "Packages/com.vincent.wgameframework"
DEFAULT_PACKAGE_NAME = "com.vincent.wgameframework"

SEMVER_RE = re.compile(
    r"^(0|[1-9]\d*)\."
    r"(0|[1-9]\d*)\."
    r"(0|[1-9]\d*)"
    r"(?:-((?:0|[1-9]\d*|\d*[A-Za-z-][0-9A-Za-z-]*)"
    r"(?:\.(?:0|[1-9]\d*|\d*[A-Za-z-][0-9A-Za-z-]*))*))?"
    r"(?:\+([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?$"
)
UNITY_VERSION_RE = re.compile(r"^\d+\.\d+$")
FULL_SHA_RE = re.compile(r"^[0-9a-fA-F]{40}$")

REQUIRED_DEPENDENCIES = (
    "com.unity.inputsystem",
    "com.unity.modules.androidjni",
    "com.unity.modules.animation",
    "com.unity.modules.assetbundle",
    "com.unity.modules.audio",
    "com.unity.modules.imageconversion",
    "com.unity.modules.imgui",
    "com.unity.modules.jsonserialize",
    "com.unity.modules.particlesystem",
    "com.unity.modules.physics",
    "com.unity.modules.uielements",
    "com.unity.modules.unitywebrequest",
    "com.unity.nuget.newtonsoft-json",
    "com.unity.render-pipelines.universal",
)

OPTIONAL_GATES = (
    {
        "name": "fmod",
        "externalReferences": ("FMODUnity", "FMODUnityEditor"),
        "define": "MXFRAMEWORK_FMOD",
        "packageName": None,
        "expression": None,
    },
    {
        "name": "fairygui",
        "externalReferences": ("FairyGUI",),
        "define": "MXFRAMEWORK_FAIRYGUI",
        "packageName": "com.fairygui.gui",
        "expression": "5.2.0",
    },
    {
        "name": "magica-cloth",
        "externalReferences": ("MagicaClothV2",),
        "define": "MAGICACLOTH2",
        "packageName": None,
        "expression": None,
    },
    {
        "name": "unity-transport",
        "externalReferences": ("Unity.Networking.Transport",),
        "define": "MXFRAMEWORK_UNITY_TRANSPORT",
        "packageName": "com.unity.transport",
        "expression": "6.5.0",
    },
    {
        "name": "vfx-graph",
        "externalReferences": (
            "Unity.VisualEffectGraph.Runtime",
            "Unity.VisualEffectGraph.Editor",
        ),
        "define": "MXFRAMEWORK_VFX_GRAPH",
        "packageName": "com.unity.visualeffectgraph",
        "expression": "17.0.0",
    },
)

UGUI_PACKAGE_NAME = "com.unity.ugui"
UGUI_DEFINE = "MXFRAMEWORK_UGUI"
UGUI_MINIMUM_EXPRESSION = "2.0.0"
UGUI_PROVIDER_GATES = ("fairygui",)
UGUI_USING_RE = re.compile(
    r"^\s*using\s+"
    r"(?:(?:static\s+)|(?:[A-Za-z_][A-Za-z0-9_]*\s*=\s*))?"
    r"(?:global::)?UnityEngine\."
    r"(?:EventSystems(?:\.[A-Za-z_][A-Za-z0-9_.]*)?"
    r"|UI(?:\.[A-Za-z_][A-Za-z0-9_.]*)?)\s*;",
    re.MULTILINE,
)

FORBIDDEN_DIRECTORY_NAMES = {
    ".git",
    ".idea",
    ".svn",
    ".vs",
    ".vscode",
    "library",
    "logs",
    "obj",
    "temp",
    "usersettings",
}
FORBIDDEN_FILE_NAMES = {
    ".ds_store",
    "desktop.ini",
    "thumbs.db",
}
FORBIDDEN_FILE_SUFFIXES = {
    ".bak",
    ".csproj",
    ".sln",
    ".suo",
    ".swp",
    ".tmp",
    ".user",
}

SURFACE_OWNERS = {
    "base-runtime",
    "portable-editor",
    "optional-adapter",
    "package-test",
    "package-sample",
    "repository-validation",
}
PATH_CLASSIFICATIONS = {
    "portable-no-input",
    "consumer-output",
    "optional-project-input",
    "sample-input",
    "repository-input",
    "test-fixture",
    "generated-authority",
}
OWNER_DEPENDENCY_RULES = {
    "base-runtime": {"base-runtime"},
    "portable-editor": {"base-runtime", "portable-editor"},
    "optional-adapter": {
        "base-runtime",
        "portable-editor",
        "optional-adapter",
    },
    "package-test": {
        "base-runtime",
        "portable-editor",
        "optional-adapter",
        "package-test",
        "package-sample",
    },
    "package-sample": {
        "base-runtime",
        "optional-adapter",
        "package-sample",
    },
    "repository-validation": set(SURFACE_OWNERS),
}
INITIALIZER_ATTRIBUTE_RE = re.compile(
    r"\[\s*(?:(?:UnityEditor|UnityEngine)\.)?"
    r"(?:InitializeOnLoad|InitializeOnLoadMethod|DidReloadScripts|RuntimeInitializeOnLoadMethod)"
    r"(?:Attribute)?(?:\s*\([^\]]*\))?\s*\]",
    re.MULTILINE,
)
INITIALIZER_BASE_TYPE_RE = re.compile(
    r":\s*(?:UnityEditor\.)?(?:AssetPostprocessor|AssetModificationProcessor)\b"
)


class Audit:
    def __init__(self) -> None:
        self.failures: list[dict[str, str]] = []
        self.readiness_gaps: list[dict[str, str]] = []

    def fail(self, code: str, path: str, message: str) -> None:
        self.failures.append({"code": code, "path": path, "message": message})

    def gap(
        self,
        code: str,
        path: str,
        message: str,
        *,
        required: bool = False,
    ) -> None:
        if required:
            self.fail(code, path, message)
        else:
            self.readiness_gaps.append(
                {"code": code, "path": path, "message": message}
            )


def relative_path(package_root: pathlib.Path, path: pathlib.Path) -> str:
    try:
        return path.relative_to(package_root).as_posix()
    except ValueError:
        return path.as_posix()


def discover_package_files(
    package_root: pathlib.Path, audit: Audit
) -> tuple[list[pathlib.Path], str]:
    """Return deliverable worktree files, or all physical files outside Git."""

    try:
        physical_files = sorted(
            (
                path
                for path in package_root.rglob("*")
                if path.is_file() or path.is_symlink()
            ),
            key=lambda item: item.as_posix(),
        )
    except OSError as error:
        audit.fail("package-tree-read", ".", f"cannot enumerate package tree: {error}")
        return [], "physical"

    git = shutil.which("git")
    if git is None:
        return physical_files, "physical"

    repository_result = subprocess.run(
        [git, "-C", str(package_root), "rev-parse", "--show-toplevel"],
        stdout=subprocess.PIPE,
        stderr=subprocess.DEVNULL,
        check=False,
    )
    if repository_result.returncode != 0:
        return physical_files, "physical"

    try:
        repository_root = pathlib.Path(
            repository_result.stdout.decode("utf-8", errors="surrogateescape").strip()
        ).resolve()
        package_relative = package_root.relative_to(repository_root)
    except (OSError, ValueError):
        return physical_files, "physical"

    ignored_result = subprocess.run(
        [
            git,
            "-C",
            str(repository_root),
            "check-ignore",
            "-q",
            "--",
            package_relative.as_posix(),
        ],
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
        check=False,
    )
    if ignored_result.returncode == 0:
        # A resolved PackageCache can live below an ignored Consumer Library. In
        # that case its physical tree, not the parent repository index, is the
        # package being audited.
        return physical_files, "physical"
    if ignored_result.returncode not in (1,):
        return physical_files, "physical"

    tracked_result = subprocess.run(
        [
            git,
            "-C",
            str(repository_root),
            "ls-files",
            "--cached",
            "--others",
            "--exclude-standard",
            "-z",
            "--",
            package_relative.as_posix(),
        ],
        stdout=subprocess.PIPE,
        stderr=subprocess.DEVNULL,
        check=False,
    )
    if tracked_result.returncode != 0:
        return physical_files, "physical"

    repository_paths = [
        pathlib.Path(raw_path.decode("utf-8", errors="surrogateescape"))
        for raw_path in tracked_result.stdout.split(b"\0")
        if raw_path
    ]
    deliverable_files = sorted(
        (
            repository_root / path
            for path in repository_paths
            if (repository_root / path).is_file()
            or (repository_root / path).is_symlink()
        ),
        key=lambda item: item.as_posix(),
    )
    if not deliverable_files and physical_files:
        return physical_files, "physical"
    return deliverable_files, "git-deliverable"


def read_json(
    path: pathlib.Path,
    package_root: pathlib.Path,
    audit: Audit,
    code: str,
) -> Any | None:
    display_path = relative_path(package_root, path)
    try:
        text = path.read_text(encoding="utf-8-sig")
    except (OSError, UnicodeError) as error:
        audit.fail(code, display_path, f"cannot read UTF-8 JSON: {error}")
        return None

    try:
        return json.loads(text)
    except json.JSONDecodeError as error:
        audit.fail(
            code,
            display_path,
            f"invalid JSON at line {error.lineno}, column {error.colno}: {error.msg}",
        )
        return None


def validate_string_list(
    definition: dict[str, Any],
    field: str,
    path: str,
    audit: Audit,
) -> list[str]:
    value = definition.get(field, [])
    if value is None:
        return []
    if not isinstance(value, list) or not all(isinstance(item, str) for item in value):
        audit.fail(
            "asmdef-field-type",
            path,
            f"'{field}' must be an array of strings",
        )
        return []
    return value


def validate_manifest(
    package_root: pathlib.Path,
    expected_package_name: str,
    audit: Audit,
) -> tuple[dict[str, Any], dict[str, str]]:
    manifest_path = package_root / "package.json"
    if not manifest_path.is_file():
        audit.fail("manifest-missing", "package.json", "package manifest is missing")
        return {}, {}

    manifest = read_json(manifest_path, package_root, audit, "manifest-json")
    if manifest is None:
        return {}, {}
    if not isinstance(manifest, dict):
        audit.fail("manifest-type", "package.json", "package manifest must be an object")
        return {}, {}

    name = manifest.get("name")
    if name != expected_package_name:
        audit.fail(
            "package-name",
            "package.json",
            f"package name is {name!r}; expected {expected_package_name!r}",
        )

    version = manifest.get("version")
    if not isinstance(version, str) or not SEMVER_RE.fullmatch(version):
        audit.fail(
            "package-version",
            "package.json",
            f"package version is not valid SemVer: {version!r}",
        )

    unity_version = manifest.get("unity")
    if not isinstance(unity_version, str) or not UNITY_VERSION_RE.fullmatch(
        unity_version
    ):
        audit.fail(
            "unity-version",
            "package.json",
            f"'unity' must use major.minor form: {unity_version!r}",
        )

    for field in ("displayName", "description"):
        value = manifest.get(field)
        if not isinstance(value, str) or not value.strip():
            audit.fail(
                "manifest-required-field",
                "package.json",
                f"'{field}' must be a non-empty string",
            )

    for field in ("repository", "author"):
        value = manifest.get(field)
        if not isinstance(value, dict) or not value:
            audit.fail(
                "manifest-required-field",
                "package.json",
                f"'{field}' must be a non-empty object",
            )

    raw_dependencies = manifest.get("dependencies")
    if not isinstance(raw_dependencies, dict):
        audit.fail(
            "dependency-type",
            "package.json",
            "'dependencies' must be an object",
        )
        return manifest, {}

    dependencies: dict[str, str] = {}
    for dependency_name, dependency_version in raw_dependencies.items():
        if not isinstance(dependency_name, str) or not isinstance(
            dependency_version, str
        ):
            audit.fail(
                "dependency-entry-type",
                "package.json",
                "dependency names and versions must be strings",
            )
            continue
        dependencies[dependency_name] = dependency_version
        if not SEMVER_RE.fullmatch(dependency_version):
            audit.fail(
                "dependency-version",
                "package.json",
                f"dependency '{dependency_name}' must use an exact SemVer; got {dependency_version!r}",
            )

    expected = set(REQUIRED_DEPENDENCIES)
    declared = set(dependencies)
    missing = sorted(expected - declared)
    unexpected = sorted(declared - expected)
    if missing or unexpected:
        audit.fail(
            "dependency-baseline",
            "package.json",
            "mandatory dependency set mismatch; "
            f"missing={missing}, unexpected={unexpected}",
        )

    return manifest, dependencies


def load_assembly_definitions(
    package_root: pathlib.Path, package_files: list[pathlib.Path], audit: Audit
) -> dict[str, dict[str, Any]]:
    definitions: dict[str, dict[str, Any]] = {}
    name_paths: dict[str, str] = {}
    asmdef_paths = [path for path in package_files if path.suffix == ".asmdef"]
    if not asmdef_paths:
        audit.fail("asmdef-missing", ".", "package does not contain any asmdef files")

    for asmdef_path in asmdef_paths:
        path = relative_path(package_root, asmdef_path)
        raw = read_json(asmdef_path, package_root, audit, "asmdef-json")
        if raw is None:
            continue
        if not isinstance(raw, dict):
            audit.fail("asmdef-type", path, "asmdef must be a JSON object")
            continue

        name = raw.get("name")
        if not isinstance(name, str) or not name.strip():
            audit.fail("asmdef-name", path, "asmdef name must be a non-empty string")
            continue
        if name in definitions:
            audit.fail(
                "asmdef-name-duplicate",
                path,
                f"assembly name '{name}' is also declared by {name_paths[name]}",
            )
            continue

        references = validate_string_list(raw, "references", path, audit)
        guid_references = sorted(
            reference for reference in references if reference.startswith("GUID:")
        )
        if guid_references:
            audit.fail(
                "asmdef-guid-reference",
                path,
                "GUID-based references are not allowed by boundary policy v1 because "
                f"they can bypass named optional reverse-closure analysis: {guid_references}",
            )
        constraints = validate_string_list(raw, "defineConstraints", path, audit)
        raw_version_defines = raw.get("versionDefines", [])
        version_defines: list[dict[str, str]] = []
        if raw_version_defines is None:
            raw_version_defines = []
        if not isinstance(raw_version_defines, list):
            audit.fail(
                "asmdef-field-type",
                path,
                "'versionDefines' must be an array",
            )
        else:
            for index, version_define in enumerate(raw_version_defines):
                if not isinstance(version_define, dict):
                    audit.fail(
                        "version-define-type",
                        path,
                        f"versionDefines[{index}] must be an object",
                    )
                    continue
                values = {
                    key: version_define.get(key) for key in ("name", "expression", "define")
                }
                if not all(
                    isinstance(value, str) and value.strip()
                    for value in values.values()
                ):
                    audit.fail(
                        "version-define-field",
                        path,
                        f"versionDefines[{index}] requires non-empty name/expression/define strings",
                    )
                    continue
                version_defines.append(values)  # type: ignore[arg-type]

        definitions[name] = {
            "name": name,
            "path": path,
            "references": references,
            "defineConstraints": constraints,
            "versionDefines": version_defines,
        }
        name_paths[name] = path

    return definitions


def validate_optional_gates(
    definitions: dict[str, dict[str, Any]], audit: Audit
) -> dict[str, dict[str, Any]]:
    results: dict[str, dict[str, Any]] = {}

    for profile in OPTIONAL_GATES:
        external_references = set(profile["externalReferences"])
        affected = {
            name
            for name, definition in definitions.items()
            if external_references.intersection(definition["references"])
        }
        roots = set(affected)

        changed = True
        while changed:
            changed = False
            for name, definition in definitions.items():
                if name in affected:
                    continue
                if affected.intersection(definition["references"]):
                    affected.add(name)
                    changed = True

        violations: list[dict[str, str]] = []
        profile_name = str(profile["name"])
        required_define = str(profile["define"])
        if not affected:
            message = f"optional gate '{profile_name}' has no asmdef roots"
            audit.fail("optional-gate-root", ".", message)
            violations.append({"assembly": "", "message": message})

        for assembly_name in sorted(affected):
            definition = definitions[assembly_name]
            constraint_count = definition["defineConstraints"].count(required_define)
            if constraint_count != 1:
                message = (
                    f"assembly '{assembly_name}' is in the {profile_name} reverse closure "
                    f"but has {constraint_count} '{required_define}' define constraints"
                )
                audit.fail("optional-define-constraint", definition["path"], message)
                violations.append({"assembly": assembly_name, "message": message})

            package_name = profile["packageName"]
            if package_name is None:
                continue

            candidates = [
                item
                for item in definition["versionDefines"]
                if item["name"] == package_name or item["define"] == required_define
            ]
            expected = {
                "name": package_name,
                "expression": profile["expression"],
                "define": required_define,
            }
            if len(candidates) != 1 or candidates[0] != expected:
                message = (
                    f"assembly '{assembly_name}' must contain exactly one versionDefine "
                    f"{expected}; observed {candidates}"
                )
                audit.fail("optional-version-define", definition["path"], message)
                violations.append({"assembly": assembly_name, "message": message})

        results[profile_name] = {
            "assemblyCount": len(affected),
            "assemblies": sorted(affected),
            "rootAssemblies": sorted(roots),
            "violationCount": len(violations),
            "violations": violations,
        }

    return results


def owning_assembly(
    package_root: pathlib.Path,
    source_path: pathlib.Path,
    definitions: dict[str, dict[str, Any]],
) -> tuple[str | None, list[str]]:
    candidates: list[tuple[int, str]] = []
    for assembly_name, definition in definitions.items():
        assembly_directory = (
            package_root / pathlib.PurePosixPath(definition["path"])
        ).parent
        try:
            source_path.relative_to(assembly_directory)
        except ValueError:
            continue
        candidates.append((len(assembly_directory.parts), assembly_name))

    if not candidates:
        return None, []

    nearest_depth = max(depth for depth, _ in candidates)
    nearest = sorted(name for depth, name in candidates if depth == nearest_depth)
    return (nearest[0] if len(nearest) == 1 else None), nearest


def has_explicit_ugui_gate(definition: dict[str, Any]) -> bool:
    if definition["defineConstraints"].count(UGUI_DEFINE) != 1:
        return False
    expected = {
        "name": UGUI_PACKAGE_NAME,
        "expression": UGUI_MINIMUM_EXPRESSION,
        "define": UGUI_DEFINE,
    }
    return definition["versionDefines"].count(expected) == 1


def validate_optional_source_dependencies(
    package_root: pathlib.Path,
    package_files: list[pathlib.Path],
    dependencies: dict[str, str],
    definitions: dict[str, dict[str, Any]],
    optional_gates: dict[str, dict[str, Any]],
    audit: Audit,
) -> dict[str, Any]:
    mandatory_ugui = UGUI_PACKAGE_NAME in dependencies
    provider_assemblies: set[str] = set()
    for gate_name in UGUI_PROVIDER_GATES:
        gate = optional_gates.get(gate_name)
        if gate is not None:
            provider_assemblies.update(gate["assemblies"])

    references: list[dict[str, str]] = []
    violations: list[dict[str, str]] = []
    for source_path in (path for path in package_files if path.suffix == ".cs"):
        display_path = relative_path(package_root, source_path)
        try:
            payload = source_path.read_bytes()
        except OSError as error:
            audit.fail(
                "source-read",
                display_path,
                f"cannot scan optional source dependencies: {error}",
            )
            continue
        try:
            source = payload.decode("utf-8-sig")
        except UnicodeDecodeError:
            source = payload.decode("latin-1")
        if UGUI_USING_RE.search(source) is None:
            continue

        assembly_name, nearest = owning_assembly(
            package_root, source_path, definitions
        )
        if assembly_name is None:
            message = (
                "cannot resolve one owning asmdef for direct uGUI source reference; "
                f"nearest candidates={nearest}"
            )
            audit.fail("optional-source-assembly", display_path, message)
            violations.append(
                {"path": display_path, "assembly": "", "message": message}
            )
            continue

        definition = definitions[assembly_name]
        coverage = ""
        if mandatory_ugui:
            coverage = "mandatory-dependency"
        elif assembly_name in provider_assemblies:
            coverage = "fairygui-reverse-closure"
        elif has_explicit_ugui_gate(definition):
            coverage = "explicit-ugui-gate"

        references.append(
            {
                "path": display_path,
                "assembly": assembly_name,
                "coverage": coverage or "missing",
            }
        )
        if coverage:
            continue

        message = (
            f"assembly '{assembly_name}' directly imports UnityEngine.EventSystems/"
            "UnityEngine.UI but com.unity.ugui is neither mandatory nor guaranteed by "
            "the FairyGUI reverse closure or an explicit MXFRAMEWORK_UGUI assembly gate"
        )
        audit.fail("optional-source-ugui", display_path, message)
        violations.append(
            {"path": display_path, "assembly": assembly_name, "message": message}
        )

    references.sort(key=lambda item: (item["path"], item["assembly"]))
    violations.sort(key=lambda item: (item["path"], item["assembly"], item["message"]))
    return {
        "ugui": {
            "packageName": UGUI_PACKAGE_NAME,
            "mandatory": mandatory_ugui,
            "directReferenceFileCount": len(references),
            "directReferences": references,
            "violationCount": len(violations),
            "violations": violations,
        }
    }


def check_forbidden_files(
    package_root: pathlib.Path,
    package_files: list[pathlib.Path],
    audit: Audit,
) -> list[str]:
    forbidden_paths: set[str] = set()
    for path in package_files:
        relative = path.relative_to(package_root)
        display_path = relative.as_posix()
        lowered_parts = [part.casefold() for part in relative.parts]
        if any(part in FORBIDDEN_DIRECTORY_NAMES for part in lowered_parts[:-1]):
            forbidden_paths.add(display_path)
            continue

        lowered_name = path.name.casefold()
        if lowered_name in FORBIDDEN_FILE_NAMES:
            forbidden_paths.add(display_path)
            continue
        if pathlib.PurePath(lowered_name).suffix in FORBIDDEN_FILE_SUFFIXES:
            forbidden_paths.add(display_path)
            continue
        if lowered_name.endswith("~"):
            forbidden_paths.add(display_path)
            continue
        if path.is_symlink():
            forbidden_paths.add(display_path)

    for path in sorted(forbidden_paths):
        audit.fail(
            "forbidden-package-path",
            path,
            "package contains generated, personal, backup, or symlink content",
        )
    return sorted(forbidden_paths)


def package_tree_hash(
    package_root: pathlib.Path,
    package_files: list[pathlib.Path],
    audit: Audit,
) -> tuple[str | None, int]:
    digest = hashlib.sha256()
    file_count = 0
    for path in package_files:
        relative = path.relative_to(package_root).as_posix()
        try:
            if path.is_symlink():
                payload = os.readlink(path).encode("utf-8")
            else:
                payload = path.read_bytes()
        except OSError as error:
            audit.fail("package-tree-read", relative, f"cannot hash file: {error}")
            continue
        digest.update(relative.encode("utf-8"))
        digest.update(b"\0")
        digest.update(payload)
        digest.update(b"\0")
        file_count += 1
    return (digest.hexdigest() if file_count else None), file_count


def detect_editor_initializers(
    package_root: pathlib.Path,
    package_files: list[pathlib.Path],
    audit: Audit,
) -> list[str]:
    detected: list[str] = []
    for path in (item for item in package_files if item.suffix == ".cs"):
        if "Tests" in path.relative_to(package_root).parts:
            continue
        try:
            payload = path.read_bytes()
        except OSError as error:
            audit.fail(
                "source-read",
                relative_path(package_root, path),
                f"cannot scan C# initializer: {error}",
            )
            continue
        try:
            text = payload.decode("utf-8-sig")
        except UnicodeDecodeError:
            # Initializer tokens are ASCII. Latin-1 preserves those bytes and lets
            # the boundary scan remain independent of legacy source-file encodings.
            text = payload.decode("latin-1")
        if INITIALIZER_ATTRIBUTE_RE.search(text) or INITIALIZER_BASE_TYPE_RE.search(text):
            detected.append(relative_path(package_root, path))
    return detected


def validate_surface_manifest(
    package_root: pathlib.Path,
    definitions: dict[str, dict[str, Any]],
    optional_gates: dict[str, dict[str, Any]],
    editor_initializers: list[str],
    audit: Audit,
    require_surface_manifest: bool,
) -> dict[str, Any]:
    surface_path = package_root / "upm-surface.json"
    if not surface_path.is_file():
        audit.gap(
            "surface-manifest-missing",
            "upm-surface.json",
            f"surface ownership is not declared for {len(definitions)} assemblies and "
            f"{len(editor_initializers)} Editor initializer files",
            required=require_surface_manifest,
        )
        return {
            "manifestPresent": False,
            "classifiedAssemblyCount": 0,
            "unclassifiedAssemblies": sorted(definitions),
            "classifiedEditorInitializerCount": 0,
            "unclassifiedEditorInitializers": editor_initializers,
            "ownerCounts": {},
            "dependencyViolationCount": 0,
            "dependencyViolations": [],
            "optionalOwnerViolationCount": 0,
            "optionalOwnerViolations": [],
        }

    raw = read_json(surface_path, package_root, audit, "surface-manifest-json")
    if not isinstance(raw, dict):
        return {
            "manifestPresent": True,
            "classifiedAssemblyCount": 0,
            "unclassifiedAssemblies": sorted(definitions),
            "classifiedEditorInitializerCount": 0,
            "unclassifiedEditorInitializers": editor_initializers,
            "ownerCounts": {},
            "dependencyViolationCount": 0,
            "dependencyViolations": [],
            "optionalOwnerViolationCount": 0,
            "optionalOwnerViolations": [],
        }

    if raw.get("schemaVersion") != 1:
        audit.fail(
            "surface-schema-version",
            "upm-surface.json",
            "schemaVersion must be 1",
        )

    assemblies = raw.get("assemblies")
    if not isinstance(assemblies, dict):
        audit.fail(
            "surface-assemblies-type",
            "upm-surface.json",
            "'assemblies' must be an object",
        )
        assemblies = {}

    assembly_names = set(definitions)
    classified_names = {name for name in assemblies if isinstance(name, str)}
    missing = sorted(assembly_names - classified_names)
    extra = sorted(classified_names - assembly_names)
    if missing:
        audit.fail(
            "surface-assembly-unclassified",
            "upm-surface.json",
            f"assemblies are missing surface owners: {missing}",
        )
    if extra:
        audit.fail(
            "surface-assembly-stale",
            "upm-surface.json",
            f"surface manifest contains unknown assemblies: {extra}",
        )

    owner_counts: dict[str, int] = {}
    valid_owners: dict[str, str] = {}
    for assembly_name, owner in sorted(assemblies.items()):
        if not isinstance(owner, str) or owner not in SURFACE_OWNERS:
            audit.fail(
                "surface-owner",
                "upm-surface.json",
                f"assembly '{assembly_name}' has invalid owner {owner!r}",
            )
            continue
        if assembly_name in definitions:
            valid_owners[assembly_name] = owner
        owner_counts[owner] = owner_counts.get(owner, 0) + 1

    dependency_violations: list[dict[str, str]] = []
    for assembly_name, owner in sorted(valid_owners.items()):
        definition = definitions[assembly_name]
        allowed_dependencies = OWNER_DEPENDENCY_RULES[owner]
        for reference in definition["references"]:
            target_owner = valid_owners.get(reference)
            if target_owner is None or target_owner in allowed_dependencies:
                continue
            message = (
                f"assembly '{assembly_name}' owned by '{owner}' cannot reference "
                f"assembly '{reference}' owned by '{target_owner}'"
            )
            audit.fail("surface-owner-dependency", definition["path"], message)
            dependency_violations.append(
                {
                    "assembly": assembly_name,
                    "owner": owner,
                    "reference": reference,
                    "referenceOwner": target_owner,
                    "message": message,
                }
            )

    known_optional_assemblies = {
        assembly_name
        for gate in optional_gates.values()
        for assembly_name in gate.get("assemblies", [])
    }
    optional_owner_violations: list[dict[str, str]] = []
    optional_compatible_owners = {
        "optional-adapter",
        "package-test",
        "package-sample",
        "repository-validation",
    }
    for assembly_name in sorted(known_optional_assemblies):
        owner = valid_owners.get(assembly_name)
        if owner is None or owner in optional_compatible_owners:
            continue
        definition = definitions[assembly_name]
        message = (
            f"assembly '{assembly_name}' is in a known optional reverse closure "
            f"and cannot use owner '{owner}'"
        )
        audit.fail("surface-optional-owner", definition["path"], message)
        optional_owner_violations.append(
            {"assembly": assembly_name, "owner": owner, "message": message}
        )

    for assembly_name, owner in sorted(valid_owners.items()):
        if owner != "optional-adapter" or assembly_name in known_optional_assemblies:
            continue
        definition = definitions[assembly_name]
        constraints = [
            item
            for item in definition["defineConstraints"]
            if item and not item.startswith("UNITY_")
        ]
        if constraints or definition["versionDefines"]:
            continue
        message = (
            f"optional-adapter assembly '{assembly_name}' has no optional define "
            "constraint or version define"
        )
        audit.fail("surface-optional-adapter-ungated", definition["path"], message)
        optional_owner_violations.append(
            {"assembly": assembly_name, "owner": owner, "message": message}
        )

    initializers = raw.get("editorInitializers")
    if not isinstance(initializers, dict):
        audit.gap(
            "surface-initializers-missing",
            "upm-surface.json",
            "'editorInitializers' classifications are missing",
            required=require_surface_manifest,
        )
        initializers = {}

    detected_set = set(editor_initializers)
    classified_initializers = {name for name in initializers if isinstance(name, str)}
    missing_initializers = sorted(detected_set - classified_initializers)
    stale_initializers = sorted(classified_initializers - detected_set)
    if missing_initializers:
        audit.gap(
            "surface-initializer-unclassified",
            "upm-surface.json",
            f"Editor initializer files are unclassified: {missing_initializers}",
            required=require_surface_manifest,
        )
    if stale_initializers:
        audit.fail(
            "surface-initializer-stale",
            "upm-surface.json",
            f"initializer classifications do not match detected files: {stale_initializers}",
        )

    for initializer_path, classification in sorted(initializers.items()):
        if not isinstance(classification, dict):
            audit.fail(
                "surface-initializer-type",
                "upm-surface.json",
                f"initializer '{initializer_path}' classification must be an object",
            )
            continue
        path_classification = classification.get("classification")
        if path_classification not in PATH_CLASSIFICATIONS:
            audit.fail(
                "surface-initializer-classification",
                "upm-surface.json",
                f"initializer '{initializer_path}' has invalid classification "
                f"{path_classification!r}",
            )
        for field in ("activation", "requiredInputs", "missingInputBehavior", "writes"):
            value = classification.get(field)
            valid = isinstance(value, str) and bool(value.strip())
            if field == "activation" and isinstance(value, list):
                valid = bool(value) and all(
                    isinstance(item, str) and bool(item.strip()) for item in value
                )
            if not valid:
                audit.fail(
                    "surface-initializer-field",
                    "upm-surface.json",
                    f"initializer '{initializer_path}' requires a non-empty '{field}'",
                )

    return {
        "manifestPresent": True,
        "classifiedAssemblyCount": len(assembly_names - set(missing)),
        "unclassifiedAssemblies": missing,
        "classifiedEditorInitializerCount": len(detected_set - set(missing_initializers)),
        "unclassifiedEditorInitializers": missing_initializers,
        "ownerCounts": dict(sorted(owner_counts.items())),
        "dependencyViolationCount": len(dependency_violations),
        "dependencyViolations": dependency_violations,
        "optionalOwnerViolationCount": len(optional_owner_violations),
        "optionalOwnerViolations": optional_owner_violations,
    }


def root_file(package_root: pathlib.Path, names: set[str]) -> pathlib.Path | None:
    for child in package_root.iterdir():
        if child.is_file() and child.name.casefold() in names:
            return child
    return None


def release_readiness(
    package_root: pathlib.Path,
    manifest: dict[str, Any],
    audit: Audit,
    require_release_metadata: bool,
) -> dict[str, Any]:
    readme = root_file(package_root, {"readme", "readme.md"})
    changelog = root_file(package_root, {"changelog", "changelog.md"})
    license_file = root_file(
        package_root,
        {"license", "license.md", "license.txt", "licence", "licence.md"},
    )
    third_party = root_file(
        package_root,
        {
            "third party notices.md",
            "third-party notices.md",
            "third party notices.txt",
            "third-party notices.txt",
            "thirdpartynotices.md",
            "thirdpartynotices.txt",
        },
    )

    if readme is None:
        audit.fail("package-readme-missing", "README.md", "package README is missing")
    if changelog is None:
        audit.fail(
            "package-changelog-missing",
            "CHANGELOG.md",
            "package changelog is missing",
        )

    if license_file is None:
        audit.gap(
            "package-license-missing",
            "LICENSE.md",
            "package license file is missing",
            required=require_release_metadata,
        )
    if third_party is None:
        audit.gap(
            "third-party-notices-missing",
            "Third Party Notices.md",
            "third-party notices file is missing",
            required=require_release_metadata,
        )

    manifest_license = manifest.get("license") or manifest.get("licensesUrl")
    if not isinstance(manifest_license, str) or not manifest_license.strip():
        audit.gap(
            "manifest-license-missing",
            "package.json",
            "manifest requires 'license' or 'licensesUrl' for release",
            required=require_release_metadata,
        )

    documentation_present = (
        (package_root / "Documentation~").is_dir()
        or isinstance(manifest.get("documentationUrl"), str)
        and bool(manifest["documentationUrl"].strip())
    )
    if not documentation_present:
        audit.gap(
            "package-documentation-missing",
            "Documentation~",
            "package has neither Documentation~ nor documentationUrl",
            required=require_release_metadata,
        )

    keywords = manifest.get("keywords")
    keywords_present = (
        isinstance(keywords, list)
        and bool(keywords)
        and all(isinstance(item, str) and item.strip() for item in keywords)
    )
    if not keywords_present:
        audit.gap(
            "manifest-keywords-missing",
            "package.json",
            "manifest release keywords are missing",
            required=require_release_metadata,
        )

    category_present = isinstance(manifest.get("category"), str) and bool(
        manifest["category"].strip()
    )
    if not category_present:
        audit.gap(
            "manifest-category-missing",
            "package.json",
            "manifest release category is missing",
            required=require_release_metadata,
        )

    samples = manifest.get("samples")
    samples_declared = isinstance(samples, list)
    if not samples_declared:
        audit.gap(
            "manifest-samples-missing",
            "package.json",
            "manifest should declare the approved samples list, including an empty list",
            required=require_release_metadata,
        )
    else:
        for index, sample in enumerate(samples):
            if not isinstance(sample, dict):
                audit.fail(
                    "manifest-sample-type",
                    "package.json",
                    f"samples[{index}] must be an object",
                )
                continue
            sample_path = sample.get("path")
            if not isinstance(sample_path, str) or not sample_path.strip():
                audit.fail(
                    "manifest-sample-path",
                    "package.json",
                    f"samples[{index}] requires a non-empty path",
                )
                continue
            if not (package_root / sample_path).is_dir():
                audit.fail(
                    "manifest-sample-path",
                    "package.json",
                    f"samples[{index}] path does not exist: {sample_path}",
                )

    version = manifest.get("version")
    changelog_has_version = False
    if changelog is not None and isinstance(version, str):
        try:
            changelog_text = changelog.read_text(encoding="utf-8-sig")
        except (OSError, UnicodeError) as error:
            audit.fail(
                "package-changelog-read",
                changelog.name,
                f"cannot read changelog: {error}",
            )
        else:
            changelog_has_version = bool(
                re.search(
                    rf"^##\s+\[{re.escape(version)}\](?:\s+-.*)?$",
                    changelog_text,
                    re.MULTILINE,
                )
            )
            if not changelog_has_version:
                audit.gap(
                    "changelog-version-missing",
                    changelog.name,
                    f"changelog has no section for package version {version}",
                    required=require_release_metadata,
                )

    return {
        "readmePresent": readme is not None,
        "changelogPresent": changelog is not None,
        "changelogHasPackageVersion": changelog_has_version,
        "licenseFilePresent": license_file is not None,
        "thirdPartyNoticesPresent": third_party is not None,
        "manifestLicensePresent": bool(manifest_license),
        "documentationPresent": documentation_present,
        "keywordsPresent": keywords_present,
        "categoryPresent": category_present,
        "samplesDeclared": samples_declared,
        "samplesDirectoryPresent": (package_root / "Samples~").is_dir(),
        "testsEditorDirectoryPresent": (package_root / "Tests/Editor").is_dir(),
        "testsRuntimeDirectoryPresent": (package_root / "Tests/Runtime").is_dir(),
    }


def build_report(args: argparse.Namespace) -> tuple[dict[str, Any], int]:
    package_root = args.package_root.resolve()
    audit = Audit()

    if not package_root.is_dir():
        audit.fail(
            "package-root-missing",
            ".",
            "package root does not exist or is not a directory",
        )
        report = {
            "schemaVersion": 1,
            "policy": {"name": "mxframework-upm-boundary", "version": 3},
            "candidateCommit": args.candidate_commit,
            "result": {
                "passed": False,
                "blockingFailureCount": len(audit.failures),
                "readinessGapCount": 0,
            },
            "failures": audit.failures,
            "readinessGaps": [],
        }
        return report, 1

    package_files, inventory_source = discover_package_files(package_root, audit)
    manifest, dependencies = validate_manifest(
        package_root, args.expected_package_name, audit
    )
    definitions = load_assembly_definitions(package_root, package_files, audit)
    optional_gates = validate_optional_gates(definitions, audit)
    source_dependencies = validate_optional_source_dependencies(
        package_root,
        package_files,
        dependencies,
        definitions,
        optional_gates,
        audit,
    )
    forbidden_paths = check_forbidden_files(package_root, package_files, audit)
    editor_initializers = detect_editor_initializers(
        package_root, package_files, audit
    )
    surface = validate_surface_manifest(
        package_root,
        definitions,
        optional_gates,
        editor_initializers,
        audit,
        args.require_surface_manifest,
    )
    release = release_readiness(
        package_root, manifest, audit, args.require_release_metadata
    )
    tree_hash, file_count = package_tree_hash(package_root, package_files, audit)

    audit.failures.sort(key=lambda item: (item["code"], item["path"], item["message"]))
    audit.readiness_gaps.sort(
        key=lambda item: (item["code"], item["path"], item["message"])
    )
    passed = not audit.failures
    report = {
        "schemaVersion": 1,
        "policy": {
            "name": "mxframework-upm-boundary",
            "version": 3,
            "forbiddenPathRuleVersion": 1,
            "releaseMetadataRequired": args.require_release_metadata,
            "surfaceManifestRequired": args.require_surface_manifest,
        },
        "candidateCommit": args.candidate_commit.lower()
        if args.candidate_commit
        else None,
        "package": {
            "name": manifest.get("name"),
            "version": manifest.get("version"),
            "unity": manifest.get("unity"),
            "dependencies": dict(sorted(dependencies.items())),
            "declaredDependencyCount": len(dependencies),
            "treeHashAlgorithm": "sha256-path-nul-content-nul-v1",
            "treeHash": tree_hash,
            "fileCount": file_count,
            "inventorySource": inventory_source,
        },
        "assemblies": {
            "count": len(definitions),
            "optionalGates": optional_gates,
        },
        "sourceDependencies": source_dependencies,
        "packageHygiene": {
            "forbiddenPathCount": len(forbidden_paths),
            "forbiddenPaths": forbidden_paths,
        },
        "surface": surface,
        "releaseReadiness": release,
        "result": {
            "passed": passed,
            "blockingFailureCount": len(audit.failures),
            "readinessGapCount": len(audit.readiness_gaps),
            "unityImportEvaluated": False,
        },
        "failures": audit.failures,
        "readinessGaps": audit.readiness_gaps,
    }
    return report, 0 if passed else 1


def write_report(path: pathlib.Path, report: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(report, ensure_ascii=False, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )


def print_summary(report: dict[str, Any]) -> None:
    result = report["result"]
    package = report.get("package", {})
    assemblies = report.get("assemblies", {})
    status = "passed" if result["passed"] else "failed"
    print(
        "UPM package boundary "
        f"{status}: {package.get('name')} {package.get('version')}, "
        f"{package.get('declaredDependencyCount', 0)} dependencies, "
        f"{assemblies.get('count', 0)} asmdefs, "
        f"{result['blockingFailureCount']} blocking failures, "
        f"{result['readinessGapCount']} readiness gaps."
    )

    optional_gates = assemblies.get("optionalGates", {})
    if optional_gates:
        gate_summary = ", ".join(
            f"{name}={gate['assemblyCount']}/{gate['violationCount']}"
            for name, gate in sorted(optional_gates.items())
        )
        print(f"Optional reverse closures (assemblies/violations): {gate_summary}")

    source_dependencies = report.get("sourceDependencies", {})
    ugui = source_dependencies.get("ugui", {})
    if ugui:
        print(
            "Optional source boundary (references/violations): "
            f"ugui={ugui.get('directReferenceFileCount', 0)}/"
            f"{ugui.get('violationCount', 0)}"
        )

    for failure in report.get("failures", []):
        print(
            f"FAIL [{failure['code']}] {failure['path']}: {failure['message']}",
            file=sys.stderr,
        )
    for gap in report.get("readinessGaps", []):
        print(f"GAP  [{gap['code']}] {gap['path']}: {gap['message']}")
    print("E0 is static evidence only; Unity package resolution/import was not evaluated.")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Validate the MxFramework UPM package boundary without Unity."
    )
    parser.add_argument(
        "--package-root",
        type=pathlib.Path,
        default=DEFAULT_PACKAGE_ROOT,
        help="Package root to inspect (default: Packages/com.vincent.wgameframework).",
    )
    parser.add_argument(
        "--expected-package-name",
        default=DEFAULT_PACKAGE_NAME,
        help=f"Expected package identity (default: {DEFAULT_PACKAGE_NAME}).",
    )
    parser.add_argument(
        "--candidate-commit",
        help="Optional full 40-character commit SHA recorded in the report.",
    )
    parser.add_argument(
        "--report-json",
        type=pathlib.Path,
        help="Write the deterministic JSON report outside the inspected package root.",
    )
    parser.add_argument(
        "--require-surface-manifest",
        action="store_true",
        help="Make missing surface/initializer classifications blocking (L0+ policy).",
    )
    parser.add_argument(
        "--require-release-metadata",
        action="store_true",
        help="Make release metadata readiness gaps blocking (release policy).",
    )
    args = parser.parse_args()

    if args.candidate_commit and not FULL_SHA_RE.fullmatch(args.candidate_commit):
        parser.error("--candidate-commit must be a full 40-character SHA")

    if args.report_json is not None:
        package_root = args.package_root.resolve()
        report_path = args.report_json.resolve()
        try:
            report_path.relative_to(package_root)
        except ValueError:
            pass
        else:
            parser.error("--report-json must be outside the inspected package root")

    return args


def main() -> int:
    args = parse_args()
    report, exit_code = build_report(args)
    if args.report_json is not None:
        write_report(args.report_json, report)
    print_summary(report)
    return exit_code


if __name__ == "__main__":
    sys.exit(main())
