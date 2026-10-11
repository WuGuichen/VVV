"""Extend the existing Composer screen/contract, without writing FairyGUI XML or published assets."""
import copy
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
screen_path = ROOT / 'ArtSource/UIKits/MxUiFoundation/screens/character_test_behavior_deck.ui-screen.json'
contract_path = ROOT / 'ArtSource/UIKits/MxUiFoundation/contracts/character_test_behavior_deck.ui-composer-binding-contract.json'
screen = json.loads(screen_path.read_text(encoding='utf-8-sig'))
contract = json.loads(contract_path.read_text(encoding='utf-8-sig'))
screen['nodes'] = [n for n in screen['nodes'] if not n['name'].startswith('tactical_')]
contract['bindings'] = [b for b in contract['bindings'] if not b['controlName'].startswith('tactical_')]
base = {n['name']: n for n in screen['nodes']}

def node(name, kind, x, y, width, height, text=''):
    key = 'background' if kind == 'Graph' else 'title' if kind == 'Text' else next(k for k,v in base.items() if v['type']=='Image') if kind=='Image' else 'btnUndoRecommendedPreset'
    n = copy.deepcopy(base[key])
    n.update(nodeId=name, name=name, bindingSlot=name, order=200+len(screen['nodes']), visible=False)
    n['layout'] = dict(x=x, y=y, width=width, height=height, horizontalAnchor='Left', verticalAnchor='Top')
    if kind == 'Text':
        n['text'].update(value=text, fontSize=18, singleLine=False, bold=False)
    elif kind == 'Image':
        n['image']['resourceId'] = text
    elif kind == 'ComponentInstance':
        n['component'].update(title=text, iconResourceId='')
        if not name.startswith('tactical_confirm_'):
            icon = 'ui.icon.minimal.view'
            if name.endswith('_remove'): icon = 'ui.icon.minimal.remove'
            elif name.startswith('tactical_slots_') and name.endswith('_use'): icon = 'ui.icon.minimal.slot.enable'
            elif name.endswith('_previous') or name.endswith('_previous_character'): icon = 'ui.icon.minimal.previous'
            elif name.endswith('_next') or name.endswith('_next_character'): icon = 'ui.icon.minimal.next'
            elif name == 'tactical_save': icon = 'ui.icon.minimal.valid'
            elif name == 'tactical_reset': icon = 'ui.icon.menu.reset'
            n['component'].update(componentRecipeId='ui.component.button.icon.compact.neutral',title='',iconResourceId=icon)
            n['layout']['width'] = 44
    screen['nodes'].append(n)
    contract['bindings'].append(dict(slotId=name, controlName=name, nodeType=kind, required=True, bindPath='', commandId=''))

node('tactical_background','Graph',0,94,1920,986)
node('tactical_character','Text',36,106,1100,38,'角色战术内容')
node('tactical_previous_character','ComponentInstance',1190,102,140,42,'上一角色')
node('tactical_next_character','ComponentInstance',1340,102,140,42,'下一角色')
node('tactical_save','ComponentInstance',1490,102,150,42,'重试保存')
node('tactical_reset','ComponentInstance',1650,102,210,42,'清档重开')
node('tactical_status','Text',36,974,1820,80,'')
for section,x,title in [('actions',36,'战术动作'),('cards',658,'战术行为卡'),('slots',1280,'选中卡的槽位')]:
    node(f'tactical_{section}_title','Text',x,164,570,36,title)
    node(f'tactical_{section}_previous','ComponentInstance',x,909,130,42,'上一页')
    node(f'tactical_{section}_page','Text',x+150,915,200,34,'')
    node(f'tactical_{section}_next','ComponentInstance',x+425,909,130,42,'下一页')
    for i in range(7):
        y=218+i*96
        node(f'tactical_{section}_{i}_icon','Image',x,y,34,34, {'actions':'ui.icon.menu.abilities','cards':'ui.icon.minimal.slots','slots':'ui.icon.minimal.slot.enable'}[section])
        node(f'tactical_{section}_{i}_text','Text',x+44,y,510,46,'')
        node(f'tactical_{section}_{i}_use','ComponentInstance',x,y+46,315,38,'')
        node(f'tactical_{section}_{i}_remove','ComponentInstance',x+56,y+46,225,38,'')
node('tactical_confirm_background','Graph',380,330,1160,330)
node('tactical_confirm_text','Text',420,356,1080,205,'')
node('tactical_confirm_yes','ComponentInstance',820,586,310,44,'确认解除并移除')
node('tactical_confirm_no','ComponentInstance',1150,586,310,44,'取消')
screen_path.write_text(json.dumps(screen,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
contract_path.write_text(json.dumps(contract,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Updated tactical inventory blueprint and binding contract.')
