# UPM-P8 Preview Release Readiness

> Status: Current
>
> Task State: Blocked
>
> Phase: UPM-P8 / R6
>
> Depends On: UPM-P7 Consumer Evidence and Optional Matrix (`Complete`)
>
> Hands Off To: Owner-reviewed Preview Publication; Stable Train Decision

本文是 UPM-P8 的实施任务规格。它把 P7 的同一候选证据补齐为一个可审查的 preview 发布候选，但不表示 metadata、legal、tag、Player、升级或 E3 receipt 已完成。活动版本、candidate、readiness gaps、测试数量和最近 ledger 只读取 [`UPM_CURRENT_STATUS.md`](../../UPM_CURRENT_STATUS.md)。

当前阻塞于 owner 决策：许可证及 manifest 表达、继续使用的 package ID、canonical repository/访问边界、preview version 与 annotated tag、public API compatibility/deprecation、支持平台、optional 最高 tier、Third Party Notices 结论、N-1 baseline，以及最终 preview publication 批准。自动可修复的 manifest `category`、`keywords` 与 `Documentation~` 已完成；strict E0 仅余 manifest license、`LICENSE.md` 和 `Third Party Notices.md` 三项 legal gap。P7 技术证据已完成，但实施者不得代为选择这些法律与支持承诺。

## 1. 阶段身份与目标结果

UPM-P8 是八阶段路线的发布收口阶段。它不再改变 package surface，而是让 owner 对身份、许可和支持承诺作出明确决策，并对 P7 的同一干净 candidate 补齐 metadata/legal、TaggedGit source、Player build+run、适用 optional Player、N-1 upgrade、migration/API compatibility 与受版本控制 E3 receipt。

目标结果是一个新的 **reviewed preview** 可以被明确 SHA/tag 消费、由 receipt 反查全部证据，且所有声明与实际支持 tier 一致。Preview readiness 不等于 stable readiness；完成 P8 也不会自动授权 `1.0.0`、RC 或 stable 发布。

## 2. 权威输入

实施者必须在阶段启动和创建 tag 前分别重新读取：

- [`UPM_PHASE_TASKS.md`](../../UPM_PHASE_TASKS.md)：P8 状态、依赖、范围和退出条件；
- [`UPM_CURRENT_STATUS.md`](../../UPM_CURRENT_STATUS.md)：唯一活动 candidate、版本、readiness gaps 和 evidence ledger；
- [`UPM_OPTIMIZATION_ROADMAP.md`](../../UPM_OPTIMIZATION_ROADMAP.md)：R6 release train 与 owner 边界；
- [`UPM_CONSUMER_BASELINE_EVIDENCE_OPTIMIZATION.md`](../../UPM_CONSUMER_BASELINE_EVIDENCE_OPTIMIZATION.md)：P7 E2 matrix、E3 晋升语义与 receipt hygiene；
- [`UPM_OPTIONAL_INTEGRATION_OPTIMIZATION.md`](../../UPM_OPTIONAL_INTEGRATION_OPTIMIZATION.md)：optional tier、Player 深度和 FMOD/Magica Cloth 分发边界；
- [`UPM_RELEASE_UPGRADE_READINESS_PLAN.md`](../../UPM_RELEASE_UPGRADE_READINESS_PLAN.md)：legal/metadata、tag、Player、N-1、compatibility 和 release receipt 政策；
- [`ADR-015`](../../Decisions/ADR-015-upm-embedded-package-topology.md)：canonical Embedded Package 决策及未冻结的 owner 决策项。

## 3. Owner 决策点

以下决策不得由 Development Agent 猜测。缺失时 P8 保持 `Blocked` 或 `Pending`，不得以占位文本创建对外 preview：

