# Resource Mapping Convergence Implementation Ledger

> Status: Current
>
> Started: 2026-07-21
>
> Design authority: `Docs/RESOURCE_MAPPING_CONVERGENCE_PLAN.md`
>
> 本文只记录里程碑状态和证据，不定义仓库级分支、提交、评审或发布流程。

## Purpose

This is the single progress and evidence ledger for implementing the resource-mapping replacement. It records delivery state only; identity, schema, Catalog-mount and activation semantics remain authoritative in the design document.

RM-C1 through RM-C5 were inactive shadow-v2 milestones. RM-C6 has validated the atomic activation batch: v2 is the sole production mapping path and the v1/proxy path is removed.

## Evidence Scope

Each milestone row records its implemented outcome and actual validation evidence. Historical commit and remote-reachability fields remain evidence from that milestone; they do not establish a reusable delivery process.

All new compiler, aggregation and bootstrap APIs are low-frequency authoring/startup paths. This work does not add a Tick/Update/Execute hot path.

## Milestone Status

| Milestone | State | Commit | Required outcome | Evidence / blocker |
| --- | --- | --- | --- | --- |
| Flow bootstrap | Pushed | `ee65d08c` | Design revision, ledger and indexes are Current | reachable from `origin/main`; `git diff --check`; V0 documentation review |
| RM-C1 | Pushed | `8dc9b8cdf957` | v2 DTO/schema, aggregator, inventory and cutover worksheet | reachable from `origin/main`; Authoring solution build 0/0; focused tests green; Resource Library smoke; Unity BatchMode compile/import; harness green |
| RM-C2 | Pushed | `968b1f68ea19` | one BCL compiler, deterministic plan and fingerprints | reachable from `origin/main`; Authoring solution build 0/0; compiler focused tests green; Unity BatchMode compile; Resource Library smoke and harness green |
| RM-C3 | Pushed | `513d9c6534fa` | Resource Manager and Unity consume the same shadow plan | reachable from `origin/main`; shared fingerprint API/UI; Unity shadow resolver; focused EditMode 11/11; solution/smoke/harness green |
| RM-C4 | Pushed | `11aabc19` | Character producer materializes canonical owned rows | reachable from `origin/main`; complete snapshot/no-op/stale deletion/revision conflict green; Unity focused 11/11 |
| RM-C5 | Pushed | `f1526c57` | remaining producers materialize 71 resolved shadow rows | reachable from `origin/main`; 71/71 decisions resolved; 70 internal + 1 excluded; zero external/mount/proxy |
| Independent review | Validated | `c83365e2` | zero unresolved activation blockers after RM-C5 | eighth independent read-only review passed with zero blocker |
| RM-C5R | Pushed | `9d7a98df`, `cf73f762`, `f0e490fc`, `0758df7f`, `84b0852d`, `26ac224d`, `c83365e2` | producer authorities, concurrency, strictness, parity and deletion gate corrections | all correction commits reachable from `origin/main`; eighth review PASS |
| RM-C6 | Pushed | `137169c9` plus ledger closeout | v2 activation and complete v1/proxy deletion | complete activation batch validated and included in the single atomic `origin/main` push |

Allowed states are `Pending`, `In Progress`, `Validated`, `Pushed` and `Blocked`. A milestone becomes `Pushed` only after the post-push fetch proves its commit is reachable from `origin/main`.

## Fixed Artifacts

- Production Profile: `Assets/Config/MxFramework/ResourceProfiles/global_resource_mapping_profile.v2.json`
- Cutover worksheet: `Docs/Validation/RESOURCE_MAPPING_CUTOVER_WORKSHEET.json`
- Generated Player artifacts: `global_runtime_catalog.json`, `global_catalog_mounts.json`, `global_preload_groups.json` and `global_bundle_dependencies.json` under `Assets/StreamingAssets/MxFramework/Resources/`

The v2 compiler rejects v1. No compatibility reader, automatic migrator, proxy Catalog or second production schema remains.

## Independent Review Gate

After RM-C5 is pushed and the shadow path is green, a read-only subagent reviews schema authority, compiler uniqueness, producer ownership, Catalog mounts/allowlists, override/package semantics, preview/build parity, activation deletion scope and recorded validation.

Correctness, data-loss, dual-authority, startup and migration findings block RM-C6. Findings are fixed in RM-C5R and the affected surface is reviewed again until no blocker remains. Passing the gate authorizes automatic RM-C6 activation without another human confirmation.

## RM-C6 Activation Gate

Activation requires all of the following:

- zero unresolved worksheet decisions;
- zero v2 legacy aliases or proxy-key fields;
- zero alternate `K1 -> K2` mappings;
- every external row resolves one concrete `CatalogMount` and allowlisted key;
- Resource Manager preview and Unity build use the same plan fingerprint;
- all v1 readers/writers/planners and proxy implementation files in `Docs/Validation/RESOURCE_MAPPING_RM_C6_DELETION_MANIFEST.md` are deleted or replaced;
- focused noEngine, Unity builder and Offline runtime acceptance are green;
- independent review has no unresolved blocker.

