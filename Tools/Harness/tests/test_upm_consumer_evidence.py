import copy
import hashlib
import json
import sys
import tempfile
import unittest
from pathlib import Path


HARNESS_ROOT = Path(__file__).resolve().parents[1]
if str(HARNESS_ROOT) not in sys.path:
    sys.path.insert(0, str(HARNESS_ROOT))

from upm_consumer_evidence import (  # noqa: E402
    canonical_json_bytes,
    normalize_relative_identifier,
    redact_document,
    sha256_document,
    validate_artifact_hashes,
    validate_matrix_report,
    validate_profile_report,
    validate_receipt_candidate,
)


HASH_A = "a" * 64
HASH_B = "b" * 64
COMMIT_A = "a" * 40


def valid_profile(source="local", profile="base"):
    commit = COMMIT_A if source == "pinned-git" else None
    integrations = []
    if profile != "base":
        integrations = [{
            "id": "fairygui",
            "profile": "with-fairygui",
            "requested": True,
            "packageName": "com.tuyoogame.yooasset",
            "sourceKind": "git",
            "minimumVersion": "5.2.0",
            "requestedVersion": "5.2.0",
            "validatedVersion": "5.2.0",
            "resolvedSource": "git",
            "resolvedVersion": "5.2.0",
            "resolvedRevision": COMMIT_A,
            "define": "FAIRYGUI",
            "adapterAssemblies": ["MxFramework.UI.FairyGUI"],
            "adapterDlls": [{"name": "MxFramework.UI.FairyGUI.dll", "compiled": True, "sha256": HASH_A}],
            "behaviorTest": "MxFramework.Tests.FairyGui",
            "behaviorPassed": True,
            "supportTier": "O2",
        }]
    return {
        "schemaVersion": 4,
        "reportType": "profile",
        "candidate": {
            "sourceMode": source,
            "requestedCommit": commit,
            "resolvedCommit": commit,
            "packageTreeHash": HASH_B,
            "packageTreeHashAlgorithm": "sha256-path-nul-content-nul-v1",
            "packageName": "com.vincent.wgameframework",
            "packageVersion": "0.8.0-preview.2",
            "resolvedPackageSource": "local" if source == "local" else "git",
            "localSourceDirty": False if source == "local" else None,
        },
        "harness": {
            "repositoryCommit": COMMIT_A,
            "repositoryDirty": False,
            "fixtureHash": HASH_A,
            "runnerHash": HASH_B,
            "runnerVersion": 4,
            "schemaVersion": 4,
        },
        "execution": {
            "profile": profile,
            "unityVersion": "6000.5.0f1",
            "platform": {"operatingSystem": "Windows", "pointerSizeBits": 64, "testPlatform": "EditMode"},
            "cleanLibrary": True,
            "evidenceLevel": "E2-row" if source == "pinned-git" else "E1",
            "eligibleForMatrix": True,
            "result": "passed",
            "failureClassification": None,
        },
        "dependencyResolution": {"source": "local", "optionalPackagesAbsent": profile == "base"},
        "compile": {"passed": True},
        "tests": {
            "expectedFullNames": ["MxFramework.Tests.Consumer.BaseContract"],
            "observedFullNames": ["MxFramework.Tests.Consumer.BaseContract"],
            "total": 1,
            "passed": 1,
            "failed": 0,
            "skipped": 0,
            "complete": True,
        },
        "optionalIntegrations": integrations,
        "player": {
            "buildRequested": False,
            "buildPassed": None,
            "runRequested": False,
            "runPassed": None,
            "buildReportHash": None,
            "runReportHash": None,
            "target": None,
        },
        "artifacts": [],
        "mutationGuard": {
            "policyVersion": 1,
            "allowedChanges": ["artifacts"],
            "packageCandidate": {"beforeHash": HASH_A, "afterHash": HASH_A, "verdict": "passed"},
            "harnessRunner": {"beforeHash": HASH_B, "afterHash": HASH_B, "verdict": "passed"},
            "consumerFixture": {"beforeHash": HASH_A, "afterHash": HASH_A, "verdict": "passed"},
            "generatedConsumerAuthority": {"beforeHash": HASH_B, "afterHash": HASH_B, "verdict": "passed"},
            "verdict": "passed",
        },
        "logPolicy": {
            "policy": "upm-consumer-forbidden-logs",
            "version": "1",
            "patterns": [{"id": "log-error", "pattern": "LogError"}],
            "checkedArtifactIds": [],
            "hits": [],
            "verdict": "passed",
        },
    }