1. MxFramework 自身许可证及 manifest `license`/`licensesUrl` 表达；
2. 确认 P6 owner-approved package ID 继续作为本 preview identity；若要求改变，P8 立即阻断，另立新 ADR 和新的 cutover 阶段，完成路径迁移与全矩阵重跑后才能重新进入 P8；
3. canonical repository URL，以及私有/离线 Consumer 的可访问与凭据边界；
4. preview 版本号、annotated tag 命名和发布渠道；
5. public C# API compatibility/deprecation policy，以及 preview breaking change 的版本与 migration note；
6. 支持平台列表和每个平台 Player 验证责任；
7. optional integration 在该 preview 中声明的最高 tier；FMOD 未获分发/许可/LFS/平台/bank ownership 批准、Magica Cloth 未获来源/许可/分发与平台责任批准时必须保持 O0；
8. Third Party Notices 的审计结论与 sample 资产许可；
9. N-1 选择：上一条已批准 preview/公开 train，以及支持的 config、SaveState、serialized asset、network/wire 兼容域。

每项决策都要有可追溯 owner 记录或受版本控制政策引用。Agent 可以准备选项、差异和证据，但不能代签支持承诺。

## 4. 进入条件

只有以下条件全部满足，阶段才能进入 `In Progress`：

1. UPM-P7 已 `Complete`，且交付 schema-valid E2 matrix 和 E3 receipt candidate；
2. P6 的 package ID 决策记录存在且 owner 确认继续用于本 preview；identity 变更提案已按第 3 节转出本阶段；
3. 所有 P7 required rows 指向同一远端可达完整 SHA、同一 package version/tree hash；
4. candidate checkout 与 release harness 均干净，package/Consumer mutation guard 通过；
5. 当前 preview train、N-1 baseline 和 package changelog 目标已由 owner 确认；
6. legal/metadata owner 决策已具备，或已明确把该项作为阻断性切片先处理；
7. tag 尚未创建；版本/tag/source smoke 的顺序可以按本规格执行；
8. Player fixture 和 upgrade fixture 的写入权威明确，Unity 序列化场景/资产由 Unity Editor、Unity MCP 或受控生成器创建，而非手写 YAML；
9. artifact 存储、脱敏、hash 和受版本控制 receipt 位置已冻结。

若 P7 candidate 在 P8 期间发生任何 package 内容、version、metadata 或 changelog 变化，应生成新的 candidate commit，并按触发规则重跑相应 E0/E2/P8 gates；不得沿用旧 tree 的 receipt。

## 5. 明确交付物

### 5.1 Metadata 与 legal 类型

- **Package manifest metadata**：经 owner 批准的 license 表达、documentation/changelog URL（如使用）、category、keywords、samples declaration 和 canonical repository identity；
- **Package legal files**：`LICENSE.md`、`Third Party Notices.md`，包含来源/版本可追溯的第三方与 sample 资产审计；
- **Package documentation**：`Documentation~` 或批准 URL，安装、Local/PinnedGit/TaggedGit、optional tier、Player/平台范围和 migration 指南；
- **Changelog entry**：目标 preview 版本的实际变化、breaking migration 与 known limitations，不只停留在 `[Unreleased]`；
- **Public API compatibility policy**：承诺面、preview/stable deprecation 和 breaking version 规则；
- **Migration notes/fixtures manifest**：package path/API/schema 迁移步骤及 fixture provenance。

### 5.2 Runner 类型

- **TaggedGit source runner**：独立 source mode，接收 tag 而非伪装成 SHA；记录 tag object、peeled commit、lock resolved commit 和 PackageCache observed version；
- **Player build+run runner**：构建最小 Consumer Player、启动、等待 deterministic completion receipt、检查 runtime log 并退出；build-only 与 run 分开判定；
- **Optional Player runner**：只为 preview 声明 O3/Player support 的 adapter 执行 matching build+run；O2 不得被文案暗示为 Player 已验证；
- **N-1 upgrade runner**：执行 Fresh N-1、Fresh N、N-1 → N 三组，保留协议规定的 Consumer assets/state，验证 dependency drift、API sentinel 和适用 migration fixtures；
- **Release verifier**：交叉校验 package version、tag、tag target、resolved SHA、package tree hash、observed version、matrix/Player/upgrade/legal hashes；
- **Receipt generator/validator**：生成稳定、脱敏、可离线复核的 E3 receipt，并拒绝缺失或 failed gate。

