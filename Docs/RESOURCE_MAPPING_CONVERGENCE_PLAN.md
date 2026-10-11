# Resource Mapping Convergence Plan

> Status: Design
>
> Date: 2026-07-21
>
> Scope: Resource identity, source location, Global Resource Build Profile compilation, Bundle assignment, external catalog routing, and domain authoring integration.
>
> Implementation state: Accepted direction; not yet implemented.
>
> Current facts: `Docs/RESOURCE_SYSTEM_WORKFLOW.md`, `Docs/Interfaces/Resources.md`, source code and tests remain authoritative until each phase of this plan is implemented.

## 1. Decision

MxFramework is still early enough to optimize for long-term resource-system clarity rather than preserve every current authoring abstraction.

The chosen direction is:

1. Keep the runtime resource core: `ResourceKey`, Catalog, Provider, Handle, reference counting, preload, variant and retain behavior are not being redesigned.
2. Converge the authoring-to-build mapping layer around one runtime identity, one authoring identity and one source locator.
3. Compile mapping and Bundle semantics once in a shared noEngine compiler; Resource Manager preview and the Unity builder must consume the same result.
4. Replace the current Profile shape with one canonical schema. The new compiler does not read old fields, aliases or old Profile versions; current first-party data is regenerated or explicitly rewritten before activation.
5. Remove the first-party proxy-Catalog path in the same controlled breaking cutover: move external entries to internal delivery or directly mounted concrete Catalogs, prove the proxy entry count is zero, then delete the proxy generator, bootstrap registration, Provider and proxy-only fields before the activation batch is pushed.

This is a structural convergence of the middle layer, not a runtime rewrite.

There is no production compatibility reader, versioned migrator or deprecation period. Read-only inventory tooling may inspect the current data to prepare an explicit rewrite worksheet, but it is not part of the new compiler and cannot save or load Player-facing artifacts. Inactive v2 components and shadow output may land on `main` early, but the production entry points remain on v1 until one validated activation commit batch switches all first-party writers, the active Profile, preview, Unity build and Offline bootstrap and deletes the old path. That batch is pushed to `main` once as a unit.

## 2. Why This Work Should Happen Early

The current runtime foundation is reusable, but authoring and build paths already expose signs of identity and policy drift. If those paths continue to grow independently, every new domain editor will add another adapter, fallback rule or mapping name.

Doing the convergence now provides four long-term benefits:

- Resource Manager preview and Unity build output become predictably identical.
- Character, UI, VFX, Audio and future Mod tooling can submit the same mapping input instead of owning separate translation chains.
- Duplicate identity, wrong type, package ambiguity and Bundle conflicts fail during authoring instead of during Player loading.
- Future local, remote, patch and Mod delivery can change Provider or address generation without changing domain-facing resource identity.

The replacement cost is currently concentrated in Authoring Core, Resource Manager and the Unity builder. Once more content and domain schemas depend on the current aliases, the same change would become materially more expensive.

## 3. Current Architecture and Observed Problems

### 3.1 Existing Responsibility Layers

```text
Runtime contract
  ResourceKey -> ResourceCatalogEntry -> ProviderId + Address

Authoring surface
  Resource Manager -> Global Resource Build Profile draft

Build authority
  Global AssetBundle Builder -> runtime catalogs + bundles + reports
```

This three-layer split is correct and remains in place. The problem is that mapping policy is duplicated between the last two layers and identity is inferred differently by different producers.

### 3.2 Baseline Snapshot

The 2026-07-21 repository snapshot contains:

- 71 Global Resource Build Profile entries;
- 16 Bundle rules;
- 5 preload groups;
- 21 internal and 50 external delivery entries.

The same snapshot also shows that all 50 external entries have an empty concrete Catalog id, while all 50 proxy outer keys already equal their target runtime keys. The dual-key deletion therefore has no current `K1 != K2` data conflict, but concrete Catalog ownership remains unresolved for every external row and is an activation blocker.

These counts are audit context, not a permanent acceptance target.

### 3.3 Concrete Gaps

#### A. Type identity is canonicalized too late

Authoring duplicate validation currently compares the raw `type` / `typeId` value, while Unity output later maps aliases such as `texture` to `Texture2D`.

The current Profile contains a concrete example:

```text
mxframework.samples : ui.start_screen.button.normal : Texture2D : internal
mxframework.samples : ui.start_screen.button.normal : texture   : external
```

The authoring layer can treat these as distinct while the emitted runtime identities collide after canonicalization.

#### B. Bundle selection is implemented more than once

`GlobalResourceBundlePlanner` gives an explicit `entry.BundleRule` precedence and returns only that rule. The Unity builder independently iterates all rules and also accepts label, package, domain and explicit-key matches.

This creates a latent state where Resource Manager previews one Bundle but Unity selects more than one. The current Profile does not need to trigger the bug for the duplicated semantics to be unsafe.

#### C. Weak authoring fields can become runtime identity

