#!/usr/bin/env python3
"""Migrate CharacterWeaponTraceBakeStorage v2 payloads to v3.

The wire format here intentionally mirrors
``CharacterWeaponTraceBakeStorage.cs``.  v2 stores trace and socket source
keys as UTF-8 strings; v3 stores their active numeric IDs.  This utility is
offline and has no Unity dependency.  It can read old payloads directly from
Git (``--source-revision``), which is useful when the checked-out package has
already been regenerated.
"""

from __future__ import annotations

import argparse
import copy
import hashlib
import json
import struct
import subprocess
import sys
import zlib
from dataclasses import dataclass, field
from pathlib import Path
from typing import BinaryIO, Iterable, Mapping, Sequence


MAGIC = 0x3242574D
V2 = 2
V3 = 3
MAX_PAYLOAD = 64 * 1024 * 1024
MAX_RECORDS = 1_000_000
TRACE_DOMAIN = 403
SOCKET_DOMAIN = 400
FORMAT_V2 = "mx.character.weaponTraceBakeCatalog.v2"
FORMAT_V3 = "mx.character.weaponTraceBakeCatalog.v3"
WEAPON_RELATIVE = "generated/weapon_trace_bake_catalog.bytes"
CONTACT_RELATIVE = "generated/contact_motion_bake_catalog.bytes"
INDEX_RELATIVE = "generated/weapon_trace_bake_catalog.json"


class MigrationError(ValueError):
    pass


@dataclass
class Vector:
    x: int
    y: int
    z: int


@dataclass
class Profile:
    profile_id: str
    source_clip_id: str
    source_clip_type: str
    source_clip_variant: str
    source_clip_package: str
    source_clip_hash: str
    skeleton_profile_id: str
    skeleton_profile_hash: str
    sample_tick_rate: int
    quantization_scale: int
    coordinate_space: int
    rounding_policy: int
    import_settings_fingerprint: str
    profile_hash: str


@dataclass
class WeaponFrame:
    local_frame: int
    trace_key: str
    socket_key: str
    root_prev: Vector
    tip_prev: Vector
    root_now: Vector
    tip_now: Vector
    trace_id: int = 0
    socket_id: int = 0


@dataclass
class SocketFrame:
    local_frame: int
    socket_key: str
    socket_path: str
    position: Vector
    delta_position: Vector
    socket_id: int = 0


@dataclass
class Artifact:
    clip_key: str
    trace_key: str
    profile: Profile
    weapon_frames: list[WeaponFrame] = field(default_factory=list)
    socket_frames: list[SocketFrame] = field(default_factory=list)
    old_hash: str = ""
    trace_id: int = 0


@dataclass
class Catalog:
    package_id: str
    sample_tick_rate: int
    quantization_scale: int
    artifacts: list[Artifact]


class Reader:
    def __init__(self, value: bytes):
        self.value = value
        self.pos = 0

    def take(self, count: int) -> bytes:
        if count < 0 or self.pos + count > len(self.value):
            raise MigrationError("truncated v2 payload")
        result = self.value[self.pos:self.pos + count]
        self.pos += count
        return result

    def u8(self) -> int:
        return self.take(1)[0]

    def boolean(self) -> bool:
        value = self.u8()
        if value not in (0, 1):
            raise MigrationError("invalid boolean in v2 payload")
        return bool(value)

    def int32(self) -> int:
        return struct.unpack("<i", self.take(4))[0]

    def unsigned(self, limit: int | None = None) -> int:
        result = 0
        for shift in range(0, 70, 7):
            byte = self.u8()
            if shift == 63 and byte > 1:
                raise MigrationError("varint overflow")
            result |= (byte & 0x7F) << shift
            if not byte & 0x80:
                if limit is not None and result > limit:
                    raise MigrationError("v2 count exceeds bounds")
                return result
        raise MigrationError("invalid varint")

    def signed(self) -> int:
        value = self.unsigned()
        return (value >> 1) ^ -(value & 1)

    def text(self, limit: int = 1024 * 1024) -> str:
        count = self.unsigned(limit)
        try:
            return self.take(count).decode("utf-8", errors="strict")
        except UnicodeDecodeError as exc:
            raise MigrationError("invalid UTF-8 in v2 payload") from exc

    def vector(self, previous: Vector) -> Vector:
        return Vector(
            i64(previous.x + self.signed()),
            i64(previous.y + self.signed()),
            i64(previous.z + self.signed()),
        )


