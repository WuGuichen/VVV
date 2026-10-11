"""Authoring-only integer identity migration for CharacterTest content.

The catalog is persistent; a second execution reads the assigned integers and
does not allocate again. Runtime loaders only validate numbers in the outputs.
"""
from __future__ import annotations
import argparse
import json
from pathlib import Path
import definition_ids

POSE_DOMAIN = 400
EVENT_DOMAIN = 401
PROFILE_DOMAIN = 402
# Pose and geometry references share their existing socket namespace.
SOCKET_DOMAIN = POSE_DOMAIN
TRACE_DOMAIN = 403
INTENT_DOMAIN = 404
SLOT_DOMAIN = 405
TARGET_POLICY_DOMAIN = 406
PRESENTATION_DOMAIN = 407
GROUP_DOMAIN = 408
DAMAGE_TYPE_DOMAIN = 409
TAG_DOMAIN = 410
ANALYSIS_DOMAIN = 411

POSE_BUILTINS = ['default', 'core', 'head', 'left_hand', 'left_leg', 'right_hand',
                 'right_leg', 'shell', 'tail', 'torso', 'spine', 'mainHand',
                 'right_upper_arm', 'right_lower_arm', 'left_upper_arm', 'left_lower_arm',
                 'region:core', 'region:default', 'region:head', 'region:left_hand',
                 'region:left_leg', 'region:right_hand', 'region:right_leg', 'region:shell',
                 'region:tail', 'region:torso', 'ik:left_arm', 'ik:right_arm']
EVENT_BUILTINS = ['character_test.fireball_explosion.afterblast',
                  'fireball_explosion.initial_vfx', 'fireball_explosion.repeat_pulse',
                  'fireball_explosion.repeat_vfx', 'character_test.fireball_explosion.repeat']
PROFILE_BUILTINS = ['movement.default', 'standing', 'crouch', 'low', 'corpse', 'revive_low']

class CharacterIdentityCatalog:
    def __init__(self, path: Path):
        self.path = path
        if not path.exists():
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(json.dumps(definition_ids.create(list(range(400, 412))), indent=2)+'\n', encoding='utf-8')
        self.document = json.loads(path.read_text(encoding='utf-8-sig'))
        definition_ids.validate(self.document)

    def assign(self, domain: int, source: str) -> int:
        if not source:
            return 0
        self.document = definition_ids.transact(self.path, self.document['revision'],
            self.document['catalogHash'], 'allocate', domain, source)
        entries = next(d['entries'] for d in self.document['domains'] if d['domainId'] == domain)
        return next(e['id'] for e in entries if e['sourceKey'] == source and e['status'] == 'active')

    def seed(self):
        for name in POSE_BUILTINS: self.assign(POSE_DOMAIN, name)
        for name in EVENT_BUILTINS: self.assign(EVENT_DOMAIN, name)
        for name in PROFILE_BUILTINS: self.assign(PROFILE_DOMAIN, name)

def migrate_timeline_catalog(catalog: CharacterIdentityCatalog, document: dict) -> bool:
    changed = False
    for timeline in document.get('timelines', []):
        if isinstance(timeline.get('timelineId'), str):
            label = timeline['timelineId']
            timeline['timelineId'] = catalog.assign(EVENT_DOMAIN, label)
            timeline['timelineLabel'] = label
            changed = True
        for event in timeline.get('events', []):
            for field, domain in [('eventId', EVENT_DOMAIN), ('targetTimelineId', EVENT_DOMAIN),
                                  ('requestId', EVENT_DOMAIN), ('presentationProfileId', PRESENTATION_DOMAIN)]:
                if isinstance(event.get(field), str):
                    label = event[field]
                    event[field] = catalog.assign(domain, label)
                    event[field[:-2]+'Label'] = label
                    changed = True
    if changed: document['schemaVersion'] = '2'
    return changed

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--catalog', type=Path, required=True)
    parser.add_argument('--timeline', type=Path, action='append', default=[])
    args = parser.parse_args()
    catalog = CharacterIdentityCatalog(args.catalog)
    catalog.seed()
    for path in args.timeline:
        document = json.loads(path.read_text(encoding='utf-8-sig'))
        if migrate_timeline_catalog(catalog, document):
            path.write_text(json.dumps(document, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print(json.dumps({'revision': catalog.document['revision'], 'catalogHash': catalog.document['catalogHash']}))

if __name__ == '__main__': main()
