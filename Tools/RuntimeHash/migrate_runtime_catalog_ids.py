"""Persist authored numeric references in generated ability/projectile catalogs. Re-running does not allocate new identities."""
from __future__ import annotations
import argparse
import copy
import hashlib
import json
from pathlib import Path
from character_definition_migration import CharacterIdentityCatalog
from emit_character_ids import emit

def migrate(root: Path) -> dict:
    catalog_path = root / 'Tools/RuntimeHash/character_definition_ids.json'
    catalog = CharacterIdentityCatalog(catalog_path)
    active = {(d['domainId'], e['sourceKey']):e['id'] for d in catalog.document['domains'] for e in d['entries'] if e['status']=='active'}
    active_ids = {(d['domainId'], e['id']) for d in catalog.document['domains'] for e in d['entries'] if e['status']=='active'}
    def identity(domain: int, label: str) -> int:
        if not label: return 0
        pair=(domain,label)
        if pair not in active:
            active[pair]=catalog.assign(domain,label)
            active_ids.add((domain,active[pair]))
        return active[pair]
    def field(obj: dict, key: str, domain: int, label_key: str | None=None):
        if obj is None or key not in obj: return
        value=obj[key]
        label_key=label_key or (key[:-2]+'Label' if key.endswith('Id') else key+'Label')
        if isinstance(value,str):
            if label_key in obj and obj[label_key]!=value: raise ValueError('Conflicting source label '+key)
            obj[label_key]=value;obj[key]=identity(domain,value)
        elif type(value) is not int or value<0 or value and (domain,value) not in active_ids:
            raise ValueError('Unknown or invalid authored integer '+key+': '+str(value))
    def damage(obj: dict | None):
        if obj is None:return
        field(obj,'damageTypeId',409);field(obj,'analysisCategory',411)
        tags=obj.get('tags',[])
        if any(isinstance(x,str) for x in tags):
            if not all(isinstance(x,str) for x in tags):raise ValueError('Mixed damage tag identity representation')
            if 'tagLabels' in obj and obj['tagLabels']!=tags:raise ValueError('Conflicting tag labels')
            obj['tagLabels']=list(tags);obj['tags']=[identity(410,x) for x in tags]
        elif any(type(x) is not int or x<=0 or (410,x) not in active_ids for x in tags):
            raise ValueError('Unknown damage tag reference')
    generated=root/'Assets/MxFrameworkGenerated'
    paths=[generated/'Abilities/ability_catalog.json',generated/'Combat/Projectiles/projectile_definitions.json',generated/'Combat/Projectiles/projectile_spawn_profiles.json',generated/'Vfx/vfx_trigger_maps.json']
    docs={p:json.loads(p.read_text(encoding='utf-8-sig')) for p in paths}
    before=copy.deepcopy(docs)
    abilities=docs[paths[0]]
    if str(abilities.get('schemaVersion')) not in ('1','2'):raise ValueError('Unsupported ability source schema')
    for definition in abilities.get('abilities',[]):
        field(definition.get('input'),'inputIntentId',404)
        field(definition.get('input'),'slotId',405)
        field(definition.get('cooldown'),'groupId',408)
        field(definition,'targetPolicyId',406)
        field(definition.get('actionBinding'),'actionIntentId',404)
        damage(definition.get('damage'))
        field(definition,'presentationProfileId',407)
        field(definition,'effectTimelineId',401)
    abilities['schemaVersion']='2'
    projectiles=docs[paths[1]]
    if str(projectiles.get('schemaVersion')) not in ('3','4'):raise ValueError('Unsupported projectile source schema')
    definitions={}
    for definition in projectiles.get('definitions',[]):
        if definition['stableId'] in definitions:raise ValueError('Duplicate native projectile label')
        definitions[definition['stableId']]=definition['definitionId']
        impact=definition.get('impact',{})
        field(impact,'presentationProfileId',407);field(impact,'stableEventId',401);field(impact,'effectTimelineId',401)
        for key in ['hitEvent','areaHitEvent']:
            event=impact.get(key)
            if event is not None:field(event,'stableEventId',401);damage(event.get('damage'))
        damage(impact.get('damage'))
    projectiles['schemaVersion']='4'
    spawns=docs[paths[2]]
    if str(spawns.get('schemaVersion')) not in ('1','2'):raise ValueError('Unsupported spawn source schema')
    for profile in spawns.get('profiles',[]):
        if 'profileId' not in profile:profile['profileId']=identity(401,profile['stableId'])
        if type(profile['profileId']) is not int or (401,profile['profileId']) not in active_ids:raise ValueError('Unknown spawn profile identity')
        native=definitions.get(profile['projectileStableId'])
        if native is None:raise ValueError('Missing native projectile reference '+profile['projectileStableId'])
        if 'projectileDefinitionId' in profile and profile['projectileDefinitionId']!=native:raise ValueError('Conflicting native projectile reference')
        profile['projectileDefinitionId']=native
        field(profile.get('origin'),'socketId',400)
    spawns['schemaVersion']='2'
    triggers=docs[paths[3]]
    if str(triggers.get('schemaVersion')) not in ('1','2'):raise ValueError('Unsupported VFX trigger source schema')
    for profile in triggers.get('presentationProfiles',[]):field(profile,'presentationProfileId',407)
    for mapping in triggers.get('mappings',[]):field(mapping,'presentationProfileId',407)
    triggers['schemaVersion']='2'
    changed=[]
    for p,document in docs.items():
        if document!=before[p]:
            p.write_text(json.dumps(document,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
            changed.append(str(p.relative_to(root)))
    emit(catalog_path,root/'Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/World/CharacterTestAuthoredContentIds.cs')
    result={'revision':catalog.document['revision'],'catalogHash':catalog.document['catalogHash'],'changed':changed,
            'outputs':[{ 'path':str(p.relative_to(root)), 'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in paths]}
    return result

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--project-root',type=Path,default=Path.cwd());parser.add_argument('--report',type=Path)
    args=parser.parse_args();result=migrate(args.project_root.resolve())
    if args.report:args.report.write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(result,indent=2))
