# ADR-007: Physical Interaction Driven Audio Expression Pipeline

> Status: ADR

Date: 2026-07-14

Decision Status: Accepted

## Context

The current audio runtime can map a `CombatPhysicalContactEvent` to a one-shot and can modulate a small parameter set, while CharacterTest weapon-hit audio has a separate rule path. Audio Workbench can generate, edit, version, apply, audition, and publish source sets. These pieces do not yet share one runtime model for both interacting bodies, air displacement, deterministic Basis selection, or authoring coverage diagnostics.

Treating each WAV or adoption set as a gameplay identity would leak asset-production details into runtime code. Letting FMOD choose between unrelated material pairs would hide gameplay-semantic selection from tests and diagnostics. Expanding Combat material enums to satisfy audio authoring would also reverse the intended module boundary.

## Decision

- A `Basis Sound` is the stable runtime identity. It references one gameplay Cue and one complete FMOD Event; adoption sets are immutable material history under that Basis, not new runtime identities.
- A canonical unordered `Material Pair` is the project-global identity for two acoustic materials. `Metal × Wood` and `Wood × Metal` share the same Pair, while the directed impactor/receiver fact remains available for response calculations.
- `Impact / Step / Landing / Hit / Block / Sweep` are Pair Variants. A Family maps gameplay interaction semantics to one Variant and response behavior; it does not own a private material matrix. A Basis is a playable implementation of one Pair Variant.
- `MxFramework.Audio.Physical` owns a noEngine `PhysicalAudioInteractionFact`, acoustic material profiles, Family/Basis definitions, deterministic selection, fallback, response curves, and low-frequency resolve traces.
- Gameplay and Combat own interaction facts only. A project composition-root provider maps weapons, body parts, equipment, colliders, terrain, and other project data to independent acoustic properties.
- `CombatSurfaceMaterialKind` is not expanded into the acoustic material catalog. Mapping between them is explicit and replaceable.
- Air displacement is an explicit interaction kind with an Air/Gas receiver profile. It is not represented as a fake Combat collision.
- C# deterministically selects one complete Basis Event and calculates bounded playback parameters. FMOD owns tracks, instruments, spatialization, DSP automation, and take-level presentation variation inside that Event.
- A one-shot interaction starts one FMOD Event. Phase 17 does not compose unrelated Events at runtime.
- Basis selection, material fallback, and calculated parameters are reproducible for the same fact and catalog. Pair fallback ties resolve by total depth, maximum single-side depth, then stable Pair ID; Basis ties resolve by priority, match distance, then stable Basis ID. FMOD take variation remains presentation-only and is excluded from Combat authority and runtime/replay hashes.
- Physical authoring uses an independent project manifest and publishes an independent physical runtime catalog linked to `mx.audioRuntimeCatalog.v1` by hash. Prompt, Recipe, WAV path, candidate metrics, and provenance remain Authoring-only.
- JSON parsing, name resolution, dictionary construction, and catalog joining occur only at initialization. Resolver and bridge hot paths use precompiled numeric indexes and caller-owned buffers and are `NoAlloc after initialization`.
- Browser mix preview is approximate. Approval evidence can only come from Unity invoking the same resolver and playing the resulting real FMOD Event.
- Missing or corrupt generated physical catalogs switch the entire CharacterTest path to the legacy fallback with a warning. Generated and legacy rules are never merged.
- Phase 17 implements one-shot Footstep, Landing, Object Impact, Weapon Hit/Block, and Air Displacement. Started/Stayed/Ended continuous interactions are deferred to Phase 18 without changing these identities or catalogs.

## Consequences

- Adding a new material pair or replacing its sources no longer requires Gameplay C# changes.
- Selection is testable and can explain why a Basis won or why fallback was used.
- FMOD remains the authority for sound construction while C# remains the authority for game-fact interpretation.
- Authoring must expose a global upper-triangle Pair matrix, Required Pair×Variant coverage and response-curve previews; publish validates three manifests plus FMOD/Bank/Snapshot/approval evidence.
- Project composition roots must supply acoustic properties that are not present in generic Combat facts, especially source material, mass, body-part construction, and weapon motion dimensions.
- The first implementation carries a migration fallback until CharacterTest mappings are fully represented in the physical catalog.

## Alternatives Considered

- **Select raw WAV/take IDs in C#.** Rejected because it leaks FMOD authoring structure into gameplay/runtime contracts and makes layered Events harder to evolve.
- **Let one large FMOD Event select all material pairs.** Rejected because semantic selection, fallback, coverage, and replay diagnostics would be hidden in FMOD conditions.
- **Expand `CombatSurfaceMaterialKind` into the authoring material library.** Rejected because audio detail would enlarge Combat authority and couple generic collision rules to presentation needs.
- **Start multiple Events for transient/body/tail.** Rejected for Phase 17 because it increases voice/lifecycle complexity and duplicates work already handled well by one FMOD Event.
- **Include FMOD take randomness in runtime determinism.** Rejected because audio is presentation-only; deterministic Basis selection is sufficient for diagnosis and reproducible intent.

## References

- Design: `Docs/AUDIO_PHYSICAL_EXPRESSION_DESIGN.md`
- Current contracts: `Docs/Interfaces/Audio.md`, `Docs/Interfaces/Combat.md`
- FMOD boundary: `Docs/AUDIO_SYSTEM_FMOD.md`
- Authoring workflow: `Docs/AUDIO_WORKBENCH_GUIDE.md`
- Delivery phases: `Docs/ROADMAP.md` Phase 17 and Phase 18