The Resource Manager frontend currently falls back through runtime/package/provider keys and then through fields such as `stableId`, library item id, display name and resource id when creating a Profile key.

A syntactically valid display label is not authoritative runtime identity. Silent promotion makes renaming and cross-tool reconciliation unsafe.

#### D. One resource can carry too many identity-like names

Current domain and authoring flows may expose combinations of:

- resource id;
- stable id;
- canonical identity;
- technical identity;
- provider resource key;
- runtime resource key;
- package resource key;
- project resource key;
- Unity GUID;
- Unity asset path.

Not all of these mean the same thing, but several are used as fallbacks for one another. That makes ownership difficult to understand and creates accidental compatibility contracts.

#### E. Package semantics are not materialized consistently

Profile entries can inherit `profile.packageId`, but dependency keys are emitted from their own fields. An empty dependency package can therefore mean either “inherit the containing package” or “resolve globally”, depending on the producer and stage.

Runtime package lookup is intentionally strict. Build-time ambiguity must not be passed into it.

#### F. Source origin and runtime delivery authority are conflated

A resource originating in a Character package or import staging area can be marked external even when the global build ultimately owns its Unity asset and Player Bundle. This produces unnecessary runtime-catalog indirection for project-owned content.

“Where the source came from” and “who owns its Player delivery” are separate facts.

#### G. Generated artifacts do not prove which Profile produced them

Generated catalogs and reports can remain locally available after the Profile changes. A timestamp alone is insufficient evidence that Resource Manager preview, the saved Profile and Player artifacts describe the same mapping.

#### H. The two planners do not currently share an assembly boundary

Authoring Core is a standalone `.NET Standard 2.1` library under `Tools/`, while the Unity builder is compiled by an Editor-only Unity assembly under `Assets/`. The Unity builder does not currently reference Authoring Core.

“One compiler” therefore requires an explicit Editor-only binary boundary. Copying source or keeping two implementations with parity tests would not complete the convergence.

## 4. Target Mental Model

Only three concepts may participate in resource identity or source-material location decisions:

| Concept | Meaning | Authority | Must not be used as |
| --- | --- | --- | --- |
| `AuthoringRecordId` | Stable identity of an authoring/library record | Owning authoring domain | Runtime lookup key or Provider address |
| `ResourceKey` | Complete runtime identity: `Id + TypeId + Variant + PackageId` | Runtime contract and compiled build plan | Unity path, GUID or display label |
| `SourceLocator` | Location of source material used during import/build | Source-owning adapter and build resolver | Runtime identity |

`ProviderId` and `Address` remain runtime Catalog output. They are routing data generated by a delivery backend, not authoring identity.

Bundle assignment, preload groups, labels, delivery mode and override intent are policy. They do not create another identity.

### 4.1 Conceptual Mapping Input

The shared compiler consumes one conceptual record per resource:

```text
Resource mapping input
  ProducerReceipt     required ownership/provenance for every persisted row
  AuthoringRecordId   required stable authoring identity for every persisted row
  ResourceKey         required for runtime-loadable resources
  SourceLocator       required when this build owns source material
  DeliveryIntent      internal / external / editor-only / excluded
  OverrideIntent      forbid / allow; runtime entries only
  ExternalCatalogBinding  reference to a declared CatalogMount; external only
  BundleIntent        one explicit Bundle rule
  Labels
  PreloadGroups
  Dependencies        complete ResourceKeys
  ProviderData        provider-specific extension data only
```

This is the required serialized contract for the replacement Profile schema, not an adapter over the current shape. Its first implementation must introduce a versioned DTO that contains only these concepts and the canonical field shapes defined in Section 6. Old Profile versions and unknown legacy fields are rejected by the production compiler.

### 4.2 Source Locator

`SourceLocator` is a discriminated authoring/build concept. Examples include:

- Unity asset: GUID as stable source identity plus path as readable cache/diagnostic data;
- generated Unity asset: generated id plus expected output path;
- FMOD or other domain source: provider-owned stable identifier;
- imported package file: package-relative source path plus content hash.

For Unity assets, GUID is authoritative. Path drift should update diagnostics or canonical output; it must not create a new runtime identity.

An external Catalog id or Provider authority is not a source location. External delivery uses a conditional `ExternalCatalogBinding` that references one Profile-level `CatalogMount`. The Catalog entry itself owns the same canonical `ResourceKey` already present in the mapping record; the binding must not contain an outer-to-target key alias. This is routing policy, not a fourth resource identity.

### 4.3 Profile Row Ownership and Aggregation

The canonical Profile is the only persisted input consumed by the new compiler, but it is not a shared file that every domain writer may rewrite directly. One Authoring-side Profile aggregator is the sole writer of the Profile. Domain adapters submit complete producer snapshots to that aggregator.

Every persisted row carries a `ProducerReceipt` with:

```text
producerId
producerContractVersion
sourceRecordFingerprint
```

Ownership and update rules are mandatory:

- `(producerId, AuthoringRecordId)` uniquely owns one materialized row;
- a producer may replace or delete only rows it owns;
- deletion occurs only when a complete producer snapshot omits a previously owned row; partial updates never imply deletion;
- two producers claiming the same complete `ResourceKey` produce a blocking conflict and never silently overwrite one another;
- Resource Manager manual rows use the reserved producer id `resource-manager.manual` and follow the same ownership rules;
- Profile writes use an optimistic content revision, so an aggregator never overwrites a newer saved Profile;
- domain adapters do not write Profile JSON, Provider addresses or Bundle layout directly.

The aggregator may hold candidate batches in memory, but it does not introduce a second persisted mapping format. The compiler receives only the resulting canonical Profile. This keeps one build authority without turning the Profile into an unsafe multi-writer document.

## 5. Target Mapping Pipeline

```text
Domain authoring records
          |
          v
first-party domain adapters
          |
          v
Profile aggregator
  - enforce producer ownership
  - replace complete producer snapshots
  - reject cross-owner conflicts
          |
          v
canonical Resource Mapping Profile
          |
          v
       GlobalResourceBuildPlanCompiler
       - validate canonical ResourceKey
       - validate identity and source
       - validate explicit package semantics
       - resolve delivery and Bundle policy
       - compile preload/dependency topology
       - emit deterministic diagnostics
                    |
                    v
          GlobalResourceBuildPlan
             /                 \
            /                   \
Resource Manager preview       Unity build adapter
and save diagnostics           - GUID -> path resolution
                               - Unity main-type validation
                               - Provider/address generation
                               - AssetBundle build and artifact write
```

The compiler belongs in the noEngine authoring/build-policy layer and must not reference `UnityEngine` or `UnityEditor`. Its only persisted mapping input is the canonical Resource Mapping Profile. Domain editors and importers submit producer snapshots through the Profile aggregator; they may not bypass the aggregator, write the Profile JSON directly or send an alternate record shape directly to the compiler.

Unity-specific source inspection and `BuildPipeline.BuildAssetBundles` remain in the Unity Editor adapter. The adapter may enrich the plan with resolved paths, hashes and sizes, but it must not recalculate identity, delivery or Bundle membership.

### 5.1 Shared Assembly Boundary

The external Authoring service and Unity Editor must execute the same compiled compiler implementation.

The preferred first route is to expose the relevant Authoring Core contracts/compiler to an Editor-only Unity assembly through the repository's existing `asmdef` / `asmref` or built-DLL bridge pattern. If Authoring Core's unrelated dependencies make that impractical, extract only the BCL-only Profile input, canonicalization, diagnostics and Build Plan compiler into a small `.NET Standard 2.1` subassembly referenced by both sides.

Whichever packaging route is selected, these rules are mandatory:

- the shared assembly is Editor/authoring-only and is not added to the Player runtime dependency graph;
- there is one compiler source and one compiler contract version;
- Unity and the Authoring service may have separate JSON/source adapters, but may not reimplement mapping policy;
- the Unity Build button must not depend on spawning a CLI process as its semantic authority;
- the build report records the compiler contract version actually loaded by Unity.

## 6. Canonical Rules

### 6.1 ResourceKey

Before duplicate checks, matching, dependency compilation or output generation:

1. require fields to already satisfy the Resource contract, including no leading/trailing whitespace;
2. validate built-in `TypeId` against the shared canonical table and preserve extension TypeIds exactly;
3. require `PackageId` to be explicit: an empty value means the global route and is never inherited from the containing Profile;
4. preserve `Variant` exactly;
5. compare the complete four-part key using ordinal semantics.

The serialized Profile accepts canonical `ResourceTypeIds` values only. An authoring UI may suggest conversions such as `texture -> Texture2D`, but it must normalize and validate before saving; the compiler rejects persisted aliases and the removed `type` field. Valid provider/domain extension TypeIds such as `MxFairyGuiPackageBytes` remain exact ordinal strings; they are validated by their owning adapter and are not coerced to `Object`.

`source.providerData.assetType` is a source-type observation. It may validate whether a Unity/import source is compatible with `ResourceKey.TypeId`, but it must not override runtime identity. When both values map to known built-in types they must canonicalize to the same TypeId; extension compatibility is decided explicitly by the owning adapter.

### 6.2 Runtime key creation

Resource Manager may create a runtime key only from:

1. an authoritative `ResourceKey` supplied by the provider/domain;
2. an explicit user-entered key that passes validation;
3. a deterministic domain key generator registered for that resource kind.

`displayName`, file name, `resourceId`, library row id and `AuthoringRecordId` may be shown as a suggested candidate, but must never be silently saved as the runtime key.

### 6.3 Package handling