class Writer:
    def __init__(self):
        self.value = bytearray()

    def u8(self, value: int) -> None:
        self.value.append(value & 0xFF)

    def boolean(self, value: bool) -> None:
        self.u8(1 if value else 0)

    def int32(self, value: int) -> None:
        self.value.extend(struct.pack("<i", value))

    def unsigned(self, value: int) -> None:
        if value < 0:
            raise MigrationError("negative unsigned value")
        while value >= 128:
            self.u8((value & 0x7F) | 0x80)
            value >>= 7
        self.u8(value)

    def signed(self, value: int) -> None:
        value = i64(value)
        self.unsigned(((value << 1) ^ (value >> 63)) & 0xFFFFFFFFFFFFFFFF)

    def text(self, value: str) -> None:
        encoded = (value or "").encode("utf-8")
        self.unsigned(len(encoded))
        self.value.extend(encoded)

    def vector(self, value: Vector, previous: Vector) -> Vector:
        self.signed(i64(value.x - previous.x))
        self.signed(i64(value.y - previous.y))
        self.signed(i64(value.z - previous.z))
        return value


def i64(value: int) -> int:
    value &= 0xFFFFFFFFFFFFFFFF
    return value - (1 << 64) if value >= (1 << 63) else value


def parse_id_catalog(path: Path) -> dict[int, dict[str, int]]:
    document = json.loads(path.read_text(encoding="utf-8"))
    result: dict[int, dict[str, int]] = {}
    for domain in document.get("domains", []):
        domain_id = int(domain.get("domainId"))
        active: dict[str, int] = {}
        for entry in domain.get("entries", []):
            if entry.get("status") != "active":
                continue
            key = str(entry.get("sourceKey", ""))
            value = int(entry.get("id"))
            if key in active and active[key] != value:
                raise MigrationError(f"duplicate active source key in domain {domain_id}: {key}")
            active[key] = value
        result[domain_id] = active
    for domain_id in (TRACE_DOMAIN, SOCKET_DOMAIN):
        if domain_id not in result:
            raise MigrationError(f"required ID domain is missing: {domain_id}")
    return result


def lookup(mapping: Mapping[int, Mapping[str, int]], domain: int, key: str) -> int:
    try:
        return int(mapping[domain][key])
    except KeyError as exc:
        raise MigrationError(f"no active ID for domain {domain} source key {key!r}; refusing to mint an ID") from exc


def read_profile(reader: Reader) -> Profile:
    profile = Profile(
        reader.text(), reader.text(), reader.text(), reader.text(), reader.text(),
        reader.text(), reader.text(), reader.text(), reader.int32(), reader.int32(),
        reader.int32(), reader.int32(), reader.text(), reader.text())
    if profile.sample_tick_rate <= 0 or profile.quantization_scale <= 0:
        raise MigrationError("invalid v2 bake profile bounds")
    if profile.coordinate_space not in (0, 1) or profile.rounding_policy not in (0, 1, 2):
        raise MigrationError("invalid v2 bake profile enum")
    return profile


