# PF-M2：确定性静态 Navigation Graph

> Status: Guide
>
> 工作包：Pathfinding Foundation PF-M2
>
> 状态权威：[Pathfinding Foundation 的 M2-M4 工作包状态](CHARACTER_TEST_RUNTIME_AI_PLANNER_PATHFINDING_FOUNDATION.md)
>
> 前置：PF-M1 `Verified`

## 1. 任务结果

在 CharacterTest runtime 内建立一份低频构建、运行期不可变的 noEngine-style 静态导航图。图必须对输入排列无敏感性，提供稳定 revision/content hash、稳定邻接顺序和 NoAlloc 查询入口，并成为 PF-M3 唯一允许消费的全局路径拓扑。

## 2. 冻结合同

- 类型保持 `MxFramework.Game.CharacterTest` internal，不新增 `MxFramework.AI` 公共 API。
- Node 使用正整数稳定 ID、`FixVector3` 世界位置、毫米级半径/高度 clearance、非负 traversal penalty 和整数 area tag mask。
- 首版支持的 graph/query 坐标范围为每轴 `[-1000m, +1000m]`；越界输入 typed reject，避免 Fix64 距离平方中间值溢出。
- Edge 是有向边；双向通行必须提供两个独立稳定 Edge ID。Edge 保存 from/to、正长度毫米、非负 penalty、clearance、blocked layer mask 和 area tag mask；作者长度不得小于两端 Node 的确定性几何距离，否则 builder typed reject，以保证 PF-M3 直线 heuristic 可采纳且一致。
- canonical Node 顺序固定为 Node ID 升序；canonical Edge 顺序固定为 Edge ID 升序；outgoing 顺序固定为 from Node ID、Edge ID、to Node ID。
- build revision 是调用方提供的正整数；content hash 只由 canonical 内容决定。图身份使用 `(revision, contentHash)`，不能只用其中一个。
- build 复制全部输入；成功后不得暴露可写数组，也不得依赖调用方集合生命周期。
- tick/query 不扫描 Unity Scene、GameObject、NavMesh、Collider 或 Transform。场景/关卡 authoring 到本合同的投影属于低频 session build 输入。
- 不可用或非法图必须返回 typed build status，不产生部分可用图。

## 3. 实现清单

- [x] 增加 Node/Edge definition、只读 runtime Node/Edge、typed build status/result。
- [x] 实现 graph builder：复制、canonical sort、重复 ID/端点/范围/clearance 校验。
- [x] 构建预计算 outgoing index/range，运行期不创建集合。
- [x] 计算稳定 content hash，并保留独立正 revision。
- [x] 提供按稳定 ID 二分查找、outgoing 读取和 clearance-aware nearest-node 查询。
- [x] XML 注释标记 build 为 `LowFreqAlloc`、读取为 `NoAlloc after initialization`。
- [x] PF-M3 之外不得复制另一份 graph DTO 或按容器枚举顺序建立邻接。

## 4. 自动化测试

- [x] 打乱 Node/Edge 输入顺序，canonical 内容、outgoing 顺序和 content hash 相同。
- [x] 重复 Node/Edge ID、缺失端点、自环、非法长度/penalty/clearance 返回精确 typed status。
- [x] build 后修改输入数组不改变 graph。
- [x] nearest-node 等距离时按 Node ID 决胜，并正确过滤 clearance 不足节点。
- [x] 任一 Node/Edge 元数据变化都会改变 content hash；只改变 build revision 不改变 content hash。
- [x] warmed 后 stable-id lookup、nearest-node 和 outgoing 读取为 0 B GC allocation。

## 5. 完成 Gate

- 纯 C# focused tests 全通过，记录通过/失败数量。
- Unity refresh/compile、domain reload、Editor ready 和 Console 0 error 通过。
- code standards report-only 无本任务新增未解释热路径问题。
- Pathfinding Foundation 与 AI/Combat 接口按实际合同回写。
- 实际文件、提交、测试与限制写入第 6 节后，PF-M2 才可进入 `Verified`。

## 6. 实际提交与验证结果

| 项目 | 当前记录 |
| --- | --- |
| 工作包状态 | 见 Pathfinding Foundation 状态表 |
| 实际分支 / 提交 | `main`；本工作包以 `feat(character-test): add deterministic navigation graph foundation` 独立提交 |
| 实际修改文件 | `CharacterTestNavigationGraph.cs`、`CharacterTestNavigationGraphTests.cs`、Pathfinding Foundation/PF-M2/PF-M3/PF-M4 任务页、AI/Combat 接口 |
| focused tests | EditMode `CharacterTestNavigationGraphTests` 17/17，通过；job `5496dcde3b8f44d8a5020a57bc8733c9` |
| Unity 验证 | `validate_unity_scripts` job `64fdd7af33554fc4a6e88303ce55f0f3`：refresh、compile、domain reload、Editor ready、Console 0 error 全部通过；warnings 为既有 obsolete baseline |
| NoAlloc | `RuntimeReads_AreNoAllocAfterInitialization` 覆盖 128 次 stable-id lookup、nearest-node、node/outgoing 读取，实测 0 B |
| 接口回写 | `Docs/Interfaces/AI.md` 与 `Docs/Interfaces/Combat.md` 已按 internal/no-public-API 边界同步 |
| 已知限制 | PF-M2 只建立静态拓扑，不提供 A*、Planner movement 或 scene bake UI |
