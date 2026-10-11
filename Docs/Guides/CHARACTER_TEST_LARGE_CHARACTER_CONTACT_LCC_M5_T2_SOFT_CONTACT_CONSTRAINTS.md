# LCC-M5-T2：动画附肢软接触约束与反馈事实

> Status: Guide
>
> 工作包：LCC-M5-T2（生产实现 / Runtime）
>
> 状态权威：[LCC-M5 第 3 节](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M5_ANIMATED_APPENDAGE_SOFT_CONTACT.md)
>
> 执行文档状态：`Verified`（主代理已复核双Sync、精确叶子、压力复用、NoAlloc/回归与Unity证据）
>
> 前置：T1 `Verified`。

## 1. 任务结果与非目标

同一 authority frame 的玩家 capsule 与已作者授权的动画 Hurtbox 叶子形成稳定、确定性、固定容量的软接触约束；结果进入现有 contact pressure/motor 安全链，并派生可供表现消费的接触相位。Dragon14 头、尾中、尾尖不再完全无反馈穿过。

本任务不把 Hurtbox 变成硬障碍，不造成普通接触伤害/硬直，不改攻击 Trace、导航、投射物 mask、root distance、DamageZone/Anchor，不新增 Unity Physics/Rigidbody 或公共通用碰撞框架。

## 2. 前置与并行

T1 必须 `Verified`，其正式 Dragon14 v2/v7 binding 和 source identity 可由 Runtime 加载。T2 与 T3 不并行；其它代理可只读审计回归，但不得修改 motion/contact/hurtbox 热点。

## 3. 必读输入

- M5、T0 最终冻结表和 T1 实际交接；
- `CharacterTestCharacterRuntimeModule.Tick`、`CharacterTestCharacterMotionSystem.Tick`；
- `CharacterTestCharacterHurtboxSystem`、`CharacterTestDamageGeometrySnapshot`；
- `CharacterTestCharacterContactResolver` M1 constraint/pressure/safety；
- M1/M3/M4 network、rollback、performance tests 与 Combat separation合同。

## 4. 写入所有权

- `CharacterTestCharacterMotionSystem.cs`、`CharacterTestCharacterContactResolver.cs`、`CharacterTestCharacterRuntimeModule.cs` 仅 declaration/schedule hunk。
- 新建 `CharacterTestCharacterMotionSystem.AnimatedAppendageSoftContact.cs` 与 `CharacterTestCharacterContactResolver.AnimatedAppendageSoftContact.cs`。
- 新建 `CharacterTestAnimatedAppendageSoftContactTests`、`CharacterTestAnimatedAppendageSoftContactNetworkTests`、`CharacterTestAnimatedAppendageSoftContactPerformanceTests`。
- `CharacterTestCharacterHurtboxSystem`、`CharacterTestDamageGeometrySnapshot`、Combat Physics、Network、Authoring 全部只读；若现有只读 seam 不足即触发 stop gate，不擅自扩 owner。

禁止写 Authoring/CharacterStudio/generated、Combat separation 数学、Network schema（除非 T0 已触发 schema 34 stop gate并由主代理重写任务）、父 Docs 和 T3 验收资产。

## 5. 约束与时序合同

- 不增加第三次 Hurtbox `SyncAll`；按 T0 冻结顺序复用现有 current-frame snapshot。
- query source 是目标角色 `ColliderPosture` authority capsule，target mask 只在本次 feature query 中读取 CharacterHurtboxLayer；不得修改全局 `MotionObstacleLayerMask`。
- 结果按 `(source entity, target entity, logical collider id, combat collider id, shape identity)` 稳定排序去重；Separated 不产生约束，invalid/unsupported/capacity/conflicting duplicate 在任何 mutation 前拒绝本 feature 子步。
- 只接受 T1 binding 授权且 metadata PartId 对应的 active leaf；source/self、Severed/invalid topology、dead/disabled/BlocksCharacters policy 按 T0 合同过滤。
- previous/current 动画 surface velocity、penetration bias、skin、pressureScale 和最大合成速度全部 fixed-point、有界、可在 restore 后重建；缺失 witness 时不读取 View 补偿。
- 复用现有三轮 pressure、mass/PushScale/CanBePushed、static sweep 和 contact state；不得调用 direct displacement。
- 普通接触永不创建 Damage/HitResolve/Active Trace。Started/Stayed/Ended 是同一 authority constraint 的派生表现事实，不进 hash/save/network，也不反向决定压力。

