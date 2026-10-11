#!/usr/bin/env python3
"""Build a portable, canonical UPM-P7 matrix/receipt evidence bundle."""

from __future__ import annotations

import argparse
import copy
import hashlib
import json
import sys
from pathlib import Path, PurePosixPath
from typing import Any, Mapping

from upm_consumer_evidence import (
    canonical_json_bytes,
    EvidenceValidationError,
    normalize_relative_identifier,
    validate_artifact_hashes,
    validate_matrix_report,
    validate_profile_report,
    validate_receipt_candidate,
)


MATRIX_NAME = "matrix.json"
RECEIPT_NAME = "p7-receipt-candidate.json"
HASH_INDEX_NAME = "reports.sha256"


def _write_bundle_json(path: Path, document: Any) -> str:
    """Write canonical JSON with the repository-standard final newline."""

    payload = canonical_json_bytes(document) + b"\n"
    path.write_bytes(payload)
    return hashlib.sha256(payload).hexdigest()


def _load_json(path: Path) -> Any:
    try:
        with path.open("r", encoding="utf-8-sig") as stream:
            return json.load(stream)
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise EvidenceValidationError((f"{path}: invalid JSON input: {error}",)) from error


def _raise_issues(label: str, issues: list[str]) -> None:
    if issues:
        raise EvidenceValidationError(tuple(f"{label}: {issue}" for issue in issues))


def _referenced_path(root: Path, identifier: str) -> Path:
    try:
        normalized = normalize_relative_identifier(identifier)
    except (TypeError, ValueError) as error:
        raise EvidenceValidationError((f"invalid evidence reference {identifier!r}: {error}",)) from error
    path = root.joinpath(*PurePosixPath(normalized).parts).resolve()
    try:
        path.relative_to(root)
    except ValueError as error:
        raise EvidenceValidationError((f"evidence reference escaped source root: {identifier}",)) from error
    return path


def _candidate_identity(document: Mapping[str, Any]) -> tuple[Any, ...]:
    candidate = document.get("candidate", {})
    return (
        candidate.get("packageName"),
        candidate.get("packageVersion"),
        candidate.get("packageTreeHash"),
    )


def _validate_source_cross_references(
    matrix: Mapping[str, Any],
    receipt: Mapping[str, Any],
    source_root: Path,
) -> dict[str, Mapping[str, Any]]:
    _raise_issues("matrix", validate_matrix_report(matrix))
    _raise_issues("receipt", validate_receipt_candidate(receipt))
    _raise_issues("matrix artifacts", validate_artifact_hashes(matrix, "matrix", source_root))
    _raise_issues("receipt artifacts", validate_artifact_hashes(receipt, "receipt", source_root))

    matrix_reference = receipt.get("matrix", {}).get("report")
    if _referenced_path(source_root, matrix_reference) != source_root / MATRIX_NAME:
        raise EvidenceValidationError((f"receipt matrix reference must resolve to {MATRIX_NAME}",))

    rows = {row["id"]: row for row in matrix["requiredRows"]}
    receipt_reports = {report["id"]: report for report in receipt["reports"]}
    if set(rows) != set(receipt_reports):
        raise EvidenceValidationError(("receipt report ids must exactly match matrix row ids",))
    if _candidate_identity(matrix) != _candidate_identity(receipt):
        raise EvidenceValidationError(("matrix and receipt package identities differ",))
    if matrix["candidate"].get("commit") != receipt["candidate"].get("commit"):
        raise EvidenceValidationError(("matrix and receipt candidate commits differ",))

    profiles: dict[str, Mapping[str, Any]] = {}
    for row_id, row in rows.items():
        receipt_report = receipt_reports[row_id]
        if (
            receipt_report.get("report") != row.get("report")
            or receipt_report.get("sha256") != row.get("reportSha256")
            or receipt_report.get("passed") != row.get("passed")
        ):
            raise EvidenceValidationError((f"{row_id}: receipt and matrix report references differ",))
        report_path = _referenced_path(source_root, row["report"])
        profile = _load_json(report_path)
        _raise_issues(f"profile {row_id}", validate_profile_report(profile))
        _raise_issues(
            f"profile artifacts {row_id}",
            validate_artifact_hashes(profile, "profile", report_path.parent),
        )
        if _candidate_identity(profile) != _candidate_identity(matrix):
            raise EvidenceValidationError((f"{row_id}: profile package identity differs from matrix",))
        if profile.get("execution", {}).get("profile") != row.get("profile"):
            raise EvidenceValidationError((f"{row_id}: profile name differs from matrix row",))
        if profile.get("candidate", {}).get("sourceMode") != row.get("sourceMode"):
            raise EvidenceValidationError((f"{row_id}: source mode differs from matrix row",))
        profiles[row_id] = profile
    return profiles


