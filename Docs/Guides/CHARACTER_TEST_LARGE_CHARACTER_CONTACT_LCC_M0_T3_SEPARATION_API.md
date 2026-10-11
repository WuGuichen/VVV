# LCC-M0-T3：通用确定性 Separation API

> Status: Guide
>
> 工作包：LCC-M0-T3（生产实现）
>
> 状态权威：[LCC-M0 第 4 节](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M0_CONTRACT_FOUNDATION.md)
>
> 执行文档状态：`Production-Frozen`
>
> 工作包派发状态：`Verified`
>
> 派发、集成与回滚规则：[LCC 单任务执行文档模板](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_TASK_EXECUTION_TEMPLATE.md)
>
> 并行规则：除本文明确写明“可并行”外，不与同里程碑其它生产写入并行；跨里程碑只允许只读调查。
>
> 必读基线：总台账第 1～8 节、父里程碑冻结合同、本任务全部章节、路径上的 AGENTS.md、Docs/API_STANDARDS.md 和 Docs/QUALITY_GATE.md；再按本文输入/所有权加载专项接口与源码。
>
> 子代理派发合同：本文是唯一任务边界；只写 Owner 范围，保留工作区其它修改，不改父状态。完成回报逐项列出实际文件、合同/兼容/失败行为、测试原始结果、未执行验证、风险冲突、集成与回滚建议。实现者不得担任 schema、network、碰撞数学、拓扑或正式资产任务的最终独立审查者。
>
> 派发门禁：T0 已 Verified，M0 已 `Production-Frozen`；本任务可与 T1 并行，但不得越过本文 owner 或修改父状态。
>
> 前置：T0 `Verified`；可与 T1 并行。

## 1. 任务结果与非目标

Combat 提供 noEngine、fixed-point、无业务词的 capsule 对 AABB/sphere/capsule/OBB/compound leaf separation 查询，返回稳定身份、法线、penetration 与 contact point，可由 M1 复用。

非目标：不产生速度/位移/伤害，不建立 Presence layer/body，不修改 CharacterTest movement 或网络状态。

## 2. 写入所有权

- Owner：`Packages/com.vincent.wgameframework/Combat/Physics/CombatPhysicsWorld.Narrowphase.cs`、`CombatPhysicsWorld.Queries.cs`、必要的专属 separation 文件/通用结果类型、`Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Combat/Physics/CombatPhysicsWorldTests.cs` 或专属测试。
- `CombatPhysicsWorld.Compounds.cs` 仅在 T0 明确需要 leaf identity 接口时由本任务写；否则只读。
- 禁止：CharacterTest、RuntimeSpawn、Authoring、Unity Physics adapter、父状态文档。

## 3. 冻结行为与实施清单

- [x] 结果包含 overlap/status、source query identity、target entity/body/collider/leaf、normalized normal、非负 penetration、contact point 和 comparer 字段。
- [x] 覆盖 capsule→AABB/sphere/capsule/OBB；compound 按稳定 leaf identity 输出，不依赖 BVH/容器枚举顺序。
- [x] 相切、零长度 capsule、同中心、平行轴、内含、等距多轴均使用 T0 冻结的 deterministic fallback。
- [x] 不命中/unsupported/invalid 使用 typed status；不返回 NaN/未归一化法线或负 penetration。
- [x] API 不分配集合、不用 LINQ/字符串；调用者提供缓冲并原地稳定排序。
- [x] 稳定 comparer 覆盖平局字段；输入顺序扰动、重复 leaf 和 identity 边界有测试。
- [x] 沿用现有 fixed-point shape/identity 合同；新算法限制在 separation 专属 partial，不修改旧 Query 语义，也未引入自研大整数或浮点实现。

## 4. 验证与证据

- Combat focused tests 覆盖每种形状的分离/相切/非重叠/退化/顺序扰动和 fixed-point 重演。
- warmed loop 0 B GC；记录迭代数、warm-up、测量范围和结果。
- Unity 默认脚本验证通过；独立审查重点核对 normal 方向约定、OBB local/world 变换、compound identity 与 comparer 完整性。
- 与现有 overlap/query 回归同时执行，确认不改变旧 API 语义。

## 5. 停止条件与派发

若需要引入 CharacterTest/Monster/Dragon 类型、UnityEngine、浮点随机退化轴或直接位移，停止；若现有 shape 表达不足，先提交公共合同变更。按统一模板回报数学约定、测试向量、分配证据和风险。

## 6. 实际执行记录

| 项目 | 当前记录 |
| --- | --- |
| 实际分支 / 提交 | `main@8a9dd405b` 工作区候选；未提交，保留既有非 LCC 修改 |
| 实际修改文件 | `CombatShapeSeparationResult.cs`、`CombatPhysicsWorld.Separation.cs`、`CombatShapeSeparationTests.cs` 及 `.meta`；按实际 API 回写 `Docs/Interfaces/Combat.md` |
| 测试与原始证据 | 实现者复跑 T3 + 旧 World/OBB 共 61/61；主代理最终复跑 `CombatShapeSeparationTests` 8/8，Unity job `576cbf1039bf40a78968af43224b2a02` |
| 集成 / 回滚结果 | 新增专属 partial/result，不改旧 Narrowphase/Query 调用合同；删除新增文件并回退接口章节即可隔离回滚 |
| 状态历史 | 2026-08-24：`Planned` → `In Progress` → `Verified` |
| 执行者 / 审查者 | 分离查询由实现代理完成；另一代理只读复核一次，两个 P1 定向修复后复算关闭，最终 P0/P1 为 0 |
| API / 形状覆盖 | capsule→AABB/sphere/capsule/OBB/compound leaf；Separated/Touching/Overlapping/UnsupportedShape/InvalidShape；法线为 target→source |
| deterministic / NoAlloc | 覆盖同中心、零长度、近平行端点最小值、共线错位、重复 leaf 与插入顺序；4096 次 warmed bulk 测试 0 B |
| Unity | 默认脚本验证已确认 refresh、compile、domain reload、Editor ready、Console error 0；最终 focused EditMode 8/8 |
| 限制 | M0 只交付几何事实，不产生位移、速度、伤害或 Presence layer；一次旧 OBB 性能组合跑曾为 2.1168 ms 超 2.0 ms，立即单测复跑 1.9893 ms 通过，留待 M1 完整接触性能门禁复测 |
