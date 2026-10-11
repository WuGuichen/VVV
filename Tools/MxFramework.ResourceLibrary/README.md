# MxFramework Authoring Resource Manager

独立资源管理器用于查看 Authoring 阶段的全局资源视图、provider 状态、运行时资源计划、引用关系和诊断信息。它的目标是服务 CharacterStudio、Animation Editor、Combat/VFX Editor、UI/Config Editor 等所有外部编辑器。

当前目录和启动脚本仍沿用旧 `ResourceLibrary` 路径以减少迁移噪音，但界面语义已经改为资源管理器。默认 Codex Flow Test 只是当前工作上下文：用于引用图、Resource Plan 和 consumer 诊断，不代表资源归属于该角色包。

设计目标已经调整为全局 Authoring Resource Manager。角色包资源只是 `characterPackage` provider，Unity AssetDatabase、现有 `MxFramework.Resources.ResourceCatalog`、FMOD snapshot、external import staging 和 generated assets 也应进入统一资源视图。

导入、重导和替换资源已经通过 Authoring API gate 开放；删除和标签编辑仍等待 reference graph delete guard 后再开放。

CharacterStudio 可以通过 `package`、`workspace`、`select`、`returnField` 和 `returnKind` 查询参数以委托模式打开 Resource Manager。该模式下 Resource Manager 仍然负责公共资源同步、替换、Build Profile 和 public import hints 编辑；作者完成资源选择后，`绑定回 CharacterStudio` 会把稳定 resource key 回传给打开它的 CharacterStudio 页面。没有 opener 时，Resource Manager 退化为复制 resource key，供作者手动回填。

## Build Profile / Bundle Plan

Ownership summary:

```text
Resource Manager Web UI
  -> edits only the complete resource-manager.manual producer draft
  -> saves through the sole Profile Aggregator with optimistic revision
  -> leaves every domain producer row and all Bundle/CatalogMount policy read-only

Global AssetBundle Builder Workbench
  -> reads the saved profile
  -> stages catalog, mounts, preload groups, bundle dependencies and AssetBundles
  -> publishes one fingerprinted artifact set and writes its commit marker last

Runtime
  -> validates the artifact-set identity and every file SHA-256 before loading
  -> does not read the Authoring Resource Manager aggregate view
```

The Resource Manager reads the canonical `global_resource_mapping_profile.v2.json` and may replace only the complete `resource-manager.manual` producer snapshot. Domain-owned rows remain read-only. The Bundle Plan uses the same compiler as Unity; the web UI does not emit Player artifacts.

“同步公共资源”只提交 `demo.samples.global-profile` 的完整 owner snapshot，不会顺带重写 Character、Audio、FairyGUI、VFX 或 policy producer。请求必须先取得 concrete Unity build target；缺少时在复制前失败。新复制且尚无 Unity GUID 的资源仍保留 canonical ResourceKey、Bundle 和 preload 意图，并以 `GeneratedUnityAsset` locator 表示 `NeedsUnityImport`，不会借用 `editorOnly` 改写交付语义；GUID 稳定后的刷新会保留该 owner 行已有的 dependencies、labels 和 provider extension。

Bundle-first rule:

```text
Synchronize producer-owned Bundle policy
  -> assign manual resources to an existing Bundle
  -> save Profile
  -> preview Bundle Plan
  -> build in Unity Workbench
  -> verify Offline Runtime
```

In canonical JSON, a Bundle is a producer-owned `bundleRules[]` item and an internal resource references it through `entries[].bundleIntent.ruleId`. The browser uses `bundleRule` only as a draft projection for manual entries. Resource Manager never creates, edits or deletes Bundle policy. `preloadGroups` describe runtime load timing; they are not physical bundle groups.

UI 工作区（已完成 `RESOURCE_MANAGER_WORKFLOW_UI_REORG_01`，并新增 StorySource 故事设定、ArtSource 状态看板、Audio 语义审查、Refs 审查工作台和 Graph 关系图）：