- Every entry and dependency key writes its package route explicitly. A same-package key repeats that package id; an empty package id intentionally means the runtime global route.
- Profile-level package metadata does not fill or modify `ResourceKey.PackageId`.
- Canonical identity, source binding and package-route uniqueness use the complete `ResourceKey`.
- The compiler must additionally validate the current package-less global route used by `ResourceManager`: Catalog-local logical-key duplicates, global override conflicts and per-mount external allowlists are checked with the same `WithoutPackage()` semantics that runtime currently uses.
- Changing the package-less global route or making external allowlist comparison package-aware is a separate Runtime contract decision. This mapping convergence must not make compile-time acceptance looser than runtime `AddCatalog` behavior.

### 6.4 Bundle authority

For every internal runtime-loadable direct entry, `BundleIntent` contains exactly one required `ruleId` naming an existing Bundle rule. The referenced rule owns the final `bundleName`, compression, build-target intent and dependency policy.

Labels, domains, package ids and group hints are search or UI recommendation data only and never select the final Bundle. Missing, unknown or multiply specified Bundle intent is a blocking compile error. One direct entry has exactly one declared owner Bundle. Backend dependency packing may add dependency bundles without changing that ownership.

There is no `standalone`, `forceBundle` or generated one-off Bundle mode in the replacement schema. A resource that needs its own Bundle uses an ordinary explicit Bundle rule containing that one entry. This removes the final alternate Bundle-naming algorithm from Authoring and Unity.

### 6.5 External delivery

`DeliveryIntent = external` means another runtime Catalog owns the concrete runtime entry and its Provider routing.

It must not mean only that the source originated outside `Assets/` or came from a Character package. Once an imported Unity asset is accepted into the global Player build, it is eligible for internal delivery regardless of its origin.

Canonical external output contains one complete `ResourceKey` plus an `ExternalCatalogBinding.catalogMountId`. At runtime, Offline bootstrap resolves the declared mount, loads and validates that concrete Catalog, selects the exact allowlisted keys compiled for that mount and registers those concrete entries directly. Each selected Catalog entry resolves the same key to its real Provider and Address.

The Profile declares `CatalogMounts[]`. Each mount contains:

```text
catalogMountId              stable Profile-local id
catalogId                   expected concrete runtime Catalog id
bootstrapKind               streamingAssetsCatalog / compositionRoot
relativeCatalogPath         required only for streamingAssetsCatalog
mountOrder                  deterministic AddCatalog order
expectedCatalogFingerprint  required canonical Catalog content hash
```

All declared mounts are required during initial startup; optional, lazy and downloaded mounts remain outside this replacement. `catalogMountId`, `catalogId` and `mountOrder` are unique. Mounts are applied after the internally built Catalog in ascending `mountOrder`; ties are forbidden. A `streamingAssetsCatalog` path is a normalized relative path beneath `StreamingAssets`, may not escape that root and is forbidden for `compositionRoot`. `relativeCatalogPath` locates a Catalog artifact, not resource source material. `compositionRoot` means the application must supply the named Catalog and its Provider registrations through the bootstrap options. For `streamingAssetsCatalog`, bootstrap provider factories are selected from the concrete Catalog entries' `ProviderId` values; an unknown or unavailable Provider id is a blocking bootstrap error.

The Build Plan emits one allowlist of complete external `ResourceKey` values per mount. The Unity adapter validates that every required key exists exactly once in the resolved Catalog, with matching type/package/variant, and records the resolved Catalog fingerprint. Bootstrap repeats the identity check before `AddCatalog`. It may create a filtered view containing the concrete entries, but it must not create proxy entries, rewrite keys or introduce a nested `ResourceManager`.

The following rules are blocking:

- project-owned imported Unity assets use internal delivery instead of a proxy Catalog;
- genuinely external resources are owned by directly mounted concrete Catalogs;
- an outer key `K1` redirecting to a target key `K2` is not a supported canonical shape; any existing `K1 != K2` row is a cutover conflict requiring an explicit identity decision;
- first-party builds must not generate `runtimeCatalog` proxy entries after the cutover;
- `global_external_runtime_catalog.json`, `RuntimeCatalogReferenceProvider`, its Offline-bootstrap registration and the proxy-only `runtimeResourceKey`, `runtimeResourceType` and `runtimePackageId` fields are deleted as soon as the zero-proxy report passes.

Source origin stays in `SourceLocator`; external Catalog ownership stays in `ExternalCatalogBinding` / `CatalogMount`; neither may be inferred from the other. A future lazy, downloaded or hot-mounted Catalog mechanism, if needed, belongs at application/bootstrap loading rather than behind a resource Provider and is outside this replacement.

### 6.6 Delivery contract

The replacement schema stores one `DeliveryIntent` enum: `internal`, `external`, `editorOnly` or `excluded`. It does not serialize `runtimeLoadable`, `editorOnly` booleans, `deliveryMode`, `bundleOverrideMode`, `forceExternal`, `exclude`, `forceBundle` or `forceStandalone`.

The compiler validates the complete record against this matrix:

