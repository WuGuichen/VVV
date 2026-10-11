# CD-M3-T4：预约权威、网络、回滚与重演

> Status: Guide
>
> 状态权威：[CD-M3 总控页第 3 节](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M3_ENGAGEMENT_ANCHOR.md)
>
> 前置：CD-M3-T3 `Verified`

## 1. 可交付结果

把会影响未来命令的 Anchor lock/reservation 纳入 Planner authority 与 formal network snapshot/hash/rollback/replay，保证多 AI 容量冲突和回滚后下一 tick 命令一致。

## 2. 实现清单

- [x] 冻结 reservation key、source owner、target/Zone/Anchor/action、capacity slot、acquired/updated frame、revision witness 和 release reason。
- [x] 同 tick 冲突按稳定 source/target/Zone/Anchor ID canonical resolve，不依赖模块/容器顺序。
- [x] target death、Zone/Anchor/phase revision、action/owner invalid、capacity/config change 和 runtime reset 确定性释放。
- [x] authority state 进入 Planner snapshot、formal network schema、full/owned/non-owned hash；canonical order 明确。
- [x] capture/apply/rollback 清理非快照 cache，并恢复 lock/reservation 后继续真实下一 tick。
- [x] path/candidate sample 仍为派生数据，不因 reservation 入快照而一并序列化。

## 3. 自动化测试

- [x] capacity 1/2、多 AI 同 tick 冲突、stable winner、release/reacquire。
- [x] schema round-trip、旧 schema 归一化、full/owned/non-owned hash sensitivity/isolation。
- [x] rollback 前后 reservation/lock/hash 和下一 tick drained command parity。
- [x] Zone/Anchor/phase/action/target invalidation 及 stale cache 清理。
- [x] 逐帧 lookup/update warmed 0 B；snapshot capture/apply 单独记录 `LowFreqAlloc`。

## 4. 完成 Gate

- authority/network focused 与真实 command replay tests 全通过。
- Unity 完整验证、Console 0 error 通过。
- Network/AI 文档明确本地 formal snapshot 证据与未验证的多进程/online server 边界。

## 5. 实际记录

| 项目 | 当前记录 |
| --- | --- |
| 状态 | 见总控页；`Verified` |
| 实际分支 / 提交 | `main`；随 CD-M3 里程碑提交 |
| schema / hash / rollback | Planner schema 12、network schema 29；clone/hash/schema28 migration/restore 通过 |
| next-tick command parity | Dragon14 PlayMode rollback 前后 hash 与 drained command parity 通过 |
| allocation contract | reservation warmed renewal 0 B；capture/apply `LowFreqAlloc` |
| production/network 边界 | 已验证 local formal snapshot/loopback；未验证多进程 dedicated server、公网 transport、认证与恢复 |
