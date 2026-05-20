# Authoring Resource Manager Editor 01：Global Resource Manager MVP

> Issue：#301 design correction; supersedes #285 character-only scope
>
> Milestone：`[Authoring] Resource Manager`
>
> 状态：Spec update required for implementation
>
> 前置：`CHARACTER_RESOURCE_LIBRARY_00_DESIGN.md`、PR #283、`Tools/MxFramework.EditorHub`

## Goal

实现一个独立的 Authoring Resource Manager Editor。它负责查看和管理项目级“可用资源集合”，并通过 Editor Hub 启动；CharacterStudio、Animation Editor、Combat/VFX Editor、UI/Config Editor 只在编辑字段时打开字段级资源选择器，不再承担完整资源浏览和管理职责。

本 MVP 的设计目标已经从“角色包资源库”调整为“全局资源管理器”。角色包 `resource_catalog.json` 是一个 provider，Unity AssetDatabase、现有 `MxFramework.Resources.ResourceCatalog`、FMOD snapshot、external import staging、generated assets 也是 provider。

本阶段不要求兼容旧角色包资源字段、旧 `/api/character/resources` API、旧 `ResourceLibrary` 工具命名或 Iron Vanguard 样例数据。实现可以破坏式重构并替换过时样例，只要新 Authoring Resource Manager 模型更清晰、可测试、可长期维护。

第一版目标是 MVP，不一次性完成所有写操作：

- 能独立打开资源管理器。
- 能看到来自所有可用 provider 的资源项，并支持按 package/domain/context 缩小范围。
- 能按类型、用途、provider、导入状态、运行时可用性和文本搜索筛选。
- 能点击资源查看详情、Unity 同步状态、runtime binding、引用关系和 diagnostics。
- 写操作入口可以出现，但 API 未补齐前必须禁用并说明原因。
- 不允许前端直接手改 `resource_catalog.json`、`unity_resource_catalog.json`、runtime catalog、FMOD snapshot 或 runtime plan 产物。

## Product Boundary

### Resource Manager Editor 负责

- 资源发现：展示 Unity AssetDatabase、现有 runtime catalog、package-local resource catalog、FMOD snapshot、external import staging、generated assets 中汇总出的全局资源视图。
- 资源检查：显示 `resourceId`、`stableId`、kind、usage、provider、source、import status、runtime availability、binding kind。
- Provider 检查：显示每个 provider 是否可用、最后同步时间、导入/扫描 diagnostics 和 source count。
- 引用检查：显示资源被哪些角色配置、武器配置、动画配置、geometry、presentation、VFX、UI/config 或 runtime/domain plan group 引用。
- 诊断检查：显示资源级 diagnostics 和 suggested fix。
- 资源管理入口：文件/文件夹导入、重导、替换源、删除、标签编辑、清理 orphan 的命令入口。

### Resource Manager Editor 不负责

- 不编辑角色部位、碰撞框、挂点、武器槽位和动画状态机，这些仍属于 CharacterStudio 或后续 Animation/Equipment authoring。
- 不直接生成 Unity Prefab；它只能触发 Authoring server / CLI gate。
- 不让 runtime 读取编辑期 resource catalog。
- 不把 FMOD event 伪装成普通 `ResourceKey`。
- 不替代 Unity AssetDatabase、Addressables、AssetBundle 或 `MxFramework.Resources.IResourceManager`。

### Consumer Editors 保持

- CharacterStudio 保持角色装配、挂点、碰撞体、武器槽位、3D 预览和折叠式角色 resource plan 预览。
- Animation / Combat / VFX / UI 编辑器保持各自业务配置，不直接解析资源目录。
- 字段级资源选择器统一输入 `ResourceFieldSpec`，输出 `ResourceSelectionRef`。
- 业务编辑器不常驻显示完整资源列表。

## Tool Surface

新增工具目录：

```text
Tools/MxFramework.ResourceManager/
  README.md
  start-resource-manager.sh
  start-resource-manager.bat
  start-resource-manager.command
  scripts/smoke.mjs
  web/index.html
  web/app.js
  web/styles.css
```

