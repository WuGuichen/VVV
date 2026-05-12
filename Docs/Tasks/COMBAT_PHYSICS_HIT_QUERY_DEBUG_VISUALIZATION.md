# Combat Physics：命中查询调试与可视化

> **状态**：Accepted / Closed（2026-05-09）
> **优先级**：P0
> **功能包类型**：制作侧可见功能
> **前置完成项**：
> - `COMBAT_PHYSICS_M11D_1_SHAPE_QUERY_CONTRACT.md`
> - `COMBAT_PHYSICS_M11D_2_BROADPHASE_V0.md`
> **设计依据**：
> - `Docs/COMBAT_ANIMATION_PHYSICS.md`
> - `Docs/Tasks/COMBAT_ANIMATION_PHYSICS_EPIC.md`
> - `Docs/Tasks/COMBAT_PHYSICS_M11D_2_BROADPHASE_V0.md`

## 功能目标

把 Combat Physics 从“测试里能证明查询正确”推进到“制作人能在运行时看懂一次命中查询为什么命中或未命中”。

## 校准记录

本文件原状态仍写作 `Ready for dispatch`，但当前代码和测试已经出现命中查询 explain / debug 数据链路的实现证据。文档校准时先标为 `Implemented / Closeout pending`；2026-05-09 已完成正式 closeout，状态收口为 `Accepted / Closed`。

已发现的实现证据：

- `CombatPhysicsQueryDebugReport` 已输出 shape、raw candidate、dedup candidate、post-filter candidate、hit count 和 summary lines。
- `CombatPhysicsWorld.ExplainQuery(...)` 已作为公开 explain 入口。
- `RuntimeCombatShowcaseRunner` 已保存最近一次 query debug report，并能生成 `shape raw=... dedup=... post=... hit=...` 摘要。
- `CombatPhysicsBroadphaseTests` 和 `CombatPhysicsDebugVisualizationTests` 已覆盖 candidate count、dedup / post-filter 文本和稳定解释签名。

## Closeout 记录

验收日期：2026-05-09。

实现范围确认：

- `CombatPhysicsQueryDebugReport` 输出 `ShapeKind`、raw candidate、dedup candidate、post-filter candidate、hit count 和 summary lines。
- `CombatPhysicsWorld.ExplainQuery(...)` 是公开 explain 入口，query / batch result 会携带 debug report。
- `RuntimeCombatShowcaseRunner` 记录最近一次 query explain，并通过 `LastQueryDebugSummary` 暴露制作侧可读摘要。
- Combat Showcase 场景中存在并激活 query visual 节点：`Combat_Query_Line_00/01/02`、`Combat_Query_Endpoint_00/01`、`Combat_Hit_Result_Marker`。
- Authoring explain 测试已验证 `Physics Query Debug` 文本、Hit / Miss / FilteredSource / FilteredLayer 行和稳定签名。

Play Mode 验收记录：

- 场景：`Assets/Scenes/CombatAnimationPhysicsTest.unity`。
- Runner：`RuntimeCombatShowcase`，组件包含 `RuntimeCombatShowcaseRunner`、`RuntimeCombatShowcaseUi`、`MxRuntimeHudController`、`RuntimeCombatShowcaseInputController`、`UIDocument`。
- Ray probe 命中：`Ray raw=6 dedup=3 post=1 hit=1`。
- Sphere far probe 未命中：`Sphere raw=10 dedup=2 post=0 hit=0`，交互摘要 `Probe Player->Enemy: miss`。
- Sphere near probe 命中：`Sphere raw=16 dedup=4 post=1 hit=1`，交互摘要 `Probe Player->Enemy: hit distance=1.000000`。
- Capsule attack 命中：`Capsule raw=12 dedup=3 post=1 hit=1`，交互摘要 `Attack Player->Enemy: Damage damage=110`。
- 可视化节点与最后一次 query 同步：`Combat_Hit_Result_Marker` active，位置更新到命中点，query endpoints active。

验收命令与结果：

- `dotnet build WGameFramework.sln --no-restore -v minimal`
  - 通过，0 warning / 0 error，用时 00:00:02.89。
- Unity MCP EditMode `MxFramework.Tests.Combat`
  - 通过，105 / 105 passed，0 failed，0 skipped，用时 5.029s。
- Unity Console
  - 通过，0 error，0 warning。
- `Tools/GitNexus/gitnexus.sh detect-changes`
  - low risk；41 files / 130 symbols；Affected processes 0。

剩余风险：

