# CD-M4-T4：预算校准与默认值冻结

> Status: Guide
>
> 状态权威：[CD-M4 总控页第 3 节](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M4_JOINT_SELECTION.md)
>
> 前置：CD-M4-T3 `Verified`

## 1. 可交付结果

在代表性正式场景中测量 actor/action/zone/PreviewTrace/CandidatePose/path query 零截断峰值，以至少 2 倍余量冻结 production default，并记录适用边界和压力诊断。

## 2. 校准清单

- [x] 至少覆盖普通遭遇、大型多 Zone Boss、多 AI、多 ready action、短剑/长枪/兼容远程、不可达/阻挡混合场景。
- [x] 固定 fixture、seed、frame window、角色数、action/Zone/Anchor 数和 graph 规模，可重复采集。
- [x] 分别记录 actor candidates、joint candidates、leaf PreviewTrace、CandidatePose/path queries、cache hit/miss 和 truncation。
- [x] 以零截断峰值计算每项默认值，至少 2 倍余量；若受类型/存储上限约束，明确风险和 typed failure。
- [x] 默认值进入正式 profile/config/constant，并由测试锁定；不以一次机器耗时替代确定性工作量预算。
- [x] 压力场景超过默认值时稳定截断、NoAlloc、无异常且诊断准确。

## 3. 完成 Gate

- calibration PlayMode job 与原始/汇总数据可复现，数字、公式、默认值和适用边界写入实际记录。
- 默认值测试证明不少于代表峰值 2 倍。
- Unity validate、Console 0 error、压力场景 NoAlloc 通过。

## 4. 实际记录

| 项目 | 当前记录 |
| --- | --- |
| 状态 | 见总控页；当前 `Verified` |
| fixture / frame window | seed `42004`；ordinary、Dragon Boss、16v16 multi-AI、weapon mix、blocked/unreachable、compatibility remote 共 6 fixture；每 fixture 5 种顺序扰动，120 tick warmup + 600 tick sample |
| 零截断峰值 | actor scan `32`、actor TopK `16`、ready action `4`、action-zone broadphase `256`、joint candidate `256`、leaf PreviewTrace `256`、CandidatePose `256`、path query `256`；trace/result working set `64/256` |
| 2 倍默认值 | `64/32/8/512/512/512/512/512`；trace/result cache `128/512`；逐项为代表峰值的 2 倍并由测试锁定 |
| 压力 / NoAlloc | actor default+1/hard+1、CandidatePose/path default+1、leaf PreviewTrace typed truncation 均稳定且 warmed 0 B；真实 Dragon14 CandidatePose 截断 PlayMode 通过 |
| Unity / job | calibration PlayMode `af847328e316459592fa630af4a8430c` 与复验 `5744ef75daae4b0891bcfe5ae1d26bde` 通过；真实 CandidatePose budget `9a037fd642c543f182dbdf8d68dc4a31`；EditMode 15/15 `01834b35dff04af0b59e0a47b614d2d8`；最终 Unity validate/Console 0 `a32266a19b5d43259e11609a530e0f02` |
| 原始/汇总数据 | [CD-M4 T4 校准记录](../Evidence/CHARACTER_TEST_CHARACTER_DISTANCE_CD_M4_T4_CALIBRATION_2026-08-20.json)；测试入口 `CharacterTestCharacterDistanceM4CalibrationPlayModeTests` |
| 适用边界 | 数字约束 local-offline 单进程、每 source 每 Planner tick 的确定性工作量；16v16 是代表上界，不证明 dedicated server、公网、多进程或超大 raid 容量；超过 default/hard limit fail closed 并输出 typed truncation |