现有 `Tools/MxFramework.ResourceLibrary` 可以在执行任务中直接重命名、替换或删除，不需要为了历史脚本名保留兼容入口。

Editor Hub 更新：

- `Tools/MxFramework.EditorHub/web/app.js` 中“资源管理器”卡片打开全局 Resource Manager Editor。
- 打开 URL：

```text
/Tools/MxFramework.ResourceManager/web/?scope=<scopeId>&provider=<optional>&domain=<optional>
```

启动脚本默认：

- port：`4873`
- scope：默认全局资源 scope；可以用任意当前测试资源 scope，不强制 Iron Vanguard。
- URL：`http://127.0.0.1:4873/Tools/MxFramework.ResourceManager/web/`

## MVP UI Layout

### Top Bar

- Scope 选择：全部资源、指定 provider、指定 package、指定 domain。
- 刷新。
- 打开 CharacterStudio / 后续业务编辑器。
- 运行资源验证。
- 查看当前 resource plan。

### Left Panel：Resource Browser

显示资源列表，每一项至少包含：

- displayName / stableId。
- kind：model、animation、texture、material、vfx、audio、config、generated。
- usage：characterModel、weaponModel、animationClipGroup、previewThumbnail 等。
- provider：unityAssetDatabase、runtimeCatalog、characterPackage、fmod、externalImportStaging、generatedAssets。
- source kind：ExternalFile、UnityAsset、RuntimeCatalogAsset、PackageResource、FmodLibrary、GeneratedAsset。
- import status：Clean、New、SourceChanged、UnityMissing、ImportFailed、Conflict、ManualOverride、OrphanCandidate。
- runtime availability：RuntimeReady、RuntimeMissing、EditorOnly、PreviewOnly、AudioCueOnly、NotRuntimeLoadable。
- reference count。
- diagnostic count。

筛选器：

- search text。
- kind。
- usage。
- provider。
- source kind。
- import status。
- runtime availability。
- tag。
- only referenced / only orphan。
- only runtime loadable。
- only has diagnostics。

### Center Panel：Preview / Summary

第一版只做 lightweight preview：

- 模型资源：显示缩略图或 fallback icon；不要求完整 Three.js 预览。
- 动画资源：显示 clip/group 信息和依赖骨架。
- 音频资源：显示 FMOD event path/guid 或 AudioCue 信息。
- 贴图/材质/VFX：显示元数据和 runtime binding。

如果没有 preview metadata，必须显示“未生成预览”，不能显示破图。

### Right Panel：Resource Inspector

Tabs：

1. Overview
   - `resourceId`
   - `libraryItemId`
   - `stableId`
   - `displayName`
   - kind / usage / tags
   - provider
   - source kind / source path
   - import status / runtime availability

2. Unity
   - `unityAssetGuid`
   - `unityAssetPath`
   - importer kind
   - main object type
   - sub-assets
   - last import operation
   - Unity diagnostics

3. Runtime
   - runtime binding kind
   - `resourceKey` if `ResourceManagerAsset`
   - provider id
   - address
   - asset type
   - hash
   - preload policy
   - included runtime plan groups

4. References
   - source config kind
   - source stable id
   - source field
   - target stable id
   - binding kind
   - required at runtime
   - preload policy

5. Diagnostics
   - severity
   - code
   - message
   - suggested fix
   - source field

### Action Bar

MVP 状态：

- `导入资源`：disabled，提示“等待 import API gate”。
- `重导`：disabled，提示“等待 reimport API gate”。
- `替换源`：disabled，提示“等待 replace-source API gate”。
- `删除资源`：disabled，提示“等待 reference graph delete guard”。
- `编辑标签`：disabled，提示“等待 tag update API gate”。
- `复制详情 JSON`：enabled。
- `复制诊断 JSON`：enabled。

如果某个写 API 已经实现，则按钮必须：

1. 先调用 validate/preview endpoint。
2. 显示影响面。
3. 用户确认后再调用 write endpoint。
4. 完成后刷新 resource list、details、reference graph、runtime plan。

## Authoring Server API Contract

### Existing API

已可复用：