Intermediate RM-C6 commits stay local. The complete activation/deletion batch is pushed to `origin/main` once after rebase-safe validation.

## RM-C1 Evidence

- Scope: shared v2 DTO/schema, producer receipt, override and mount contracts, complete-snapshot Profile Aggregator, optimistic revision conflict, read-only inventory command, empty shadow Profile and checked worksheet.
- Path classification: low-frequency Authoring/import only; no Runtime, Tick, Update or Player activation path changed.
- Worksheet: 71 rows, 21 internal, 50 external, 50 missing external Catalog identities, 43 legacy TypeId aliases, 2 collision rows and 71 unresolved decisions. Unresolved decisions are recorded observations for later producer ownership work, not guessed identities.
- Determinism: worksheet SHA-256 `bf628b3c8caeb25f14d4540f849df755edf6f0ff65d801ed20236250ab160c24` across consecutive runs; source v1 Profile SHA-256 `083474f29b185625a3b88e14fb27af536b98e58ce9f89145f617c27354682ecf`.
- Fixed artifacts: shadow Profile SHA-256 `e23a6ca3049167b0d4a5ce4995244181fa97737d2807bca020e0bdb6f64965a9`; schema SHA-256 `105418d4e9d5f6b020bc245f983d0f1c7afd5ce36dff0a7456dddb335ae1f44e`.
- Validation: Unity 6000.5.0f1 BatchMode import/compile exited 0; `dotnet build Tools/MxFramework.Authoring/MxFramework.Authoring.sln --no-restore /nr:false -m:1` succeeded with 0 warnings and 0 errors; `ResourceMappingProfileV2Tests` passed; Resource Library smoke passed; `git diff --check` passed; changed-path standards found no issues; lightweight harness passed.
- Baseline note: the full unfiltered Authoring test executable reaches an unrelated existing `FairyGuiArtSliceAuthoringTests.UiComposer_LoadsRuntimeAiLabShellBlueprint` fixture failure. The new focused `resource-mapping` entry point is green and the entire solution compiles.
- Residual input to RM-C2: implement one deterministic BCL compiler and canonical serializer over these exact DTOs; production remains exclusively on v1.

## RM-C2 Evidence

- Scope: shared `GlobalResourceBuildPlanCompiler`, typed Build Plan, strict v2 JSON reader, canonical JSON writer, normalized Profile revision, mapping-input fingerprint, plan fingerprint, deterministic Bundle membership, Catalog-mount allowlists, preload expansion and ordered diagnostics.
- Authority boundary: the implementation lives only in the Editor-only `MxFramework.ResourceMapping.Authoring` source assembly and is linked into Authoring Core by `Compile Include`; no copied source, generated DLL or CLI semantic delegation exists.
- Contract coverage: old schema/unknown fields, invalid shapes, dependency resolution, explicit Bundle ownership, mount resolution, ProviderData authority, complete-key conflicts, `WithoutPackage()` override/type parity, receipt provenance and delivery counts have focused tests.
- Determinism: reordered Profile lists and duplicate set inputs produce byte-identical canonical plan JSON and identical fingerprints; a producer-receipt fingerprint change invalidates `mappingInputFingerprint`; canonical output is UTF-8 without BOM and ends in LF.
- Source fingerprints: plan DTO `e0a62f704f78f1107b6883431b853bc87bed6592919128dbebef036ac609defb`; compiler `ef58b8997c774fe254ab69b8f807082770fcf961ad0ab2fd031f7cfdf06dc393`; canonicalizer/strict reader `99fb95baf8877f8dd0e8b0a9668087a37ec2538af46963b5b04ab9dc32aca859`.
- Validation: Unity 6000.5.0f1 BatchMode import/compile exited 0 with no compiler errors; Authoring solution build succeeded with 0 warnings and 0 errors; `ResourceMappingProfileV2Tests` passed; Resource Library smoke passed; `git diff --check` passed; changed-path standards found no issues; lightweight harness passed.
- Residual input to RM-C3: add inactive shadow adapters so Resource Manager preview and Unity Builder deserialize v2 strictly and consume this exact compiled plan; v1 remains the only production action.

## RM-C3 Evidence

