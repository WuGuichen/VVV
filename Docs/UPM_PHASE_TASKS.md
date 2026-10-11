# MxFramework UPM 阶段任务台账

> Status: Current
>
> Established: 2026-08-23
>
> Program authority: `UPM_OPTIMIZATION_ROADMAP.md`

本文把 UPM 路线固定为 8 个阶段任务，记录阶段边界、依赖和退出条件。每个阶段都有一份位于 `Docs/UPM/Phases/` 的独立任务规格；本台账拥有阶段顺序与状态，独立规格拥有该阶段的交付物、实施切片、验证和回滚要求。

`Docs/Tasks/` 是历史归档，不承载这些活动任务。阶段尚未启动时，独立规格只冻结可执行边界，不宣称实现已存在；进入 `In Progress` 后才按现场依赖创建 Issue 或更细实施切片。完成时把持久事实更新到 Current/Design/Policy，并把一次性执行记录归档。

## 1. 状态与顺序

| ID | 阶段任务 | 状态 | 前置阶段 | 路线映射 | 独立任务规格 |
| --- | --- | --- | --- | --- | --- |
| UPM-P1 | Surface Manifest | Complete | 文档权威收口已完成 | R1 / L0 | [任务规格](UPM/Phases/UPM_P1_SURFACE_MANIFEST.md) |
| UPM-P2 | CharacterTest Extraction | Complete | UPM-P1 | R2 / L1 | [任务规格](UPM/Phases/UPM_P2_CHARACTERTEST_EXTRACTION.md) |
| UPM-P3 | Test Surface Split | Complete | UPM-P2 | R2-R3 / L2 | [任务规格](UPM/Phases/UPM_P3_TEST_SURFACE_SPLIT.md) |
| UPM-P4 | Demo and Samples Split | Complete | UPM-P3 | R2-R3 / L3 | [任务规格](UPM/Phases/UPM_P4_DEMO_SAMPLES_SPLIT.md) |
| UPM-P5 | Editor Portability | Complete | UPM-P4 | R2-R3 / L4 | [任务规格](UPM/Phases/UPM_P5_EDITOR_PORTABILITY.md) |
| UPM-P6 | Embedded Package Cutover | Complete | UPM-P5 | R4 / L5 | [任务规格](UPM/Phases/UPM_P6_EMBEDDED_PACKAGE_CUTOVER.md) |
| UPM-P7 | Consumer Evidence and Optional Matrix | Complete | UPM-P6 | R5-R6 | [任务规格](UPM/Phases/UPM_P7_CONSUMER_EVIDENCE_OPTIONAL_MATRIX.md) |
| UPM-P8 | Preview Release Readiness | Blocked | UPM-P7 | R6 | [任务规格](UPM/Phases/UPM_P8_PREVIEW_RELEASE_READINESS.md) |

允许状态：`Pending`、`Ready`、`In Progress`、`Validated`、`Complete`、`Blocked`。

- `Ready` 只表示前置条件满足，不表示已经开始修改源码；
- `Validated` 表示阶段结果已在候选工作树验证，但尚未完成最终交付或证据登记；
- `Complete` 必须满足该阶段退出条件，并更新本台账及 `UPM_CURRENT_STATUS.md`；
- 后置阶段可以做只读调查，但不得提前改变其生产权威。

## 2. UPM-P1：Surface Manifest

### 目标

把“哪些 assembly 和 Editor initializer 属于哪种 surface”从人工判断变成机器可检查的 owner contract。

### 范围

- 定义并新增 `upm-surface.json`；
- 分类 package root 内全部 asmdef 和 Editor initializer；
- 对六类 owner 的允许依赖、optional gate 和 repository-validation 反向依赖执行机器校验；
- 记录 surface owner、optional gate、project capability 和 absent-capability 行为；
- 让 E0 对未知或未分类 surface fail closed；
- 固化分类器与 manifest 的确定性测试。

### 非目标

- 不移动 CharacterTest、Tests、Demo 或 Editor 文件；
- 不改变 package root、package ID、asmdef identity 或公共 Runtime API；
- 不在本阶段增加新的 optional integration。

### 退出条件

- package root 内未分类 asmdef/initializer 为 0；
- 非法 owner 依赖、optional gate 不匹配和 package → repository-validation 反向依赖为 0；
- 新增未分类项能稳定阻断 E0；
- manifest、checker 与报告输出可重复且有测试；
- 当前 E0 继续保持 0 blocking failure，readiness gap 只按真实变化更新。

## 3. UPM-P2：CharacterTest Extraction

### 目标

把 CharacterTest 作为 repository validation game 迁出基础 package surface，同时保持其框架验证能力。

### 范围

