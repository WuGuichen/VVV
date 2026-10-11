# LCC-M4-T5 Performance Evidence — 2026-08-25

> 状态：`Candidate measurement complete`。T0 冻结的是同机校准基线与 steady 0 B，不含未经实测的绝对毫秒阈值。

## 1. 测量路径

`CharacterTestAuthorityPosePerformanceTests` 使用 8 个正式 Dragon14，每只 4 probes/4 analytic solves/18 Hurtbox，固定 `attack1a localFrame=10`。每帧顺序严格为：

```text
first CharacterTestCharacterHurtboxSystem.SyncAll
  -> 8 characters x (4 CombatPhysics probes + IK + resolved frame)
  -> second CharacterTestCharacterHurtboxSystem.SyncAll
```

- warm-up：300 ticks
- measured：1800 ticks
- 单列阶段：first sync、4-probe/IK、second sync、full chain 的 p50/p95/max
- 容量断言：8 characters、4 limbs each、18 Hurtbox each、144 compound leaves
- 测量窗口前创建计时数组；窗口内不写字符串、不排序、不采集 diagnostics，避免测试工具污染 0 B。

## 2. 失败与恢复路径

同一 warmed fixture 移除正式 fixed-point ground body，settle 16 ticks 后测量 900 no-hit ticks；恢复 ground 并 settle 16 ticks 后测量 900 recovery ticks。两段分别断言 steady managed allocation 为 0 B，并验证 8 个角色的四肢 failure/state 与 18 Hurtbox snapshot。

## 3. Overlay 校准边界

- `overlay-off`：上述 runtime-only measurement。
- `overlay-on=N/A (no production M4 unified overlay)`。
- PlayMode 测试专用彩色 marker 不属于生产 overlay，也不进入该性能测量。

## 4. 原始结果

聚焦 EditMode job `59d0868279cd4332aac5d4f4dadd1bdf`：`2 passed / 0 failed / 0 skipped`，`5.6082072 s`。同次采样记录：

| stage | p50 | p95 | max |
| --- | ---: | ---: | ---: |
| first SyncAll | 344.300 µs | 507.900 µs | 4746.900 µs |
| 4 probes + IK | 442.000 µs | 690.300 µs | 1369.200 µs |
| second SyncAll | 447.800 µs | 692.900 µs | 3139.200 µs |
| full chain | 1238.500 µs | 1821.400 µs | 6708.600 µs |

| path | measured ticks | managed GC |
| --- | ---: | ---: |
| full chain | 1800 | 0 B |
| steady no-hit | 900 | 0 B |
| steady recovery | 900 | 0 B |

容量断言为 8 characters × 4 limbs × 18 Hurtbox = 144 leaves，全部通过。由于 T0 未冻结绝对毫秒预算，而本次 steady allocation、容量与双 Sync 路径均通过，不触发优化，不修改生产调度。结果是本机同次候选校准，不外推为跨机器帧预算。