| API | 用途 |
| --- | --- |
| `GET /api/authoring/resource-scopes` | 资源 scope 列表 |
| `GET /api/authoring/resources?scope=...` | 全局资源列表、provider、状态、diagnostics |
| `GET /api/authoring/resource-plans?scope=...&consumerKind=...` | 指定 consumer 的 runtime/domain resource plan |

### Required MVP API

第一版需要补齐全局 query / inspect。Resource Manager Editor 只应使用全局 Authoring API：

```text
GET /api/authoring/resources?scope=<optional>&provider=<optional>&domain=<optional>&kind=<optional>&q=<optional>
GET /api/authoring/resources/inspect?id=<resourceId-or-stableId-or-resourceKey-or-unityGuid>
GET /api/authoring/resources/providers
```

返回：

```json
{
  "scope": {
    "packageId": "iron_vanguard",
    "domain": "character"
  },
  "item": {},
  "provider": {},
  "authoring": {},
  "unity": {},
  "runtime": {},
  "references": [],
  "plans": [],
  "diagnostics": []
}
```

inspect 必须接受三种查找键：

- `resourceId`
- `stableId`
- Unity GUID
- provider-local key（例如 package-local `resourceKey`）

如果资源不存在，返回结构化错误：

```json
{
  "error": "AUTH_RESOURCE_ITEM_NOT_FOUND",
  "message": "Authoring resource item was not found.",
  "id": "..."
}
```

### Deferred Write APIs

这些 API 不是 MVP 必须完成，但 UI 需要为它们留入口并保持 disabled：

```text
POST /api/authoring/resources/import
POST /api/authoring/resources/reimport
POST /api/authoring/resources/replace-source
POST /api/authoring/resources/delete/preview
POST /api/authoring/resources/delete
POST /api/authoring/resources/tags
```

写 API 的共同规则：

- request 必须包含 provider 或可推导的 target scope；角色包写入才必须包含 package。
- response 必须包含 diagnostics。
- delete / replace 必须包含 reference impact。
- 写入前必须 validate。
- 写入后必须能重新扫描 provider，并在需要时重新生成 domain resource plan。
- 失败不能留下半写状态。

## Data Mapping

Resource Manager Editor 只消费 Authoring server 聚合结果，不在前端自己拼多份 JSON。

后端聚合输入：

- Unity AssetDatabase scan result
- existing `MxFramework.Resources.ResourceCatalog`
- package-local `resource_catalog.json`
- `unity_resource_catalog.json`
- `fmod_audio_library_snapshot.json`
- external import staging index
- generated assets index
- compiled `runtime_resource_catalog.json`
- compiled `character_resource_plan.json`
- `resource_validation_report.json`
- reference graph

前端消费模型：

```text
AuthoringResourceListResult
  items[]
  providers[]
  filters
  diagnostics[]

AuthoringResourceInspectResult
  item
  provider
  authoring
  unity
  runtime
  references[]
  plans[]
  diagnostics[]
```

## Implementation Order

### Step 1：Global Query and Inspect API

Owner files:

- `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/EditorServer.cs`
- `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/CharacterPackages/CharacterResourceLibrary.cs`
- future Authoring resource manager service files
- Authoring tests if needed.

Work:

- Add global resource manager query / inspect service over current character package implementation.
- Add `GET /api/authoring/resources`, `GET /api/authoring/resources/inspect`, `GET /api/authoring/resources/providers`.
- Delete or replace `/api/character/resources/inspect`; do not build new UI on the character-prefixed endpoint.
- Reuse existing reference graph / diagnostics builders.
- Return stable not-found errors.

Validation:

```bash
dotnet run --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- \
  editor serve --root . --port 4883 \
  --package Tools/MxFramework.Authoring/samples/character-iron-vanguard

curl -fsS 'http://127.0.0.1:4883/api/authoring/resources/inspect?scope=default&id=model.body'
```

### Step 2：Resource Manager Web App

Owner files:

- `Tools/MxFramework.ResourceManager/web/index.html`
- `Tools/MxFramework.ResourceManager/web/app.js`
- `Tools/MxFramework.ResourceManager/web/styles.css`
- `Tools/MxFramework.ResourceManager/scripts/smoke.mjs`
- `Tools/MxFramework.ResourceManager/README.md`

