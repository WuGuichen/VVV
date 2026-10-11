#!/usr/bin/env python3
"""Focused tests for the cross-platform UPM package boundary checker."""

from __future__ import annotations

import importlib.util
import json
import pathlib
import tempfile
import types
import unittest


CHECKER_PATH = pathlib.Path(__file__).resolve().parents[1] / "check_upm_package_boundary.py"
SPEC = importlib.util.spec_from_file_location("check_upm_package_boundary", CHECKER_PATH)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"Cannot load checker module from {CHECKER_PATH}")
CHECKER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(CHECKER)


class UpmPackageBoundaryTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp_directory = tempfile.TemporaryDirectory()
        self.package_root = pathlib.Path(self.temp_directory.name) / "Package"
        self.package_root.mkdir()
        self.write_valid_fixture()

    def tearDown(self) -> None:
        self.temp_directory.cleanup()

    def write_json(self, relative_path: str, value: object) -> None:
        path = self.package_root / relative_path
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")

    def write_text(self, relative_path: str, value: str) -> None:
        path = self.package_root / relative_path
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(value, encoding="utf-8")

    def write_asmdef(
        self,
        relative_path: str,
        name: str,
        references: list[str],
        constraints: list[str],
        version_defines: list[dict[str, str]] | None = None,
    ) -> None:
        self.write_json(
            relative_path,
            {
                "name": name,
                "references": references,
                "defineConstraints": constraints,
                "versionDefines": version_defines or [],
            },
        )

    def write_valid_fixture(self) -> None:
        dependencies = {
            name: "1.0.0" for name in CHECKER.REQUIRED_DEPENDENCIES
        }
        self.write_json(
            "package.json",
            {
                "name": CHECKER.DEFAULT_PACKAGE_NAME,
                "displayName": "Fixture",
                "version": "0.1.0-preview.1",
                "unity": "6000.5",
                "description": "Boundary checker fixture.",
                "repository": {"type": "git", "url": "https://example.invalid/repo"},
                "author": {"name": "fixture"},
                "dependencies": dependencies,
            },
        )
        (self.package_root / "README.md").write_text("# Fixture\n", encoding="utf-8")
        (self.package_root / "CHANGELOG.md").write_text(
            "# Changelog\n\n## [0.1.0-preview.1]\n",
            encoding="utf-8",
        )
        self.write_asmdef(
            "Fmod/Fmod.asmdef",
            "Fixture.Fmod",
            ["FMODUnity"],
            ["MXFRAMEWORK_FMOD"],
        )
        self.write_asmdef(
            "FairyGui/FairyGui.asmdef",
            "Fixture.FairyGui",
            ["FairyGUI"],
            ["MXFRAMEWORK_FAIRYGUI"],
            [
                {
                    "name": "com.fairygui.gui",
                    "expression": "5.2.0",
                    "define": "MXFRAMEWORK_FAIRYGUI",
                }
            ],
        )
        self.write_asmdef(
            "Magica/Magica.asmdef",
            "Fixture.Magica",
            ["MagicaClothV2"],
            ["MAGICACLOTH2"],
        )
        self.write_asmdef(
            "Transport/Transport.asmdef",
            "Fixture.Transport",
            ["Unity.Networking.Transport"],
            ["MXFRAMEWORK_UNITY_TRANSPORT"],
            [
                {
                    "name": "com.unity.transport",
                    "expression": "6.5.0",
                    "define": "MXFRAMEWORK_UNITY_TRANSPORT",
                }
            ],
        )
        self.write_asmdef(
            "Vfx/Vfx.asmdef",
            "Fixture.Vfx",
            ["Unity.VisualEffectGraph.Runtime"],
            ["MXFRAMEWORK_VFX_GRAPH"],
            [
                {
                    "name": "com.unity.visualeffectgraph",
                    "expression": "17.0.0",
                    "define": "MXFRAMEWORK_VFX_GRAPH",
                }
            ],
        )

    def valid_surface_owners(self) -> dict[str, str]:
        return {
            "Fixture.FairyGui": "optional-adapter",
            "Fixture.Fmod": "optional-adapter",
            "Fixture.Magica": "optional-adapter",
            "Fixture.Transport": "optional-adapter",
            "Fixture.Vfx": "optional-adapter",
        }

    def write_surface_manifest(
        self,
        assembly_owners: dict[str, str] | None = None,
        editor_initializers: dict[str, object] | None = None,
    ) -> None:
        self.write_json(
            "upm-surface.json",
            {
                "schemaVersion": 1,
                "assemblies": (
                    self.valid_surface_owners()
                    if assembly_owners is None
                    else assembly_owners
                ),
                "editorInitializers": (
                    {} if editor_initializers is None else editor_initializers
                ),
            },
        )

    def audit(self, **overrides: object) -> tuple[dict[str, object], int]:
        arguments = {
            "package_root": self.package_root,
            "expected_package_name": CHECKER.DEFAULT_PACKAGE_NAME,
            "candidate_commit": None,
            "require_surface_manifest": False,
            "require_release_metadata": False,
        }
        arguments.update(overrides)
        return CHECKER.build_report(types.SimpleNamespace(**arguments))

    def test_valid_preview_fixture_passes_with_advisory_gaps(self) -> None:
        report, exit_code = self.audit()

        self.assertEqual(0, exit_code)
        self.assertTrue(report["result"]["passed"])
        self.assertEqual(5, report["assemblies"]["count"])
        self.assertEqual(0, report["result"]["blockingFailureCount"])
        self.assertGreater(report["result"]["readinessGapCount"], 0)
        self.assertEqual(
            {
                "fairygui": 1,
                "fmod": 1,
                "magica-cloth": 1,
                "unity-transport": 1,
                "vfx-graph": 1,
            },
            {
                name: gate["assemblyCount"]
                for name, gate in report["assemblies"]["optionalGates"].items()
            },
        )

    def test_reverse_closure_without_define_constraint_fails(self) -> None:
        self.write_asmdef(
            "Consumer/Consumer.asmdef",
            "Fixture.FmodConsumer",
            ["Fixture.Fmod"],
            [],
        )

        report, exit_code = self.audit()

        self.assertEqual(1, exit_code)
        self.assertFalse(report["result"]["passed"])
        self.assertIn(
            "optional-define-constraint",
            {failure["code"] for failure in report["failures"]},
        )

    def test_wrong_version_define_expression_fails(self) -> None:
        self.write_asmdef(
            "FairyGui/FairyGui.asmdef",
            "Fixture.FairyGui",
            ["FairyGUI"],
            ["MXFRAMEWORK_FAIRYGUI"],
            [
                {
                    "name": "com.fairygui.gui",
                    "expression": "5.1.0",
                    "define": "MXFRAMEWORK_FAIRYGUI",
                }
            ],
        )

        report, exit_code = self.audit()

        self.assertEqual(1, exit_code)
        self.assertIn(
            "optional-version-define",
            {failure["code"] for failure in report["failures"]},
        )

    def test_base_assembly_direct_ugui_using_fails(self) -> None:
        self.write_asmdef("Base/Base.asmdef", "Fixture.Base", [], [])
        self.write_text(
            "Base/UsesEventSystem.cs",
            "using UnityEngine.EventSystems;\n\ninternal sealed class UsesEventSystem {}\n",
        )

        report, exit_code = self.audit()

        self.assertEqual(1, exit_code)
        self.assertIn(
            "optional-source-ugui",
            {failure["code"] for failure in report["failures"]},
        )
        self.assertEqual(
            1,
            report["sourceDependencies"]["ugui"]["violationCount"],
        )

    def test_fairygui_gated_assembly_may_use_event_systems(self) -> None:
        self.write_text(
            "FairyGui/UsesEventSystem.cs",
            "using UnityEngine.EventSystems;\n\ninternal sealed class UsesEventSystem {}\n",
        )

        report, exit_code = self.audit()

        self.assertEqual(0, exit_code)
        self.assertEqual(
            0,
            report["sourceDependencies"]["ugui"]["violationCount"],
        )
        self.assertEqual(
            "fairygui-reverse-closure",
            report["sourceDependencies"]["ugui"]["directReferences"][0]["coverage"],
        )

    def test_generated_file_and_strict_policies_fail(self) -> None:
        (self.package_root / ".DS_Store").write_bytes(b"fixture")

        report, exit_code = self.audit(
            require_surface_manifest=True,
            require_release_metadata=True,
        )

        self.assertEqual(1, exit_code)
        failure_codes = {failure["code"] for failure in report["failures"]}
        self.assertIn("forbidden-package-path", failure_codes)
        self.assertIn("surface-manifest-missing", failure_codes)
        self.assertIn("package-license-missing", failure_codes)

    def test_valid_surface_manifest_passes_strict_policy(self) -> None:
        self.write_surface_manifest()

        report, exit_code = self.audit(require_surface_manifest=True)

        self.assertEqual(0, exit_code)
        self.assertEqual(5, report["surface"]["classifiedAssemblyCount"])
        self.assertEqual(0, report["surface"]["dependencyViolationCount"])
        self.assertEqual(0, report["surface"]["optionalOwnerViolationCount"])

    def test_surface_manifest_missing_and_stale_assemblies_fail(self) -> None:
        owners = self.valid_surface_owners()
        del owners["Fixture.Fmod"]
        owners["Fixture.Stale"] = "base-runtime"
        self.write_surface_manifest(owners)

        report, exit_code = self.audit(require_surface_manifest=True)

        self.assertEqual(1, exit_code)
        failure_codes = {failure["code"] for failure in report["failures"]}
        self.assertIn("surface-assembly-unclassified", failure_codes)
        self.assertIn("surface-assembly-stale", failure_codes)

    def test_surface_manifest_invalid_owner_fails(self) -> None:
        owners = self.valid_surface_owners()
        owners["Fixture.Fmod"] = "everything"
        self.write_surface_manifest(owners)

        report, exit_code = self.audit(require_surface_manifest=True)

        self.assertEqual(1, exit_code)
        self.assertIn(
            "surface-owner",
            {failure["code"] for failure in report["failures"]},
        )

    def test_surface_owner_dependency_matrix_fails_closed(self) -> None:
        self.write_asmdef(
            "Repository/Repository.asmdef",
            "Fixture.Repository",
            [],
            [],
        )
        self.write_asmdef(
            "Base/Base.asmdef",
            "Fixture.Base",
            ["Fixture.Repository"],
            [],
        )
        owners = self.valid_surface_owners()
        owners.update(
            {
                "Fixture.Base": "base-runtime",
                "Fixture.Repository": "repository-validation",
            }
        )
        self.write_surface_manifest(owners)

        report, exit_code = self.audit(require_surface_manifest=True)

        self.assertEqual(1, exit_code)
        self.assertIn(
            "surface-owner-dependency",
            {failure["code"] for failure in report["failures"]},
        )

    def test_known_optional_closure_cannot_use_base_owner(self) -> None:
        owners = self.valid_surface_owners()
        owners["Fixture.Fmod"] = "base-runtime"
        self.write_surface_manifest(owners)

        report, exit_code = self.audit(require_surface_manifest=True)

        self.assertEqual(1, exit_code)
        self.assertIn(
            "surface-optional-owner",
            {failure["code"] for failure in report["failures"]},
        )

    def test_optional_adapter_without_gate_fails(self) -> None:
        self.write_asmdef(
            "Optional/Ungated.asmdef",
            "Fixture.UngatedOptional",
            [],
            [],
        )
        owners = self.valid_surface_owners()
        owners["Fixture.UngatedOptional"] = "optional-adapter"
        self.write_surface_manifest(owners)

        report, exit_code = self.audit(require_surface_manifest=True)

        self.assertEqual(1, exit_code)
        self.assertIn(
            "surface-optional-adapter-ungated",
            {failure["code"] for failure in report["failures"]},
        )

    def test_initializer_contract_accepts_portable_no_input(self) -> None:
        self.write_text(
            "Editor/Bootstrap.cs",
            "[UnityEditor.InitializeOnLoad] internal static class Bootstrap {}\n",
        )
        self.write_surface_manifest(
            editor_initializers={
                "Editor/Bootstrap.cs": {
                    "classification": "portable-no-input",
                    "activation": "domain reload",
                    "requiredInputs": "none",
                    "missingInputBehavior": "not applicable",
                    "writes": "none",
                }
            }
        )

        report, exit_code = self.audit(require_surface_manifest=True)

        self.assertEqual(0, exit_code)
        self.assertEqual(1, report["surface"]["classifiedEditorInitializerCount"])

    def test_initializer_contract_missing_required_field_fails(self) -> None:
        self.write_text(
            "Editor/Bootstrap.cs",
            "[UnityEditor.InitializeOnLoad] internal static class Bootstrap {}\n",
        )
        self.write_surface_manifest(
            editor_initializers={
                "Editor/Bootstrap.cs": {
                    "classification": "portable-no-input",
                    "activation": "domain reload",
                    "requiredInputs": "none",
                    "missingInputBehavior": "not applicable",
                }
            }
        )

        report, exit_code = self.audit(require_surface_manifest=True)

        self.assertEqual(1, exit_code)
        self.assertIn(
            "surface-initializer-field",
            {failure["code"] for failure in report["failures"]},
        )

    def test_initializer_contract_missing_and_stale_entries_fail(self) -> None:
        self.write_text(
            "Editor/Bootstrap.cs",
            "[UnityEditor.InitializeOnLoad] internal static class Bootstrap {}\n",
        )
        self.write_surface_manifest(
            editor_initializers={
                "Editor/Stale.cs": {
                    "classification": "portable-no-input",
                    "activation": "domain reload",
                    "requiredInputs": "none",
                    "missingInputBehavior": "not applicable",
                    "writes": "none",
                }
            }
        )

        report, exit_code = self.audit(require_surface_manifest=True)

        self.assertEqual(1, exit_code)
        failure_codes = {failure["code"] for failure in report["failures"]}
        self.assertIn("surface-initializer-unclassified", failure_codes)
        self.assertIn("surface-initializer-stale", failure_codes)

    def test_report_content_is_deterministic(self) -> None:
        self.write_surface_manifest()
        first_report, first_exit_code = self.audit(require_surface_manifest=True)
        second_report, second_exit_code = self.audit(require_surface_manifest=True)

        self.assertEqual(first_exit_code, second_exit_code)
        self.assertEqual(first_report, second_report)


if __name__ == "__main__":
    unittest.main()
