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
| `ActionStateToHitTargetAdapter` | 将 `CombatActionRunner` 的无敌 / 振刀 / 霸体窗口查询桥接为 `HitTargetStateFlags` |
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

## Hit Resolve

| 类型 | 语义 |
|------|------|
| `HitCandidate` | 武器轨迹 / 物理查询产出的命中候选，携带 attacker / target / action / trace / frame / target state 和结算排序权重 |
| `HitResolveSystem` | 稳定排序候选，执行 hit-once 去重、owner 防护、阵营过滤、动态目标状态过滤和伤害结果生成 |
| `HitResolveResult` | 结构化结算结果，包含 attacker / target / action / trace / frame、`HitResolveKind`、damage、stagger 和 knockback |
| `HitResolveKind` | 结算结果枚举；`SelfDamage` 表示 attacker 命中自身，`Friendly` 表示友方 / 同队且未开启友伤 |
| `ITeamRelationProvider` | 游戏层阵营关系查询接口，提供 hostile / friendly / same-team 判断 |
| `IHitTargetStateResolver` | 动态目标状态解析接口，用于结算时从运行时状态覆盖 `HitCandidate.TargetState` |
| `ICombatEventDispatcher` | 命中结算事件派发接口，接收所有 resolved result，并对 blocked result 提供独立回调 |

`HitResolveSystem.Resolve(candidates, consumedHitOnceKeys, results)` 保持旧签名不变。新增重载允许传入 `ITeamRelationProvider`、`IHitTargetStateResolver`，以及 `allowFriendlyFire`。默认不允许 friendly fire；当 provider 判定双方非 hostile 且 same-team / friendly 时输出 `HitResolveKind.Friendly`。`SetEventDispatcher` 是可选注入点，未设置时结算保持纯结果列表输出。
