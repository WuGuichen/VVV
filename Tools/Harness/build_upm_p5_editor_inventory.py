#!/usr/bin/env python3
"""Generate the source-level UPM-P5 Editor portability inventory."""

from __future__ import annotations

import csv
import json
import pathlib
import re


ROOT = pathlib.Path(__file__).resolve().parents[2]
PACKAGE = ROOT / "Assets/Scripts/MxFramework"
PROJECT_ROOTS = (
    ROOT / "Assets/Scripts/WGameFrameworkProject/Editor/Combat",
    ROOT / "Assets/Scripts/WGameFrameworkProject/Editor/CharacterTestVfx",
    ROOT / "Assets/Scripts/WGameFrameworkProject/Editor/FairyGUI",
    ROOT / "Assets/Scripts/WGameFrameworkProject/Demos/Editor/Optional/VfxGraph",
)
OUTPUT = ROOT / "Docs/UPM/Inventories/UPM_P5_EDITOR_PORTABILITY.csv"
SURFACE = PACKAGE / "upm-surface.json"

PATTERNS = {
    "Tools": re.compile(r"(?:^|[\"'])Tools/"),
    "FGUIProject": re.compile(r"FGUIProject/"),
    "Config": re.compile(r"Assets/Config/"),
    "Scenes": re.compile(r"Assets/Scenes/"),
    "VFX": re.compile(r"Assets/VFX/"),
    "Bundles": re.compile(r"Assets/Bundles/"),
    "StreamingAssets": re.compile(r"Assets/StreamingAssets/"),
    "CharacterTest": re.compile(r"CharacterTest"),
}


def relative(path: pathlib.Path) -> str:
    return path.relative_to(ROOT).as_posix()


def guid(path: pathlib.Path) -> str:
    meta = pathlib.Path(str(path) + ".meta")
    if not meta.is_file():
        return ""
    match = re.search(r"^guid:\s*([0-9a-fA-F]+)\s*$", meta.read_text(encoding="utf-8"), re.MULTILINE)
    return match.group(1).lower() if match else ""


def asmdefs() -> list[tuple[pathlib.Path, dict]]:
    results = []
    for path in ROOT.glob("Assets/Scripts/**/*.asmdef"):
        results.append((path.parent, json.loads(path.read_text(encoding="utf-8-sig"))))
    return sorted(results, key=lambda item: len(item[0].parts), reverse=True)


def owning_assembly(path: pathlib.Path, definitions: list[tuple[pathlib.Path, dict]]) -> dict | None:
    for directory, definition in definitions:
        try:
            path.relative_to(directory)
            return definition
        except ValueError:
            continue
    return None


def main() -> int:
    surface = json.loads(SURFACE.read_text(encoding="utf-8"))
    initializers = surface.get("editorInitializers", {})
    definitions = asmdefs()
    rows = []

    roots = [PACKAGE, *PROJECT_ROOTS]
    seen: set[pathlib.Path] = set()
    for scan_root in roots:
        if not scan_root.is_dir():
            continue
        for path in sorted(scan_root.rglob("*.cs")):
            resolved = path.resolve()
            if resolved in seen:
                continue
            seen.add(resolved)
            definition = owning_assembly(path, definitions)
            if definition is None:
                continue
            include_platforms = definition.get("includePlatforms", [])
            is_editor = "Editor" in include_platforms or "Editor" in path.parts
            if not is_editor:
                continue

            text = path.read_text(encoding="utf-8", errors="replace")
            hits = sorted(name for name, pattern in PATTERNS.items() if pattern.search(text))
            package_relative = ""
            try:
                package_relative = path.relative_to(PACKAGE).as_posix()
            except ValueError:
                pass
            initializer = initializers.get(package_relative, {})
            in_package = bool(package_relative)
            assembly_name = definition.get("name", "")
            owner = surface.get("assemblies", {}).get(assembly_name, "project-only")
            decision = "retain-package" if in_package else "project-only"
            if package_relative == "Editor/VfxPublishedBindingEditorBootstrap.cs":
                decision = "retain-package-no-write"
            if any(token in package_relative for token in (
                "CharacterImport/CharacterLocomotionCalibrationEditorBridge.cs",
                "CharacterImport/CharacterPackageImportRequestBridge.cs",
            )):
                decision = "retain-explicit-request-capability"

            rows.append({
                "sourcePath": relative(path),
                "guid": guid(path),
                "assembly": assembly_name,
                "surfaceOwner": owner,
                "initializerKind": "runtime" if "RuntimeInitializeOnLoadMethod" in text else ("editor" if initializer else "none"),
                "activation": "|".join(initializer.get("activation", [])) if isinstance(initializer.get("activation"), list) else initializer.get("activation", "explicit user command or library API"),
                "requiredInputs": initializer.get("requiredInputs", "|".join(hits) if hits else "none"),
                "writes": initializer.get("writes", "explicit user command only" if any(value in text for value in ("SaveAssets", "WriteAll", "CreateAsset")) else "none detected"),
                "optionalGate": "|".join(definition.get("defineConstraints", [])),
                "projectPathHits": "|".join(hits),
                "decision": decision,
                "newOwner": "package" if in_package else relative(path.parent),
            })

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    fields = [
        "sourcePath", "guid", "assembly", "surfaceOwner", "initializerKind",
        "activation", "requiredInputs", "writes", "optionalGate",
        "projectPathHits", "decision", "newOwner",
    ]
    with OUTPUT.open("w", newline="", encoding="utf-8") as stream:
        writer = csv.DictWriter(stream, fieldnames=fields)
        writer.writeheader()
        writer.writerows(sorted(rows, key=lambda row: row["sourcePath"]))
    print(f"Wrote {relative(OUTPUT)}: {len(rows)} Editor source rows.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
