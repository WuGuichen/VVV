# CD-M1：静态目标 Attack Opportunity 与 PrimaryAttack 切换

> Status: Guide
>
> 里程碑：CD-M1
>
> 状态权威：[实施总台账第 4 节](CHARACTER_TEST_CHARACTER_DISTANCE_DEVELOPMENT.md)
>
> 父设计范围：[角色距离计算方案](CHARACTER_TEST_CHARACTER_DISTANCE_CALCULATION.md) 第 7.2、7.4、7.5 节
>
> 本文不维护独立状态；任务是否成立以实际交付记录和总台账状态为准。

## 1. 目标

在 CD-M0 基础上实现无副作用的 static-target PreviewTrace，并把 `PrimaryAttack` 的最终 action eligibility 直接切换到 Attack Opportunity；旧 fact 和特殊动作兼容路径保持显式可见。

## 2. 最小上下文与前置依赖

实施或评审 CD-M1 时只需加载：

- [实施总台账](CHARACTER_TEST_CHARACTER_DISTANCE_DEVELOPMENT.md) 的公共合同、状态和统一门禁。
- [CD-M0 执行文档](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M0_FOUNDATION.md) 的完成 Gate 与实际交付记录。
- [父设计](CHARACTER_TEST_CHARACTER_DISTANCE_CALCULATION.md) 第 7.2、7.4、7.5 节。
- [AI 接口](../Interfaces/AI.md)、[Combat 接口](../Interfaces/Combat.md) 和 [CharacterAction 接口](../Interfaces/CharacterAction.md) 的相关章节。

前置门禁：

- CD-M0 已达到 `Verified`。
- 实际命中链路的 collision/damage mask、blocker、penetration、multi-hit 和 HitZone 解析入口已确认。
- Planner 最终 action eligibility 与零平移转向命令入口已确认。

## 3. 实现内容

- [x] 实现 `CharacterTestAttackOpportunityService` 与无副作用 PreviewTrace 核心。
- [x] target Hurtbox 冻结在决策快照；source 按真实 Active Trace、socket 轨迹和 root motion 推进。
- [x] 复用实际 collision/damage mask、自身忽略、目标资格、blocker 顺序、当前零穿透 direct-hit 合同与 multi-hit policy。
- [x] Preview 的 damage eligibility 直接调用 actual 的 `TryResolveCharacterActionDamage` 入口；缺失或非法 `hitEvent.damage` 失败关闭。M1 只冻结 HitZone identity 并保留现有 `Default/body` fallback，不承诺 M2 前的 Zone 严格校验或伤害/硬直/冲量倍率同构。
- [x] 输出当前姿态、原地转向、必须位移和量化转角。
- [x] 输出最早 action tick offset、trace sample、选中 Hurtbox/Zone、witness/aim point 和失败原因。
- [x] `CandidatePose` 在 CD-M3 前稳定返回 `Unsupported`。
- [x] 当前姿态可命中时，`PrimaryAttack` 提交攻击。
- [x] 只需转向时提交零平移转向，本 Tick 不攻击。
- [x] 必须位移时禁止攻击并继续接近。
- [x] `StaleSnapshot` 或 `Unavailable` 时不攻击并输出诊断。
- [x] `CastAbilitySlot` 等特殊动作保留旧射程判断，并标记 `CompatibilityLegacyRange` 来源。

## 4. 兼容与非目标

- `target.distance` 与 `target.inAttackRange` 的旧根节点语义不变。
- PrimaryAttack 使用 action-level guard，不把新结果反写成旧 fact。
- Attack Opportunity 内部没有旧范围静默回退。
- static-target prediction 冻结目标，不承诺预测目标未来运动。
- 查询中遇到未冻结的其它动态 actor 时返回 `Unavailable/NonTargetActorStateUnavailable`，不把它近似成 world blocker 或可命中目标。
- `Wall` / `Ground` 只有显式 world classification authority 时成立；当前没有正式生产写入方，未知环境碰撞体按 `WorldStatic` fail closed，不用法线等近似猜测类别。
- 不支持 CandidatePose、合法 ApproachPose、Zone lock 或路径成本。

## 5. 自动化测试

- [x] 大型目标根远尾近仍得到有效机会。
- [x] 背后目标在 `CurrentPose` 与 `TurnInPlace` 间给出正确分级。
- [x] source root motion 改变最早命中 tick/sample，且与实际执行一致。
- [x] 快速移动目标在查询内保持静态假设，结果可复现且诊断明确。
- [x] 垂直超界不会被 XZ 根距离误判为可命中。
- [x] 多 blocker、penetration 与 multi-hit 使用真实稳定顺序。
- [x] snapshot/revision mismatch 返回 `StaleSnapshot`，PrimaryAttack fail closed。
- [x] `CastAbilitySlot` 等兼容动作继续旧路径并暴露兼容来源。
- [x] warmed 后 PreviewTrace 与结果归约为 0 B GC allocation。
- [x] 同帧 root、yaw、AuthorityPose/socket 变更由独立 source pose/facing revision 使旧 Plan/adapter witness 失效；相同值写入和读取不推进 revision。
- [x] hit policy ledger、活动 HitTrace window、instance/window ID、hit-once 状态、source revisions 与 CasterBody 跨 tick previous-center authority 进入正式 network snapshot/hash/rollback/replay；schema 24～27 的兼容与真实 direct-hit rollback parity 均有 focused coverage。

