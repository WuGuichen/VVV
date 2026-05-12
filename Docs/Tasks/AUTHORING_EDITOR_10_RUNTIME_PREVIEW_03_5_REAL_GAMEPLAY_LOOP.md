# Authoring Editor 10：Runtime Preview 03.5 Real Gameplay Loop

> **状态**: Completed / Verified 2026-05-09（03.5A done, 03.5B done, 03.5C done, 03.5E done）
> **优先级**: P0
> **归属**: 子需求 03 Runtime Preview 的 03.5 `Preview Verified`
> **目标**: 把 `preview.applyBuff` 从 DummyPreviewWorld / 2003 边界推进到真实框架 Gameplay 闭环。

## 背景

Runtime Preview 03.2-03.4 已经完成 Authoring 端 RPC 客户端、Unity 侧 `PreviewRpcServer` 雏形和外部编辑器 UI/API 接入。当前链路可以握手、加载 Patch、返回结构化错误；但 `applyBuff` 在缺少真实 `IBuffFactory` / runtime config resolver 时仍可能停在 `2003`，只能证明 RPC 和 UI 转发可用，不能证明真实 Buff 运行时结果可用。

校准说明：03.5 已完成。03.5A Runtime Adapter、03.5B Runtime Config Resolver closeout、03.5C Preview Protocol / Result Mapping、03.5E UI Status Polish 均已有完成记录和验证命令。外部编辑器现在只消费稳定 preview result 字段展示状态，不重算运行时规则。

03.5 的任务是把 Preview Server 接入已有框架运行时能力：

- `IBuffFactory` / `ConfigBuffFactory<TConfig>`。
- `RuntimeConfigPatchJsonLoader` / `RuntimeConfigPatchMerger`。
- Phase 11 的 `RuntimeAbilityConfigResolver` / `RuntimeConfigChangeSummary` 语义。
- `GameplayDiagnosticSnapshotBuilder` 和 M3/M4 Showcase diagnostic 字段映射。
- 现有 `PreviewRpcServer` 03.4 协议和连接发现。

完成后，外部 Buff 编辑器点击运行时预览时，`applyBuff` 应回传真实 Buff 状态、属性变化、日志、性能和错误，而不是只返回 DummyPreviewWorld 的 `2003` 预期边界。

## 目标

03.5 的目标是建立框架级真实闭环：

```text
Authoring Editor draft
  -> Runtime Patch v1 export
  -> PreviewRpcServer loadPatch
  -> Runtime config resolver / merged registry
  -> IBuffFactory creates configured Buff
  -> ScenePreviewWorld or runtime sandbox applies Buff to target
  -> tick
  -> GameplayDiagnosticSnapshot + Preview logs
  -> RuntimePreviewResult
  -> Editor UI displays status / attributes / buffs / errors
```

必须做到：

- `preview.loadPatch` 使用 Runtime Patch v1 / runtime config patch loader 主路径，不只把 patch 存在内存里。
- `preview.applyBuff` 通过真实 `IBuffFactory` 创建并挂载 Buff。
- 结果映射到 `RuntimePreviewResult.buffSnapshots`、`attributeChanges`、`damageTicks` 或可解释日志。
- `RuntimeConfigChangeSummary` 和 `GameplayDiagnosticSnapshot` 中的关键信息进入 preview result 或 logs。
- 失败时返回稳定错误码和 `errors[]`，UI 不把失败显示成成功。

## 硬依赖

| 依赖 | 状态要求 | 03.5 使用方式 |
| --- | --- | --- |
| Phase 11 Config Change Handling | `RUNTIME_GAMEPLAY_06_CONFIG_CHANGE_APPLY.md` 已完成 | 复用 config source / changed ids / failed ids / no retroactive Buff semantics |
| M3 Config / Patch / Rebuild Panel | Phase 12 M3 已完成 | 对齐 `RuntimeConfigChangeSummary` 展示语义，不抢改 Showcase UI |
| M4 Diagnostic View | `UI_SHOWCASE_04_DIAGNOSTIC_VIEW.md` 已完成 | 复用 `GameplayDiagnosticSnapshot` 字段映射和 DTO 缺口记录 |
| PreviewRpcServer 03.4 | `AUTHORING_EDITOR_03_RUNTIME_PREVIEW.md` 03.4 已完成 | 保持 JSON-RPC 方法、错误码、连接描述文件和外部编辑器 API |
| Scene Target Preview | `AUTHORING_EDITOR_07_SCENE_PREVIEW.md` 已完成 | 优先使用 scene target，缺失时允许明确 dummy fallback |

