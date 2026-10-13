#!/usr/bin/env python3
"""Export a pinned Git code snapshot; publish only an explicitly supplied plan."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import subprocess
import sys
import tempfile
from datetime import datetime, timezone
from pathlib import Path
from typing import Any
from urllib.parse import urlsplit


TOOL = "wgameframework-code-mirror"
VERSION = 1
MARKER = ".code-mirror.json"
NOTICE = "CODE_MIRROR.md"
ROOT = Path(__file__).resolve().parents[2]
DEFAULT_CONFIG = Path(__file__).with_name("code-mirror.json")
BLOCKED_PARTS = {
    ".git", ".agent", ".agents", ".agentsroom", ".codex", ".mcp", ".mirror-local",
    "library", "temp", "logs", "usersettings", "node_modules", "__pycache__",
    ".pytest_cache", ".venv", "venv", "bin", "obj", "credentials", ".ssh",
}
RESOURCE_EXTENSIONS = {
    ".png", ".jpg", ".jpeg", ".tga", ".psd", ".psb", ".svg", ".exr", ".hdr",
    ".tif", ".tiff", ".gif", ".webp", ".fbx", ".blend", ".obj", ".dae", ".glb",
    ".gltf", ".wav", ".mp3", ".ogg", ".flac", ".aiff", ".bank", ".otf", ".ttf",
    ".unity", ".prefab", ".asset", ".mat", ".anim", ".controller", ".playable",
    ".overridecontroller", ".physicmaterial", ".physicsmaterial2d", ".inputactions",
    ".dll", ".exe", ".so", ".dylib", ".a", ".lib", ".pdb", ".pyc", ".zip",
    ".7z", ".rar", ".tar", ".gz", ".unitypackage", ".assetbundle", ".mp4", ".mov",
}
DEVICE = re.compile(r"^(con|prn|aux|nul|com[1-9]|lpt[1-9])(?:\.|$)", re.I)
OID = re.compile(r"^[0-9a-f]{40}$")
LFS_PREFIX = b"version https://git-lfs.github.com/spec/v1"


class MirrorError(RuntimeError):
    pass


def canonical(value: Any) -> bytes:
    return (json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":")) + "\n").encode("utf-8")


def digest(value: Any) -> str:
    return hashlib.sha256(canonical(value)).hexdigest()


def write_json(path: Path, value: Any) -> None:
    path.write_text(json.dumps(value, ensure_ascii=False, sort_keys=True, indent=2) + "\n", encoding="utf-8")


def safe_path(value: str) -> str:
    parts = value.split("/")
    if not value or any(not part or part in {".", ".."} or part[-1:] in {".", " "} or DEVICE.match(part) for part in parts):
        raise MirrorError("A selected path cannot be represented safely on Windows.")
    if any(ord(c) < 32 or c in '\\:<>"|?*' for c in value) or any(part.casefold() == ".git" for part in parts):
        raise MirrorError("A selected path contains a reserved character or Git directory.")
    return value


def blocked(path: str) -> bool:
    parts = [part.casefold() for part in path.split("/")]
    return any(part in BLOCKED_PARTS or part.startswith((".env", ".agent", ".codex", ".mcp")) for part in parts) or parts[-1] in {
        "id_rsa", "id_ed25519", "credentials.json", "auth.json",
    } or Path(path).suffix.casefold() in {".pem", ".key", ".pfx", ".p12"}


def repository_identity(url: str) -> str:
    """Reject inline secrets and unsupported transports, without echoing the URL."""
    if any(ord(c) < 32 for c in url) or url.startswith("-"):
        raise MirrorError("Invalid repository address.")
    if re.match(r"^[A-Za-z]:[\\/]", url) or url.startswith("/"):
        return str(Path(url).resolve()).casefold()
    if "://" in url:
        parsed = urlsplit(url)
        if parsed.scheme not in {"http", "https", "ssh", "file"} or parsed.password or parsed.query or parsed.fragment:
            raise MirrorError("Repository addresses must use Git transports without embedded secrets, queries or fragments.")
        if parsed.scheme in {"http", "https"} and parsed.username:
            raise MirrorError("HTTP repository addresses must not contain credentials.")
        if parsed.scheme == "file":
            raise MirrorError("Use an absolute local path for local fixture repositories.")
        if not parsed.hostname or not parsed.path.strip("/"):
            raise MirrorError("Repository address is missing a host or repository path.")
        return parsed.hostname.casefold() + "/" + parsed.path.strip("/").removesuffix(".git").casefold()
    match = re.fullmatch(r"(?:[A-Za-z0-9_.-]+@)?([A-Za-z0-9_.-]+):([A-Za-z0-9_./-]+)", url)
    if not match:
        raise MirrorError("Use an HTTP(S)/SSH repository address or an absolute local repository path.")
    return match[1].casefold() + "/" + match[2].strip("/").removesuffix(".git").casefold()


def validate_config(config: dict[str, Any]) -> None:
    if config.get("version") != VERSION:
        raise MirrorError("Unsupported configuration version.")
    source = config["source"]
    identity = repository_identity(source["url"])
    if not re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", source["name"]):
        raise MirrorError("Source name must be owner/repository, without an address or credentials.")
    for endpoint in [source, *config["targets"].values()]:
        branch = endpoint["branch"]
        if not re.fullmatch(r"[A-Za-z0-9_][A-Za-z0-9_./-]*", branch) or ".." in branch or "//" in branch or branch.endswith(("/", ".", ".lock")):
            raise MirrorError("Invalid branch name.")
        repository_identity(endpoint["url"])
        if not isinstance(endpoint.get("bypass_proxy", False), bool):
            raise MirrorError("bypass_proxy must be true or false.")
    for name, target in config["targets"].items():
        if not re.fullmatch(r"[A-Za-z0-9_-]+", name):
            raise MirrorError("Target names must use letters, digits, underscores or hyphens.")
        if repository_identity(target["url"]) == identity:
            raise MirrorError("A mirror target must not be the source repository.")
    profile = config["profile"]
    for path in profile.get("exclude_files", []):
        safe_path(path)
    for key in ("roots", "exclude_prefixes"):
        for prefix in profile[key]:
            if not prefix.endswith("/"):
                raise MirrorError(f"{key} entries must end with /.")
            safe_path(prefix[:-1])
    for path in profile["extra_files"]:
        safe_path(path)
        if blocked(path) or Path(path).suffix.casefold() in RESOURCE_EXTENSIONS:
            raise MirrorError("extra_files cannot include credentials, caches or resource files.")
    if any(ext.casefold() in RESOURCE_EXTENSIONS or ext == ".meta" for ext in profile["extensions"]):
        raise MirrorError("Resource files and standalone .meta files cannot be included as code extensions.")
    if type(profile["max_file_bytes"]) is not int or profile["max_file_bytes"] <= 0:
        raise MirrorError("max_file_bytes must be a positive integer.")
    if not isinstance(profile["include_file_meta"], bool):
        raise MirrorError("include_file_meta must be true or false.")


def git_failure(operation: str, result: subprocess.CompletedProcess[bytes]) -> MirrorError:
    # Never print raw Git/helper diagnostics: these can contain credential-bearing URLs.
    diagnostic = result.stderr.lower()
    reasons = (
        (b"could not resolve", "host resolution failed"),
        (b"failed to connect", "connection failed"),
        (b"couldn't connect", "connection failed"),
        (b"authentication", "authentication failed"),
        (b"could not read username", "Git authentication is unavailable in noninteractive mode"),
        (b"certificate", "certificate validation failed"),
        (b"stale info", "the destination changed during publication"),
        (b"rejected", "the destination rejected the update"),
        (b"not found", "repository or ref was not found"),
    )
    reason = next((description for token, description in reasons if token in diagnostic), "Git failed; inspect the configured transport and access")
    return MirrorError(f"{operation}: {reason} (exit {result.returncode}).")


class Git:
    def __init__(self, repo: Path, timeout: int = 180):
        self.repo = repo
        self.timeout = timeout

    def command(self, args: list[str], endpoint: dict[str, Any] | None = None) -> list[str]:
        command = ["git", "-c", f"core.hooksPath={self.repo / 'disabled-hooks'}", "-c", "gc.auto=0", "-c", "protocol.ext.allow=never", "-c", "commit.gpgsign=false"]
        if os.name == "nt":
            # Temporary export prefixes can make valid source paths exceed MAX_PATH.
            command += ["-c", "core.longpaths=true"]
        if endpoint and endpoint.get("bypass_proxy"):
            command += ["-c", "http.proxy="]
        return command + ["-C", str(self.repo)] + args

    def environment(self) -> dict[str, str]:
        env = os.environ.copy()
        for key in ("GIT_DIR", "GIT_WORK_TREE", "GIT_INDEX_FILE", "GIT_OBJECT_DIRECTORY", "GIT_ALTERNATE_OBJECT_DIRECTORIES", "GIT_CONFIG_PARAMETERS", "GIT_CONFIG_COUNT"):
            env.pop(key, None)
        env.update(GIT_TERMINAL_PROMPT="0", GCM_INTERACTIVE="Never", GIT_LFS_SKIP_SMUDGE="1",
                   GIT_AUTHOR_NAME="Code Mirror", GIT_AUTHOR_EMAIL="code-mirror@local.invalid",
                   GIT_COMMITTER_NAME="Code Mirror", GIT_COMMITTER_EMAIL="code-mirror@local.invalid")
        return env

    def run(self, args: list[str], *, operation: str = "Git", data: bytes | None = None, endpoint: dict[str, Any] | None = None) -> bytes:
        try:
            result = subprocess.run(self.command(args, endpoint), input=data, stdout=subprocess.PIPE,
                                    stderr=subprocess.PIPE, env=self.environment(), timeout=self.timeout, check=False)
        except subprocess.TimeoutExpired as exc:
            raise MirrorError(f"{operation}: timed out after {self.timeout} seconds.") from exc
        if result.returncode:
            raise git_failure(operation, result)
        return result.stdout

    def init(self) -> None:
        self.repo.mkdir(parents=True, exist_ok=True)
        self.run(["init", "--bare", "--object-format=sha1", "."], operation="Initialize isolated repository")

    def check_transport(self, endpoint: dict[str, Any]) -> None:
        resolved = self.run(["ls-remote", "--get-url", endpoint["url"]], operation="Resolve configured transport", endpoint=endpoint).decode().strip()
        if resolved.rstrip("/") != endpoint["url"].rstrip("/"):
            raise MirrorError("Git URL rewriting changes the configured repository; configure its exact transport explicitly.")

    def remote_head(self, endpoint: dict[str, Any]) -> str | None:
        self.check_transport(endpoint)
        ref = "refs/heads/" + endpoint["branch"]
        self.run(["check-ref-format", ref], operation="Validate branch")
        lines = self.run(["ls-remote", "--refs", endpoint["url"], ref], operation="Read destination branch", endpoint=endpoint).decode().splitlines()
        if not lines:
            return None
        if len(lines) != 1 or lines[0].split()[1] != ref or not OID.fullmatch(lines[0].split()[0]):
            raise MirrorError("Unexpected destination ref response; only SHA-1 repositories are supported.")
        return lines[0].split()[0]

    def fetch(self, endpoint: dict[str, Any], expected: str | None = None) -> str:
        self.check_transport(endpoint)
        self.run(["fetch", "--depth=1", "--no-tags", endpoint["url"], "refs/heads/" + endpoint["branch"]], operation="Fetch pinned branch", endpoint=endpoint)
        sha = self.run(["rev-parse", "FETCH_HEAD^{commit}"]).decode().strip()
        if not OID.fullmatch(sha) or expected is not None and sha != expected:
            raise MirrorError("The branch changed while it was being inspected; generate a fresh plan.")
        return sha

    def tree(self, sha: str) -> dict[str, dict[str, Any]]:
        result = {}
        for record in self.run(["ls-tree", "-r", "-l", "-z", sha]).split(b"\0"):
            if record:
                header, path = record.split(b"\t", 1)
                mode, kind, oid, size = header.decode("ascii").split()
                result[path.decode("utf-8")] = {"mode": mode, "kind": kind, "oid": oid, "size": int(size) if size != "-" else 0}
        return result


class Blobs:
    """Stream selected objects only, without checkout, filters or LFS downloads."""
    def __init__(self, git: Git):
        self.process = subprocess.Popen(git.command(["cat-file", "--batch"]), stdin=subprocess.PIPE,
                                        stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, env=git.environment())

    def read(self, oid: str, limit: int) -> bytes:
        assert self.process.stdin is not None and self.process.stdout is not None
        self.process.stdin.write(oid.encode("ascii") + b"\n")
        self.process.stdin.flush()
        header = self.process.stdout.readline().split()
        if len(header) != 3 or header[0].decode() != oid or header[1] != b"blob" or int(header[2]) > limit:
            raise MirrorError("Selected object is missing, is not a blob, or exceeds max_file_bytes.")
        size = int(header[2])
        data = self.process.stdout.read(size)
        if len(data) != size or self.process.stdout.read(1) != b"\n":
            raise MirrorError("Incomplete Git object response.")
        return data

    def __enter__(self) -> Blobs:
        return self

    def __exit__(self, *_: Any) -> None:
        if self.process.stdin:
            self.process.stdin.close()
        if self.process.stdout:
            self.process.stdout.close()
        try:
            self.process.wait(timeout=5)
        except subprocess.TimeoutExpired:
            self.process.kill()
            self.process.wait()


def is_selected(path: str, profile: dict[str, Any]) -> bool:
    if blocked(path) or path in profile.get("exclude_files", []) or any(path.casefold().startswith(prefix.casefold()) for prefix in profile["exclude_prefixes"]):
        return False
    if path in profile["extra_files"]:
        return True
    return any(path.startswith(prefix) for prefix in profile["roots"]) and (
        Path(path).suffix in profile["extensions"] or path.rsplit("/", 1)[-1] in profile["filenames"] or path.endswith(".schema.json")
    )


def validate_text(path: str, data: bytes) -> None:
    if data.removeprefix(b"\xef\xbb\xbf").startswith(LFS_PREFIX):
        raise MirrorError(f"Selected code file is an LFS pointer: {path}")
    if data.startswith((b"\xff\xfe", b"\xfe\xff", b"\x00\x00\xfe\xff")):
        try:
            text = data.decode("utf-32" if data.startswith((b"\xff\xfe\x00\x00", b"\x00\x00\xfe\xff")) else "utf-16")
        except UnicodeError as exc:
            raise MirrorError(f"Invalid encoded text: {path}") from exc
        if text.startswith(LFS_PREFIX.decode()) or "\x00" in text:
            raise MirrorError(f"Selected code file is binary or an LFS pointer: {path}")
    elif b"\x00" in data or any(c < 9 or 13 < c < 32 for c in data):
        raise MirrorError(f"Selected code file contains binary data: {path}")


def file_entry(path: str, mode: str, data: bytes) -> dict[str, Any]:
    return {"path": path, "mode": mode, "size": len(data), "sha256": hashlib.sha256(data).hexdigest(),
            "oid": hashlib.sha1(b"blob " + str(len(data)).encode() + b"\0" + data).hexdigest()}


def export_snapshot(git: Git, sha: str, config: dict[str, Any], export: Path) -> tuple[list[dict[str, Any]], dict[str, int]]:
    tree = git.tree(sha)
    profile = config["profile"]
    selected = {path for path in tree if is_selected(path, profile)}
    if profile["include_file_meta"]:
        selected.update(path + ".meta" for path in list(selected) if path + ".meta" in tree and not blocked(path + ".meta")
                        and path + ".meta" not in profile.get("exclude_files", []))
    if not selected:
        raise MirrorError("The whitelist selects no files.")
    if {MARKER.casefold(), NOTICE.casefold(), ".gitattributes"} & {path.casefold() for path in selected}:
        raise MirrorError("The source whitelist includes a reserved generated mirror file.")
    export.mkdir()
    seen: set[str] = set()
    files = []
    with Blobs(git) as blobs:
        for path in sorted(selected):
            safe_path(path)
            folded = path.casefold()
            if folded in seen:
                raise MirrorError("Selected paths collide on a case-insensitive filesystem.")
            seen.add(folded)
            item = tree[path]
            if item["kind"] != "blob" or item["mode"] not in {"100644", "100755"}:
                raise MirrorError(f"Selected path is a symlink or submodule: {path}")
            if item["size"] > profile["max_file_bytes"]:
                raise MirrorError(f"Selected file exceeds max_file_bytes: {path}")
            data = blobs.read(item["oid"], profile["max_file_bytes"])
            validate_text(path, data)
            destination = export / path
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_bytes(data)
            files.append(file_entry(path, item["mode"], data))
    marker = {"tool": TOOL, "version": VERSION, "source_repository": config["source"]["name"],
              "source_branch": config["source"]["branch"], "source_commit": sha,
              "profile_sha256": digest(profile), "content_sha256": digest(files), "source_file_count": len(files)}
    generated = {
        MARKER: canonical(marker),
        ".gitattributes": b"# Code snapshot: preserve original blob bytes and disable LFS filters.\n* -filter -text\n",
        NOTICE: ("# Code snapshot mirror\n\nThis branch contains selected source and documentation from " + config["source"]["name"] +
                 ". The source repository remains authoritative.\n\nResources, original commit history, Issues and pull requests are omitted. "
                 "This snapshot is intended for code browsing and is not a complete runnable Unity project or an asset backup.\n\n"
                 "See `.code-mirror.json` for the pinned source commit and export policy digest.\n").encode("utf-8"),
    }
    for path, data in generated.items():
        (export / path).write_bytes(data)
        files.append(file_entry(path, "100644", data))
    return sorted(files, key=lambda item: item["path"]), {"tracked": len(tree), "selected_source": len(selected), "excluded": len(tree) - len(selected)}


def changes(files: list[dict[str, Any]], tree: dict[str, dict[str, Any]]) -> dict[str, list[str]]:
    desired = {item["path"]: item for item in files}
    return {"added": sorted(desired.keys() - tree.keys()), "deleted": sorted(tree.keys() - desired.keys()),
            "modified": sorted(path for path in desired.keys() & tree.keys() if (desired[path]["mode"], desired[path]["oid"]) != (tree[path]["mode"], tree[path]["oid"]))}


def managed_target(git: Git, sha: str, tree: dict[str, dict[str, Any]], source_name: str) -> bool:
    marker = tree.get(MARKER)
    if not marker or marker["kind"] != "blob" or marker["size"] > 16384:
        return False
    try:
        data = json.loads(git.run(["show", f"{sha}:{MARKER}"]))
        return data.get("tool") == TOOL and data.get("version") == VERSION and data.get("source_repository") == source_name
    except (ValueError, AttributeError, MirrorError):
        return False


def create_plan(config: dict[str, Any], output: Path, names: list[str] | None = None, *, export_only: bool = False, timeout: int = 180) -> dict[str, Any]:
    validate_config(config)
    output = output.resolve()
    if output == ROOT or ROOT in output.parents:
        raise MirrorError("Keep preview/cache output outside the project working tree.")
    if output.exists():
        raise MirrorError("The preview output must be a new directory; existing files are never replaced.")
    selected_names = names if names is not None else list(config["targets"])
    if any(name not in config["targets"] for name in selected_names):
        raise MirrorError("Unknown target name.")
    output.mkdir(parents=True)
    source = Git(output / "source.git", timeout)
    source.init()
    sha = source.fetch(config["source"])
    files, counts = export_snapshot(source, sha, config, output / "export")
    targets = {}
    if not export_only:
        for name in selected_names:
            endpoint = config["targets"][name]
            git = Git(output / "targets" / (name + ".git"), timeout)
            git.init()
            expected = git.remote_head(endpoint)
            tree = {}
            managed = True
            if expected:
                git.fetch(endpoint, expected)
                tree = git.tree(expected)
                managed = managed_target(git, expected, tree, config["source"]["name"])
            targets[name] = {"endpoint": endpoint, "expected_head": expected, "managed": managed, "changes": changes(files, tree)}
    plan = {"tool": TOOL, "version": VERSION, "config": config, "source_commit": sha, "files": files,
            "counts": counts, "targets": targets, "export_only": export_only}
    plan["plan_sha256"] = digest(plan)
    write_json(output / "plan.json", plan)
    report = ["# Code mirror preview", "", f"Source: {config['source']['name']} / {config['source']['branch']} / `{sha}`",
              f"Plan digest: `{plan['plan_sha256']}`", "", f"Files: {len(files)}; bytes: {sum(item['size'] for item in files)}",
              f"Excluded tracked paths: {counts['excluded']}", "", "Inspect `export/` and `plan.json` before publishing.", ""]
    if export_only:
        report += ["Export only: no destination branch was inspected; this plan cannot be published.", ""]
    for name, target in targets.items():
        report += [f"## {name}: {target['endpoint']['branch']}", "", f"Expected head: `{target['expected_head'] or '(new branch)'}`",
                   f"Managed mirror branch: {target['managed']}", ""]
        for kind, paths in target["changes"].items():
            report += [f"### {kind}: {len(paths)}", ""] + [f"- `{path}`" for path in paths] + [""]
    (output / "PREVIEW.md").write_text("\n".join(report), encoding="utf-8")
    return plan


def load_plan(path: Path) -> dict[str, Any]:
    plan = json.loads(path.read_text(encoding="utf-8-sig"))
    recorded = plan.pop("plan_sha256", None)
    if plan.get("tool") != TOOL or plan.get("version") != VERSION or recorded != digest(plan):
        raise MirrorError("Plan identity or digest does not match; generate a fresh preview.")
    plan["plan_sha256"] = recorded
    validate_config(plan["config"])
    if not OID.fullmatch(plan["source_commit"]):
        raise MirrorError("Invalid pinned source commit.")
    for name, target in plan["targets"].items():
        if name not in plan["config"]["targets"] or target["endpoint"] != plan["config"]["targets"][name]:
            raise MirrorError("Plan destination differs from the recorded configuration.")
        if target["expected_head"] is not None and not OID.fullmatch(target["expected_head"]):
            raise MirrorError("Invalid pinned destination commit.")
    return plan


def verify_export(export: Path, files: list[dict[str, Any]]) -> None:
    if not export.is_dir() or export.is_symlink():
        raise MirrorError("Export directory is missing or is a symlink.")
    actual = set()
    for directory, directories, filenames in os.walk(export):
        for name in [*directories, *filenames]:
            if (Path(directory) / name).is_symlink():
                raise MirrorError("Export contains a symlink.")
        actual.update((Path(directory) / name).relative_to(export).as_posix() for name in filenames)
    expected = {item["path"] for item in files}
    if actual != expected or len(expected) != len(files):
        raise MirrorError("Export file list changed after preview.")
    for item in files:
        safe_path(item["path"])
        data = (export / item["path"]).read_bytes()
        if item["mode"] not in {"100644", "100755"} or file_entry(item["path"], item["mode"], data) != item:
            raise MirrorError("Export content changed after preview.")


def publish_target(plan: dict[str, Any], name: str, export: Path, timeout: int) -> dict[str, Any]:
    target = plan["targets"][name]
    if not target["managed"]:
        raise MirrorError("Destination branch is not a managed snapshot branch; choose a new branch.")
    endpoint = target["endpoint"]
    expected = target["expected_head"]
    with tempfile.TemporaryDirectory(prefix="code-mirror-publish-") as directory:
        git = Git(Path(directory) / "mirror.git", timeout)
        git.init()
        current = git.remote_head(endpoint)
        tree = {}
        if current:
            git.fetch(endpoint, current)
            tree = git.tree(current)
        delta = changes(plan["files"], tree)
        if current and not any(delta.values()) and managed_target(git, current, tree, plan["config"]["source"]["name"]):
            return {"status": "unchanged", "commit": current}
        if current != expected:
            raise MirrorError("Destination branch changed after preview; generate a fresh plan.")
        # Hash the reviewed bytes without attributes/filters, then create a fresh index.
        filenames = b"".join((json.dumps((export / item["path"]).as_posix(), ensure_ascii=False) + "\n").encode("utf-8") for item in plan["files"])
        oids = git.run(["hash-object", "-w", "--no-filters", "--stdin-paths"], data=filenames).decode().splitlines()
        if oids != [item["oid"] for item in plan["files"]]:
            raise MirrorError("Export changed while Git objects were being written.")
        index = b"".join(f"{item['mode']} {item['oid']}\t{item['path']}\0".encode("utf-8") for item in plan["files"])
        git.run(["update-index", "-z", "--index-info"], data=index)
        tree_sha = git.run(["write-tree"]).decode().strip()
        args = ["commit-tree", tree_sha] + (["-p", expected] if expected else [])
        message = f"Code snapshot {plan['source_commit'][:12]}\n\nSource-Commit: {plan['source_commit']}\nPlan-SHA256: {plan['plan_sha256']}\n"
        commit = git.run(args, data=message.encode()).decode().strip()
        ref = "refs/heads/" + endpoint["branch"]
        if git.remote_head(endpoint) != expected:
            raise MirrorError("Destination branch changed while the snapshot was built; generate a fresh plan.")
        # New independent branches use an ordinary push. Existing managed branches
        # use an exact lease, with the reviewed head as parent: no history rewrite.
        push = ["push", "--porcelain"]
        if expected:
            push.append(f"--force-with-lease={ref}:{expected}")
        git.run(push + [endpoint["url"], f"{commit}:{ref}"],
                operation="Publish snapshot to the reviewed destination", endpoint=endpoint)
        verified = git.remote_head(endpoint)
        if verified != commit:
            raise MirrorError("Push finished but the published head could not be confirmed; check the destination before retrying.")
        return {"status": "published", "commit": commit}


def publish_plan(path: Path, names: list[str] | None = None, *, timeout: int = 180) -> dict[str, Any]:
    path = path.resolve()
    plan = load_plan(path)
    if plan["export_only"] or not plan["targets"]:
        raise MirrorError("An export-only plan cannot be published; generate a plan that inspects the destination.")
    export = path.parent / "export"
    verify_export(export, plan["files"])
    selected_names = names if names is not None else list(plan["targets"])
    if not selected_names or any(name not in plan["targets"] for name in selected_names):
        raise MirrorError("Target was not included in this preview plan.")
    receipt = {"plan_sha256": plan["plan_sha256"], "source_commit": plan["source_commit"],
               "checked_at_utc": datetime.now(timezone.utc).isoformat(), "targets": {}}
    for name in dict.fromkeys(selected_names):
        try:
            verify_export(export, plan["files"])
            receipt["targets"][name] = publish_target(plan, name, export, timeout)
        except MirrorError as exc:
            receipt["targets"][name] = {"status": "failed", "error": str(exc), "verification_required": True}
        write_json(path.parent / "publish-receipt.json", receipt)
    return receipt


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command")
    preview = commands.add_parser("plan", help="Read source/destinations and write a local preview; never push")
    preview.add_argument("--config", type=Path, default=DEFAULT_CONFIG)
    preview.add_argument("--output", type=Path, help="New directory outside the project; default: a new system temporary directory")
    preview.add_argument("--export-only", action="store_true", help="Skip destination reads; produce a preview that cannot be published")
    preview.add_argument("--target", action="append", help="Configured target name; repeat to select multiple targets")
    preview.add_argument("--no-proxy", action="store_true", help="Bypass HTTP proxy for this plan's source/target Git commands only")
    preview.add_argument("--timeout", type=int, default=180)
    publish = commands.add_parser("publish", help="Explicitly publish the exact exported files from a reviewed local plan")
    publish.add_argument("--plan", type=Path, required=True)
    publish.add_argument("--target", action="append")
    publish.add_argument("--timeout", type=int, default=180)
    args = parser.parse_args(argv if argv is not None else sys.argv[1:] or ["plan"])
    if not args.command:
        parser.print_help()
        return 0
    try:
        if args.timeout <= 0:
            raise MirrorError("timeout must be positive.")
        if args.command == "plan":
            config = json.loads(args.config.read_text(encoding="utf-8-sig"))
            if args.no_proxy:
                for endpoint in [config["source"], *config["targets"].values()]:
                    endpoint["bypass_proxy"] = True
            output = args.output
            if output is None:
                # Reserve a unique name; create_plan itself requires a new directory.
                with tempfile.TemporaryDirectory(prefix="code-mirror-preview-") as reserved:
                    output = Path(reserved)
            plan = create_plan(config, output, args.target, export_only=args.export_only, timeout=args.timeout)
            print(f"Preview: {output.resolve() / 'PREVIEW.md'}")
            print(f"Source commit: {plan['source_commit']}; files: {len(plan['files'])}; bytes: {sum(item['size'] for item in plan['files'])}")
            for name, target in plan["targets"].items():
                counts = {kind: len(paths) for kind, paths in target["changes"].items()}
                print(f"{name}: {target['endpoint']['branch']}; managed={target['managed']}; {counts}")
            return 0
        receipt = publish_plan(args.plan, args.target, timeout=args.timeout)
        for name, result in receipt["targets"].items():
            print(f"{name}: {result['status']}; {result.get('commit', result.get('error'))}")
        return int(any(result["status"] == "failed" for result in receipt["targets"].values()))
    except (MirrorError, OSError, ValueError, KeyError, TypeError) as exc:
        # Configuration parsing errors are deliberately generic to avoid echoing secrets.
        print(f"Error: {exc if isinstance(exc, MirrorError) else 'Invalid configuration/plan or local I/O failure.'}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
