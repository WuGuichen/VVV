# LCC-M0-T1：Body Presence Authoring Core

> Status: Guide
>
> 工作包：LCC-M0-T1（生产实现）
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
> 派发门禁：T0 已 Verified，M0 已 `Production-Frozen`；本任务可与 T3 并行，但不得越过本文 owner 或修改父状态。
>
> 前置：T0 `Verified`；可与 T3 并行。

## 1. 任务结果与非目标

Authoring Core、CLI/service 能无损读取、校验、编辑和以独立单文件事务保存 `geometry/body_presence.json` v1，并给出稳定 canonical 内容与 typed diagnostics；同时对 `body_geometry.json` 非零 center X/Z 给出“当前 Runtime 仅消费 Y”的合同债务诊断。缺失文件不补写，future schema 不覆盖。

非目标：不输出 binding v4、不改 RuntimeSpawn/Combat/CharacterTest、不制作 Dragon14 正式 fixture、不实现完整 CharacterStudio 编辑器，也不把既有整包多文件 `SaveCharacterPackage` 改造成 package-wide staging/rollback 事务。

## 2. 输入与依赖

- [M0 合同](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M0_CONTRACT_FOUNDATION.md)第 2.1、2.2、2.4 节和 T0 冻结记录。
- `CharacterAuthoringGeometry.cs`、`CharacterResourcePackage*.cs`、`CharacterPackageValidation.cs`、`CharacterPackageCommands.cs`、`EditorServer.cs` 及 Authoring tests。
- 路径上的项目规则、`Docs/API_STANDARDS.md`、`Docs/QUALITY_GATE.md` 和 Authoring pipeline 文档。

## 3. 写入所有权

- Owner：`Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/CharacterPackages/CharacterAuthoringGeometry.cs`、package schema/load/save/validation 与 body-geometry typed diagnostic 职责文件、CLI package/service 入口、对应 Authoring tests。
- 可新增同目录 `CharacterAuthoringBodyPresence*.cs`，但不得建立旁路 package store。
- 只读：`CharacterAuthoringCompiler.cs`（T2 owner）、RuntimeSpawn、Combat、CharacterTest、Dragon14 正式作者包、CharacterStudio web。
- 禁止修改父状态文档；若共享 partial class 必须先按 T0 ownership 拆到本任务专属文件。

## 4. 实施合同与清单

- [ ] 实现 v1 DTO、shape/pose/value 类型和 `volumes[]` 总数最大 4 的验证；禁用项同样占容量并接受完整字段校验。
- [ ] 区分 Missing、Current、Invalid、Future/ReadOnly；保留 future 原始 UTF-8 payload。
- [ ] 重复 ID、非 ModelRoot、非法 shape/尺寸/数值、超限和未知引用返回 T0 冻结的 typed code。
- [ ] 对 `body_geometry.json` 非零 center X/Z 发出冻结 typed diagnostic，明确 source 值与“Runtime 未消费”事实；不改 motor/root capsule 语义，也不阻断本来合法的包编译，除非 T0 另有字段级结论。
- [ ] canonical 序列化按 `volumeId` ordinal，覆盖禁用项和全部字段，不依赖 JSON 属性/集合顺序。
- [ ] 接入 package load、CLI validate/read/write 和 EditorServer service；Body Presence 写入的权威路径是独立 save operation，携带 expected package revision，复用既有 package 路径解析、同一校验与 write-eligible gate，并只通过一次 `WriteJsonFileAtomic` 替换 `body_presence.json`。CharacterStudio T5 必须调用该独立入口；既有整包保存可读取/保留 Presence 状态，但不得成为绕过此入口的第二条 Presence 写路径。
- [ ] 缺失文档读取不创建默认、不标脏；future/invalid 禁止被默认 DTO 覆盖。
- [ ] 添加 round-trip、顺序扰动、missing、explicit empty、invalid、future raw preservation、revision conflict 和单文件原子替换失败测试；失败不得改变原 `body_presence.json` 字节，也不得遗留可被正式读取的临时内容。

## 5. 验证与完成证据

- 运行 Authoring focused tests 和 CLI validate/round-trip fixture；记录命令、通过数和失败原文。
- 比较同语义不同顺序的 canonical bytes/hash；Body Presence 独立保存失败前后目标文件字节必须一致。不得把该证据表述为整个 character package 的多文件原子性。
- 审查 Runtime/noEngine 边界，无 `UnityEngine`/`UnityEditor` 依赖。
- 任务只可到候选完成；主代理复跑关键测试并核对 diff 后决定状态。

## 6. 停止条件与派发

若需要改 binding format、Runtime parser、Dragon14 源文件或 CharacterStudio UI，停止并交给 T2/T4/T5；若独立 Body Presence 保存无法在现有 package 路径、revision 与原子文件替换设施上保证 future raw preservation，报告阻断而不建第二套 store。若实现被迫要求通用整包 staging/rollback，停止并由主代理重新定界。按统一模板回报实际文件、测试、未验证项和风险。

## 7. 实际执行记录

| 项目 | 当前记录 |
| --- | --- |
| 实际分支 / 提交 | `main@8a9dd405b` 工作区候选；未提交，保留既有非 LCC 修改 |
| 实际修改文件 | Body Presence Core/validation/CLI/EditorServer 专属 partial，四个最小共享 hook，以及 focused tests |
| 测试与原始证据 | Authoring tests project build：0 warning/0 error；`character-body-presence` focused exit 0；CLI `read`/`validate` exit 0 |
| 集成 / 回滚结果 | 主代理复核并补齐 required-property fail-closed、capsule height、route 顺序和 Presence 专属 temp cleanup；复跑通过 |
| 状态历史 | 2026-08-24：`Planned` → `In Progress` → `Verified` |
| 执行者 / 审查者 | 实现代理 `/root/lcc_m0_t1_authoring`；主代理集成审查 |
| 实际文件 | `CharacterAuthoringBodyPresence.cs`、`CharacterPackageValidation.BodyPresence.cs`、`CharacterPackageCommands.BodyPresence.cs`、`EditorServer.BodyPresence.cs`、必要 hooks/tests |
| Authoring / CLI tests | focused、build、CLI read/validate 通过；全套入口被既有 `ResourceMappingProfileV2Tests.FirstPartySync_ConsumesReviewedDecisionsWithoutAliases` 失败阻断，堆栈不在 T1 链路 |
| 兼容 / raw preservation | Missing 不补写；Current canonical/hash 稳定；Future/Invalid 禁止覆盖；revision/atomic failure 保持原 bytes；整包保存不是第二 writer |
| 限制 | 真实 HTTP listener 与 CharacterStudio 浏览器闭环留给 T5；T1 不声明 compiler、Runtime 或可玩接触行为 |