```text
Story / Art / Browse / Audio / Profile / Refs / Graph / Build / Debug 九工作区；详见 Docs/Tasks/RESOURCE_MANAGER_WORKFLOW_UI_REORG_01.md
```

面向使用者的完整流程见 `Docs/USAGE.md` 的 `13.1 Authoring Resource Manager / Editor Hub`；资源系统边界和 DoD 见 `Docs/RESOURCE_SYSTEM_WORKFLOW.md`。

Story 工作区用于维护 `StorySource/Projects/WGame/Canon`、`Outlines`、`story_packets.json` 和 `Packets/*.json`。它是 WGame 项目故事设计底座，并以“故事设定编辑器”的形式呈现：顶部显示设定进度、当前阻塞和下一步动作；左侧维护世界观记忆 canon facts，中间逐条编辑故事大纲 beat flow，右侧从已批准的 canon / outline 派生 Story Input Packet，并在字段完整后“批准给美术”。选择 packet 会高亮它引用的 canon / outline，选择 outline 会高亮相关 canon，但三栏保持独立选中。保存时使用 `storySourcePacketsHash` 做冲突保护；批准 packet 前会强制检查必需字段、`canonRefs` 是否都是 `canon`、`outlineRefs` 是否都是 `approved-for-packet`。服务端 diagnostics 是最终权威，前端只做提前提示和分组展示。该页不写 ArtSource manifest、不导入 Unity、不保存 Profile、不生成 runtime Story SaveState；批准后的 packet 供 ArtSource 派生解除 story gate。

Art 工作区用于显示 `ArtSource/Projects/WGame/art_manifest.json` 和 `ArtSource/Projects/WGame/art_tasks.json` 派生出的美术开发状态。它是多进程、预览优先的任务推进工作台：顶部显示 Story / Reference / Source / Handoff / Legacy 等并行 workstream，左侧按 workstream 展开多条任务，中间显示任务级预览、绑定资产 contact sheet 或明确的“未产生预览”，右侧显示“本条下一动作”和受控任务推进表单。预览只来自已有 PNG/JPG/WebP，不生成占位图，也不渲染 broken image。该页允许通过 Authoring API 创建 `art_tasks.json` 里的进程和任务，并保存已有任务的 `status`、`priority`、`blockedBy`、`nextAction` 和 `assetIds`；保存时使用 `artSourceTasksHash` 做冲突保护。它不写 manifest、不审批故事、不生成图片、不导入 Unity、不保存 Profile；正式运行资源仍要在 Unity/FairyGUI 导入后通过 Browse / Profile / Build 路径进入 Resource Manager 和 Unity Workbench。

Audio 工作区是 Cue-first 的只读总览，显示 FMOD 来源、语义状态、引用、package 采用和诊断，并按 `audioCueId` 深链独立的 `Tools/MxFramework.AudioWorkbench`。Cue/Event 编辑、真实 Unity/FMOD 试听、人工批准、保存草稿和 Runtime Catalog 发布只在 Audio Workbench 进行，Resource Manager 不再提供第二套保存入口。

Browse / Profile / Build 工作区内的上下文流程助手会按 `选择资源 -> 分配 Bundle -> 保存 Profile -> 检查 Build -> Unity 构建 -> Offline 验证` 给出当前阶段和下一步主动作。步骤本身可点击，用于切换工作区、应用推荐筛选、刷新 Build 状态、显示 Unity 构建指引或复制验证清单。它只根据现有 Authoring API、Bundle Plan 和 Build Status 派生状态，不改变 Profile schema，也不会在 Web 中执行 AssetBundle 构建；Offline 只显示“可验证 / 待验证”，不代表已经完成运行时验证。

