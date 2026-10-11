"""Persistent, repeatable authoring migration for CharacterTest numeric identities.

This tool is a repository producer, never a runtime name-to-ID adapter. It reads
the approved semantic domains, allocates through the shared revision/hash CAS,
and writes the resulting IDs and original display labels to formal artifacts.
"""
from __future__ import annotations
import argparse, hashlib, json, re, struct, zlib
import definition_ids
from pathlib import Path
from character_definition_migration import CharacterIdentityCatalog
from migrate_action_vfx_identities import load_references as load_vfx_references, migrate_document as migrate_vfx

POSE_FIELDS = set('partId parentPartId hitZoneId defaultHitZoneId colliderId regionId channelId rootChannelId midChannelId tipChannelId probeId ikBindingId partStateId requiredPartStateId mirrorPeerProbeId socketId startSocketId endSocketId attachSocketId traceStartSocketId traceEndSocketId mirrorPairSocketId zoneId damageZoneId anchorId phaseId bindingId motionSocketId'.split())
POSE_ARRAYS = set('partIds memberPartIds hitZoneIds socketIds regionIds'.split())
EVENT_FIELDS = set('stableEventId sourceAnimationEventId eventId requestId targetTimelineId effectTimelineId'.split())
PROFILE_FIELDS = {'collisionProfileId'}
SLOT_FIELDS = {'slotId', 'equipmentSlotId', 'equipSlot', 'mountedSlot'}
SLOT_ARRAYS = {'occupiesSlots', 'slotIds', 'requiredFilledSlots', 'requiredEmptySlots'}
FORMULAS = {'': 0, 'flat': 1, 'attack_minus_defense': 2, 'attack_scaled_minus_defense': 3, 'ct.soft_defense_v1': 4}
REACTION_GROUPS = {'': 0, 'reaction.height.high': 1, 'reaction.height.mid': 2, 'reaction.height.low': 3,
    'reaction.special.launch': 4, 'reaction.side.right': 5, 'reaction.special.stun': 6,
    'reaction.humanoid.body': 7, 'reaction.humanoid.head': 8, 'reaction.humanoid.limb': 9,
    'reaction.000003.default': 10}
# Freeze the identities already used by published CharacterTest worlds. These
# are adopted once; future authored packages receive directory allocations.
EXISTING_RUNTIME_CHARACTERS = {'000001': 668367834, '000002': 651590215,
    '000003': 634812596, '000004': 618034977, '000007': 567702120, '000008': 550924501}

def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()

def canonical(value) -> bytes:
    return json.dumps(value, ensure_ascii=False, separators=(',', ':'), sort_keys=True).encode('utf-8')

def json_bytes(value) -> bytes:
    return (json.dumps(value, ensure_ascii=False, indent=2)+'\n').encode('utf-8')

def unsigned(value):
    out=bytearray()
    while value>=128: out.append((value&127)|128);value>>=7
    out.append(value);return bytes(out)

def migrate_hurtbox_binary(data, assign):
    if len(data)<6: raise ValueError('Truncated hurtbox binary')
    version=struct.unpack_from('<H',data,4)[0]
    if version==4: return data
    if version!=2: raise ValueError('Unsupported old hurtbox binary version')
    raw=zlib.decompress(data[6:],-15); cursor=0
    def read_unsigned():
        nonlocal cursor
        value=shift=0
        while shift<64:
            item=raw[cursor];cursor+=1;value|=(item&127)<<shift
            if item<128:return value
            shift+=7
        raise ValueError('Hurtbox varint exceeds 64 bits')
    def read_string():
        nonlocal cursor
        length=read_unsigned();value=raw[cursor:cursor+length];cursor+=length;return value
    count=read_unsigned();out=bytearray(unsigned(count))
    for _ in range(count):
        clip=read_string();collider=read_string().decode('utf-8')
        out+=unsigned(len(clip))+clip+unsigned(assign(400,collider))
        start=cursor
        read_unsigned();runs=read_unsigned()
        for _ in range(runs):
            read_unsigned();cursor+=1
            for _ in range(19):read_unsigned()
        out+=raw[start:cursor]
    if cursor!=len(raw): raise ValueError('Unexpected hurtbox binary tail')
    encoder=zlib.compressobj(level=9,wbits=-15)
    return data[:4]+struct.pack('<H',4)+encoder.compress(out)+encoder.flush()

