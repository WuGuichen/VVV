# CD-M2-T2：DamageZone Runtime Binding、聚合与 Revision

> Status: Guide
>
> 工作包：CD-M2-T2
>
> 工作包状态权威：[CD-M2 总控第 3 节](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M2_DAMAGE_ZONE.md)
>
> 里程碑状态权威：[实施总台账第 4 节](CHARACTER_TEST_CHARACTER_DISTANCE_DEVELOPMENT.md)
>
> 前置：CD-M2-T1 `Verified`
>
> 本文只建立 Runtime Zone authority 与聚合快照；不实现稳定选择、持久化 Zone lock、CharacterStudio 或 CD-M3 ApproachPose。

## 1. 任务结果

让 CharacterTest Runtime 从正式 `geometry_binding.json` 构建可验证的 DamageZone binding，并在 sealed Hurtbox authority 上生成稳定、可失效、warmed 0 B GC 的 Zone aggregate snapshot，供后续选择器消费。

## 2. 最小上下文与前置检查

- [CD-M2 总控](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M2_DAMAGE_ZONE.md)、[T0 冻结记录](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M2_T0_CONTRACT_AUDIT.md) 和 [T1 实际交付](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M2_T1_AUTHORING_COMPILER.md)。
- [CD-M0](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M0_FOUNDATION.md) 的 geometry/revision snapshot 合同。
- [CD-M1](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M1_ATTACK_OPPORTUNITY.md) 的 sealed CombatPhysics/Hurtbox authority、静态目标和 stale 规则。
- [CharacterApplication](../Interfaces/CharacterApplication.md) 与 [Combat](../Interfaces/Combat.md) 的 RuntimeSpawn、Hurtbox 和 CombatPhysics 章节。
- `Packages/com.vincent.wgameframework/AGENTS.md`、`Docs/API_STANDARDS.md`、`Docs/QUALITY_GATE.md`。

开工前确认 Runtime 只消费 T1 编译结果，绝不直接读取 `Authoring/CharacterPackages/**/geometry/damage_zones.json`。

## 3. 实现范围

### 3.1 Runtime Binding

- [ ] 扩展现有 Character RuntimeSpawn/import binding，使 T1 的 DamageZone 投影进入 CharacterTest Runtime。
- [ ] 在构建期校验稳定 Zone ID、HitZone 唯一归属、属性和优先级；binding 与 Runtime Hurtbox 身份不一致时 fail closed 并给出 typed diagnostic。
- [ ] 缺少 DamageZone 投影时保持旧 Hurtbox 可直接命中和瞄准，但不伪造可长期接近 Zone。
- [ ] 不在 Runtime 建立第二份可编辑 Zone 真源或启发式分组。

### 3.2 Zone Aggregate Snapshot

- [ ] 按 T0 冻结的模拟阶段读取上一已完成帧的 sealed Hurtbox authority。
- [ ] 将有效 Hurtbox 按 HitZone → DamageZone binding 聚合为冻结坐标空间中的 bounds 和稳定成员列表/索引。
- [ ] `Hittable`、`Aimable`、`Approachable` 分别保存和查询，不能互相推出。
- [ ] 空 Zone、全部成员禁用、部分成员失效和 target entity 销毁均返回冻结合同规定的状态。
- [ ] aggregate 结果以稳定 ID 排序；broadphase、collider 或容器返回顺序不得成为隐式决胜。
- [ ] 预分配/复用热路径存储，warmed 条件下构建、查询和枚举为 0 B GC。

### 3.3 Revision 与 Stale

- [ ] 定义并实现 binding revision、Hurtbox aggregate revision 和 Zone aggregate revision 的推进条件。
- [ ] 位置/姿态变化与身份/成员资格变化按 T0 合同分别处理，避免每次浮点抖动都产生错误身份 revision。
- [ ] 部位禁用、成员变化、binding reload 和 target authority 变化使旧 Zone snapshot/query 失效。
- [ ] CD-M1 Attack Opportunity 若收到 revision 不一致的 Zone witness，必须返回 `StaleSnapshot` 或冻结的等价失败，不得回退根距离。

