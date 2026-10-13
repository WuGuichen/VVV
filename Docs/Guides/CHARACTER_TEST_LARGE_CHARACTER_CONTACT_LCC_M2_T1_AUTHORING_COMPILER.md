# LCC-M2-T1：部位伤害 Authoring、Compiler 与 Dragon14 定义

> Status: Guide
>
> 工作包：LCC-M2-T1（生产实现与正式数据）
>
> 状态权威：[LCC-M2 第 5 节](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M2_WOUND_BREAK.md)
>
> 执行文档状态：`Verified`
>
> 派发、集成与回滚规则：[LCC 单任务执行文档模板](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_TASK_EXECUTION_TEMPLATE.md)
>
> 并行规则：除本文明确写明“可并行”外，不与同里程碑其它生产写入并行；跨里程碑只允许只读调查。
>
> 必读基线：总台账第 1～8 节、父里程碑冻结合同、本任务全部章节、路径上的 AGENTS.md、Docs/API_STANDARDS.md 和 Docs/QUALITY_GATE.md；再按本文输入/所有权加载专项接口与源码。
>
> 子代理派发合同：本文是唯一任务边界；只写 Owner 范围，保留工作区其它修改，不改父状态。完成回报逐项列出实际文件、合同/兼容/失败行为、测试原始结果、未执行验证、风险冲突、集成与回滚建议。实现者不得担任 schema、network、碰撞数学、拓扑或正式资产任务的最终独立审查者。
>
> 两级派发门禁：只有 T0 只读审计可在全路线 Audit-Frozen 后派发。T0 只写自身冻结表并结构化回报精确文件/partial owner、测试 filter、Docs/Evidence/LCC/<TASK-ID>/ 证据文件名和交接点；T1+ 必须等待主代理把这些结论回写到生产任务，并将本里程碑标记为 Production-Frozen。
>
> 前置：T0 `Verified`。

## 1. 任务结果与非目标

Authoring/CLI可无损管理 `geometry/body_part_damage.json`，compiler/runtime binding确定性提供固定容量part definitions，Dragon14具有可校准的正式部位分组定义。

非目标：不维护运行时状态、不修改damage/selector/network/UI、不引入Severed。

## 2. 写入所有权

- Owner：Authoring geometry/package/validation/compiler/CLI service专属职责、RuntimeSpawn imported part-definition binding、对应Authoring/RuntimeSpawn tests。
- Owner：T0确认的Dragon14作者 `body_part_damage.json` 和通过正式生成链产生的versioned outputs。
- 只读：CharacterTest reducer/damage/selector/network、CharacterStudio web；正式生成结果不得手改。

## 3. 实施清单

- [x] DTO覆盖partStateId、BodyPart成员、wound/structure阈值、倍率、动态priority/Focus政策和T0冻结字段；最多8组，阈值/倍率/priority边界遵循T0；`focusConsumeTag`只存显式语义，不把damage tag解释为consume。
- [x] validation覆盖稳定唯一身份、成员重叠/缺失、阈值/倍率/priority范围、容量、非法状态和typed codes。
- [x] Missing/Current/Future/Invalid 四态明确；invalid/future保留raw且write-ineligible；transaction不部分写。
- [x] canonical/hash/source mapping按稳定ordinal；compiler输出v5 binding且v1～v4仍可读。
- [x] Runtime binding提供最多8组的稳定数组/lookup，导入时校验source identity/hash，tick不解析JSON。
- [x] Dragon14按已有BodyPart聚合为head、left wing、right wing、tail四组；Focus tag为空，初始阈值留待T6 Playable校准。
- [x] round-trip/order/future/missing/invalid/compiler byte stability/旧binding compatibility/Dragon引用测试。

## 4. 验证与停止

运行Authoring、CLI、RuntimeSpawn focused tests、重复生成diff、Unity脚本验证；保留生成报告/hash/source mapping。若BodyPart身份无法稳定映射、生成链会丢future raw或需要UI旁路存储，停止。回报文件、schema、fixture、tests/Unity和校准限制。

## 5. 实际执行记录

| 项目 | 当前记录 |
| --- | --- |
| 实际分支 / 提交 | `main@8a9dd405b` 工作区候选；未提交，保留既有M0/M1、CharacterTest及用户修改 |
| 实际修改文件 | Body-part-damage Core DTO/codec、validation partial、CLI/service partial与最小loader/server/compiler hooks；RuntimeSpawn contracts/parser；Authoring/RuntimeSpawn focused tests；Dragon14作者文件及正式生成输出 |
| 测试与原始证据 | Authoring runner `character-body-part-damage`：4 cases，主代理修订后复跑 exit 0；CLI `character body-part-damage validate`：exit 0、status ready；最终 Unity validator job `fabb807c80ba451aabc0d1c087b41595`：refresh/compile/domain reload/ready，0 Console errors；最终 EditMode job `5f7191180f69443e8b30b5e472279dbe`：4/4 passed |
| 集成 / 回滚结果 | Dragon14正式import写入v5 geometry/compile/import产物，报告49 artifacts、5 updated、44 skipped、0 conflict/error；后置全局ResourceMapping profile因既有`RESOURCE_MAPPING_BUILD_TARGET_REQUIRED`使命令exit 1，未扩写兼容层；回滚为移除作者文件并重跑正式生成链 |
| 状态历史 | 2026-08-24：`In Progress` → `Verified`；主代理复核 diff，补齐 unknown-format Invalid 与 Focus tag 前缀两处 fail-closed 边界后，focused/Unity 证据全部复跑通过 |
| 执行者 / 审查者 | 实现代理 `/root/lcc_m2_t1_authoring`；主代理完成集成检查；未增加任务级独立评审，M2唯一最终只读审查留到T6 |
| Authoring / compiler / binding | `mx.characterBodyPartDamage.v1`/`1.0`四态、max 8与T0边界；Current canonical save/hash；Future/Invalid raw只读；binding升级`mx.characterGeometryBinding.v5`/`5.0`并保留v1～v4读取；source format/schema/revision/hash进入package/binding identity，非法/hash mismatch typed fail closed |
| Dragon14 definitions | 4组/10个BodyPart lookup：head、left wing、right wing、tail(base/mid/tip)；正式binding hash `sha256:22197f9c611871cd40bbe28d48d43243b1082d614e5135b59b23ab3e978d8714`；Focus tag为空 |
| tests / Unity | 最终重复compile两次均exit 0；`compile_result`、config patch、geometry binding、resource mapping、write plan、gate report共6个文件逐字节一致，geometry SHA-256 `c27d1826a62c48eca670ea8a9f1fec1fc8e04d06509c6705954124ab5d0e48b5` |
| 限制 | 未跑全仓测试、真实HTTP listener、Playable或独立审查；Dragon初始阈值尚未做T6实战校准；正式import的全局ResourceMapping后置项仍由既有build-target状态阻断；这些不阻断T1 Authoring/compiler/binding合同 |