def lowkey(key): return key[:1].lower()+key[1:]

def prepare_event_orders(value, orders):
    if isinstance(value,list):
        for child in value: prepare_event_orders(child,orders)
    elif isinstance(value,dict):
        if 'kind' in value and isinstance(value.get('stableEventId'),int):
            number=value['stableEventId']
            if number>0 and number not in orders: raise ValueError('Unknown authored event order: '+str(number))
            value['stableEventOrder']=orders.get(number,0)
        for child in value.values(): prepare_event_orders(child,orders)

def prepare_direct_clip_bindings(documents, embedded, migration):
    keys=set()
    def visit(node):
        if isinstance(node,list):
            for value in node: visit(value)
        elif isinstance(node,dict):
            if node.get('kind') in ('PlayAnimation','CrossFadeAnimation','SetAnimationBlend'):
                key=node.get('animationClipResourceKey') or node.get('animationActionKey')
                if isinstance(key,str) and key.strip():keys.add(key)
            for value in node.values():visit(value)
    for _,document in documents: visit(document)
    for _,document in embedded: visit(document)
    for path,document in documents:
        if path.name!='animation_set_definition.json':continue
        for animation_set in document.get('sets',[]):
            existing={item.get('actionId') for item in animation_set.get('actionBindings',[])}
            prepared=[]
            for key in sorted(keys-existing):
                label='runtime.clip.'+key
                prepared.append({'bindingId':migration.assign(400,label),'bindingLabel':label,'clipKey':key})
            animation_set['preparedClipBindings']=prepared

def label_key(key):
    if key.endswith('Ids'): return key[:-3]+'Labels'
    if key.endswith('Id'): return key[:-2]+'Label'
    return key+'Label'