Work:

- Build vanilla web app as global Resource Manager Editor.
- Use API list and inspect.
- Implement filters.
- Implement provider status display.
- Implement details tabs.
- Implement copy JSON actions.
- Keep write actions disabled with explicit reasons.

Validation:

```bash
node --check Tools/MxFramework.ResourceManager/web/app.js
node Tools/MxFramework.ResourceManager/scripts/smoke.mjs
```

### Step 3：Launchers and Hub Integration

Owner files:

- `Tools/MxFramework.ResourceManager/start-resource-manager.sh`
- `Tools/MxFramework.ResourceManager/start-resource-manager.bat`
- `Tools/MxFramework.ResourceManager/start-resource-manager.command`
- `Tools/MxFramework.EditorHub/web/app.js`
- `Tools/MxFramework.EditorHub/scripts/smoke.mjs`
- README files.

Work:

- Reuse EditorHub / CharacterStudio script checks:
  - repo root
  - Authoring CLI project
  - .NET 9+
  - package path and manifest
  - port availability
- Update Hub card from disabled to enabled.

Validation:

```bash
Tools/MxFramework.ResourceManager/start-resource-manager.sh 4884
```

### Step 4：End-to-End Smoke

Required commands:

```bash
git diff --check

node --check Tools/MxFramework.ResourceManager/web/app.js
node Tools/MxFramework.ResourceManager/scripts/smoke.mjs
node Tools/MxFramework.EditorHub/scripts/smoke.mjs
npm --prefix Tools/MxFramework.CharacterStudio run smoke

dotnet run --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- \
  character validate \
  --package Tools/MxFramework.Authoring/samples/character-iron-vanguard \
  --check-files --check-hashes
```

Manual smoke:

1. Start Resource Manager Editor.
2. Confirm provider status is visible.
3. Confirm resources are visible under provider filters; use current sample data or a replacement sample set.
4. Confirm Unity/runtime/FMOD providers are either populated or clearly marked unavailable/stale.
5. Search `sword`, `shield`, `anim`.
6. Filter `RuntimeReady`.
7. Click body model.
8. Confirm Overview / Unity / Runtime / References / Diagnostics tabs populate.
9. Confirm write buttons are disabled with clear reasons.
10. Open CharacterStudio from Resource Manager or Hub and confirm it still uses field picker, not full resource browser panel.

## Acceptance Checklist

- [ ] `Tools/MxFramework.ResourceManager` exists with README, launchers, smoke script and web app.
- [ ] Editor Hub opens Resource Manager Editor with optional package/domain query.
- [ ] Resource list shows provider status and current sample resources.
- [ ] Resource list can also represent Unity/runtime/FMOD/external/generated providers when available.
- [ ] Filters work without server-side mutation.
- [ ] Inspect API returns item details, references and diagnostics.
- [ ] Details tabs show Overview / Unity / Runtime / References / Diagnostics.
- [ ] Copy JSON actions work.
- [ ] Import / reimport / replace / delete / tag edit actions are disabled until API gates exist.
- [ ] CharacterStudio remains scoped to field-level resource picking.
- [ ] Validation commands pass.

## Out of Scope for MVP

- Full 3D model preview inside Resource Manager Editor.
- Real import / replace / delete writes.
- FMOD audition playback.
- Unity AssetDatabase mutation from the web UI.
- Editing animation ownership or animation state machines.
- Closing `Authoring Resource Manager` milestone.

## Follow-Up Issues

After MVP, split these as separate issues:

1. Authoring Resource Manager 02：Write API Gate for Import / Reimport / Replace.
2. Authoring Resource Manager 03：Reference Graph Visualization and Delete Guard.
3. Authoring Resource Manager 04：External Folder Import and Staging Filters.
4. Authoring Resource Manager 05：Preview Generation and Thumbnail Cache.
5. Authoring Resource Manager 06：FMOD Event / AudioCue Resource Management.
6. Authoring Resource Manager 07：Unity AssetDatabase and Runtime Catalog Providers.
