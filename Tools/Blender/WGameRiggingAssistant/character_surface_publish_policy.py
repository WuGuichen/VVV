"""Resolve the publish path from existing authority without modifying a package."""
import json
from pathlib import Path


def resolve_surface_publish_mode(package_root, force_baked=False):
    config = Path(package_root) / 'config'
    baked = config / 'character_baked_surface_binding.json'
    if baked.exists():
        document = json.loads(baked.read_text(encoding='utf-8-sig'))
        if not isinstance(document, dict) or document.get('format') != 'mx.characterBakedSurfaceBinding.v1':
            raise ValueError('烘焙描述损坏，请先修复；不会回退到历史色板发布')
        return 'BAKED'
    if force_baked:
        return 'BAKED'
    palette = config / 'character_palette_binding.json'
    if palette.exists():
        document = json.loads(palette.read_text(encoding='utf-8-sig'))
        if not isinstance(document, dict) or not isinstance(document.get('enabled'), bool):
            raise ValueError('历史色板描述损坏，请先修复或明确选择转为烘焙发布')
        if document['enabled']:
            if document.get('format') != 'mx.characterPaletteBinding.v5':
                raise ValueError('历史色板格式需要迁移，或明确选择转为烘焙发布')
            return 'LEGACY'
    return 'BAKED'
