#!/usr/bin/env python3
"""Stable serialization and semantic validation for UPM-P7 evidence.

The JSON schema files document the wire format.  This module deliberately uses
only the Python standard library and supplies the cross-field checks that JSON
Schema cannot express (Player tri-state, candidate equality, and promotability).
"""

from __future__ import annotations

import hashlib
import json
import re
from pathlib import Path, PurePosixPath
from typing import Any, Iterable, Mapping, Sequence


PROFILE_TYPE = "upm-consumer-profile"
MATRIX_TYPE = "upm-consumer-matrix"
RECEIPT_TYPE = "upm-e3-receipt-candidate"
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
COMMIT_RE = re.compile(r"^[0-9a-f]{40}$")
FAILURE_CATEGORIES = {
    "product-failure",
    "schema-failure",
    "identity-mismatch",
    "mutation",
    "network",
    "credential",
    "unity-license",
    "infrastructure-unavailable",
}
PROFILE_NAMES = {
    "base",
    "with-fairygui",
    "with-unity-transport",
    "with-vfx-graph",
}
ARTIFACT_TYPES = {
    "profile-report",
    "matrix-report",
    "nunit-xml",
    "unity-log-summary",
    "player-build-report",
    "player-run-report",
    "static-boundary-report",
    "unity-log",
    "adapter-assembly",
    "other",
}


class EvidenceValidationError(ValueError):
    """Raised when an evidence document does not satisfy its contract."""

    def __init__(self, issues: Sequence[str]):
        self.issues = tuple(issues)
        super().__init__("; ".join(self.issues))


