# LCC-M4-T5 Unique Independent Read-Only Review — 2026-08-25

> 结论：`Verified` 可晋升。P0=`0`，P1=`0`，P2=`1`，P3=`3`。

## 1. 审查边界

- 审查者不同于 T5 实现者；本次是 M4 唯一一次最终独立只读审查。
- 只核对当前 LCC M4/T5 diff、冻结合同、已落盘测试/性能/Playable 证据及接口/能力表。
- 未写文件、未启动 Unity job、测试或编译，未修改场景或 Git 状态；较新 job 不在当前 job store 时只采用已落盘 Evidence，不重跑。
- 重点检查 noEngine/Runtime assembly 边界、Unity Physics/Animator/Transform authority 隔离、唯一 resolved frame、schema32 恢复/hash/rollback、消费者同帧 witness、root-XZ/GeometryInReach 合同和性能/0 B 门禁。

## 2. 结论

### P0：0

未发现 authority 双真源、schema32 不完整恢复、运行时 Unity pose/Physics authority readback、消费者跨帧消费或正式数据破坏。

### P1：0

- Combat ground probe 与 Character RuntimeSpawn 保持 noEngine；terrain 查询输入/结果为 fixed-point Combat 数据。
- Unity `Animator`/`Transform` 只在 View 输出端使用，正式骨路径从 Animator root 精确解析，不回流 authority。
- 生产只有一个 fixed-capacity resolved authority frame：4 limbs、最多 32 samples；消费者同时校验 runtime frame、revision、definition/base-source witness 并 fail closed。
- schema32 只持久化会影响未来 tick 的 terrain history；document shape 在 commit 前校验，full/owned/non-owned hash、rollback/replay/late join/source mismatch 均闭合。
- rollback view sync 前重建既有 derived Animation；Body Presence 对 geometry v6/6.0 仍为 strict gate。
- 未发现 `BigInteger`、`System.Numerics`、自研任意精度、HTTP 取证、新 Pose Graph/provider/cache/scheduler 或第二 authority frame/snapshot/revision。既有压缩流 `ReadByte()` 不是 HTTP。
- root-XZ、`GeometryInReach`、Severed Hurtbox 过滤顺序均未被 M4 改写。

因此没有需要回派 owner 的 P0/P1 hunk。

## 3. 接受项

### P2-1：父计划的 T5 前状态残留

- 问题：总台账准备度行及 M4 实际记录仍含 T4 已派发/T5 等待、接口未回写、terrain-active/overlay 待 T5 等旧状态。
- 接受理由：这是父状态必须由主里程碑 owner 在最终晋升时写入造成的文档滞后，不是代码或证据虚报。
- Owner：M4 主里程碑 owner。
- 关闭方式：本次最终晋升同步更新总台账、M4 实际矩阵和无生产统一 overlay 的事实口径。
- 是否阻止 `Verified`：否；必须随本次封版一并关闭，不新增审查轮次。

### P3-1：约 10.88 MB socket catalog

- 接受理由：加载/hash 属 LowFreqAlloc，authority tick 使用固定容量；当前正式 Dragon 和性能门禁通过。
- Owner：T1 Authoring / RuntimeSpawn。
- 后续门禁：同包大量实例导致启动、内存或载入预算超标时，再评估包级共享或紧凑格式。

### P3-2：每 tick 两次 Hurtbox `SyncAll` 保留

- 实测：8 Dragon 的 first Sync → 4 probes/IK → second Sync 全链 p50/p95/max=`1238.5/1821.4/6708.6 µs`；full/no-hit/recovery 均 `0 B`。
- 接受理由：T0 未冻结绝对毫秒阈值，容量和 steady GC 门禁通过；当前去重会增加调度风险。
- Owner：CharacterTest RuntimeModule / Hurtbox。
- 后续门禁：同机基线明显回退或目标硬件正式帧预算失败。

### P3-3：正式 Dragon 内容覆盖有限

- 正式数据：ContactPoint config/attachment=`0/0`，无 `disabledActionTags`，四个 limb 无 `partStateId`；没有生产统一 overlay，Game View 使用 tests-only marker。
- 接受理由：空数据保持 fail closed，未猜测或补造；能力表未声明 live terrain-contact audio 或正式 limb/action 配置覆盖。
- Owner：Dragon 内容 Authoring；ContactPoint 后续由 Physical Audio owner 配合。
- 后续门禁：宣称 live terrain-contact audio、正式 action-disabled 或 limb-Severed Playable 前，必须增加真实作者数据与正式路径验收。

三项 P3 均不阻止 `Verified`；能力等级保持 Framework Feature，并将当前 Playable 证据限定为 Dragon14 四肢。

## 4. 已核对证据

| gate | 证据 |
| --- | --- |
| terrain-active PlayMode | `bdd838f341804a8fbe87222f06e41454`，3/3 |
| schema32 network acceptance | `724b5174c3fe4870848e37447e9cc641`，1/1 |
| existing rollback regression | `1427b590025a4de3b38ac8e799f8a7ba`，1/1 |
| performance / NoAlloc | `59d0868279cd4332aac5d4f4dadd1bdf`，2/2 |
| 27-filter EditMode | `7857bb06ae3c4b359375b2c224ada014`，290/290 |
| frozen 5-class PlayMode | `076edfb771df4120b7608375cdad3354`，12/12 |
| Unity scripts / Console | integrated validator 只调用一次并在 domain reload 回包时断线；一次分步 fallback 完成新 compile/domain reload、Editor idle/not compiling/no pending，Console error=0 |

失败与 TestRunner infrastructure 历史均保留在 [Acceptance Evidence](ACCEPTANCE_2026-08-25.md)，未把失败 job 计为通过。

## 5. 未验证与能力边界

- 未运行全仓测试。
- 未做 dedicated server、多进程或公网 transport 验收。
- Game View 只证明正式 Prefab、地面和 tests-only marker 可见；同源正确性由同帧数值断言承担。
- 不声明生产统一 overlay、正式 terrain-contact Physical Audio、任意生物 rig、脊柱贴地、长蛇/触手、全身程序化动画、飞行翼空气动力、ragdoll 或可拆尾资源。

在这些边界下，本次唯一终审确认 LCC-M4-T5 可晋升 `Verified`，并允许主里程碑 owner 将 M4 与 32/32 总路线封版为 `Verified`。
