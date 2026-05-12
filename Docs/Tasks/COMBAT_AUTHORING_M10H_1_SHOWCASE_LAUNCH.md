# Combat Authoring M10H.1：Authoring -> Runtime Showcase Launch v0

> **状态**：已完成
> **优先级**：P0
> 前置任务：`COMBAT_AUTHORING_M10G_EXPORT_RUNTIME_JSON.md`、`COMBAT_AUTHORING_M10I_5_VALIDATION_QUICK_ACTIONS.md`
> 设计依据：`COMBAT_AUTHORING_GIZMO_TOOL_DESIGN.md` 的 Runtime Showcase 衔接、制作侧“做成一个小游戏才能直观感受开发进程”的验收口径
> 派发对象：Editor / Demo 子代理

## 背景

Combat Authoring 现在已经能创建 Action / Binding、编辑 shape、拖时间轴、做 validation quick action、Preview Explain 和 JSON export。但用户从编辑器切到游戏体验还没有一条直观路径：当前数据能不能在 Play Mode 里跑、跑起来看到什么、是否和 Authoring 预览一致，都还需要手动拼流程。

本阶段要补上第一条“从 Authoring 窗口进入 Runtime Showcase”的闭环。目标不是做最终 Runtime 数据加载器，而是让制作人能在当前 Combat 测试场景中用鼠标手测刚编辑的 Action / Binding，并在 HUD / 场景反馈中看到来源和结果。

## 目标

在 `Combat Authoring` 窗口提供一个明确入口，例如 `运行 Showcase 预览`：

1. 使用当前 `CombatActionAuthoringAsset` 和 `CombatSceneBindingAsset`。
2. 先执行 validation gate；有 Error 时禁止启动，并显示中文原因和 quick action 提示。
3. Edit Mode 下可打开 Combat 测试场景并进入 Play Mode。
4. Play Mode 下自动找到或创建 `RuntimeCombatShowcaseRunner`，把当前 authoring 上下文应用到 showcase。
5. 进入游戏后 HUD / 事件日志能显示当前来源 Action / Binding、ActionId、TraceId、验证状态。
6. 现有鼠标操作仍可用：选角色、拖动移动、右键移动、探测、攻击、Trace、Resolve、Snapshot。

## 范围

本阶段做 v0，优先打通可体验路径：

- Editor 侧新增会话缓存：
  - 保存当前 Action / Binding 的 GUID。
  - 只在 Unity Editor Play Mode 启动链路使用，不写 Runtime Core。
  - Play 启动失败或用户清空后要能恢复，不留下隐式脏状态。
- `CombatAuthoringWindow` 新增入口按钮和中文提示：
  - `运行 Showcase 预览`
  - `停止 / 清除预览会话` 如实现成本低可加入，否则至少提供状态提示。
- Play Mode 进入后由 Editor 桥接逻辑执行：
  - 读取会话 GUID。
  - 找到当前场景中的 `RuntimeCombatShowcaseRunner`，没有则创建临时 GameObject。
  - 根据 Binding 的 marker target path 解析 Player / Enemy marker。
  - 将 actionId、首个 weaponTrace traceId、marker transform、来源摘要传给 runner。
- Runtime Demo 侧只暴露普通 public 配置方法或小型配置 struct：
  - 不直接依赖 UnityEditor。
  - 不让 `MxFramework.Demo` 必须引用 `MxFramework.Combat.Editor`。
  - 不要求场景预挂新组件。
- HUD / event log 至少显示：
  - `Authoring Preview: <ActionAsset> / <BindingAsset>`
  - `ActionId=<id> TraceId=<id>`
  - validation 状态：通过 / 有 warning。

## 技术约束

- 不把 Authoring asset 变成 Runtime Core 的运行时依赖。
- 不改 Combat Core / Physics / HitResolve 权威逻辑。
- Runtime Demo 可以增加一个无 Editor 依赖的配置入口；Editor 桥接负责把 authoring 数据翻译成 runner 参数。
- 如果 Binding 找不到 marker：
  - 不静默移动或创建场景对象。
  - 在 Authoring 窗口和 runner event log 中显示中文错误。
  - 允许 fallback 到现有 `Combat_Player_Marker` / `Combat_Enemy_Marker`，但必须标记为 fallback。
- 进入 Play Mode 前必须 validation 无 Error；Warning 可继续，但 HUD / log 要显示。
- 所有 Editor 侧入口都要中文 tooltip，说明“这是测试预览，不是导出 Runtime 数据”。

## 建议实现文件

可按现有代码风格调整，但建议范围控制在：

```text
Assets/Scripts/MxFramework/Combat.Editor/CombatAuthoringWindow.cs
Assets/Scripts/MxFramework/Combat.Editor/CombatAuthoringShowcasePlaySession.cs
Assets/Scripts/MxFramework/Demo/Combat/RuntimeCombatShowcaseRunner.cs
Assets/Scripts/MxFramework/Demo/Combat/RuntimeCombatShowcaseUi.cs
Assets/Scripts/MxFramework/Tests/Combat/Authoring/...
Docs/Tasks/COMBAT_AUTHORING_M10H_1_SHOWCASE_LAUNCH.md
```

建议数据结构：

```text
CombatAuthoringShowcasePlaySession
  ActionGuid
  BindingGuid
  RequestedAt
  ValidationSummary

RuntimeCombatShowcaseAuthoringConfig
  SourceSummary
  ActionId
  TraceId
  PlayerMarker
  EnemyMarker
  ValidationSummary
```

