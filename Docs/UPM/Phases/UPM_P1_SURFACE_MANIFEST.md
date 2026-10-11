# UPM-P1 Surface Manifest 任务规格

> Status: Current
>
> Task State: Complete
>
> Completed: 2026-08-26
>
> Phase: UPM-P1 / R1 / L0
>
> Depends On: Documentation authority closeout (`Complete`)
>
> Hands Off To: UPM-P2 CharacterTest Extraction

## 1. 阶段身份与依赖

UPM-P1 是 8 个 UPM 阶段中第一个可启动的生产阶段。它的前置“文档权威收口”已经完成，因此状态为 `Ready`；`Ready` 只表示允许启动，不表示 manifest、owner 语义校验或本阶段证据已经完成。现有 E0 checker 已具备 surface schema/完整性校验骨架，本阶段必须扩展该单一实现，不能另建第二套 classifier。

本阶段必须先完成，UPM-P2 才能依据机器 owner contract 迁出 CharacterTest。后续阶段可以只读调查，但在 P1 完成前不得移动 package surface、改变 assembly identity 或切换 package root。

活动 package 数量、asmdef 数量、initializer 数量、tree hash 和 E0 结果只读取 [UPM_CURRENT_STATUS.md](../../UPM_CURRENT_STATUS.md)，本文不复制这些易变化数字。

## 2. 目标结果

把 package root 内 assembly 与 Editor initializer 的所有权从人工判断转成可重复、fail-closed 的机器合同：

- 每个 asmdef 恰好拥有一个合法 surface owner；
- 每条 assembly dependency 都满足 owner 允许依赖矩阵、optional gate 和 repository-validation 反向依赖规则；
- 每个自动 Editor 入口完整声明 activation、输入、缺失能力行为与写入边界；
- E0 严格模式对漏登、陈旧、非法或不确定条目稳定失败；
- 同一工作树重复扫描得到同序、同义的 inventory 与 JSON report；
- 为 P2-P5 留下 asmdef identity、`.meta`/GUID 与反向依赖基线。

## 3. 权威输入

- [UPM_PHASE_TASKS.md](../../UPM_PHASE_TASKS.md)：阶段边界、依赖、状态与退出条件；
- [UPM_CURRENT_STATUS.md](../../UPM_CURRENT_STATUS.md)：唯一 Current 数字与最近证据；
- [UPM_OPTIMIZATION_ROADMAP.md](../../UPM_OPTIMIZATION_ROADMAP.md)：R1 顺序和跨阶段验证；
- [UPM_PACKAGE_SURFACE_LAYOUT_OPTIMIZATION.md](../../UPM_PACKAGE_SURFACE_LAYOUT_OPTIMIZATION.md)：owner 枚举、initializer 字段和 L0 规则；
- [UPM_CONSUMER_BASELINE_EVIDENCE_OPTIMIZATION.md](../../UPM_CONSUMER_BASELINE_EVIDENCE_OPTIMIZATION.md)：E0、严格开关、确定性报告与失败语义；
- [UPM_OPTIONAL_INTEGRATION_OPTIMIZATION.md](../../UPM_OPTIONAL_INTEGRATION_OPTIMIZATION.md)：base absence、optional closure 与 adapter owner；
- 仓库根 `AGENTS.md`：程序集边界、Unity 资产权威和默认验证规则。

若文档与机器扫描冲突，先报告冲突并修正权威，不得为通过检查静默漏登文件。

## 4. 进入条件

1. 对当前 `HEAD` 运行 E0；若 candidate commit、package tree hash 或活动计数与 `UPM_CURRENT_STATUS.md` 不一致，先刷新 Current snapshot，再进入 manifest 分类。
2. 记录启动时完整 Git 状态，区分本阶段改动与用户已有改动。
3. 从当前过渡 package root 扫描全部 `.asmdef`、对应 `.meta`、assembly name/reference、define/version constraint 和自动 initializer；不得采用旧文档中的固定数量。
4. 冻结本轮允许写入的文件清单和报告输出目录；报告不得写入 package root。
5. 确认 owner 枚举、owner 允许依赖矩阵、initializer 必填字段和 optional gate 与权威文档一致。
6. 明确本阶段不移动任何 Unity 资产；若扫描发现必须先移动才能分类，阶段应标记 `Blocked` 并提交 owner 决策，而不是扩大范围。

## 5. 明确交付物

实施 P1 时必须交付：