def valid_matrix():
    rows = []
    for row_id, source in (("local-base", "local"), ("pinned-git-base", "pinned-git")):
        rows.append({
            "id": row_id,
            "sourceMode": source,
            "profile": "base",
            "required": True,
            "report": f"profiles/{row_id}.json",
            "reportSha256": HASH_A,
            "passed": True,
            "eligible": True,
            "schemaValid": True,
            "artifactHashValid": True,
            "mutationClean": True,
            "testsComplete": True,
            "logsClean": True,
            "candidateEqual": True,
            "failureReason": None,
        })
    return {
        "schemaVersion": 1,
        "type": "upm-consumer-matrix",
        "evidenceLevel": "E2-matrix",
        "candidate": {
            "commit": COMMIT_A,
            "packageName": "com.vincent.wgameframework",
            "packageVersion": "0.8.0-preview.2",
            "packageTreeHash": HASH_B,
            "workingTreeDirty": False,
        },
        "requiredRowCount": len(rows),
        "requiredRows": rows,
        "candidateEquality": {"passed": True, "commit": COMMIT_A},
        "promotable": True,
        "reasons": [],
    }


def valid_receipt():
    return {
        "schemaVersion": 1,
        "type": "upm-e3-receipt-candidate",
        "candidate": {
            "commit": COMMIT_A,
            "packageTreeHash": HASH_B,
            "packageName": "com.vincent.wgameframework",
            "packageVersion": "0.8.0-preview.2",
            "workingTreeDirty": False,
        },
        "matrix": {"report": "matrix/report.json", "sha256": HASH_B},
        "reports": [
            {"id": "local-base", "report": "profiles/local-base.json", "sha256": HASH_A, "passed": True},
            {"id": "pinned-git-base", "report": "profiles/pinned-git-base.json", "sha256": HASH_A, "passed": True},
        ],
        "promotable": True,
        "releaseReady": False,
        "pendingP8Gates": ["legal", "player-run", "tag", "upgrade"],
    }


class ProfileValidationTests(unittest.TestCase):
    def test_valid_local_and_pinned_optional_profiles(self):
        self.assertEqual([], validate_profile_report(valid_profile()))
        self.assertEqual([], validate_profile_report(valid_profile("pinned-git", "with-fairygui")))

    def test_player_unrequested_requires_null(self):
        report = valid_profile()
        report["player"]["buildPassed"] = False
        self.assertTrue(any("unrequested" in issue for issue in validate_profile_report(report)))

    def test_pinned_revision_mismatch_fails(self):
        report = valid_profile("pinned-git")
        report["candidate"]["resolvedCommit"] = "b" * 40
        self.assertTrue(any("must equal" in issue for issue in validate_profile_report(report)))

    def test_dirty_local_cannot_claim_eligibility(self):
        report = valid_profile()
        report["candidate"]["localSourceDirty"] = True
        self.assertTrue(any("eligibleForMatrix" in issue for issue in validate_profile_report(report)))

    def test_missing_expected_test_is_incomplete(self):
        report = valid_profile()
        report["tests"]["observedFullNames"] = []
        report["tests"]["total"] = 0
        report["tests"]["passed"] = 0
        self.assertTrue(any("complete" in issue for issue in validate_profile_report(report)))

    def test_mutation_guard_fails_closed(self):
        report = valid_profile()
        report["mutationGuard"]["packageCandidate"]["afterHash"] = HASH_B
        self.assertTrue(any("mutation guard" in issue for issue in validate_profile_report(report)))