def read_weapon_frames(reader: Reader, artifact_trace_key: str) -> list[WeaponFrame]:
    count = reader.unsigned(MAX_RECORDS)
    frames: list[WeaponFrame] = []
    root = Vector(0, 0, 0)
    tip = Vector(0, 0, 0)
    previous_frame = -1
    for _ in range(count):
        frame = reader.unsigned(MAX_RECORDS)
        if frame <= previous_frame:
            raise MigrationError("unordered weapon frame in v2 payload")
        # v2 already carried the sampled frame's numeric trace identity.  The
        # artifact identity remained a source key, so preserve this positive
        # value and validate it against domain 403 during migration.
        trace_id = reader.signed()
        if trace_id <= 0 or trace_id > 0x7FFFFFFF:
            raise MigrationError("invalid positive v2 trace frame ID")
        socket_key = reader.text()
        same_previous = reader.boolean()
        if not same_previous:
            root = reader.vector(root)
            tip = reader.vector(tip)
        root_prev, tip_prev = root, tip
        root = reader.vector(root)
        tip = reader.vector(tip)
        frames.append(WeaponFrame(frame, artifact_trace_key, socket_key, root_prev, tip_prev, root, tip,
                                  trace_id=trace_id))
        previous_frame = frame
    return frames


def read_socket_frames(reader: Reader) -> list[SocketFrame]:
    tracks = reader.unsigned(20_000)
    frames: list[SocketFrame] = []
    seen: set[str] = set()
    for _ in range(tracks):
        socket_key = reader.text()
        if socket_key in seen:
            raise MigrationError(f"duplicate socket track in v2 payload: {socket_key}")
        seen.add(socket_key)
        count = reader.unsigned(MAX_RECORDS - len(frames))
        position = Vector(0, 0, 0)
        delta = Vector(0, 0, 0)
        path = ""
        previous_frame = -1
        for _ in range(count):
            frame = reader.unsigned(MAX_RECORDS)
            if frame <= previous_frame:
                raise MigrationError("unordered socket frame in v2 payload")
            if not reader.boolean():
                path = reader.text()
            position = reader.vector(position)
            delta = reader.vector(delta)
            frames.append(SocketFrame(frame, socket_key, path, position, delta))
            previous_frame = frame
    return frames


def unpack_v2(data: bytes, expected_package: str, contacts: bool) -> Catalog:
    if len(data) < 46 or len(data) > MAX_PAYLOAD:
        raise MigrationError("invalid v2 payload length")
    reader = Reader(data)
    if reader.int32() != MAGIC or reader.u8() != V2 or reader.boolean() != contacts:
        raise MigrationError("v2 header magic/version/kind mismatch")
    raw_length = reader.int32()
    digest = reader.take(32)
    compressed_length = reader.int32()
    compressed = reader.take(compressed_length)
    if reader.pos != len(data) or compressed_length != len(compressed):
        raise MigrationError("v2 compressed length mismatch")
    try:
        raw = zlib.decompress(compressed, -15)
    except zlib.error as exc:
        raise MigrationError("invalid v2 deflate payload") from exc
    if len(raw) != raw_length or hashlib.sha256(raw).digest() != digest:
        raise MigrationError("v2 payload checksum or decoded length mismatch")
    body = Reader(raw)
    package_id = body.text()
    if package_id != expected_package:
        raise MigrationError(f"v2 package identity mismatch: {package_id!r} != {expected_package!r}")
    count = body.unsigned(20_000)
    artifacts: list[Artifact] = []
    seen: set[tuple[str, str]] = set()
    for _ in range(count):
        clip_key, trace_key = body.text(), body.text()
        key = (clip_key, trace_key)
        if not clip_key or key in seen:
            raise MigrationError("missing or duplicate v2 artifact identity")
        seen.add(key)
        profile = read_profile(body)
        artifact_hash = body.text()
        artifact = Artifact(clip_key, trace_key, profile)
        artifact.old_hash = artifact_hash
        if contacts:
            artifact.socket_frames = read_socket_frames(body)
        else:
            artifact.weapon_frames = read_weapon_frames(body, trace_key)
        artifacts.append(artifact)
    if body.pos != len(raw):
        raise MigrationError("trailing v2 payload")
    return Catalog(expected_package, 0, 0, artifacts)


