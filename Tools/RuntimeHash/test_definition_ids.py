import copy
import json
from pathlib import Path
import tempfile
import unittest

import definition_ids as ids


class DefinitionIdentityTests(unittest.TestCase):
    def test_allocate_is_idempotent_and_copy_gets_new_id(self):
        initial = ids.create([11])
        first = ids.apply(initial, "allocate", 11, "binding-a")
        self.assertEqual(first, ids.apply(first, "allocate", 11, "binding-a"))
        copied = ids.apply(first, "copy", 11, "binding-a", "binding-b")
        self.assertEqual([1, 2], [entry["id"] for entry in copied["domains"][0]["entries"]])

    def test_retired_numbers_and_source_keys_are_not_reused(self):
        first = ids.apply(ids.create([11]), "allocate", 11, "a")
        retired = ids.apply(first, "retire", 11, "a")
        with self.assertRaises(ValueError):
            ids.apply(retired, "allocate", 11, "a")
        next_version = ids.apply(retired, "allocate", 11, "b")
        self.assertEqual(2, next_version["domains"][0]["entries"][1]["id"])

    def test_invalid_or_duplicate_declared_ids_are_rejected(self):
        for domains in ([0], [-1], [True], [11, 11]):
            with self.assertRaises(ValueError):
                ids.create(domains)
        document = ids.apply(ids.create([11]), "allocate", 11, "a")
        document["domains"][0]["entries"].append({"id": 1, "sourceKey": "b", "status": "active"})
        document["catalogHash"] = ids.digest(document)
        with self.assertRaises(ValueError):
            ids.validate(document)

    def test_undeclared_domain_and_stale_transaction_do_not_write(self):
        document = ids.create([11])
        with self.assertRaises(ValueError):
            ids.apply(document, "allocate", 12, "a")
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "catalog.json"
            original = json.dumps(document).encode()
            path.write_bytes(original)
            with self.assertRaises(ValueError):
                ids.transact(path, 1, document["catalogHash"], "allocate", 11, "a")
            self.assertEqual(original, path.read_bytes())
            updated = ids.transact(path, 0, document["catalogHash"], "allocate", 11, "a")
            self.assertEqual(updated, json.loads(path.read_bytes()))
            self.assertEqual([path], list(Path(directory).iterdir()))

    def test_existing_lock_prevents_a_second_writer(self):
        document = ids.create([11])
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "catalog.json"
            path.write_text(json.dumps(document))
            lock = path.with_suffix(".json.lock")
            lock.write_text("another writer")
            with self.assertRaises(FileExistsError):
                ids.transact(path, 0, document["catalogHash"], "allocate", 11, "a")
            self.assertEqual("another writer", lock.read_text())


if __name__ == "__main__":
    unittest.main()