| `DeliveryIntent` | `ResourceKey` | `AuthoringRecordId` | `SourceLocator` | `OverrideIntent` | `ExternalCatalogBinding` | `BundleIntent` | dependencies / preload |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `internal` | required | required | required | required | forbidden | required | allowed |
| `external` | required | required | optional diagnostic evidence only | required | required | forbidden | allowed |
| `editorOnly` | forbidden | required | required | forbidden | forbidden | forbidden | forbidden |
| `excluded` | forbidden | required | optional | forbidden | forbidden | forbidden | forbidden |

Additional constraints:

- `ExternalCatalogBinding` contains only `catalogMountId`. `CatalogMount` owns Catalog artifact loading authority and expected `catalogId`; neither contains an alternate ResourceKey, Provider address or resource source location.
- every external row references exactly one declared mount; one mount may own many external rows;
- runtime rows require one explicit `OverrideIntent`; non-runtime rows forbid it. `forbid` emits `allowOverride = false`, while `allow` emits or requires `allowOverride = true` on the concrete Catalog entry;
- mount order is deterministic and compiled key conflicts obey the same override, type and `WithoutPackage()` checks as `ResourceManager.AddCatalog`; an overlap is accepted only when the later row has `OverrideIntent = allow` and the type matches;
- `ProviderId` and `Address` are emitted by the owning concrete Catalog/backend and are never accepted as top-level mapping identity.
- `ProviderData` may carry adapter/backend extension data, but it may not contain an alternate ResourceKey, delivery override, Bundle override, ProviderId or Address.
- A field marked forbidden is a schema/compile error rather than ignored data.
- Runtime entries (`internal` and `external`) always have one complete canonical `ResourceKey`; non-runtime entries use `AuthoringRecordId` and cannot reserve a runtime identity.

### 6.7 Mapping and artifact provenance

The compiler and build report expose a deterministic provenance tuple:

```text
mappingInputFingerprint
compilerContractVersion
buildTarget
buildPlanFingerprint
```

`mappingInputFingerprint` hashes the complete normalized compiler input, including the canonical Profile schema version, every entry, Bundle rule, Catalog mount, preload group and producer receipt embedded in the Profile. Each first-party adapter records its adapter id, adapter contract version and source-record fingerprint when materializing Profile rows. Timestamps and machine-local paths are excluded. Matching this tuple proves **mapping freshness** only: the current domain materialization, saved Profile and compiler still produce the reported plan.

“Artifact fresh” requires additional Unity-side evidence, such as a deterministic `sourceInputFingerprint` derived from resolved GUID/content/dependency inputs plus hashes of emitted bundles/catalogs. Resource Manager must show mapping freshness and artifact freshness separately; it must not infer source-content freshness from the Profile hash alone.

## 7. Shared Build Plan Contract

The Build Plan must be detailed enough that consumers do not repeat policy decisions.

Each compiled entry should expose at least:

- canonical `ResourceKey`;
- source locator summary;
- delivery classification;
- explicit override intent and effective mount order;
- final Bundle owner or concrete `ExternalCatalogBinding`;
- preload groups and labels;
- canonical dependency keys;
- blocking errors and non-blocking warnings;
- traceable source path back to the Profile/domain record.

The overall plan should expose:

- deterministic ordered entries and Bundles;
- deterministic Catalog mounts, per-mount external allowlists and expected Catalog fingerprints;
- mapping-input and plan fingerprints;
- compiler contract version;
- build target intent;
- counts by delivery mode;
- identity, type, Bundle, dependency and external-routing diagnostics;
- a stable comparison surface for review and cutover reports.

The compiler is a low-frequency authoring/build path. Clear deterministic output is more important than zero allocation here; it must still remain pure, bounded by input size and free of hidden filesystem or Unity state.

Canonical plan serialization uses UTF-8 without BOM, LF line endings and invariant formatting. Entries are ordered by complete canonical `ResourceKey`; Bundles by bundle id; Catalog mounts by `mountOrder` then `catalogMountId`; producer receipts by `producerId` then `AuthoringRecordId`; labels, preload groups and dependency keys are deduplicated and ordinal-sorted; extension dictionaries are serialized by ordinal key; diagnostics are ordered by severity, code, ResourceKey and source record. Timestamps and machine-local paths are excluded. `buildPlanFingerprint` is the hash of this canonical serialization, so byte stability has one defined meaning across Authoring and Unity.

## 8. Breaking Replacement Policy

The replacement Profile uses `schema = mx.resource-mapping-profile.v2` and is the only input accepted by `GlobalResourceBuildPlanCompiler`. The active system has no compatibility mode.

Rules:

1. Old Profile versions, removed fields and persisted aliases fail schema validation before semantic compilation.
2. A separate read-only inventory command may parse current files into a human-reviewable worksheet. It is not linked into the production compiler, cannot write canonical data and is deleted after cutover evidence is accepted.
3. Current data is regenerated by updated first-party producers or explicitly rewritten from reviewed decisions. No automatic normalizer guesses identity, delivery, package or Bundle ownership.
4. The Profile aggregator is the only v2 Profile writer. All first-party adapters must successfully materialize replacement producer snapshots through the inactive aggregator before v2 becomes active.
5. The active Profile, generated Catalogs, Bundle report and Offline bootstrap are switched together. Partial output is never treated as current.
6. The activation commit batch deletes the old Profile writers/DTO/readers, duplicated planners, proxy Catalog generator, proxy Provider, bootstrap registration and obsolete tests before the batch is pushed.
7. A repository scan proving zero active legacy readers/writers and zero direct v2 Profile JSON writers outside the aggregator is an activation gate.

The read-only inventory worksheet must force an explicit decision for every current ambiguity:

| Current data shape | Required replacement decision |
| --- | --- |
| `resourceKey.type`, aliased TypeId or conflicting type evidence | write one canonical `ResourceKey.TypeId` |
| weak display/stable-id fallback | enter or generate one authoritative `ResourceKey` |
| Package/Project/Provider/Runtime key variants | select one complete `ResourceKey`; no alias chain |
| proxy outer key and target key | select one identity; `K1 != K2` blocks cutover |
| old delivery booleans/overrides | select one `DeliveryIntent` satisfying Section 6.6 |
| implicit or conflicting `allowOverride` behavior | write one explicit `OverrideIntent` and verify mount order/type compatibility |
| source origin used as delivery authority | choose project-owned internal or concrete external Catalog ownership |
| external row with missing or implicit Catalog authority | assign one declared `CatalogMount`; missing ownership blocks cutover |
| Bundle auto-match or multiple matching rules | write one canonical `BundleIntent` |

Git history and the checked-in cutover worksheet are the recovery record. Runtime/editor code is not required to reopen old Profile files after activation.

## 9. Phased Delivery

The new path is developed on `main` behind inactive Editor/authoring entry points. During Phase 0 through Phase 2, v1 remains the only production Profile path while the v2 aggregator writes an explicitly shadow-only Profile and the v2 compiler produces shadow plans. No production reader accepts both schemas, and shadow artifacts are never Player-facing authority.

The final activation is one validated breaking commit batch. Consecutive local commits may keep the change reviewable, but they are pushed to `origin/main` once, only after the entire batch passes the Phase 3 gates. If `origin/main` advances, the whole batch is rebased and revalidated before push; no partial activation commit is published as a supported state.

### Phase 0: Replacement contract and inventory

Goal: make every required identity and ownership decision explicit before implementation.

Deliverables:

- the replacement Profile DTO and JSON schema;
- `ProducerReceipt`, Profile aggregator ownership/update rules and optimistic Profile revision contract;
- `CatalogMounts[]`, external per-mount allowlist and bootstrap validation contract;
- canonical fixtures covering internal, external, editor-only, excluded, scene, preload and packaged records;
- canonical serialization and fingerprint fixtures;
- a disposable read-only inventory command;
- a checked-in cutover worksheet listing every current key, source, delivery owner and Bundle owner;
- explicit decisions for the current TypeId collision, Bundle-rule divergence and every proxy `K1/K2` pair.

Exit criteria:

- the replacement schema represents every accepted first-party use case;
- the canonical fixtures compile without reading current Profile data;
- every current ambiguity is resolved or blocks further cutover work;
- no automatic conversion rule is part of the production contract.

### Phase 1: One canonical Build Plan compiler

Goal: implement the replacement contract once without activating it for the current Profile.

Deliverables:

- an Editor-only shared-assembly bridge for the BCL-only DTO, canonicalizer, diagnostics and compiler;
- `GlobalResourceBuildPlanCompiler` accepting only the replacement schema;
- complete-key, package-route, dependency and duplicate validation;
- the Delivery matrix, SourceLocator validation, Catalog mount/allowlist validation and explicit Bundle-rule authority from Section 6;
- deterministic canonical plan serialization and fingerprints;
- Resource Manager preview and Unity build adapters consuming the same compiled plan through an inactive development entry point;
- Unity-only source resolution and artifact emission isolated after compilation.

Exit criteria:

- Authoring and Unity execute the same compiler implementation;
- no second implementation decides key, delivery, Bundle ownership or dependency package semantics;
- preview membership equals Unity build input membership;
- repeated compilation produces byte-identical canonical plan output;
- the new compiler rejects old schemas and removed fields.

### Phase 2: First-party producer cutover

Goal: make every first-party adapter materialize complete replacement snapshots through the inactive v2 aggregator before activation, without replacing the live v1 path early.

Recommended order:

1. Character package/compiler/import path;
2. UI and FairyGUI resource selections;
3. VFX Published Effect/catalog path;
4. Audio resources owned by generic Resources;
5. remaining Demo and sample importers;
6. Resource Manager manual create/edit/save flows.

Deliverables:

