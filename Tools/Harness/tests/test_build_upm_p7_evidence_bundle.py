import copy
import hashlib
import json
import sys
import tempfile
import unittest
from pathlib import Path


HARNESS_ROOT = Path(__file__).resolve().parents[1]
TEST_ROOT = Path(__file__).resolve().parent
if str(HARNESS_ROOT) not in sys.path:
    sys.path.insert(0, str(HARNESS_ROOT))
if str(TEST_ROOT) not in sys.path:
    sys.path.insert(0, str(TEST_ROOT))

from build_upm_p7_evidence_bundle import (  # noqa: E402
    MATRIX_NAME,
    RECEIPT_NAME,
    build_evidence_bundle,
)
from test_upm_consumer_evidence import valid_matrix, valid_profile, valid_receipt  # noqa: E402
from upm_consumer_evidence import (  # noqa: E402
    EvidenceValidationError,
    validate_artifact_hashes,
    validate_matrix_report,
    validate_receipt_candidate,
)


def write_pretty_json(path, document):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(document, indent=2), encoding="utf-8")


def prepare_source(root: Path):
    matrix = valid_matrix()
    receipt = valid_receipt()
    for row in matrix["requiredRows"]:
        profile = valid_profile(row["sourceMode"], row["profile"])
        artifact = row["id"].encode("utf-8")
        artifact_name = f"{row['id']}.bin"
        profile["artifacts"] = [{
            "id": artifact_name,
            "type": "other",
            "sha256": hashlib.sha256(artifact).hexdigest(),
            "externalStorageReference": f"consumer-output:{artifact_name}",
        }]
        report_path = root / row["report"]
        write_pretty_json(report_path, profile)
        (report_path.parent / artifact_name).write_bytes(artifact)
        row["reportSha256"] = hashlib.sha256(report_path.read_bytes()).hexdigest()
    write_pretty_json(root / MATRIX_NAME, matrix)
    matrix_hash = hashlib.sha256((root / MATRIX_NAME).read_bytes()).hexdigest()
    receipt["candidate"] = copy.deepcopy(matrix["candidate"])
    receipt["matrix"] = {"report": MATRIX_NAME, "sha256": matrix_hash}
    receipt["reports"] = [
        {
            "id": row["id"],
            "report": row["report"],
            "sha256": row["reportSha256"],
            "passed": row["passed"],
        }
        for row in matrix["requiredRows"]
    ]
    write_pretty_json(root / RECEIPT_NAME, receipt)


class EvidenceBundleBuilderTests(unittest.TestCase):
    def test_bundle_is_canonical_and_self_verifies_from_its_own_root(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / "source"
            output = root / "bundle"
            prepare_source(source)

            hashes = build_evidence_bundle(source, output)
            matrix = json.loads((output / MATRIX_NAME).read_text(encoding="utf-8"))
            receipt = json.loads((output / RECEIPT_NAME).read_text(encoding="utf-8"))

            self.assertEqual([], validate_matrix_report(matrix))
            self.assertEqual([], validate_receipt_candidate(receipt))
            self.assertEqual([], validate_artifact_hashes(matrix, "matrix", output))
            self.assertEqual([], validate_artifact_hashes(receipt, "receipt", output))
            self.assertEqual(
                hashlib.sha256((output / MATRIX_NAME).read_bytes()).hexdigest(),
                hashes[MATRIX_NAME],
            )
            self.assertEqual(
                hashlib.sha256((output / RECEIPT_NAME).read_bytes()).hexdigest(),
                hashes[RECEIPT_NAME],
            )
            self.assertTrue(all(row["report"].startswith("profiles/") for row in matrix["requiredRows"]))
            self.assertTrue((output / MATRIX_NAME).read_bytes().endswith(b"\n"))

    def test_repeat_build_has_identical_bytes_and_hash_index(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / "source"
            first = root / "first"
            second = root / "second"
            prepare_source(source)

            first_hashes = build_evidence_bundle(source, first)
            second_hashes = build_evidence_bundle(source, second)

            self.assertEqual(first_hashes, second_hashes)
            for relative_path in [*first_hashes, "reports.sha256"]:
                self.assertEqual(
                    (first / relative_path).read_bytes(),
                    (second / relative_path).read_bytes(),
                )

    def test_rejects_corrupt_profile_artifact_before_copy(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / "source"
            prepare_source(source)
            (source / "profiles" / "local-base.json").parent.mkdir(exist_ok=True)
            report_path = source / "profiles" / "local-base.json"
            # The fixture matrix uses this path; replace its neighboring proof.
            report_path.parent.joinpath("local-base.bin").write_bytes(b"corrupt")
            with self.assertRaises(EvidenceValidationError):
                build_evidence_bundle(source, root / "bundle")

    def test_rejects_receipt_matrix_report_mismatch(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / "source"
            prepare_source(source)
            receipt = json.loads((source / RECEIPT_NAME).read_text(encoding="utf-8"))
            receipt["matrix"]["report"] = "other-matrix.json"
            write_pretty_json(source / RECEIPT_NAME, receipt)
            with self.assertRaises(EvidenceValidationError):
                build_evidence_bundle(source, root / "bundle")


if __name__ == "__main__":
    unittest.main()
