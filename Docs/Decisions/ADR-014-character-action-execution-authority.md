# ADR-014: CharacterAction 单一执行权威

Date: 2026-08-03

> Status: ADR

## Context

CharacterAction 曾同时暴露 Runner 原始启动/切换/打断/取消入口、通用 Cancel 请求、Reaction 的两套执行路径，以及 CharacterTest 的裸实体清理接口。部分组合根还让 `CombatActionRunner` 与 CharacterAction 同时决定外层动作生命周期。由此产生了以下实际漏洞：普通 CohesionBreak 被打断规则拒绝后仍可清除活动动作；落地、死亡、复活、销毁和配置重载无法表达不同的终止语义；生命周期替换可能先丢失旧实例再发现新动作无效；Replay 还能记录已废弃的取消命令。

这些入口也把解析、执行、下游派发、ECS 投影、快照恢复和决策追踪混在大型控制器中，难以证明拒绝原子性、事件唯一性与稳定帧无分配。

## Decision

`CharacterActionExecutionGateway` 是 CharacterAction 的唯一有状态执行权威。每个执行实体拥有一个 Gateway，Gateway 内部独占不可公开的 `CharacterActionRunner`。第一方代码只能调用：

- `Execute(in CharacterActionExecutionRequest, CharacterActionRunnerEventBuffer)`；
- `Tick(CharacterActionRunnerEventBuffer)`；
- `CaptureState()` / `TryRestore(...)`；
- `CreateDebugSnapshot()`。

执行请求只保留六种明确语义：`StartSlot`、`StartIntent`、`React`、`CompleteExternal`、`ReplaceForLifecycle` 和 `TerminateForLifecycle`。普通动作切换遵守 transition window，普通反应遵守 `interruptRules`；规则为空即拒绝，且不得改变活动实例。`CompleteExternal` 必须校验预期动作 ID，首个正式原因是 `Landed`，并生成正常完成事件。

强制终止只允许发生在 `Death`、`ReviveReset`、`EntityDespawn`、`ConfigurationReload` 和 `RuntimeShutdown` 五种生命周期边界。`ReplaceForLifecycle` 必须先完整验证目标动作，再在同一调用方事件缓冲中终止旧实例并启动新实例；目标无效时，旧实例 ID、动作、帧和状态保持不变。

`CharacterActionRunner`、原始 operation result 及其 Start/RequestCancel/RequestInterrupt/CancelActive 操作均为 internal 实现细节。Reaction 只保留基于预热缓存与调用方缓冲的执行路径。Track 与 Presentation Adapter 也必须使用调用方复用的 `CharacterActionDispatchBuffer`；初始化和扩容允许分配，稳定成功及常规拒绝路径为 NoAlloc。

`CharacterActionController` 只负责正式 CharacterAction，并内部拆为 Request Coordinator 与 Dispatch Coordinator。旧 CombatAction 仅允许作为 CharacterAction Combat Track 的子动作，由 `CharacterActionCombatTrackBridge` 启动，并随外层实例终止；它不得决定外层动作状态。Gameplay 请求只由 `CharacterActionGameplayTrackBridge` 提交。压力控制器只维护独立控制状态，正式动作反应统一经过 `CharacterActionReactionBridge`。

CharacterTest 的 facade 只组合 Catalog、Execution Store、Projection Writer 和 Snapshot Adapter 四个单一所有者。Runtime AI Planner Overlay 同样把 binding switch、entry-action handshake、logic facts 与 decision trace 状态交给各自唯一所有者，facade 不复制可变字段。

CharacterTest 动作命令与 Replay 同步升级为 schema v2：动作命令固定 `payload0=1` 且 `payload1` 为正动作 ID，不再存在取消命令。Replay v1 在播放前返回明确的不支持错误，不做隐式语义迁移。

## Breaking Migration

| 已移除入口或语义 | 替代方式 |
| --- | --- |
| `CharacterActionRunner.Start` / `RequestCancel` | `Gateway.Execute(StartSlot/StartIntent, buffer)` |
| `CharacterActionRunner.RequestInterrupt` | `Gateway.Execute(React, buffer)` |
| `CancelActive` / `ClearEntityAction` | 外部正常结束使用 `CompleteExternal`；生命周期使用 `TerminateForLifecycle` |
| 通用 `CharacterActionRequest.Cancel` | 把输入绑定到具体目标动作，由切换窗口判定 |
| Reaction 强制取消标记 | 普通反应配置 `interruptRules`；生命周期使用 `ReplaceForLifecycle` |
| `CombatCohesionBreakAdapter` | `CharacterActionReactionBridge` → Gateway |
| Controller 直接拥有 Combat/Gameplay 执行设施 | `CharacterActionCombatTrackBridge` / `CharacterActionGameplayTrackBridge` |
| CharacterTest 动作命令 `payload0=0` | schema v2 Start：`payload0=1`、`payload1>0` |
| Replay v1 | 重新录制为 Replay v2；旧文件在播放前明确拒绝 |

## Consequences

- 普通切换和反应拒绝具有可测试的原子性，不再留下“旧动作消失、新动作未启动”的空白帧。
- 外部完成、生命周期替换和生命周期终止各自产生一次且仅一次的明确终态事件。
- CombatAction、Gameplay 与表现层变为下游消费者，CharacterAction 是唯一外层动作真相。
- 公共入口更少，组合根必须显式构建 Gateway、复用事件缓冲并选择正确的请求语义。
- 这是破坏式迁移：旧 Cancel 命令、Runner 调用和 Replay v1 不再兼容；历史记录只能作为证据阅读，不能继续播放。
- RuntimeFrame、Runtime AI Planner SaveState、Network Snapshot 动作字段和行为卡 semantic hash 不改变。

## Alternatives Considered

- 保留 Runner 公共兼容 API 并依靠源码扫描约束：变量改名和新调用点仍可绕过合同，无法形成编译级权威边界。
- 普通反应失败后清理动作：破坏拒绝原子性，并把 Gameplay 压力事实错误提升为动作强制终止权限。
- 先终止再解析生命周期目标：目标无效时会不可逆地丢失旧实例。
- 自动迁移 Replay v1 Cancel：旧 `payload0=0` 无法无歧义还原为外部完成、生命周期终止或具体目标动作切换。
- 保留独立 CohesionBreak CombatAction 适配器：会重新建立第二套外层动作权威。

## References

- `Docs/Interfaces/CharacterAction.md`
- `Docs/Interfaces/CharacterControl.md`
- `Docs/Interfaces/Combat.md`
- `Docs/Guides/CHARACTER_ACTION_AUTHORING_GUIDE.md`
- `Docs/QUALITY_GATE.md`