- domain adapters materialize complete canonical rows, including `ProducerReceipt`, `AuthoringRecordId`, `ResourceKey`, `SourceLocator`, `DeliveryIntent`, `OverrideIntent` and the conditional Bundle/external binding policy;
- each adapter writes its adapter id, contract version and source-record fingerprint into the Profile producer receipt;
- the aggregator enforces replace-by-owner snapshots, scoped stale-row deletion, cross-owner conflict rejection and optimistic Profile revision;
- redundant Project/Provider/Package/Runtime key aliases are removed from first-party Profile output;
- no domain editor decides Provider address or final Bundle layout;
- the current repository content is regenerated as one shadow-only replacement Profile on `main` while v1 remains the production input;
- per-domain round-trip and schema-rejection tests.

Exit criteria:

- every first-party v2 adapter emits only the replacement schema through the aggregator;
- repository scans show zero removed fields or proxy key fields in v2 output and identify the exact remaining v1 readers/writers that the activation batch will delete;
- the regenerated shadow Profile passes the canonical compiler with no blocking diagnostics;
- all inventory worksheet decisions are reflected in the regenerated Profile.

### Phase 3: Atomic activation and old-path deletion

Goal: activate the replacement system and remove the old and proxy paths without leaving a supported dual state.

The activation batch contains consecutive reviewable local commits but is pushed only as a complete unit:

1. build preview, Bundle membership, concrete Catalogs and reports from the regenerated Profile;
2. change Offline bootstrap to resolve declared Catalog mounts, validate per-mount allowlists and register concrete entries that own the same requested `ResourceKey`;
3. switch Resource Manager save/build and Unity build entry points to the new compiler and Profile;
4. stop generating and consuming `global_external_runtime_catalog.json`;
5. delete `RuntimeCatalogReferenceProvider`, proxy bootstrap registration and proxy-only fields;
6. delete the old Profile DTO/readers, old semantic planners, fallback writers, fixtures and tests;
7. delete the disposable inventory command after its accepted worksheet is committed;
8. update current-state resource documentation.

Exit criteria:

- only `mx.resource-mapping-profile.v2` is accepted;
- no first-party artifact contains a `runtimeCatalog` proxy entry or alternate target ResourceKey;
- every external entry references one resolved required Catalog mount, and every allowlisted key exists exactly once in its concrete Catalog;
- Offline loads internal, packaged, directly mounted external and scene resources;
- mapping, source-input and emitted-artifact fingerprints all match;
- repository scans show zero old Profile readers/writers, duplicate planning paths, proxy implementation or proxy-only fields;
- package-scoped and package-less global lookup semantics remain unchanged.

The activation and deletion form one atomic supported source state. Intermediate activation states are not supported releases.

## 10. Impact Assessment

| Area | Expected impact | Reason |
| --- | --- | --- |
| `MxFramework.Resources` runtime core | Low | contract remains stable; focused regression only |
| `MxFramework.Resources.Unity` runtime providers | Medium to high in Phase 3 | direct Catalog mounting replaces and deletes the proxy Provider/bootstrap path |
| Authoring Core resource models/validation | High but contained | canonicalization and shared compiler live here |
| Resource Manager web/profile API | Medium | key creation, diagnostics and plan rendering change |
| Unity Global AssetBundle Builder | Medium | semantic planning removed; Unity resolution/emission retained |
| Character authoring/compiler/import | Medium in Phase 2 | redundant key aliases and source mapping converge |
| Existing Profile data | High in Phase 2/3 | the active Profile is regenerated in the replacement schema; unresolved identity or ownership decisions block activation |
| Domain runtime configs | Low through Phase 2 | continue consuming `ResourceKey` |

Phase 1 and Phase 2 are cross-module authoring/build changes. Phase 3 has the highest technical impact because it activates the new Profile schema and removes runtime/bootstrap and build-artifact paths.

## 11. Validation Strategy

### 11.1 Pure compiler tests

Cover at minimum:

- rejection of persisted TypeId aliases, removed `type` fields and unknown Profile schemas;
- canonical built-in TypeIds without rejecting exact extension TypeIds;
- complete key comparison across package and variant;
- explicit same-package versus global dependency handling;
- package-route uniqueness and package-less global override conflicts;
- explicit `OverrideIntent`, deterministic internal-first/mount-order application and concrete `allowOverride` parity;
- deterministic Catalog mount order, per-mount external allowlists and `WithoutPackage()` conflict parity with the current global route;
- missing/duplicate Catalog mount ids or orders, invalid/escaping mount paths, invalid mount field combinations and unresolved mounts;
- concrete Catalog id/fingerprint mismatch, missing allowlisted keys and unavailable Provider bootstrap factories;
- source `assetType` mismatch without source metadata overriding key identity;
- all required and forbidden combinations in the Delivery matrix;
- one required `BundleIntent.ruleId`, including the ordinary one-entry Bundle-rule case;
- rejection of missing/unknown Bundle rules and removed standalone/force Bundle fields;
- labels/domain/package recommendations having no Bundle authority;
- internal versus external authority;
- rejection and reporting of proxy `K1 != K2` aliases;
- external entries retaining one canonical `ResourceKey` while binding a concrete Catalog authority;
- zero first-party `runtimeCatalog` proxy output after cutover;
- deterministic canonical serialization and fingerprints;
- producer ownership, complete-snapshot replacement, scoped stale-row deletion, cross-owner conflict and optimistic revision behavior;
- producer-receipt changes invalidating `mappingInputFingerprint`;
- old Profile versions and removed fields failing before semantic compilation.

