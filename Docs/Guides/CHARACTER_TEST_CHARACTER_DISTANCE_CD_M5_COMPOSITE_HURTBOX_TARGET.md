# CD-M5：角色 + 受击框复合目标与精确净空距离

> 状态：Implementation Complete / Focused Verified
>
> 前置：CD-M0～CD-M4、Completed Frame Geometry

## 目标与合同

CD-M5 将战斗目标拆为稳定 Actor identity 与持久 `LogicalHurtboxId`。首次 Actor lock 以 source 当前姿态胶囊到目标合法 Hurtbox 表面的三维净空选择最近 leaf；同一 `GameplayEntityId` 下不因动作、动画、距离、Opportunity、Zone priority 或部位状态变化重新选择。

- `target.distance`：锁定 Hurtbox 的三维表面净空，Fix64 求值、向上量化到毫米。
- `target.distanceAvailable`：锁定 Hurtbox 完成帧几何是否可解析。
- `target.rootDistance`：原 Transform-root XZ 距离。
- `target.inAttackRange`：正式 PrimaryAttack 对锁定 Hurtbox 的 CurrentPose 命中机会。
- Hurtbox 消失或被切断时保留 Handle，距离与攻击失败关闭；Actor identity 变化才是 `TargetChanged`。

实际 Combat Trace 仍决定真正命中的 collider；锁定 leaf 只约束距离、接敌、机会预判和瞄准，不覆盖真实物理命中。

## 实现边界

- `CombatPhysicsWorld.CalculateCapsuleDistance` 复用现有最近点内核，支持 AABB、Sphere、Capsule、OBB/compound leaf，返回 gap 与双侧 witness。
- Hurtbox Bake 在量化 leaf pose 前使用动画 clip 的 `RootT.*` 与 `RootQ.*` 曲线反变换到 motion-root 局部空间；Runtime 只再应用一次角色权威 Transform，禁止把 locomotion 累计根位移同时烘进 Hurtbox local shape。
- Completed Geometry 双缓冲冻结 root、姿态胶囊和 Hurtbox shape；DamageZone/member 仅在角色包提供时作为可选高级分组冻结。没有 DamageZone 的角色仍直接从 Hurtbox 建立复合锁与精确距离。Pair 根距离与定向投影保持 CD-M0～M4 语义。
- AI 与玩家 Target Lock 共享 `CharacterTestCombatTargetHandle`；同 Actor steady state 只解析 exact Hurtbox。
- 正式子目标身份是 `GameplayEntityId + LogicalHurtboxId`；`DamageZoneId` 可为空，不能再阻止 Hurtbox acquisition、witness、AimPoint、attack opportunity 或 snapshot restore。
- CD-M4 对当前 Actor 只评估锁定 leaf；challenger Actor 使用按 GeometryGeneration memo 的 provisional nearest leaf。
- Projectile 初始 AimPoint、Chain Lightning 首目标、HUD reticle 和锁定相机 anchor 使用锁定 Hurtbox；发射后 Homing 继续 Actor tracking。
- Runtime AI Lab Game View 与 Editor Scene Preview 始终只绘制一条目标指示线：锁定 Hurtbox 样本可用时连接 `SourceWitnessPoint → TargetWitnessPoint`；精确样本尚不可用时使用同一 Completed Geometry 的冻结 Actor endpoints 保持指示连续。它不绘制测距文本、端点球或第二条 root XZ 辅助折线；fallback 只是预览可用性，不改变 `target.distanceAvailable` 或 Gameplay 距离合同。

本阶段不实现被命中、角色部位接触、玩家主动切部位、动作变化、周期刷新或其它 Hurtbox 重选触发器。

## 版本迁移

| 合同 | 旧版 | 当前 |
| --- | ---: | ---: |
| Published Behavior Runtime Contract | 6 | 7 |
| Runtime AI Planner State | 13 | 14 |
| CharacterTest Network Snapshot | 33 | 34 |
| CharacterTest Replay | 3 | 4 |

- v6 行为卡在普通兼容加载中把旧 `TargetDistance` 迁移为 `TargetRootDistance`；当前仓库正式卡通过 Publisher 显式发布为 v7，保留 `TargetDistance` 时即采用新净空语义。
- schema 34 持久化玩家稳定 Actor/Hurtbox identity，不持久化 gap、witness、AimPoint、CombatColliderId 或 cache generation。
- Planner schema 13 的 lock identity 可迁移到 14；旧 action/opportunity/revision 字段不再参与 lock authority hash。
- Replay v3 明确拒绝，避免用新距离语义冒充旧 replay parity。

## 验收摘要

- Combat shape distance focused EditMode：4/4，含 4096 次 warmed 0 B。
- Composite lock / behavior migration / schema / body-part rollback focused EditMode：39/39。
- Completed Geometry、Target Lock compatibility focused EditMode：10/10。
- CD-M4 candidate/selection focused EditMode：18/18。
- CD-M2 PlayMode：5/5，包含首次锁定、同 Actor 不重选、距离刷新、玩家复合锁、schema 34 rollback roundtrip 与 4096 次 warmed cache 0 B。
- CD-M4 代表性 16v16 校准：1/1，零 typed truncation；既有 production budget 仍覆盖峰值。
- `codex_flow_test` locomotion Hurtbox Root Motion 归一化 EditMode：1/1；run/walk 最大已观测 head planar offset 从 `3.322m` 降至 `0.305m`。
- CD-M2 Root Motion 归一化后 PlayMode：6/6；Hurtbox-only 角色移动 36 Tick 后所有 leaf 相对权威根保持在 `1.5m` 身体范围内。
- 距离指示 UI / Scene Preview focused EditMode：7/7；复合锁 PlayMode 复验：5/5。
- Unity final job `66d2844a08ad4abaae2a7be60e85d918`：refresh、3 assemblies compile、domain reload、Editor ready、Console 0 error。

完整 Projectile/Chain Lightning action-path 回归在当前工作区被既有 CharacterAction fixture 的 `TryStartIntent=false` 阻断，失败发生在进入 CD-M5 AimPoint 消费前；恢复对应 ActionSet 基线后仍需补一次直接全链复验。其它已知限制为本页声明的无重选触发器、post-launch Homing 仍按 Actor tracking，以及 local-offline/loopback 代表规模边界。
