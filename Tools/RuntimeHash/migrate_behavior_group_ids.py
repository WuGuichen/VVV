"""Persist numeric identities for existing, pinned behavior group entries.

This authoring migration uses the entry GUID and pinned card reference, never a
display name. Runtime loading only validates the persisted identities.
"""
from __future__ import annotations

import argparse
import copy
import hashlib
import json
import os
from pathlib import Path
import tempfile

import definition_ids
from emit_character_ids import emit

ENTRY_DOMAIN = 420
CARD_DOMAIN = 417
GROUP_PATH = Path('Assets/StreamingAssets/MxFramework/CharacterTest/runtime_ai_planner/behavior_group_catalog.json')
CATALOG_PATH = Path('Tools/RuntimeHash/character_definition_ids.json')
GENERATED_PATH = Path('Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/World/CharacterTestAuthoredContentIds.cs')


def migrate(root: Path) -> dict:
    path = root / GROUP_PATH
    original = path.read_bytes()
    document = json.loads(original.decode('utf-8-sig'))
    if document.get('schema') != 'mx.character-test.runtime-ai-planner.behavior-group-catalog.v3':
        raise ValueError('Expected the authenticated v3 behavior group content contract')
    catalog_path = root / CATALOG_PATH
    catalog = json.loads(catalog_path.read_text(encoding='utf-8-sig'))
    definition_ids.validate(catalog)
    cards = next(d for d in catalog['domains'] if d['domainId'] == CARD_DOMAIN)
    card_ids = {e['sourceKey']: e['id'] for e in cards['entries'] if e['status'] == 'active'}
    candidate = copy.deepcopy(document)
    plans = []
    for group in candidate['groups']:
        references = group.get('cards', [])
        by_entry = {c['entryId']: c for c in references}
        if len(by_entry) != len(references):
            raise ValueError('Duplicate pinned entry reference')
        seen = set()
        for entry in group.get('entries', []):
            entry_id = entry.get('entryId')
            if not isinstance(entry_id, str) or not entry_id or entry_id in seen:
                raise ValueError('Missing or duplicate persistent behavior entry identity')
            seen.add(entry_id)
            reference = by_entry.get(entry_id)
            if reference is None:
                raise ValueError('Only entries with an explicit pinned card can be migrated')
            definition_id = card_ids.get('behavior-card/' + reference['stableCardId'])
            if definition_id is None:
                raise ValueError('Pinned behavior card identity is missing or retired')
            plans.append((entry, 'behavior-entry/' + entry_id, definition_id))

    # The reserved domain is explicitly registered once. Existing and retired
    # entries are never renumbered; an interrupted run can safely be repeated.
    operations = [('declare', '')]
    for source in sorted({source for _, source, _ in plans}):
        operations.append(('allocate', source))
    prepared_catalog = catalog
    for operation, source in operations:
        prepared_catalog = definition_ids.apply(prepared_catalog, operation, ENTRY_DOMAIN, source)
    entries = next(d for d in prepared_catalog['domains'] if d['domainId'] == ENTRY_DOMAIN)
    ids = {e['sourceKey']: e['id'] for e in entries['entries'] if e['status'] == 'active'}
    for entry, source, definition_id in plans:
        for name, value in (('numericId', ids[source]), ('definitionNumericId', definition_id)):
            if name in entry and (type(entry[name]) is not int or entry[name] != value):
                raise ValueError('Persisted identity conflicts with the authoring catalog: ' + name)
            entry[name] = value

    # Validate all pre-existing references before publishing any allocations.
    for operation, source in operations:
        catalog = definition_ids.transact(catalog_path, catalog['revision'],
            catalog['catalogHash'], operation, ENTRY_DOMAIN, source)

    changed = candidate != document
    if changed:
        backup = root / 'Library/NumericHashMigration/behavior_group_catalog.before-numeric-entries.json'
        backup.parent.mkdir(parents=True, exist_ok=True)
        if not backup.exists():
            backup.write_bytes(original)
        temporary = None
        try:
            with tempfile.NamedTemporaryFile(mode='w', encoding='utf-8', newline='\n',
                    dir=path.parent, prefix=path.name + '.', suffix='.tmp', delete=False) as stream:
                temporary = Path(stream.name)
                json.dump(candidate, stream, ensure_ascii=False, indent=2)
                stream.write('\n')
                stream.flush()
                os.fsync(stream.fileno())
            if path.read_bytes() != original:
                raise ValueError('Behavior group catalog changed during migration')
            current = json.loads(catalog_path.read_text(encoding='utf-8-sig'))
            if current != catalog:
                raise ValueError('Numeric catalog changed during migration')
            os.replace(temporary, path)
            temporary = None
        finally:
            if temporary is not None:
                temporary.unlink(missing_ok=True)
    emit(catalog_path, root / GENERATED_PATH)
    return {'changed': changed, 'groups': len(candidate['groups']),
        'entryReferences': len(plans), 'uniqueEntries': len({p[1] for p in plans}),
        'catalogRevision': catalog['revision'], 'catalogHash': catalog['catalogHash'],
        'contentRevision': candidate['revision'], 'contentHash': candidate['catalogHash'],
        'sha256': hashlib.sha256(path.read_bytes()).hexdigest()}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project-root', type=Path, default=Path.cwd())
    parser.add_argument('--report', type=Path)
    args = parser.parse_args()
    report = json.dumps(migrate(args.project_root.resolve()), indent=2) + '\n'
    if args.report:
        args.report.write_text(report, encoding='utf-8')
    print(report)