### 5.3 Report、receipt 与 metadata 类型

- **TaggedGit profile/matrix report**：tag object/target、requested tag、resolved commit、observed version、candidate tree 和完整 Consumer evidence；
- **Player report**：platform/backend、build/run requested/passed 三态、completion receipt hash、runtime log hash、package identity 和 optional profile；
- **Upgrade report**：N-1/N identities、fixture hashes、cache retention protocol、API sentinel、Config/SaveState/serialized asset/其他适用域结果和 dependency diff；
- **Legal audit report**：owner decision references、第三方来源/version、sample provenance、manifest/legal/documentation completeness；
- **Release receipt schema**：package name/version/candidate/tag/tag target/tree hash、Unity/platform、P7 consumer matrix、Player matrix、upgrade matrix、legal audit 和 compatibility policy hash；
- **`reports.sha256` 与 matrix Markdown**：只做稳定摘要与 hash 索引，不复制原始巨型 Unity artifact 或敏感字段；
- **受版本控制 E3 receipt**：存放在 Current/Policy 指定的 `Docs/Reports/UPM/<version>/<candidate>/` 结构或启动时批准的等价位置。

## 6. 实施切片及顺序

1. **冻结 owner decisions**：形成 license、P6 package ID 确认、repository URL、version/tag、platform、optional tier、N-1 和 compatibility 的决策清单；identity 变更转出本阶段，其余未决项显式阻断相关切片。
2. **完成 metadata/legal**：补齐 manifest、Documentation、LICENSE、Third Party Notices、samples declaration 和 changelog；执行 E0 strict release metadata 检查。
3. **建立 compatibility 与 fixtures**：冻结 public API sentinel、N-1 identity、Config/SaveState/serialized asset 等适用 migration fixtures和缓存保留协议。
4. **实现 Player build+run**：使用最小 package-owned/Consumer fixture，写 deterministic completion receipt；先 base，再按已声明 O3 tier 增加 matching optional Player。
5. **实现 N-1 upgrade matrix**：依次运行 Fresh N-1、真实 Upgrade N-1 → N、Fresh N 控制组；区分 package、API、schema、SaveState、network/wire 和 serialized asset 域。
6. **冻结最终 candidate commit**：metadata、legal、documentation、fixtures、version 和 changelog 全部进入同一干净、远端可达完整 SHA；重新执行受影响 E0 与 P7 Local/PinnedGit matrix。
7. **生成 pre-tag receipt candidate**：聚合 consumer、Player、upgrade、legal、compatibility hashes，保持 tag 状态为未验证。
8. **创建 annotated preview tag**：tag 指向已验证 candidate，不删除、不移动；错误候选以新版本修正。
9. **执行 TaggedGit smoke**：直接用 tag URL 安装，验证 tag object/peeled commit、lock resolved SHA、PackageCache observed version 和 package tree。
10. **生成并复核 E3 receipt**：release verifier 证明 version/tag/SHA/tree/report 全一致；人工复核声明、known limitations、optional tier 和敏感信息。
11. **更新 Current 与阶段状态**：Current 只引用 E3 receipt；阶段台账在证据独立复核后更新，不复制易变化运行数字到本规格。
12. **Owner 发布决策**：owner 明确批准或拒绝 reviewed preview publication；stable/RC 是否启动作为新的决策，不由 P8 自动推进。

## 7. 范围与非目标

### 范围

- Owner 决策的冻结与可追溯记录；
- package metadata、legal、Documentation、samples declaration 和 changelog readiness；
- TaggedGit source mode；
- base Player build + run，以及声明 O3 的 matching optional Player；
- 上一 approved preview/公开 train到候选的 N-1 upgrade；
- public API compatibility policy、API sentinel 与适用 migration fixtures；
- 同一 candidate 的 E3 release receipt 和 owner-reviewed preview 决策。

### 非目标

