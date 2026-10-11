"""Generate immutable vectors from the pinned xxHash v0.8.3 C reference DLL.

The DLL is a test-only local artifact; no native dependency enters Runtime.
See README.md for the two exported 16-byte wrappers and build commands.
"""
import argparse
import ctypes
import json
import random
import struct
from pathlib import Path

MASK = (1 << 64) - 1
SEED = (1001 << 32) | 1


def fnv(first, second, seed):
    value = 14695981039346656037 ^ seed
    for byte in struct.pack("<QQ", first, second):
        value = ((value ^ byte) * 1099511628211) & MASK
    value ^= value >> 33
    value = value * 0xFF51AFD7ED558CCD & MASK
    value ^= value >> 33
    value = value * 0xC4CEB9FE1A85EC53 & MASK
    return value ^ (value >> 33)


def generate(library):
    reference = ctypes.CDLL(str(library.resolve()))
    reference.refversion.restype = ctypes.c_uint
    if reference.refversion() != 803:
        raise ValueError("Expected xxHash v0.8.3, XXH_versionNumber=803")
    for function in (reference.ref64, reference.ref3):
        function.argtypes = [ctypes.c_void_p, ctypes.c_uint64]
        function.restype = ctypes.c_uint64
    pairs = [(0, 0), (MASK, MASK), (1 << 63, (1 << 63) - 1),
             (0, MASK), (MASK, 0), (1, 1), (1, 2), (2, 1)]
    pairs += [(1 << bit, 0) for bit in range(64)]
    pairs += [(0, 1 << bit) for bit in range(64)]
    random_source = random.Random(1001)
    pairs += [(random_source.getrandbits(64), random_source.getrandbits(64)) for _ in range(128)]
    vectors = []
    for first, second in pairs:
        for seed in (0, SEED, MASK):
            data = struct.pack("<QQ", first, second)
            values = (first, second, seed, reference.ref64(data, seed),
                      reference.ref3(data, seed), fnv(first, second, seed))
            vectors.append([f"{value:016X}" for value in values])
    return {"reference": "xxHash v0.8.3 C implementation, XXH_versionNumber=803",
            "columns": ["first", "second", "seed", "xxh64", "xxh3", "fnv-direct"], "vectors": vectors}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--library", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--check", action="store_true")
    arguments = parser.parse_args()
    data = generate(arguments.library)
    if arguments.check:
        if json.loads(arguments.output.read_text(encoding="utf-8")) != data:
            raise ValueError("Frozen reference vectors differ")
    else:
        arguments.output.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
    print(f"Verified {len(data['vectors'])} reference vectors")


if __name__ == "__main__":
    main()
