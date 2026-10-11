# LCC-M5-T3 Performance Candidate Evidence - 2026-08-26

## Result

`Candidate Ready for Final Review after recorded repairs`. The bounded M5 contact resolver slice satisfies the frozen M5 contact-chain and steady-state 0 B gate for its 8-owner workload. Independent timers for witness capture and each of the two existing Hurtbox syncs are unavailable through the production diagnostics API; accepting the combined `query/build/pressure` plus `apply contact state` measurement is an explicit evidence-granularity limit, not an unmeasured performance pass.

| Field | Value |
| --- | --- |
| Unity job | `e001f235a1254181ba41ce31cf88b859` |
| Result | 1/1 Passed |
| Roster | 8 animated-source owners + 1 player |
| Sources | 3 per owner, 24 witnesses |
| Directed constraints | 192 |
| Warm / measure | 300 / 1800 frames |
| Measured thread allocation | 0 B |

| Stage | p50 us | p95 us | max us |
| --- | ---: | ---: | ---: |
| query/build/pressure | 503.000 | 638.100 | 1060.600 |
| apply contact state | 7.700 | 11.400 | 122.000 |
| whole measured contact chain | 510.700 | 647.800 | 1080.600 |

The production API does not expose independent witness-capture and each of the two Hurtbox-sync timers. Consequently this result must not be relabeled as separately timed dual-sync/witness stages. T3 accepts that instrumentation granularity limit because the frozen M5 contact-chain workload, capacity and 0 B gate are directly measured here; M4 retains its prior whole double-`SyncAll` chain evidence separately.

## P1 formal RuntimeModule full-chain replacement

The corrected tests-only fixture loads the formal Dragon14 v7 package, asserts exactly three animated sources, spawns 8 Dragon14 plus 1 combat-girl player, and measures the real `RuntimeHost -> CharacterTestCharacterRuntimeModule.Tick` chain. That production Tick contains Motion/Hurtbox `SyncAll` #1, M4 authority pose plus derived-topology Hurtbox `SyncAll` #2, and M5 witness/resolve/apply. It uses `createViews=false` only for performance isolation; the separate PlayMode fixture owns real-view evidence.

Final focused job `58457a8e727b4a8d8acd069dc937108f`:

| Field | Value |
| --- | --- |
| Result | 13/13 aggregate; full-runtime performance test passed |
| Formal roster | 8 Dragon14 + 1 player |
| Sources | 3 per Dragon14 |
| Warm / measure | 300 / 1800 RuntimeModule ticks |
| Final frame | 2101 |
| Measured thread allocation | 0 B |
| Whole RuntimeModule p50 / p95 / max | 2125.7 / 3010.4 / 3964.0 us |

Existing production `ProfilerMarker` seams were recorded without adding a production timing API: `CharacterTest.CharacterRuntime.Motion` covers Motion including first Hurtbox sync, while `CharacterTest.CharacterRuntime.AuthorityPose` covers M4 authority, second derived-topology sync and M5 resolve/apply. Both recorder handles reported `Valid=true`, but `LastValue` remained 0 in this EditMode execution, so no fabricated per-stage time is reported.

The synthetic contact-chain fixture remains narrower substage evidence. In the final aggregate it remained 0 B with p50/p95 `602.6/899.6 us`; a `61.501 ms` maximum outlier was observed and retained rather than hidden. The formal full RuntimeModule run is the acceptance timing authority.
