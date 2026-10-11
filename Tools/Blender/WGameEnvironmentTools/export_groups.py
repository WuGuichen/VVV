"""Background entrypoint for the shared-detail extension of character baking."""
import argparse
import sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from WGameEnvironmentTools.group_bake import run
parser=argparse.ArgumentParser();parser.add_argument('--output',required=True)
parser.add_argument('--device',choices=('AUTO','CPU','GPU'),default='AUTO')
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:]);run(args.output,args.device)
