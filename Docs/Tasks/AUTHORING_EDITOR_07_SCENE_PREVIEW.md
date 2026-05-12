# 子需求 07：Runtime Preview Scene Target

> **状态**: ✅ 已完成（详情见 `Docs/CAPABILITIES.md`）

## 目标

把 Runtime Preview 从 dummy world 链路验证推进到**场景对象预览**：外部 Buff Authoring Editor 导出的 `mx.runtimeConfigPatch.v1` 文件，可以在 Unity 中加载，并应用到 `RuntimeVerticalSlice.unity` 场景里的指定测试目标身上，回传该目标的属性、Buff、Modifier、ChangeSet 和日志。

本任务不接入 WGame 真实角色、关卡或业务数据，只建立框架级测试沙盒。

## 背景

当前已完成：

- `RuntimeVerticalSlice.unity` 可运行 Attributes / Buffs / Modifiers。
- Runtime Config Slice 已证明 `ConfigTable` / `ConfigRegistry` / Factory 可驱动运行时对象。
- Runtime Config Patch Slice 已证明 `mx.runtimeConfigPatch.v1` 可被 Unity 加载并合并。
- Authoring 08 / 09 已完成：外部编辑器可导出 Runtime Patch v1。
- Unity Preview Server 可启动并响应 JSON-RPC。
- dummy world 可证明 Preview RPC 链路可用。

当前缺口：

- Preview RPC 仍主要面向 dummy world。
- Runtime Patch v1 尚未通过 Preview Server 应用到场景目标。
- 外部编辑器的“运行时预览”还不能反馈场景对象状态。

## 非目标

本任务不做：

- WGame 真实角色接入。
- 真实战斗 AI / 技能 / 怪物逻辑。
- 完整 Mod 包格式。
- 完整血条 UI 或美术表现。
- 新建多个专用预览场景。
- Steam Workshop / Mod 平台上传。
- AI 自动修复。

如果本任务开始处理真实项目角色或完整 Mod 发布，说明范围失控。

## 用户流程

```text
外部编辑器保存 Authoring 草稿
  -> 点击导出运行时 Patch
  -> 生成 mx.runtimeConfigPatch.v1
  -> Unity 打开 RuntimeVerticalSlice.unity
  -> 场景中存在 PreviewCaster / PreviewTarget
  -> Unity Preview Server 启动
  -> 外部编辑器点击运行时预览
  -> Preview Server 加载 Runtime Patch v1
  -> 根据 casterId / targetId 找到场景目标
  -> 应用 Buff / Modifier 并推进 tick
  -> 场景目标显示 HP / Attack / Buff / ChangeSet
  -> 外部编辑器收到 RuntimePreviewResult
```

## 场景策略

复用现有场景：

```text
Assets/Scenes/RuntimeVerticalSlice.unity
```

不新建 `BuffPreview.unity`。原因：

- 当前所有运行时垂直切片已经集中在 `RuntimeVerticalSlice.unity`。
- 继续新增场景会增加维护成本和心智负担。
- Scene Target Preview 是当前垂直切片的自然延伸，不需要独立场景。

场景对象建议：

| 对象 | 作用 |
| --- | --- |
| `PreviewCaster` | 施法者 / 来源对象 |
| `PreviewTarget` | 被施加 Buff 的目标对象 |
| `RuntimeSliceRunner` | 保留现有垂直切片入口 |
| `PreviewRuntime` | 可选，承载预览状态显示和简单 tick 驱动 |

如果需要创建或修改场景，必须通过 Unity Editor / Unity MCP，不手写 `.unity` YAML。

## 组件

建议新增：

```text
Assets/Scripts/MxFramework/Preview/Runtime/MxPreviewSceneTargetConfig.cs
Assets/Scripts/MxFramework/Preview/Runtime/MxPreviewSceneTarget.cs
```

职责：

- `MxPreviewSceneTargetConfig` 作为场景中的编辑态配置组件。
- `MxPreviewSceneTarget` 作为运行时动态生成的测试 Buff 目标。
- 运行时目标实现或包装 `IBuffTarget`。
- 配置组件暴露稳定 `TargetId` 和初始属性。
- 运行时目标持有 `AttributeStore`、`BuffPipeline`、`ModifierPipeline`。
- 运行时目标可从 Runtime Patch v1 的 merged config 创建 Buff / Modifier。
- 运行时目标提供快照给 Preview Server。
- 配置组件在 Scene View 显示 Gizmo，运行时目标可在 Game View 显示调试信息。

建议字段：

| 字段 | 默认值 | 说明 |
| --- | --- | --- |
| `TargetId` | `TestTarget` / `TestCaster` | 外部预览用的目标 ID |
| `InitialHp` | `1000` | 初始 HP |
| `InitialAttack` | `100` | 初始攻击 |
| `InitialDefense` | `20` | 初始防御 |
| `ResetOnPreviewRun` | `true` | 每次预览前是否重置属性和 Buff |
| `ShowOverlay` | `true` | 是否显示运行时调试文本 |
| `CreateRuntimeTarget` | `true` | Preview Server 启动时是否从该配置生成运行时目标 |

## Preview World

建议新增：

```text
Assets/Scripts/MxFramework/Preview/Runtime/ScenePreviewWorld.cs
```

职责：