def write_profile(writer: Writer, profile: Profile) -> None:
    for value in (profile.profile_id, profile.source_clip_id, profile.source_clip_type,
                  profile.source_clip_variant, profile.source_clip_package,
                  profile.source_clip_hash, profile.skeleton_profile_id,
                  profile.skeleton_profile_hash):
        writer.text(value)
    for value in (profile.sample_tick_rate, profile.quantization_scale,
                  profile.coordinate_space, profile.rounding_policy):
        writer.int32(value)
    writer.text(profile.import_settings_fingerprint)
    writer.text(profile.profile_hash)


def hash_artifact(profile: Profile, weapons: Sequence[WeaponFrame] = (), contacts: Sequence[SocketFrame] = ()) -> str:
    lines = ["mxanimation.bake.artifact.v2\n",
             f"profileHash={profile.profile_hash}\n",
             f"sourceClipHash={profile.source_clip_hash}\n"]
    for index, frame in enumerate(sorted(weapons, key=lambda f: (f.local_frame, f.trace_id, f.socket_id))):
        lines.extend((f"weapon[{index}]\n", f"localFrame={frame.local_frame}\n",
                      f"traceId={frame.trace_id}\n", f"socketId={frame.socket_id}\n",
                      f"rootPrev={vector_text(frame.root_prev)}\n", f"tipPrev={vector_text(frame.tip_prev)}\n",
                      f"rootNow={vector_text(frame.root_now)}\n", f"tipNow={vector_text(frame.tip_now)}\n"))
    for index, frame in enumerate(sorted(contacts, key=lambda f: (f.local_frame, f.socket_id, f.socket_path))):
        lines.extend((f"socket[{index}]\n", f"localFrame={frame.local_frame}\n",
                      f"socketId={frame.socket_id}\n", f"socketPath={frame.socket_path}\n",
                      f"position={vector_text(frame.position)}\n",
                      f"deltaPosition={vector_text(frame.delta_position)}\n"))
    return "sha256:" + hashlib.sha256("".join(lines).encode("utf-8")).hexdigest()


def vector_text(value: Vector) -> str:
    return f"{value.x},{value.y},{value.z}"


def encode_v3(catalog: Catalog, contacts: bool, mapping: Mapping[int, Mapping[str, int]]) -> bytes:
    artifacts = [a for a in catalog.artifacts if (a.socket_frames if contacts else a.weapon_frames)]
    artifacts.sort(key=lambda a: (a.clip_key, a.trace_id))
    raw_writer = Writer()
    raw_writer.text(catalog.package_id)
    raw_writer.unsigned(len(artifacts))
    for artifact in artifacts:
        raw_writer.text(artifact.clip_key)
        raw_writer.unsigned(artifact.trace_id)
        write_profile(raw_writer, artifact.profile)
        if contacts:
            raw_writer.text(hash_artifact(artifact.profile, contacts=artifact.socket_frames))
            tracks: dict[int, list[SocketFrame]] = {}
            for frame in artifact.socket_frames:
                tracks.setdefault(frame.socket_id, []).append(frame)
            raw_writer.unsigned(len(tracks))
            for socket_id in sorted(tracks):
                raw_writer.unsigned(socket_id)
                track = sorted(tracks[socket_id], key=lambda f: f.local_frame)
                raw_writer.unsigned(len(track))
                position, delta, path = Vector(0, 0, 0), Vector(0, 0, 0), ""
                for frame in track:
                    raw_writer.unsigned(frame.local_frame)
                    same_path = frame.socket_path == path
                    raw_writer.boolean(same_path)
                    if not same_path:
                        raw_writer.text(frame.socket_path)
                        path = frame.socket_path
                    position = raw_writer.vector(frame.position, position)
                    delta = raw_writer.vector(frame.delta_position, delta)
        else:
            raw_writer.text(hash_artifact(artifact.profile, weapons=artifact.weapon_frames))
            frames = sorted(artifact.weapon_frames, key=lambda f: f.local_frame)
            raw_writer.unsigned(len(frames))
            root, tip = Vector(0, 0, 0), Vector(0, 0, 0)
            for frame in frames:
                raw_writer.unsigned(frame.local_frame)
                raw_writer.unsigned(frame.trace_id)
                raw_writer.unsigned(frame.socket_id)
                same_previous = frame.root_prev == root and frame.tip_prev == tip
                raw_writer.boolean(same_previous)
                if not same_previous:
                    root = raw_writer.vector(frame.root_prev, root)
                    tip = raw_writer.vector(frame.tip_prev, tip)
                root = raw_writer.vector(frame.root_now, root)
                tip = raw_writer.vector(frame.tip_now, tip)
    raw = bytes(raw_writer.value)
    if len(raw) > MAX_PAYLOAD:
        raise MigrationError("v3 raw payload exceeds bounds")
    compressed = zlib.compress(raw, 9)[2:-4]  # raw Deflate, matching .NET DeflateStream
    output = bytearray(struct.pack("<IB?i", MAGIC, V3, contacts, len(raw)))
    output.extend(hashlib.sha256(raw).digest())
    output.extend(struct.pack("<i", len(compressed)))
    output.extend(compressed)
    return bytes(output)