如果这些依赖的代码或文档状态与本表不一致，先暂停 03.5 实现并补齐依赖，不在 03.5 内重新设计这些能力。

## 非目标

03.5 不做：

- 不导入 WGame 真实 Buff、角色、怪物、关卡或私有配置。
- 不做完整 Mod 平台、上传、订阅、冲突 UI 或权限系统。
- 不重写 Combat、AIAction 或 Runtime Showcase UI。
- 不做复杂网络安全升级；继续使用 03 协议定义的本机 WebSocket、token 和连接描述文件。
- 不做完整 Ability 编辑器，也不把 ability cast 作为 `applyBuff` 的必经路径。
- 不修改已挂载 Buff / Modifier 的配置回溯语义。
- 不把 Authoring Core、CLI 或外部编辑器绑定到 UnityEditor。

## 子任务拆分

### 03.5A Runtime Adapter

目标：让 Preview runtime 拥有一个真实、可测试的 Gameplay 适配层。

建议边界：

- 在 `Assets/Scripts/MxFramework/Preview/Runtime/` 内新增或收敛 adapter，不改 Combat / AIAction 试点文件。
- 从 `loadPatch` 结果构建 runtime config registry / config source。
- 提供 `ApplyBuff(casterId, targetId, buffId, stack, durationOverrideMs, waitTicks)` 主入口。
- 使用 `IBuffFactory` 主路径创建 Buff；如果缺少配置或工厂，返回 `2003` + 结构化原因。
- 推进 tick 并收集属性变化、Buff 快照和日志。
- 对齐 Phase 11：配置变更只影响后续新建 Buff / Modifier，不回溯已挂载实例。

验收：

- 有纯 C# 或 EditMode 测试覆盖有效 Buff 创建、未知 Buff、缺失目标、tick 后快照。
- `applyBuff` 成功时不再依赖 DummyPreviewWorld 成功假数据。
- dummy fallback 必须在 logs 中标明 `previewMode=dummy`，scene 成功标明 `previewMode=scene`。

完成记录（2026-05-09）：

- 新增 `RuntimePreviewAdapter`，统一 `applyBuff / tick / snapshot / reset` 的 runtime 边界。
- `PreviewRpcServer` 的 `applyBuff` 和 `getSnapshot` 已通过 adapter 收集 Buff / Attribute / Damage / Status 快照，结果扩展 `previewMode`。
- `applyBuff` 失败返回 `2003`，并在 JSON-RPC error `data` 中带 `reason / previewMode / buffId / targetId`。
- `ScenePreviewWorld` 与 `DummyPreviewWorld` 暴露 preview mode；dummy fallback 和 scene 成功路径都会写入 `previewMode=...` 日志。
- `MxPreviewBootstrap.StartServer(buffFactory)` 已把自定义 `IBuffFactory` 传入 fallback dummy world，避免无场景目标时绕过 factory 主路径。
- 未做 03.5B 的 Runtime Patch v1 resolver / change summary 接入；`loadPatch` 的真实 registry 合并仍归 03.5B。

### 03.5B Runtime Config Resolver Integration

目标：把 Authoring 导出的 Runtime Patch v1 变成预览可用的 runtime config source。

建议边界：

- 复用 `RuntimeConfigPatchJsonLoader` / `RuntimeConfigPatchMerger`，不新增第二套 Patch 格式。
- `loadPatch` 记录 source id、loaded patch ids、change set 和 merge warnings。
- 维护最近一次 `RuntimeConfigChangeSummary` 或等价摘要，供 `applyBuff` / `getSnapshot` 映射。
- Patch 解析失败走 `2001`；运行时拒绝或 merge 失败走 `2002`。

验收：

- Runtime Patch v1 中 Buff / Modifier patch 能影响后续新建 Buff / Modifier。
- 错误输入不会污染上一份可用配置，除非 `discardPrevious=true` 语义明确要求清空。
- `preview.reset` 后 runtime config source 和目标状态的清理规则可测试。

完成记录（2026-05-09）：

