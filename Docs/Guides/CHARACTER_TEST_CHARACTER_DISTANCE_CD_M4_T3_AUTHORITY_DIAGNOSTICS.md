# CD-M4-T3：Planner 权威与可解释诊断

> Status: Guide
>
> 状态权威：[CD-M4 总控页第 3 节](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M4_JOINT_SELECTION.md)
>
> 前置：CD-M4-T2 `Verified`

## 1. 可交付结果

将 joint selection 接入 Runtime AI Planner 的目标/action/movement 命令链，并让 Runtime AI Lab/Debug 只读解释完整选择；按 T0 决策处理跨帧 selection authority。

## 2. 实现清单

- [x] Planner 使用联合选择结果提交攻击、TurnInPlace、ApproachPose 或换目标，不再先硬选最近目标。
- [x] 最终命令仍经现有 command/action/motion authority；selector 无直接副作用。
- [x] target lock、Zone lock、Anchor reservation 与 joint result 的 owner/lifecycle 不冲突。
- [x] joint selection 每个 Planner tick 重算，只提交既有 Zone lock/reservation/command authority；不新增跨帧 selection schema。
- [x] Debug Commands overlay 显示 target/action/zone/anchor、mode、path cost/earliest tick、candidate counts/truncation、cache hit 和 failure reason。
- [x] overlay 只复制上一 tick 的不可变诊断，不触发候选生成、cache mutation、query 或 command。

## 3. 自动化测试

- [x] T2 双 evaluator action（short/spear）归约与 T3 生产 resolved BasicAttack 多目标切换组合覆盖 action/target switch；CurrentPose、TurnInPlace、fail-closed/compatibility 边界已覆盖，正式内容规模矩阵继续由 T4 校准。
- [x] exact Zone leaf、reservation revision/只读 CanAcquire 与 rollback cache clear 已覆盖。
- [x] rollback/apply 后 joint 派生状态清空，下一 tick 重算同一 target/action/zone/anchor；不新增 schema/hash 字段。
- [x] overlay snapshot identity 与 selector result 一致；NoAlloc `TryGetDiagnostic` 不改变 query/cache counters。
- [x] 既有 full Planner submit/apply 热链与新增诊断读取 warmed 0 B GC allocation。

## 4. 完成 Gate

- Planner/command/network（若适用）focused 与受影响回归通过。
- Unity validate、Console 0 error 通过。
- AI/CharacterAction/Network 文档准确记录 authority 与 compatibility 边界。

## 5. 实际记录

| 项目 | 当前记录 |
| --- | --- |
| 状态 | 见总控页；当前 `Verified` |
| 实际分支 / 提交 | `main`；`d9761dd05`（已推送） |
| Planner / command tests | Unity EditMode focused 39/39，job `3f1c7c85285d46ee9790d4ef1007baa7`；包含多目标非最近切换、TurnInPlace、fail-closed、rollback、M2/M3/T2 回归 |
| snapshot/hash/rollback | joint result、trace/result cache 与诊断均为每 tick 派生；snapshot apply 清空并重算；既有 Zone lock schema 28、reservation schema 29 和 command authority 不变 |
| overlay / NoAlloc | Commands 页新增只读 joint card；full Planner submit/apply 32 ticks 0 B 与 diagnostic lookup 64 reads 0 B 通过 |
| Unity / build | Unity scripts validate `4b403fb17cbd41a397ed5e8b3ed75b37`，Console 0；Game/Test 双程序集 0 error |
| production/network 边界 | local-offline authoritative simulation 已接入；特殊技能继续显式 `CompatibilityLegacyRange`；未新增 wire schema，未声明 dedicated server/公网验证 |
