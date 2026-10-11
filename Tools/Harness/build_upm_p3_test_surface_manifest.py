#!/usr/bin/env python3
"""Build the reviewed UPM-P3 test ownership manifest without moving assets."""

from __future__ import annotations

import json
import pathlib
import re


REPOSITORY_ROOT = pathlib.Path(__file__).resolve().parents[2]
SOURCE_ROOT = REPOSITORY_ROOT / "Assets/Scripts/MxFramework/Tests"
TARGET_ROOT = REPOSITORY_ROOT / "Assets/Scripts/WGameFrameworkProject/Tests/Repository"
OUTPUT = REPOSITORY_ROOT / "Docs/UPM/Evidence/UPM-P3/test-surface-manifest.json"

CONTRACTS = {
    "MCPForUnityTests.EditMode": "Repository plugin integration; depends on MCPForUnity.Editor.",
    "MxFramework.Tests.Application.Unity.Editor": "Repository scene-fixture generator for Application PlayMode validation.",
    "MxFramework.Tests.Application.SessionBridge.Unity.Editor": "Repository candidate-scene fixture generator/validator that references Application PlayMode tests.",
    "MxFramework.Tests.Persistence.RuntimeBridge": "Deep persistence/runtime bridge regression retained in repository validation.",
    "MxFramework.Tests.PlayMode.Application": "Repository PlayMode scene and build-settings validation.",
    "MxFramework.Tests.PlayMode.Audio": "FMOD bank and Demo-backed repository PlayMode validation.",
    "MxFramework.Tests.PlayMode.GameplayTriggers": "Demo scene, Input Test Framework, and project physics validation.",
    "MxFramework.Tests.PlayMode.Resources": "Project CharacterTestScene resource-flow validation.",
    "MxFramework.Tests.PlayMode.Vfx": "Extended Unity ParticleSystem lifecycle regression retained outside the minimal package contract.",
    "MxFramework.Tests.UI.FairyGUI": "FGUIProject/Bundles/Demo/scene integration regression.",
}

PACKAGE_CONTRACTS = [
    {
        "assembly": "MxFramework.PackageTests.Runtime",
        "path": "Assets/Scripts/MxFramework/Tests/Runtime",
        "mode": "EditMode",
        "expectedTests": [
            "MxFramework.PackageTests.Runtime.RuntimeHostPackageContractTests.Lifecycle_StartTickStop_DispatchesInOrder",
            "MxFramework.PackageTests.Runtime.RuntimeHostPackageContractTests.CommandEnvelope_RoundTripsThroughBuffer",
        ],
    },
    {
        "assembly": "MxFramework.PackageTests.Editor",
        "path": "Assets/Scripts/MxFramework/Tests/Editor",
        "mode": "EditMode",
        "expectedTests": [
            "MxFramework.PackageTests.Editor.PackageAssetLocatorContractTests.RepresentativeStyle_ResolvesAndLoads",
            "MxFramework.PackageTests.Editor.PackageAssetLocatorContractTests.UnsafeRelativePaths_AreRejected",
        ],
    },
]


def read_guid(meta_path: pathlib.Path) -> str:
    match = re.search(
        r"^guid:\s*([0-9a-fA-F]{32})\s*$",
        meta_path.read_text(encoding="utf-8-sig"),
        re.MULTILINE,
    )
    if not match:
        raise RuntimeError(f"Missing GUID in {meta_path}")
    return match.group(1).lower()


def posix(path: pathlib.Path) -> str:
    return path.relative_to(REPOSITORY_ROOT).as_posix()


