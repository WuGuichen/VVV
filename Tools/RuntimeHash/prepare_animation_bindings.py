"""Repair generated animation bindings using existing frozen numeric identities.

Runs after every animation set (including derived equipment sets) is generated.
Does not allocate IDs or modify action/equipment definitions.
"""
import argparse
import json
import os
from pathlib import Path
import tempfile

import definition_ids
from migrate_character_runtime_content import embedded_documents, prepare_direct_clip_bindings


def prepare(root: Path, write: bool = False):
    catalog_path = root / 'Tools/RuntimeHash/character_definition_ids.json'
    catalog_bytes = catalog_path.read_bytes()
    catalog = json.loads(catalog_bytes)
    definition_ids.validate(catalog)
    identities = {(domain['domainId'], entry['sourceKey']): entry['id']
                  for domain in catalog['domains'] for entry in domain['entries']
                  if entry['status'] == 'active'}

    class FrozenIdentities:
        def assign(self, domain, key):
            identity = identities.get((domain, key))
            if identity is None:
                raise ValueError(f'Missing authored binding identity: {domain}/{key}')
            return identity

    paths = sorted((root / 'Assets/MxFrameworkGenerated/CharacterActions').rglob('*.json'))
    animation_paths = sorted((root / 'Assets/MxFrameworkGenerated/CharacterPackages').glob('*/config/animation_set_definition.json'))
    paths += animation_paths
    originals = {path: path.read_bytes() for path in paths}
    documents = [(path, json.loads(originals[path].decode('utf-8-sig'))) for path in paths]
    embedded_path = root / 'Assets/Scripts/WGameFrameworkProject/CharacterTest/Combat/CharacterTestCharacterActionConfigSource.EmbeddedJson.cs'
    _, embedded = embedded_documents(embedded_path)
    prepare_direct_clip_bindings(documents, embedded, FrozenIdentities())
    changes = []
    for path, document in documents:
        if path not in animation_paths:
            continue
        if document == json.loads(originals[path].decode('utf-8-sig')):
            continue
        changes.append((path, json.dumps(document, ensure_ascii=False, indent=2) + '\n'))
    if write:
        if catalog_path.read_bytes() != catalog_bytes:
            raise ValueError('Identity catalog changed during preparation')
        for path, _ in changes:
            if path.read_bytes() != originals[path]:
                raise ValueError(f'Animation set changed during preparation: {path}')
        for path, output in changes:
            temporary = None
            try:
                with tempfile.NamedTemporaryFile(mode='w', encoding='utf-8', newline='\n',
                        dir=path.parent, prefix=path.name + '.', suffix='.tmp', delete=False) as stream:
                    temporary = Path(stream.name)
                    stream.write(output)
                    stream.flush()
                    os.fsync(stream.fileno())
                os.replace(temporary, path)
                temporary = None
            finally:
                if temporary is not None:
                    temporary.unlink(missing_ok=True)
    return {'changedFiles': [str(path.relative_to(root)) for path, _ in changes],
            'animationFilesChecked': len(animation_paths), 'allocatedIds': 0, 'written': write}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument('--write', action='store_true')
    args = parser.parse_args()
    print(json.dumps(prepare(args.root.resolve(), args.write), indent=2))
