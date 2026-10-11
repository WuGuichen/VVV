"""Preview or permanently delete owned data for numeric characters.

Default is read-only. --apply requires the exact planHash from a fresh preview.
This removes working-tree data, not Git history or copies outside the repository.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import stat
import sys
import subprocess
import tempfile


CATALOG = "Authoring/CharacterPackages/character_id_catalog.json"
RESOURCE_MAPPING = "Assets/Config/MxFramework/ResourceProfiles/global_resource_mapping_profile.v2.json"
TACTICAL_ACQUISITION = "Assets/StreamingAssets/MxFramework/CharacterTest/tactical_acquisition.json"
RUNTIME_CATALOG = "Assets/StreamingAssets/MxFramework/Resources/global_runtime_catalog.json"
OWNED_ROOTS = (
    "ArtSource/Characters/{id}",
    "Authoring/CharacterPackages/character-{id}",
    "ArtSource/Archive/Characters/{id}",
    "Authoring/Archive/CharacterPackages/character-{id}",
    "Assets/MxFrameworkGenerated/CharacterPackages/{id}",
    "Assets/MxFrameworkGenerated/CharacterActions/Profiles/{id}",
    "Assets/MxFrameworkGenerated/CharacterActions/ActionSets/{id}",
    "Assets/MxFrameworkGenerated/CharacterActions/Libraries/{id}",
)
TEXT_SUFFIXES = {".json", ".yaml", ".yml", ".asset", ".prefab", ".unity", ".controller", ".anim", ".overridecontroller", ".meta", ".txt", ".xml", ".uxml"}
SKIP = {".git", "Library", "Temp", "Logs", "node_modules", "bin", "obj", ".agent"}


def digest(data):
    return hashlib.sha256(data).hexdigest()


def linked(path):
    try:
        info = path.lstat()
    except FileNotFoundError:
        return False
    return stat.S_ISLNK(info.st_mode) or bool(getattr(info, "st_file_attributes", 0) & getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0))


def safe_path(root, relative):
    path = root / relative
    path.resolve().relative_to(root)
    for part in (path, *path.parents):
        if part == root:
            break
        if linked(part):
            raise ValueError(f"Linked path is not allowed: {part}")
    return path


def files_under(path):
    if path.is_file():
        yield path
        return
    if not path.exists():
        return
    for directory, dirs, files in os.walk(path, followlinks=False):
        for name in dirs + files:
            candidate = Path(directory) / name
            if linked(candidate):
                raise ValueError(f"Linked content is not allowed: {candidate}")
        for name in files:
            yield Path(directory) / name


def references_id(text, value, pattern):
    """Search near ID occurrences, avoiding full regex scans of large numeric mesh data."""
    position = text.find(value)
    while position >= 0:
        end = position + len(value)
        if pattern.search(text, max(0, position - 80), min(len(text), end + 1)):
            return True
        # Also cover unquoted YAML IDs with arbitrarily long whitespace before the value.
        previous = position - 1
        while previous >= 0 and text[previous].isspace():
            previous -= 1
        if previous >= 0 and text[previous] == ":" and (end == len(text) or not text[end].isdigit()):
            key_end = previous
            while key_end > 0 and text[key_end - 1].isspace():
                key_end -= 1
            if text.endswith(("characterId", "packageId"), 0, key_end):
                return True
        position = text.find(value, end)
    return False


def has_owned_guid(text, guids):
    lower = text.lower()
    for guid in guids:
        position = lower.find(guid)
        while position >= 0:
            end = position + 32
            before = position > 0 and (lower[position-1].isalnum() or lower[position-1] == '_')
            after = end < len(lower) and (lower[end].isalnum() or lower[end] == '_')
            if not before and not after:return True
            position = lower.find(guid, end)
    return False


def reference_candidates(root, scopes, ids, guids):
    """Optional native literal prefilter; exact checks and link audit still run below."""
    executable = shutil.which('rg')
    if not executable:return None
    command = [executable, '--files-with-matches', '--null', '--fixed-strings', '--ignore-case',
               '--hidden', '--no-ignore', '--text', '--path-separator', '/']
    for suffix in sorted(TEXT_SUFFIXES):command += ['--iglob', '*' + suffix]
    for directory in sorted(SKIP):command += ['--glob', '!**/' + directory + '/**']
    result = []
    for needles in (ids, guids):
        if not needles:
            result.append(set());continue
        scan = subprocess.run(command + ['--file', '-', '--'] + scopes, cwd=root,
            input='\n'.join(sorted(needles)) + '\n', encoding='utf-8',
            stdout=subprocess.PIPE, stderr=subprocess.PIPE,
            creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
        if scan.returncode not in (0, 1):raise ValueError('Reference scan failed: ' + scan.stderr)
        result.append({p for p in scan.stdout.split('\0') if p})
    return result


def shared_record_edits(root, ids):
    """Project only explicitly owned registrations; retained content is still scanned."""
    edits, projected, runtime_owners = [], {}, {}
    for relative, schema, collection in (
        (RESOURCE_MAPPING, 'mx.resource-mapping-profile.v2', 'entries'),
        (TACTICAL_ACQUISITION, 'mx.character-test.tactical-acquisition.v1', 'characters'),
        (RUNTIME_CATALOG, None, 'entries')):
        path = safe_path(root, relative)
        if not path.exists():continue
        before = path.read_bytes()
        document = json.loads(before.decode('utf-8-sig'))
        valid_schema = isinstance(document, dict) and (document.get('schema') == schema if schema else
            document.get('schemaVersion') == 1 and document.get('catalogId') == 'global.runtime' and document.get('packageId') == '')
        if not valid_schema or not isinstance(document.get(collection), list):
            raise ValueError('Unsupported shared registration document: ' + relative)
        kept, removed = [], []
        for record in document[collection]:
            owner = None
            if isinstance(record, dict):
                for cid in ids:
                    if relative == RESOURCE_MAPPING:
                        receipt = record.get('producerReceipt') or {}
                        if (isinstance(receipt, dict) and receipt.get('producerId') == 'character.package.' + cid
                            and receipt.get('producerContractVersion') == '1'
                            and isinstance(record.get('authoringRecordId'), str)
                            and record['authoringRecordId'].startswith('charpkg.' + cid + '.')):
                            owner = {'characterId': cid, 'recordId': record['authoringRecordId']}
                            key, source = record.get('resourceKey') or {}, record.get('sourceLocator') or {}
                            if isinstance(key, dict) and isinstance(source, dict):
                                asset_path = source.get('cachedPath', '')
                                if (source.get('kind') == 'generatedUnityAsset' and key.get('packageId') == cid and isinstance(key.get('id'), str)
                                    and key['id'].startswith('char.' + cid + '.')
                                    and isinstance(key.get('typeId'), str) and isinstance(key.get('variant'), str)
                                    and isinstance(asset_path, str) and asset_path.startswith('Assets/MxFrameworkGenerated/CharacterPackages/' + cid + '/')
                                    and not any(p in {'.', '..', ''} for p in asset_path.split('/')) and '\\' not in asset_path):
                                    identity = (key['id'], key.get('typeId'), key.get('variant'), cid)
                                    runtime_owners.setdefault(identity, set()).add(asset_path)
                    elif relative == RUNTIME_CATALOG:
                        identity = (record.get('id'), record.get('type'), record.get('variant'), record.get('packageId'))
                        provider = record.get('providerData') or {}
                        if record.get('packageId') == cid and record.get('provider') == 'assetBundle' and isinstance(provider, dict):
                            asset_path, bundle = provider.get('assetPath'), provider.get('bundleName')
                            if (all(isinstance(v, str) for v in identity) and isinstance(asset_path, str)
                                and asset_path in runtime_owners.get(identity, set()) and isinstance(bundle, str) and bundle
                                and record.get('address') == bundle + '|' + asset_path):
                                owner = {'characterId': cid, 'recordId': record['id']}
                    elif (record.get('characterStableId') == 'char.' + cid
                          and set(record) == {'characterStableId', 'actionIds', 'cards'}
                          and record['actionIds'] == [] and record['cards'] == []):
                        owner = {'characterId': cid, 'recordId': record['characterStableId']}
            if owner is None:kept.append(record)
            else:removed.append(owner)
        if not removed:continue
        document[collection] = kept
        after = (json.dumps(document, ensure_ascii=False, indent=2) + '\n').encode('utf-8')
        projected[relative] = after
        edits.append({'path': relative, 'beforeHash': digest(before), 'afterHash': digest(after),
                      'kind': 'characterResourceRegistration' if relative == RESOURCE_MAPPING else
                              'characterRuntimeRegistration' if relative == RUNTIME_CATALOG else 'emptyTacticalAcquisition',
                      'recordCount': len(removed), 'records': removed})
    # Keep mapping ownership proof until runtime cleanup has succeeded, so an
    # interrupted cleanup can be safely planned again without guessing ownership.
    edits.sort(key=lambda e: e['path'] != RUNTIME_CATALOG)
    return edits, projected


def write_shared_edit(root, edit, content):
    path = safe_path(root, edit['path'])
    if digest(path.read_bytes()) != edit['beforeHash'] or digest(content) != edit['afterHash']:
        raise ValueError('Shared registration changed: preview again before applying')
    temporary = None
    try:
        with tempfile.NamedTemporaryFile(dir=path.parent, prefix=path.name + '.delete-', delete=False) as stream:
            temporary = Path(stream.name)
            stream.write(content);stream.flush();os.fsync(stream.fileno())
        os.replace(temporary, path)
    finally:
        if temporary is not None:temporary.unlink(missing_ok=True)


def build_plan(root, character_ids):
    root = Path(root).resolve(strict=True)
    ids = sorted(set(character_ids))
    if not ids or any(not re.fullmatch(r"\d{6}", value, re.ASCII) or value == "000000" for value in ids):
        raise ValueError("IDs must be six ASCII digits from 000001 to 999999")
    catalog_path = safe_path(root, CATALOG)
    catalog_bytes = catalog_path.read_bytes()
    catalog = json.loads(catalog_bytes.decode("utf-8-sig"))
    if catalog.get("schemaVersion") != "1.0" or catalog.get("width") != 6:
        raise ValueError("Unsupported character ID catalog")
    known = {entry["id"] for entry in catalog["entries"]} | set(catalog["retiredIds"])
    if set(ids) - known:
        raise ValueError("Unknown IDs: " + ", ".join(sorted(set(ids) - known)))
    paths, manifest, guids = [], [], set()
    for character_id in ids:
        for template in OWNED_ROOTS:
            relative = template.format(id=character_id)
            for candidate_relative in (relative, relative + ".meta"):
                path = safe_path(root, candidate_relative)
                if not path.exists():
                    continue
                paths.append(candidate_relative)
                for file in files_under(path):
                    content = file.read_bytes()
                    manifest.append({"path": file.relative_to(root).as_posix(), "bytes": len(content), "sha256": digest(content)})
                    if file.suffix == ".meta":
                        guids.update(re.findall(r"(?m)^guid:\s*([a-fA-F0-9]{32})\s*$", content.decode("utf-8-sig", errors="replace")))
    paths.sort()
    manifest.sort(key=lambda item: item["path"])
    owned_files = {item["path"] for item in manifest}
    shared_edits, projected = shared_record_edits(root, ids)
    owned_guids = {guid.lower() for guid in guids}
    scopes = [scope for scope in ("Assets", "Authoring", "ArtSource", "Packages") if safe_path(root, scope).exists()]
    candidates = reference_candidates(root, scopes, ids, owned_guids) if scopes else [set(), set()]
    blockers = []
    patterns = [re.compile(
        rf'''(?:["']{value}["']|\b(?:char|charpkg|body|attr|character\.package)\.{value}(?!\d)|\bcharacter-{value}(?!\d)|(?:Characters|CharacterPackages|Profiles|ActionSets|Libraries)[/\\]+{value}(?!\d)|\b(?:characterId|packageId)\s*:\s*{value}(?!\d))'''
    ) for value in ids]
    for scope in scopes:
        start = safe_path(root, scope)
        if not start.exists():
            continue
        for directory, dirs, files in os.walk(start, followlinks=False):
            if any(linked(Path(directory) / name) for name in dirs if name not in SKIP):
                raise ValueError(f"Cannot fully check references through linked content: {directory}")
            dirs[:] = [name for name in dirs if name not in SKIP]
            relative_directory = Path(directory).relative_to(root).as_posix()
            for name in files:
                if os.path.splitext(name)[1].lower() not in TEXT_SUFFIXES:continue
                relative = relative_directory + '/' + name
                if relative in owned_files or relative == CATALOG:
                    continue
                file = Path(directory) / name
                if linked(file):
                    raise ValueError(f"Cannot check a linked reference file: {file}")
                if relative not in projected and candidates is not None and relative not in candidates[0] and relative not in candidates[1]:
                    continue
                text = projected[relative].decode('utf-8') if relative in projected else file.read_text(encoding="utf-8-sig", errors="replace")
                has_guid = bool(owned_guids) and (candidates is None or relative in candidates[1]) and has_owned_guid(text, owned_guids)
                if not any(value in text for value in ids) and not has_guid:
                    continue
                reasons = [ids[i] for i, pattern in enumerate(patterns) if references_id(text, ids[i], pattern)]
                if has_guid:
                    reasons.append("owned Unity GUID")
                if reasons:
                    blockers.append({"path": relative, "references": reasons})
    plan = {"schemaVersion": "1.0", "root": str(root), "characterIds": ids,
            "catalogHash": digest(catalog_bytes), "paths": paths, "files": manifest,
            "fileCount": len(manifest), "bytes": sum(item["bytes"] for item in manifest),
            "blockingReferences": sorted(blockers, key=lambda item: item["path"]),
            "sharedEdits": shared_edits,
            "nextCandidate": catalog["nextCandidate"],
            "status": "blocked" if blockers else "ready" if paths or shared_edits or any(e["id"] in ids for e in catalog["entries"]) else "already_deleted"}
    plan["planHash"] = digest(json.dumps(plan, sort_keys=True, ensure_ascii=False).encode("utf-8"))
    return plan


def apply_plan(root, character_ids, expected_hash):
    root = Path(root).resolve(strict=True)
    lock = safe_path(root, "Authoring/CharacterPackages/.character-permanent-delete.lock")
    handle = os.open(lock, os.O_CREAT | os.O_EXCL | os.O_WRONLY)
    try:
        os.close(handle)
        plan = build_plan(root, character_ids)
        if plan["planHash"] != expected_hash:
            raise ValueError("Plan changed: preview again before applying")
        if plan["blockingReferences"]:
            raise ValueError("External references block deletion; resolve them first")
        if plan["status"] == "already_deleted":
            return plan
        shared_edits, projected = shared_record_edits(root, plan['characterIds'])
        if shared_edits != plan['sharedEdits']:
            raise ValueError('Shared registration changed: preview again before applying')
        catalog_path = safe_path(root, CATALOG)
        catalog = json.loads(catalog_path.read_text(encoding="utf-8-sig"))
        ids = set(plan["characterIds"])
        # Retire first: even an interrupted cleanup cannot make an old ID reusable.
        retired = sorted(set(catalog["retiredIds"]) | ids)
        entries = [entry for entry in catalog["entries"] if entry["id"] not in ids]
        if retired != catalog["retiredIds"] or entries != catalog["entries"]:
            catalog.update(entries=entries, retiredIds=retired, revision=catalog["revision"] + 1)
            temporary = catalog_path.with_suffix(".delete-tmp")
            with temporary.open("x", encoding="utf-8") as stream:
                stream.write(json.dumps(catalog, ensure_ascii=False, indent=2) + "\n")
                stream.flush()
                os.fsync(stream.fileno())
            os.replace(temporary, catalog_path)
        deleted, errors, cleaned = [], [], []
        for index, edit in enumerate(shared_edits):
            try:
                write_shared_edit(root, edit, projected[edit['path']])
                cleaned.append(edit)
            except (OSError, ValueError) as error:
                # No owned assets are removed until registration cleanup succeeds.
                return {'status': 'partial', 'characterIds': sorted(ids), 'deletedPaths': [],
                        'cleanedSharedRecords': cleaned, 'remainingPaths': [e['path'] for e in shared_edits[index:]] + plan['paths'],
                        'errors': [{'path': edit['path'], 'error': str(error)}],
                        'retiredIds': sorted(ids), 'nextCandidate': catalog['nextCandidate']}
        for relative in plan["paths"]:
            try:
                path = safe_path(root, relative)
                list(files_under(path))  # Recheck links immediately before removal.
                if path.is_dir():
                    shutil.rmtree(path)
                elif path.exists():
                    path.unlink()
                deleted.append(relative)
            except (OSError, ValueError) as error:
                errors.append({"path": relative, "error": str(error)})
        residual = [relative for relative in plan["paths"] if (root / relative).exists()]
        return {"status": "partial" if errors or residual else "deleted", "characterIds": sorted(ids),
                "deletedPaths": deleted, "remainingPaths": residual, "errors": errors,
                "cleanedSharedRecords": cleaned,
                "retiredIds": sorted(ids), "nextCandidate": catalog["nextCandidate"],
                "note": "Only owned repository data was removed. Git history and external copies are unchanged."}
    finally:
        lock.unlink(missing_ok=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[3])
    parser.add_argument("--ids", nargs="+", required=True)
    parser.add_argument("--apply", action="store_true")
    parser.add_argument("--plan-hash")
    args = parser.parse_args()
    try:
        if args.apply and not args.plan_hash:
            parser.error("--apply requires --plan-hash from the preview")
        result = apply_plan(args.root, args.ids, args.plan_hash) if args.apply else build_plan(args.root, args.ids)
        print(json.dumps(result, ensure_ascii=False, indent=2))
        return 0 if result["status"] in {"ready", "already_deleted", "deleted"} else 2
    except (OSError, ValueError, KeyError) as error:
        print(json.dumps({"status": "error", "error": str(error)}, ensure_ascii=False))
        return 1


if __name__ == "__main__":
    sys.exit(main())
