# CD-M3-T6：集成、Playable、性能与收口

> Status: Guide
>
> 状态权威：[CD-M3 总控页第 3 节](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M3_ENGAGEMENT_ANCHOR.md)
>
> 前置：CD-M3-T4、CD-M3-T5 `Verified`

## 1. 可交付结果

用正式大型/非人形角色包与多 AI 场景闭合 CD-M3 全链验收，并形成独立、已 rebase、已推送的里程碑提交。

## 2. 验收清单

- [x] 正式 Compiler fixture 包含多个 Zone/Anchor、不同容量、不可站立/不可达/phase invalid 分支。
- [x] 直接 Game View/PlayMode 显示 authored Zone/Anchor、CandidatePose、standing/path status、path cost、reservation owner/capacity 和选择原因。
- [x] 多 AI 同时接敌不会全部拥挤到同一满容量 Anchor；释放后稳定补位。
- [x] 尾部动画不带动长期 Anchor；Zone/Anchor/phase revision 变化使旧 pose/lock/reservation fail closed。
- [x] 合并 focused、CD-M0～M2、PF-M4、Planner/network 受影响回归全通过。
- [x] Unity refresh/compile/domain reload、Editor ready、Console 0 error 通过。
- [x] warmed CandidatePose + standing + path cost + selection + reservation/Planner Tick 为 0 B；snapshot capture/apply 单独标 `LowFreqAlloc`。
- [x] CharacterStudio 直接证据、AI/Combat/CharacterApplication/Network/Pathfinding 文档回写和独立终审 P0/P1=0。

## 3. 提交与推送 Gate

- exact stage 仅含 CD-M3 相关源码、资产/fixture、测试、证据和文档。
- 提交后 fetch/rebase `origin/main`，重新执行 Unity validate、focused/关键回归和文档健康检查。
- push 后确认 `HEAD...origin/main = 0 0`、工作区干净且用户 stash/无关改动保持原状。

## 4. 实际记录

| 项目 | 当前记录 |
| --- | --- |
| 状态 | 见总控页；`Verified` |
| 实际分支 / 提交 | `main`；实现 `9196cb2ce`、Studio 收口 `4d9864cde` 均已推送，提交后 `HEAD...origin/main = 0 0`、工作区干净 |
| focused / 回归 | Authoring Anchor focused passed；EditMode 163/163；M1～M3 PlayMode 12/12；双程序集 0 error；提交后 M3+diagnostics 22/22、M3 PlayMode 3/3、Authoring focused 与 Studio smoke 通过 |
| Unity / Playable / Game View | Unity compile/domain reload/Console 0；M3 3/3；两张 active Dragon14 diagnostics 截图 |
| CharacterStudio | smoke passed；Browser 直接验证标准/900px、6-anchor Inspector/3D、dirty refresh、Undo、save/reopen、copy/order 与 unknown raw read-only，Console 0 error |
| snapshot / replay / NoAlloc | schema12/29、rollback next-tick parity；projection/path/selection 与 renewal warmed 0 B |
| 接口回写 / 终审 | AI/Combat/CharacterApplication/Network/PF 已回写；最终 P0/P1=0；同步点 Unity job `4f9d3f6520c349f0a6e6413399370ac5`、EditMode 22/22、PlayMode 3/3、Studio/Authoring passed |
| production/network 边界 | local-offline/formal snapshot/loopback；不包含多进程 server、公网 transport、online authority 或独立 Boss phase controller |