- 实现 `IPreviewWorld`，或作为 `DummyPreviewWorld` 前面的优先路径。
- 根据 `casterId` / `targetId` 查找动态生成的 `MxPreviewSceneTarget`。
- 如果场景中没有运行时目标，先从 `MxPreviewSceneTargetConfig` 生成。
- 加载 Runtime Patch v1。
- 使用 `RuntimeConfigPatchJsonLoader` + `RuntimeConfigPatchMerger` 合并 Base + Patch / Mod。
- 构建 `ConfigRegistry`。
- 使用 `ConfigBuffFactory` / `ConfigModifierFactory` 主路径创建 Buff / Modifier。
- 推进 tick。
- 收集属性变化、Buff 变化、伤害 / tick、ChangeSet 和日志。

要求：

- 不依赖 WGame。
- 不修改 Base 数据。
- 目标缺失时可以回退 dummy world，但必须明确记录 `previewMode=dummy`。
- 找到场景目标时必须记录 `previewMode=scene`。
- Runtime Patch v1 解析失败时返回明确错误，不允许静默回退 Base。

## 外部编辑器要求

首版不重做 UI，只调整运行时预览结果展示：

- 继续使用现有 `/api/preview/run`。
- 默认 `target=TestTarget`、`caster=TestCaster`。
- 右侧 Runtime Preview 面板显示当前模式：`scene` 或 `dummy`。
- 如果使用 scene 模式，显示 HP、Attack、Buff 列表、ChangeSet 摘要。
- 如果回退 dummy，显示“未找到场景目标，已使用 dummy world”。
- 如果 Runtime Patch v1 加载失败，显示错误并阻止预览继续。

## Runtime Patch v1 输入

本任务的预览输入必须优先使用 Authoring 08 / 09 导出的 Runtime Patch v1。

首版默认路径可沿用：

```text
Assets/StreamingAssets/MxFramework/Demo/runtime_config_patch.json
```

验收时不再以旧 `buff.patch.json` 作为 Unity 场景预览主输入。旧 Authoring 草稿只负责编辑，运行时预览使用导出的 Runtime Patch v1。

## 验收标准

- Unity 编译无项目 error。
- 不新建场景，仍使用 `Assets/Scenes/RuntimeVerticalSlice.unity`。
- 场景中存在 `PreviewCaster` 和 `PreviewTarget`。
- Unity Preview Server 启动后，外部编辑器点击 `运行时预览` 不报连接错误。
- Preview Server 能加载 `mx.runtimeConfigPatch.v1`。
- 找到场景目标时，结果标明 `previewMode=scene`。
- Runtime Patch v1 中 Buff / Modifier 通过 `ConfigBuffFactory` / `ConfigModifierFactory` 主路径创建。
- Patch 示例 modifier 使 `PreviewTarget.Attack` 从 `100 -> 180`。
- Buff tick 使 `PreviewTarget.Hp` 发生可见变化。
- Game View 或 overlay 可看到目标 HP、Attack、Buff 和 ChangeSet。
- `RuntimePreviewResult.attributeChanges` 包含场景目标属性变化。
- `RuntimePreviewResult.logs` 包含 ChangeSet 摘要或 patch sourceId。
- 连续点击预览时，如果 `ResetOnPreviewRun=true`，结果稳定从初始值开始。
- 删除或禁用场景目标后，预览回退 dummy world，并有清晰提示。
- Runtime Patch v1 格式错误时返回明确错误，不静默回退 dummy 或 Base。

## 测试建议

### Unity

- 使用 Unity MCP 刷新脚本并检查 Console error。
- 使用 Unity MCP 打开 `RuntimeVerticalSlice.unity`。
- 创建或确认 `PreviewCaster` / `PreviewTarget`。
- 启动 Preview Server。
- 通过外部编辑器 `/api/preview/status` 检查连接。
- 通过 `/api/preview/run` 检查结果。

### CLI / API

```bash
curl -sS http://127.0.0.1:4873/api/preview/status
curl -sS -X POST -d '' 'http://127.0.0.1:4873/api/preview/run?package=Tools/MxFramework.Authoring/samples/buff-preview&buff=100001&target=TestTarget&caster=TestCaster&stack=1&waitTicks=60'
```

### 自动化

- 保留现有 Mock Preview Server 测试。
- 新增场景目标查找的 EditMode 测试。
- 新增 Runtime Patch v1 加载失败的预览错误测试。
- 如果 PlayMode 测试成本可控，补一个 scene preview smoke test。

## 文档更新

完成实现后必须更新：

- `Docs/AUTHORING_EDITOR_USAGE.md`
- `Docs/Tasks/AUTHORING_EDITOR_03_RUNTIME_PREVIEW.md`
- `Docs/ROADMAP.md`

文档必须明确：

- dummy preview 是 RPC 链路验证。
- scene target preview 是框架级真实测试入口。
- 当前仍不接入 WGame 真实业务。
- 运行时预览主输入是 Runtime Patch v1，不是 Authoring 草稿。

## 状态

`Implemented (r1163)`

## 依赖

- 子需求 03：游戏运行时实时预览。
- 子需求 08：Authoring Core / CLI 导出 Runtime Patch v1。
- 子需求 09：Authoring Editor 导出运行时 Patch 按钮。
- `RUNTIME_CONFIG_PATCH_SLICE_01_FILE_DRIVEN_OVERRIDE.md`。
- Unity Preview Server 已可启动。

## 风险

- Edit Mode tick 和 Play Mode tick 行为可能不一致，首版必须在文档中说明。
- 场景对象与 dummy world 的 reset 语义必须一致，否则预览结果会不稳定。
- Runtime Patch v1 和 Preview RPC 的错误边界需要清晰，否则 UI 会误报“预览成功”。
- 如果过早接入 WGame 真实角色，会破坏框架纯净性；本任务禁止这样做。