- Scope: read-only Resource Manager `/resource-mapping-v2-preview` endpoint and Build-workspace panel; Unity `GlobalResourceMappingV2ShadowBuilder`; assembly references and focused fixtures. Both adapters strictly read the fixed v2 Profile and call the RM-C2 `GlobalResourceBuildPlanCompiler` directly.
- Production boundary: the Resource Manager panel is labelled `Shadow v2 · 只读`; the API returns `productionActive=false` and `readOnly=true`; the Unity result fixes `ProductionActive=false` and `WritesPlayerArtifacts=false`; no v2 build menu or Player/StreamingAssets writer exists. The v1 Validate/Build actions and artifacts are unchanged.
- Unity responsibility: after shared compilation, Unity resolves only internal GUID/path/main-object type and dependency hashes into a source-input fingerprint. It does not recompute identity, delivery, Bundle membership, dependencies, mount order or allowlists.
- Parity: Resource Manager and Unity expose the exact shared plan's `mappingInputFingerprint` and `buildPlanFingerprint`; the checked-in empty shadow Profile compiles without inventing producer rows.
- Source fingerprints: Unity shadow adapter `d2afc6ef0d4f55564cee75a2995979d1b72711d38d4884786375c06b71d9ef68`; Resource Manager adapter `a506a91f048e0499345389e1be5bef0735de088f6191dae5d8d5c4384b663f8f`; read-only UI `0b1d12ada0ec57bb2545e27449f818174290cd4f2a4d8d302f7930eaa6902fd0`.
- Validation: Unity 6000.5.0f1 compile exited 0; focused EditMode set passed 11/11 (two v2 shadow builder cases plus all `GeneratedResourceBuildArtifactLoaderTests`); Authoring solution build succeeded with 0 warnings/errors; Authoring resource-mapping tests and Resource Library smoke passed; `git diff --check`, changed-path standards and lightweight harness passed.
- Baseline note: the broader legacy `GlobalPlayerResourceBuildProfileBuilderTests` set passed 7/8; its unrelated Editor runtime load test cannot find existing key `char.iron_vanguard.model.body`. Both new v2 tests and all required artifact-loader tests are green.
- Residual input to RM-C4: Character authoring must submit one complete owner snapshot through the Aggregator; v1 Character writers remain active until RM-C6.

## RM-C4 Evidence

- Scope: the Character package compiler now emits one canonical v2 producer snapshot; `compile` writes it beside the existing outputs, `import-unity` submits it through the shared Profile file Aggregator, and `sync-character-shadow` materializes the checked-in inactive shadow Profile without altering the v1 writer.
- Ownership: the sample package owns producer `character.package.codex-flow-test`; its complete snapshot contains four rows (two `internal`, two `editorOnly`). Runtime model/texture rows have explicit canonical TypeId and Bundle ownership; preview rows remain non-runtime and carry no ResourceKey. Config and geometry sources remain outside resource mapping.
- Replacement semantics: focused tests cover initial apply, byte-identical no-op, complete-snapshot stale-row deletion, owner-scoped deletion rejection, cross-owner conflict, optimistic revision conflict and canonical file round-trip. The import command proves the Character path reaches the same file Aggregator.
- Identity boundary: v2 output contains no Project/Provider/Package/Runtime key aliases and does not infer identity from a v1 compatibility reader. The active v1 Character mapping output remains present until the atomic RM-C6 deletion.
- Fingerprints: checked-in shadow Profile/revision `11d799b97502c228d722ca3f32766b94a23e785fa273455fe8d8ae5069dc2eff`; compiled plan `a1d3e329cdbe683c9509372d697de0a014edc9bd53d9c48f4d5af705ce75c1d9`; file Aggregator source `560b50aabcc7f7547b95be823f444da1aa3eac606a821c530d269f80338ccb84`; Character producer source `359bb0c03f8772bad6bcc0c9f29a7afd518fcdf5ddd147de018ad3183ea84c0f`.
- Validation: Authoring solution build succeeded with 0 warnings/errors; focused `ResourceMappingProfileV2Tests` passed; repeat shadow sync reported `changed=false`; Resource Library smoke passed; Unity 6000.5.0f1 compiled with no errors; checked-in Character shadow plus shared adapter/artifact-loader EditMode set passed 11/11; `git diff --check`, changed-path standards and lightweight harness passed.
- Residual input to RM-C5: replace the temporary sample producer snapshot with repository-owned Character rows while UI/FairyGUI, VFX, Audio, Demo/sample and manual producers submit complete snapshots; resolve all 71 worksheet rows and every external Catalog ownership from repository evidence.

## RM-C5 Evidence

- Scope: the reviewed cutover worksheet now assigns every source row to one named complete-snapshot producer: Character 35, Audio 4, FairyGUI 15, Start Screen UI 7, VFX 4, Demo/sample 5 and Resource Manager manual 1. The temporary RM-C4 sample producer is replaced with an empty complete snapshot.
- Ownership decision: 49 legacy `external` rows resolve to checked repository-owned Unity assets under `Assets/...`; the remaining row is a duplicate proxy observation for `ui.start_screen.button.normal`, retained as one non-runtime `excluded` audit record while the Resource Manager manual producer owns the canonical internal row. The existing `global_external_assetbundle_catalog.json` and `global_external_runtime_catalog.json` are generated first-party proxy artifacts rather than independent Catalog authorities. Therefore no row requires a guessed external Catalog identity.
- Result: 71 materialized records, 70 runtime internal entries, one excluded record, zero external entries, zero Catalog mounts, zero unresolved decisions, zero duplicate runtime keys and zero missing checked source paths. The Profile declares 19 Bundle rules and five non-empty preload groups; the explicit reviewed memberships cover `boot.base`, `SpawnCritical`, `EquipmentInitial`, `AnimationWarmup` and `UiDeferred`.
- Identity boundary: the worksheet is source-byte pinned and every decision records producer, authoring record, complete canonical key, delivery, override, Bundle and preload ownership. The materializer refuses stale source bytes, unresolved decisions and row reorder/key mismatch; Profile output contains no v1 TypeId aliases, delivery aliases, proxy routing fields or K1-to-K2 redirect.
- Fingerprints: shadow Profile/mapping input `16fc412e7d2318d18057072eea64be90ad8ced8136c27f2185365b938dadf965`; compiled plan `9bd6252c16684c1248b2159feb4b123327795c334d20eb55d53597cea0726298`; reviewed worksheet `1204302f05ec20f10f34c78d38142bcebb769b9996b8f1caa0e11b477c46d433`; materializer source `50be5175fd5e0207400ab4d1259459ffba00d912b3223225af4ef57259e06b28`.
- Validation: deterministic repeat sync reported `changed=false`; Authoring solution build succeeded with 0 warnings/errors; focused `ResourceMappingProfileV2Tests` passed; Resource Library smoke passed; Unity 6000.5.0f1 compile exited 0 and the full 70-source checked shadow plus shared adapter/artifact-loader EditMode set passed 11/11; `git diff --check`, changed-path standards and lightweight harness passed.
- Residual input to independent review: verify that making all first-party legacy external rows internal correctly eliminates Catalog ownership rather than masking a concrete third-party authority; verify the seven producer boundaries, excluded collision decision, preload memberships, activation deletion list and RM-C6 runtime/bootstrap replacement.

