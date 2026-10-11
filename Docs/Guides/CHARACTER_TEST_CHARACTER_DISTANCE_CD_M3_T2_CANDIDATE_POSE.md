# CD-M3-T2：CandidatePose 与站立验证

> Status: Guide
>
> 状态权威：[CD-M3 总控页第 3 节](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M3_ENGAGEMENT_ANCHOR.md)
>
> 前置：CD-M3-T1 `Verified`

## 1. 可交付结果

将 target ModelRoot-local Anchor 确定性投影为 world CandidatePose，验证角色 capsule 可站立/未被占用，并用 M1 同一 Attack Opportunity evaluator 验证该姿态。

## 2. 实现清单

- [x] CandidatePose 绑定 source/target/action/Zone/Anchor、world position、quantized yaw、Zone/Anchor/pose/physics revision witness。
- [x] world projection 只使用 sealed target ModelRoot pose；尾部 bone/Hurtbox 动画不得带动长期 Anchor。
- [x] 站立、地面支撑、静态阻挡和当前 occupancy 走 noEngine CombatPhysics capsule/query，stable-sort results。
- [x] source 自身 collider、允许的 target overlap 与障碍分类按 T0 合同处理，不靠 Unity Physics。
- [x] 以 CandidatePose 调用 M1 同一 evaluator；CurrentPose/TurnInPlace 行为不变，unsupported action fail closed。
- [x] Zone/Anchor/phase/pose/physics revision 变化使旧 CandidatePose stale；失败清空旧 success evidence。

## 3. 自动化测试

- [x] ModelRoot translation/yaw projection、尾部 animation independence、equal-anchor stable ID。
- [x] standable、no ground、blocked、occupied、invalid Zone、stale revision。
- [x] CandidatePose 与同一手工 source pose 下 M1 evaluator 结果/earliest tick/Zone/Hurtbox 完全一致。
- [x] query/broadphase 输入顺序扰动不改变结果。
- [x] warmed candidate projection + standing + evaluator 为 0 B GC allocation。

## 4. 完成 Gate

- focused tests、Unity 完整脚本验证、Console 0 error 通过。
- 正式 Runtime fixture 使用 Compiler binding，不仅是手工单元 DTO。
- NoAlloc、typed failures、evaluator equivalence 和限制写入实际记录。

## 5. 实际记录

| 项目 | 当前记录 |
| --- | --- |
| 状态 | 见总控页；`Verified` |
| 实际分支 / 提交 | `main`；随 CD-M3 里程碑提交 |
| focused / Unity | M3 13/13、合并受影响 EditMode 163/163；Unity compile/Console 0 |
| evaluator equivalence | M1 `CandidatePose` 同 evaluator 与 Dragon14 PlayMode 活跃选择通过 |
| NoAlloc | warmed projection + standing + path + comparator 32 次循环 0 B |
| 已知限制 | v1 phase witness 固定 `phase.default`；独立 Boss phase controller 不在本里程碑 |