class Migration:
    def __init__(self, root: Path):
        self.root = root
        self.catalog = CharacterIdentityCatalog(root/'Tools/RuntimeHash/character_definition_ids.json')
        self.assigned = {(domain['domainId'], entry['sourceKey']):entry['id']
            for domain in self.catalog.document['domains'] for entry in domain['entries'] if entry['status']=='active'}
        self.entries = []
        self.combat_actions = {}
        self.combat_labels = set()
        self.weapons = {}
        self.audio = {}
        self.abilities = {}
        self.contact_points = {}
        audio_path=root/'Assets/MxFrameworkGenerated/Audio/audio_runtime_catalog.json'
        for cue in json.loads(audio_path.read_text(encoding='utf-8-sig')).get('cues',[]):
            self.audio[cue['audioCueId']]=cue['id']
        for ability in json.loads((root/'Assets/MxFrameworkGenerated/Abilities/ability_catalog.json').read_text(encoding='utf-8-sig')).get('abilities',[]):
            self.abilities[ability['abilityStableId']]=ability['abilityId']

    def native(self, values, value, kind):
        if value == '': return 0
        if isinstance(value,int): return value
        if value not in values: raise ValueError(f'No existing {kind} ID for {value!r}')
        return values[value]

    def assign(self, domain, value):
        if isinstance(value,int): return value
        if not isinstance(value,str): raise ValueError(f'Invalid identity {value!r} in {domain}')
        if value == '': return 0
        if domain==412 and re.fullmatch(r'[0-9]+',value):
            number=int(value)
            if number<=0 or number>2147483647: raise ValueError('Native combat identity outside Int32')
            return number
        pair=(domain,value)
        if pair not in self.assigned: self.assigned[pair]=self.catalog.assign(domain,value)
        return self.assigned[pair]

    def inventory(self, documents):
        def visit(obj):
            if isinstance(obj,dict):
                if 'actions' in obj:
                    for action in obj['actions']:
                        if not isinstance(action,dict): continue
                        aid=action.get('actionId')
                        label=(action.get('combatTrack') or {}).get('combatActionId')
                        if label and isinstance(label,str) and isinstance(aid,int):
                            self.combat_labels.add(label)
                if 'bodyPartContactPoints' in obj:
                    for point in obj['bodyPartContactPoints']:
                        label=point.get('stableId'); number=point.get('contactPointId')
                        if label and isinstance(number,int): self.contact_points[label]=number
                for entry in (obj.get('patch') or {}).get('entries',[]):
                    if entry.get('source')=='WeaponConfig':
                        fields=entry['fields']; number=int(fields.get('WeaponId') or entry['id'])
                        for label in (fields.get('StableId'), fields.get('WeaponKey')):
                            if label:
                                old=self.weapons.setdefault(label,number)
                                if old!=number: raise ValueError(f'Conflicting existing weapon identity {label}')
                                # CharacterAuthoringCompiler's explicit source projection
                                # uses "mx.weapon." + weaponSegment; recover that source key
                                # only here in the authoring migration.
                                if label.startswith('mx.weapon.'):
                                    self.weapons[label[len('mx.weapon.'):]]=number
                for value in obj.values(): visit(value)
            elif isinstance(obj,list):
                for value in obj: visit(value)
        for _,document in documents: visit(document)

    def convert(self, obj, path=(), family='action'):
        if isinstance(obj,list):
            for index,value in enumerate(obj): self.convert(value,path+(str(index),),family)
            return
        if not isinstance(obj,dict): return
        if family=='action' and 'requirements' in obj:
            for requirement in obj.get('requirements') or []:
                kind=requirement.get('kind','None');label=requirement.get('stableId','');previous=requirement.get('referenceId',0)
                if any(c.isspace() for c in label):raise ValueError('Requirement reference label contains whitespace')
                if kind=='ResourceAvailable':
                    reference=int(label) if label else previous
                    if reference<=0:raise ValueError('Requirement resource attribute is missing')
                elif kind=='CooldownReady':
                    reference=self.native(self.abilities,label,'ability') if label else previous
                    if reference<=0 or reference not in self.abilities.values():raise ValueError('Requirement ability reference is missing')
                elif kind in ('EquipmentTagRequired','EquipmentTagForbidden'):
                    reference=self.assign(416,label) if label else previous
                    if reference<=0:raise ValueError('Requirement equipment tag is missing')
                else:reference=previous
                if previous not in (0,reference):raise ValueError('Conflicting requirement ReferenceId')
                requirement['referenceId']=reference
        damage=any(segment in ('damage','damagePayload') for segment in path)
        for key,value in list(obj.items()):
            k=lowkey(key); domain=None; native=None
            if family in ('geometry','config','socket','trace') and k in POSE_FIELDS: domain=400
            if family=='animation' and k=='bindingId': domain=400
            if family=='action' and k in {'stableEventId','sourceAnimationEventId','requestId','requiredPartStateId','startSocketId','endSocketId','collisionProfileId','intentId','combatActionId','traceProfileId','equipmentSlotId','targetPolicyId','cueId','reactionGroupId','damageTypeId','formulaId','analysisCategory','presentationProfileId'}:
                domain=400 if k in POSE_FIELDS else None
            if k in EVENT_FIELDS and family=='action': domain=401
            if k in PROFILE_FIELDS: domain=402
            if k=='movementProfileId' and family=='action': domain=402
            if k in ('traceId','traceProfileId') and family in ('action','geometry','trace'): domain=403
            if k=='intentId' and family=='action': domain=404
            if k in SLOT_FIELDS and family in ('action','geometry','config'): domain=405
            if k=='targetPolicyId' and family=='action': domain=406
            if k=='presentationProfileId' and family=='action': domain=407
            if k=='damageTypeId' and family=='action': domain=409
            if k=='analysisCategory' and family=='action': domain=411
            if k=='combatActionId' and family=='action': domain=412
            if k=='abilityStableId' and family=='action': native=self.abilities
            if k=='cueId' and family=='action':
                if obj.get('kind')=='PlayAudioCue': native=self.audio
                else: domain=401
            if k=='weaponId' and family=='geometry': native=self.weapons
            if k=='reactionGroupId' and family in ('action','config','geometry'): native=REACTION_GROUPS
            if k=='formulaId' and family=='action': native=FORMULAS
            if k=='stableId' and len(path)>=2 and path[-2]=='attachments' and family=='geometry':
                kind=obj.get('kind','')
                if kind=='BodyPartContactPoint': native=self.contact_points
                else: domain=400
            if isinstance(value,str) and (domain is not None or native is not None):
                obj[label_key(key)]=value
                obj[key]=1 if family=='geometry' and k=='regionId' and not value.strip() else self.native(native,value,k) if native is not None else self.assign(domain,value)
            elif isinstance(value,list) and ((family in ('geometry','config') and k in POSE_ARRAYS) or k in SLOT_ARRAYS or k=='allowedPostureIds' or (damage and k=='tags')):
                array_domain=405 if k in SLOT_ARRAYS else 402 if k=='allowedPostureIds' else 410 if k=='tags' else 400
                if any(isinstance(item,str) for item in value):
                    obj[label_key(key) if k!='tags' else key[:-4]+'tagLabels' if key!='tags' else 'tagLabels']=list(value)
                    obj[key]=[self.assign(array_domain,item) for item in value]
            else: self.convert(value,path+(k,),family)

    def migrate_document(self,path,document):
        name=path.name
        family='action'
        if name in ('geometry_binding.json','character_geometry_binding.json'): family='geometry'
        elif name in ('character_config_patch.json','character_application_config_patch.json'): family='config'
        elif name=='character_socket_bake_catalog.json': family='socket'
        elif name=='weapon_trace_bake_catalog.json': family='trace'
        elif name=='animation_set_definition.json': family='animation'
        self.convert(document,family=family)
        if family=='config':
            package=document.get('packageId','')
            if not package: raise ValueError('Character package identity is missing')
            number=self.assign(413,package)
            if document.get('runtimeCharacterId',0) not in (0,number): raise ValueError('Conflicting runtime character identity')
            document['runtimeCharacterId']=number
        if family=='action' and 'schemaVersion' in document:
            document['schemaVersion']='0.5.0' if 'actions' in document else '0.6.0'
        if family=='geometry':
            for zone in document.get('damageZones',[]): zone['hitZoneIds']=sorted(zone.get('hitZoneIds',[]))
            old=document.get('format','')
            if old!='mx.characterGeometryBinding.v8':
                features=(2 if isinstance(document.get('engagementAnchors'),list) else 0)|(4 if isinstance(document.get('bodyPresenceVolumes'),list) else 0)|(8 if isinstance(document.get('animatedHurtboxSources'),list) else 0)|(16 if isinstance(document.get('bodyPartDamageDefinitions'),list) else 0)|(32 if isinstance(document.get('authorityPoseRig'),dict) else 0)
                document['features']=features
                document['format']='mx.characterGeometryBinding.v8';document['schemaVersion']='8.0'
            rig=document.get('authorityPoseRig')
            if rig:
                rig['format']='mx.characterAuthorityPoseRig.v2';rig['schemaVersion']='2.0'
            if document.get('bodyPartDamageFormat') in ('mx.characterBodyPartDamage.v1','mx.characterBodyPartDamage.v2'):
                document['bodyPartDamageContentEncodingVersion']=1 if document['bodyPartDamageFormat'].endswith('.v1') else 2
                document['bodyPartDamageFormat']='mx.characterBodyPartDamage.v3';document['bodyPartDamageSchemaVersion']='3.0'
            if isinstance(document.get('bodyPartDamagePartLookup'),list):
                document['bodyPartDamagePartLookup'].sort(key=lambda value:value['partId'])
        if family=='socket': document['format']='mx.character.socketBakeCatalog.v2'
        if family=='trace':
            document['format'] = 'mx.character.weaponTraceBakeCatalog.v3' if 'weapons' in document else 'mx.character.weaponTraceBakeCatalog.v2'
        if family=='animation':
            document['format']='mx.animationSetDefinition.v2';document['schemaVersion']='2.0'
        return document

    def write(self,path,data):
        before=path.read_bytes() if path.exists() else b''
        if before!=data:
            path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(data)
        self.entries.append({'path':path.relative_to(self.root).as_posix(),'sha256':sha(data),'size':len(data)})

