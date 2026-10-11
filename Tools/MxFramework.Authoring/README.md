# MxFramework Authoring

Standalone Authoring Core / CLI tooling for content pipelines that must work outside Unity compilation and runtime.

## Project Layout

```text
Tools/MxFramework.Authoring/
  src/MxFramework.Authoring.Core/    # .NET Standard 2.1 core DTOs and validators
  src/MxFramework.Authoring.Cli/     # authoring CLI and local editor service
  tests/MxFramework.Authoring.Tests/ # dependency-light authoring smoke tests
  samples/ability-character-test/    # CharacterTest ability package sample
  samples/buff-character-test/       # CharacterTest Buff v2 + Combat Aura sample
  samples/character-codex-flow-test/   # humanoid character resource package sample
  samples/character-slime/           # non-humanoid character resource package sample
```

## Retired Buff Tooling

The legacy external Buff Authoring Editor, Buff workflow/schema export, Buff preview client/server protocol, WGame `BuffType`, `DamageByAttr`, `applyBuff`, and old Buff patch samples are retired.

Current Buff v2 authoring is JSON + CLI, with Buff v2 Workbench as a vanilla Web management surface for existing `config/buff_authoring.json` packages. The Workbench uses Authoring server validation and semantic diff as the Save gate, and Compile previews generated runtime JSON diff without writing generated files. It also consumes `GET /api/authoring/buffs/options` for designer-facing labels, field help, safe enum choices, Buff type choices, known CharacterTest attributes, Aura layer presets, stacking modes, description modes, and `durationFrameRate=60` / `instantFrames=2` time editing metadata. Buff authoring now requires player-facing multi-type `buffs[].types`; generated presentation `polarity` is inferred from those types for runtime HUD compatibility. Buff presentation authoring includes localized `presentation.nameKey`, `displayName` fallback, short badge name, icon resource key diagnostics, and `descriptionMode` (`Auto` / `Manual`). Auto descriptions are generated from current Buff config for preview/generated presentation output and are not copied back into source authoring JSON. The package validates and compiles to runtime JSON consumed by `GameplayBuffDefinitionRegistry`, `GameplayBuffPresentationDefinitionRegistry`, and `CombatGameplayAuraBuffDefinitionRegistry`. There is no legacy Buff Web UI, preview protocol, or old data migration path.

## Supported Commands

```bash
dotnet restore Tools/MxFramework.Authoring/MxFramework.Authoring.sln
dotnet build Tools/MxFramework.Authoring/MxFramework.Authoring.sln --no-restore /nr:false -m:1 -v:minimal
dotnet run --no-build --project Tools/MxFramework.Authoring/tests/MxFramework.Authoring.Tests/MxFramework.Authoring.Tests.csproj

dotnet run --no-build --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- ability compile --package Tools/MxFramework.Authoring/samples/ability-character-test --out Temp/AbilityAuthoringSmoke
dotnet run --no-build --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- buff validate --package Tools/MxFramework.Authoring/samples/buff-character-test
dotnet run --no-build --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- buff compile --package Tools/MxFramework.Authoring/samples/buff-character-test --out Temp/BuffAuthoringSmoke
dotnet run --no-build --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- buff import-unity --package Tools/MxFramework.Authoring/samples/buff-character-test --project-root .
dotnet run --no-build --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- character inspect --package Tools/MxFramework.Authoring/samples/character-codex-flow-test
dotnet run --no-build --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- character validate --package Tools/MxFramework.Authoring/samples/character-codex-flow-test --check-files
dotnet run --no-build --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- character import-unity --package Tools/MxFramework.Authoring/samples/character-codex-flow-test --project-root . --check-files --check-hashes --build-target StandaloneWindows64
dotnet run --no-build --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- character import-unity --package Authoring/CharacterPackages/character-000008 --project-root . --build-target StandaloneWindows64 --check-files --diagnostics-out Temp/CharacterPackageDiagnostics/000008 --migration-backup Temp/CharacterPackageExportMigration
dotnet run --no-build --project Tools/MxFramework.Authoring/tests/MxFramework.Authoring.Tests/MxFramework.Authoring.Tests.csproj -- character-export-layout
dotnet run --no-build --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- animation compile --package Tools/MxFramework.Authoring/samples/character-codex-flow-test --out Temp/AnimationAuthoringSmoke
dotnet run --no-build --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- editor serve --package Tools/MxFramework.Authoring/samples/character-codex-flow-test
```

## Character Package Export Layout

`character import-unity` 的默认导出布局为 `exportLayoutVersion = 4`（方案见 `Docs/CHARACTER_PACKAGE_EXPORT_SIZE_OPTIMIZATION_PLAN.md`、`Docs/CHARACTER_ANIMATION_RESOURCE_PLAN_RUNTIME_EXPORT_TASK.md` 与 `Docs/CHARACTER_PREVIEW_GLB_AUTHORING_ONLY_TASK.md`）：

- **不再默认写入两份完整编译快照**（`package_cache/compile_result.json`、`package_cache/animation_compile_result.json`）。完整诊断改用显式外部导出。
- **改为写入一份有界摘要** `package_cache/compile_summary.json`（`mx.characterPackageCompileSummary.v1`，≤ 8 KiB，无时间戳、确定性字节）。摘要不是加载清单、不是发布门禁：Runtime 与 Readiness 都不得依赖它；内容相同不会重写。
- **三份配置镜像收敛到 `config/`**：`character_config_patch.json`、`geometry_binding.json`、`resource_catalog_mapping.json` 只写入 `config/`，`generated/` 下不再生成同名镜像；`generated/` 保留烘焙索引与 payload。
- **动画资源计划改为五字段运行投影** `mx.animationResourcePlan.v2`（`format` / `schemaVersion` / `packageId` / `stableId` / `planHash`，≤ 512 B）。完整 `mx.animationResourcePlan.v1`（含 runtimeResourceCatalog / characterResourcePlan / audioCueManifest / diagnostics）只保留在编译对象、制作 API 与 `--diagnostics-out` 快照中；`planHash` 是完整计划的语义哈希，**不能由五字段正文重建**，投影只原样复制。投影校验发生在写盘之前，失败以 `CHARPKG_ANIMATION_RUNTIME_PLAN_INVALID` 返回非成功并保留旧文件与 `.meta`。
- **专用预览 GLB 仅保留在制作侧**：`CharacterPackageResourceExportPolicy` 判定 `usage=previewMesh` **且**有效源格式为 GLB 的条目（`typeId` 不参与判定）。这类条目**保留在作者源 `resource_catalog.json`**，但从全部运行出口排除：`resource_catalog_mapping.json`、write plan、`unity_resource_catalog.json`、`runtime_resource_catalog.json`、`character_resource_plan.json`、`character_config_patch.json` 表现资源键、resolver verification plan。生成包不再复制该 GLB。**同一物理文件被正式条目同时声明时**，保留文件并报 `CHAR_PREVIEW_GLB_RUNTIME_REFERENCE`（ExportBlocked），不静默删引用、不自动换模型。
- **废弃产物自动迁移**：导入成功收口后，按白名单删除废弃快照、废弃镜像与**未被本次导入声明**的 `resources/`、`previews/` 孤立文件（含同名 `.meta`）。删除前先备份并做哈希前置校验；内容变化、包身份不匹配、路径越界或 reparse point 一律保留并报告。导入未完整成功时不执行迁移。
  - **延期保护**：被判定为专用预览 GLB 的路径不参与该自动删除，结果记为 `Deferred`；CLI 会额外打印 `exportCleanupPending=<n>`，明确声明"Unity 引用验证与定向删除待完成"。真正的删除由 Unity 侧在 Prefab/目录重建后完成。
- **Socket Bake 由 Unity 生成 v3 配对**：`generated/character_socket_bake_catalog.json` 仅为小索引，逐帧 Int64 数据保存在同目录 `.bytes`；旧完整 JSON 必须重新导入，无 Runtime 回退。正式接受使用 receipt v3，两条 Readiness 路径核对文件大小及索引/载荷 SHA-256。CLI 摘要布局仍为 4，不提前宣称 Unity Bake 已完成；详见 `Docs/CHARACTER_SOCKET_BAKE_BINARY_EXPORT_TASK.md` §11.5。
- **Player 暂存过滤**：`CharacterTestPackageTestCommand` 按完整相对路径排除两份废弃快照、摘要和三份废弃镜像；保留同名不同路径、导入报告及烘焙索引/payload。暂存目标由包目录与导入报告中一致的 `packageId` 确定，不同角色互不覆盖；回归测试使用独立暂存根，不清理正式 StreamingAssets。

### 同名文件在不同命令下的含义

| 命令 | `animation_resource_plan.json` 形态 |
| :--- | :--- |
| `character import-unity`（正式包默认导出） | **v2 五字段运行投影** |
| `animation compile --out <dir>`（显式编译导出） | **完整 v1** |
| `character import-unity --diagnostics-out <dir>` | 完整 v1，位于诊断目录的 `animation_compile_result.json` 内 |

### 兼容与回退顺序

- 当前 Reader 同时接受 v1 与 v2；**旧 Reader 只接受 v1**，因此不能把 v2 文件分发给未升级的 Player / Editor，发布时 Reader 与新内容必须成套更新。
- v2 为严格模式：缺字段、非字符串、空身份、非法 `planHash`、`schemaVersion` 非 `2.0`、以及任何额外顶层字段都会被拒绝（新增字段必须升级 wire version）。非法内容会让整包加载抛出 `CharacterImportedPackageJsonException`，不产生半成品对象；文件缺失则走既有诊断路径。
- 回退 producer 到旧输出而保留新 Reader 是可读的 v1；仍须重建 Prefab 内容哈希与 receipt。若回退 Reader，必须同时恢复完整 v1 内容及对应已验证 Prefab，不能只回退 C#。

新增参数：

| 参数 | 作用 |
| :--- | :--- |
| `--diagnostics-out <dir>` | 导出**同一次导入**的完整编译对象与动画对象、导入报告，以及标注阶段（`compiler` / `preparedAnimation`）的小型诊断 manifest。默认关闭，不在正式包内隐式创建诊断目录。目标不得位于 `Assets/`、`Packages/`、`Authoring/`、`ArtSource/`、`WeaponSource/`、`CharacterActionSource/` 之下，预检在写盘前完成。 |
| `--migration-backup <dir>` | 指定迁移备份目录。缺省为 `Temp/CharacterPackageExportMigration/<packageId>/<run>/`；因 `Temp/` 属可被清理的本地工作状态，报告会标注该路径为易失。 |

测量与验证：

```bash
python Tools/MxFramework.Authoring/scripts/measure_character_package_export.py Assets/MxFrameworkGenerated/CharacterPackages/000008
python Tools/MxFramework.Authoring/scripts/measure_character_package_export.py <包目录> --json
```

该脚本按类别输出 JSON / 非 JSON 字节、Player 暂存集合（过滤前与过滤后）、孤立资源候选与废弃路径残留，用于与优化前基线逐项对照。

## Current Scope

- Ability package compile and validation for current deterministic runtime data.
- Buff v2 package validate / compile / import for component-native Gameplay Buff definitions and Combat Aura range-refresh definitions.
- Buff v2 editor service APIs for package discovery, load, save, validate, compile preview, explicit generated import, semantic diff, designer-facing options metadata, badge metadata diagnostics, and auto description preview support.
- Character resource package inspect / validate / compile / explicit Unity import.
- Character Application 8.0 and Global Weapon Library 6.0 action-slot compilation: Common and character `character_actions.json` sources (schema 0.2.0), library-level animation source-rig declarations, compiler-derived Profile/ActionSet projections (schema 0.3.0), pure action references, structured command sequences, per-action Humanoid compatibility validation, source hash invalidation, and atomic Common-to-character copy.
- CharacterStudio grouped action catalog plus observable Unity open/save/conflict status; Profile and ActionSet IDs are compiler-owned and cannot be typed manually.
- Animation mapping compile.
- Local authoring service endpoints used by EditorHub, Buff v2 Workbench, CharacterStudio, AnimationEditor, ResourceLibrary, and Action Damage Analysis.
- Mod/package diagnostics for still-supported generic runtime config patch packages.

Not included: legacy external Buff editor, legacy Buff Web UI, Buff preview runtime, Buff patch report bundle, old workflow export, or runtime Buff data migration.