Refs 审查工作台用于 Bundle 前检查。它复用 Authoring `referenceGraph`、单资源 inspect references、当前 Resource Mapping Profile 草稿和 Bundle Plan 预览，默认面向策划 / 美术显示“本页能回答什么”：谁在引用当前资源、同组资源 Bundle 是否合理、哪些建议可以安全写入 Profile 草稿。审查范围、引用者配置卡和可执行修复清单都会用中文解释“这是什么、为什么重要、下一步做什么”；`sourceStableId`、`sourceField`、`targetStableId` 等技术字段保留在“工程详情”折叠区或 Debug References 原始页签中。行级“写入推荐 Bundle 草稿”和批量“应用可见建议”只会修改前端 Profile 草稿并显示“Profile 草稿未保存”；“撤销本轮应用”只回滚最近一次 Refs 自动应用且未被后续手动改动的草稿项。必须再点击“保存 Profile”才会写入 `global_resource_mapping_profile.v2.json`。Refs 不构建 AssetBundle，也不写 `StreamingAssets`。

Graph 关系图用于只读理解引用关系。它复用同一份 `referenceGraph`、Profile 草稿和 Bundle Plan 数据，用三列 SVG 显示“引用者配置 -> 资源 -> Bundle / delivery”。默认从当前资源视角打开，可切换到已勾选资源、只看需要处理、当前 Bundle 或全部引用图；全部图超过 80 个节点时会自动降噪。点击节点或连线只显示中文解释、Bundle 状态和相关建议；修复入口统一跳回 Refs，Graph 本身不写 Profile 草稿、不保存 Profile、不构建 AssetBundle，也不写 `StreamingAssets`。

顶栏保留全局操作；底部常驻操作栏已移除。后续在本地 Player 流程稳定前，不要扩展删除资源、标签编辑、Online 模式或新 provider。

Save validation rejects invalid profile drafts. Common causes:

- `ResourceKey id contains invalid characters.` The generated or edited resource key must be a runtime key such as `ui.start_screen.button.normal`, not a provider id, `resourceId`, path, or value containing characters such as `:`, `|`, whitespace or slashes.
- `Internal entry requires one Bundle intent.` Internal Player resources must select an existing producer-owned Bundle policy.
- `Runtime-loadable entry references a missing bundle rule.` The entry points to a Bundle id that is not present in `bundleRules[]`; choose an existing rule or ask its policy producer to publish the required rule.
- `External entry requires one CatalogMount.` An external manual entry must reference a declared concrete Catalog mount; the UI does not infer identity, path or Provider ownership.
- `只读：该条目由 ... 生产。` Domain producer rows must be changed in their owning authoring tool and resynchronized; Resource Manager will not overwrite them.

Resource Manager 已接入 Global Resource Build Profile 的 Bundle-first authoring 流程：

- Build Profile 筛选：资源浏览器可按 `notInProfile`、`saved`、`draftOnly`、`removedInDraft`、`modifiedInDraft` 过滤，也可只看 runtime-ready candidates。
- 显式多选：每个资源行都有独立 checkbox；“选择可见”只勾选当前 filtered/rendered list，不会包含隐藏、被筛掉或未来分页的资源。
- Bundle policy：`bundleRules[]` 只读，显示 `id`、`bundleName`、`compression`、`buildTarget`、`includeDependencies` 和 `allowEmpty`；修改必须回到 policy producer。
- “加入到 Bundle” / “批量加入 Bundle”：只把 manual 资源加入草稿，并选择一个已存在的 Bundle policy id。
- “批量移出 Bundle”：只清空资源的 `entry.bundleRule`，不删除资源 entry。
- “批量移动到 Bundle”：对已有 entry 再次执行“批量加入 Bundle”，会把资源归属改到目标 Bundle。
- “加入构建 Profile”：只把当前选中资源加入 `ResourceMappingProfile` 草稿，不自动分包；正式分包仍要选择 Bundle。
- “移出构建 Profile”：只从 manual producer 草稿中移除当前资源；域生产者条目不会被删除。
- “批量加入” / “批量移出”：只处理已显式勾选的资源，并复用现有 Profile entry 匹配和构造语义。
- “保存 Profile”：通过 Authoring API 和 Aggregator 校验后替换 `resource-manager.manual` 的完整 snapshot；生产者拥有的行只读。
- Profile 工作区：左侧 Bundle 列表，中间 Bundle 成员资源表，右侧 Bundle 设置；单资源字段编辑折叠在“当前资源 Profile 字段”里。
- 单资源 Profile 编辑：只有 `resource-manager.manual` 条目可编辑；`bundleRule` 下拉框只显示现有 policy，覆盖字段使用 v2 `overrideIntent = forbid / allow`。
- Build Profile 批量字段编辑：只作用于已勾选、已在草稿且由 manual producer 拥有的条目；每个字段都要先显式勾选启用，空输入不会覆盖。支持 `deliveryMode`、`overrideIntent`、`bundleRule`、`preloadGroups` 和 `labels`，其中数组字段使用逗号分隔并替换整个数组。域生产者条目和未入草稿资源计入“跳过”。
- Bundle Plan：来自已保存 Profile 和唯一编译器的当前 DTO，使用 `entries`、`bundles[].bundleId/resourceKeys/includeDependencies` 与 `counts`；草稿保存后刷新。
- Build Status：Build 工作区通过只读 Authoring API 汇总 Profile、Plan、Unity build report 和 `global_resource_artifact_set.json`。只有 artifact-set 内所有必需文件存在、SHA-256 匹配且 Profile/Catalog/target/mapping/build/source identity 与报告一致时，状态才可进入 Offline 验证；Authoring 端不会把“文件存在”冒充 Unity source currentness。