def descriptor(relative: str, data: bytes) -> dict[str, object]:
    return {"path": relative, "size": len(data), "sha256": hashlib.sha256(data).hexdigest()}


def atomic_write(path: Path, data: bytes) -> None:
    temporary = path.with_name(f".{path.name}.migration.tmp")
    temporary.write_bytes(data)
    try:
        temporary.replace(path)
    finally:
        if temporary.exists():
            temporary.unlink()


def read_git_file(repo: Path, revision: str, relative: str) -> bytes:
    try:
        return subprocess.check_output(["git", "show", f"{revision}:{relative}"], cwd=repo)
    except subprocess.CalledProcessError as exc:
        raise MigrationError(f"cannot read {relative} from Git revision {revision}") from exc


def read_source(root: Path, revision: str | None, relative: str) -> bytes:
    if revision:
        repo = Path(__file__).resolve().parents[2]
        relative_to_repo = root.resolve().relative_to(repo.resolve()) / relative
        return read_git_file(repo, revision, relative_to_repo.as_posix())
    return (root / relative).read_bytes()


def verify_v3(root: Path, package_id: str) -> dict[str, object]:
    index = json.loads((root / INDEX_RELATIVE).read_text(encoding="utf-8"))
    if index.get("format") != FORMAT_V3 or index.get("packageId") != package_id:
        raise MigrationError("existing index is neither a matching v3 index nor a migratable v2 index")
    for kind, relative in (("weapons", WEAPON_RELATIVE), ("contacts", CONTACT_RELATIVE)):
        data = (root / relative).read_bytes()
        item = index.get(kind)
        if not isinstance(item, dict) or item.get("path") != relative or item.get("size") != len(data):
            raise MigrationError(f"existing v3 {kind} descriptor mismatch")
        if item.get("sha256") != hashlib.sha256(data).hexdigest():
            raise MigrationError(f"existing v3 {kind} checksum mismatch")
    return index


