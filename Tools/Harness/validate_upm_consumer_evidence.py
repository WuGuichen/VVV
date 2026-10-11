#!/usr/bin/env python3
"""Offline validator for UPM-P7 profile, matrix, and receipt evidence."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

from upm_consumer_evidence import sha256_document, validate_artifact_hashes, validate_document


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="kind", required=True)
    for command in ("profile", "matrix", "receipt-candidate"):
        subparser = subparsers.add_parser(command)
        subparser.add_argument("evidence", type=Path, help="JSON evidence file")
        subparser.add_argument("--artifact-root", type=Path, help="verify referenced artifacts below this directory")
        subparser.add_argument("--print-hash", action="store_true", help="print the canonical document SHA-256 after validation")
    args = parser.parse_args(argv)
    try:
        with args.evidence.open("r", encoding="utf-8-sig") as stream:
            document = json.load(stream)
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        print(f"invalid evidence input: {error}", file=sys.stderr)
        return 2
    validation_kind = "receipt" if args.kind == "receipt-candidate" else args.kind
    issues = validate_document(document, validation_kind)
    if not issues and args.artifact_root is not None:
        issues.extend(validate_artifact_hashes(document, args.kind, args.artifact_root))
    if issues:
        for issue in issues:
            print(issue, file=sys.stderr)
        return 1
    if args.print_hash:
        print(sha256_document(document))
    else:
        print("valid")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
