# Character Resource Library Editor 01：Independent Resource Library Editor MVP

> Issue：#285
>
> Milestone：`[Character] Resource Library Editor`
>
> 状态：Spec for implementation
>
> 前置：`CHARACTER_RESOURCE_LIBRARY_00_DESIGN.md`、PR #283、`Tools/MxFramework.EditorHub`

## Goal

实现一个独立的 Resource Library Editor。它负责查看和管理角色资源包中的“可用资源集合”，并通过 Editor Hub 启动；CharacterStudio 只在编辑角色字段时打开字段级资源选择器，不再承担完整资源库浏览和管理职责。

第一版目标是 MVP，不一次性完成所有写操作：

- 能独立打开资源库编辑器。
- 能看到当前角色包的所有资源项。
- 能按类型、用途、导入状态、运行时可用性和文本搜索筛选。
- 能点击资源查看详情、Unity 同步状态、runtime binding、引用关系和 diagnostics。
- 写操作入口可以出现，但 API 未补齐前必须禁用并说明原因。
- 不允许前端直接手改 `resource_catalog.json`、`unity_resource_catalog.json` 或 runtime plan 产物。

## Product Boundary

### Resource Library Editor 负责

- 资源发现：展示 package-local resource catalog、Unity import catalog、FMOD snapshot、compiled runtime plan 中汇总出的资源视图。
- 资源检查：显示 `libraryItemId`、`stableId`、kind、usage、source、import status、runtime availability、runtime binding。
- 引用检查：显示资源被哪些角色配置、武器配置、动画配置、geometry、presentation 或 runtime plan group 引用。
- 诊断检查：显示资源级 diagnostics 和 suggested fix。
- 资源管理入口：导入、重导、替换源、删除、标签编辑、清理 orphan 的命令入口。

### Resource Library Editor 不负责

- 不编辑角色部位、碰撞框、挂点、武器槽位和动画状态机，这些仍属于 CharacterStudio 或后续 Animation/Equipment authoring。
- 不直接生成 Unity Prefab；它只能触发 Authoring server / CLI gate。
- 不让 runtime 读取编辑期 resource catalog。
- 不把 FMOD event 伪装成普通 `ResourceKey`。

### CharacterStudio 保持

- 角色装配、挂点、碰撞体、武器槽位、3D 预览。
- 字段级资源选择器：输入 `ResourceFieldSpec`，输出字段选择结果。
- 折叠式 runtime resource plan 预览。
- 不常驻显示完整资源库列表。

## Tool Surface

新增工具目录：

```text
Tools/MxFramework.ResourceLibrary/
  README.md
  start-resource-library.sh
  start-resource-library.bat
  start-resource-library.command
  scripts/smoke.mjs
  web/index.html
  web/app.js
  web/styles.css
```

Editor Hub 更新：

- `Tools/MxFramework.EditorHub/web/app.js` 中“资源库编辑器”卡片从 `待实现` 改为可打开。
- 打开 URL：

```text
/Tools/MxFramework.ResourceLibrary/web/?package=<packageRelative>
```

启动脚本默认：

- port：`4873`
- package：`Tools/MxFramework.Authoring/samples/character-iron-vanguard`
- URL：`http://127.0.0.1:4873/Tools/MxFramework.ResourceLibrary/web/`

## MVP UI Layout

### Top Bar

- 当前角色包选择。
- 刷新。
- 打开 CharacterStudio。
- 运行资源验证。
- 查看当前 resource plan。

### Left Panel：Resource Browser

显示资源列表，每一项至少包含：

- displayName / stableId。
- kind：model、animation、texture、material、vfx、audio、config、generated。
- usage：characterModel、weaponModel、animationClipGroup、previewThumbnail 等。
- source kind：ExternalFile、UnityAsset、FmodLibrary、GeneratedAsset。
- import status：Clean、New、SourceChanged、UnityMissing、ImportFailed、Conflict、ManualOverride、OrphanCandidate。
- runtime availability：RuntimeReady、RuntimeMissing、EditorOnly、PreviewOnly、AudioCueOnly、NotRuntimeLoadable。
- reference count。
- diagnostic count。

筛选器：

- search text。
- kind。
- usage。
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
   - `libraryItemId`
   - `stableId`
   - `displayName`
   - kind / usage / tags
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
| `GET /api/character/packages` | 包列表 |
| `GET /api/character/resources?package=...` | 当前资源库列表、状态、diagnostics |
| `GET /api/character/resource-plan?package=...` | 当前角色 runtime resource plan |

### Required MVP API

第一版需要补齐 inspect：

```text
GET /api/character/resources/inspect?package=<relative>&id=<libraryItemId-or-stableId-or-resourceKey>
```

返回：

```json
{
  "packageId": "iron_vanguard",
  "item": {},
  "authoring": {},
  "unity": {},
  "runtime": {},
  "references": [],
  "plans": [],
  "diagnostics": []
}
```

inspect 必须接受三种查找键：

- `libraryItemId`
- `stableId`
- package-local `resourceKey`

如果资源不存在，返回结构化错误：

```json
{
  "error": "RESOURCE_LIBRARY_ITEM_NOT_FOUND",
  "message": "Resource library item was not found.",
  "id": "..."
}
```

### Deferred Write APIs

这些 API 不是 MVP 必须完成，但 UI 需要为它们留入口并保持 disabled：

```text
POST /api/character/resources/import
POST /api/character/resources/reimport
POST /api/character/resources/replace-source
POST /api/character/resources/delete/preview
POST /api/character/resources/delete
POST /api/character/resources/tags
```