- `ScenePreviewWorld.LoadPreviewPatch(...)` 已从 `preview.loadPatch.params.rawSource` 或直接 Runtime Patch v1 JSON 调用 `RuntimeConfigPatchJsonLoader.Load(...)` 与 `RuntimeConfigPatchMerger.Merge(...)`，不再读取固定 `StreamingAssets` patch 文件。
- 已将合并后的 `BasicBuffConfig` / `BasicModifierConfig` 注册进 `ConfigRegistry`，并重建 `ConfigBuffFactory<BasicBuffConfig>` / `ConfigModifierFactory<BasicModifierConfig>`；有效 Patch 能影响后续新建 Buff / Modifier。
- merge 后会做运行时表校验；解析失败映射 `2001`，非法引用 / runtime rejected 映射 `2002`，失败不会污染上一份已提交 config source。
- `preview.reset` 清理目标运行状态和当前 preview config source；后续 `applyBuff` 需要重新成功 `loadPatch`。
- `preview.loadPatch.result` 与后续 `RuntimePreviewResult` 暴露 `configMetadata.sourceId / layer / loadedPatchIds / changedConfigIds / failedConfigIds / mergeWarnings`，并在 logs 写入 source / changed / failed 摘要。03.5C 可以直接消费这些字段，不需要重新解析 patch。

### 03.5C Preview Protocol / Result Mapping

目标：保持 03.4 协议不破坏，同时补齐真实结果映射。

建议边界：

- 不改 JSON-RPC 方法名和连接描述文件结构。
- 只在兼容范围内扩展 `RuntimePreviewResult` 字段内容和 `logs/errors/performance`。
- 从 `GameplayDiagnosticSnapshot` 映射：
  - Entity Buffs -> `buffSnapshots`
  - AttributeChanged Events -> `attributeChanges`
  - Last failure / config errors -> `errors`
  - Config source / change summary -> `logs` 或 result metadata
- 明确无法映射的 DTO 缺口，例如 event timestamp、attribute display name、buff display name，只写日志或文档，不在本任务补 Runtime DTO。

验收：

- 成功结果包含 `success=true`、`appliedBuffId`、至少一个 Buff snapshot 或可解释日志。
- 属性变化存在时进入 `attributeChanges`，没有变化时必须有明确日志说明。
- 失败结果包含稳定 `error.code` 或 result `errors[]`，message 能定位缺配置、缺目标、缺工厂或 tick 失败。
- 单个 result 遵守 03 协议的 1 MB 软上限和日志截断语义。

完成记录（2026-05-09）：

- `PreviewRpcServer` 成功路径已将 runtime snapshot 映射到 `RuntimePreviewResult.success / previewMode / appliedBuffId / buffSnapshots / attributeChanges / damageTicks / statusChanges / logs / performance / configMetadata`。
- `PreviewRpcServer` 可共享 bootstrap/world 的 `PreviewLogBuffer`，因此 `ScenePreviewWorld` 的 patch loaded/rejected、missing target、config registry 等日志能进入 result `logs` 与 `preview.getLogs`。
- 对无属性变化或无伤害 tick 的成功结果写入解释日志，避免 UI 把空数组误读成协议失败。
- `applyBuff` 失败保持 03.4 JSON-RPC `error.code=2003`，并在 `error.data.result.errors[]` 中提供稳定 result 错误形状；reason 覆盖 missing runtime patch、invalid buff id、unknown buff/config、missing target、missing dummy factory。
- 单个 result 按 1 MB 软上限从旧到新裁剪 inline logs，并设置 `truncated=true`。
- 新增 Preview EditMode tests 覆盖成功映射、zero-delta no attribute log、unknown Buff、missing target、malformed patch residue、result log truncation。

### 03.5D Tests / Fixtures

目标：为后续并行实现提供可复现的最小样例和防回归测试。

建议边界：

- 使用框架级 demo config / sample package，不引入 WGame 真实数据。
- 保留 Mock Preview Server 测试，不把网络测试变成 Unity PlayMode 的唯一验证。
- 新增 adapter / resolver / result mapper 的小粒度测试。
- 可选增加一条端到端 smoke：`loadPatch -> applyBuff(waitTicks=60) -> getSnapshot -> reset`。

建议 fixtures：

- 有效 Buff：能挂载、持续、tick，并造成属性变化或稳定 Buff 状态。
- Modifier patch：能证明 patch 后新实例使用新配置。
- 无效 Buff：未知 id / 参数非法 / 缺 modifier config。
- 目标缺失：scene target 不存在时的 dummy fallback 或明确失败。

验收：

- 测试覆盖成功、失败、reset、连续预览、配置变更后新实例生效。
- 测试名称能直接表达 03.5 行为边界。
- 所有新增测试不依赖外部网络、不依赖 WGame 私有数据。

