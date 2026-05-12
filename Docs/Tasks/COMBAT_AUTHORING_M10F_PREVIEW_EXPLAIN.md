# Combat Authoring M10F：Preview Query / HitResolve Explain v0

> **状态**：已完成
> **优先级**：P0
> 前置任务：`COMBAT_AUTHORING_M10E_1_SHAPE_TRANSFORM_HANDLES.md`
> 派发对象：Runtime Combat / Editor Authoring 子代理

## 目标

让 Combat Authoring 从“能看和能摆 shape”推进到“能解释当前帧为什么会命中或不命中”。测试者应能在 EditorWindow 中看到 generated query、candidate、resolve result 和 reason chain，不依赖 Console 判断战斗预览结果。

## 范围

本阶段实现 Editor Preview Explain v0：

- 从当前 `CombatActionAuthoringAsset`、`CombatSceneBindingAsset` 和当前 frame 生成预览报告。
- 报告至少包含：
  - 当前 Action / Binding / Frame。
  - Generated Queries。
  - Candidate Hits。
  - Resolve Results。
  - Reason Chain 或说明文本。
  - Hash / stable summary key。
- Combat Authoring 窗口提供：
  - `预览 Explain` 按钮。
  - Explain 面板或右侧/底部报告区展示结构化结果。
  - `复制 Explain` 按钮。
- 输出必须稳定排序，不依赖 Unity 查找顺序。
- Console 只允许极少量入口级信息，不作为主要反馈。

## 实现原则

- Preview 逻辑应尽量调用现有 Combat Runtime API：
  - `CombatPhysicsWorld`
  - `CombatQueryResult`
  - `HitCandidate`
  - `HitResolveSystem`
- Editor 层可以做 authoring asset 到 preview DTO 的转换，但不能重新实现 HitResolve 规则。
- 如果 authoring data 暂时不足以生成真实 WeaponTrace，可以先基于当前帧 Hitbox / Hurtbox / Scene Binding 生成最小 query/candidate explain，并明确标记为 `Authoring Preview v0`。
- Report DTO 放在 `MxFramework.Combat.Authoring`，便于后续 CLI / Export 复用。
- UI 展示放在 `MxFramework.Combat.Editor`。

## 建议产物

可按实际代码风格命名，但建议包括：

```text
CombatAuthoringPreviewReport
CombatAuthoringPreviewQuery
CombatAuthoringPreviewCandidate
CombatAuthoringPreviewResolve
CombatAuthoringPreviewExplainer
```

Report 字段建议：

```text
ActionName
ActionId
BindingName
Frame
GeneratedQueries[]
Candidates[]
ResolveResults[]
ReasonLines[]
StableHash
HasRuntimePreview
```

Query / Candidate / Resolve 行必须有稳定 key：

```text
Frame
QueryId
EntityId
BodyId
ColliderId
TraceId / TrackId
ActionId
SourceOrder
Kind
Message
```

## Editor UI 要求

- Combat Authoring 窗口增加按钮：
  - `预览 Explain`
  - `复制 Explain`
- Explain 内容应显示在现有右侧解释面板或底部 Report Preview 附近。
- 空结果要可读，例如：

```text
Frame 0 / Action 1
Generated Queries: 1
Candidates: 0
Resolve Results: 0
Reason:
  - Hitbox 1 generated Sphere query.
  - No candidate body overlapped this query.
Hash: ...
```

不要只写“无结果”或只输出 Console。

## 非目标

- 不做 Runtime Export。
- 不做 JSON package。
- 不要求 Play Mode Showcase 同步加载 authoring asset。
- 不做完整 WeaponTrace root/tip 编辑。
- 不做时间轴 range 编辑。
- 不做复杂 TreeView；v0 可以用只读 TextField / ListView，但结构要清楚。

## 需要修改的主要文件

可能涉及：

- `Assets/Scripts/MxFramework/Combat.Authoring/`
- `Assets/Scripts/MxFramework/Combat.Editor/CombatAuthoringWindow.cs`
- `Assets/Scripts/MxFramework/Tests/Combat/Authoring/`

按需读取 runtime 文件：

- `Assets/Scripts/MxFramework/Combat/Physics/`
- `Assets/Scripts/MxFramework/Combat/Hit/`

## 验收标准

- 打开 `MxFramework > Combat > Combat Authoring`。
- 选择 Action / Binding。
- 点击 `预览 Explain`。
- 窗口中显示当前帧 generated query / candidate / resolve / reason / hash。
- 点击 `复制 Explain` 后可复制完整文本报告。
- 查询、候选、resolve 行稳定排序。
- 没有 Binding、没有 marker、没有 shape 时不崩溃，显示清晰原因。
- Unity Console 无 error。
- Authoring EditMode tests 通过。

## 完成结果

- 新增 `CombatAuthoringPreviewReport`、query / candidate / resolve DTO 和 `CombatAuthoringPreviewExplainer`。
- Preview Explainer 调用 `CombatPhysicsWorld`、`WeaponTraceQueryBuilder` 和 `HitResolveSystem` 生成 Authoring Preview v0。
- Combat Authoring 窗口新增 `预览 Explain`、`复制 Explain`。
- Explain 文本显示 generated query、candidate hit、resolve result、reason 和 stable hash。
- 缺 Action、缺 Binding、缺 marker、无 active shape/trace 时输出可读原因。
- 当前仍标记为 `Authoring Preview v0`，真实 damage、target state、action instance 和完整 runtime WeaponTrace 后续由 M10H / Export 数据接入。

## 测试要求

子代理完成后不要提交 SVN，由主代理复核和提交。子代理至少自查：

- Unity MCP refresh / Console error 检查。
- 相关 EditMode tests。
- 说明是否做过窗口手动验证。

## 提交边界

本任务只允许修改 Combat Authoring / Editor / Tests 相关文件和本任务文档状态。不要改动或提交以下未跟踪本地文件：

- `Assets/CombatActionAuthoringAsset.asset`
- `Assets/CombatSceneBindingAsset.asset`
- `.claude` / `.gitnexus` / `Tools/*.py`