## RM-C5R Review Corrections

- Independent review result: seven activation blockers were recorded. They covered durable producer authority, cross-process lost updates, strict required-field and alias rejection, internal route collision parity, Resource Manager/Unity build-target parity, precise RM-C6 removal scope and ignored proxy artifacts surviving on disk. All seven are blocking until the affected-surface re-review passes.
- Superseded producer disposition from `9d7a98df`: the generic producer-manifest format was rejected by the second independent review because it duplicated the complete mapping schema and created a persisted dual authority. RM-C5R2 deletes that format and its eight files. Repository-source adapters now materialize six domain snapshots from checked source assets plus Unity GUID metadata and adapter-owned mapping conventions; the central policy adapter is code-owned. Resource Manager manual snapshots persist only through the canonical Profile Aggregator, so the v2 Profile is again the only persisted mapping input.
- Concurrency/strictness disposition: the file Aggregator holds a deterministic cross-process lock across final revision verification and atomic replacement; a two-writer fixture proves exactly one commit and one typed conflict. Strict v2 reads require every schema property, reject unknown fields and reject the full retired built-in alias set including `model` and `avatarMask`.
- Semantic parity disposition: two internal entries may never share one package-less route, even with `overrideIntent=allow`; cross-Catalog override remains explicit. Resource Manager v2 preview and manual save reject `ActiveBuildTarget` and require the concrete target obtained from Unity build status; focused preview evidence uses `StandaloneOSX`, matching the Unity builder.
- Activation safety disposition: `Docs/Validation/RESOURCE_MAPPING_RM_C6_DELETION_MANIFEST.md` names every whole-file deletion and mixed-file replacement plus the zero-scan gate. `LegacyResourceMappingArtifactCleanup` enumerates and removes only the exact ignored proxy outputs; its fixture proves unrelated concrete external Catalogs are preserved. Real workspace cleanup remains deferred to the atomic RM-C6 batch.
- RM-C5R2 shadow result: 71 records remain deterministic: 70 internal, one excluded duplicate, zero external and zero Catalog mounts. Mapping/Profile fingerprint is `56b9fd160728cc43ba6e8b65f697c63056e7fb066952ef42549ce5d9b1b758f2`; Build Plan fingerprint is `b0782cf0eb372ffc8ac63b394ede7011ad82b58bdcfd07851127ea354d12d1ec`.
- Source fingerprints: manifest adapter `cbbafbdc80fbc8cb159594d2909db7250454453cfae8109075dac7847f9b0857`; file Aggregator `33d9716cfdd390de25a9f53460f80337bb551190cd2ae4e44facf4ca10b271cb`; strict reader `c900fe3aa42fc9e8c0bf088646b9650a26d7eb34b6ee101c736d6b3cba805c4f`; producer registry `41f648c0a7724f1b5e99faa5fdeebcb0b8fdf98718ddc53df38a1e9c23fa4dcf`; cleanup gate `424a2c81660923539403ba4af31f7ebb99abb6f45cea8d6d753b199400dbc274`.
- Validation: Authoring solution build succeeded with 0 warnings/errors; focused resource-mapping executable passed; deterministic first-party sync returned `changed=false`; Resource Library smoke passed; Unity 6000.5.0f1 BatchMode compile exited successfully; focused EditMode tests passed 11/11; `git diff --check`, changed-path standards and lightweight harness passed. The unrelated Unity VFX auto-refresh outputs were restored and excluded.
- Residual input to re-review: verify the seven dispositions against implementation and tests. RM-C6 remains blocked until that review reports zero unresolved activation blockers.

## RM-C5R2 Second-review Corrections