- 在启动阶段冻结 project-owned 目标根和移动清单；
- 迁移 CharacterTest Runtime、Editor、测试及其直接项目资源 owner；
- 使用 Unity 资产写入权威保留 `.meta`、GUID 和序列化引用；
- 修正 assembly dependency，使 project assembly 只依赖 package assembly；
- 记录遗留 package → project 反向依赖并清零。

### 非目标

- 不顺带重构 CharacterTest 功能；
- 不完成整个 Tests surface 拆分；
- 不执行 Embedded Package root cutover。

### 退出条件

- 基础包不再安装 CharacterTest assembly 和项目资源；
- package assembly 对 project assembly 的反向依赖为 0；
- CharacterTest 的编译、关键 focused tests 和正式场景验证保持有效；
- base Consumer 与受影响 optional profile 无新增回归。

## 4. UPM-P3：Test Surface Split

### 目标

把 package contract tests 与 repository-only regression tests 分开，避免把主仓库完整测试面交付给所有 Consumer。

### 范围

- 建立 package-test 与 repository-validation 的机器分类；
- 只保留最小 `Tests/Editor`、`Tests/Runtime` package contract；
- 迁出聚合、CharacterTest、项目场景和工具链测试；
- 更新 asmdef、testables、focused test 名单和 runner 预期名单；
- 保留或替换被移动测试覆盖的真实契约，不以删测试缩小包。

### 非目标

- 不重新设计全部测试框架；
- 不把 repository-only tests 伪装为 `Samples~` 或 Consumer tests；
- 不执行 package root cutover。

### 退出条件

- 每个测试 assembly 只有一个明确 owner；
- package tests 在真实 Consumer 中可运行；
- repository tests 继续在主工程验证对应能力；
- 测试总覆盖没有未经说明的删除或降级。

## 5. UPM-P4：Demo and Samples Split

### 目标

把主工程 Demo 与真正可导入的 UPM Sample 分开，只向 Consumer 提供自包含示例。

### 范围

- 分类现有 Demo 的 project-validation 与 consumer-sample owner；
- 先评估现有 `RuntimeVerticalSlice` 的自包含提取方案，再冻结一个进入 `Samples~` 的最小 vertical slice；若改用更低层 RuntimeHost lifecycle sample，必须记录覆盖差异和不替代现有权威切片的理由；
- 清除该 sample 对主工程 Scene、Config、Tools、FGUIProject 和私有内容的隐式依赖；
- 补齐 sample manifest metadata、导入说明和 smoke；
- 其余 Demo 迁出 package root。

### 非目标

- 不把 CharacterTest 打包成示例；
- 不追求一次发布多个 sample；
- 不以复制项目资产规避 owner 问题。

### 退出条件

- package 中只有明确登记的 consumer-ready sample；
- sample 可在干净 Consumer 导入、编译、运行最小行为且不产生异常日志；
- repository Demo 不随基础包安装；
- sample 资产来源和许可信息可追溯。

## 6. UPM-P5：Editor Portability

### 目标

把 package-owned portable Editor 与 project-only authoring 分开，使干净 Consumer 在缺少项目内容和 optional integration 时安全降级。

### 范围

- 按 Surface Manifest 分类并迁出 project-only Editor；
- 收敛依赖 `Tools/`、`FGUIProject/`、项目 Config、Scene、VFX 和 Bundles 的入口；
- 为 portable Editor 建立 absent-capability、只读定位和无隐式写入契约；
- 覆盖 auto-load、菜单入口、窗口创建与 domain reload smoke；
- 保留 package root 开发回退，直到 UPM-P6 完成 canonical path cutover。

### 非目标

- 不新增公共 Runtime API 来服务 Editor 迁移；
- 不在本阶段删除仍用于过渡开发根的 locator fallback；
- 不把外部 Authoring CLI 合并进 Unity Editor。

### 退出条件

- portable Editor 不要求仓库专有目录存在；
- 缺少项目数据或 optional package 时不抛异常、不写 `LogError`、不隐式修改 Consumer；
- project-only authoring 不随基础包安装；
- 对应窗口和 initializer Consumer tests 通过。

## 7. UPM-P6：Embedded Package Cutover

### 目标

把开发工程 package root 切换到 `Packages/com.vincent.wgameframework`，统一开发工程与 Consumer 的 canonical asset path。

### 范围

- 在 cutover 前由 owner 冻结本次使用的 package ID；若不沿用当前 identity，先以新 ADR 修订目标目录和任务规格；
- 迁移已收敛的 package root，并保留 package 内部资产的 `.meta`、GUID 和 asmdef identity；源根目录自身的 sibling folder `.meta` 作为预期退役特例单独审计；
- 更新 local `file:`、Git subpath、Consumer runner、CI 和文档；
- 更新 package asset locator，只保留 canonical package path；
- 提供 preview breaking migration note；
- 对移动前后 package 内容和 Consumer 资产做哈希/变更审计。