Build Profile 状态含义：

- `notInProfile`：既不在已保存 Profile，也不在当前草稿。
- `saved`：已保存 Profile 和当前草稿都有该资源，字段未变化。
- `draftOnly`：当前草稿新增，尚未保存。
- `removedInDraft`：已保存 Profile 中存在，但当前草稿已移除，尚未保存。
- `modifiedInDraft`：已保存 Profile 和当前草稿都有该资源，但草稿字段有变化。

典型流程：

1. 启动 Resource Manager。
2. 在 Profile 工作区确认 policy producer 已提供目标 Bundle；Web 端只读该规则。
3. 在 Browse 用 Build Profile / runtime-ready / provider / kind / search 等筛选器收窄候选资源。
4. 勾选多个资源后选择目标 Bundle，点击“批量加入 Bundle”；单资源也可以用“加入到 Bundle”。
5. 回到 Profile 工作区查看 Bundle 成员表，必要时移动 manual 资源，并编辑 delivery mode、override intent、preload groups 和 labels。
6. 点击“保存 Profile”。
7. 在 Build 工作区查看构建状态总览和 Bundle Plan saved-profile preview，确认 Bundle、生成产物和成员资源符合预期。

Bundle Plan 是预览面，Web UI 不构建 AssetBundle、不写 `StreamingAssets`。真正生成 Player 产物仍在 Unity 中执行：

```text
MxFramework/Resources/Validate Global Resource Mapping
MxFramework/Resources/Build Global Player Resources
```

当前已完成的是 manual producer authoring、所有权保护、保存校验、Bundle Plan 预览、Global AssetBundle Builder Workbench、原子生成标记、严格 CatalogMount 解析和 Unity 菜单本地 Player 构建入口。未完成的是批量/增量构建、远端热更 manifest、CDN 发布、签名、加密、断点续传和 YooAsset adapter。默认路线仍是 MxFramework Catalog + AssetBundleProvider / RemoteBundleProvider；YooAsset 不是默认路线，本仓库不做 adapter。

## 导入规则

资源管理器的导入面板先选择“导入类型”，再选择单个文件或文件夹。文件夹导入会先走 external import staging 预检：

- `.meta`、`.DS_Store`、隐藏目录等编辑器元数据会计入“忽略元数据”，不会成为资源。
- 当前导入类型会作为最终归类依据。例如选择“动画 Clip/Group”后，`.anim`、`.glb`、`.gltf`、`.json` 会导入为 `animation / animationClipGroup`；`.fbx` 只作为模型/外部转换来源，不归类为动画 Clip 或 Clip Group。
- 不匹配当前导入类型、格式不支持、重复 hash 或超过大小限制的文件会计入“跳过非匹配”或诊断，不会自动写入资源目录。
- 资源浏览器只负责准备和提供资源列表；角色、动画、战斗等编辑器通过资源选择器引用这些资源。