- Second review result: RM-C6 remained blocked on a persisted producer-manifest dual authority, manual two-file transaction risk, incomplete JSON-schema kind enforcement and TypeId alias coverage, Catalog-local external route collision, and an activation zero-scan that matched its own cleanup helper.
- Authority correction: the generic `mx.resource-mapping-producer-manifest.v1` DTO and all eight persisted mapping copies are deleted. Six repository-source adapters now validate each checked asset and its Unity `.meta` GUID before emitting complete domain snapshots; mapping conventions and policy are adapter code, while the canonical v2 Profile remains the only persisted compiler input. Manual save submits one complete `resource-manager.manual` snapshot directly to the same file Aggregator and creates no sidecar authority.
- Strictness correction: the strict reader now enforces object/array/string-map/string/boolean/integer/nullability kinds for every required schema field and forbids integer enum values. A shared canonicalization table covers every built-in runtime TypeId plus retired aliases; persisted casing/aliases such as `gameObject`, `sprite`, `material`, `textAsset` and `panelSettings` are rejected, while domain adapters use the same table before materialization.
- Route correction: runtime collision validation derives a source-Catalog identity (`primary` or `CatalogMountId`). Duplicate package-less routes inside one source Catalog always fail; explicit override applies only across different Catalogs in mount order.
- Activation-manifest correction: the RM-C6 list now explicitly covers `EditorServer.cs`, the shadow Unity adapter/coordinator and tests/current docs. The one-shot proxy cleanup helper and fixture must be deleted after invocation, so the final production zero-scan cannot self-match.
- Determinism: repeated repository-source synchronization reports `changed=false`; 71 records remain 70 internal plus one excluded, with zero external/mount. Mapping/Profile fingerprint is `56b9fd160728cc43ba6e8b65f697c63056e7fb066952ef42549ce5d9b1b758f2`; Build Plan fingerprint is `b0782cf0eb372ffc8ac63b394ede7011ad82b58bdcfd07851127ea354d12d1ec`.
- Source fingerprints: repository-source adapters `3616fa01db23de6d358ab4804251a81b05d5eeddb30b8b996972497d66f33289`; strict reader `378d5ea6f8b3b2e66dfc0c07254cd0a1c66e337a76a07f50058bbf964d99ca13`; TypeId validator `a0fc3640b68aafc3e150b0fa05a620b68bb9847a63e90f88328eeb028606b96b`; compiler `9d2af5478d1244c3b799cf4fa26fda576578743557c7c8cc79ec33674cbffe53`.
- Validation: Authoring solution build succeeded with 0 warnings/errors; focused resource-mapping tests, concurrent Profile/manual writers, Resource Library smoke and deterministic sync passed; Unity 6000.5.0f1 BatchMode compile succeeded and focused EditMode passed 11/11. Changed-path standards, lightweight harness and independent re-review are the remaining pre-push gates.

## RM-C5R3 Third-review Corrections

- Third review result: the per-resource C# `Seeds` table remained a complete mapping copy, the first-party coordinator was scheduled for deletion after activation, the Character coordinator/help text were missing from the replacement manifest, and Character plus the central adapter could both replace the same policy owner.
- Producer authority correction: the `Seeds` table is deleted. Character, Audio, FairyGUI, Start Screen, VFX and Demo adapters enumerate their domain-owned repository surfaces, read the current Unity `.meta` GUID and derive canonical key, type, package, Bundle, label and preload intent from domain path conventions. A focused fixture proves discovery of an added asset, complete-snapshot omission after deletion, and GUID-preserving move/path refresh.
- Lifecycle correction: RM-C6 now productionizes and retains the first-party and Character synchronization coordinators instead of deleting them; `Program.cs` is explicitly in the replacement set. Domain publication can therefore rebuild owner-scoped complete snapshots after activation rather than leaving orphaned Profile rows.
- Policy correction: the central policy adapter is the only constructor for the complete `resource-manager.profile-policy` snapshot, including the optional `character.package.assets` rule. Character import delegates to that adapter and only substitutes the requested concrete build target; the second policy builder was removed.
- Determinism: repeated repository synchronization reports `changed=false`; the Profile remains 71 records (70 internal, one excluded, zero external/mount). Mapping/Profile fingerprint is `290e29eec847debd1507c6a5d74eae3b6cd5eb4907d3c7046ffe30c0ee7cadf8`; Build Plan fingerprint is `e1df5a8a60d2a6b37631c904ef1df95488ebdece2aa8dc67bea47d0b1c6b0b6b`.
- Source fingerprints: domain enumerators and central policy `a27ccda47300428b87c759992fcf0507ca0ea6229df49494d7fba205c944c156`; Character coordinator `b37efe0a6b054956727b12cc99dadc47a465a73912ba93dcd574932d2a2ec255`.
- Validation: Authoring solution build succeeded with 0 warnings/errors; focused resource-mapping cases passed before the known unrelated FairyGUI shell fixture failure in the unfiltered executable; deterministic sync passed; `git diff --check`, changed-path standards and lightweight harness passed. Independent affected-surface review remains required before RM-C6.

## RM-C5R4 Fourth-review Corrections