| 交付物 | 预期位置或类型 | 内容要求 |
| --- | --- | --- |
| Surface manifest | `Assets/Scripts/MxFramework/upm-surface.json` | schema version；全部 assembly owner；全部 initializer 合同；稳定排序 |
| E0 classifier/checker | `Tools/Harness/check_upm_package_boundary.py` | 在现有实现上严格校验 manifest、漏登、陈旧条目、非法 owner/字段、owner-to-owner dependency、optional gate、repository-validation 反向依赖和确定性输出 |
| Checker focused tests | `Tools/Harness/tests/test_check_upm_package_boundary.py` | 正向、漏登、陈旧、非法 owner、非法 owner dependency、optional gate 不匹配、repository-validation 反向引用、initializer 字段、重复输出与严格模式用例 |
| Assembly inventory | `Temp/MxFramework/UPM/UPM-P1/assembly-inventory.json` | assembly name、asmdef path/GUID、owner、references、gate、反向依赖 |
| Initializer inventory | `Temp/MxFramework/UPM/UPM-P1/editor-initializers.json` | source path、classification、activation、required inputs、missing behavior、writes |
| E0 reports | `Temp/MxFramework/UPM/UPM-P1/e0-preview.json` 与 `e0-strict.json` | candidate/package identity、manifest coverage、failure/gap、报告 hash |
| 完成登记 | `Docs/UPM_PHASE_TASKS.md`、`Docs/UPM_CURRENT_STATUS.md` | 只在证据齐全后更新状态与持久事实，不复制报告明细 |

`Temp/` 报告是本地执行证据，不纳入版本化 package content；完成登记必须记录可复核的命令、候选身份和报告 hash/摘要。

## 6. 实施切片及顺序

1. **P1-S1 只读 inventory**：扫描 assembly、GUID、initializer、optional closure 与 reverse dependency，生成稳定清单，不改生产文件。
2. **P1-S2 schema 与 owner review**：为每个 assembly 分配唯一 owner；逐项复核 `repository-validation` 和 `optional-adapter`，禁止用笼统 default 掩盖未知项。
3. **P1-S3 manifest 落地**：写入 `upm-surface.json`，按确定性规则排序；initializer 使用相对 package root 路径。
4. **P1-S4 E0 fail-closed**：扩展现有 checker；preview 模式报告 readiness，严格模式要求 manifest，并对漏登/陈旧条目、非法 owner dependency、optional gate 不匹配和 package → repository-validation 反向引用失败。
5. **P1-S5 focused tests**：先用最小 fixture 覆盖失败语义，再覆盖当前仓库 inventory 和重复执行一致性。
6. **P1-S6 候选验证与登记**：对同一工作树运行 preview/strict E0、检查 diff，确认没有资产移动后更新 Current 与阶段状态。

每个切片必须可独立审阅；任何清单变化都要解释为扫描修正或 owner 决策，不能夹带 P2 迁移。

## 7. 范围与非目标

范围包括 manifest schema/数据、E0 classifier、focused tests、inventory 与退出证据。

非目标：

- 不移动 CharacterTest、Tests、Demo、Editor 或任何 Unity 资产；
- 不改变 package root、package ID、package version、asmdef identity、GUID 或公共 Runtime API；
- 不新增 optional integration，不提升 support tier；
- 不补齐 LICENSE、Documentation、samples 等 P8 release metadata；
- 不生成 E2/E3 release claim。

## 8. 关键不变量

- Runtime assembly 不含 `UnityEditor`；noEngine assembly 不含 `UnityEngine`。
- 合法 assembly owner 仅为权威布局方案定义的六类；禁止 `unclassified`。
- initializer 集合必须与静态检测集合精确相等；漏登和陈旧条目都失败。
- P1 只如实登记 initializer 的 activation、缺失输入行为和写入边界；发现缺输入时 `LogError` 或隐式写 Consumer `Assets`，应记录为 P5 portability blocker，而不是在 P1 内修复或把理想行为写入 manifest。
- `repository-validation` 仍位于过渡 package root 时只表示待迁 owner，不代表已经迁出。
- E0 只证明静态边界；不能替代 Unity import、Consumer 或 Player 证据。

## 9. 逐层验证