def canonical_json_bytes(document: Any) -> bytes:
    """Return deterministic UTF-8 JSON bytes without a BOM or trailing newline."""

    return json.dumps(
        document,
        ensure_ascii=False,
        allow_nan=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")


def sha256_document(document: Any) -> str:
    return hashlib.sha256(canonical_json_bytes(document)).hexdigest()


def write_stable_json(path: str, document: Any) -> str:
    payload = canonical_json_bytes(document)
    with open(path, "wb") as stream:
        stream.write(payload)
    return hashlib.sha256(payload).hexdigest()


def normalize_relative_identifier(value: str) -> str:
    """Normalize an artifact identifier to a portable, traversal-free path."""

    if not isinstance(value, str) or not value.strip():
        raise ValueError("artifact identifier must be a non-empty string")
    candidate = value.strip().replace("\\", "/")
    if candidate.startswith("/") or re.match(r"^[A-Za-z]:", candidate):
        raise ValueError("artifact identifier must be relative")
    parts: list[str] = []
    for part in candidate.split("/"):
        if part in ("", "."):
            continue
        if part == "..":
            raise ValueError("artifact identifier must not traverse parent directories")
        if "\x00" in part:
            raise ValueError("artifact identifier must not contain NUL")
        parts.append(part)
    if not parts:
        raise ValueError("artifact identifier must name an artifact")
    return str(PurePosixPath(*parts))


_SECRET_ASSIGNMENT_RE = re.compile(
    r"(?i)\b(token|password|passwd|secret|license(?:[-_ ]?key)?|unity[-_ ]?serial)"
    r"\s*[:=]\s*([^\s,;]+)"
)
_URL_CREDENTIAL_RE = re.compile(r"(?i)(https?://)([^/@\s:]+):([^/@\s]+)@")
_WINDOWS_USER_PATH_RE = re.compile(r"(?i)\b[A-Z]:\\Users\\[^\\/\s]+")
_POSIX_USER_PATH_RE = re.compile(r"(?<![\w.])/(?:home|Users)/[^/\s]+")


def redact_sensitive_text(value: str) -> str:
    """Redact common credentials and machine-user path prefixes."""

    redacted = _URL_CREDENTIAL_RE.sub(r"\1<redacted>@", value)
    redacted = _SECRET_ASSIGNMENT_RE.sub(lambda m: f"{m.group(1)}=<redacted>", redacted)
    redacted = _WINDOWS_USER_PATH_RE.sub("<user-home>", redacted)
    return _POSIX_USER_PATH_RE.sub("<user-home>", redacted)


def redact_document(value: Any) -> Any:
    if isinstance(value, str):
        return redact_sensitive_text(value)
    if isinstance(value, list):
        return [redact_document(item) for item in value]
    if isinstance(value, dict):
        return {key: redact_document(item) for key, item in value.items()}
    return value


def _mapping(value: Any, path: str, issues: list[str]) -> Mapping[str, Any]:
    if not isinstance(value, dict):
        issues.append(f"{path}: expected object")
        return {}
    return value


def _array(value: Any, path: str, issues: list[str]) -> list[Any]:
    if not isinstance(value, list):
        issues.append(f"{path}: expected array")
        return []
    return value


def _required(obj: Mapping[str, Any], fields: Iterable[str], path: str, issues: list[str]) -> None:
    for field in fields:
        if field not in obj:
            issues.append(f"{path}.{field}: missing required field")


def _is_bool(value: Any) -> bool:
    return isinstance(value, bool)


def _check_hash(value: Any, path: str, issues: list[str]) -> None:
    if not isinstance(value, str) or not SHA256_RE.fullmatch(value):
        issues.append(f"{path}: expected lowercase SHA-256")


def _check_commit(value: Any, path: str, issues: list[str], *, nullable: bool = False) -> None:
    if nullable and value is None:
        return
    if not isinstance(value, str) or not COMMIT_RE.fullmatch(value):
        issues.append(f"{path}: expected full lowercase 40-character commit SHA")


def _check_candidate(candidate: Mapping[str, Any], path: str, issues: list[str]) -> None:
    required = (
        "sourceMode", "requestedCommit", "resolvedCommit", "sourceTreeHash",
        "packageTreeHash", "packageName", "packageVersion", "workingTreeDirty",
    )
    _required(candidate, required, path, issues)
    source = candidate.get("sourceMode")
    if source not in {"local", "pinned-git"}:
        issues.append(f"{path}.sourceMode: expected local or pinned-git")
    if source == "pinned-git":
        _check_commit(candidate.get("requestedCommit"), f"{path}.requestedCommit", issues)
        _check_commit(candidate.get("resolvedCommit"), f"{path}.resolvedCommit", issues)
        if candidate.get("requestedCommit") != candidate.get("resolvedCommit"):
            issues.append(f"{path}: pinned-git requestedCommit must equal resolvedCommit")
    elif source == "local":
        if candidate.get("requestedCommit") is not None or candidate.get("resolvedCommit") is not None:
            issues.append(f"{path}: local commits must be null")
    _check_hash(candidate.get("sourceTreeHash"), f"{path}.sourceTreeHash", issues)
    _check_hash(candidate.get("packageTreeHash"), f"{path}.packageTreeHash", issues)
    if not _is_bool(candidate.get("workingTreeDirty")):
        issues.append(f"{path}.workingTreeDirty: expected boolean")
    for field in ("packageName", "packageVersion"):
        if not isinstance(candidate.get(field), str) or not candidate.get(field):
            issues.append(f"{path}.{field}: expected non-empty string")


def _check_artifacts(artifacts: Any, path: str, issues: list[str]) -> dict[str, Mapping[str, Any]]:
    result: dict[str, Mapping[str, Any]] = {}
    for index, raw in enumerate(_array(artifacts, path, issues)):
        item_path = f"{path}[{index}]"
        item = _mapping(raw, item_path, issues)
        _required(item, ("id", "type", "sha256", "externalStorageReference"), item_path, issues)
        identifier = item.get("id")
        try:
            normalized = normalize_relative_identifier(identifier)
            if normalized != identifier:
                issues.append(f"{item_path}.id: identifier is not normalized")
        except (TypeError, ValueError) as error:
            issues.append(f"{item_path}.id: {error}")
            continue
        if identifier in result:
            issues.append(f"{item_path}.id: duplicate artifact identifier")
        result[identifier] = item
        if item.get("type") not in ARTIFACT_TYPES:
            issues.append(f"{item_path}.type: unsupported artifact type")
        _check_hash(item.get("sha256"), f"{item_path}.sha256", issues)
        external = item.get("externalStorageReference")
        if not isinstance(external, str) or not external.startswith("consumer-output:"):
            issues.append(f"{item_path}.externalStorageReference: expected consumer-output reference")
        else:
            storage_key = external[len("consumer-output:"):]
            try:
                normalized_storage_key = normalize_relative_identifier(storage_key)
                if normalized_storage_key != storage_key:
                    issues.append(
                        f"{item_path}.externalStorageReference: storage key is not normalized"
                    )
            except (TypeError, ValueError) as error:
                issues.append(f"{item_path}.externalStorageReference: {error}")
    return result


def _check_no_sensitive_strings(value: Any, path: str, issues: list[str]) -> None:
    if isinstance(value, str):
        if redact_sensitive_text(value) != value:
            issues.append(f"{path}: contains sensitive or machine-specific text")
        if re.match(r"^(?:[A-Za-z]:[\\/]|/)", value):
            issues.append(f"{path}: absolute paths are forbidden")
    elif isinstance(value, list):
        for index, item in enumerate(value):
            _check_no_sensitive_strings(item, f"{path}[{index}]", issues)
    elif isinstance(value, dict):
        for key, item in value.items():
            _check_no_sensitive_strings(item, f"{path}.{key}", issues)


def validate_profile_report(document: Any) -> list[str]:
    issues: list[str] = []
    root = _mapping(document, "$", issues)
    required = ("schemaVersion", "reportType", "candidate", "harness", "execution",
                "dependencyResolution", "compile", "tests", "optionalIntegrations",
                "player", "artifacts", "mutationGuard", "logPolicy")
    _required(root, required, "$", issues)
    if root.get("schemaVersion") != 4:
        issues.append("$.schemaVersion: expected 4")
    if root.get("reportType") != "profile":
        issues.append("$.reportType: expected profile")
    if "promotable" in root:
        issues.append("$.promotable: profile reports must not declare promotable")

    candidate = _mapping(root.get("candidate"), "$.candidate", issues)
    _required(candidate, ("sourceMode", "requestedCommit", "resolvedCommit", "packageTreeHash",
                          "packageTreeHashAlgorithm", "packageName", "packageVersion",
                          "resolvedPackageSource", "localSourceDirty"), "$.candidate", issues)
    source = candidate.get("sourceMode")
    if source not in {"local", "pinned-git"}:
        issues.append("$.candidate.sourceMode: expected local or pinned-git")
    if source == "pinned-git":
        _check_commit(candidate.get("requestedCommit"), "$.candidate.requestedCommit", issues)
        _check_commit(candidate.get("resolvedCommit"), "$.candidate.resolvedCommit", issues)
        if candidate.get("requestedCommit") != candidate.get("resolvedCommit"):
            issues.append("$.candidate: pinned-git requestedCommit must equal resolvedCommit")
        if candidate.get("localSourceDirty") is not None:
            issues.append("$.candidate.localSourceDirty: pinned-git value must be null")
    else:
        if candidate.get("requestedCommit") is not None or candidate.get("resolvedCommit") is not None:
            issues.append("$.candidate: local commits must be null")
        if not _is_bool(candidate.get("localSourceDirty")):
            issues.append("$.candidate.localSourceDirty: local value must be boolean")
    _check_hash(candidate.get("packageTreeHash"), "$.candidate.packageTreeHash", issues)
    if candidate.get("packageTreeHashAlgorithm") != "sha256-path-nul-content-nul-v1":
        issues.append(
            "$.candidate.packageTreeHashAlgorithm: expected "
            "sha256-path-nul-content-nul-v1"
        )

    harness = _mapping(root.get("harness"), "$.harness", issues)
    _required(harness, ("repositoryCommit", "repositoryDirty", "fixtureHash", "runnerHash", "runnerVersion", "schemaVersion"), "$.harness", issues)
    _check_commit(harness.get("repositoryCommit"), "$.harness.repositoryCommit", issues)
    _check_hash(harness.get("fixtureHash"), "$.harness.fixtureHash", issues)
    _check_hash(harness.get("runnerHash"), "$.harness.runnerHash", issues)
    if not _is_bool(harness.get("repositoryDirty")):
        issues.append("$.harness.repositoryDirty: expected boolean")
    if harness.get("runnerVersion") != 4 or harness.get("schemaVersion") != 4:
        issues.append("$.harness: runnerVersion and schemaVersion must be 4")

    execution = _mapping(root.get("execution"), "$.execution", issues)
    _required(execution, ("profile", "unityVersion", "platform", "cleanLibrary", "evidenceLevel", "eligibleForMatrix", "result", "failureClassification"), "$.execution", issues)
    if execution.get("profile") not in PROFILE_NAMES:
        issues.append("$.execution.profile: unsupported profile")
    expected_level = "E2-row" if source == "pinned-git" else "E1"
    if execution.get("evidenceLevel") != expected_level:
        issues.append(f"$.execution.evidenceLevel: expected {expected_level}")
    for field in ("cleanLibrary", "eligibleForMatrix"):
        if not _is_bool(execution.get(field)):
            issues.append(f"$.execution.{field}: expected boolean")
    if not isinstance(execution.get("platform"), dict):
        issues.append("$.execution.platform: expected object")
    result = execution.get("result")
    if result not in {"passed", "failed"}:
        issues.append("$.execution.result: expected passed or failed")
    failure = execution.get("failureClassification")
    if failure is not None and failure not in FAILURE_CATEGORIES:
        issues.append("$.execution.failureClassification: unsupported failure category")
    if (result == "passed") != (failure is None):
        issues.append("$.execution: result and failureClassification disagree")

    tests = _mapping(root.get("tests"), "$.tests", issues)
    _required(tests, ("expectedFullNames", "observedFullNames", "total", "passed", "failed", "skipped", "complete"), "$.tests", issues)
    expected = _array(tests.get("expectedFullNames"), "$.tests.expectedFullNames", issues)
    observed = _array(tests.get("observedFullNames"), "$.tests.observedFullNames", issues)
    if not expected or any(not isinstance(name, str) or not name for name in expected):
        issues.append("$.tests.expectedFullNames: expected non-empty full test names")
    if len(set(expected)) != len(expected) or len(set(observed)) != len(observed):
        issues.append("$.tests: test full names must be unique")
    for field in ("passed", "failed", "skipped"):
        if not isinstance(tests.get(field), int) or isinstance(tests.get(field), bool) or tests.get(field, -1) < 0:
            issues.append(f"$.tests.{field}: expected non-negative integer")
    computed_complete = set(expected) == set(observed) and len(expected) == len(observed)
    if tests.get("complete") is not computed_complete:
        issues.append("$.tests.complete: does not match expected/observed full names")
    if tests.get("total") != len(observed):
        issues.append("$.tests.total: must equal observed test count")
    if isinstance(tests.get("passed"), int) and isinstance(tests.get("failed"), int) and isinstance(tests.get("skipped"), int):
        if tests["passed"] + tests["failed"] + tests["skipped"] != len(observed):
            issues.append("$.tests: result counts do not equal observed tests")

    integrations = _array(root.get("optionalIntegrations"), "$.optionalIntegrations", issues)
    selected = []
    for index, raw in enumerate(integrations):
        path = f"$.optionalIntegrations[{index}]"
        item = _mapping(raw, path, issues)
        _required(item, ("id", "profile", "requested", "packageName", "sourceKind", "minimumVersion",
                         "requestedVersion", "validatedVersion", "resolvedSource", "resolvedVersion",
                         "resolvedRevision", "define", "adapterAssemblies", "adapterDlls", "behaviorTest",
                         "behaviorPassed", "supportTier"), path, issues)
        if not _is_bool(item.get("requested")):
            issues.append(f"{path}.requested: expected boolean")
        if item.get("requested"):
            selected.append(item)
            if item.get("id") == "fmod":
                issues.append(f"{path}.id: FMOD must remain O0")
            if item.get("resolvedVersion") != item.get("validatedVersion"):
                issues.append(f"{path}: resolvedVersion must equal validatedVersion")
            for dll_index, dll in enumerate(_array(item.get("adapterDlls"), f"{path}.adapterDlls", issues)):
                if not isinstance(dll, dict) or dll.get("compiled") is not True:
                    issues.append(f"{path}.adapterDlls[{dll_index}]: selected adapter DLL must be compiled")
                elif not SHA256_RE.fullmatch(str(dll.get("sha256", ""))):
                    issues.append(f"{path}.adapterDlls[{dll_index}].sha256: expected lowercase SHA-256")
            if item.get("behaviorPassed") is not True:
                issues.append(f"{path}.behaviorPassed: selected behavior must pass")
        else:
            for field in ("requestedVersion", "resolvedSource", "resolvedVersion", "resolvedRevision", "behaviorPassed"):
                if item.get(field) is not None:
                    issues.append(f"{path}.{field}: unrequested integration value must be null")
    expected_selected = 0 if execution.get("profile") == "base" else 1
    if len(selected) != expected_selected:
        issues.append("$.optionalIntegrations: selected integration count does not match profile")

    artifacts = _check_artifacts(root.get("artifacts"), "$.artifacts", issues)
    player = _mapping(root.get("player"), "$.player", issues)
    _required(player, ("buildRequested", "buildPassed", "runRequested", "runPassed", "buildReportHash", "runReportHash", "target"), "$.player", issues)
    for gate in ("build", "run"):
        requested = player.get(f"{gate}Requested")
        passed = player.get(f"{gate}Passed")
        report_hash = player.get(f"{gate}ReportHash")
        if not _is_bool(requested):
            issues.append(f"$.player.{gate}Requested: expected boolean")
        if requested:
            if not _is_bool(passed):
                issues.append(f"$.player.{gate}Passed: requested result must be boolean")
            _check_hash(report_hash, f"$.player.{gate}ReportHash", issues)
            if report_hash not in {item.get("sha256") for item in artifacts.values()}:
                issues.append(f"$.player.{gate}ReportHash: hash must reference an artifact")
        elif passed is not None or report_hash is not None:
            issues.append(f"$.player.{gate}: unrequested result and report hash must be null")
    if player.get("runRequested") and not player.get("buildRequested"):
        issues.append("$.player: run cannot be requested without build")

    mutation = _mapping(root.get("mutationGuard"), "$.mutationGuard", issues)
    scope_names = ("packageCandidate", "harnessRunner", "consumerFixture", "generatedConsumerAuthority")
    _required(mutation, ("policyVersion", "allowedChanges", *scope_names, "verdict"), "$.mutationGuard", issues)
    for name in scope_names:
        scope = _mapping(mutation.get(name), f"$.mutationGuard.{name}", issues)
        _required(scope, ("beforeHash", "afterHash", "verdict"), f"$.mutationGuard.{name}", issues)
        _check_hash(scope.get("beforeHash"), f"$.mutationGuard.{name}.beforeHash", issues)
        _check_hash(scope.get("afterHash"), f"$.mutationGuard.{name}.afterHash", issues)
        if scope.get("verdict") != "passed" or scope.get("beforeHash") != scope.get("afterHash"):
            issues.append(f"$.mutationGuard.{name}: mutation guard must pass with equal hashes")
    if mutation.get("verdict") != "passed":
        issues.append("$.mutationGuard.verdict: expected passed")

    logs = _mapping(root.get("logPolicy"), "$.logPolicy", issues)
    _required(logs, ("policy", "version", "patterns", "checkedArtifactIds", "hits", "verdict"), "$.logPolicy", issues)
    if not isinstance(logs.get("hits"), list):
        issues.append("$.logPolicy.hits: expected array")
    if logs.get("verdict") != "passed" or logs.get("hits") != []:
        issues.append("$.logPolicy: passed verdict requires zero hits")

    eligible = bool(
        result == "passed"
        and execution.get("cleanLibrary")
        and tests.get("complete")
        and tests.get("failed") == 0
        and mutation.get("verdict") == "passed"
        and logs.get("verdict") == "passed"
        and not candidate.get("localSourceDirty")
        and (candidate.get("sourceMode") != "pinned-git" or candidate.get("requestedCommit") == candidate.get("resolvedCommit"))
    )
    if execution.get("eligibleForMatrix") is not eligible:
        issues.append("$.execution.eligibleForMatrix: does not match fail-closed row eligibility")
    _check_no_sensitive_strings(root, "$", issues)
    return issues


def compute_matrix_promotable(document: Mapping[str, Any]) -> bool:
    candidate = document.get("candidate", {})
    equality = document.get("candidateEquality", {})
    required = document.get("requiredRows", [])
    rows = document.get("rows", [])
    # The matrix runner uses requiredRows as the row result manifest.  Accept a
    # separate rows array as a forward-compatible richer representation.
    if not rows:
        rows = required
    required_ids = [row.get("id", row.get("rowId")) for row in required if isinstance(row, dict)]
    row_by_id = {row.get("id", row.get("rowId")): row for row in rows if isinstance(row, dict)}
    sources = {row.get("sourceMode") for row in required if isinstance(row, dict)}
    return bool(
        isinstance(candidate, dict)
        and COMMIT_RE.fullmatch(str(candidate.get("commit", "")))
        and SHA256_RE.fullmatch(str(candidate.get("packageTreeHash", "")))
        and not candidate.get("workingTreeDirty")
        and equality.get("passed") is True
        and required_ids
        and len(required_ids) == len(set(required_ids))
        and set(required_ids) == set(row_by_id)
        and sources == {"local", "pinned-git"}
        and all(
            row_by_id[row_id].get("passed") is True
            and row_by_id[row_id].get("eligible", True) is True
            and row_by_id[row_id].get("schemaValid", True) is True
            and row_by_id[row_id].get("artifactHashValid", True) is True
            and row_by_id[row_id].get("mutationClean", True) is True
            and row_by_id[row_id].get("testsComplete", True) is True
            and row_by_id[row_id].get("logsClean", True) is True
            and row_by_id[row_id].get("candidateEqual", True) is True
            for row_id in required_ids
        )
    )


def validate_matrix_report(document: Any) -> list[str]:
    issues: list[str] = []
    root = _mapping(document, "$", issues)
    _required(root, ("schemaVersion", "type", "evidenceLevel", "candidate", "requiredRowCount", "requiredRows", "candidateEquality", "promotable", "reasons"), "$", issues)
    if root.get("schemaVersion") != 1:
        issues.append("$.schemaVersion: expected 1")
    if root.get("type") != MATRIX_TYPE:
        issues.append(f"$.type: expected {MATRIX_TYPE}")
    if root.get("evidenceLevel") != "E2-matrix":
        issues.append("$.evidenceLevel: expected E2-matrix")
    candidate = _mapping(root.get("candidate"), "$.candidate", issues)
    _required(candidate, ("commit", "packageTreeHash", "packageName", "packageVersion", "workingTreeDirty"), "$.candidate", issues)
    _check_commit(candidate.get("commit"), "$.candidate.commit", issues)
    if candidate.get("packageTreeHash") is not None:
        _check_hash(candidate.get("packageTreeHash"), "$.candidate.packageTreeHash", issues)
    if root.get("promotable") and (not candidate.get("packageName") or not candidate.get("packageVersion") or candidate.get("packageTreeHash") is None):
        issues.append("$.candidate: promotable matrix requires complete package identity")
    if "workingTreeDirty" in candidate and not _is_bool(candidate.get("workingTreeDirty")):
        issues.append("$.candidate.workingTreeDirty: expected boolean")

    required = _array(root.get("requiredRows"), "$.requiredRows", issues)
    required_ids: list[str] = []
    for index, raw in enumerate(required):
        path = f"$.requiredRows[{index}]"
        item = _mapping(raw, path, issues)
        _required(item, ("id", "sourceMode", "profile", "required", "report", "reportSha256", "passed", "failureReason"), path, issues)
        required_ids.append(item.get("id"))
        if item.get("sourceMode") not in {"local", "pinned-git"}:
            issues.append(f"{path}.sourceMode: unsupported source")
        if item.get("profile") not in PROFILE_NAMES:
            issues.append(f"{path}.profile: unsupported profile")
        if item.get("report") is not None:
            try:
                if normalize_relative_identifier(item.get("report")) != item.get("report"):
                    issues.append(f"{path}.report: identifier is not normalized")
            except (TypeError, ValueError) as error:
                issues.append(f"{path}.report: {error}")
        if item.get("reportSha256") is not None:
            _check_hash(item.get("reportSha256"), f"{path}.reportSha256", issues)
        if not _is_bool(item.get("passed")):
            issues.append(f"{path}.passed: expected boolean")
        if item.get("passed") and (item.get("report") is None or item.get("reportSha256") is None):
            issues.append(f"{path}: passed row requires report and reportSha256")
        if item.get("required") is not True:
            issues.append(f"{path}.required: matrix manifest rows must be required")
        if (item.get("passed") is True) != (item.get("failureReason") is None):
            issues.append(f"{path}.failureReason: must be null exactly when row passed")
        for field in ("eligible", "schemaValid", "artifactHashValid", "mutationClean", "testsComplete", "logsClean", "candidateEqual"):
            if field in item and not _is_bool(item.get(field)):
                issues.append(f"{path}.{field}: expected boolean")
    if required_ids != sorted(required_ids) or len(required_ids) != len(set(required_ids)):
        issues.append("$.requiredRows: rowId values must be unique and stably sorted")
    if root.get("requiredRowCount") != len(required):
        issues.append("$.requiredRowCount: must equal requiredRows length")

    equality = _mapping(root.get("candidateEquality"), "$.candidateEquality", issues)
    _required(equality, ("passed",), "$.candidateEquality", issues)
    if not _is_bool(equality.get("passed")):
        issues.append("$.candidateEquality.passed: expected boolean")
    reasons = _array(root.get("reasons"), "$.reasons", issues)
    if any(not isinstance(reason, str) or not reason for reason in reasons):
        issues.append("$.reasons: expected non-empty strings")
    computed = compute_matrix_promotable(root)
    if not _is_bool(root.get("promotable")) or root.get("promotable") is not computed:
        issues.append("$.promotable: must equal the fail-closed computed matrix verdict")
    if computed and reasons:
        issues.append("$.reasons: promotable matrix must have no failure reasons")
    if not computed and not reasons:
        issues.append("$.reasons: non-promotable matrix requires at least one reason")
    _check_no_sensitive_strings(root, "$", issues)
    return issues


def validate_receipt_candidate(document: Any) -> list[str]:
    issues: list[str] = []
    root = _mapping(document, "$", issues)
    _required(root, ("schemaVersion", "type", "candidate", "matrix", "reports", "promotable", "releaseReady", "pendingP8Gates"), "$", issues)
    if root.get("schemaVersion") != 1:
        issues.append("$.schemaVersion: expected 1")
    if root.get("type") != RECEIPT_TYPE:
        issues.append(f"$.type: expected {RECEIPT_TYPE}")
    candidate = _mapping(root.get("candidate"), "$.candidate", issues)
    _required(candidate, ("commit", "packageTreeHash", "packageName", "packageVersion", "workingTreeDirty"), "$.candidate", issues)
    _check_commit(candidate.get("commit"), "$.candidate.commit", issues)
    _check_hash(candidate.get("packageTreeHash"), "$.candidate.packageTreeHash", issues)
    if candidate.get("workingTreeDirty") is not False:
        issues.append("$.candidate.workingTreeDirty: receipt candidate must be clean")
    matrix = _mapping(root.get("matrix"), "$.matrix", issues)
    _required(matrix, ("report", "sha256"), "$.matrix", issues)
    try:
        if normalize_relative_identifier(matrix.get("report")) != matrix.get("report"):
            issues.append("$.matrix.report: identifier is not normalized")
    except (TypeError, ValueError) as error:
        issues.append(f"$.matrix.report: {error}")
    _check_hash(matrix.get("sha256"), "$.matrix.sha256", issues)
    reports = _array(root.get("reports"), "$.reports", issues)
    report_ids: list[str] = []
    for index, raw in enumerate(reports):
        path = f"$.reports[{index}]"
        item = _mapping(raw, path, issues)
        _required(item, ("id", "report", "sha256", "passed"), path, issues)
        report_ids.append(item.get("id"))
        try:
            if normalize_relative_identifier(item.get("report")) != item.get("report"):
                issues.append(f"{path}.report: identifier is not normalized")
        except (TypeError, ValueError) as error:
            issues.append(f"{path}.report: {error}")
        _check_hash(item.get("sha256"), f"{path}.sha256", issues)
        if item.get("passed") is not True:
            issues.append(f"{path}.passed: receipt may reference only passed reports")
    if not report_ids or report_ids != sorted(report_ids) or len(report_ids) != len(set(report_ids)):
        issues.append("$.reports: id values must be non-empty, unique, and stably sorted")
    if root.get("promotable") is not True:
        issues.append("$.promotable: E3 candidate requires a promotable E2 matrix")
    if root.get("releaseReady") is not False:
        issues.append("$.releaseReady: P7 receipt candidate cannot declare release readiness")
    pending = _array(root.get("pendingP8Gates"), "$.pendingP8Gates", issues)
    if not pending or pending != sorted(pending) or len(pending) != len(set(pending)):
        issues.append("$.pendingP8Gates: values must be non-empty, unique, and stably sorted")
    _check_no_sensitive_strings(root, "$", issues)
    return issues


def validate_document(document: Any, kind: str | None = None) -> list[str]:
    if kind is None and isinstance(document, dict):
        kind = document.get("type", document.get("reportType"))
        if kind is None and document.get("schemaVersion") == 4:
            kind = "profile"
    validators = {
        "profile": validate_profile_report,
        PROFILE_TYPE: validate_profile_report,
        "matrix": validate_matrix_report,
        MATRIX_TYPE: validate_matrix_report,
        "receipt": validate_receipt_candidate,
        RECEIPT_TYPE: validate_receipt_candidate,
    }
    validator = validators.get(kind)
    if validator is None:
        return ["$.reportType: cannot determine evidence document type"]
    return validator(document)


def require_valid(document: Any, kind: str | None = None) -> None:
    issues = validate_document(document, kind)
    if issues:
        raise EvidenceValidationError(issues)


def validate_artifact_hashes(document: Mapping[str, Any], kind: str, artifact_root: str | Path) -> list[str]:
    """Verify referenced artifacts under an explicit root without path escape."""

    root = Path(artifact_root).resolve()
    references: list[tuple[str, str, str]] = []
    if kind == "profile":
        for index, artifact in enumerate(document.get("artifacts", [])):
            if isinstance(artifact, dict):
                external = artifact.get("externalStorageReference")
                identifier = (
                    external[len("consumer-output:"):]
                    if isinstance(external, str)
                    and external.startswith("consumer-output:")
                    else external
                )
                references.append((f"$.artifacts[{index}]", identifier, artifact.get("sha256")))
    elif kind == "matrix":
        for index, row in enumerate(document.get("requiredRows", [])):
            if isinstance(row, dict):
                if row.get("report") is not None:
                    references.append((f"$.requiredRows[{index}]", row.get("report"), row.get("reportSha256")))
    elif kind in {"receipt", "receipt-candidate"}:
        matrix = document.get("matrix", {})
        if isinstance(matrix, dict):
            references.append(("$.matrix", matrix.get("report"), matrix.get("sha256")))
        for index, report in enumerate(document.get("reports", [])):
            if isinstance(report, dict):
                references.append((f"$.reports[{index}]", report.get("report"), report.get("sha256")))
    issues: list[str] = []
    for path, identifier, expected_hash in references:
        try:
            normalized = normalize_relative_identifier(identifier)
        except (TypeError, ValueError) as error:
            issues.append(f"{path}: cannot verify artifact: {error}")
            continue
        artifact_path = root.joinpath(*PurePosixPath(normalized).parts).resolve()
        try:
            artifact_path.relative_to(root)
        except ValueError:
            issues.append(f"{path}: artifact escaped the artifact root")
            continue
        try:
            digest = hashlib.sha256(artifact_path.read_bytes()).hexdigest()
        except OSError as error:
            issues.append(f"{path}: artifact unavailable: {error}")
            continue
        if digest != expected_hash:
            issues.append(f"{path}: artifact SHA-256 mismatch")
    return issues