- Fourth review result: RM-C6 remained blocked because policy materialization ignored explicit `allowEmpty=false`, missing domain roots returned successful empty complete snapshots, Character synchronization persisted one concrete build target, and the activation replacement/zero-scan list omitted several legacy and shadow symbols.
- Fail-closed correction: every repository producer declares required domain roots; Demo also declares its required scene. Missing authority now returns `ProducerSourceRootMissing` / `ProducerSourceMissing` before a snapshot can reach the Aggregator. A focused fixture proves a missing root cannot become a successful empty complete snapshot or owner-scoped mass deletion.
- Empty-output correction: Bundle rules again preserve their explicit `allowEmpty` policy and all preload groups remain non-empty gates. Rules with current production members, including `common.models` and `common.textures`, are fail-closed; only rules with no current producer members (including pre-import `character.package.assets`) may be empty.
- Target correction: central policy remains persisted as `ActiveBuildTarget`; Character synchronization passes the requested concrete target only to compilation and never rewrites reusable Profile policy. The focused Character fixture asserts this persistence boundary.
- Identity correction: FairyGUI keys preserve their concrete package IDs such as `MxCharacterTestUi`; a GUID-preserving same-name move across directories retains the runtime ResourceKey while refreshing only the cached source path.
- Activation correction: the manifest now covers three additional Character v1 readers, the Character write-plan compiler, the active build report, both coordinator type names, the Unity shadow builder type and PascalCase shadow flags.
- Determinism: repeated synchronization reports `changed=false`; mapping/Profile fingerprint is `e3ed2fccfd5c56b743377a90074522f5706ec07e3d60551129cc5c813cf22b46`; Build Plan fingerprint is `cdf519adc13a4eb2c3e09322d690a0c93c6a96357aea009065b0844a05df30a0`.
- Source fingerprints: fail-closed domain adapters/policy `7c05cd68a409d3fa91ff42b5ea33798426acef235837304aec571b597088c0f0`; Character coordinator `b3750c89e7b4869b0bfd0943d1e8211b0981be7820a5f61b5ba5b512712853b1`.
- Validation: Authoring solution/test build succeeded with 0 warnings/errors; focused `ResourceMappingProfileV2Tests` passed; deterministic sync and Resource Library smoke passed; `git diff --check`, changed-path standards and lightweight harness passed. A new independent affected-surface review remains required before RM-C6.

## RM-C5R5 Fifth-review Corrections

- Fifth review result: an obsolete recurring empty Character-package tombstone could delete a valid imported package; critical FairyGUI bytes were not individually fail-closed; file rename still changed identity; and activation coverage omitted v1 test references plus several shadow API names.
- Ownership correction: the one-shot `character.package.codex-flow-test` tombstone is removed from recurring first-party synchronization. A focused invariant forbids any recurring first-party adapter from claiming `character.package.*`; Character import remains the sole complete-snapshot owner for those rows.
- GUID identity correction: adapters consult the canonical v2 Profile's prior owner rows by Unity GUID before applying path conventions. A GUID-preserving rename now retains ResourceKey/type/package/Bundle/preload identity and updates only the source path and receipt fingerprint; the Profile remains the sole persisted mapping authority.
- Completeness correction: all required FairyGUI package bytes are explicit producer input gates, even when the shared package root and atlas remain. `common.models` and `common.textures` now reject empty membership because both have current production rows; missing roots/files fail before aggregation.
- Activation correction: the manifest now includes the Authoring test runner and every Character test partial that references v1 DTO/providers. Shadow zero-scan includes build-input/result types, server reader, web payload and `WritesPlayerArtifacts=false` in addition to the previously covered names.
- Determinism: repeated synchronization reports `changed=false`; mapping/Profile fingerprint is `a2ad422478b39c5b13e057864cf26557f4955d734bb8c98b0399ee5aa1299310`; Build Plan fingerprint is `4c819562991c4eaad18fc641db8e4336d6c3ca5187d5cce5a242c2e21e3856c6`.
- Source fingerprints: GUID-authoritative fail-closed adapters/policy `103916b73317aaeb569d1834401053c6c161526f3e7898086407a37a330eac4a`; recurring coordinator without tombstone `12fa880ed0048a27628286289fa0b223cffb5ae8b6a8b3f520b8b541bcff02a5`.
- Validation: Authoring solution build succeeded with 0 warnings/errors; focused `ResourceMappingProfileV2Tests`, deterministic synchronization and Resource Library smoke passed; `git diff --check`, changed-path standards and lightweight harness passed. Independent re-review remains required before RM-C6.

## RM-C5R6 Sixth-review Corrections

- Sixth review result: GUID inheritance could last-win on ambiguous prior identities, adapters read an unbound Profile before the coordinator acquired the commit revision, fixed-name assets were described before GUID lookup, and prior semantic inheritance omitted Variant/dependencies/provider data. Cross-sync and FairyGUI fixtures did not yet execute the full failure paths.
- Revision/identity correction: the coordinator reads the canonical Profile and revision once, passes that exact immutable snapshot to every adapter, then submits against the same revision. Adapters never re-read Profile. GUID lookup runs before path conventions, duplicate owner/GUID identity is a typed blocking diagnostic, and inherited rows retain complete ResourceKey, override, Bundle, labels, preload, dependencies and provider data while refreshing source path/receipt.
- Cross-owner correction: a full Character sync followed by recurring first-party sync now proves all four package-owned rows survive. This exercises the coordinator path that previously injected the tombstone rather than only inspecting adapter IDs.
- Completeness correction: all eight FairyGUI package bytes and all seven current atlases are required inputs. The fixture asserts all 15 independent missing-file diagnostics. The RM-C6 manifest also covers the Unity Character-import fixture and additional shadow payload/field/CSS names.
- Determinism: repeated synchronization reports `changed=false`; mapping/Profile fingerprint is `60cf71c3e6eea79a024e0f183eadca71472ebac2861a4383ec373f91cee19173`; Build Plan fingerprint is `96f90c7d195b3699bb890f44ae08d143c627c522a5874aae27bfd8fbeb607e27`.
- Source fingerprints: revision-bound GUID authority/adapters `36c82f4f5cc2eb91f8528095fcd4e0e279d3f9fcf03231766959d382f9c3d568`; coordinator `294ad664a405f5e0cd837db6d906c56bb356734ec57ddf35435180bef0a7b9c8`.
- Validation: Authoring solution build succeeded with 0 warnings/errors; focused `ResourceMappingProfileV2Tests` including ambiguous GUID, rename, cross-sync and 15-file FairyGUI gates passed; deterministic synchronization and Resource Library smoke passed; `git diff --check`, changed-path standards and lightweight harness passed. Independent re-review remains required before RM-C6.

## RM-C5R7 Seventh-review Correction

- Seventh review found no new producer/revision/completeness blocker, but the activation zero-scan could still miss specific Unity, Server and Web Shadow surfaces.
- The zero-scan now explicitly covers `ShadowProfilePath`, `ValidateShadowProfile`, `ResolveResourceMappingV2ShadowProfilePath`, load/render preview methods, unavailable state, Shadow labels and the no-Player-artifact contract in addition to the already listed types, payloads, flags and CSS class.
- Scope is documentation-only activation-gate hardening; Profile and Plan fingerprints remain `60cf71c3e6eea79a024e0f183eadca71472ebac2861a4383ec373f91cee19173` and `96f90c7d195b3699bb890f44ae08d143c627c522a5874aae27bfd8fbeb607e27`.
- Validation: `git diff --check` and lightweight harness passed; independent re-review remains required before RM-C6.

## RM-C6 Activation Evidence

- Activation: `global_resource_mapping_profile.v2.json` is production-active and is the only mapping input. Resource Manager preview/save, Character import, recurring first-party synchronization, Unity Editor runtime and Player build all use v2; the Resource Manager manual surface replaces only its complete `resource-manager.manual` snapshot with optimistic revision.
- Deletion: the v1 Profile, DTO, validator, planner, Resource Library provider, inventory command and tests are deleted. The proxy Catalog provider, bootstrap rewrite, one-shot cleanup helper and exact generated proxy artifacts are deleted. Production source and current-fact documentation pass the deletion-manifest zero scan with no retired reader, writer, planner, proxy key, shadow route or shadow flag.
- Runtime: Offline startup loads the internal catalog plus `global_catalog_mounts.json`; a mount validates relative path, optional SHA-256, expected Catalog id, exact compiled key allowlist and concrete Provider availability before entries are registered. No `K1 -> K2` redirection exists.
- Current data: worksheet decisions are 71/71 resolved. The production Profile contains 71 records: 70 internal, one excluded audit row, zero external and zero Catalog mounts. This repository therefore requires no human external-Catalog ownership decision at activation.
- Parity: the actual StandaloneOSX Player artifact build reports mapping-input fingerprint `sha256:0cca644b4cb8131a6e1a59b7e6bcc7130ea29f0662a246f13560bb34c5c2b2ab` and Build Plan fingerprint `sha256:46f125c235630446e997cfbba8d2518e2e57b233f6bec966f09a745e5b7edf1d`; the generated catalog contains 70 concrete `assetBundle` / `sceneAssetBundle` entries and the mount manifest contains zero mounts.
- Validation: Authoring solution build succeeded with 0 warnings/errors; focused `ResourceMappingProfileV2Tests`, Resource Library smoke and CharacterStudio smoke passed. Unity 6000.5.0f1 BatchMode compile succeeded; focused builder EditMode passed 5/5, mount/artifact loader EditMode passed 4/4, Character public-default-resource mapping passed 1/1 and Offline scene loading/release PlayMode passed 2/2. `git diff --check` passed; changed-path standards reported no findings after the final diagnostic wording correction; lightweight harness passed.
- Baseline note: the unfiltered Authoring executable still reaches the pre-existing unrelated FairyGUI Runtime AI Lab shell-blueprint fixture failure recorded in RM-C1. RM-C6 focused tests, builds and mandatory harnesses are green.

## Post-activation Final-review Corrections (2026-07-21)

- State: `Validated` for direct `main` delivery from base `3d098b3972a5b2783addd45f32443c20ba301628`; commit identity and `origin/main` reachability are recorded by the Git delivery history instead of a self-referential hash in this commit.
- Producer ownership: Common Resource sync no longer constructs a second partial `demo.samples.global-profile` writer and no longer runs every unrelated first-party producer. It submits only that named owner's complete snapshot through the sole file Aggregator, including the CharacterTest scene plus common assets. A missing concrete target is rejected before file mutation. Pending Unity imports retain their runtime key, Bundle/preload intent and a `GeneratedUnityAsset` locator instead of abusing `editorOnly`; a focused full-flow fixture proves later GUID refresh preserves dependencies, labels and provider extensions.
- Resource Manager ownership: the Web UI may replace only the complete `resource-manager.manual` snapshot. Domain producer rows and all Bundle/CatalogMount policy are read-only. Single, batch and Refs recommendation paths skip non-manual rows; save preserves canonical source locators and provider extension fields, strips retired routing aliases, rejects external rows without an explicit mount, and gives the edited delivery/Bundle projection precedence over stale loaded canonical intent.
- Compiler/emission contract: Bundle rules require a concrete resolved target and canonical `lz4` / `lzma` / `uncompressed` compression. Mixed compression and target mismatch fail before Unity emission. The Builder applies the chosen compression and `includeDependencies`; Authoring commands resolve a concrete target from an explicit argument or a schema-v2 Unity build report and never guess a platform default.
- Plan/UI parity: Resource Manager consumes the current shared Plan DTO (`entries`, `counts`, `bundles[].bundleId/resourceKeys/includeDependencies`) and the corrected Unity menu names. Retired v1 fields and default-target constants remain only in negative regression scans or the compiler's forbidden-alias table.
- CatalogMount boundary: each compiled allowlist row freezes one complete `ResourceKey` plus override intent. Runtime and Editor validate mount order, safe concrete path or exact composition-root supply, expected Catalog id, canonical Catalog SHA-256, duplicate/absent keys, override parity and Provider availability before registration. Editor now supplies the standard external AssetBundle Provider under the same target-specific root as Offline. Application/test-local catalogs outside global mapping are registered explicitly after global bootstrap.
- Artifact generation: the Unity Builder stages JSON and Bundles, publishes payloads atomically per file, then writes `global_resource_artifact_set.json` last as the generation commit marker. Runtime validates required kinds, unique relative paths, per-file SHA-256, target and cross-artifact identity before Provider registration. Resource Manager Build Status additionally requires the schema-v2 build report, artifact set and current compiled Profile to agree on Profile/Catalog/target and mapping/build/source fingerprints.
- Runtime migration cleanup: unvalidated `ExternalCatalogs` / `ExternalCatalogPaths` injection and platform-derived target fallback are removed. CharacterTest's own VFX/test catalogs are application composition-root catalogs and are added after global services are created rather than masquerading as global external mapping inputs.
- Source fingerprints after correction: compiler `1226ce567fd232ea4cab01b19e45ad1dfc8a7b7b3cdc0d694444532cbd9ca01e`; Unity Builder `04555a0d3c1ef8db910d3a53e7405d502afe05ca0047cd4c77ea23c916360b7b`; CatalogMount loader `f28263aa852cc34256a8181c596b4f6aa58a36aa9516aa84e5cbc6d13e369b3d`; artifact-set loader `ae736571fc8d2bec9686d223cbd8b864d0e682782df9a45d2930c6e29b88685c`; Authoring build status `07d926f4c52629999d66c2c492260131a6a8a71e05322f7c6401b06174f4a59e`; first-party adapters `6f4bc936640fb74667218d6f5b4cb870c3000792970eaa6fe5b7ab44054cd3f1`; Resource Manager manual serializer `ceb9a4c791c336275a411eef9ff03d2b421e4ceb0f3a75803ca9e4c54151e84c`.
- Validation: Authoring solution build succeeded with 0 warnings/errors; focused `resource-mapping` and `common-resources` executables passed; Resource Library smoke passed. `MxFramework.Editor.csproj` built with 0 errors and existing report-only warnings. Unity 6000.5.0f1 BatchMode focused EditMode passed 10/10 across `GlobalPlayerResourceBuildProfileBuilderTests` and `GeneratedResourceBuildArtifactLoaderTests`; the affected CharacterTest preload integration case passed 1/1. Changed-path standards scanned 24 first-party code files with no findings; `run_lightweight_checks.sh`, docs/meta/forbidden-path gates and `git diff --check` passed. Unity MCP and a project Editor instance were unavailable, so the repository-prescribed BatchMode fallback supplied compile/test evidence; unrelated VFX auto-refresh outputs were restored after each run.
- Baseline note: the unfiltered Authoring executable still stops at the pre-existing unrelated `FairyGuiArtSliceAuthoringTests.UiComposer_LoadsRuntimeAiLabShellBlueprint` fixture recorded since RM-C1. No resource-mapping focused test is failing.
- Residual risks: the checked production Profile currently has zero external rows, so concrete external mounts are covered by focused composition-root and StreamingAssets fixtures rather than a production mount. Authoring cannot independently recompute Unity GUID/path/type source currentness; Build Status reports that Unity verification is required instead of inferring it from file existence. Stale unreferenced Bundle cleanup remains a separate operational follow-up; the committed artifact set prevents Runtime from accepting a partial or mixed generation.