## 启动

macOS / Linux:

```bash
Tools/MxFramework.ResourceLibrary/start-resource-library.sh
```

Windows:

```bat
Tools\MxFramework.ResourceLibrary\start-resource-library.bat
```

macOS Finder 可双击：

```text
Tools/MxFramework.ResourceLibrary/start-resource-library.command
```

默认：

- port: `4873`
- default package context: `Tools/MxFramework.Authoring/samples/character-codex-flow-test`
- URL: `http://127.0.0.1:4873/Tools/MxFramework.ResourceLibrary/web/?package=Tools/MxFramework.Authoring/samples/character-codex-flow-test`

可指定端口和角色包：

```bash
Tools/MxFramework.ResourceLibrary/start-resource-library.sh 4884 Tools/MxFramework.Authoring/samples/character-codex-flow-test
```

## 环境变量

- `MXFRAMEWORK_RESOURCE_LIBRARY_PORT`: 默认端口。
- `MXFRAMEWORK_RESOURCE_LIBRARY_PACKAGE`: 默认工作上下文路径。当前阶段通常是角色包路径，后续应扩展为通用 authoring scope。
- `MXFRAMEWORK_RESOURCE_LIBRARY_OPEN_BROWSER`: 设为 `0` 时不自动打开浏览器。

## Authoring Server

启动脚本会检查：

- 仓库根目录。
- `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj`。
- .NET 9+ SDK。
- 角色包目录和 `manifest.json`。
- 端口占用。
- 已运行且匹配 Authoring Resource Manager API 的 Authoring server，包括资源列表 API、inspect API 和 Audio semantic API。

如果指定端口上已有 Authoring server 且资源列表 API、inspect API 和 `audio-semantics` API 都可访问，脚本会直接打开 Resource Manager Editor。

如果页面显示“服务未就绪”，优先重新运行启动脚本。浏览器页面不能直接执行本地 shell/bat 脚本；需要从终端运行 `.sh` / `.bat`，或在 macOS Finder 双击 `.command`。

如果 Audio 工作区显示“语义 API 不可用”，优先确认浏览器连接的端口是否仍是旧 Authoring server；这通常不是 manifest 内容问题，而是该端口的服务端缺少 `/api/authoring/resources/audio-semantics`。

如果端口已被旧 Authoring server 占用，脚本会提示该进程不匹配当前 Authoring Resource Manager API。此时可以停止旧进程，或换端口启动：

```bash
Tools/MxFramework.ResourceLibrary/start-resource-library.sh 4884 Tools/MxFramework.Authoring/samples/character-codex-flow-test
```

## 验证

```bash
bash -n Tools/MxFramework.ResourceLibrary/start-resource-library.sh
for file in \
  Tools/MxFramework.ResourceLibrary/web/app.js \
  Tools/MxFramework.ResourceLibrary/web/resource-library-core.js \
  Tools/MxFramework.ResourceLibrary/web/resource-library-audio.js \
  Tools/MxFramework.ResourceLibrary/web/resource-library-audio-workspace.js \
  Tools/MxFramework.ResourceLibrary/web/resource-library-audio-inspector.js \
  Tools/MxFramework.ResourceLibrary/web/resource-library-audio-preview.js \
  Tools/MxFramework.ResourceLibrary/web/resource-library-audio-diagnostics.js \
  Tools/MxFramework.ResourceLibrary/web/resource-library-browse.js \
  Tools/MxFramework.ResourceLibrary/web/resource-library-profile.js \
  Tools/MxFramework.ResourceLibrary/web/resource-library-references.js \
  Tools/MxFramework.ResourceLibrary/web/resource-library-graph.js \
  Tools/MxFramework.ResourceLibrary/web/resource-library-build-debug.js \
  Tools/MxFramework.ResourceLibrary/web/story-source-status.js \
  Tools/MxFramework.ResourceLibrary/web/art-source-status.js; do
  node --check "$file"
done
node Tools/MxFramework.ResourceLibrary/scripts/smoke.mjs
```
