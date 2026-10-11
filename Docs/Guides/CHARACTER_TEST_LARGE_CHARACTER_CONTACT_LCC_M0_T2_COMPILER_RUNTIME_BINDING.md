# LCC-M0-T2：Compiler 与 Runtime binding v4

> Status: Guide
>
> 工作包：LCC-M0-T2（生产实现）
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
> 派发门禁：T1 已 Verified，M0 为 `Production-Frozen`；本任务可与 T3 并行，但不得修改 T1 保存实现、Combat 或父状态。
>
> 前置：T1 `Verified`；在 T3 尚未完成时可与 T3 并行，双方写入面不得交叉。

## 1. 任务结果与非目标

Authoring compiler 将 Current Presence 确定性编译为 geometry binding v4 与 source mapping；RuntimeSpawn 同时读取 v1～v4，向 CharacterTest 提供只读通用 Presence binding，并提供不参与Gameplay的body-geometry author-center/runtime-consumed诊断投影。缺失 Presence 保持既有输出兼容。

非目标：不创建 Combat body、不求 separation、不推挤角色、不制作 Dragon14 fixture或 overlay。

## 2. 写入所有权

- Owner：`CharacterAuthoringCompiler.cs` 及专属 compiler partial、compiler tests；`Packages/com.vincent.wgameframework/Character.RuntimeSpawn/CharacterRuntimeSpawnContracts.cs`、`CharacterImportedPackageJson.cs` 及 RuntimeSpawn tests。
- 可修改正式 binding format 常量/source mapping，但仅限 T0 冻结的 v4 追加。
- 只读：T1 Authoring DTO/load-save、CombatPhysics、CharacterTest motion/debug、Dragon14 作者 JSON。
- 共享 compiler 文件由本任务唯一写入；T1/T4 不并行写该文件。

## 3. 实施清单

- [x] Current Presence（含显式空）输出 v4；Missing 保持既有 v1～v3 选择与字节/hash 行为。
- [x] 稳定排序并编译 shape、local pose、skin、pressure、enabled、revision/content hash；单位和旋转表达与 T0 一致。
- [x] source mapping 精确指向 `geometry/body_presence.json` 和 volume 对象路径。
- [x] source/package identity 覆盖 Presence；future/invalid 不生成部分 binding。
- [x] Runtime DTO/parser 接受 v1～v4；旧版生成空 Presence 集，新版验证容量、shape、数值和身份后 fail closed。
- [x] compiler/import DTO 暴露低频只读的 author center X/Z 与 Runtime 实际消费 center 事实；不改变 root capsule、motor 或 formal snapshot。
- [x] Runtime 不读取作者 JSON，不依赖 Unity Transform scale，也不把 Presence 默认为 Hard Root/Hurtbox。
- [x] 测试覆盖 Missing 输出稳定、v4 round-trip、顺序扰动、invalid/future 拒绝、v1～v3 compatibility、source mismatch/hash。

## 4. 验证与证据

- Authoring compiler focused tests 与 RuntimeSpawn EditMode tests 均通过，并记录生成 binding 的 format/schema/hash 样本。
- 运行 Unity 默认脚本验证；证据必须包含 refresh、compile、domain reload、Editor ready、Console 0。
- 审查程序集边界与分配：解析可低频分配，运行时 binding 读取不得在 tick 解析 JSON。
- 对同输入重复编译做字节级一致性；对 v1～v3 fixture 验证未被误升级。

## 5. 停止条件与派发

若 v4 需要破坏旧字段、改变 source identity 总合同或需迁移正式资产，停止回到主代理评审；不得静默改 v1～v3。回报实际格式、兼容矩阵、文件、测试、Unity 证据和残余风险。

## 6. 实际执行记录

| 项目 | 当前记录 |
| --- | --- |
| 实际分支 / 提交 | `main@8a9dd405b` 工作区候选；未提交，保留既有非 LCC 修改 |
| 实际修改文件 | `CharacterAuthoringCompiler.cs`、`CharacterPackageTests.Compiler.cs`、`CharacterRuntimeSpawnContracts.cs`、`CharacterImportedPackageJson.cs`、`CharacterRuntimeSpawnFirstSliceTests.cs` |
| 测试与原始证据 | Authoring `character-weapons` focused 通过；主代理 Unity focused 3/3，job `b93dce968bdc498c8552cf9b834e27c8`；生产程序集 build 0 warning/0 error |
| 集成 / 回滚结果 | v4 为纯追加；Missing 继续走 v1～v3，删除新增 v4 字段/分支和 tests 即可隔离回滚，无正式资产迁移 |
| 状态历史 | 2026-08-24：`Planned` → `In Progress` → `Verified` |
| 执行者 / 审查者 | binding 实现代理完成；另一代理完成唯一一次 schema 只读审查，P0/P1 为 0，主代理复跑关键 focused tests |
| compiler / RuntimeSpawn | Current/显式空 Presence 生成 v4；Runtime v1～v3 为 `CompatibilityHardRoot`，v4 为严格 `Authored`；中心 X/Z 仅投影诊断事实 |
| compatibility / hash | Missing 的 format/source hash/geometry hash/序列化 bytes 保持；按 volumeId ordinal 排序；内容变化使 Presence/source/geometry identity 同步失效 |
| Unity | refresh、compile、domain reload、Editor ready 均观察到，Console error 0；最终 focused 3/3 |
| 限制 | 未迁移正式角色资产、未接 Gameplay/CharacterTest；Dragon14 实际 v4 生成与 overlay 由 T4 完成 |
