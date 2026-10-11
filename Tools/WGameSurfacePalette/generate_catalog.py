#!/usr/bin/env python3
"""Generate or verify the repository-authoritative WGame Surface Palette."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from catalog_core import build_catalog, build_surface_profiles_hlsl, validate_catalog, write_catalog


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_CATALOG = (
    REPOSITORY_ROOT
    / "ArtSource"
    / "Common"
    / "Palettes"
    / "WGameSurfacePalette"
    / "catalog.json"
)
DEFAULT_HLSL = (
    REPOSITORY_ROOT
    / "Assets"
    / "Art"
    / "WGame"
    / "Shaders"
    / "ShaderLibrary"
    / "WGameSurfaceProfiles.generated.hlsl"
)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path, default=DEFAULT_CATALOG)
    parser.add_argument("--hlsl-output", type=Path, default=DEFAULT_HLSL)
    parser.add_argument("--check", action="store_true")
    parser.add_argument("--check-systematic", action="store_true")
    parser.add_argument("--force-reset", action="store_true")
    args = parser.parse_args()

    generated = build_catalog()
    generated_hlsl = build_surface_profiles_hlsl(generated)
    issues = validate_catalog(generated)
    if issues:
        raise SystemExit("generated catalog is invalid: " + "; ".join(issues))

    output = args.output.resolve()
    if args.check or args.check_systematic:
        if not output.is_file():
            raise SystemExit(f"catalog is missing: {output}")
        current = json.loads(output.read_text(encoding="utf-8"))
        current_issues = validate_catalog(current)
        if current_issues:
            raise SystemExit("catalog is invalid: " + "; ".join(current_issues))
        if args.check_systematic and current != generated:
            raise SystemExit("catalog differs from deterministic generator output")
        hlsl_output = args.hlsl_output.resolve()
        if not hlsl_output.is_file() or hlsl_output.read_text(encoding="utf-8") != generated_hlsl:
            raise SystemExit("generated Surface Profile HLSL differs from Catalog")
        print(
            f"catalog ok: colors={current['colorCount']} profiles={current['surfaceProfileCount']} "
            f"logical={current['logicalTokenCount']} hash={current['catalogHash']}"
        )
        return 0

    if output.is_file() and not args.force_reset:
        current = json.loads(output.read_text(encoding="utf-8"))
        if current != generated:
            raise SystemExit(
                "catalog differs from the v3 RGB565 formula generator; use --force-reset for the approved incompatible rebuild."
            )
    write_catalog(output, generated)
    hlsl_output = args.hlsl_output.resolve()
    hlsl_output.parent.mkdir(parents=True, exist_ok=True)
    hlsl_output.write_text(generated_hlsl, encoding="utf-8", newline="\n")
    print(
        f"wrote {output}: colors={generated['colorCount']} profiles={generated['surfaceProfileCount']} "
        f"logical={generated['logicalTokenCount']} hash={generated['catalogHash']}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
