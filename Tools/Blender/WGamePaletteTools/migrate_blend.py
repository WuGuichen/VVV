"""Blender background entry point for the explicit RGB565 Color Palette repaint migration."""

import os
from pathlib import Path
import sys

import bpy


def main():
    arguments = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if len(arguments) != 1:
        raise SystemExit("usage: blender <file.blend> --background --python migrate_blend.py -- <repository-root>")
    repository = Path(arguments[0]).resolve()
    os.environ["WGAME_REPOSITORY_ROOT"] = str(repository)
    sys.path.insert(0, str(repository / "Tools" / "Blender"))
    import WGamePaletteTools as addon

    addon.register()
    try:
        addon.load_catalog(bpy.context, prefer_server=False)
        original = Path(bpy.data.filepath).resolve()
        backup, report, invalid_faces = addon.migrate_selected_to_global(bpy.context)
        bpy.ops.wm.save_as_mainfile(filepath=str(original))
        print(
            "WG_SURFACE_PALETTE_MIGRATION_OK",
            f"source={original}",
            f"backup={backup}",
            f"report={report}",
            f"invalidFaces={invalid_faces}",
        )
    finally:
        addon.unregister()


if __name__ == "__main__":
    main()
