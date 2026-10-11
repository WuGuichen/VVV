#!/usr/bin/env python3
"""Generate the deterministic Player audio Bank manifest and its mirror."""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import sys


ROOT = pathlib.Path(__file__).resolve().parents[2]
FMOD_PROJECT = ROOT / "FMOD/MxFrameworkAudioDemo/MxFrameworkAudioDemo/MxFrameworkAudioDemo.fspro"
BUILD_ROOT = ROOT / "FMOD/MxFrameworkAudioDemo/MxFrameworkAudioDemo/Build/Desktop"
STREAMING_ROOT = ROOT / "Assets/StreamingAssets"
GLOBAL_CATALOG = ROOT / "Assets/MxFrameworkGenerated/Audio/audio_runtime_catalog.json"
PHYSICAL_CATALOG = ROOT / "Assets/MxFrameworkGenerated/Audio/audio_physical_runtime_catalog.json"
OUTPUTS = (
    ROOT / "Assets/MxFrameworkGenerated/Audio/audio_bank_manifest.json",
    ROOT / "Assets/StreamingAssets/MxFramework/Audio/audio_bank_manifest.json",
)


def sha256(path: pathlib.Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def require_file(path: pathlib.Path) -> None:
    if not path.is_file():
        raise RuntimeError(f"required audio artifact is missing: {path.relative_to(ROOT)}")


def build_manifest() -> dict:
    require_file(FMOD_PROJECT)
    require_file(GLOBAL_CATALOG)
    bank_specs = (
        ("Master.strings", "Master.strings.bank", True, False),
        ("Master", "Master.bank", True, True),
    )
    banks = []
    for stable_name, file_name, required, preload_samples in bank_specs:
        source = BUILD_ROOT / file_name
        mirror = STREAMING_ROOT / file_name
        require_file(source)
        require_file(mirror)
        source_hash = sha256(source)
        mirror_hash = sha256(mirror)
        if source_hash != mirror_hash:
            raise RuntimeError(f"FMOD Build/Streaming Bank hash mismatch: {file_name}")
        banks.append(
            {
                "stableName": stable_name,
                "fileName": file_name,
                "sha256": source_hash,
                "sizeBytes": source.stat().st_size,
                "required": required,
                "group": "bootstrap",
                "scope": "AppPersistent",
                "preloadSamples": preload_samples,
            }
        )

    global_json = json.loads(GLOBAL_CATALOG.read_text(encoding="utf-8"))
    physical_hash = sha256(PHYSICAL_CATALOG) if PHYSICAL_CATALOG.is_file() else ""
    return {
        "format": "mx.audioBankManifest.v1",
        "schemaVersion": 1,
        "platform": "Desktop",
        "fmodProjectSha256": sha256(FMOD_PROJECT),
        "audioCatalogSha256": sha256(GLOBAL_CATALOG),
        "audioManifestHash": global_json.get("manifestHash", ""),
        "audioSnapshotHash": global_json.get("snapshotHash", ""),
        "physicalCatalogSha256": physical_hash,
        "banks": banks,
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true", help="verify outputs without writing")
    args = parser.parse_args()
    try:
        content = json.dumps(build_manifest(), ensure_ascii=False, indent=2) + "\n"
        if args.check:
            stale = [path for path in OUTPUTS if not path.is_file() or path.read_text(encoding="utf-8") != content]
            if stale:
                for path in stale:
                    print(f"stale: {path.relative_to(ROOT)}", file=sys.stderr)
                return 1
            print("Audio Bank manifest is current.")
            return 0
        for path in OUTPUTS:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(content, encoding="utf-8")
            print(f"wrote {path.relative_to(ROOT)}")
        return 0
    except (OSError, RuntimeError, ValueError, json.JSONDecodeError) as error:
        print(f"audio Bank manifest generation failed: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