def build_evidence_bundle(source_root: str | Path, output_root: str | Path) -> dict[str, str]:
    """Canonicalize a validated external P7 root into a self-verifying bundle.

    The output contains eight profile reports at stable paths, followed by a
    matrix and receipt whose raw SHA-256 references are recalculated from those
    canonical bytes. Profile payload artifacts remain external to this compact
    bundle; their source bytes are verified before the reports are copied.
    """

    source = Path(source_root).resolve()
    output = Path(output_root).resolve()
    matrix = _load_json(source / MATRIX_NAME)
    receipt = _load_json(source / RECEIPT_NAME)
    profiles = _validate_source_cross_references(matrix, receipt, source)

    output.mkdir(parents=True, exist_ok=True)
    hashes: dict[str, str] = {}
    matrix_copy = copy.deepcopy(matrix)
    for row in matrix_copy["requiredRows"]:
        relative_path = normalize_relative_identifier(f"profiles/{row['id']}.json")
        destination = _referenced_path(output, relative_path)
        destination.parent.mkdir(parents=True, exist_ok=True)
        digest = _write_bundle_json(destination, profiles[row["id"]])
        row["report"] = relative_path
        row["reportSha256"] = digest
        hashes[relative_path] = digest

    _raise_issues("bundled matrix", validate_matrix_report(matrix_copy))
    matrix_hash = _write_bundle_json(output / MATRIX_NAME, matrix_copy)
    hashes[MATRIX_NAME] = matrix_hash

    receipt_copy = copy.deepcopy(receipt)
    receipt_copy["matrix"] = {"report": MATRIX_NAME, "sha256": matrix_hash}
    receipt_copy["reports"] = [
        {
            "id": row["id"],
            "report": row["report"],
            "sha256": row["reportSha256"],
            "passed": row["passed"],
        }
        for row in matrix_copy["requiredRows"]
    ]
    _raise_issues("bundled receipt", validate_receipt_candidate(receipt_copy))
    receipt_hash = _write_bundle_json(output / RECEIPT_NAME, receipt_copy)
    hashes[RECEIPT_NAME] = receipt_hash

    _raise_issues(
        "bundled matrix artifacts",
        validate_artifact_hashes(matrix_copy, "matrix", output),
    )
    _raise_issues(
        "bundled receipt artifacts",
        validate_artifact_hashes(receipt_copy, "receipt", output),
    )

    index = "".join(f"{digest}  {name}\n" for name, digest in sorted(hashes.items()))
    (output / HASH_INDEX_NAME).write_text(index, encoding="utf-8", newline="\n")
    return dict(sorted(hashes.items()))


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source_root", type=Path, help="external matrix artifact root")
    parser.add_argument("output_root", type=Path, help="portable bundle destination")
    args = parser.parse_args(argv)
    try:
        hashes = build_evidence_bundle(args.source_root, args.output_root)
    except EvidenceValidationError as error:
        for issue in error.issues:
            print(issue, file=sys.stderr)
        return 1
    print(f"built {len(hashes)} canonical JSON files in {args.output_root}")
    print(f"matrix raw SHA-256: {hashes[MATRIX_NAME]}")
    print(f"receipt raw SHA-256: {hashes[RECEIPT_NAME]}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