## 6. 实施清单

1. 在 T0 冻结的现有 Hurtbox sync seam 获取同帧 committed snapshot，不新增第三次 sync。
2. 构建固定容量、稳定排序的 authored-leaf query/constraint 集并在 mutation 前全量校验。
3. 产生 previous/current surface witness、skin/pressure/clamp，并接入 M1 pressure accumulator。
4. 派生 Started/Stayed/Ended 与低频 diagnostics，保持 presentation 单向。
5. 组合 Severed、restore/source mismatch 和 legacy no-v2 fail-closed。
6. 完成 focused/NoAlloc 后只交 T3 验收，不修改父状态。

## 7. 必须覆盖的自动化

- head OBB、tail-mid/tail-tip capsule：相离、相切、浅/深重叠、完全重合和多叶去重。
- idle、walk/run、转身、动画切换、动作中非伤害帧、高速摆动、双向移动、质量差、不可推动、死亡/禁用。
- 墙边、角落、窄通道、profile clamp、无 teleport/穿墙/无界速度。
- Severed tail 同一 topology 边界后尾部约束消失、Ended 只派生一次、head 保持。
- spawn/restore/rollback+1、source revision mismatch、实体/叶子/broadphase 顺序扰动。
- bite/tail sweep/spin 伤害窗口和 M1 root Presence 压力无回归。
- 8 Dragon 固定容量预热后 contact-build/apply steady-state 0 B；实际耗时门槛留 T3 校准。

证据写入 `Docs/Evidence/LCC/LCC-M5-T2/SOFT_CONTACT_CONSTRAINTS_2026-08-26.md`；focused filter 固定为上述三个 M5 test fixture。

## 8. 回滚、派发与停止

执行者只写 T0 回写的文件/partial。回滚只撤销 M5 feature-specific constraint/schedule/tests；不得恢复或改写 M1 pressure、M3 topology、M4 pose 或用户其它 dirty hunk。若实现需要全局 Hurtbox obstacle mask、复制 shape、第三次 SyncAll、Unity Transform、未持久化跨帧 cache、无界常量或 Combat core 重构，立即停止并回报，不自行扩范围。

回报必须包含：实际调度顺序、约束 key/容量/failure、压力复用点、Severed 行为、focused tests 原始结果、GC 数据、未运行验证和 T3 交接。

## 9. 实际执行记录

| 项目 | 当前记录 |
| --- | --- |
| 状态权威 | M5 工作包表 |
| 执行者 / 审查者 | `lcc_m5_t2_runtime` / 主代理接收完成，T3负责唯一最终审查 |
| 实际修改文件 | motion/contact/runtime-module declaration/schedule 窄 hunk；2 个 M5 Runtime partial；3 个 M5 EditMode fixture 及 meta |
| Runtime / tests | same-tick previous/final Hurtbox witness、最多 256 定向约束、M1 三轮 pressure、typed presentation phase 已实现；T3 暴露 zero-acceleration tail-mid 零推出后已将 penetration bias 上限修正为既有 maximum composed speed；修复后 focused 5/5 + 2/2 + 1/1 通过 |
| 性能 / 网络 | 8x4、256 约束 warmed 128 帧 steady-state 0 B；顺序扰动与 restore silent baseline 通过；未占 schema 34 |
| 证据 | `Docs/Evidence/LCC/LCC-M5-T2/SOFT_CONTACT_CONSTRAINTS_2026-08-26.md`；初始 gate `7fd7c9d722664bc5b2aeaaae6de82312` 与修复 gate `cd31676b20ca4b46b7058a01831cd1ca` 均 passed、Console 0 |
| 限制 | 未声明 Playable；M4 consumer 既有 0-ContactPoint 断言因当前 Dragon14 已有 3 个 PresentationOnly 点而 8/9，留 T3/对应 owner；等待主代理接收后进入 T3 |
