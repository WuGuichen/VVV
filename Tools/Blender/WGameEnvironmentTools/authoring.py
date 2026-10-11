"""Scene painting adapter over the existing WGame palette and bake utilities."""
import importlib
import json
import os
import sys
from base64 import b64decode
from pathlib import Path

import bpy


def repository():
    configured = os.environ.get('WGAME_REPOSITORY_ROOT')
    deployment = Path(__file__).with_name('deployment.json')
    if configured:
        return Path(configured).resolve()
    if deployment.is_file():
        return Path(json.loads(deployment.read_text(encoding='utf-8-sig'))['repositoryRoot'])
    return Path(__file__).resolve().parents[3]


def palette_module():
    root = repository()
    sys.path.insert(0, str(root/'Tools/Blender')) if str(root/'Tools/Blender') not in sys.path else None
    module = importlib.import_module('WGamePaletteTools')
    if module.STATE.catalog is None:
        data = json.loads((root/module.CATALOG_RELATIVE).read_text(encoding='utf-8'))
        module._validate_catalog(data)
        module.STATE.catalog = data
        module.STATE.rgb8 = b64decode(data['colorData']['rgb8Base64'])
        module.STATE.source = '仓库'
    return module


def bake_module():
    # Keep snapshot image ownership and the no-lighting base-color contract shared.
    name = 'wgame_environment_character_bake'
    if name not in sys.modules:
        path = repository()/'Tools/Blender/WGameRiggingAssistant/baked_surface.py'
        spec = importlib.util.spec_from_file_location(name, path)
        module = importlib.util.module_from_spec(spec)
        sys.modules[name] = module
        spec.loader.exec_module(module)
    return sys.modules[name]


def activate(obj):
    if getattr(bpy.context,'object',None) and bpy.context.object.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.object.select_all(action='DESELECT')
    obj.hide_set(False)
    obj.hide_viewport = False
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj

