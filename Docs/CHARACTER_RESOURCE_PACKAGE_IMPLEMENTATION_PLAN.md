# Character Resource Package 实现方案

> Status: Draft
> Design: `Docs/CHARACTER_RESOURCE_PACKAGE_AUTHORING.md`
> Scope: 工程目录、模块拆分、核心 DTO、CLI、外部 3D 编辑器、Unity Importer、Runtime Spawn 接入和验证计划

## 目标

把角色资源包方案落成可开发的工程计划：

```text
Character Resource Package
  -> Authoring Core / CLI
  -> Tauri + React + Three.js 外部编辑器
  -> Authoring Compiler
  -> Unity Importer Bridge
  -> Runtime Spawn
```

本实现方案的重点是固定“代码放哪、谁依赖谁、每阶段产出什么、怎么测试”。视觉设计和产品逻辑见 `Docs/CHARACTER_RESOURCE_PACKAGE_AUTHORING.md`。

## 总体技术选型

第一版采用：

| 层 | 技术 | 说明 |
| --- | --- | --- |
| 外部桌面壳 | Tauri | 负责窗口、文件选择、本地命令、跨平台打包。 |
| UI | React + TypeScript | 资源树、Inspector、诊断栏、状态管理。 |
| 3D 视口 | Three.js / React Three Fiber | glTF/GLB 加载、camera、gizmo、collider/trace 可视化。 |
| Authoring Core | C# `.NET Standard 2.1` | package DTO、schema、validation、compiler 纯逻辑。 |
| CLI | C# `net9.0` | 给外部编辑器、CI、Development Agent 调用。 |
| Unity Importer | Unity Editor assembly | 只负责导入、AssetDatabase 写入、ResourceCatalog 映射和报告。 |
| Runtime | 现有 MxFramework Runtime / Gameplay / Combat / Resources / CharacterControl | 消费导入后的配置和 binding。 |

不使用 Unity Editor 作为主创工具，不使用 Blender 插件作为主入口。

## 依赖方向

```text
Tools/MxFramework.CharacterStudio
  -> MxFramework.Authoring.Cli
  -> MxFramework.Authoring.Core

Unity Editor Importer
  -> MxFramework.Authoring.Core
  -> MxFramework.Character.Application
  -> MxFramework.Config / Resources

Runtime Spawn
  -> MxFramework.Character.Application
  -> Runtime / Gameplay / Combat / Resources / CharacterControl
```

硬性规则：

- `MxFramework.Authoring.Core` 不引用 `UnityEngine`、`UnityEditor`、Unity 项目程序集或 WGame 业务程序集。
- `MxFramework.Character.Application` 继续保持 noEngine。
- Unity Importer 可以引用 `UnityEditor`，但只能在 Editor assembly 中。
- 外部编辑器不实现 resolver / compiler 私有逻辑，只调用 Authoring Core / CLI。
- Runtime 不直接读取未导入的 `.mxchar` 包；Runtime 消费 Unity Importer 生成的配置、ResourceCatalog 映射和 binding。

## 推荐目录

基于现有 `Tools/MxFramework.Authoring` 结构，第一版建议这样落地：

```text
Tools/MxFramework.Authoring/
  src/
    MxFramework.Authoring.Core/
      CharacterPackages/
        CharacterPackageManifest.cs
        CharacterPackageResourceCatalog.cs
        CharacterAuthoringGeometry.cs
        CharacterPackageValidation.cs
        CharacterPackageHash.cs
        CharacterAuthoringCompiler.cs
    MxFramework.Authoring.Cli/
      CharacterPackageCommands.cs
  tests/
    MxFramework.Authoring.Tests/
      CharacterPackageTests.cs
      CharacterAuthoringCompilerTests.cs
  samples/
    character-iron-vanguard/
      manifest.json
      resource_catalog.json
      resources/
      config/
      geometry/
      validation/

Tools/MxFramework.CharacterStudio/
  package.json
  src-tauri/
  src/
    app/
    package/
    viewport/
    inspector/
    diagnostics/
    unity-import/

Assets/Scripts/MxFramework/
  Character.Application/
    existing config and resolver contracts
  Editor/
    CharacterImport/
      CharacterPackageImporterWindow.cs
      CharacterPackageImportCommand.cs
      CharacterPackageUnityWriter.cs
  Tests/
    CharacterApplication/
    CharacterImport/
    RuntimeSpawn/
```

如果 C0/C0.5 的 DTO 体量明显变大，可以把 `CharacterPackages/` 独立成 `MxFramework.Authoring.Character` 项目；第一版先放进现有 `MxFramework.Authoring.Core`，减少工程复杂度。

## 实施阶段

### C0：角色资源包与 3D Authoring 契约

对应 Issue：#221

目标：让一个角色资源包能被纯 C# 读取、校验、序列化 roundtrip。

当前 C0 落点：

```text
Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/CharacterPackages/
Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/CharacterPackageCommands.cs
Tools/MxFramework.Authoring/tests/MxFramework.Authoring.Tests/CharacterPackageTests.cs
Tools/MxFramework.Authoring/samples/character-iron-vanguard/
Tools/MxFramework.Authoring/samples/character-slime/
```

新增核心类型：

| 类型 | 职责 |
| --- | --- |
| `CharacterPackageManifest` | 包身份、版本、schema、坐标系、依赖、hash。 |
| `CharacterPackageCoordinateConvention` | up/forward、unit scale、handedness、rotation authority。 |
| `CharacterPackageResourceCatalog` | 包内 ResourceKey、relative path、type、variant、hash、import hints。 |
| `CharacterBodyGeometryAuthoring` | 身高、半径、默认 capsule、模型 root、skeleton root。 |
| `CharacterBodyColliderAuthoring` | capsule / box / sphere collider、partId、hitZoneId、local pose。 |
| `CharacterSocketAuthoring` | socketId、parent part、bone path、locator path、local pose、usage。 |
| `WeaponAttachmentAuthoring` | weapon id、equip slot、attach socket、grip pose、preview key。 |
| `WeaponTraceAuthoring` | start/end/radius/sample rule。 |
| `CharacterAuthoringValidationIssue` | stable code、severity、gate、source path、field、suggested fix。 |

CLI 命令：

```bash
mx-authoring character inspect --package Tools/MxFramework.Authoring/samples/character-iron-vanguard
mx-authoring character validate --package Tools/MxFramework.Authoring/samples/character-iron-vanguard
mx-authoring character schema
```

验收：

- Iron Vanguard 样例包可读取。
- JSON roundtrip 后 stable id、ResourceKey、local pose、collider size 不丢失。
- unsupported shape，例如 convex/custom mesh，输出稳定 warning 或 error。
- Core 不依赖 Unity API。

C0 不做资源文件存在性、hash 计算、依赖图、import/write plan 或 generated config patch，这些分别属于 #223 和 #224。

### C0.5：包内资源管线

对应 Issue：#223

目标：固定包内资源身份、hash、依赖图、import hints 和 Unity mapping 输入。

新增核心类型：

| 类型 | 职责 |
| --- | --- |
| `CharacterPackageResourceEntry` | 单个资源条目，包含 package-local `ResourceKey`、`localId`、`stableId`、type、usage、source format、relative path、tags。 |
| `CharacterPackageResourceHashes` | `contentHash`、`importHash`、`dependencyHash`，默认算法为 `sha256`。 |
| `CharacterPackageImportHint` | Unity target path policy、target relative path、scale、material policy、animation policy、axis hint、collision/physics data policy。 |
| `CharacterPackageResourceDependency` | 包内资源依赖边，记录 target ResourceKey、required、relation 和是否影响 dependency hash。 |
| `CharacterPackageDependencyGraph` | 从 catalog 派生的 nodes / edges 资源依赖图。 |
| `CharacterPackageResourceHashReport` | 文件存在性、声明 content hash、计算 content/import/dependency hash 和 diagnostics。 |
| `CharacterPackageConflictPolicy` | same stable id、hash unchanged、hash changed 时的 skip/report/upgrade/variant 策略。 |
| `CharacterPackageResourceProvenance` | source tool、source file、authoring schema version、license、origin、created/modified metadata。 |
| `CharacterPackagePreviewMetadata` | thumbnail、preview mesh、placeholder 和 camera preset 元数据。 |
| `CharacterPackageResourceKeyGenerator` | package-local `ResourceKey` 稳定生成和语法校验。 |

CLI 命令：

```bash
mx-authoring character resources --package Tools/MxFramework.Authoring/samples/character-iron-vanguard
mx-authoring character hash --package Tools/MxFramework.Authoring/samples/character-iron-vanguard
mx-authoring character validate --package Tools/MxFramework.Authoring/samples/character-iron-vanguard --check-files --check-hashes
```

验收：

- package-local `ResourceKey` 生成稳定。
- missing file、duplicate ResourceKey / stableId、missing dependency、duplicate dependency、self dependency、hash mismatch、unsupported resource format 都有稳定 diagnostics。
- import hints 不写死 Unity 绝对路径，使用 project-relative target policy。
- 包内资源不要求已经存在于 Unity 项目。
- v1 source format：model / animation 首选 glTF / GLB；FBX 记录为 future / optional warning。
- Iron Vanguard 和 Slime 样例包带 package-local catalog、placeholder 资源文件和可校验 content hash。

注意：C0.5 只固定资源管线契约和 noEngine 校验，不确认 Unity 6 Editor 是否内置 glTF / GLB 导入。C0.6 只生成确定性的 import/write plan，不执行 Unity 导入；#222 必须明确采用 importer package、格式转换或 placeholder 策略。

### C0.6：Authoring Compiler

对应 Issue：#224

目标：把角色资源包编译为 runtime config patch、geometry binding、resource mapping 和 Unity import/write plan。

新增核心类型：

| 类型 | 职责 |
| --- | --- |
| `CharacterAuthoringCompileRequest` | `CharacterResourcePackage`、package root、可选 existing config source index、可选 project ResourceCatalog summary、compile options。输入是角色资源包本身，不是 Unity Project Authoring Pack。 |
| `CharacterAuthoringCompileOptions` | strict、allow warnings、resource file/hash 校验、target output format、Unity generated root、target path policy、target coordinate convention。 |
| `CharacterAuthoringCompileResult` | compiler 总结果，包含 config patch、geometry binding、resource mapping、write plan、dependency graph、hash report、gate report、resolver verification plan 和 source mapping。 |
| `CharacterCompilerGateReport` | `ExportBlocked` / `ImportBlocked` / `SpawnBlocked` / `WarningOnly` 聚合状态和结构化 diagnostics。 |
| `CharacterAuthoringCompiledConfigPatch` | 生成的 Character Application patch bundle，表名对齐 12 张 Character Application 表，字段仍是配置初始值和引用，不保存运行时当前值。 |
| `CharacterAuthoringGeometryBinding` | body collider、hit zone、socket、weapon attachment、trace 和 coordinate conversion plan。 |
| `CharacterPackageResourceMapping` | package-local `ResourceKey` 到 Unity project ResourceCatalog/import target 的映射。 |
| `CharacterUnityImportWritePlan` | Unity importer 要执行的资源导入和配置写入计划。 |
| `CharacterResolverVerificationPlan` | 声明导入后应交给 `CharacterPackageResolver.Resolve` 的表集合、默认 loadout、预期 active equipment state、combat action set、animation profile、known ability ids 和 required resources。 |
| `CharacterPackageSourceMapping` | 包内路径到生成配置字段和 Unity target 的映射。 |

CLI 命令：

```bash
mx-authoring character compile \
  --package Tools/MxFramework.Authoring/samples/character-iron-vanguard \
  --out Temp/MxFrameworkAuthoring/character-iron-vanguard \
  --check-files \
  --check-hashes
```

`--out` 目录固定输出：

| 文件 | 说明 |
| --- | --- |
| `compile_result.json` | 完整 compiler result。 |
| `generated_config_patch.json` | 生成的 Character Application config patch bundle。 |
| `geometry_binding.json` | 几何、部位、碰撞体、socket、trace binding。 |
| `resource_mapping.json` | package-local `ResourceKey` 到 Unity import target 的映射。 |
| `unity_import_write_plan.json` | Unity Importer Bridge 后续执行的写入计划。 |
| `gate_report.txt` | 面向人阅读的 gate diagnostics 摘要。 |

验收：

- Iron Vanguard 能编译出 config patch、geometry binding、resource mapping、write plan、gate report、resolver verification plan。
- compiler 输出可以被 Unity Importer 后续适配为完整 `CharacterPackageResolver.Resolve` 输入；v1 不要求 incremental diff resolver API。
- `ExportBlocked` 时外部编辑器只能保存 draft，不能保存为可导入产物。
- `ImportBlocked` 时 `UnityImportWritePlan.CanWriteToUnityProject=false`，Unity Importer 不得写项目资源或配置。
- `SpawnBlocked` 时可以生成导入计划，但 `CanSpawnAfterImport=false`，Runtime Spawn 不得生成实例。
- `WarningOnly` 时允许继续，但必须保留稳定 issue code 和 source mapping。
- `sourcePackageHash`、`generatedConfigHash`、`geometryBindingHash`、`resourceMappingHash`、`writePlanHash` 对同一输入稳定。
- v1 只支持 capsule / box / sphere collider；convex/custom mesh 触发 `ExportBlocked`。
- missing socket 触发 `SpawnBlocked`，missing resource / hash mismatch / ResourceKey conflict 触发 `ImportBlocked`，coordinate mismatch 触发 `WarningOnly` 并生成 conversion plan。
- 外部编辑器和 Unity Importer 都只能消费这个 compile result，不各自写转换逻辑。

C0.6 仍不做 Unity Editor 写入和 glTF/GLB 实际导入验证。它只输出确定性的 import/write plan；是否使用 Unity glTF importer package、格式转换器或 placeholder 策略属于 C2 Unity Importer Bridge。

## 外部 3D 编辑器实现

对应 Issue：#217

### 应用结构

```text
Tools/MxFramework.CharacterStudio/src/
  app/
    App.tsx
    layout.ts
    commandBridge.ts
  package/
    packageStore.ts
    packageTree.ts
    packageSchema.ts
  viewport/
    CharacterViewport.tsx
    gltfLoader.ts
    sceneLayers.ts
    colliderMeshes.ts
    socketGizmos.ts
    traceGizmos.ts
    transformBinding.ts
    picking.ts
  inspector/
    InspectorPanel.tsx
    ColliderInspector.tsx
    SocketInspector.tsx
    WeaponAttachmentInspector.tsx
    ResourceInspector.tsx
  diagnostics/
    ValidationPanel.tsx
    GateSummary.tsx
    DiagnosticLocator.ts
  unity-import/
    UnityTargetSettings.tsx
    ImportResultPanel.tsx
```

### UI 分层

| 区域 | 第一版职责 |
| --- | --- |
| 顶部工具栏 | 打开包、保存、预检、导入 Unity、显示 package 状态。 |
| 左侧资源树 | 浏览模型、贴图、动画、config、geometry、colliders、sockets、traces。 |
| 中央 3D 视口 | 显示 glTF 模型、collider、socket、weapon attachment、trace。 |
| 右侧 Inspector | 编辑当前选中对象字段。 |
| 底部诊断栏 | 显示 resolver / compiler / import gate 报告。 |

### 3D 视口实现要点

- 使用 glTF/GLB 作为第一版模型格式。
- 每个 authoring object 都有 `objectPath`，例如 `geometry/colliders/head_01`。
- 视口 object 的 `userData.objectPath` 必须和 package source path 对齐，便于 diagnostics 定位。
- collider 显示为半透明 mesh：
  - sphere -> `SphereGeometry`
  - box -> `BoxGeometry`
  - capsule -> capsule helper mesh 或自定义 capsule mesh
- socket 显示为坐标轴 gizmo，支持 translate / rotate。
- trace 显示为 start/end handle、半径线和半透明扫掠带。
- TransformControls 只改 authoring local pose，不直接改模型骨骼。
- 所有修改先进入 editor draft state，保存时写回 package JSON。

### 命令桥

外部编辑器不直接引用 C# DLL，第一版通过 CLI 调用：

```text
Open Package
  -> read manifest/resource_catalog/geometry json

Validate
  -> mx-authoring character validate --package <path>

Compile Preview
  -> mx-authoring character compile --package <path> --out <temp>

Import Unity
  -> unity importer command --package <path> --project <unityProject>
```

后续如果 CLI 启动开销明显，再把 Core 包成常驻本地服务；第一版先用 CLI，方便 CI 和 Agent 复用。

### 普通模式与开发者模式

普通模式只显示用户完成装配必须知道的信息：

- 模型是否存在。
- 碰撞体是否映射到部位。
- 武器是否挂到 socket。
- trace 是否完整。
- 预检是否通过。

开发者模式显示：

- StableId / typed id / ResourceKey。
- hash / source mapping。
- resolver diagnostics。
- generated config patch 预览。
- Unity target import path。

## Unity Importer Bridge 实现

对应 Issue：#222

### Unity 入口

第一版提供两个入口：

```text
MxFramework > Character > Import Character Package...
MxFramework > Character > Reimport Last Character Package
```

同时提供 batchmode command，便于外部编辑器调用：

```bash
Unity -batchmode -projectPath <project> \
  -executeMethod MxFramework.Editor.CharacterImport.CharacterPackageImportCommand.Import \
  -characterPackage <path> \
  -quit
```

外部编辑器和 CI 也可以直接调用同一条 Authoring CLI 导入桥：

```bash
dotnet run --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- \
  character import-unity \
  --package <CharacterPackage> \
  --project-root <UnityProjectRoot> \
  --unity-root Assets/MxFrameworkGenerated/CharacterPackages \
  --check-files \
  --check-hashes
```

### Importer 执行流程

```text
读取 Character Resource Package
  -> 调用 Authoring Compiler
  -> 检查 ImportBlocked
  -> 导入 resources/models/textures/materials/animations/previews
  -> 生成 Unity project ResourceCatalog mapping
  -> 写入 config patch / package cache / adapter asset
  -> 刷新 AssetDatabase
  -> 输出 import report
```

### 写入目标

第一版优先写入明确、可 diff、可复现的目标：

```text
Assets/MxFrameworkGenerated/CharacterPackages/<packageId>/
  package_cache/
    manifest.json
    resource_catalog.json
    compile_result.json
    unity_import_write_plan.json
    resource_hash_report.json
    dependency_graph.json
    gate_report.txt
    import_report.json
    import_report.txt
  resources/
    models/
    textures/
    materials/
    animations/
  previews/
  config/
    character_config_patch.json
    geometry_binding.json
    resource_catalog_mapping.json
    resolver_verification_plan.json
    unity_resource_catalog.json
  generated/
    character_application_config_patch.json
    character_geometry_binding.json
    character_resource_mapping.json
```

如需 ScriptableObject adapter，只作为 Unity 侧索引和菜单友好入口；noEngine runtime config 仍以 JSON / Config source 为权威。

当前 C2 第一版实现采用“文件导入 + ResourceCatalog 映射”策略：资源文件按 C0.6 `CharacterUnityImportWritePlan` 复制到 Unity 项目 `Assets/` 下，`config/unity_resource_catalog.json` 使用 `memory` provider + `providerData.assetPath` 指向导入后的项目资源路径，保留 package-local ResourceKey、stable id、source path、content/import/dependency hash 和 source package hash。实际 glTF / GLB 是否能被 Unity 解析为 `GameObject` / `AnimationClip` 由项目安装的 importer package 决定；该桥接不私自实现模型格式转换，也不把 `UnityEngine.Object` 写入 noEngine config。

C2 后续必须升级为“源资源包 -> Unity 导入资产 -> Runtime Preview Prefab”的三层同步系统，任务拆分见 `Docs/Tasks/CHARACTER_RESOURCE_PACKAGE_C2_UNITY_ASSET_SYNC.md`。该阶段需要补齐 Unity imported asset catalog、GLB/FBX importer 策略、幂等导入、冲突状态机、Unity authoring override 回写和 CharacterStudio 同步状态 UI。

### 资源导入策略

- 模型、贴图、动画按 import/write plan 的 target path 导入。
- 导入前检查 target path conflict。
- hash 未变可跳过。
- hash 变化但 stable id 相同，输出 update report。
- 资源导入失败时，不写 config patch。
- config patch 写入成功后，保留 `resolver_verification_plan.json`；真正调用 `CharacterPackageResolver` 做导入后一致性检查属于 Runtime Spawn / Workstation 接入切片。
- `ExportBlocked` / `ImportBlocked` 时不写 Unity 项目目标；如果传入 `--report-out`，只在外部报告目录输出 import report。
- `SpawnBlocked` 时允许写入项目目标，但 `import_report.json.canSpawnAfterImport=false`，Runtime Spawn 不得使用该包生成角色。

## Runtime Spawn 实现

对应 Issue：#218

Runtime 不理解 `.mxchar` 包目录，只理解 Unity Importer 生成的配置和 binding。

生成流程：

```text
CharacterSpawnRequest
  -> SpawnPlanResolver
  -> CharacterPackageResolver
  -> ResourceDependencyReport preload
  -> Gameplay entity
  -> Combat body / colliders / hit zones
  -> CharacterRuntimeBinding
  -> Equipment runtime state
  -> Presentation view
  -> CharacterControl command source
```

#218 第一切片当前落点：

- 新增 `MxFramework.Character.RuntimeSpawn` noEngine 程序集，不改变 `MxFramework.Character.Application` 只依赖 Config 的边界。
- `CharacterImportedPackageJson` 从 `Assets/MxFrameworkGenerated/CharacterPackages/<packageId>/` 读取 #222 生成的 JSON 产物。
- `CharacterRuntimeSpawnResolver` 先检查 `import_report.json` gate；`SpawnBlocked` 不生成 binding。
- Runtime 解析必须复用 `SpawnPlanResolver` 和 `CharacterPackageResolver`，不在 runtime 侧私有拼表。
- 第一切片输出 `CharacterRuntimeBinding`、Gameplay registration plan、Combat body collider binding plan、weapon attachment / trace plan 和 Resource preload binding plan。
- `CharacterRuntimeSpawnModule` 可注册到 `RuntimeHost`，消费排队的 `CharacterSpawnRequest`，但目前只生成 plan，不创建真实 Gameplay / Combat / Presentation 实例。

后续 Runtime 验收场景：

- 生成 Iron Vanguard 剑盾玩家角色。
- 同一 `CharacterConfig` 生成敌方单剑角色。
- 切换 unarmed / single sword / sword shield loadout。
- Debug UI 显示 resolved profile、runtime ids、equipment state、abilities、resource issues、source package hash。
- SaveState roundtrip 恢复 config binding，并重新解析 active equipment state。

## 数据格式策略

第一版统一 JSON：

- package manifest：JSON。
- package-local resource catalog：JSON。
- geometry authoring：JSON。
- compile result：JSON。
- Unity import report：JSON + text summary。
- config patch：JSON。

要求：

- JSON property 使用 camelCase。
- enum 使用字符串。
- 所有 ID 字段保留 stable id 和 typed id 语义。
- 所有输出带 schema version。
- 所有 diagnostics 使用稳定 code。
- 生成文件应 deterministic，方便 diff 和 CI。

## 测试矩阵

| 阶段 | 测试 |
| --- | --- |
| C0 | manifest / geometry / socket / trace JSON roundtrip；unsupported shape diagnostics。 |
| C0.5 | resource catalog hash、duplicate ResourceKey、missing file、dependency graph。 |
| C0.6 | Iron Vanguard compile success；ImportBlocked / SpawnBlocked / WarningOnly；resolver consistency。 |
| C1 | 外部编辑器 smoke test；打开包、选中 collider、拖 socket、保存、预检。 |
| C2 | Unity EditMode importer test；资源导入计划、ResourceCatalog mapping、config patch 写入。 |
| D | PlayMode / Runtime Showcase；生成角色、切 loadout、SaveState roundtrip。 |

最低命令：

```bash
dotnet restore Tools/MxFramework.Authoring/MxFramework.Authoring.sln
dotnet build Tools/MxFramework.Authoring/MxFramework.Authoring.sln --no-restore /nr:false -m:1 -v:minimal
dotnet run --no-build --project Tools/MxFramework.Authoring/tests/MxFramework.Authoring.Tests/MxFramework.Authoring.Tests.csproj
```

Unity 相关阶段再补 Unity EditMode / PlayMode 验证。

## 开发顺序

不要从 UI 开始。正确顺序：

1. #221：包格式和 3D authoring DTO。
2. #223：资源 catalog、hash、依赖图、import hints。
3. #224：compiler、gate、write plan。
4. #217：外部 3D 编辑器。
5. #222：Unity importer。
6. #218：Runtime spawn。

每个阶段都必须能独立验收。C1 之前如果没有 C0.6，外部编辑器只能保存草稿，不能宣称可导入 Unity。

## Done Definition

实现完成时，应达到以下效果：

- `Tools/MxFramework.Authoring/samples/character-iron-vanguard` 是一个完整样例角色包。
- CLI 可以 inspect / validate / resources / hash / compile 这个包。
- 外部编辑器可以打开它，显示模型、collider、socket、trace，修改并保存。
- 外部编辑器预检结果和 CLI compile result 一致。
- Unity Importer 可以导入这个包，并生成 ResourceCatalog mapping、config patch、geometry binding。
- Runtime Showcase 可以从导入产物生成 Iron Vanguard 角色。
- Debug UI 可以显示 source package hash、resolved profile、equipment state、abilities、resource issues。

这时角色资源包主线才算从 authoring 到 runtime 跑通。