- 当前可视化仍是 Showcase 级反馈，不是完整 authoring gizmo 或专业调试器。
- Candidate marker 以 query line、endpoint 和 hit result marker 为主；完整 candidate 列表仍主要通过 debug report / HUD 摘要表达。
- 后续如果要支持更细的制作排查，应增加独立 debug panel，显示每个 candidate row 的 body / collider / layer / filter reason / hit reason。

完成后，Play Mode Showcase 或等价验证场景里应能演示：

1. 选中一次武器轨迹、范围探测或手动 query。
2. 看见 query shape、起点、终点、范围、layer / source / filter 摘要。
3. 看见 broadphase 收集了哪些 candidate、过滤了哪些 candidate、最终 narrowphase 命中了哪些目标。
4. 对未命中结果能解释原因，例如没有 broadphase candidate、layer 被过滤、source 被排除、shape 未相交。
5. HUD 或调试面板显示最近一次 query explain，不依赖 Console 作为主要反馈。

这个功能包面向“命中查询调试与可视化”完整体验，不再拆成微任务编号。子代理内部可以拆 runtime explain、demo binding、UI、tests，但顶层交付必须以可演示功能闭环验收。

## 用户可见结果

- 制作人进入 Combat Showcase 后，可以触发一次探测或攻击，并立即看到命中查询的可视化反馈。
- 场景中有 query shape / trace / hit marker / filtered candidate marker 的基础表现。
- HUD 或调试区显示：
  - query id / frame / shape kind；
  - raw candidate count；
  - dedup candidate count；
  - post-filter candidate count；
  - final hit count；
  - 最近若干 candidate 的 body / collider / layer / filter reason / hit reason；
  - `preview/debug` 文本状态，说明本次结果来自 Combat Physics explain，而不是 Unity Physics。
- 同一输入重复触发时，可视化顺序和文本顺序稳定。

## 范围

允许修改：

- Combat Physics explain / debug report 的最小扩展。
- Combat Demo / Runtime Showcase 的查询调试绑定。
- Combat UI / HUD 中用于展示 query explain 的小面板。
- Combat tests / Showcase tests / PlayMode smoke tests。
- 本任务文档和 Epic 中的当前下一步说明。

建议关注文件范围：

```text
Assets/Scripts/MxFramework/Combat/Physics/
Assets/Scripts/MxFramework/Combat/Diagnostics/
Assets/Scripts/MxFramework/Demo/
Assets/Scripts/MxFramework/UI.Toolkit/
Assets/Scripts/MxFramework/Tests/Combat/
Assets/UI/MxFramework/Showcase/
Assets/Art/MxFramework/Showcase/
Docs/Tasks/COMBAT_PHYSICS_HIT_QUERY_DEBUG_VISUALIZATION.md
Docs/Tasks/COMBAT_ANIMATION_PHYSICS_EPIC.md
```

如果现有 Showcase 不适合承载完整显示，可以先做一个轻量 debug overlay，但必须仍然从 Combat Physics 的公开 explain / snapshot 数据读取，不允许直接窥探临时私有状态。

## 非目标

- 不做完整物理引擎、刚体、摩擦、堆叠、关节或连续碰撞求解。
- 不接入 Unity Physics / Unity Collider 作为权威查询结果。
- 不改变 M11D.1 / M11D.2 已验证的命中语义。
- 不修改 Gameplay / Buff / Ability 业务逻辑。
- 不引入 WGame 真实角色、技能、Buff 或关卡数据。
- 不做复杂美术表现、后处理或特效。
- 不做 Authoring Editor 的完整 Gizmo 工具；本包只做运行时调试与演示。
- 不用 Console log 作为验收输出。

## 串行链路

本功能包内部必须保持以下先后顺序：

1. 稳定 explain 数据契约：确认现有 `CombatPhysicsQueryDebugReport` 是否足够表达 candidate / filter / hit reason，不足时最小扩展。
2. Runtime debug snapshot：把单次 query explain 包装成 Showcase / HUD 可读取的数据，不让 UI 直接拼接 Physics 内部对象。
3. 场景可视化：用 snapshot 驱动 query shape、candidate、hit / miss marker。
4. HUD / 调试面板：展示本次 query 的计数、原因和排序后的 candidate 列表。
5. E2E smoke：Play Mode 里触发一次探测或攻击，确认场景表现和 HUD 文本同步。

没有 explain 数据契约前，不先做 UI；没有 Runtime snapshot 前，不把 Demo 直接绑死到 Physics 私有实现。

## 可并行工作

在 explain 数据契约确定后，可以并行派发：