class MatrixReceiptValidationTests(unittest.TestCase):
    def test_valid_matrix_and_receipt(self):
        self.assertEqual([], validate_matrix_report(valid_matrix()))
        self.assertEqual([], validate_receipt_candidate(valid_receipt()))

    def test_caller_cannot_force_promotable(self):
        matrix = valid_matrix()
        matrix["requiredRows"][0]["testsComplete"] = False
        self.assertTrue(any("promotable" in issue for issue in validate_matrix_report(matrix)))

    def test_matrix_requires_local_and_pinned_sources(self):
        matrix = valid_matrix()
        matrix["requiredRows"] = [matrix["requiredRows"][0]]
        matrix["requiredRowCount"] = 1
        matrix["promotable"] = False
        matrix["reasons"] = ["missing pinned row"]
        self.assertEqual([], validate_matrix_report(matrix))

    def test_receipt_rejects_failed_report_and_release_claim(self):
        receipt = valid_receipt()
        receipt["reports"][0]["passed"] = False
        receipt["releaseReady"] = True
        issues = validate_receipt_candidate(receipt)
        self.assertTrue(any("passed reports" in issue for issue in issues))
        self.assertTrue(any("release readiness" in issue for issue in issues))

    def test_receipt_rejects_absolute_path_and_sensitive_value(self):
        receipt = valid_receipt()
        receipt["matrix"]["report"] = r"C:\Users\Alice\matrix.json"
        receipt["pendingP8Gates"][0] = "token=super-secret"
        issues = validate_receipt_candidate(receipt)
        self.assertTrue(any("relative" in issue for issue in issues))
        self.assertTrue(any("sensitive" in issue for issue in issues))


class StabilityAndArtifactTests(unittest.TestCase):
    def test_canonical_json_and_hash_are_order_independent(self):
        first = {"z": 1, "text": "证据", "a": [2, 3]}
        second = {"a": [2, 3], "text": "证据", "z": 1}
        self.assertEqual(canonical_json_bytes(first), canonical_json_bytes(second))
        self.assertEqual(sha256_document(first), sha256_document(second))
        self.assertFalse(canonical_json_bytes(first).startswith(b"\xef\xbb\xbf"))

    def test_path_normalization_and_traversal(self):
        self.assertEqual("reports/base.json", normalize_relative_identifier(r"reports\.\base.json"))
        with self.assertRaises(ValueError):
            normalize_relative_identifier("../secret.json")
        with self.assertRaises(ValueError):
            normalize_relative_identifier(r"C:\temp\report.json")

    def test_redaction_is_recursive(self):
        redacted = redact_document({"url": "https://user:pass@example.invalid/repo", "path": r"C:\Users\Alice\x", "token": "token=abc"})
        rendered = json.dumps(redacted)
        self.assertNotIn("Alice", rendered)
        self.assertNotIn("pass@", rendered)
        self.assertNotIn("token=abc", rendered)

    def test_artifact_root_verifies_bytes(self):
        matrix = valid_matrix()
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for row in matrix["requiredRows"]:
                path = root / Path(row["report"])
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(b"profile")
                row["reportSha256"] = hashlib.sha256(b"profile").hexdigest()
            self.assertEqual([], validate_artifact_hashes(matrix, "matrix", root))
            (root / "profiles" / "local-base.json").write_bytes(b"changed")
            self.assertTrue(any("mismatch" in issue for issue in validate_artifact_hashes(matrix, "matrix", root)))

    def test_profile_artifact_uses_storage_reference_not_stable_id(self):
        profile = valid_profile()
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            artifact = root / "UnityProject" / "Library" / "Adapter.dll"
            artifact.parent.mkdir(parents=True)
            artifact.write_bytes(b"adapter")
            profile["artifacts"] = [{
                "id": "adapter/Adapter.dll",
                "type": "adapter-assembly",
                "sha256": hashlib.sha256(b"adapter").hexdigest(),
                "externalStorageReference": (
                    "consumer-output:UnityProject/Library/Adapter.dll"
                ),
            }]
            self.assertEqual([], validate_artifact_hashes(profile, "profile", root))

    def test_schema_documents_are_valid_json(self):
        schema_root = HARNESS_ROOT / "Schemas" / "UPM"
        names = {path.name for path in schema_root.glob("*.schema.json")}
        self.assertEqual({"profile-report-v4.schema.json", "matrix-report-v1.schema.json", "receipt-candidate-v1.schema.json"}, names)
        for path in schema_root.glob("*.schema.json"):
            self.assertIsInstance(json.loads(path.read_text(encoding="utf-8")), dict)


if __name__ == "__main__":
    unittest.main()