## 6. Playable 证据

- [x] Unity PlayMode 6/6 覆盖当前姿态命中、只转向、继续接近、`Unavailable` fail closed、特殊动作 `CompatibilityLegacyRange`，以及确定性合成 compound multi-Hurtbox 的 near-leaf 命中。
- [x] 六项测试驱动真实 `GameSlice -> Runtime AI Planner -> RuntimeCommand -> action guard` 链路；它们是 headless PlayMode 行为证据，不是 EditMode 替代品。
- [x] compound multi-Hurtbox 用例只证明正式多 Hurtbox authority 的 near-leaf 消费；`x=0.95m` 小于旧根距离阈值，不把它误写成 root-far。root-far/leaf-near 粗筛由 CD-M0 focused test 证明。
- 证据边界：本阶段没有保存 Game View framebuffer 截图，因此这些证据不能被描述为画面或美术验收；这不阻断本里程碑的 PlayMode 行为 Gate。

## 7. 完成 Gate

- 大型目标尾部、背后目标、root motion、静态预测、垂直超界、真实 blocker 顺序和 snapshot mismatch focused tests 全部通过。
- CharacterTest PlayMode 已证明 CurrentPose、TurnInPlace、RequiresTranslation、Unavailable fail closed、特殊动作兼容路径和 compound multi-Hurtbox authority 由对应合同控制。
- 特殊技能显式兼容路径无回归，且没有静默 fallback。
- Preview/actual damage eligibility、multi-hit policy、活动窗口、source revision 与 CasterBody previous-center authority 的 snapshot/hash/rollback/replay 门禁通过；不把 M1 的布尔资格评估写成完整 HitZone 倍率解析。
- PlayMode 6/6、full-chain warmed 0 B GC、Unity refresh/compile/domain reload、Editor ready 和 Console 0 error 均已由 root 复证。
- AI、Combat、CharacterAction、CharacterApplication 与 Network 接口文档按实际实现回写。

## 8. 实际提交与验证结果

| 项目 | 当前记录 |
| --- | --- |
| 里程碑状态 | `Verified`；见[总台账 CD-M1](CHARACTER_TEST_CHARACTER_DISTANCE_DEVELOPMENT.md) |
| 实际分支 / 提交 | `main` 工作区实现；尚未提交 |
| 受影响文件 | Attack Opportunity service/adapter、冻结 geometry 与 authority snapshot、共用 trace/policy/damage eligibility、独立 source revision、Runtime/Planner guard、显式 TurnInPlace command/input/network/snapshot/hash/replay、CharacterAction 行为 overlay 与 focused/Playable tests；新增文件均已补 `.meta` |
| focused tests | M0 + M1 core/authority/planner/network 合并复证 107/107（EditMode job `bf5550f87b60457a98636d2500105fd5`）；network authority 整类 10/10（job `eed93d70d9334dd5a290642fafa1c923`） |
| authority tests | schema 24 ledger 兼容、schema 25 active HitTrace window、schema 26 source revisions、schema 27 CasterBody previous-center 的 capture/apply/deep clone/full-owned-non-owned hash/rollback/replay 全部覆盖；真实 CasterBody 用例比较 target、physics candidate、Damage/Duplicate、分支回滚后下一 tick 的完整 sample/query/result parity |
| Unity 验证 | 最终 `validate_unity_scripts` job `2c6c0efec80a41898db63cc3b9d12030`：refresh 完成、compilation started/finished、domain reload observed、Editor ready、Console 0 error |
| Playable 证据 | CharacterTest headless PlayMode 6/6（job `f471da910e824194ba4be952ef2588d6`）；BakedSocket 实际伤害/每事件去重回归 1/1（job `a11aa7259e204f33be8f967591c48cc5`）；无 Game View framebuffer 截图 |
| NoAlloc 证据 | 合并 107/107 内的 full-chain warmed fixture 连续 32 次走 Planner → intent → RuntimeCommands → action guard → submit/apply，断言 0 B GC；Preview/actual warmed 回归同时通过 |
| 接口文档回写 | 本次同步 AI、Combat、CharacterAction、CharacterApplication 与 Network 的实际命中合同 |
| 已知限制 / 阻断 | 无 M1 验收阻断。限制：static target；`CandidatePose` Unsupported；未知环境类别 fail closed；previous-center 状态允许合法稀疏；M1 只保证 damage eligibility/HitZone identity，不承诺 M2 Zone 严格语义或完整倍率解析 |