- Runtime / Physics 子代理：补充 explain reason、snapshot builder、稳定排序测试。
- Demo / Visualization 子代理：接入 query shape、candidate marker、hit / miss marker。
- UI 子代理：做 HUD debug panel，显示 counts 和 candidate rows。
- Test 子代理：补 EditMode contract tests 和 PlayMode / visual tree smoke tests。

这些子任务只通过 explain / snapshot 契约协作，不互相改对方内部状态。

## 验收标准

功能验收：

- Play Mode Showcase 中能触发至少一种武器 trace 或手动 query，并看到 query explain。
- 至少覆盖 Capsule 或 Sector 中的一种攻击/探测形状；Ray / AABB / Sphere 如已存在入口，不得回归。
- HUD 显示 raw candidate、dedup candidate、post-filter candidate、final hit 的数量。
- 至少能解释三类结果：
  - 命中；
  - broadphase 无 candidate 或 narrowphase 未相交；
  - layer / source / filter 排除。
- 场景表现和 HUD 文本来自同一次 query id / frame，不出现上一帧视觉和当前文本错配。
- 同一输入重复执行，candidate row 和 hit row 顺序稳定。

技术验收：

- Combat Physics Runtime 不引用 `UnityEditor`。
- Combat Physics Runtime 不引用 `UnityEngine.Physics`。
- 纯 Physics explain 不依赖 Demo / UI / Gameplay。
- 不改变已有 query hit 结果。
- 不让 `Dictionary` 枚举顺序或 GameObject 遍历顺序影响最终显示顺序。
- Console 最终无 error。

文档验收：

- 本文档记录最终实现范围和演示方式。
- Epic 的当前下一步指向本功能包。
- 如果发现必须拆后续包，只记录为后续功能点，不把当前包拆回微任务列表。

## 测试门槛

最低测试：

```text
dotnet build WGameFramework.sln --no-restore -v minimal
Unity EditMode: MxFramework.Tests.Combat.Physics.*
Unity EditMode: MxFramework.Tests.Combat
Unity Console: 0 error
Tools/GitNexus/gitnexus.sh detect-changes
```

建议补充：

- Debug report / snapshot builder 的 EditMode 测试。
- Candidate reason / hit reason 的排序稳定性测试。
- Runtime Showcase visual tree 或 PlayMode smoke，确认 HUD debug panel 存在。
- 若可通过 Unity MCP 读取场景对象，确认 query visual、candidate marker、hit marker 至少各有一个可观测节点。

验收输出不要只写“测试通过”，必须记录：

- 触发了哪种 query shape；
- 目标命中或未命中的原因；
- HUD 显示的四个 count；
- Console 最终状态；
- GitNexus 风险等级。

## 完成后演示方式

推荐演示脚本：

1. 打开 Combat Showcase 或当前 Runtime Showcase 战斗场景。
2. 选中 Player 或测试攻击源。
3. 触发一次探测或攻击。
4. 镜头里观察 query shape / trace / candidate / hit marker。
5. HUD 中读取最近一次 query explain：
   - `shapeKind`
   - `rawCandidateCount`
   - `dedupCandidateCount`
   - `postFilterCandidateCount`
   - `hitCount`
   - candidate / hit reason rows
6. 移动目标到范围外再次触发，确认未命中原因可读。
7. 切换 layer / source 过滤输入，确认 filter reason 可读。

演示通过标准：制作人无需打开 Console 或测试日志，就能判断“这次攻击为什么命中 / 为什么没命中”。

## 派发提示

派发给子代理时使用功能包口径：

```text
你负责 WGameFramework 功能包 `Combat Physics：命中查询调试与可视化` 的实现。
目标不是新增一个小接口，而是交付 Play Mode 可演示的命中查询解释闭环。
先读 AGENTS.md、COMBAT_PHYSICS_HIT_QUERY_DEBUG_VISUALIZATION.md、COMBAT_PHYSICS_M11D_2_BROADPHASE_V0.md。
不要回退他人改动。
只在文档允许范围内修改文件；如需越界，先说明原因。
完成后必须跑 build、Combat 测试、Unity Console 检查和 GitNexus，并记录演示步骤。
```

## 当前记录

- 2026-05-09：创建功能包任务文档。
- 2026-05-09：文档校准发现 explain / debug report 数据链路已存在，状态从 `Ready for dispatch` 调整为 `Implemented / Closeout pending`。下一步补 Play Mode 可视化与验收记录。
- 2026-05-09：正式 closeout 完成，build / Unity Combat EditMode / Play Mode query explain / Console / GitNexus 均通过，状态收口为 `Accepted / Closed`。