def migrate_package(root: Path, ids: Mapping[int, Mapping[str, int]], source_revision: str | None = None,
                    write: bool = False) -> dict[str, object]:
    index_path = root / INDEX_RELATIVE
    index_bytes = read_source(root, source_revision, INDEX_RELATIVE) if source_revision else index_path.read_bytes()
    existing = json.loads(index_bytes.decode("utf-8"))
    package_id = str(existing.get("packageId", root.name))
    if existing.get("format") == FORMAT_V3 and source_revision is None:
        verify_v3(root, package_id)
        return {"packageId": package_id, "status": "already-v3", "changed": False}
    if existing.get("format") != FORMAT_V2:
        raise MigrationError(f"{index_path}: expected v2 index")
    weapon_v2 = unpack_v2(read_source(root, source_revision, WEAPON_RELATIVE), package_id, False)
    contact_v2 = unpack_v2(read_source(root, source_revision, CONTACT_RELATIVE), package_id, True)
    if weapon_v2.sample_tick_rate or contact_v2.sample_tick_rate:
        raise MigrationError("internal catalog metadata unexpectedly populated")
    all_artifacts: dict[tuple[str, str], Artifact] = {}
    for source in (weapon_v2, contact_v2):
        for artifact in source.artifacts:
            target = all_artifacts.setdefault((artifact.clip_key, artifact.trace_key), artifact)
            if target is not artifact:
                if target.profile != artifact.profile:
                    raise MigrationError(f"profile mismatch between weapon/contact payloads for {artifact.clip_key}")
                target.socket_frames.extend(artifact.socket_frames)
                target.weapon_frames.extend(artifact.weapon_frames)
    for artifact in all_artifacts.values():
        artifact.trace_id = lookup(ids, TRACE_DOMAIN, artifact.trace_key)
        for frame in artifact.weapon_frames:
            expected_trace_id = lookup(ids, TRACE_DOMAIN, frame.trace_key or artifact.trace_key)
            if frame.trace_id != expected_trace_id:
                raise MigrationError(f"trace frame ID {frame.trace_id} disagrees with active source key {frame.trace_key!r}")
            frame.socket_id = lookup(ids, SOCKET_DOMAIN, frame.socket_key)
        for frame in artifact.socket_frames:
            frame.socket_id = lookup(ids, SOCKET_DOMAIN, frame.socket_key)
    catalog = Catalog(package_id, int(existing.get("sampleTickRate", 60)),
                      int(existing.get("quantizationScale", 1_000_000)), list(all_artifacts.values()))
    weapon_bytes = encode_v3(catalog, False, ids)
    contact_bytes = encode_v3(catalog, True, ids)
    artifacts = []
    for artifact in sorted(all_artifacts.values(), key=lambda a: (a.clip_key, a.trace_id)):
        if not artifact.weapon_frames:
            continue
        artifacts.append({"clipKey": artifact.clip_key, "traceId": artifact.trace_id,
                          "frameCount": len(artifact.weapon_frames),
                          "artifactHash": hash_artifact(artifact.profile, weapons=artifact.weapon_frames),
                          "traceLabel": artifact.trace_key})
    output_index = {"format": FORMAT_V3, "packageId": package_id,
                    "sampleTickRate": catalog.sample_tick_rate,
                    "quantizationScale": catalog.quantization_scale,
                    "weapons": descriptor(WEAPON_RELATIVE, weapon_bytes),
                    "contacts": descriptor(CONTACT_RELATIVE, contact_bytes),
                    "artifacts": artifacts}
    changed = True
    if write:
        atomic_write(root / WEAPON_RELATIVE, weapon_bytes)
        atomic_write(root / CONTACT_RELATIVE, contact_bytes)
        atomic_write(root / INDEX_RELATIVE,
                     (json.dumps(output_index, indent=2, ensure_ascii=False) + "\n").encode("utf-8"))
    return {"packageId": package_id, "status": "converted", "changed": changed,
            "weaponBytes": len(weapon_bytes), "contactBytes": len(contact_bytes),
            "artifactCount": len(artifacts), "index": output_index}


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--package-root", action="append", type=Path, required=True)
    parser.add_argument("--id-catalog", type=Path, default=Path(__file__).with_name("character_definition_ids.json"))
    parser.add_argument("--source-revision", help="read v2 index and bytes from this Git revision")
    parser.add_argument("--write", action="store_true", help="publish converted bytes and index")
    parser.add_argument("--report", type=Path, help="write a JSON report")
    args = parser.parse_args(argv)
    try:
        ids = parse_id_catalog(args.id_catalog)
        results = [migrate_package(root, ids, args.source_revision, args.write) for root in args.package_root]
        report = {"tool": "migrate_weapon_contact_storage", "sourceRevision": args.source_revision,
                  "write": args.write, "packages": results}
        if args.report:
            args.report.write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
        print(json.dumps(report, indent=2, ensure_ascii=False))
        return 0
    except (OSError, json.JSONDecodeError, MigrationError) as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
