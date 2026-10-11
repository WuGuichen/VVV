"""Migrate an isolated snapshot; do not draw or unwrap any faces."""
import argparse
import importlib
import json
import sys
from pathlib import Path
import bpy

root=Path(__file__).resolve().parents[3]
sys.path.insert(0,str(root/'Tools/Blender'))
module=importlib.import_module('WGameEnvironmentTools.shared_detail')
parser=argparse.ArgumentParser();parser.add_argument('--output',required=True)
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
if bpy.context.object and bpy.context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
report=module.convert(bpy.context.scene)
report['validation']=module.validate()
bpy.ops.wm.save_as_mainfile(filepath=str(Path(args.output).resolve()),compress=True)
Path(args.output).with_suffix('.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print(json.dumps(report,ensure_ascii=True))
