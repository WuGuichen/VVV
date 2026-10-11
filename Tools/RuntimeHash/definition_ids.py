"""Authoring-only, transactional allocation of numeric definition identities.

Runtime readers consume domainId/id and never allocate from sourceKey.
"""
from __future__ import annotations

import argparse
import copy
import hashlib
import json
import os
from pathlib import Path
import tempfile

SCHEMA = "mx.runtime-definition-id-catalog.v1"
MAX_ID = 2_147_483_647


def canonical(document: dict) -> bytes:
    value = {key: item for key, item in document.items() if key != "catalogHash"}
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False).encode("utf-8")


def digest(document: dict) -> str:
    return "sha256:" + hashlib.sha256(canonical(document)).hexdigest()


def validate(document: dict) -> None:
    if document.get("schema") != SCHEMA:
        raise ValueError("Unsupported definition identity catalog schema")
    if type(document.get("revision")) is not int or document["revision"] < 0:
        raise ValueError("Invalid catalog revision")
    if document.get("catalogHash") != digest(document):
        raise ValueError("Catalog content hash mismatch")
    ids, sources, domains = set(), set(), set()
    for domain in document["domains"]:
        domain_id = domain["domainId"]
        if type(domain_id) is not int or not 0 < domain_id <= MAX_ID or domain_id in domains:
            raise ValueError("Duplicate or invalid predeclared domain id")
        domains.add(domain_id)
        maximum = 0
        for entry in domain["entries"]:
            number = entry["id"]
            if type(number) is not int or not 0 < number <= MAX_ID or (domain_id, number) in ids:
                raise ValueError("Duplicate or invalid definition id")
            if not isinstance(entry["sourceKey"], str) or not entry["sourceKey"]:
                raise ValueError("Authoring source key is required")
            if (domain_id, entry["sourceKey"]) in sources:
                raise ValueError("Duplicate authoring source key, including retired entries")
            if entry["status"] not in ("active", "retired"):
                raise ValueError("Invalid definition status")
            ids.add((domain_id, number))
            sources.add((domain_id, entry["sourceKey"]))
            maximum = max(maximum, number)
        if type(domain["nextId"]) is not int or not maximum < domain["nextId"] <= MAX_ID + 1:
            raise ValueError("nextId must exceed every active and retired id")


def create(domain_ids: list[int]) -> dict:
    if len(domain_ids) != len(set(domain_ids)):
        raise ValueError("Duplicate domain id")
    result = {"schema": SCHEMA, "revision": 0, "domains": [
        {"domainId": number, "nextId": 1, "entries": []} for number in sorted(domain_ids)
    ]}
    result["catalogHash"] = digest(result)
    validate(result)
    return result


def apply(document: dict, operation: str, domain_id: int, source_key: str, target_key: str | None = None,
          numeric_id: int | None = None) -> dict:
    validate(document)
    result = copy.deepcopy(document)
    domain = next((item for item in result["domains"] if item["domainId"] == domain_id), None)
    if operation == "declare" and domain is not None:
        return result
    if domain is None and operation != "declare":
        raise ValueError("Domain must be explicitly declared before allocation")
    existing = next((item for item in domain["entries"] if item["sourceKey"] == source_key), None) if domain else None
    if operation == "declare":
        if type(domain_id) is not int or not 0 < domain_id <= MAX_ID:
            raise ValueError("Invalid explicitly declared domain")
        result["domains"].append({"domainId": domain_id, "nextId": 1, "entries": []})
        result["domains"].sort(key=lambda item: item["domainId"])
    elif operation == "adopt":
        if type(numeric_id) is not int or not 0 < numeric_id <= MAX_ID:
            raise ValueError("Adoption requires an existing positive Int32 identity")
        if existing:
            if existing["id"] != numeric_id or existing["status"] != "active":
                raise ValueError("Existing or retired source identity cannot be reassigned")
            return result
        if any(entry["id"] == numeric_id for entry in domain["entries"]):
            raise ValueError("Existing numeric identity is owned by a different source")
        domain["entries"].append({"id": numeric_id, "sourceKey": source_key, "status": "active"})
        domain["nextId"] = max(domain["nextId"], numeric_id + 1)
    elif operation == "allocate":
        if existing:
            if existing["status"] == "retired":
                raise ValueError("Retired identities cannot be reused")
            return result
        _allocate(domain, source_key)
    elif operation == "copy":
        if not existing or existing["status"] != "active":
            raise ValueError("Copy source must be active")
        if not target_key or any(item["sourceKey"] == target_key for item in domain["entries"]):
            raise ValueError("Copy target must be a new authoring source")
        _allocate(domain, target_key)
    elif operation == "retire":
        if not existing:
            raise ValueError("Cannot retire an unknown identity")
        if existing["status"] == "retired":
            return result
        existing["status"] = "retired"
    else:
        raise ValueError("Unknown operation")
    result["revision"] += 1
    result["catalogHash"] = digest(result)
    validate(result)
    return result


def _allocate(domain: dict, source_key: str) -> None:
    if not source_key or domain["nextId"] > MAX_ID:
        raise ValueError("Missing authoring source or exhausted id range")
    domain["entries"].append({"id": domain["nextId"], "sourceKey": source_key, "status": "active"})
    domain["nextId"] += 1


def transact(path: Path, expected_revision: int, expected_hash: str, operation: str,
             domain_id: int, source_key: str, target_key: str | None = None,
             numeric_id: int | None = None) -> dict:
    path = path.resolve(strict=True)
    lock_path = path.with_suffix(path.suffix + ".lock")
    descriptor = os.open(lock_path, os.O_CREAT | os.O_EXCL | os.O_WRONLY)
    temporary = None
    try:
        os.close(descriptor)
        original = path.read_bytes()
        document = json.loads(original)
        validate(document)
        if document["revision"] != expected_revision or document["catalogHash"] != expected_hash:
            raise ValueError("Stale catalog revision/hash; no changes written")
        result = apply(document, operation, domain_id, source_key, target_key, numeric_id)
        if result == document:
            return result
        with tempfile.NamedTemporaryFile(mode="w", encoding="utf-8", newline="\n", dir=path.parent,
                                         prefix=path.name + ".", suffix=".tmp", delete=False) as stream:
            temporary = Path(stream.name)
            json.dump(result, stream, ensure_ascii=False, indent=2)
            stream.write("\n")
            stream.flush()
            os.fsync(stream.fileno())
        if path.read_bytes() != original:
            raise ValueError("Catalog changed during transaction; no changes written")
        os.replace(temporary, path)
        temporary = None
        return result
    finally:
        if temporary is not None:
            temporary.unlink(missing_ok=True)
        lock_path.unlink(missing_ok=True)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("operation", choices=("validate", "allocate", "copy", "retire", "adopt", "declare"))
    parser.add_argument("catalog", type=Path)
    parser.add_argument("--domain", type=int)
    parser.add_argument("--source")
    parser.add_argument("--target")
    parser.add_argument("--id", type=int)
    parser.add_argument("--expected-revision", type=int)
    parser.add_argument("--expected-hash")
    args = parser.parse_args()
    if args.operation == "validate":
        validate(json.loads(args.catalog.read_text(encoding="utf-8-sig")))
        print("valid")
        return
    if any(value is None for value in (args.domain, args.source, args.expected_revision, args.expected_hash)):
        parser.error("Mutations require domain, source, expected-revision and expected-hash")
    result = transact(args.catalog, args.expected_revision, args.expected_hash, args.operation,
                      args.domain, args.source, args.target, args.id)
    print(json.dumps({"revision": result["revision"], "catalogHash": result["catalogHash"]}))


if __name__ == "__main__":
    main()
