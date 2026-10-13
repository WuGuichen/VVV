"""Integration checks use local Git fixtures only; no service credentials required."""

from __future__ import annotations

import copy
import importlib.util
import json
import os
import subprocess
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch


spec = importlib.util.spec_from_file_location("sync_code_mirror", Path(__file__).resolve().parents[1] / "sync_code_mirror.py")
mirror = importlib.util.module_from_spec(spec)
assert spec.loader is not None
spec.loader.exec_module(mirror)


def git(repo: Path, *args: str, data: bytes | None = None) -> str:
    env = os.environ.copy()
    env.update(GIT_AUTHOR_NAME="Fixture", GIT_AUTHOR_EMAIL="fixture@local.invalid",
               GIT_COMMITTER_NAME="Fixture", GIT_COMMITTER_EMAIL="fixture@local.invalid")
    # Keep fixtures independent of hooks, signing and credential setup on the host.
    result = subprocess.run(["git", "-c", f"core.hooksPath={repo / 'disabled-hooks'}", "-c", "commit.gpgsign=false",
                             "-C", str(repo), *args], input=data, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                            env=env, check=True, timeout=20)
    return result.stdout.decode("utf-8").strip()


class MirrorIntegrationTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory(prefix="code-mirror-test-")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.source = self.root / "source"
        self.target = self.root / "target.git"
        self.source.mkdir()
        self.target.mkdir()
        git(self.source, "init", "--object-format=sha1", "-b", "main")
        git(self.target, "init", "--bare", "--object-format=sha1")
        self.config = json.loads(mirror.DEFAULT_CONFIG.read_text(encoding="utf-8"))
        self.config["source"] = {"url": str(self.source), "branch": "main", "name": "fixture/source"}
        self.config["targets"] = {"fixture": {"url": str(self.target), "branch": "code-mirror"}}
        for path, data in {
            "Assets/Scripts/Main.cs": b"public class Main {}\n",
            "Assets/Scripts/Main.cs.meta": b"guid: fixture\n",
            "Assets/Shaders/Test.shader": b"Shader \"Fixture\" {}\n",
            "Assets/Textures/Test.png": b"\x89PNG\0resource",
            "Assets/Textures/Test.png.meta": b"guid: resource\n",
            "Assets/Scenes/Test.unity": b"Unity YAML resource\n",
            "Tools/agent.py": b"print('fixture')\n",
            "Tools/__pycache__/accident.py": b"# tracked cache\n",
            "Tools/.env.cs": b"# tracked secret-looking path\n",
            "Docs/Interfaces/Test.md": b"# Fixture contract\n",
            "Docs/Tasks/Internal.md": b"# Task evidence excluded by profile\n",
            "Packages/manifest.json": b'{"dependencies": {}}\n',
            "AGENTS.md": b"# Fixture rules\n",
        }.items():
            self.write(path, data)
        self.commit()

    def write(self, path: str, data: bytes) -> None:
        file = self.source / path
        file.parent.mkdir(parents=True, exist_ok=True)
        file.write_bytes(data)

    def commit(self) -> str:
        git(self.source, "add", "--all")
        git(self.source, "commit", "-m", "fixture")
        return git(self.source, "rev-parse", "HEAD")

    def plan(self, name: str = "preview", config: dict | None = None, **kwargs) -> tuple[Path, dict]:
        output = self.root / name
        plan = mirror.create_plan(config or self.config, output, **kwargs)
        return output, plan

    def test_export_pins_committed_source_filters_resources_and_preserves_paths(self) -> None:
        committed = git(self.source, "rev-parse", "HEAD")
        self.write("Assets/Scripts/Main.cs", b"uncommitted work must stay local\n")
        output, plan = self.plan()
        selected = {item["path"] for item in plan["files"]}
        self.assertEqual(plan["source_commit"], committed)
        self.assertEqual((output / "export/Assets/Scripts/Main.cs").read_bytes(), b"public class Main {}\n")
        self.assertTrue({"Assets/Scripts/Main.cs.meta", "Assets/Shaders/Test.shader", "Docs/Interfaces/Test.md", "Packages/manifest.json"} <= selected)
        self.assertFalse(any(path in selected for path in ("Assets/Textures/Test.png", "Assets/Textures/Test.png.meta", "Assets/Scenes/Test.unity", "Tools/.env.cs", "Tools/__pycache__/accident.py", "Docs/Tasks/Internal.md")))
        self.assertEqual(git(self.target, "for-each-ref"), "")

    def test_publish_has_independent_history_preserves_main_and_is_idempotent(self) -> None:
        source_sha = git(self.source, "rev-parse", "HEAD")
        git(self.source, "push", str(self.target), "HEAD:refs/heads/main")
        output, plan = self.plan()
        pushes = []
        original_run = mirror.Git.run

        def capture(instance, args, **kwargs):
            if args[0] == "push":
                pushes.append(args)
            return original_run(instance, args, **kwargs)

        with patch.object(mirror.Git, "run", capture):
            first = mirror.publish_plan(output / "plan.json")["targets"]["fixture"]
        self.assertEqual(len(pushes), 1)
        self.assertFalse(any(arg.startswith("--force") for arg in pushes[0]))
        self.assertEqual(first["status"], "published")
        self.assertEqual(git(self.target, "rev-parse", "main"), source_sha)
        self.assertEqual(git(self.target, "rev-list", "--count", "code-mirror"), "1")
        self.assertNotIn("Assets/Textures/Test.png", git(self.target, "ls-tree", "-r", "--name-only", "code-mirror"))
        repeated = mirror.publish_plan(output / "plan.json")["targets"]["fixture"]
        self.assertEqual(repeated, {"status": "unchanged", "commit": first["commit"]})
        _, second_plan = self.plan("same-source")
        self.assertEqual(second_plan["files"], plan["files"])
        self.assertFalse(any(second_plan["targets"]["fixture"]["changes"].values()))

    def test_publish_reads_export_paths_over_windows_legacy_limit(self) -> None:
        path = "Assets/" + "nested_" + "a" * 43 + "/" + "nested_" + "b" * 43 + "/LongPath.cs"
        payload = b"public class LongPath {}\n"
        self.write(path, payload)
        self.commit()
        output, _ = self.plan("preview_" + "p" * 100)
        self.assertGreater(len(str(output / "export" / path)), 259)
        result = mirror.publish_plan(output / "plan.json")["targets"]["fixture"]
        self.assertEqual(result["status"], "published", result)
        self.assertEqual(git(self.target, "show", f"code-mirror:{path}"), payload.decode().strip())

    def test_incremental_update_deletes_removed_source_without_changing_old_main(self) -> None:
        source_sha = git(self.source, "rev-parse", "HEAD")
        git(self.source, "push", str(self.target), "HEAD:refs/heads/main")
        first_output, _ = self.plan()
        first = mirror.publish_plan(first_output / "plan.json")["targets"]["fixture"]["commit"]
        (self.source / "Tools/agent.py").unlink()
        self.write("Assets/Scripts/新增.cs", b"public class Added {}\n")
        self.commit()
        output, plan = self.plan("updated")
        self.assertIn("Tools/agent.py", plan["targets"]["fixture"]["changes"]["deleted"])
        result = mirror.publish_plan(output / "plan.json")["targets"]["fixture"]
        self.assertEqual(result["status"], "published")
        self.assertEqual(git(self.target, "rev-parse", "code-mirror^"), first)
        self.assertEqual(git(self.target, "rev-parse", "main"), source_sha)
        self.assertIn("Assets/Scripts/新增.cs", git(self.target, "-c", "core.quotePath=false", "ls-tree", "-r", "--name-only", "code-mirror"))

    def test_modified_export_and_modified_plan_are_rejected_before_remote_write(self) -> None:
        output, _ = self.plan()
        (output / "export/Assets/Scripts/Main.cs").write_bytes(b"changed after review\n")
        with self.assertRaisesRegex(mirror.MirrorError, "content changed"):
            mirror.publish_plan(output / "plan.json")
        self.assertEqual(git(self.target, "for-each-ref"), "")
        plan = json.loads((output / "plan.json").read_text())
        plan["targets"]["fixture"]["endpoint"]["branch"] = "main"
        mirror.write_json(output / "plan.json", plan)
        with self.assertRaisesRegex(mirror.MirrorError, "digest"):
            mirror.publish_plan(output / "plan.json")

    def test_existing_unmanaged_branch_is_not_replaced(self) -> None:
        git(self.source, "push", str(self.target), "HEAD:refs/heads/code-mirror")
        old = git(self.target, "rev-parse", "code-mirror")
        output, plan = self.plan()
        self.assertFalse(plan["targets"]["fixture"]["managed"])
        result = mirror.publish_plan(output / "plan.json")["targets"]["fixture"]
        self.assertEqual(result["status"], "failed")
        self.assertEqual(git(self.target, "rev-parse", "code-mirror"), old)

    def test_destination_changed_after_preview_is_not_overwritten(self) -> None:
        output, _ = self.plan()
        git(self.source, "push", str(self.target), "HEAD:refs/heads/code-mirror")
        concurrent = git(self.target, "rev-parse", "code-mirror")
        result = mirror.publish_plan(output / "plan.json")["targets"]["fixture"]
        self.assertEqual(result["status"], "failed")
        self.assertIn("changed after preview", result["error"])
        self.assertEqual(git(self.target, "rev-parse", "code-mirror"), concurrent)

    def test_new_branch_push_rejects_concurrent_creation_between_check_and_push(self) -> None:
        output, _ = self.plan()
        concurrent = git(self.source, "rev-parse", "HEAD")
        original_run = mirror.Git.run

        def race(instance, args, **kwargs):
            if args[0] == "push":
                git(self.source, "push", str(self.target), "HEAD:refs/heads/code-mirror")
            return original_run(instance, args, **kwargs)

        with patch.object(mirror.Git, "run", race):
            result = mirror.publish_plan(output / "plan.json")["targets"]["fixture"]
        self.assertEqual(result["status"], "failed")
        self.assertEqual(git(self.target, "rev-parse", "code-mirror"), concurrent)

    def test_exact_file_exclusions_override_whitelist_and_paired_metadata(self) -> None:
        self.write("Assets/Shaders/Test.shader.meta", b"guid: shader\n")
        self.commit()
        config = copy.deepcopy(self.config)
        config["profile"]["exclude_files"] = ["Assets/Scripts/Main.cs", "Assets/Shaders/Test.shader.meta", "Packages/manifest.json"]
        _, plan = self.plan(config=config)
        paths = {item["path"] for item in plan["files"]}
        self.assertNotIn("Assets/Scripts/Main.cs", paths)
        self.assertNotIn("Assets/Scripts/Main.cs.meta", paths)
        self.assertNotIn("Assets/Shaders/Test.shader.meta", paths)
        self.assertNotIn("Packages/manifest.json", paths)
        self.assertIn("Assets/Shaders/Test.shader", paths)

    def test_lfs_pointer_binary_symlink_and_size_fail_closed(self) -> None:
        for index, payload in enumerate((mirror.LFS_PREFIX + b"\noid sha256:fixture\nsize 123\n", b"binary\0data")):
            with self.subTest(payload=payload[:10]):
                self.write("Assets/Scripts/Bad.cs", payload)
                self.commit()
                with self.assertRaises(mirror.MirrorError):
                    self.plan(f"bad-{index}")
        (self.source / "Assets/Scripts/Bad.cs").unlink()
        self.commit()
        blob = git(self.source, "hash-object", "-w", "--stdin", data=b"../../outside")
        git(self.source, "update-index", "--add", "--cacheinfo", f"120000,{blob},Assets/Scripts/Link.cs")
        git(self.source, "commit", "-m", "symlink fixture")
        with self.assertRaisesRegex(mirror.MirrorError, "symlink"):
            self.plan("symlink")
        tiny = copy.deepcopy(self.config)
        tiny["profile"]["max_file_bytes"] = 1
        with self.assertRaisesRegex(mirror.MirrorError, "max_file_bytes"):
            self.plan("too-large", tiny)

    def test_export_only_cannot_publish_and_credentials_are_not_echoed(self) -> None:
        output, _ = self.plan(export_only=True)
        with self.assertRaisesRegex(mirror.MirrorError, "export-only"):
            mirror.publish_plan(output / "plan.json")
        with self.assertRaises(mirror.MirrorError) as caught:
            mirror.repository_identity("https://user:secret-example@git.example/repo.git")
        self.assertNotIn("secret-example", str(caught.exception))
        failure = subprocess.CompletedProcess([], 128, b"", b"fatal: authentication failed for https://user:secret-example@git.example/repo.git")
        self.assertNotIn("secret-example", str(mirror.git_failure("fixture", failure)))

    def test_targets_have_independent_receipts_and_failures(self) -> None:
        second = self.root / "second.git"
        second.mkdir()
        git(second, "init", "--bare", "--object-format=sha1")
        config = copy.deepcopy(self.config)
        config["targets"]["second"] = {"url": str(second), "branch": "code-mirror"}
        output, _ = self.plan(config=config)
        git(self.source, "push", str(self.target), "HEAD:refs/heads/code-mirror")
        receipt = mirror.publish_plan(output / "plan.json")
        self.assertEqual(receipt["targets"]["fixture"]["status"], "failed")
        self.assertEqual(receipt["targets"]["second"]["status"], "published")
        self.assertEqual(json.loads((output / "publish-receipt.json").read_text())["targets"], receipt["targets"])


if __name__ == "__main__":
    unittest.main()