def embedded_documents(path):
    text=path.read_text(encoding='utf-8-sig')
    pattern=re.compile(r'(private const string (\w+Json)\s*=\s*@")((?:[^"]|"")*)(";)',re.S)
    documents=[]
    for match in pattern.finditer(text):
        documents.append((match,json.loads(match[3].replace('""','"'))))
    return text,documents

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root',type=Path,default=Path(__file__).resolve().parents[2])
    parser.add_argument('--apply',action='store_true')
    args=parser.parse_args();root=args.root.resolve();migration=Migration(root)
    paths=list((root/'Assets/MxFrameworkGenerated/CharacterActions').rglob('*.json'))
    names={'geometry_binding.json','character_geometry_binding.json','character_config_patch.json','character_application_config_patch.json','character_socket_bake_catalog.json','weapon_trace_bake_catalog.json','animation_set_definition.json'}
    paths += [p for p in (root/'Assets/MxFrameworkGenerated/CharacterPackages').rglob('*.json') if p.name in names]
    paths=sorted(set(paths))
    docs=[(p,json.loads(p.read_text(encoding='utf-8-sig'))) for p in paths]
    embedded=root/'Assets/Scripts/WGameFrameworkProject/CharacterTest/Combat/CharacterTestCharacterActionConfigSource.EmbeddedJson.cs'
    text, embedded_docs=embedded_documents(embedded)
    migration.inventory(docs+[(embedded,d) for _,d in embedded_docs])
    if not args.apply:
        print(json.dumps({'documents':len(docs),'embeddedDocuments':len(embedded_docs),'combatDefinitions':sorted(migration.combat_labels),'nativeWeapons':migration.weapons},indent=2));return
    catalog=migration.catalog
    catalog.document=definition_ids.transact(catalog.path,catalog.document['revision'],catalog.document['catalogHash'],'declare',412,'')
    catalog.document=definition_ids.transact(catalog.path,catalog.document['revision'],catalog.document['catalogHash'],'declare',413,'')
    catalog.document=definition_ids.transact(catalog.path,catalog.document['revision'],catalog.document['catalogHash'],'declare',416,'')
    for package,number in EXISTING_RUNTIME_CHARACTERS.items():
        catalog.document=definition_ids.transact(catalog.path,catalog.document['revision'],catalog.document['catalogHash'],'adopt',413,package,numeric_id=number)
        migration.assigned[(413,package)]=number
    outputs=[]
    vfx_defs,sockets=load_vfx_references(root/'Assets/MxFrameworkGenerated/Vfx/vfx_definitions.json',catalog.path)
    for p,d in docs: migration.migrate_document(p,d)
    for match,d in embedded_docs: migration.migrate_document(Path(match[2]+'.json'),d)
    prepare_direct_clip_bindings(docs,embedded_docs,migration)
    event_domain=next(domain for domain in catalog.document['domains'] if domain['domainId']==401)
    event_orders={entry['id']:rank for rank,entry in enumerate(sorted(event_domain['entries'],key=lambda entry:entry['sourceKey'].encode('utf-16-be',errors='surrogatepass')),1) if entry['status']=='active'}
    for p,d in docs:
        if 'CharacterActions' in p.parts:
            migrate_vfx(d,vfx_defs,sockets);prepare_event_orders(d,event_orders)
        outputs.append((p,json_bytes(d)))
    for match,d in reversed(embedded_docs):
        migrate_vfx(d,vfx_defs,sockets)
        prepare_event_orders(d,event_orders)
        encoded=json_bytes(d).decode('utf-8').rstrip().replace('"','""')
        text=text[:match.start(3)]+'\n'+encoded+text[match.end(3):]
    outputs.append((embedded,text.encode('utf-8')))
    for path in sorted((root/'Assets/MxFrameworkGenerated/CharacterPackages').rglob('character_hurtbox_bake_catalog.json')):
        document=json.loads(path.read_text(encoding='utf-8-sig'))
        if document.get('format') in ('mx.character.hurtboxBakeCatalog.v2','mx.character.hurtboxBakeCatalog.v4'):
            binary_path=path.parent.parent/document['binaryPath']
            old=binary_path.read_bytes();data=migrate_hurtbox_binary(old,migration.assign)
            document.setdefault('legacyBinaryHash',document['binaryHash'])
            document['format']='mx.character.hurtboxBakeCatalog.v4'
            document['binaryHash']='sha256:'+sha(data);document['binarySize']=len(data)
            outputs.extend([(path,json_bytes(document)),(binary_path,data)])
        else: raise ValueError('Unsupported hurtbox source format: '+str(path))
    for path,data in outputs: migration.write(path,data)
    out=root/'Tools/RuntimeHash/character_content_migration_outputs.json'
    out.write_bytes(json_bytes({'schema':'mx.character-numeric-content-migration.v1','catalogRevision':migration.catalog.document['revision'],'entries':migration.entries}))
    print(json.dumps({'documents':len(docs),'embeddedDocuments':len(embedded_docs),'catalogRevision':migration.catalog.document['revision']}))

if __name__=='__main__': main()