def main() -> int:
    moved = TARGET_ROOT.is_dir()
    scan_root = TARGET_ROOT if moved else SOURCE_ROOT
    baseline_guids: dict[str, str] = {}
    if OUTPUT.is_file():
        previous = json.loads(OUTPUT.read_text(encoding="utf-8-sig"))
        baseline_guids = {
            str(item["assembly"]): str(item["asmdefGuid"])
            for item in previous.get("existingAssemblies") or []
        }
    assemblies: list[dict[str, object]] = []
    all_test_sources: set[str] = set()
    test_attribute_count = 0
    asmdef_paths = list(scan_root.rglob("*.asmdef"))
    extra_source = (
        REPOSITORY_ROOT
        / "Assets/Scripts/MxFramework/Application.SessionBridge.Unity.Editor"
        / "MxFramework.Application.SessionBridge.Unity.Editor.asmdef"
    )
    if extra_source.is_file():
        asmdef_paths.append(extra_source)
    for asmdef_path in sorted(asmdef_paths):
        asmdef = json.loads(asmdef_path.read_text(encoding="utf-8-sig"))
        name = str(asmdef["name"])
        if name not in CONTRACTS:
            raise RuntimeError(f"Unclassified P3 test assembly: {name}")
        assembly_root = asmdef_path.parent
        entry_moved = assembly_root.is_relative_to(TARGET_ROOT)
        relative_directory = (
            assembly_root.relative_to(TARGET_ROOT)
            if entry_moved
            else assembly_root.relative_to(SOURCE_ROOT)
            if assembly_root.is_relative_to(SOURCE_ROOT)
            else pathlib.Path(assembly_root.name)
        )
        original_directory = (
            REPOSITORY_ROOT
            / "Assets/Scripts/MxFramework/Application.SessionBridge.Unity.Editor"
            if name == "MxFramework.Tests.Application.SessionBridge.Unity.Editor"
            else SOURCE_ROOT / relative_directory
        )
        target_directory = TARGET_ROOT / relative_directory
        sources = sorted(assembly_root.glob("*.cs"))
        current_paths = [posix(path) for path in sources]
        original_paths = [
            posix(original_directory / path.name)
            for path in sources
        ]
        overlap = all_test_sources.intersection(current_paths)
        if overlap:
            raise RuntimeError(f"Duplicate test source ownership: {sorted(overlap)}")
        all_test_sources.update(current_paths)
        assembly_test_attributes = 0
        for source in sources:
            text = source.read_text(encoding="utf-8-sig")
            assembly_test_attributes += len(
                re.findall(r"\[(?:UnityTest|Test|TestCase)(?:\(|\])", text)
            )
        test_attribute_count += assembly_test_attributes
        asmdef_guid = read_guid(asmdef_path.with_suffix(asmdef_path.suffix + ".meta"))
        if entry_moved and baseline_guids.get(name) not in (None, asmdef_guid):
            raise RuntimeError(
                f"Moved asmdef GUID changed for {name}: "
                f"baseline={baseline_guids.get(name)} current={asmdef_guid}"
            )
        assemblies.append(
            {
                "assembly": name,
                "asmdefPath": posix(asmdef_path),
                "asmdefGuid": asmdef_guid,
                "sourceDirectory": posix(original_directory),
                "targetDirectory": posix(target_directory),
                "owner": "repository-validation",
                "mode": "PlayMode" if ".PlayMode." in name else "EditMode",
                "originalTestSources": original_paths,
                "testSources": current_paths,
                "testAttributeCount": assembly_test_attributes,
                "references": list(asmdef.get("references") or []),
                "contract": CONTRACTS[name],
                "status": "moved-guid-preserved" if entry_moved else "classified-before-move",
            }
        )

    package_contracts: list[dict[str, object]] = []
    for contract in PACKAGE_CONTRACTS:
        item = dict(contract)
        contract_root = REPOSITORY_ROOT / str(contract["path"])
        asmdef_path = contract_root / (str(contract["assembly"]) + ".asmdef")
        if asmdef_path.is_file() and asmdef_path.with_suffix(asmdef_path.suffix + ".meta").is_file():
            item["asmdefGuid"] = read_guid(
                asmdef_path.with_suffix(asmdef_path.suffix + ".meta")
            )
            item["testSources"] = [
                posix(path) for path in sorted(contract_root.glob("*.cs"))
            ]
            item["status"] = "created-working-tree"
        else:
            item["asmdefGuid"] = None
            item["testSources"] = []
            item["status"] = "planned"
        package_contracts.append(item)

    payload = {
        "schemaVersion": 1,
        "phase": "UPM-P3",
        "state": (
            "moved-working-tree"
            if all(item["status"] == "moved-guid-preserved" for item in assemblies)
            else "partially-moved-working-tree"
            if moved
            else "classified-before-move"
        ),
        "sourceRoot": posix(SOURCE_ROOT),
        "repositoryTargetRoot": posix(TARGET_ROOT),
        "rules": {
            "deleteApprovedCount": 0,
            "packageTestsMustBeSelfContained": True,
            "repositoryTestsRemainVersioned": True,
            "preserveAsmdefIdentityAndGuid": True,
        },
        "summary": {
            "existingAssemblyCount": len(assemblies),
            "existingTestSourceCount": len(all_test_sources),
            "existingTestAttributeCount": test_attribute_count,
            "repositoryValidationAssemblyCount": len(assemblies),
            "newPackageContractAssemblyCount": len(PACKAGE_CONTRACTS),
            "newPackageContractExpectedTestCount": sum(
                len(item["expectedTests"]) for item in PACKAGE_CONTRACTS
            ),
        },
        "existingAssemblies": assemblies,
        "newPackageContracts": package_contracts,
    }
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
        newline="\n",
    )
    print(
        "UPM-P3 manifest built: "
        f"assemblies={len(assemblies)} sources={len(all_test_sources)} "
        f"testAttributes={test_attribute_count} packageContracts=4"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
