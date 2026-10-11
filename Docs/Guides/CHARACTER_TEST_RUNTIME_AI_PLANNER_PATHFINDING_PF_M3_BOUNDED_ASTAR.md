# PF-M3：确定性有界 A* 与 Path Sample

> Status: Guide
>
> 工作包：Pathfinding Foundation PF-M3
>
> 状态权威：[Pathfinding Foundation 的 M2-M4 工作包状态](CHARACTER_TEST_RUNTIME_AI_PLANNER_PATHFINDING_FOUNDATION.md)
>
> 前置：PF-M2 `Verified`

## 1. 任务结果

在 PF-M2 唯一静态图上实现确定性、有硬预算、初始化后 NoAlloc 的 A* 查询；结果写入 caller-owned corridor buffer，并输出 typed status、next waypoint、remaining cost 和 expanded node count。

## 2. 实现边界

- [x] 查询输入绑定 graph revision/content hash、source/target position、agent clearance、endpoint snap 上限、area/hard-block mask、最大 expansion 和 path length。
- [x] source/target 先经 PF-M2 clearance-aware nearest-node 归约；无合法节点返回明确状态。
- [x] open/closed/node record/path scratch 在 service 初始化或显式 capacity build 时预分配。
- [x] 排序固定为 `total cost -> heuristic -> node ID -> incoming edge ID`，不得依赖 heap/集合插入偶然顺序。
- [x] 成本使用确定性整数/定点：edge length + 非负 node/edge/hazard penalty；hazard 默认软成本，不是 hard block。
- [x] typed status 至少区分 `Success`、`GraphUnavailable`、`InvalidQuery`、`SourceNodeUnavailable`、`TargetNodeUnavailable`、`Unreachable`、`BudgetExceeded`、`PathBufferTooSmall` 和 `StaleGraph`。
- [x] 输出 caller-owned node/edge corridor、next waypoint、remaining distance、total cost、expanded count；失败不得留下看似成功的部分路径。
- [x] 不引入 service 内跨帧 path memory；每次结果是当前查询的派生 sample。

冻结细节：PF-M2 Edge 长度不得小于两端 Node 的确定性几何距离，使“到目标 Node 的直线毫米距离”成为 admissible/consistent heuristic；成本为 source snap + Edge 长度/penalty + 到达 Node penalty + target snap。open set 排序固定为 `f -> h -> NodeId -> incoming EdgeId`，同一 Node 等价 cost 的 parent 更新再按 incoming EdgeId、parent NodeId 决胜。所有距离、成本和预算使用非负整数毫米/计数，不把 float 引入权威比较。graph 与 query 坐标沿用 PF-M2 每轴 `[-1000m,+1000m]` 的显式安全范围，越界输入返回 typed failure，不进入异常控制流。

## 3. 自动化测试

- [x] direct、detour、unreachable、source=target、clearance filter。
- [x] 等成本路径、输入/邻接顺序扰动后结果一致。
- [x] expansion cap、path buffer cap 和 stale graph 返回精确状态。
- [x] hazard penalty 改变最优路径，但默认不硬阻断。
- [x] caller-owned buffer 之外不分配、不越界，失败清理有效长度。
- [x] warmed repeated queries 为 0 B GC allocation。

## 4. 完成 Gate 与实际记录

纯 C# focused、Unity refresh/compile、Console 0、NoAlloc 和接口回写全部闭合后才进入 `Verified`。

| 项目 | 实际记录 |
| --- | --- |
| 工作包状态 | 见 Pathfinding Foundation 状态表；本节证据已满足 `Verified` gate |
| 实际分支 / 提交 | `main`；本工作包以 `feat(character-test): add deterministic bounded A-star pathfinding` 独立提交 |
| 实际修改文件 | `CharacterTestNavigationGraph.cs`、`CharacterTestBoundedAStarPathfinder.cs`、对应 focused tests、Pathfinding Foundation/PF-M2/PF-M3 与 AI/Combat 接口 |
| focused tests | EditMode `CharacterTestNavigationGraphTests` + `CharacterTestBoundedAStarPathfinderTests` 32/32，通过；提交后复验 job `373443404deb480893531c413bcbae31` |
| 算法交叉验证 | 固定 5x5 有向网格、5 组 source/target 与独立 reference Dijkstra 的 status/cost 完全一致，并逐边验证 corridor 连通性 |
| Unity 验证 | 提交后 `validate_unity_scripts` job `8dcd1ac4f5c643cdb8734c2647220233`：refresh、compile、domain reload、Editor ready、Console 0 error 全部通过 |
| 命令行构建 | 串行 `dotnet build MxFramework.Game.CharacterTest.csproj --no-restore -m:1` 与 `MxFramework.Tests.csproj` 均为 0 warning / 0 error |
| NoAlloc | 预热后同一 pathfinder/buffer 连续 128 次查询实测 0 B managed allocation；graph runtime reads 同样保持 0 B |
| 规范检查 | 本工作包 4 个 C# 文件 code standards report-only 无 finding；文档健康检查通过 |
| 已知限制 | PF-M3 只消费 immutable static graph；`ActiveBlockedLayerMask` 是调用方提供的当前阻断摘要，不自行查询 CombatPhysics；不持有 path cursor/replan cooldown，不接 Planner facts/movement/Play Monitor；这些属于 PF-M4 |

caller buffer 的容量合同是显式上限：容量必须覆盖 `min(MaxPathNodeCount, Graph.NodeCount)`，查询不会在发现一条偶然较短路径后放宽该前置条件。失败会清空有效 corridor 长度。所有 status/sample 都是当前查询派生数据，不进入 SaveState、network、rollback、replay 或 authority hash。