- 不重新设计或迁移 package surface，不重开 P1-P6；
- 不在 P8 内改变 P6 已冻结的 package ID；变更必须退出本阶段，由新 ADR 和新的 cutover 阶段处理；
- 不用 metadata-only commit 沿用旧 candidate matrix；
- 不为赶发布降低 P7 required matrix、跳过 Player run 或用 build-only 代替 run；
- 不由 Agent 选择许可证、package ID、canonical URL 或支持承诺；
- 不把 O2 optional behavior 宣称为 O3 Player support；
- 不把 preview 完成解释为 RC、stable 或 `1.0.0` 已批准；
- 不删除/移动失败 tag，不覆盖历史 receipt；
- 不把主工程、移动 `main`、dirty Local 或复制源码结果当作 release evidence。

## 8. 关键不变量

1. package.json version、tag 去除 `v`、receipt version 和 Consumer observed version 必须一致。
2. tag target commit、Consumer resolved commit、receipt candidate commit 和 P7 candidate 必须一致。
3. package tree hash 必须覆盖 metadata/legal/documentation 的最终内容；任何变化都使旧 matrix/receipt 失配。
4. TaggedGit 必须直接验证 tag source，拒绝 branch、短 SHA、移动 tag 或先手工换算为 SHA 的伪 tag smoke。
5. Player `buildPassed` 与 `runPassed` 分开记录；未请求为 `null`，build 通过不能替代 runtime completion receipt。
6. N-1 必须来自上一条已晋升 baseline/公开 train，不能复制当前源码后改版本号伪造。
7. Upgrade 组必须保留协议规定的 Assets/state；不能通过任意清空 Library 或状态来掩盖真实升级失败。
8. optional Player 行只由声明 tier 触发；FMOD 与 Magica Cloth 在 owner 未批准前维持 O0，不进入 preview Player 支持声明。
9. legal、repository、package identity 和 compatibility policy 都必须有 owner authority。
10. E3 receipt 只引用 schema-valid、hash-valid、全部 required gate 通过的同一 candidate artifact。
11. 原始日志可以外置，但 receipt 必须能以 hash 反查；仓库摘要不得包含凭据、用户名、绝对路径、license token/serial 或漂移时间字段。
12. Reviewed preview 只证明声明范围内的 preview readiness；stable 需要另行满足 stable gate 与 owner 决策。

## 9. 逐层验证

### 静态与 metadata gate

- E0 使用 strict surface 与 release metadata policy；
- 校验 manifest JSON/SemVer、name/unity/dependencies、category/keywords/license/samples、Documentation、README、CHANGELOG、LICENSE 和 Third Party Notices；
- 校验 package 不含缓存、个人设置、绝对路径或未授权制品；
- legal audit 与 owner decisions 可追溯。

### P7 Consumer gate

- 对最终 candidate 重跑或验证触发范围内的同一 SHA Local/PinnedGit required matrix；
- 确认 clean Library、expected full names、optional provenance、forbidden logs、mutation guard 和 artifact hash；
- P7 E3 candidate 只能作为输入，P8 最终 receipt 必须重新验证交叉引用。

### Player gate

- base Windows（或 owner 批准的首个权威平台）执行 build + actual run；
- Player 启动 RuntimeHost/probe 生命周期，生成 deterministic completion receipt 并正常退出；
- build/runtime log 无 error、exception、`LogError`，receipt identity 与 candidate/version 一致；
- 对声明 O3 的 optional adapter运行 matching Player；未验证平台/adapter 明确 `not-yet-validated`。

### N-1 upgrade gate

- Fresh N-1 建立 lock、API sentinel 与 migration fixture基线；
- Upgrade N-1 → N 只改变 MxFramework reference及政策明确的 optional version，记录 dependency diff；
- 验证 API compile、Config/SaveState/serialized asset及其他适用域精确成功或结构化 fail-closed；
- Fresh N 控制组区分 candidate 本身问题与升级残留；
- schema 跨多个受支持版本时按 policy 增加 oldest-supported 行。

### TaggedGit 与 E3 gate

