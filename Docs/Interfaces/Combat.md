# Combat 接口

Combat 模块提供 noEngine 的确定性战斗基础，包括固定帧、动作时间轴、动作运行态、物理查询和后续命中结算桥接。Runtime 逻辑不读取 Unity Animator、Unity Physics 或 `Time.deltaTime` 作为权威输入。

## Animation / Action Runtime

| 类型 | 语义 |
|------|------|
| `CombatActionTimeline` | 静态动作时间轴，定义 `TotalFrames`、Startup / Active / Recovery 阶段、窗口和帧事件 |
| `CombatActionRegistry` | 低频 timeline 注册表，按 action id 注册、查询和移除动作配置 |
| `CombatActionRunner` | 动作运行时状态机，维护每个 `CombatEntityId` 当前动作、local frame、phase 和单调 `ActionInstanceId` |
| `ActionResult` | 可恢复操作结果；成功路径返回 instance id，失败路径提供诊断 reason |
| `ActionStartedEvent` / `ActionPhaseChangedEvent` / `ActionFinishedEvent` / `ActionCanceledEvent` / `ActionCancelRejectedEvent` | runner 显式回调事件 payload |
| `ICombatActionTraceProvider` | 动作状态到外部 `WeaponTraceFrame` 配置的查询接口 |
| `CombatActionTimelineTraceProvider` | 默认 trace 注册表，按 `(actionId, localFrame)` 注册和查询武器轨迹 |
| `CombatWeaponTraceEvaluator` | 读取 active action，构造 capsule query，调用 `CombatPhysicsWorld` 并输出 `HitCandidate` |
| `CombatHitCollector` | 基于 `WeaponHitOnceKey` 去重并按 `HitCandidate.CompareTo` 稳定排序候选 |

`StartAction(entityId, actionId, currentFrame)` 在实体没有动作时直接启动；实体已有动作时只在当前动作的 `Cancel` window 允许 `nextActionId` 时替换，否则返回失败并发布拒绝事件。`ForceStartAction` 和 `ForceCancel` 是调试或外部强制状态切换入口，不参与普通取消规则。

查询 API：

```csharp
CombatActionState? GetActionState(CombatEntityId entityId);
CombatActionPhase GetCurrentPhase(CombatEntityId entityId);
int GetActionInstanceId(CombatEntityId entityId);
bool IsInCancelWindow(CombatEntityId entityId, int nextActionId);
bool IsInInvincibleWindow(CombatEntityId entityId);
bool IsInParryWindow(CombatEntityId entityId);
bool IsInSuperArmorWindow(CombatEntityId entityId);
CombatActionState[] GetRunningActions();
```

## 约束

- `MxFramework.Combat` 保持 `noEngineReferences=true`。
- `CombatActionRunner.TickActions` 由调用方按固定帧推进；表现层只能消费状态和事件。
- `ActionInstanceId` 是每次启动动作递增的运行时 id，可用于后续 `WeaponHitOnceKey` 去重。
- `CombatWeaponTraceEvaluator` 默认把目标状态写为 `HitTargetStateFlags.Alive`，避免后续 `HitResolveSystem` 将未注入状态的候选误判为 dead；游戏层可通过构造函数注入目标状态解析函数。
