"""Offline regression tests for migrate_weapon_contact_storage.py."""

import hashlib
import json
import sys
import tempfile
import unittest
import zlib
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
import migrate_weapon_contact_storage as m  # noqa: E402


ROOT = Path(__file__).resolve().parents[2]
REVISION = "773fb28c3"
PACKAGES = ("000001", "000002", "000004", "000008")


def payload_signature(data, package_id, contacts):
    reader = m.Reader(data)
    if reader.int32() != m.MAGIC or reader.u8() != m.V3 or reader.boolean() != contacts:
        raise AssertionError("expected v3 payload")
    raw_length = reader.int32()
    digest = reader.take(32)
    compressed = reader.take(reader.int32())
    raw = zlib.decompress(compressed, -15)
    if len(raw) != raw_length or hashlib.sha256(raw).digest() != digest:
        raise AssertionError("invalid v3 checksum")
    body = m.Reader(raw)
    if body.text() != package_id:
        raise AssertionError("invalid package identity")
    signature = []
    for _ in range(body.unsigned(20_000)):
        clip, trace = body.text(), body.unsigned()
        profile = m.read_profile(body)
        stored_hash = body.text()
        rows = []
        if contacts:
            for _ in range(body.unsigned(20_000)):
                socket = body.unsigned()
                position = delta = m.Vector(0, 0, 0)
                path = ""
                for _ in range(body.unsigned(m.MAX_RECORDS)):
                    frame = body.unsigned(m.MAX_RECORDS)
                    if not body.boolean():
                        path = body.text()
                    position = body.vector(position)
                    delta = body.vector(delta)
                    rows.append((frame, socket, path, position.__dict__.copy(), delta.__dict__.copy()))
            calculated = m.hash_artifact(profile, contacts=[m.SocketFrame(x[0], "", x[2], m.Vector(**x[3]), m.Vector(**x[4]), x[1]) for x in rows])
        else:
            root = tip = m.Vector(0, 0, 0)
            for _ in range(body.unsigned(m.MAX_RECORDS)):
                frame, frame_trace, socket = body.unsigned(m.MAX_RECORDS), body.unsigned(), body.unsigned()
                same = body.boolean()
                if not same:
                    root, tip = body.vector(root), body.vector(tip)
                root_prev, tip_prev = root, tip
                root, tip = body.vector(root), body.vector(tip)
                rows.append((frame, frame_trace, socket, root_prev.__dict__.copy(), tip_prev.__dict__.copy(), root.__dict__.copy(), tip.__dict__.copy()))
            calculated = m.hash_artifact(profile, weapons=[m.WeaponFrame(x[0], "", "", m.Vector(**x[3]), m.Vector(**x[4]), m.Vector(**x[5]), m.Vector(**x[6]), x[1], x[2]) for x in rows])
        if calculated != stored_hash:
            raise AssertionError(f"numeric artifact hash mismatch for {clip}")
        if contacts:
            rows.sort(key=lambda x: (x[0], x[1], x[2]))
        signature.append((clip, trace, rows))
    if body.pos != len(raw):
        raise AssertionError("trailing v3 bytes")
    return signature


class MigrationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.ids = m.parse_id_catalog(ROOT / "Tools/RuntimeHash/character_definition_ids.json")

    def test_all_eight_payloads_preserve_frames_and_recompute_hashes(self):
        for package_id in PACKAGES:
            package = ROOT / "Assets/MxFrameworkGenerated/CharacterPackages" / package_id
            index = json.loads(m.read_source(package, REVISION, m.INDEX_RELATIVE))
            weapon = m.unpack_v2(m.read_source(package, REVISION, m.WEAPON_RELATIVE), package_id, False)
            contact = m.unpack_v2(m.read_source(package, REVISION, m.CONTACT_RELATIVE), package_id, True)
            artifacts = {}
            for source in (weapon, contact):
                for artifact in source.artifacts:
                    target = artifacts.setdefault((artifact.clip_key, artifact.trace_key), artifact)
                    if target is not artifact:
                        target.weapon_frames.extend(artifact.weapon_frames)
                        target.socket_frames.extend(artifact.socket_frames)
            for artifact in artifacts.values():
                artifact.trace_id = m.lookup(self.ids, m.TRACE_DOMAIN, artifact.trace_key)
                for frame in artifact.weapon_frames:
                    self.assertEqual(frame.trace_id, artifact.trace_id)
                    frame.socket_id = m.lookup(self.ids, m.SOCKET_DOMAIN, frame.socket_key)
                for frame in artifact.socket_frames:
                    frame.socket_id = m.lookup(self.ids, m.SOCKET_DOMAIN, frame.socket_key)
            catalog = m.Catalog(package_id, index["sampleTickRate"], index["quantizationScale"], list(artifacts.values()))
            weapon_bytes = m.encode_v3(catalog, False, self.ids)
            contact_bytes = m.encode_v3(catalog, True, self.ids)
            expected_weapon = []
            expected_contact = []
            for artifact in sorted(artifacts.values(), key=lambda x: (x.clip_key, x.trace_id)):
                weapon_rows = [(x.local_frame, x.trace_id, x.socket_id,
                                x.root_prev.__dict__.copy(), x.tip_prev.__dict__.copy(),
                                x.root_now.__dict__.copy(), x.tip_now.__dict__.copy())
                               for x in sorted(artifact.weapon_frames, key=lambda x: x.local_frame)]
                contact_rows = [(x.local_frame, x.socket_id, x.socket_path,
                                 x.position.__dict__.copy(), x.delta_position.__dict__.copy())
                                for x in sorted(artifact.socket_frames, key=lambda x: (x.local_frame, x.socket_id, x.socket_path))]
                if weapon_rows:
                    expected_weapon.append((artifact.clip_key, artifact.trace_id, weapon_rows))
                if contact_rows:
                    expected_contact.append((artifact.clip_key, artifact.trace_id, contact_rows))
            self.assertEqual(expected_weapon, payload_signature(weapon_bytes, package_id, False))
            self.assertEqual(expected_contact, payload_signature(contact_bytes, package_id, True))

    def test_idempotent_v3_verification_and_no_new_ids(self):
        package = ROOT / "Assets/MxFrameworkGenerated/CharacterPackages/000001"
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / "generated").mkdir()
            index = json.loads(m.read_source(package, REVISION, m.INDEX_RELATIVE))
            (root / m.INDEX_RELATIVE).write_text(json.dumps(index), encoding="utf-8")
            (root / m.WEAPON_RELATIVE).write_bytes(m.read_source(package, REVISION, m.WEAPON_RELATIVE))
            (root / m.CONTACT_RELATIVE).write_bytes(m.read_source(package, REVISION, m.CONTACT_RELATIVE))
            self.assertRaises(m.MigrationError, m.migrate_package, package, {**self.ids, m.SOCKET_DOMAIN: {}}, REVISION, False)
            catalog = m.unpack_v2(m.read_source(package, REVISION, m.WEAPON_RELATIVE), "000001", False)
            for artifact in catalog.artifacts:
                artifact.trace_id = m.lookup(self.ids, m.TRACE_DOMAIN, artifact.trace_key)
                for frame in artifact.weapon_frames:
                    frame.trace_id = artifact.trace_id
                    frame.socket_id = m.lookup(self.ids, m.SOCKET_DOMAIN, frame.socket_key)
            output = m.encode_v3(m.Catalog("000001", 60, 1000000, catalog.artifacts), False, self.ids)
            contact = m.encode_v3(m.Catalog("000001", 60, 1000000, []), True, self.ids)
            (root / m.WEAPON_RELATIVE).write_bytes(output)
            (root / m.CONTACT_RELATIVE).write_bytes(contact)
            v3 = {"format": m.FORMAT_V3, "packageId": "000001", "sampleTickRate": 60, "quantizationScale": 1000000,
                  "weapons": m.descriptor(m.WEAPON_RELATIVE, output), "contacts": m.descriptor(m.CONTACT_RELATIVE, contact), "artifacts": []}
            (root / m.INDEX_RELATIVE).write_text(json.dumps(v3), encoding="utf-8")
            self.assertEqual(m.migrate_package(root, self.ids)["status"], "already-v3")


if __name__ == "__main__":
    unittest.main()
