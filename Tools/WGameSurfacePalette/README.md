# WGame Surface Palette

`catalog.json` is generated deterministically from the project-wide formula Color Palette:

```powershell
python Tools/WGameSurfacePalette/generate_catalog.py
python Tools/WGameSurfacePalette/generate_catalog.py --check
python Tools/WGameSurfacePalette/generate_catalog.py --check-systematic
```

The v3 catalog stores a compact 256×256 RGB565-derived table plus 16 immutable
Surface Profiles. Index `0..65535` maps bidirectionally to `(R5,G6,B5)`; expanding
the components to RGB8 yields exactly 65,536 unique colors and zero aliases.
Material response is selected by `PaletteData.A`; logical tokens are
`colorIndex*16+surfaceProfile`. There are no Custom, Retired, alias, or mutable
per-character tokens.

The generator also writes `WGameSurfaceProfiles.generated.hlsl`; Catalog and HLSL
must pass `--check-systematic` together.