## 4. 候选影响区域

- `Packages/com.vincent.wgameframework/Character.RuntimeSpawn*/`
- `Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems/Character/`
- `Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems/Combat/`
- `Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/World/`
- 对应 CharacterRuntimeSpawn、CharacterTest distance M2 focused tests。

实际改动不得扩展到 Planner lock、network snapshot 或 CharacterStudio。

## 5. 自动化测试

计划由单一 focused class `CharacterTestCharacterDistanceM2RuntimeTests`（或 T0 冻结的等价类）拥有 T2 用例；不得把 T3/T4 断言混进同一 fixture。执行时记录 Unity Test Runner/MCP 的精确 filter、通过数、失败数和 job id。

- [ ] 单 Zone/单 Hurtbox、单 Zone/多 Hurtbox、多 Zone/多 Hurtbox聚合正确。
- [ ] 随机打乱 Runtime Hurtbox、collider 和 binding 输入顺序，aggregate 身份、bounds 和枚举顺序稳定。
- [ ] Hittable/Aimable/Approachable 的八种布尔组合保持正交。
- [ ] 缺少作者投影时旧 Hurtbox 命中/瞄准兼容成立，Approachable 不被伪造。
- [ ] 重复/未知 binding 在 Runtime 构建期 fail closed，不产生部分合法的伪 aggregate。
- [ ] 部位禁用、成员移除、target 销毁和 binding revision 改变会推进正确 revision 并使旧查询 stale。
- [ ] 仅姿态更新按冻结规则更新 bounds，不错误改变稳定身份。
- [ ] warmed 后连续构建与查询 Zone aggregate 为 0 B GC。

## 6. Unity 验证

- C# 修改完成后执行 Unity refresh/compile、等待 domain reload 和 Editor ready，并确认 Console error count 为 0。
- focused EditMode/PlayMode 只证明 Runtime binding/aggregation/revision；不得在本任务宣称 Planner 稳定选择或最终可玩闭环。
- 记录测试入口、通过数、失败数、job/环境和执行日期。

## 7. 完成 Gate

- 正式 Compiler 输出能被 Runtime 消费，且 Authoring JSON 没有 Runtime 直读路径。
- 聚合、正交属性、顺序扰动、失效和 NoAlloc 测试全部通过。
- 旧包兼容与非法 binding fail-closed 都有测试。
- Unity 完整脚本验证通过，实际交付记录完整。
- T2 `Verified` 后才允许 T3 接入稳定选择。

## 8. 实际提交与验证结果

| 项目 | 当前记录 |
| --- | --- |
| 工作包状态 | 见[CD-M2 总控第 3 节](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M2_DAMAGE_ZONE.md) |
| 实际分支 / 提交 | `main` 脏工作区，基线 `84d3d084d`，尚未提交 |
| 实际修改文件 | RuntimeSpawn/import binding、`CharacterTestDamageZoneSnapshot.cs`、Hurtbox/Motion/RuntimeModule 接线、`CharacterTestCharacterDistanceM2RuntimeTests.cs` |
| Runtime focused tests | M2 EditMode 合并入口 22/22 通过（job `b58ac4e2c5de424b8a1942ed271d40d8`） |
| Revision / stale | binding revision 覆盖全部 binding；zone revision 覆盖活动成员 identity；world/authority mismatch 与非法/空成员 fail closed |
| NoAlloc | warmed aggregate 构建与查询用例通过，0 B GC；不把低频 snapshot capture 冒充热路径 |
| Unity 验证 | refresh/compile/domain reload/editor ready 通过，Console 0 error（最终 job `f51be42d052245e88ef17a8b8a80f081`） |
| 已知限制 / 阻断 | 无 T2 阻断；旧 binding v1 保持 legacy Hurtbox 行为，不生成持久 Zone |
