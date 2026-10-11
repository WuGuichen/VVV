# PF-M4-T1：关卡 Graph 输入与 Session 服务

> Status: Guide
>
> 状态权威：[PF-M4 总控页第 3 节](CHARACTER_TEST_RUNTIME_AI_PLANNER_PATHFINDING_PF_M4_PLANNER_INTEGRATION.md)
>
> 前置：PF-M4-T0 `Verified`

## 1. 可交付结果

把正式 CharacterTest level/session 输入确定性投影为 PF-M2 graph，并在组合根创建唯一共享的 graph/pathfinder 服务。构建只发生在启动或显式 level apply 低频路径。

## 2. 实现清单

- [x] 按 T0 冻结方案增加正式 graph 输入，不手写 Unity Scene YAML、不使用 NavMesh/Collider/Transform 作为 Tick 权威。
- [x] stable Node/Edge ID、位置、clearance、length、penalty、area/layer metadata 可追踪到 level source。
- [x] graph revision 单调、content hash 稳定；等价输入排列得到同一图，内容变化使旧 witness stale。
- [x] session build 对 absent/invalid input 返回 typed status，并按冻结合同进入显式 L1 fallback 或 fail closed。
- [x] 组合根只创建一份 immutable graph 和 bounded pathfinder；多 agent 共享，不复制 topology。
- [x] Runtime reset/level apply 释放旧引用并建立新 identity；Tick 不 rebuild、不枚举 GameObject。

## 3. 自动化测试

- [x] 正式 level fixture 构建、输入顺序扰动、revision/hash、invalid/absent 输入。
- [x] session reset/apply 后旧 graph identity 失效，新实例被注入 navigation service。
- [x] 多 agent 共用同一 graph/pathfinder owner，不能出现平行 topology。
- [x] 低频 build 可分配；建立后 graph read/query 保持 NoAlloc。

## 4. 完成 Gate

- focused tests、Unity 完整脚本验证、Console 0 error 通过。
- 正式 session fixture 证明 Runtime 消费的是唯一 graph 输入，不是测试内临时图。
- 文件、测试 job、build status、fallback 和限制写入实际记录。

## 5. 实际记录

| 项目 | 当前记录 |
| --- | --- |
| 状态 | 见总控页；当前 `Verified` |
| 实际分支 / 提交 | `main` PF-M4 工作树；随 PF-M4 收口独立提交 |
| 受影响文件 | `CharacterTestNavigationSession.cs`、composition/session/network 透传、两份正式 Scene、`CharacterTestNavigationSessionTests.cs` |
| focused / Unity | 最终 graph/session/perception 合并任务 `cbe124437c384386a2fb1b83b8eaad05` 36/36；脚本验证 `4a133243498e4934bb6c44674df4a6a7`，Console 0 error |
| graph fixture | `CharacterTestScene` 与 `CharacterTestRuntimeAiLabScene` 均从正式序列化输入编译；重复构建 hash 稳定；build clearance 取场景 fallback 与全部已注册 playable character physics profile 的分量最大值，避免较高/较宽角色得到 `SourceNodeUnavailable` |
| 已知限制 | graph 是低频构建的派生 runtime 数据，不进入 authority snapshot/hash；角色目录或关卡静态输入变化需要显式重建 session |
