import json
from pathlib import Path
import tempfile
import unittest

import definition_ids
from migrate_behavior_group_ids import CATALOG_PATH, GENERATED_PATH, GROUP_PATH, migrate


class BehaviorGroupIdentityMigrationTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        catalog = definition_ids.create([400, 417])
        catalog = definition_ids.apply(catalog, 'adopt', 417, 'behavior-card/card-a', numeric_id=7)
        catalog = definition_ids.apply(catalog, 'adopt', 417, 'behavior-card/card-b', numeric_id=11)
        self.write(CATALOG_PATH, catalog)
        group = {'schema': 'mx.character-test.runtime-ai-planner.behavior-group-catalog.v3',
            'revision': 12, 'catalogHash': 123, 'groups': []}
        for revision in [4, 5]:
            group['groups'].append({'revision': revision, 'contentHash': 456,
                'entries': [{'entryId': 'entry-a', 'behaviorId': 'legacy-placeholder'},
                            {'entryId': 'entry-b', 'behaviorId': 'legacy-placeholder'}],
                'cards': [{'entryId': 'entry-a', 'stableCardId': 'card-a'},
                          {'entryId': 'entry-b', 'stableCardId': 'card-b'}]})
        self.write(GROUP_PATH, group)

    def write(self, path, value):
        target = self.root / path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(json.dumps(value), encoding='utf-8')

    def read(self, path):
        return json.loads((self.root / path).read_text(encoding='utf-8'))

    def test_repeat_preserves_every_output_and_pinned_identity_across_revisions(self):
        first = migrate(self.root)
        self.assertTrue(first['changed'])
        before = {p: (self.root / p).read_bytes() for p in [CATALOG_PATH, GROUP_PATH, GENERATED_PATH]}
        self.assertFalse(migrate(self.root)['changed'])
        self.assertEqual(before, {p: (self.root / p).read_bytes() for p in before})
        groups = self.read(GROUP_PATH)['groups']
        self.assertEqual(groups[0]['entries'], groups[1]['entries'])
        self.assertEqual([7, 11], [e['definitionNumericId'] for e in groups[0]['entries']])
        self.assertEqual([1, 2], [e['numericId'] for e in groups[0]['entries']])
        self.assertEqual(123, self.read(GROUP_PATH)['catalogHash'])
        self.assertEqual([456, 456], [g['contentHash'] for g in groups])

    def test_conflicting_persisted_identity_does_not_allocate_or_write(self):
        document = self.read(GROUP_PATH)
        document['groups'][0]['entries'][0]['numericId'] = 99
        self.write(GROUP_PATH, document)
        before = {p: (self.root / p).read_bytes() for p in [CATALOG_PATH, GROUP_PATH]}
        with self.assertRaisesRegex(ValueError, 'conflicts'):
            migrate(self.root)
        self.assertEqual(before, {p: (self.root / p).read_bytes() for p in before})

    def test_retired_entry_is_rejected_without_reallocating(self):
        migrate(self.root)
        catalog = definition_ids.apply(self.read(CATALOG_PATH), 'retire', 420, 'behavior-entry/entry-a')
        self.write(CATALOG_PATH, catalog)
        before = (self.root / CATALOG_PATH).read_bytes()
        with self.assertRaisesRegex(ValueError, 'Retired'):
            migrate(self.root)
        self.assertEqual(before, (self.root / CATALOG_PATH).read_bytes())

    def test_unpinned_entry_is_rejected_before_catalog_changes(self):
        document = self.read(GROUP_PATH)
        document['groups'][0]['cards'].pop()
        self.write(GROUP_PATH, document)
        before = (self.root / CATALOG_PATH).read_bytes()
        with self.assertRaisesRegex(ValueError, 'explicit pinned card'):
            migrate(self.root)
        self.assertEqual(before, (self.root / CATALOG_PATH).read_bytes())


if __name__ == '__main__':
    unittest.main()
