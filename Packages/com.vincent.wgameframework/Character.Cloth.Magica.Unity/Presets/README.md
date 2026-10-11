# WGame Character Cloth Presets

These presets are generated from MagicaCloth2's Cape preset and provide three
character-garment starting points. They are framework-owned assets; do not edit
the MagicaCloth2 vendor presets to tune WGame defaults.

| Preset | Intended use | Damping | World / Local inertia | Angle limit | Motion character |
| --- | --- | ---: | ---: | ---: | --- |
| `WGame_Cloth_Soft.json` | Long cape, long skirt, ribbons | 0.07 | 0.85 / 0.90 | 70 | Largest lag and swing |
| `WGame_Cloth_Medium.json` | General character garments | 0.10 | 0.70 / 0.80 | 60 | Clear inertial motion with controlled stopping |
| `WGame_Cloth_Firm.json` | Short cape, thick cloth, armor panels | 0.16 | 0.45 / 0.60 | 40 | Fast follow with visible secondary motion |

Cloth Authoring discovers these files automatically. Applying a WGame quick
preset clears the selected Cloth's parameter overrides after confirmation so
the preset remains an exact, reproducible baseline. Root bones, colliders, and
Cloth grouping are preserved.
