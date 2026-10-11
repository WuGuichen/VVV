# Fixed 16-byte state hash verification

Reference: xxHash **v0.8.3**, https://github.com/Cyan4973/xxHash/tree/v0.8.3.
The C reference, object files, binaries and reports belong in `Library/FixedHashOptimization/`, not the package.
Keep `xxHash-LICENSE.txt` with derived comparison code and artifacts.

Download `xxhash.h` and `LICENSE` from that tag. Compile this test-only wrapper with an x64 C compiler:

```c
#define XXH_STATIC_LINKING_ONLY
#define XXH_IMPLEMENTATION
#include "xxhash.h"
__declspec(dllexport) unsigned long long ref64(const void* data, unsigned long long seed)
{ return XXH64(data, 16, seed); }
__declspec(dllexport) unsigned long long ref3(const void* data, unsigned long long seed)
{ return XXH3_64bits_withSeed(data, 16, seed); }
__declspec(dllexport) unsigned refversion(void) { return XXH_versionNumber(); }
```

In a Visual C++ x64 environment: `cl /nologo /O2 /LD /Fe:reference.dll reference.c`.
Then regenerate or check the committed fixture:

```text
python Tools/FixedHashBenchmark/generate_vectors.py --library Library/FixedHashOptimization/reference.dll --output Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Runtime/FixedHashReferenceVectors.json --check
```

The generator rejects any reference library version other than 803. Fixtures contain full 64-bit hex strings, avoiding JSON consumer precision loss. FNV vectors use an independent Python byte implementation with the specified seed and finalizer.

Unity diagnostic sources are guarded by `UNITY_EDITOR || CHARACTER_TEST_HASH_BENCHMARK`. They do not run at Editor startup and are excluded from ordinary Players. `FixedHashBenchmark.CaptureSession("before")` prepares the real-session corpus; `Run(label, verifyOldProduction)` runs direct compiled batches. `FixedHashBenchmarkBuild.Build("before"|"after")` creates a temporary additive scene, builds Windows x64 Mono using an extra per-build define, and removes its own temporary scene. Run the executable with the repository as working directory and `--fixed-hash-label=player-before` or `player-after`.

Builds and reports are diagnostics, not a playable or release acceptance claim.

The selected production kernel is XXH64, algorithm ID 2. Its frozen layout is schema 1001,
version 1, seed `0x000003E900000001`. Only the selected kernel is in the Runtime package.
The historical entry and the three comparison implementations are compiled only in the
Editor or the explicitly defined benchmark Player; they are not runtime fallback paths.

Player outputs are under `.agent/FixedHashOptimization/player-before`, `player-after` and
`player-after-allocations`; Unity rejects Player output paths inside `Library`.
`FixedHashBenchmarkBuild.RunBefore` and `RunAfter` are batch Editor entry points.
RunBefore requires a checkout with the original production combination; it verifies all
65,536 production results against the captured historical implementation before measuring.
RunAfter also builds a separate Development Player for allocation instrumentation. Timings
from that allocation build are excluded from algorithm selection and release Player results.
If using an isolated project copy, include the repository's `FMOD` authoring directory as
well as Assets, Packages, ProjectSettings and Tools, since the existing FMOD build callback
validates its project path.

The session fixture configures a real server/client authority handshake with one legal
empty deck lock, records replay, and verifies every captured completed-frame result through
the production combination. Each of the three rounds warms 300 frames and measures 600.
The after run also compares all 1,800 upstream gameplay/authority pairs with the before
corpus, rejecting an upstream state change. Corpus reads and these checks are outside the
simulation timing interval.

Allocation counters must pass an 8 KiB positive control. `-1` means unavailable, never zero.
Unity 6000.5 Mono's current-thread byte counter returns zero even for the positive control;
the tested Editor and Development Player instead verify zero `GC.Alloc` events. The
non-Development Player has no working allocation recorder, so its allocation result remains
unavailable while its timings are used for the production performance comparison.

See `Docs/Tasks/RUNTIME_FIXED_LAYOUT_SESSION_HASH.md` for measured results and known baseline failures.
