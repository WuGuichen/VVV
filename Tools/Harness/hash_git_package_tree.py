#!/usr/bin/env python3
"""Hash one package subpath from immutable Git blob content."""

from __future__ import annotations

import argparse
import hashlib
import pathlib
import subprocess


def git(repository: pathlib.Path, *args: str, input_bytes: bytes | None = None) -> bytes:
    result = subprocess.run(
        ["git", "-C", str(repository), *args],
        input=input_bytes,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=False,
    )
    if result.returncode != 0:
        raise RuntimeError(result.stderr.decode("utf-8", errors="replace").strip())
    return result.stdout


def package_tree_hash(repository: pathlib.Path, revision: str, subpath: str) -> str:
    normalized = pathlib.PurePosixPath(subpath.strip("/"))
    if not normalized.parts or any(part in ("", ".", "..") for part in normalized.parts):
        raise ValueError("package subpath must be normalized and repository-relative")
    prefix = normalized.as_posix() + "/"
    listing = git(
        repository,
        "ls-tree",
        "-r",
        "-z",
        "--full-tree",
        revision,
        "--",
        normalized.as_posix(),
    )
    records: list[tuple[str, str]] = []
    for raw in listing.split(b"\0"):
        if not raw:
            continue
        metadata, raw_path = raw.split(b"\t", 1)
        fields = metadata.decode("ascii").split()
        path = raw_path.decode("utf-8", errors="surrogateescape")
        if len(fields) != 3 or fields[1] != "blob" or not path.startswith(prefix):
            raise RuntimeError(f"unexpected git tree entry: {raw!r}")
        records.append((path[len(prefix):], fields[2]))
    if not records:
        raise RuntimeError("package subpath has no Git blobs")

    requests = b"".join((oid + "\n").encode("ascii") for _, oid in records)
    batch = git(repository, "cat-file", "--batch", input_bytes=requests)
    offset = 0
    digest = hashlib.sha256()
    for relative, expected_oid in records:
        header_end = batch.index(b"\n", offset)
        header = batch[offset:header_end].decode("ascii")
        fields = header.split()
        if len(fields) != 3 or fields[0] != expected_oid or fields[1] != "blob":
            raise RuntimeError(f"unexpected git blob response: {header}")
        size = int(fields[2])
        start = header_end + 1
        end = start + size
        payload = batch[start:end]
        if batch[end:end + 1] != b"\n":
            raise RuntimeError(f"missing git blob terminator for {relative}")
        offset = end + 1
        digest.update(relative.encode("utf-8", errors="surrogateescape"))
        digest.update(b"\0")
        digest.update(payload)
        digest.update(b"\0")
    return digest.hexdigest()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repository", type=pathlib.Path, required=True)
    parser.add_argument("--revision", required=True)
    parser.add_argument("--package-subpath", required=True)
    args = parser.parse_args()
    repository = args.repository.resolve()
    resolved = git(repository, "rev-parse", args.revision).decode("ascii").strip()
    if len(resolved) != 40:
        raise RuntimeError("revision did not resolve to a full commit")
    print(package_tree_hash(repository, resolved, args.package_subpath))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