2026-05-09 测试记录：

- 新增 `RuntimePreviewAdapterFixtureTests`，使用测试内 `IBuffFactory` / fixture Buff 驱动 `DummyPreviewWorld`，为 03.5A adapter 提供纯 EditMode contract tests。
- 覆盖 apply / tick / snapshot / reset 成功链路、连续预览 reset 隔离、无效 Buff 输入失败不污染状态、配置变更只影响新实例，以及 `PreviewError.ApplyBuffFailed = 2003` 的 result 错误契约。
- 该测试不依赖外部网络、Play 场景或 WGame 私有数据；03.5A 完成后可将同一 fixture 迁移到真实 adapter 主路径断言。

### 03.5E UI Status Polish

目标：外部编辑器只做结果呈现和状态文案收口，不重做 Runtime Showcase UI。

建议边界：

- 触碰范围限于 Authoring Editor preview panel / EditorServer preview API。
- 展示 `previewMode`、loaded patch ids、Buff 状态、属性变化、日志和错误。
- 对 `2001/2002/2003/2004` 给出清晰状态，不吞掉 `errors[]`。
- 不修改 `GameplayShowcase.uxml` / `.uss`、Runtime HUD、Combat 或 AIAction 试点文件。

验收：

- 未启动 Preview Server 时仍显示 unavailable，不返回 500。
- 成功预览和失败预览在 UI 上可区分。
- 如果 result 来自 dummy fallback，UI 明确显示 fallback 状态。
- UI 只消费 Preview result，不在前端重算 Buff 或属性。

完成记录（2026-05-09）：

- Authoring Preview DTO 已补齐 `previewMode / configMetadata / structured error reason`，与 03.5C 稳定字段对齐。
- EditorServer `/api/preview/status` 和 `/api/preview/run` 对未启动或不可连接 Preview Server 返回 `status=unavailable` 的 JSON 响应，不走 HTTP 500。
- `preview.applyBuff` 的 JSON-RPC 失败会映射成 `status=failed`，保留 `error.code / reason / previewMode / error.data.result.errors[]`，UI 可直接呈现。
- 外部预览面板展示 connection、scene/dummy fallback、config metadata、Buff snapshots、attribute changes、damage ticks、status changes、logs、errors、performance 和 `truncated`。
- Authoring tests 覆盖 unavailable、03.5C 字段反序列化、apply failure 结构化结果；Authoring solution build 和仓库级 build 均通过。

## 串行与并行关系

必须串行：

1. 03.5A Runtime Adapter 先定义 apply / snapshot / reset 的运行时边界。
2. 03.5B Runtime Config Resolver Integration 接在 adapter 之后完成 loadPatch 到 factory 的主路径。
3. 03.5C Preview Protocol / Result Mapping 在 A/B 的数据形状稳定后收口。
4. 端到端验收必须最后执行。

可以并行：

- 03.5D Tests / Fixtures 可以与 03.5A 并行先落 fixture 和失败用例，再随实现补成功断言。
- 03.5E UI Status Polish 可以与 03.5C 后半并行，但只能基于已确认的 result 字段，不能提前发明前端专用协议。
- 文档更新可与任一实现子任务并行，但必须以本任务文档为入口。

不建议并行：

- 多个代理同时改 `PreviewRpcServer` / `ScenePreviewWorld` / result DTO 映射文件。
- 03.5E 与 Runtime Showcase M3/M4 UI 文件并行。

## 文件边界

允许触碰：

```text
Assets/Scripts/MxFramework/Preview/Runtime/
Assets/Scripts/MxFramework/Preview/Editor/
Assets/Scripts/MxFramework/Tests/Preview/
Assets/Scripts/MxFramework/Tests/Config.Runtime/
Tools/MxFramework.Authoring/
Tools/MxFramework.Authoring.Editor/
Docs/Tasks/
Docs/AUTHORING_EDITOR_USAGE.md
Docs/CAPABILITIES.md
```

谨慎触碰，仅当适配必须且有测试：

```text
Assets/Scripts/MxFramework/Config.Runtime/
Assets/Scripts/MxFramework/Gameplay/
Assets/Scripts/MxFramework/Buffs/
Assets/Scripts/MxFramework/Attributes/
```

禁止触碰：