1. **文档/schema 层**：JSON 可解析，字段/枚举合法，相对路径无绝对路径或 `..`。
2. **inventory 层**：manifest 与当前 asmdef、initializer 集合双向完全匹配；assembly name 唯一；GUID/identity baseline 完整。
3. **checker 层**：focused tests 覆盖新增、删除、改名、非法字段、陈旧项、非法 owner dependency、optional gate 不匹配、repository-validation 反向引用、严格开关和确定性输出。
4. **静态边界层**：E0 preview 与 `--require-surface-manifest` strict 都对同一 package tree 运行；strict 为 0 blocking failure。
5. **仓库层**：`git diff --check`；diff 只包含约定 manifest/checker/tests/docs，不含 Unity 资产移动或 `.meta` 改写。

本阶段没有普通 Unity C# 生产改动时不虚构 Unity runtime 证明；若实施中实际触及 Unity C#，按根 `AGENTS.md` 追加 Unity refresh/compile、Editor ready 和 Console 0 error。

## 10. 失败与回滚

- inventory 无法唯一分类：停止写 manifest，保留只读报告，向 owner 提交冲突列表。
- checker 与现有 preview policy 冲突：保持旧入口可运行，单独回滚严格开关改动，不降低失败条件。
- manifest 导致大量非预期 failure：回到 inventory/schema 切片修正；禁止通过删除 assembly、initializer 或测试获得通过。
- 确定性失败：固定排序、路径规范化与报告字段后重跑，不接受手工整理报告。
- 回滚时只撤销本阶段已确认文件；保留用户原有工作树与本地报告，不使用破坏性清理。

## 11. 退出证据

P1 只有同时满足以下证据才可从 `Ready/In Progress` 进入 `Validated` 或 `Complete`：

- manifest 对当前 package root 的 asmdef/initializer 未分类数为 0；
- owner 依赖矩阵、optional gate 和 repository-validation 反向依赖检查为 0 blocking failure；
- 新增未知 asmdef 或 initializer 的 fixture 能稳定阻断 strict E0；
- checker focused tests 全部通过，连续两次报告语义一致；
- strict E0 为 0 blocking failure，readiness gap 只反映真实未完成项；
- asmdef identity、GUID 与 reverse dependency baseline 已生成；
- diff 审计证明没有执行 P2-P6 的移动或 cutover；
- Current 与阶段台账已按实际证据更新，未把 E0 表述成 Consumer/release 通过。

## 12. 完成证据（2026-08-26）

- 实现 lineage base：`7af1027762d15a1fe437bf0e9f695c518b7f6d48`；完成候选以 package tree hash 标识，不在文档内嵌自引用 commit，也不声明远端可达或 E2/E3 release evidence；
- 正式资产：`Assets/Scripts/MxFramework/upm-surface.json` 及 Unity 生成 `.meta`，GUID `ed01dca320e56944a83b2ba0a8ea2092`；
- 覆盖：133 个 asmdef、39 个 initializer，未分类/陈旧条目均为 0；owner counts 为 base-runtime 102、optional-adapter 7、portable-editor 6、repository-validation 18；
- 语义门禁：owner dependency、optional owner/gate 和 package → repository-validation 反向依赖均为 0 blocking failure；
- focused tests：`Tools.Harness.tests.test_check_upm_package_boundary` 为 16/16；包含漏登、陈旧、非法 owner、非法依赖、optional owner、ungated adapter 和 initializer contract 负向用例；
- strict E0 policy v3：0 blocking failure、7 readiness gaps；package tree hash `85a60bf29fd0433b0da4b57dcd02f4cf05fc45d228159da7cac375879dd7a677`；
- 确定性：两次 strict report SHA-256 均为 `ee19f10719edb2d0b04b0f0a5b9ab47c48cfb040ec01a844cfdbcb57c0014c1d`；
- 历史执行曾生成 `Temp/MxFramework/UPM/UPM-P1/assembly-inventory.json`、`editor-initializers.json`、`e0-preview.json`、`e0-strict.json`；这些临时原始文件未做耐久保留，当前只能依据本文登记的哈希和可重跑 checker 复核机器事实，不得宣称仓库内仍保存原始 P1 artifacts；
- Unity 导入：Unity `6000.5.0f1` BatchMode import 返回 0，TextScriptImporter 导入 manifest 并生成 `.meta`；未运行 Unity gameplay/runtime tests，因为本阶段没有修改 Unity C# 或运行时资产。

## 13. 移交 UPM-P2

向 P2 移交 manifest、assembly/GUID inventory、reverse dependency baseline、CharacterTest owner 集合以及仍需人工确认的边界项。P2 必须从该基线生成移动清单；若 P2 发现漏登，先返回修正 P1 合同，不能在迁移脚本中维护旁路分类。
