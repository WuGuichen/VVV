"""Exercise Git delivery boundaries and keep asset-integrity failures blocking."""
from __future__ import annotations

import contextlib
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest import mock

SPEC = importlib.util.spec_from_file_location(
    "character_source_check", Path(__file__).resolve().parents[1] / "check_character_authoring_sources.py")
CHECK = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(CHECK)


class CharacterAuthoringDeliveryTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.repo = Path(self.temp.name)
        self.root = self.repo / "ArtSource/Characters/000001"
        self.git("init", "-q")
        # Fake fixture bytes must not invoke the machine's real LFS filters,
        # including during diff, while check-attr still verifies the policy.
        self.git("config", "filter.lfs.process", "")
        self.git("config", "filter.lfs.clean", "")
        self.git("config", "filter.lfs.required", "false")
        self.write("Authoring/CharacterPackages/character_id_catalog.json", {
            "schemaVersion": "1.0", "width": 6, "entries": [{"id": "000001"}], "retiredIds": []})
        self.write("ArtSource/Characters/authoring_legacy_exemptions.json", {"characterIds": []})
        self.write("ArtSource/Characters/000001/authoring_manifest.json", {
            "schemaVersion": "1.0", "characterId": "000001", "packageId": "000001",
            "sourceRoot": ".", "authority": {"blenderPath": "", "finalFbxPath": ""}, "items": []})
        self.write(".gitignore", "*.blend1\n*.blend2\n*_backup_*.blend\n")
        self.commit_fixture()

    def git(self, *args):
        return subprocess.check_output(["git", *args], cwd=self.repo, stderr=subprocess.DEVNULL)

    def write(self, relative, value):
        path = self.repo / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(value) if isinstance(value, dict) else value, encoding="utf-8")
        return path

    def commit_fixture(self):
        # Commits exist only in each TemporaryDirectory fixture, never in the project.
        self.git("-c", "filter.lfs.process=", "-c", "filter.lfs.clean=",
                 "-c", "filter.lfs.required=false", "add", ".")
        self.git("-c", "user.name=Harness Fixture", "-c", "user.email=fixture@example.invalid",
                 "-c", "commit.gpgsign=false", "commit", "-q", "-m", "fixture")

    def run_check(self):
        output = io.StringIO()
        with contextlib.redirect_stdout(output), contextlib.redirect_stderr(output):
            result = CHECK.main(self.repo)
        return result, output.getvalue()

    def declare(self, relative, content="accepted", declared_hash=None):
        source = self.write("ArtSource/Characters/000001/" + relative, content)
        path = self.root / "authoring_manifest.json"
        manifest = json.loads(path.read_text())
        manifest["items"].append({"id": "source", "path": relative,
            "sha256": declared_hash or "sha256:" + hashlib.sha256(source.read_bytes()).hexdigest()})
        self.write(path.relative_to(self.repo).as_posix(), manifest)
        return source

    def prepare_retained_migration(self):
        self.write(".gitattributes", "*.blend filter=lfs\n")
        self.declare("Blender_Work/000001_work.blend", "accepted source")
        rel = "Blender_Work/explicit_before.blend"
        self.write("ArtSource/Characters/000001/" + rel, "retained backup")
        report = {"format": "mx.wgameLegacyPalettePreserveMigration.v1", "characterId": "000001",
                  "backupBlend": "E:/old-clone/ArtSource/Characters/000001/" + rel,
                  "catalogHash": "sha256:" + "b" * 64}
        path = self.write("ArtSource/Characters/000001/Blender_Work/audit/migration.json", report)
        self.commit_fixture()
        return path, self.root / rel

    def test_retained_migration_uses_report_content_and_head_not_filename_age(self):
        report, backup = self.prepare_retained_migration()
        before = (report.read_bytes(), backup.read_bytes())
        self.assertEqual(self.run_check()[0], 0)
        self.assertEqual((report.read_bytes(), backup.read_bytes()), before)

    def test_new_migration_record_cannot_hide_an_unadopted_source(self):
        self.prepare_retained_migration()
        self.write("ArtSource/Characters/000001/Blender_Work/new.blend", "new data")
        self.write("ArtSource/Characters/000001/Blender_Work/audit/new.json", {
            "format": "mx.wgameLegacyPalettePreserveMigration.v1", "characterId": "000001",
            "backupBlend": "ArtSource/Characters/000001/Blender_Work/new.blend",
            "catalogHash": "sha256:" + "b" * 64})
        self.assertIn("not declared", self.run_check()[1])

    def test_modified_retained_migration_record_stays_blocking(self):
        report, _ = self.prepare_retained_migration()
        value = json.loads(report.read_text())
        value["catalogHash"] = "sha256:" + "c" * 64
        report.write_text(json.dumps(value))
        self.assertIn("not declared", self.run_check()[1])

    def test_retained_migration_backup_cannot_be_promoted_to_active_source(self):
        _, backup = self.prepare_retained_migration()
        path = self.root / "authoring_manifest.json"
        manifest = json.loads(path.read_text())
        manifest["items"].append({"id": "backup", "path": backup.relative_to(self.root).as_posix(),
                                 "sha256": CHECK.file_sha256(backup)})
        self.write(path.relative_to(self.repo).as_posix(), manifest)
        self.assertIn("retained migration backup cannot be an active source", self.run_check()[1])

    def test_edited_historical_backup_cannot_be_promoted_to_active_source(self):
        _, backup = self.prepare_retained_migration()
        backup.write_text("edited historical backup", encoding="utf-8")
        path = self.root / "authoring_manifest.json"
        manifest = json.loads(path.read_text())
        manifest["items"].append({"id": "backup", "path": backup.relative_to(self.root).as_posix(),
                                 "sha256": CHECK.file_sha256(backup)})
        self.write(path.relative_to(self.repo).as_posix(), manifest)
        self.assertIn("retained migration backup cannot be an active source", self.run_check()[1])

    def test_migration_backup_link_must_match_character_and_safe_relative_path(self):
        report, _ = self.prepare_retained_migration()
        original = json.loads(report.read_text())
        for link in ("ArtSource/Characters/000002/Blender_Work/other.blend",
                     "ArtSource/Characters/000001/../other.blend"):
            with self.subTest(link=link):
                document = dict(original, backupBlend=link)
                self.write(report.relative_to(self.repo).as_posix(), document)
                self.commit_fixture()
                self.assertIn("not declared", self.run_check()[1])

    def prepare_published_bake(self):
        self.write(".gitattributes", "*.blend filter=lfs\n*.fbx filter=lfs\n*.png filter=lfs\n")
        source = self.declare("Blender_Work/000001_work.blend", "accepted saved source")
        manifest_path = self.root / "authoring_manifest.json"
        manifest = json.loads(manifest_path.read_text())
        manifest["items"][0]["kind"] = "BlenderWork"
        self.write(manifest_path.relative_to(self.repo).as_posix(), manifest)
        binding = {"schemaVersion": "1.0", "packageId": "000001", "textureSize": 512,
                   "authoringSourcePath": source.relative_to(self.repo).as_posix(),
                   "authoringSourceHash": CHECK.file_sha256(source), "sourceBlendHash": "sha256:" + "a" * 64}
        entries = []
        rows = (("000001_LOD0.fbx", "model", "modelHash", "modelFile", "body", "resources/models/body.fbx"),
                ("BaseColor.png", "texture", "baseColorHash", "baseColorFile", "baked_base_color", "resources/textures/BaseColor.png"),
                ("SurfaceData.png", "texture", "surfaceDataHash", "surfaceDataFile", "baked_surface_data", "resources/textures/SurfaceData.png"))
        package_binding = dict(binding)
        for filename, kind, hash_field, file_field, suffix, relative in rows:
            payload = "identity fixture bytes for " + filename
            final = self.write("ArtSource/Characters/000001/Final_Source/" + filename, payload)
            self.write("Authoring/CharacterPackages/character-000001/" + relative, payload)
            digest = CHECK.file_sha256(final)
            binding[hash_field] = package_binding[hash_field] = digest
            binding[file_field] = filename
            package_binding[file_field] = relative
            entries.append({"resourceKey": "char.000001." + kind + "." + suffix,
                            "relativePath": relative, "hash": digest})
        self.write("ArtSource/Characters/000001/Final_Source/character_baked_surface_binding.json", binding)
        self.write("Authoring/CharacterPackages/character-000001/config/character_baked_surface_binding.json", package_binding)
        self.write("Authoring/CharacterPackages/character-000001/manifest.json", {"packageId": "000001"})
        self.write("Authoring/CharacterPackages/character-000001/resource_catalog.json", {"entries": entries})
        return source

    def test_existing_baked_registration_does_not_require_duplicate_dcc_items(self):
        self.prepare_published_bake()
        self.assertEqual(self.run_check()[0], 0)
        manifest = json.loads((self.root / "authoring_manifest.json").read_text())
        self.assertEqual(len(manifest["items"]), 1)

    def test_baked_registration_cannot_silence_unpublished_work_edit(self):
        source = self.prepare_published_bake()
        source.write_text("pending edit")
        code, output = self.run_check()
        self.assertEqual(code, 1)
        self.assertIn("SHA-256 is missing or stale", output)
        self.assertIn("Work registration pending", output)
        self.assertIn("unpublished authoring changes", output)
        self.assertIn("published artifact identity and bytes remain intact", output)
        self.assertNotIn("not declared", output)

    def register_work_save(self, source):
        source.write_text("new saved authoring content", encoding="utf-8")
        path = self.root / "authoring_manifest.json"
        manifest = json.loads(path.read_text())
        manifest["items"][0]["sha256"] = CHECK.file_sha256(source)
        manifest["revision"] = manifest.get("revision", 1) + 1
        self.write(path.relative_to(self.repo).as_posix(), manifest)

    def test_registered_work_save_keeps_frozen_publication_intact(self):
        source = self.prepare_published_bake()
        self.commit_fixture()
        final = self.root / "Final_Source/character_baked_surface_binding.json"
        package = self.repo / "Authoring/CharacterPackages/character-000001/config/character_baked_surface_binding.json"
        before = (final.read_bytes(), package.read_bytes())
        self.register_work_save(source)
        code, output = self.run_check()
        self.assertEqual(code, 0, output)
        self.assertIn("unpublished authoring changes", output)
        self.assertIn("frozen source bytes verified in HEAD", output)
        self.assertNotIn("Work registration pending", output)
        self.assertEqual((final.read_bytes(), package.read_bytes()), before)

    def test_registered_work_save_cannot_hide_damaged_publication(self):
        source = self.prepare_published_bake()
        self.register_work_save(source)
        self.write("Authoring/CharacterPackages/character-000001/resources/models/body.fbx", "damaged release")
        code, output = self.run_check()
        self.assertEqual(code, 1)
        self.assertIn("package payload content mismatch", output)
        self.assertNotIn("published artifact identity and bytes remain intact", output)

    def test_well_formed_unavailable_source_hash_is_explicitly_unverified(self):
        self.prepare_published_bake()
        for relative in ("ArtSource/Characters/000001/Final_Source/character_baked_surface_binding.json",
                         "Authoring/CharacterPackages/character-000001/config/character_baked_surface_binding.json"):
            path = self.repo / relative
            binding = json.loads(path.read_text())
            binding["authoringSourceHash"] = "sha256:" + "b" * 64
            self.write(relative, binding)
        code, output = self.run_check()
        self.assertEqual(code, 0, output)
        self.assertIn("frozen source bytes unverified", output)
        self.assertIn("frozen bake snapshot bytes unverified", output)
        self.assertNotIn("source bytes verified", output)

    def test_invalid_frozen_snapshot_hash_stays_blocking(self):
        self.prepare_published_bake()
        for relative in ("ArtSource/Characters/000001/Final_Source/character_baked_surface_binding.json",
                         "Authoring/CharacterPackages/character-000001/config/character_baked_surface_binding.json"):
            binding = json.loads((self.repo / relative).read_text())
            binding["sourceBlendHash"] = "not-a-sha256"
            self.write(relative, binding)
        code, output = self.run_check()
        self.assertEqual(code, 1)
        self.assertIn("frozen bake snapshot hash missing or invalid", output)

    def test_head_lfs_source_evidence_requires_actual_matching_cached_bytes(self):
        payload = b"frozen source contents"
        digest = hashlib.sha256(payload).hexdigest()
        source = self.write("ArtSource/Characters/000001/Blender_Work/000001_work.blend",
                            "version https://git-lfs.github.com/spec/v1\noid sha256:" + digest
                            + "\nsize " + str(len(payload)) + "\n")
        self.commit_fixture()
        source.write_text("new saved work", encoding="utf-8")
        cached = self.repo / ".git/lfs/objects" / digest[:2] / digest[2:4] / digest
        cached.parent.mkdir(parents=True)
        cached.write_bytes(payload)
        self.assertIn("verified through HEAD and local LFS",
                      CHECK.saved_source_evidence(self.repo, source, "sha256:" + digest))
        cached.write_bytes(b"corrupt cache contents")
        self.assertIn("frozen source bytes unverified",
                      CHECK.saved_source_evidence(self.repo, source, "sha256:" + digest))

    def test_baked_final_content_mismatch_stays_blocking(self):
        self.prepare_published_bake()
        (self.root / "Final_Source/BaseColor.png").write_text("unpublished texture")
        self.assertIn("Final artifact content mismatch", self.run_check()[1])

    def test_baked_package_payload_mismatch_stays_blocking(self):
        self.prepare_published_bake()
        self.write("Authoring/CharacterPackages/character-000001/resources/models/body.fbx", "unpublished model")
        self.assertIn("package payload content mismatch", self.run_check()[1])

    def test_baked_catalog_identity_missing_or_ambiguous_stays_blocking(self):
        self.prepare_published_bake()
        path = self.repo / "Authoring/CharacterPackages/character-000001/resource_catalog.json"
        original = json.loads(path.read_text())
        for entries in (original["entries"][1:], original["entries"] + [original["entries"][0]]):
            with self.subTest(count=len(entries)):
                self.write(path.relative_to(self.repo).as_posix(), {"entries": entries})
                self.assertIn("catalog identity missing/ambiguous", self.run_check()[1])

    def test_baked_package_binding_cannot_change_nonpath_identity(self):
        self.prepare_published_bake()
        path = self.repo / "Authoring/CharacterPackages/character-000001/config/character_baked_surface_binding.json"
        binding = json.loads(path.read_text())
        binding["authoringSourceHash"] = "sha256:" + "0" * 64
        self.write(path.relative_to(self.repo).as_posix(), binding)
        self.assertIn("Final/package binding identity mismatch", self.run_check()[1])

    def test_baked_catalog_path_escape_stays_blocking(self):
        self.prepare_published_bake()
        path = self.repo / "Authoring/CharacterPackages/character-000001/resource_catalog.json"
        catalog = json.loads(path.read_text())
        catalog["entries"][0]["relativePath"] = "../escape.fbx"
        self.write(path.relative_to(self.repo).as_posix(), catalog)
        self.assertIn("catalog/package artifact identity mismatch", self.run_check()[1])

    def test_ignored_local_backup_preserved_and_does_not_block(self):
        backup = self.write("ArtSource/Characters/000001/Blender_Work/work.blend1", "user backup")
        before = backup.read_bytes()
        self.assertEqual(self.run_check()[0], 0)
        self.assertEqual(backup.read_bytes(), before)

    def test_force_added_ignored_backup_blocks_once(self):
        path = "ArtSource/Characters/000001/Blender_Work/work.blend1"
        self.write(path, "backup")
        self.git("add", "-f", path)
        code, output = self.run_check()
        self.assertEqual(code, 1)
        self.assertEqual(output.count("error:"), 1)
        self.assertIn("forbidden in delivery", output)

    def test_untracked_nonignored_current_source_still_needs_adoption(self):
        self.write("ArtSource/Characters/000001/Blender_Work/audit/current.json", "{}")
        code, output = self.run_check()
        self.assertEqual(code, 1)
        self.assertIn("not declared", output)

    def test_tracked_unregistered_current_source_still_blocks(self):
        self.write("ArtSource/Characters/000001/Final_Source/model.json", "{}")
        self.commit_fixture()
        self.assertIn("not declared", self.run_check()[1])

    def test_retained_head_publication_archive_preserved(self):
        path = self.write("ArtSource/Characters/000001/Blender_Work/publish_backups/receipt/model.json", "old bytes")
        self.commit_fixture()
        self.assertEqual(self.run_check()[0], 0)
        self.assertEqual(path.read_text(), "old bytes")

    def test_new_publication_backup_cannot_hide_unregistered_source(self):
        rel = "ArtSource/Characters/000001/Blender_Work/publish_backups/new/model.json"
        self.write(rel, "new")
        for staged in (False, True):
            with self.subTest(staged=staged):
                if staged: self.git("add", rel)
                code, output = self.run_check()
                self.assertEqual(code, 1)
                self.assertIn("new publication backups", output)

    def test_ignored_new_publication_backup_remains_local(self):
        self.write(".gitignore", "**/Blender_Work/publish_backups/\n")
        rel = "ArtSource/Characters/000001/Blender_Work/publish_backups/new/model.json"
        backup = self.write(rel, "local backup")
        self.assertEqual(self.run_check()[0], 0)
        self.assertEqual(backup.read_text(), "local backup")
        self.git("add", "-f", rel)
        self.assertIn("new publication backups", self.run_check()[1])

    def test_authority_cannot_point_at_unadopted_or_historical_source(self):
        path = self.root / "authoring_manifest.json"
        for relative in ("Final_Source/current.json", "Blender_Work/publish_backups/old/model.json"):
            with self.subTest(relative=relative):
                manifest = json.loads(path.read_text())
                manifest["authority"]["blenderPath"] = relative
                self.write(path.relative_to(self.repo).as_posix(), manifest)
                self.assertIn("must reference a declared active source", self.run_check()[1])

    def test_changed_historical_archive_requires_review(self):
        path = self.write("ArtSource/Characters/000001/Blender_Work/publish_backups/receipt/model.json", "old")
        self.commit_fixture()
        path.write_text("replacement")
        self.assertIn("historical publication archive changed", self.run_check()[1])

    def test_untracking_historical_archive_requires_review_without_deleting_data(self):
        rel = "ArtSource/Characters/000001/Blender_Work/publish_backups/receipt/model.json"
        path = self.write(rel, "retained user data")
        self.commit_fixture()
        self.write(".gitignore", "**/Blender_Work/publish_backups/\n")
        self.git("rm", "--cached", "--", rel)
        self.assertIn("historical publication archive changed", self.run_check()[1])
        self.assertEqual(path.read_text(), "retained user data")

    def test_archive_cannot_be_adopted_as_current_authority(self):
        self.declare("Blender_Work/publish_backups/receipt/model.json")
        self.commit_fixture()
        self.assertIn("cannot be an active source", self.run_check()[1])

    def test_declared_missing_source_remains_blocking_even_if_ignored(self):
        source = self.declare("Blender_Work/optional.json")
        source.unlink()  # Only a file created by this fixture.
        self.assertIn("declared optional source is missing", self.run_check()[1])

    def test_declared_ignored_source_cannot_escape_hash_check(self):
        self.write(".gitignore", "ignored.json\n")
        self.declare("Blender_Work/ignored.json", declared_hash="sha256:" + "0" * 64)
        before = (self.root / "authoring_manifest.json").read_bytes()
        self.assertIn("SHA-256 is missing or stale", self.run_check()[1])
        self.assertEqual((self.root / "authoring_manifest.json").read_bytes(), before)

    def test_registered_character_still_requires_manifest(self):
        (self.root / "authoring_manifest.json").unlink()
        self.assertIn("missing authoring_manifest", self.run_check()[1])

    def test_empty_optional_dcc_manifest_keeps_import_source_contract(self):
        self.write("ArtSource/Characters/000001/Basemesh/imported.json", "source")
        self.write("ArtSource/Characters/000001/character_sources.json", {"items": [{"id": "body"}]})
        self.assertEqual(self.run_check()[0], 0)

    def test_unsafe_declarations_remain_blocking(self):
        for path in ("../outside.json", "C:/outside.json", "/outside.json"):
            with self.subTest(path=path):
                manifest = json.loads((self.root / "authoring_manifest.json").read_text())
                manifest["items"] = [{"id": "unsafe", "path": path}]
                self.write("ArtSource/Characters/000001/authoring_manifest.json", manifest)
                self.assertIn("unsafe or duplicate item path", self.run_check()[1])

    def test_git_scope_failure_is_blocking_not_full_disk_fallback(self):
        with mock.patch.object(CHECK, "delivery_scope", side_effect=ValueError("scope unavailable")):
            code, output = self.run_check()
        self.assertEqual(code, 1)
        self.assertIn("scope unavailable", output)


if __name__ == "__main__":
    unittest.main()
