# Authoring Workflow

> Status: Current

Authoring tooling is limited to pipelines that are still active in the framework: Ability packages, Buff v2 packages, Character resource packages, Animation mapping, VFX Authoring Catalog validation, Resource Library diagnostics, UI Art Resource Library tooling, semantic UI Composer, FairyGUI art-slice usage audits, and Action Damage Analysis.

## Retired Buff Workflow

The legacy external Buff Authoring Editor has been removed. The following old paths are not supported:

- WGame `BuffType` and `DamageByAttr` authoring.
- `BasicBuffConfig` / `ConfigBuffFactory` / external Buff data migration.
- Buff workflow/schema export commands.
- Buff patch validate / merge preview / report bundle commands.
- Preview commands that sent `applyBuff` to Unity.
- `Tools/MxFramework.Authoring.Editor` and the old `buff-preview` / `buff-mod` samples.

Buff v2 now has a JSON + CLI authoring path and a Buff v2 Workbench for managing existing `config/buff_authoring.json` packages. The Workbench is the current designer-friendly editing surface for existing Buff/Aura fields: it shows Chinese labels and field explanations, edits Buff time as `瞬时 / 持续 / 永久`, edits stacking as `可叠层 / 最大层数`, edits player-facing multi-type classification through `buffs[].types`, maps known CharacterTest attributes to safe dropdown choices, and folds raw identity/structure fields into technical details while saving the existing authoring JSON fields. Buff presentation metadata now includes `presentation.nameKey` for localized display names, `displayName` as fallback/developer-readable text, `descriptionMode` (`Auto` / `Manual`), short badge text, and icon resource key diagnostics. Auto descriptions are generated from effect, time, and stacking config for preview/generated presentation output; they are not written back into `buff_authoring.json`. Generated presentation JSON still includes a compatibility `polarity` value for HUD color/sort, but it is inferred from `types[]` rather than edited as authoring source. Both paths compile to runtime JSON for `GameplayBuffDefinitionRegistry`, `GameplayBuffPresentationDefinitionRegistry`, and `CombatGameplayAuraBuffDefinitionRegistry`. This is not the old Buff editor and does not restore `BuffType`, `DamageByAttr`, `applyBuff`, patch preview, or legacy migration.

## Current Workflow Boundaries

- Authoring Core and CLI do not reference `UnityEngine`, `UnityEditor`, or WGame runtime assemblies.
- External tools work on package folders and JSON manifests, then emit validation or compile outputs.
- Unity import remains an explicit bridge step; external editors must not write Unity YAML, asset GUIDs, prefabs, or scene data directly.
- Runtime gameplay consumes compiled or registered data, not live authoring drafts.

## Active Package Workflows

### Ability Package

```bash
mx-authoring ability compile --package <AbilityPackage> --out <dir>
```

Used for deterministic ability definitions and CharacterTest runtime slices.

### Projectile Authoring Core

```bash
mx-authoring projectile validate --package <AbilityPackage> [--report-out <dir>]
mx-authoring projectile compile --package <AbilityPackage> --out <dir>
mx-authoring projectile workspace --package <AbilityPackage> [--out <workspace.json>]
mx-authoring projectile mutate --package <AbilityPackage> --request <mutation.json> --draft-out <draft.json> [--report-out <dir>]
mx-authoring projectile apply --package <AbilityPackage> --draft <draft.json> --out <generated-dir> [--project-root <repoRoot>] [--projectile-root <Assets/...>] [--player-mirror-root <Assets/...>] [--receipt-out <dir>]
mx-authoring projectile import-unity --package <AbilityPackage> --project-root <repoRoot> [--projectile-root <Assets/...>] [--player-mirror-root <Assets/...>] [--report-out <dir>] [--dry-run]
```

Projectile source remains the package-local `config/projectiles/<requestId>.json` authority. The noEngine workspace preserves original bytes and JSON trees and distinguishes Request, Spawn Profile, Definition/numeric ID and Presentation Binding. Semantic no-op preserves bytes; full-document changes/removal of unknown fields are blocked. `compile` emits three projectile JSON catalogs, three binary companions and a compile report (current schemas 5/2/2). `apply` checks expected SHA-256, recompiles an isolated staged package, and guards commit/rollback for the complete source/generated set. With `--project-root`, that same transaction also writes six Unity Generated artifacts and six CharacterTest Player mirror artifacts; without it, mirror freshness is `NotUpdatedByTransaction`. Explicit `import-unity` compiles once and atomically updates both roots, reporting hashes, freshness and rollback/recovery status. Its dry-run reports target paths without writing the twelve artifacts; explicit `--report-out` still writes a report, and the dry-run itself does not emit per-artifact hashes. See the [current transaction contract](Guides/PROJECTILE_AUTHORING_GUIDE.md#core-与-cli-事务合同).

The Unity menu `MxFramework/Combat/Projectile Authoring` is an Editor-only projection over this CLI boundary. `workspace` produces the relative-path Editor contract; `mutate` produces a versioned planning draft and reference impact without modifying content. The window never serializes gameplay DTOs or writes projectile files directly. Save delegates to `apply --project-root`; only `Committed` with `playerMirrorFreshness=UpdatedAtomically` refreshes AssetDatabase and reloads workspace. Failed or externally-conflicted sessions retain dirty state. Start with the [Projectile Authoring guide](Guides/PROJECTILE_AUTHORING_GUIDE.md).

CharacterAction is a read-only projectile consumer. Its own gameplay event retains the Request reference, trigger frame and event identity/kind; its summary reads saved source/generated through the read-only store, and its preview uses the last formally imported catalogs, not another window's unsaved projection. It deep-links to the unique Projectile Authoring writer. CharacterAction Save/Apply/session recovery does not write projectile source, generated catalogs, binary artifacts or Player mirrors; consumer mechanic tuning remains with its existing owner. See [consumer boundaries](Guides/PROJECTILE_AUTHORING_GUIDE.md#consumer-与兼容快照).

### Buff v2 Package

```bash
mx-authoring buff validate --package <BuffPackage>
mx-authoring buff compile --package <BuffPackage> --out <dir>
mx-authoring buff import-unity --package <BuffPackage> --project-root <repoRoot> [--dry-run]
```

Used for component-native Buff definitions and caster-following Combat Aura range-refresh definitions. Unity import writes generated JSON under `Assets/MxFrameworkGenerated/Gameplay/Buffs/` and `Assets/MxFrameworkGenerated/Combat/Auras/`.

Buff v2 Workbench is available through EditorHub at `Tools/MxFramework.BuffWorkbench/web/`. It edits existing Buff/Aura entries and uses Validate as the review gate: field diagnostics and authoring diff are refreshed by the Authoring server, Save is enabled only for Ready non-stale drafts, Compile shows generated semantic diff without writing generated files, and Import writes generated runtime JSON only from the saved Ready package. The Authoring server also provides Buff editing metadata at `GET /api/authoring/buffs/options`, including Buff type options, attribute candidates, enum labels, stacking modes, description modes, layer-mask presets, `durationFrameRate=60`, `instantFrames=2`, and field descriptions used by the Workbench UI. Workbench does not edit Localization TSV; Validate warns when `nameKey` is blank or undeclared in available TSV data, while project localization sources remain owned by the existing Localization pipeline.

### Character Resource Package

```bash
mx-authoring character validate --package <CharacterPackage> --check-files
mx-authoring character compile --package <CharacterPackage> --out <dir> --check-files --check-hashes
mx-authoring character project-common --package <CharacterPackage> --source <candidate-character_actions.json> --out <staged-projection.json>
mx-authoring character import-unity --package <CharacterPackage> --project-root <repoRoot> --check-files --check-hashes --build-target StandaloneWindows64 [--dry-run]
```

Used for character manifest, package-local resources, geometry, colliders, sockets, character weapon slots, compiler diagnostics, and links to global single-weapon pose/trace authoring.

CharacterStudio owns action-slot selection, not action timeline authoring. Public action bodies are authored in `CharacterActionSource/Libraries/Common/character_actions.json`; character-owned bodies are authored in `<CharacterPackage>/config/character_actions.json`. Character Application 8.0 persists only `{ actionRole, actionId }` references plus structured command sequences. The compiler derives Profile `<package>.default` and ActionSet `<package>.<weaponCombination>`, validates Common/character library permissions and effective-ID uniqueness, then writes schema 0.3.0 projections under `Assets/MxFrameworkGenerated/CharacterActions`; generated files are never an editing authority.

The Unity Character Action Authoring window is the sole writer for action body timelines, hit tracks, motion and presentation. A CharacterStudio open request identifies Profile, ActionSet, source Library and Action ID, includes read/write mode and the current source SHA-256, and publishes open/save/failure/timeout status back to CharacterStudio. Common actions open read-only in a character context. “Copy as character-specific and edit” first performs an atomic source copy plus slot-reference update; Unity rejects a save if the source hash changed on disk.

### Animation Package

```bash
mx-authoring animation compile --package <CharacterPackage> --out <dir>
```

Used for animation set / group / clip mapping and runtime binding outputs.

### VFX Authoring Catalog

The project-level semantic source is `Assets/Config/MxFramework/Vfx/vfx_authoring_catalog.json` using `mx.vfx.authoring.v2`; v1 remains a read-compatible migration input. `Assets/Config/MxFramework/Vfx/vfx_generation_templates.json` independently owns Provider, Style Profile, certified generation template, experimental adapter version and quality Profile data. Neither file is Runtime authority.

The precise property editing contract is documented in `VFX_SEMANTIC_PROPERTY_RETIMING_DESIGN.md`. The Current ParticleSystem v3 workbench uses content-addressed Source Snapshot + Component/Element/Shader Profile + a 95-property Registry to compile sparse `mx.vfx.composition-draft.v3` overrides into preview and Candidate output. Registry entries are vocabulary contracts, not unconditional writable coverage: a write is available only when a Profile Binding and Adapter exist and the compiler's module-specific safety checks pass. AutoSuggested profiles may be used for Candidate bake because the explicit bake action accepts the current result and freezes the resolved Profile hashes, field diff and source evidence in the receipt. Publishing those suggestions as repository-wide shared Profiles is a separate optional action with impact analysis; only persisted Profiles require both Manifest review state and Profile status to be Confirmed. “Copy profile gap report” emits deterministic `mx.vfx.profile-gap-report.v1` evidence for Development Agent/manual curation and separates actual compiler blockers from optional shared-Profile gaps and unused capability limitations; it performs no approval write. The workbench includes exact curve/gradient modes, Layer/member retiming, member participation, guarded Draft/Profile writes, recovery/three-way remap, Renderer/Material/Custom Data adapters and Preview/Bake/source-integrity receipts. Trails now has a structured snapshot and a confirmed `PreserveNormalizedLifetime` ExtendTail slice that preserves normalized lifetime, width and color while the main particle lifetime changes; general Trail properties, SubEmitter/Collision, unsafe shader bindings, pixel-level visual Golden approval and VFX Graph remain fail-closed Design boundaries.

创作实验室顶部的 `+ 添加素材` 是与“手动组装”并列的全屏工作区；手动组装工具栏中的同名按钮只是进入该工作区的快捷入口。左侧按真实目录树虚拟化浏览 Piloto Source Index 中已有的 Prefab；选中 Prefab 后，成员树默认全部不选，用户可全选、清空、选择层级分支，或只保留当前完整 SubEmitter 依赖组。父节点显示三态选择，任何 SubEmitter 连通组都按整体选择或取消，空选择、外部引用、过期索引和不稳定成员身份继续 fail-closed。

中间同时显示“完整源效果”和“最终选中子集”两个隔离预览，两侧共享固定 seed、播放、逐帧、时间轴、速度、循环、背景和镜头；子集为空时明确显示空状态。`创建全局素材` 将当前有序成员集合写入 Editor-only 手工派生素材库，同一 Prefab 可产生多个不同组合，相同组合由 `Prefab GUID + 有序成员 ID 集合 SHA-256` 定位而不会重复创建；`创建并加入当前阶段` 才额外修改当前草稿 Layer。全局素材可重命名，删除前会扫描全部草稿并阻止删除仍被 Layer 引用的素材。`ExplicitAuthored` 模式禁用自动精选，素材栏只显示用户明确创建的全局素材、当前草稿本地素材，以及当前草稿实际引用的全局素材；该流程始终不修改 `Assets/Piloto Studio`、Source Index schema、草稿 schema 或运行时接口。

手工派生全局素材使用 `Assets/Config/MxFramework/Vfx/AuthoredElements/manifest.json` 作为提交点，每个 Element 保存为 `AuthoredElements/Elements/<id-hash-prefix>/<id-hash>.<content-hash>.json` 独立分片。分片为不可变、内容寻址文件，新增或更新时先写分片再原子切换 manifest；原 `vfx_authored_elements.json` 仅作为首次迁移输入和兼容备份保留，manifest 存在后不再读取或改写旧单文件。机器本地 Source Index 缺失时仍从这些分片显示素材卡，但成员预览、语义编辑、源导入、加入 Layer 与烘焙保持禁用，直到显式重建索引。

完整 Compound Element 可以保留同一源 Prefab 内的 SubEmitter 网络；实例化与 Candidate 生成均先建立全部 ParticleSystem，再按稳定成员身份恢复目标、顺序、触发类型和继承属性并反向验证。外部引用、缺失目标或不属于当前 Compound 的目标仍然阻止导入。

The current Catalog has `strictCoverage=true` and covers all 37 direct CharacterTest/Samples effects; all entries remain `ReviewNeeded` until explicit human artistic approval. Validation covers schema/taxonomy, stable ids, Primitive/Recipe references and cycles, parameter binding types/ranges, Recipe/Primitive backend and Anchor compatibility, Anchor/lifecycle, ResourceKey/Definition evidence, preview/budget fields, Graph fallback, stale published effects, coverage and Ready gate. A stale bake is blocking for `Ready`, and human-facing `Ready` approval is never inferred automatically.

VFX 制作与能力引用是两个独立工作流。VFX 创作工作台只负责 Composition Draft、独立预览、Candidate 烘焙、艺术评审和 Published VFX 注册；它不扫描、选择或修改 CharacterAction、Projectile、技能槽位和 gameplay 配置。每个可引用的 Published VFX 获得稳定 `ResourceKey`，其 Prefab 投影由 Published Binding Manifest 管理。

角色动作配置编辑器负责消费侧绑定：用户在 `SpawnVisualCue` 等表现事件中通过可视化资源卡选择稳定 `ResourceKey`，并在动作时间轴的隔离预览中直接看到当前解析到的 Prefab。修改 VFX 的 Prefab 绑定不会改写动作配置；修改动作引用也不会回写 VFX Draft、Profile 或 Catalog。技能合同检查、真实场景试玩和成套发布若仍需要，应作为独立发布/验收工具运行，不得重新嵌入通用 VFX 创作工作台。

The noEngine CLI can validate the project evidence and write deterministic JSON/Markdown reports, or produce a stale-hash guarded save plan without writing Unity assets:

```text
mx-authoring vfx validate --project-root <repoRoot> [--catalog <catalog.json>] [--json-report <report.json>] [--markdown-report <report.md>] [--fail-on-warning]
mx-authoring vfx propose-coverage --project-root <repoRoot> --output <catalog.json> [--enable-strict]
mx-authoring vfx search --project-root <repoRoot> [--text <query>] [--role <role>] [--theme <theme>] [--anchor <anchor>] [--backend <backend>] [--review <status>] [--limit <n>]
mx-authoring vfx impact --project-root <repoRoot> --id <effect|recipe|primitive id>
mx-authoring vfx plan-save --project-root <repoRoot> --proposed <catalog.json> [--catalog <current.json>] [--expected-hash <sha256:...>]
mx-authoring vfx plan-bake --project-root <repoRoot> --effect <effectId> [--catalog <catalog.json>]
mx-authoring vfx plan-create --project-root <repoRoot> --brief <brief.json> [--templates <templates.json>] [--output <plan.json>]
mx-authoring vfx source scan --project-root <repoRoot> [--index <Temp/.../index.json>] [--output <report.json>]
mx-authoring vfx source search --project-root <repoRoot> [--role <role>] [--theme <theme>] [--anchor <anchor>] [--shape <shape>] [--quality <tier>] [--max-particles <n>]
mx-authoring vfx source seal-registry --project-root <repoRoot> [--registry <registry.json>]
mx-authoring vfx source validate-registry --project-root <repoRoot> [--index <index.json>] [--registry <registry.json>]
mx-authoring vfx source plan-extract --project-root <repoRoot> --request <request.json> [--index <index.json>] [--registry <registry.json>] [--output <plan.json>]
mx-authoring vfx source validate-extraction --project-root <repoRoot> --plan <plan.json> --receipt <receipt.json> [--index <index.json>] [--output <report.json>]
mx-authoring vfx source plan-compose --recipe <id> --name <name> --output-prefab <Assets/...prefab> --expected-hash <sha256:...> --primitives <id,id,...> [--output <plan.json>]
mx-authoring vfx validate-receipt --project-root <repoRoot> --plan <plan.json> --receipt <receipt.json> [--templates <templates.json>] [--skip-file-check] [--output <report.json>]
mx-authoring vfx evaluate-candidates --project-root <repoRoot> --plan <plan.json> --receipt <receipt.json> --input <evaluation-input.json> [--templates <templates.json>] [--output <report.json>]
mx-authoring vfx plan-promote --project-root <repoRoot> --plan <generation-plan.json> --receipt <candidate-receipt.json> --evaluation <evaluation-report.json> --request <promotion-request.json> [--templates <templates.json>] [--output <plan.json>]
mx-authoring vfx plan-register-published --project-root <repoRoot> --request <registration-request.json> --promotion-plan <promotion-plan.json> --promotion-receipt <promotion-receipt.json> [--output <plan.json>]
```

The scanner reads Prefab/VFX Graph paths, Resource Catalog evidence, generated VFX Definitions, derived bake/thumbnail evidence and package availability. It emits stable component fingerprints, structural reuse candidates, exact preview-hash groups, 64-bit dHash perceptual candidate groups, coverage/Recipe/Primitive/duplicate rates and a static component-concurrency budget gate. `propose-coverage` creates deterministic `LegacyPlaceholder + ReviewNeeded` candidates and can propose strict mode; it never approves art. `search` exposes Effect, Recipe and Primitive matches but only recommends `Production + Ready` entries for ordinary visual reuse; `impact` exposes semantic, fallback and runtime bindings before mutation. The Piloto Source Library deep scanner indexes actual AssetDatabase Prefabs and ParticleSystems using package + Prefab GUID + component local fileID, while `vfx source` commands only search, validate and plan. The current project selects `DraftLocalOnly`: the workbench shows only the complete Compound Element embedded in the open draft and keeps its ParticleSystem members in the detail editor. Automatic curation and full-source palette views remain framework capabilities but are disabled by this project configuration; the complete source index stays rebuildable and `Assets/Piloto Studio` remains unchanged. `mx.vfx.composition-draft.v3` stores an ordered five-phase `Build/Rebuild/Loop/Complete/Dissipate` Layer Stack, integer frame scheduling, timing segments, sparse semantic overrides and exact Profile dependencies. `Loop` is mandatory and chooses `OneShot` or `Continuous`; other empty phases mean no extra visual. Preview instances remain isolated and are recompiled from source snapshots; explicit baking resolves or extracts only used Elements through Unity Editor API, directly references unchanged support assets, and writes a complete Candidate Prefab with five phase roots, `UnityVfxLifecycleController`, one Layer Scheduler and optional MaterialPropertyBlock tracks only below the ignored manual-candidate root. Apply runs authoritative validation/dry-run/hash guards; it never hand-writes Prefab, Graph, material or texture YAML. Current Catalog is strict at 37/37 semantic coverage and 0/37 Production coverage.

The creative flywheel is deliberately staged. `plan-create` first searches Ready exemplars and existing Effect/Recipe/Primitive entries, then emits the six Round 0 directions. Optional Local CLI/HTTP capability grants must be supplied from a repository-external file; no credential or machine endpoint is stored in the repository. The current Unity executor implements only built-in procedural support assets plus whitelisted ParticleSystem/LineRenderer candidates. Graph, Local CLI/HTTP and Cloud generation remain fail-closed. The executor writes only to the Git-ignored `Assets/VFX/MxCandidates/<sessionId>/` sandbox and returns immutable asset/provenance receipts. Receipt validation, candidate evaluation, promotion planning and published registration planning are separate commands so Catalog, Resource Catalog and VfxDefinition changes cannot be smuggled into one write.

Promotion requires a current Catalog/template hash, a valid generation receipt, a complete evaluation report, explicit human selection, and a versioned selection-authority receipt verified by a trusted host integration. `selectedBy`, reviewer names, and arbitrary hashes are audit claims, not authorization. Because no authenticated approval-authority integration is currently configured, standalone CLI `plan-promote` and Catalog Ready validation deliberately fail closed; an approved host must first verify an external content-addressed receipt and inject the matching verifier. `VfxCreativeQualityEvidenceRunner` produces the full seed/view/background/time-slice/loop evidence matrix and machine gates; its default performance provider deliberately returns `NotMeasured`, so a versioned target-platform CPU/GPU/GC provider must be injected before promotion can pass. Unity promotion uses a GUID/hash guarded `AssetDatabase.MoveAsset` transaction with rollback/recovery; the Core/CLI commands only emit plans. Automatic processing may produce `Draft` or a proposed `ReviewNeeded` transition, never `Ready`. Ready additionally requires immutable approval evidence and all transitive Recipe/Primitive/fallback dependencies to be Ready. Scanner accepts evaluation evidence only when its exact candidate is both hard-gate-passed and shortlisted and the report is bound to the same target through a valid PromotionPlan/PromotionReceipt chain.

### UI Art Resource Library

UI Art Resource Library tooling is available through EditorHub at `Tools/MxFramework.FairyGuiArtSlices/web/?library=MxUiFoundation`. It edits the independent hand-curated resource catalog at `ArtSource/UIKits/MxUiFoundation/ui-art-resources.json`, previews source PNG files through read-only project-root `ArtSource/` static routing, validates replacement constraints, records recommended FairyGUI usage for future package authoring, and can add a new PNG-backed resource through a plan/create flow.

The desktop editor uses a fixed three-column workbench: browse resources on the left, inspect the PNG and draggable NineSlice boundaries in the center, and edit grouped semantic fields on the right. New-resource and PNG-replacement operations open as file-first drawers; preflight and diagnostics stay collapsed until needed.

The FairyGUI inspector also provides a read-only integration handoff. Select a package.xml-backed target package, generate the plan, then copy or download its deterministic JSON/Markdown recipe. The plan reports suggested import paths, existing resource ids, PNG content drift, sidecar usage drift, and publish requirements; it never writes FairyGUI files.

`MxCharacterTestHud` package scanning remains available as a FairyGUI usage audit / drift-check path, but it is no longer the default source for new UI cut-up resources.

The Authoring server endpoints are:

- `GET /api/authoring/ui-art/resources/capabilities`
- `GET /api/authoring/ui-art/resources/libraries`
- `GET /api/authoring/ui-art/resources/state?library=...`
- `GET /api/authoring/ui-art/resources/fairygui-packages`
- `POST /api/authoring/ui-art/resources/plan-fairygui-import`
- `POST /api/authoring/ui-art/resources/save`
- `POST /api/authoring/ui-art/resources/plan-replace`
- `POST /api/authoring/ui-art/resources/replace-png`
- `POST /api/authoring/ui-art/resources/plan-create`
- `POST /api/authoring/ui-art/resources/create`
- `POST /api/authoring/ui-art/resources/plan-draft`

Legacy FairyGUI package audit endpoints are still available under `/api/authoring/fairygui/art-slices/*`.

All writes require a dry-run plan and catalog hash check. `save` validates the complete proposed catalog, preserves resource ids/paths/inventory, and writes only `ui-art-resources.json`; `replace-png` replaces only an existing resource-library PNG; `create` writes one new PNG under the resource library plus one catalog entry. Catalog and PNG roots are restricted to repository-relative paths under `ArtSource/UIKits`. The web tool checks the Authoring capability version before loading and exposes explicit alpha, sizing, stretch, and NineSlice creation parameters. It does not write FairyGUI `package.xml`, component XML, atlas files, or `*_fui.bytes`; FairyGUI Editor remains the authority for layout and package publishing.

### Semantic UI Composer

UI Composer is available at `Tools/MxFramework.FairyGuiUiComposer/web/` and defaults to the CharacterTest main menu. It combines the independent UI resource catalog, reusable component recipes, generated read-only binding contracts, and constrained screen blueprints to author allowlisted screens. `MxCharacterTestUi/MainMenuPanel`, `MxCharacterTestCombatHud/CombatHudPanel`, `MxCharacterTestSettingsUi/SettingsPanel`, `MxCharacterTestNetworkUi/NetworkPanel`, `MxCharacterTestAbilityUi/AbilityBindingPanel`, `MxCharacterTestRuntimeAiLabUi/RuntimeAiPlannerDevPanel`, and `MxCharacterTestRuntimeAiLabRecoveryUi/RuntimeAiLabRecoverySurface` are published runtime targets; `MxUiFoundationDemo/UiFoundationDemoPanel` remains the regression sample. Runtime AI Lab uses one dedicated package for its unified behavior-group workbench and formal five-track timeline plus an independently preflighted recovery package for transition/fail-safe presentation. Its former persistent launcher is no longer registered; F9 is the single reopen entry, while other unmigrated surfaces may still use legacy `MxCharacterTestHud`. The workflow is Save Blueprint -> Validate -> Plan Apply -> Apply through the FairyGUI helper -> optional Plan Sync/Sync -> Publish. Every save/apply/sync/publish request carries the current catalog, recipe, contract, blueprint, mapping, and source XML hashes.

Blueprint v2 supports Graph, controlled Text including input restrictions and maximum lengths, controlled semantic Image, ComponentInstance nodes, tree grouping, multi-selection, batch alignment/distribution/size operations, and authoring-only Free/Horizontal/Vertical/Grid containers. Blueprint v3 adds a controlled screen-level FairyGUI List with a component-recipe default item, Column/Row layout, selection, scrolling, gaps, and runtime virtualization intent. Apply flattens authoring containers into the existing FairyGUI display list, while List remains an actual `GList` node; binder names, node ids, and mappings stay stable. Existing v1/v2 files remain readable and upgrade only when a capability requiring a newer schema is added. The v5 Authoring API rejects older web clients that do not understand List nodes. Standard Button recipes are editable; controlled Composite recipes are catalog-owned, read-only in the web form, recursively validated, and deterministically materialized. The helper plugin is the only writer for allowlisted Composer-owned packages and publishes through the existing `PublishHandler` with `genCode=false`. The Combat HUD demonstrates a pure Image/Graph/Text screen with no component recipe instances and preserves 178 existing runtime binding slots; Settings, Network, and Ability Binding demonstrate compact semantic button recipes and controlled input fields. Runtime AI Lab demonstrates a Composer-owned debug/validation surface with reusable behavior-card, gameplay-editor, developer-flow, and formal timeline composites. Full authority, API, runtime preview, and validation rules are in `Docs/FAIRYGUI_UI_COMPOSER.md` and ADR-007.

The v4 unified workspace introduced same-screen resource, standard Button recipe, and node authoring. The v5 compatibility gate retains that workflow and prevents older clients from opening Blueprint v3 List data. Each layer keeps an independent draft and authority file; server-built reverse references support contextual navigation and safe deletion; unified preflight validates all layers and hashes before Apply/Publish.

## EditorHub

EditorHub now links only to active tools: Buff v2 Workbench, CharacterStudio, AnimationEditor, ResourceLibrary, UI Art Resource Library, UI Composer, and Action Damage Analysis. It no longer displays a legacy Buff Editor card or calls retired Buff authoring API endpoints.

## Acceptance

A new authoring workflow is acceptable only when:

- It has a current runtime consumer.
- It owns a current schema and validation path.
- It can be validated by CLI tests without Unity.
- Unity import or runtime binding is explicit and reviewable.
- It does not depend on retired Buff editor data, `BuffType`, or `DamageByAttr`.