```text
Assets/Scripts/MxFramework/Combat/
Assets/Scripts/MxFramework/AIAction/
Assets/UI/MxFramework/Showcase/GameplayShowcase.uxml
Assets/UI/MxFramework/Showcase/GameplayShowcase.uss
Assets/Scripts/MxFramework/UI.Toolkit/
WGame 真实数据或私有运行时代码
```

如后续实现发现必须修改禁止触碰文件，先拆新任务并让负责人确认，不在 03.5 内顺手改。

## 验收标准

03.5 完成必须满足：

1. `preview.handshake/loadPatch/applyBuff/getSnapshot/getLogs/reset` 仍兼容 03.4 客户端。
2. `loadPatch` 使用 Runtime Patch v1 / runtime config patch loader 主路径。
3. `applyBuff` 通过真实 `IBuffFactory` / config-driven factory 创建 Buff。
4. 有效 Buff 预览返回 `success=true`，并包含真实 Buff 状态。
5. 有属性变化时，`RuntimePreviewResult.attributeChanges` 包含 before / after / source。
6. `GameplayDiagnosticSnapshot` 或等价 runtime snapshot 进入 result 映射，不只依赖 Console 文本。
7. 配置变更摘要进入 logs 或 errors，能看出 source、changed、rebuilt、failed。
8. 缺 Buff、缺目标、Patch 解析失败、运行时拒绝和非预览模式分别返回清晰错误。
9. 连续预览和 `reset` 后结果稳定，不把上一次 Buff 状态泄漏到下一次。
10. 未启动 Preview Server 时外部编辑器仍保留离线校验和合并预览能力。
11. 不引入 WGame 真实业务数据，不修改 Combat、AIAction 试点文件。
12. Unity Console 无编译 Error；相关 EditMode / PlayMode / dotnet 测试按任务要求通过。

## 测试要求

最低测试矩阵：

| 场景 | 要求 |
| --- | --- |
| valid patch + valid buff | `loadPatch` 成功，`applyBuff` 成功，Buff snapshot 可断言 |
| valid patch + waitTicks | tick 后 remaining / attribute change / logs 可断言 |
| unknown buff id | 返回 `2003` 或 result error，不崩溃 |
| malformed patch | 返回 `2001`，不污染上一份有效配置 |
| runtime rejected patch | 返回 `2002`，带 merge / validation reason |
| target missing | 返回明确错误或带 `previewMode=dummy` 的 fallback 日志 |
| reset | 清理目标 Buff、日志游标和临时 patch 规则符合文档 |
| consecutive previews | 第二次结果不携带第一次残留，除非明确选择累积模式 |
| config change | 新挂载 Buff / Modifier 使用新配置，已挂载实例不回溯 |
| UI/API status | unavailable / success / failed 三类状态可区分 |

建议命令由实现任务按实际测试程序集补齐；本拆分文档不要求当前运行。

## 风险

| 风险 | 影响 | 约束 |
| --- | --- | --- |
| Preview adapter 绕过真实 factory | 03.5 仍是假闭环 | 测试必须断言 `IBuffFactory` / config-driven 路径 |
| Patch 格式分叉 | Authoring 与 Runtime 结果不一致 | 只复用 Runtime Patch v1 |
| Snapshot DTO 缺字段 | UI 误以为信息完整 | 缺口写入 logs / 文档，不临时扩 DTO |
| reset 语义不清 | 连续预览不稳定 | adapter 必须定义 reset 和累积模式 |
| 多代理抢改 UI / Preview server | 合并风险高 | 按子任务文件边界串行修改关键文件 |
| 过早接入 WGame 真实数据 | 破坏框架纯净性 | 只使用框架 demo fixtures |
| 错误被前端吞掉 | 手测误判成功 | UI/API 必须显示 code、message、errors |

## 交付物

- Runtime adapter / resolver / mapper 的实现和测试。
- 至少一个框架级 preview fixture 或 sample package。
- 外部编辑器 preview panel 的状态展示收口。
- `Docs/Tasks/AUTHORING_EDITOR_03_RUNTIME_PREVIEW.md` 更新 03.5 状态和入口。
- `Docs/CAPABILITIES.md` / `Docs/AUTHORING_EDITOR_USAGE.md` 在实现完成后更新真实能力描述。

## 完成定义

当本任务完成时，`AUTHORING_EDITOR_03_RUNTIME_PREVIEW.md` 的 03.5 可标记为 `Preview Verified`，并且 `Docs/CAPABILITIES.md` 中 Unity Preview Server 能力不再描述为 Dummy / 2003 边界，而是框架级真实 Buff 闭环。