建议流程：

```text
CombatAuthoringWindow.RunShowcasePreview()
  -> Validate(action, binding)
  -> if errors: show report and stop
  -> save session GUIDs
  -> open CombatAnimationPhysicsTest scene if needed
  -> EditorApplication.EnterPlaymode()

InitializeOnLoad playModeStateChanged
  -> EnteredPlayMode
  -> load session GUIDs
  -> find/create runner
  -> resolve markers from binding targetPath
  -> runner.ApplyAuthoringPreviewConfig(config)
```

## 非目标

- 不做正式 runtime JSON package loader。
- 不做外部 Authoring Editor 启动游戏。
- 不做复杂动画烘焙或真实角色动画。
- 不改变 Runtime Showcase 现有鼠标手测交互。
- 不提交本地临时 Action / Binding asset。

## 验收标准

- 在 Edit Mode 打开 `MxFramework > Combat > Open Authoring Layout`。
- 选择当前测试 Action / Binding，点击 `运行 Showcase 预览`。
- 若 validation 有 Error：
  - 不进入 Play Mode。
  - 窗口显示中文错误提示，并保留 validation report / quick actions。
- 若 validation 无 Error：
  - 自动进入 `CombatAnimationPhysicsTest` Play Mode。
  - 场景中不需要预挂新的 bridge 组件；runner 可被找到或动态创建。
  - HUD / event log 显示当前 Action / Binding 来源和 ActionId / TraceId。
  - 鼠标选中、移动、探测、攻击、Trace、Resolve 仍可用。
  - Console 无 error。
- Play Mode 中点击 `Trace` / `Resolve` 后场景表现和 HUD 仍正常。
- 停止 Play 后不会把临时 runner 或 bridge 对象保存进场景。
- Unity MCP 编译通过，Authoring / Combat 相关 EditMode tests 通过。

## 测试要求

子代理完成后不要提交 SVN，由主代理复核和提交。子代理至少自查：

- Unity MCP refresh / Console error 检查。
- Authoring 相关 EditMode tests。
- Runtime Combat / Demo 相关测试（如已有）。
- 手动从 Authoring 窗口点击 `运行 Showcase 预览`：
  - error gate 路径。
  - valid play launch 路径。
  - Play Mode 下 Trace / Resolve 路径。

## 完成记录

- 已在 `Combat Authoring` 窗口新增 `运行 Showcase 预览` 与 `清除预览会话` 入口；启动前执行 validation gate，Error 会阻断并显示中文处理提示，Warning 允许进入 Showcase。
- 新增 Editor-only `CombatAuthoringShowcasePlaySession`，使用 `SessionState` 保存当前 Action / Binding GUID、请求时间和验证摘要；Edit Mode 下会打开 `Assets/Scenes/CombatAnimationPhysicsTest.unity` 并进入 Play Mode，Play Mode 下找到或临时创建 `RuntimeCombatShowcaseRunner` 并应用配置。
- Demo runtime 侧新增无 `UnityEditor` 依赖的 `RuntimeCombatShowcaseAuthoringConfig` 和 `ApplyAuthoringPreviewConfig`，支持来源摘要、ActionId、首个 weaponTrace TraceId、Player / Enemy marker、validation 摘要和 marker fallback / error 摘要；HUD 与 event log 会显示 authoring 来源和验证状态。
- Binding marker 由 Editor 桥接按 actor markerId -> marker targetPath 解析；targetPath 找不到时会记录中文 fallback / error 摘要，并可 fallback 到 `Combat_Player_Marker` / `Combat_Enemy_Marker`，不会静默创建或移动场景对象。
- 已补充 `RuntimeCombatShowcaseRunnerTests.ApplyAuthoringPreviewConfig_UpdatesSummaryAndEventLogWithoutEditorDependency`，覆盖 runtime 配置入口和 event log / marker 应用。
- 验证记录：Unity MCP scripts refresh 编译通过，Console error 为 0；`MxFramework.Tests` EditMode `MxFramework.Tests.Combat` 相关测试 62/62 通过。
- 主代理手动验收：
  - 制造 `TotalFrames=1 / Startup=0-24` 后点击 `运行 Showcase 预览`，Play Mode 未启动，窗口显示 validation Error 阻断和中文修复提示。
  - 修复为 valid Action / Binding 后点击 `运行 Showcase 预览`，自动打开 `CombatAnimationPhysicsTest` 并进入 Play Mode。
  - Play Mode HUD 显示 `Authoring Preview: CombatActionAuthoringAsset / CombatSceneBindingAsset`、`ActionId=400001`、`TraceId=7` 和 `validation: 通过`。
  - 运行 Trace / Resolve 后场景显示 trace / hit 表现，event log 出现 `WeaponTrace generated` 与 `HitResolve: Damage`，Enemy HP 从 600 降到 490。
  - 停止 Play 后 scene 文件无 SVN 修改，临时 preview runner 未保存进场景。
- 未修改任务禁止的本地未跟踪 Action / Binding asset、`.claude`、`.gitnexus`、`Tools/*.py`。

## 提交边界

本任务只允许修改 Combat Authoring / Editor / Demo / Tests 相关文件和本任务文档状态。不要提交或改动以下未跟踪本地文件：

- `Assets/CombatActionAuthoringAsset.asset`
- `Assets/CombatSceneBindingAsset.asset`
- `.claude` / `.gitnexus` / `Tools/*.py`