- annotated tag object、peeled target 和运行前后 tag identity 一致；
- Consumer 直接从 tag URL 解析，lock full SHA、PackageCache observed version、tree hash 与 receipt 一致；
- release verifier 离线校验 consumer/player/upgrade/legal report hashes；
- E3 receipt 稳定排序、UTF-8 无 BOM、确定性重放一致且受版本控制。

### Independent review

- reviewer 不依赖执行者口头结论，能从 receipt 反查所有 required artifact hash；
- reviewer核对 owner decisions、support tier、known limitations、migration note 和 preview wording；
- 只有 reviewer 与 owner 均接受，才更新可消费 preview 声明。

## 10. 失败与回滚

- metadata/legal 未决或 audit 失败：阻断对外 preview；可继续 private development，但必须明确 private-only。
- candidate 在 P8 期间变化：创建新 commit并重跑受影响 E0/E2/P8 gates，不修改旧 report identity。
- owner 要求改变 package ID：把 P8 标记 `Blocked`，保留现有候选证据为历史输入；创建替代 ADR 和新的 cutover 阶段，不能在 P8 内直接改目录、locator 或 Consumer path。
- Player build 通过但 run 失败：candidate 未通过；不得标为环境问题后跳过。若平台不再支持，必须由 owner 更新支持矩阵并留下决策记录。
- N-1 upgrade 失败：按 package/API/Config/SaveState/network/serialized asset域定位；不得清空状态来制造 fresh install 假象。
- TaggedGit 失败或 tag/version 不一致：不移动、不删除 tag；修复后提升 preview 序号并创建新 tag。
- preview tag 已发布后发现缺陷：发布新的 preview/patch identity，不重写历史 tag 或 passed receipt。
- report/hash/receipt mismatch：E3 fail-closed，保留失败 artifact，修复生成器或重跑 gate。
- optional Player 失败：如果该 tier 已对外承诺则阻断；降 tier 需要 owner 决策、文档与 receipt 同步，不能由 runner 自动降级。
- 回滚仅针对本阶段 metadata/runner/fixture/receipt 切片；不得删除历史 E2/E3 evidence、破坏 package GUID/asmdef identity 或清理用户工作区。

## 11. 退出证据

UPM-P8 只有在以下证据全部满足时才能标记 `Complete`：

- 所有 owner 决策有可追溯批准，package ID/repository/license/support/compatibility 无占位或猜测；
- strict static readiness gaps 清零，metadata/legal/documentation/sample/changelog 与目标 preview version 一致；
- 最终干净远端 SHA 的 P7 required Consumer matrix 全部通过；
- base Player build + run completion receipt通过，声明 O3 的 optional Player 行完整；
- N-1 identity 合法，Fresh N-1、Upgrade、Fresh N 及适用 migration/API assertions 通过；
- TaggedGit 直接解析通过，tag object/target、resolved SHA、observed version 和 tree hash 一致；
- E3 receipt 受版本控制、schema/hash-valid、稳定脱敏并能反查完整报告；
- independent reviewer确认所有行属于同一 candidate，未拼接历史、dirty Local、build-only 或不同 SHA 证据；
- Current ledger 和阶段台账按 receipt 更新；
- owner 对新的 reviewed preview 作出显式发布决定，并明确该决定不等于 stable。

## 12. 移交与后续决策

P8 完成后向 owner/release operator 移交：

- reviewed preview 的 version、annotated tag、candidate/target SHA 和 package tree hash；
- 受版本控制 E3 receipt 与 consumer/player/upgrade/legal report hash 索引；
- 安装、升级、migration、supported platform 和 optional tier 文档；
- known limitations、未验证平台/adapter 和 FMOD/Magica Cloth O0 状态；
- tag 不可移动、失败版本以新版本修正的操作规则。

后续分叉必须由 owner 明确选择：继续收集该 preview 的外部消费反馈、启动新的 preview patch，或另立 RC/stable readiness 任务。P8 `Complete` 只说明本次 reviewed preview 在声明范围内可晋升，不构成 stable API、长期支持平台、公开许可范围或 `1.0.0` 的自动承诺。