写 API 的共同规则：

- request 必须包含 package。
- response 必须包含 diagnostics。
- delete / replace 必须包含 reference impact。
- 写入前必须 validate。
- 写入后必须能重新生成 resource plan。
- 失败不能留下半写状态。

## Data Mapping

Resource Library Editor 只消费 Authoring server 聚合结果，不在前端自己拼多份 JSON。

后端聚合输入：

- package-local `resource_catalog.json`
- `unity_resource_catalog.json`
- `fmod_audio_library_snapshot.json`
- compiled `runtime_resource_catalog.json`
- compiled `character_resource_plan.json`
- `resource_validation_report.json`
- reference graph

前端消费模型：

```text
ResourceLibraryListResult
  items[]
  filters
  diagnostics[]

ResourceLibraryInspectResult
  item
  authoring
  unity
  runtime
  references[]
  plans[]
  diagnostics[]
```

## Implementation Order

### Step 1：API Inspect

Owner files:

- `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/EditorServer.cs`
- `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/CharacterPackages/CharacterResourceLibrary.cs`
- Authoring tests if needed.

Work:

- Add inspect helper in Authoring Core if current list model is insufficient.
- Add `GET /api/character/resources/inspect`.
- Reuse existing reference graph / diagnostics builders.
- Return stable not-found errors.

Validation:

```bash
dotnet run --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- \
  editor serve --root . --port 4883 \
  --package Tools/MxFramework.Authoring/samples/character-iron-vanguard

curl -fsS 'http://127.0.0.1:4883/api/character/resources/inspect?package=Tools%2FMxFramework.Authoring%2Fsamples%2Fcharacter-iron-vanguard&id=model.body'
```

### Step 2：Resource Library Web App

Owner files:

- `Tools/MxFramework.ResourceLibrary/web/index.html`
- `Tools/MxFramework.ResourceLibrary/web/app.js`
- `Tools/MxFramework.ResourceLibrary/web/styles.css`
- `Tools/MxFramework.ResourceLibrary/scripts/smoke.mjs`
- `Tools/MxFramework.ResourceLibrary/README.md`

Work:

- Build vanilla web app.
- Use API list and inspect.
- Implement filters.
- Implement details tabs.
- Implement copy JSON actions.
- Keep write actions disabled with explicit reasons.

Validation:

```bash
node --check Tools/MxFramework.ResourceLibrary/web/app.js
node Tools/MxFramework.ResourceLibrary/scripts/smoke.mjs
```

### Step 3：Launchers and Hub Integration

Owner files:

- `Tools/MxFramework.ResourceLibrary/start-resource-library.sh`
- `Tools/MxFramework.ResourceLibrary/start-resource-library.bat`
- `Tools/MxFramework.ResourceLibrary/start-resource-library.command`
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
Tools/MxFramework.ResourceLibrary/start-resource-library.sh 4884 Tools/MxFramework.Authoring/samples/character-iron-vanguard
```

### Step 4：End-to-End Smoke

Required commands:

```bash
git diff --check

node --check Tools/MxFramework.ResourceLibrary/web/app.js
node Tools/MxFramework.ResourceLibrary/scripts/smoke.mjs
node Tools/MxFramework.EditorHub/scripts/smoke.mjs
npm --prefix Tools/MxFramework.CharacterStudio run smoke

dotnet run --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- \
  character validate \
  --package Tools/MxFramework.Authoring/samples/character-iron-vanguard \
  --check-files --check-hashes
```

Manual smoke:

1. Start Resource Library Editor.
2. Confirm Iron Vanguard resources are visible.
3. Search `sword`, `shield`, `anim`.
4. Filter `RuntimeReady`.
5. Click body model.
6. Confirm Overview / Unity / Runtime / References / Diagnostics tabs populate.
7. Confirm write buttons are disabled with clear reasons.
8. Open CharacterStudio from Resource Library or Hub and confirm it still uses field picker, not full resource library panel.

## Acceptance Checklist

- [ ] `Tools/MxFramework.ResourceLibrary` exists with README, launchers, smoke script and web app.
- [ ] Editor Hub opens Resource Library Editor with current package query.
- [ ] Resource list shows all Iron Vanguard library items.
- [ ] Filters work without server-side mutation.
- [ ] Inspect API returns item details, references and diagnostics.
- [ ] Details tabs show Overview / Unity / Runtime / References / Diagnostics.
- [ ] Copy JSON actions work.
- [ ] Import / reimport / replace / delete / tag edit actions are disabled until API gates exist.
- [ ] CharacterStudio remains scoped to field-level resource picking.
- [ ] Validation commands pass.

## Out of Scope for MVP

- Full 3D model preview inside Resource Library Editor.
- Real import / replace / delete writes.
- FMOD audition playback.
- Unity AssetDatabase mutation from the web UI.
- Editing animation ownership or animation state machines.
- Closing `Resource Library Editor` milestone.

## Follow-Up Issues

After MVP, split these as separate issues:

1. Resource Library Editor 02：Write API Gate for Import / Reimport / Replace.
2. Resource Library Editor 03：Reference Graph Visualization and Delete Guard.
3. Resource Library Editor 04：Tag Editing and Orphan Cleanup Workflow.
4. Resource Library Editor 05：Preview Generation and Thumbnail Cache.
5. Resource Library Editor 06：FMOD Event / AudioCue Resource Management.