### 非目标

- 不与 CharacterTest、Tests、Demo 大搬迁混成同一切片；
- 不因目录变化拆 Git repository 或多个 UPM package；
- 不在 cutover 过程中改变已冻结的 package ID、公共 API 或模块语义。

### 退出条件

- 开发工程和 Consumer 都从 `Packages/com.vincent.wgameframework` 加载资源；
- canonical root 与 cutover 前的 owner-approved package ID 一致；
- `Assets/Scripts/MxFramework` 开发回退和旧安装示例删除；
- Unity compile、受影响测试、Local/PinnedGit matrix 与 mutation guard 通过；
- migration note 能让现有 Consumer 明确完成 manifest 迁移。

## 8. UPM-P7：Consumer Evidence and Optional Matrix

### 目标

让 Consumer runner 对 candidate、测试、Player 和 optional integration 生成不可误读、可晋升的统一证据。

### 范围

- 升级 report schema，统一 candidate/harness identity、test full names 和 Player build/run 三态语义；
- 建立 E0/E1/E2/E3 matrix 编排和 promotable 计算；
- 对 base、FairyGUI、Unity Transport、VFX Graph 维护 matching Local/PinnedGit 行；
- 固化 package/Consumer mutation guard、报告 hash 和失败分类；
- 明确 FMOD 与 Magica Cloth 保持 O0，或在 owner 批准各自来源与分发边界后另拆 positive profile 实施切片。

### 非目标

- 不用脏 Local 结果晋升 baseline；
- 不把不同 candidate 的 E0、Consumer、Player 结果拼成同一 receipt；
- 不自动提升 optional support tier。

### 退出条件

- 同一完整 SHA 可生成 schema-valid E0/E1/E2 matrix；
- 未执行的 Player gate 明确为 `null`，不会伪装成通过或失败；
- expected test full names、optional source/version、resolved revision 和 artifact hashes 完整；
- 只有满足政策的干净 immutable matrix 才能标记 promotable。

## 9. UPM-P8：Preview Release Readiness

### 目标

补齐发布元数据、Player、升级和兼容门禁，为一个新的 reviewed preview 生成受版本控制的 E3 receipt。

### 范围

- 由 owner 冻结许可证、canonical repository URL 和 public API compatibility policy，并确认 P6 已冻结的 package ID 继续作为本 preview identity；
- 补齐 manifest metadata、Documentation、LICENSE、Third Party Notices 和 sample declaration；
- 增加 tag source smoke、Player run、要求范围内的 optional Player 和 N-1 upgrade；
- 建立 migration fixtures、release receipt 和 tag/version/SHA/tree-hash 一致性检查；
- 对同一干净远端 SHA 执行最终候选矩阵。

### 非目标

- 不把“新的 preview 可晋升”自动解释为 stable `1.0.0`；
- 不由 Development Agent 猜测许可证、仓库权威或支持承诺；
- 不在 P8 内改变 P6 已冻结的 package ID；变更提案必须阻断 P8，并通过新 ADR 和新的 cutover 阶段处理；
- 不为赶发布跳过 P1-P7 的完成条件。

### 退出条件

- 静态 readiness gaps 清零；
- 必需 Consumer、Player、optional 和 upgrade 行全部属于同一 candidate；
- E3 receipt 受版本控制且可反查完整报告；
- 新 reviewed preview 的 version、tag、resolved SHA、package tree hash 和文档声明一致；
- stable 是否启动由 owner 基于 preview 证据另行决定。

## 10. 阶段切片规则

阶段进入 `In Progress` 前，只允许补充调查证据和冻结边界。启动时必须：

1. 从阶段范围生成最小可审计实施切片；
2. 列出实际受影响路径、asmdef/GUID/API 风险和回滚点；
3. 明确本切片验证，不复制整条路线的所有门禁；
4. 一次只允许一个改变生产权威的阶段处于 `In Progress`；
5. 阶段完成后先更新 Current/Design/Policy，再把一次性任务记录归档。

UPM-P1 至 UPM-P7 已完成。活动 P7 候选与 tree identity 统一读取 `UPM_CURRENT_STATUS.md`；固定八行 schema-valid matrix 自动计算 `promotable=true`，并已生成仓库内可独立校验 matrix/receipt report 引用的精简 evidence bundle 与 `releaseReady=false` 的 E3 receipt candidate。UPM-P8 因许可证、canonical repository、版本/tag、兼容政策、平台、N-1 baseline 与最终发布批准尚无 owner 决策而 `Blocked`；不得由实施者猜测。