### 11.2 Existing focused suites

Extend or reuse:

- `GlobalResourceBuildProfileTests`;
- `GlobalPlayerResourceBuildProfileBuilderTests`;
- `ResourceManagerTests`;
- `GeneratedResourceBuildArtifactLoaderTests`;
- Resource Manager `scripts/smoke.mjs`.

### 11.3 Unity acceptance

For phases touching the Unity builder:

1. refresh/compile with no Console error;
2. run focused EditMode resource builder tests;
3. build the replacement Global Profile through the Workbench;
4. confirm mapping-input and plan fingerprints match the saved Profile, Catalog mounts and producer receipts;
5. compare preview and built Bundle membership;
6. start Offline mode and load at least one internal, packaged, directly mounted concrete-external and scene key where fixtures exist;
7. release handles/groups and verify expected resource diagnostics.

For Phase 3, acceptance additionally proves that every required mount resolves in deterministic order, the same external `ResourceKey` resolves through an allowlisted concrete Catalog entry, no generated artifact contains a proxy entry, and the removed Provider/bootstrap types and proxy-only fields have zero repository usage.

### 11.4 Cutover acceptance

Before the replacement Profile becomes active, accept a reviewable worksheet containing:

- keys added, removed or retyped;
- package changes;
- Bundle owner changes;
- delivery/provider/address changes;
- external Catalog mount ownership, artifact path, mount order and expected Catalog fingerprint;
- preload/dependency changes;
- removed legacy fields and writers found by repository scan;
- proxy entries and proxy implementation references remaining;
- unresolved conflicts.

Activation requires zero unresolved conflicts, zero legacy Profile writers and zero proxy entries. The worksheet records explicit human decisions; it is not evidence that old data remains loadable.

## 12. Explicit Non-Goals

This plan does not authorize:

- rewriting `ResourceManager`, Handle/ref-count, retain or preload behavior;
- replacing AssetBundle with Addressables or YooAsset;
- adding CDN publishing, signing, encryption or delta patching;
- merging all external catalogs into the global catalog;
- designing lazy/downloaded Catalog discovery or hot-mount behavior beyond the declared startup `CatalogMounts[]` contract;
- changing `ResourceManager` package-scoped lookup or package-less global override semantics; replacing proxy filtering with the explicit per-mount allowlist defined here is in scope, but changing its `WithoutPackage()` comparison semantics is not;
- changing player-facing domain configs away from `ResourceKey`;
- introducing a new top-level resource editor;
- changing FairyGUI, FMOD or VFX ownership rules outside their resource adapter boundary.

## 13. Implementation Slices

| Slice | Scope | Technical impact |
| --- | --- | --- |
| RM-C1 | Replacement DTO/JSON schema, ProducerReceipt/Profile aggregator contract, CatalogMount contract, canonical fixtures and read-only inventory worksheet | Contained schema and fixture change |
| RM-C2 | Shared `GlobalResourceBuildPlanCompiler`, canonical serialization and validation | Cross-module compiler change |
| RM-C3 | Inactive Resource Manager/Unity adapters, parity and provenance | Inactive runtime-adapter integration |
| RM-C4 | Character first-party Profile producer cutover | Producer ownership cutover |
| RM-C5 | UI, VFX, Audio, Demo and manual Resource Manager writer cutover | Multi-domain producer cutover |
| RM-C6 | Atomic Profile activation, direct Catalog mounting, old planner/schema/proxy deletion and Offline acceptance | Atomic production activation |

RM-C1 through RM-C5 may add inactive replacement components and shadow producer adapters, but they do not make the production compiler accept both schemas or make shadow artifacts Player authority. RM-C6 is one atomic activation state; versioned source does not expose a compatibility period.

## 14. Final Success Condition

The convergence is complete when a developer can answer these questions without reading provider-specific fallback code:

1. What is the runtime identity? `ResourceKey`.
2. Where did the source come from? `SourceLocator`.
3. Who owns its Player delivery? The compiled delivery decision.
4. Which Bundle owns it? The explicit compiled Bundle assignment.
5. Why did Resource Manager and Unity choose that result? The same Build Plan entry and diagnostics.
6. How is an external resource resolved? A concrete mounted Catalog owns the same `ResourceKey`; no proxy key or proxy Provider is involved.
7. Can an old Profile still load? No; only the replacement schema is accepted after activation.
8. Who may update or delete this Profile row? Exactly one `(producerId, AuthoringRecordId)` owner through the Profile aggregator.

At that point, adding a new domain resource or delivery Provider extends an adapter/backend instead of adding another identity system.
