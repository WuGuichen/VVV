# UPM-P7 Consumer Evidence and Optional Matrix

> Status: Current
>
> Task State: Complete
>
> Phase: UPM-P7 / R5-R6
>
> Depends On: UPM-P6 Embedded Package Cutover (`Complete`)
>
> Hands Off To: UPM-P8 Preview Release Readiness

本文是 UPM-P7 的实施任务规格与完成判据归档。它保留 Consumer 证据与 optional matrix 的可执行切片，但活动 package identity、测试名单、版本、哈希和最近证据只读取 [`UPM_CURRENT_STATUS.md`](../../UPM_CURRENT_STATUS.md)，不在本文复制。

P7 已完成：profile report schema 4、matrix/receipt schema 1、独立 validator、artifact/mutation/log policy、optional O2/O0 机器权威与固定八行 runner 均已落地。审计修复候选已关闭 project-only friend、Editor 项目路径、VFX 自动发现日志与 Magica Cloth O0 authority 缺口；活动八行 matrix 和可复核 evidence bundle 统一读取 Current 与 `UPM/Evidence/UPM-P7/`。E3 receipt candidate 保持 `releaseReady=false`，现移交 P8 owner 决策与发布门禁。

## 1. 阶段身份与目标结果

UPM-P7 在 Embedded Package canonical path 已完成后，统一 Consumer runner 的候选身份、测试、Player 三态、optional profile 和可晋升判定。阶段结果应让一个完整 candidate 在 Local 与 PinnedGit 两种来源下产生不可误读、可聚合、可校验的 E0/E1/E2 证据，并能生成供 P8 审核的 E3 receipt candidate。

目标状态：

- 每一行报告都区分 candidate、harness、fixture 和 source identity；
- Local 与 PinnedGit 的 base、FairyGUI、Unity Transport、VFX Graph 行属于同一个 candidate；
- Player build/run 使用“请求且通过 / 请求但失败 / 未请求”的三态语义，未执行字段为 `null`；
- expected test full names、optional source/version/resolved revision、artifact hash 和 forbidden-log policy 完整；
- profile row eligibility 与 matrix-level `promotable` 分离；只有完整 matrix 的 `promotable` 能由 schema、clean state、candidate 一致性和 required rows 自动计算，调用者不能强制覆盖；
- package 与 Consumer mutation guard 证明验证过程没有修改候选或 fixture 权威内容；
- FMOD 与 Magica Cloth 维持 O0，除非 owner 已批准各自来源、许可、分发边界和平台责任，并另立实施切片。

## 2. 权威输入

实施者必须在启动切片前重新读取：

- [`UPM_PHASE_TASKS.md`](../../UPM_PHASE_TASKS.md)：阶段状态、依赖、范围和退出条件；
- [`UPM_CURRENT_STATUS.md`](../../UPM_CURRENT_STATUS.md)：唯一活动数字、candidate、测试名单和 evidence ledger；
- [`UPM_OPTIMIZATION_ROADMAP.md`](../../UPM_OPTIMIZATION_ROADMAP.md)：R5-R6 顺序与迁移验证要求；
- [`UPM_CONSUMER_BASELINE_EVIDENCE_OPTIMIZATION.md`](../../UPM_CONSUMER_BASELINE_EVIDENCE_OPTIMIZATION.md)：E0-E3、schema、runner 和晋升政策；
- [`UPM_OPTIONAL_INTEGRATION_OPTIMIZATION.md`](../../UPM_OPTIONAL_INTEGRATION_OPTIMIZATION.md)：optional source、gate、support tier 与 FMOD/Magica Cloth 边界；
- [`UPM_RELEASE_UPGRADE_READINESS_PLAN.md`](../../UPM_RELEASE_UPGRADE_READINESS_PLAN.md)：P8 所需 Player、tag、upgrade 与 receipt 接口；
- [`ADR-015`](../../Decisions/ADR-015-upm-embedded-package-topology.md)：已接受的 Embedded Package canonical topology。

## 3. 进入条件

只有以下条件全部满足，阶段才能从 `Pending` 进入 `Ready` 或 `In Progress`：

1. UPM-P6 已标记 `Complete`，开发工程与 Consumer 都使用 canonical embedded package path；
2. 旧开发路径 fallback 已按 P6 退出条件处理，P7 不再为双 package root 设计新兼容语义；
3. candidate 是已推送、远端可达的完整 SHA；候选 checkout 可创建干净 detached worktree；
4. 当前 package version、optional support authority、expected test full names 和 fixture owner 可从机器事实或 Current 入口确定；
5. runner/report 当前行为已有保留样例，能够用于 schema migration 回归；
6. 工作区中用户未完成改动与候选运行目录隔离，且不会被 baseline 刷新流程 stash、丢弃或提交；
7. 任何需要把 FMOD 或 Magica Cloth 提升到 O1/O2 的提案已取得 owner 的书面决策；否则 P7 matrix 明确排除对应 positive profile。

进入条件不满足时，只允许只读调查与设计校对，不得改变现有证据权威。

## 4. 明确交付物

文件名可在启动时按现有工具布局校正，但职责和数据契约不得合并丢失。

### 4.1 Runner 类型

- **Profile runner**：兼容入口 `run_upm_consumer_smoke.ps1`，负责准备单个 Consumer、运行 E0/Unity/tests/可选 Player gate，并写一份 profile report；
- **Matrix runner**：目标入口 `run_upm_consumer_matrix.ps1`，负责执行 required Local/PinnedGit × base/optional 行、校验 candidate identity、聚合结果和生成 matrix candidate；
- **Schema validator**：独立校验 profile report、matrix report 和 receipt candidate 的结构、枚举、三态及交叉引用；
- **Mutation guard**：在每行前后记录 package candidate tree、harness tracked state、Consumer fixture tracked state，并对未授权变化 fail-closed；
- **Artifact hasher**：对主 report、NUnit XML、Unity log 摘要、Player build/run report 等被引用 artifact 生成稳定 SHA-256 清单。

### 4.2 Report 与 schema 类型

- **Profile report schema v4 或后继评审版本**，最低对象：
  - `candidate`：source mode、requested/resolved commit、package tree hash、package name/version；
  - `harness`：repository commit、dirty state、fixture hash、runner/schema version；
  - `execution`：profile、Unity/platform、clean Library、evidence level、matrix eligibility 和失败分类；
  - `tests`：完整 `expectedFullNames`、observed full names、passed/failed/skipped；
  - `optionalIntegrations`：package source、requested/validated version、resolved revision、define、adapter assembly/DLL 和 support tier；
  - `player`：`buildRequested`/`buildPassed` 与 `runRequested`/`runPassed`，未请求结果必须为 `null`；
  - `artifacts`：稳定相对标识、类型、hash 和外部存储引用；
  - `mutationGuard`：前后 hash、允许变化白名单和 verdict；
  - `logPolicy`：forbidden pattern policy/version 与命中结果。
- **Matrix report**：required row manifest、每行 profile report hash、candidate equality verdict、缺失/失败/不适用原因、promotable verdict；
- **E3 receipt candidate**：稳定排序、UTF-8 无 BOM、脱敏，只引用通过的 E2 report hash；P7 生成的是待 P8 审核输入，不得自行宣布发布；
- **Failure report**：用结构化分类区分 product failure、schema failure、identity mismatch、mutation、network、credential、Unity license 和 infrastructure unavailable；“未得到证据”不能改写为通过。

### 4.3 Matrix 类型

默认 required matrix 为同一 candidate 的：

| Source | Profiles | 证据角色 |
| --- | --- | --- |
| Local | `base` + FairyGUI + Unity Transport + VFX Graph matching positive profiles | E1、单行永远 `promotable=false`；干净 matching candidate checkout 可作为 E2 matrix 的 required diagnostic input |
| PinnedGit | `base` + 同三项 matching positive profiles | 完整 SHA、lock resolved commit 和 immutable package boundary |

具体 profile 名、expected tests 和活动版本从 Current 与机器权威读取。FMOD 与 Magica Cloth 不在 required positive matrix；保持 O0 base-absence/closure 断言，除非 owner 批准后另行扩大矩阵。

## 5. 实施切片及顺序

一次只推进一个可审计切片，前一切片的 schema/测试通过后再进入下一切片。

1. **冻结旧报告与契约**：采集当前 schema 样例、runner 参数、expected tests、artifact 输出和失败语义；建立迁移 golden fixtures，不把历史结果当作新 candidate 证据。
2. **定义 profile schema**：实现 candidate/harness/fixture 分离、optional provenance、test full names、Player 三态和失败分类；为合法、缺字段、非法三态、脏 Local、revision mismatch 建 schema tests。
3. **收敛单 profile runner**：主 JSON 内嵌 Player build/run 状态并以 hash 关联详细 artifact；保留兼容入口，但禁止继续由独立 Player 文件决定主 verdict。
4. **实现 mutation guard**：覆盖 package candidate、Consumer fixture 与受控 harness；明确允许生成目录，任何 tracked/source 权威变化阻断该行。
5. **实现 matrix runner**：声明 required rows，按同一 candidate 执行 Local/PinnedGit × base/optional，校验 source、version、resolved SHA、tree hash 和 fixture identity。
6. **实现 row eligibility 与 matrix promotable 计算**：profile 只计算是否可作为 required input；完整 matrix 再从 clean state、source mode、完整行、schema、tests、logs、artifact hash 和 mutation verdict 推导 `promotable`，拒绝 CLI 强制设为 true。
7. **验证 optional matrix**：base 必须 absence-safe；三个已支持 optional profile分别验证 positive source/version、adapter DLL 和最小行为；FMOD 与 Magica Cloth 仅验证 O0 closure。
8. **生成 E2 matrix 与 E3 receipt candidate**：只引用同一候选的通过报告，稳定排序并脱敏；执行确定性重放与 hash 校验。
9. **更新 Current/Policy 接口**：阶段确有证据后才更新 Current ledger、阶段状态和必要政策链接；不把一次性原始日志写入 Current。

## 6. 范围与非目标

### 范围

- Consumer profile/matrix runner 的职责拆分与兼容入口；
- report schema、schema validator、artifact hash 和失败分类；
- E0/E1/E2 编排与面向 E3 的 receipt candidate；
- Local/PinnedGit base 与 matching optional matrix；
- package/Consumer mutation guard；
- optional provenance、support tier、profile row eligibility 与 matrix-level promotable 的机器判定。

### 非目标

- 不补齐 LICENSE、Third Party Notices、Documentation 或 manifest release metadata；
- 不创建或验证 release tag，不宣告 preview/stable；
- 不实现 N-1 upgrade、migration fixture 或完整 Player run release gate；
- 不把不同 candidate、脏 Local、旧 E0 或独立 Player artifact 拼成可晋升 receipt；
- 不自动提升 optional integration support tier；
- 不在未获 owner 批准时新增 `with-fmod` 或 `with-magica-cloth`，也不判断两者的分发许可；
- 不重新移动 package root、拆 adapter package 或改变 public API。

## 7. 关键不变量

1. `promotable` 只属于完整 E2 matrix/E3 receipt candidate；Local profile report 永远是 E1 且 `promotable=false`。matrix 内全部 required reports 必须指向同一完整 candidate SHA、package version 和 package tree hash。
2. PinnedGit 的 requested commit 必须等于 lock/resolved commit；branch、短 SHA 或不可达 revision fail-closed。
3. Local 行只有在 candidate 的干净 checkout 上运行且 source tree hash 与候选一致时，才能作为 E2 matrix 的 matching diagnostic input；它仍是 E1、`promotable=false`。脏 Local 只能作为不匹配诊断输入，不能满足 required matching row。
4. `buildPassed`/`runPassed` 只在对应 `Requested=true` 时取布尔值；未执行必须是 `null`。
5. expected test full names 必须逐项记录并完全执行；Unity 退出码为 0 不足以替代测试断言。
6. base 不解析 optional package；positive profile 同时证明来源、版本、resolved identity、adapter assembly 和行为。
7. optional tier 只由机器权威与已有证据声明；主工程功能通过不能提升 Consumer tier。
8. FMOD 与 Magica Cloth 保持 O0，直到 owner 批准并完成独立实施切片；不得由 runner 自动发现后纳入支持声明。
9. mutation guard 失败即整行失败；验证工具不得修改 candidate、fixture 或正式 package 内容。
10. report/receipt 不含绝对路径、用户名、token、license key、Unity serial 或其他敏感/易漂移字段。
11. E3 是显式晋升，不是普通 runner 的副作用；P7 只能准备候选证据。

## 8. 逐层验证

### E0 Static Boundary

- 对 resolved package root 运行单一 E0 checker；
- 严格检查 surface manifest、package hygiene、asmdef/initializer 分类、mandatory dependency 和 optional reverse closure；
- 输出确定性 JSON 与 package tree hash；
- E0 通过只证明静态边界，不产生 Consumer/Player 声明。

### E1 Local Diagnostic

- 从隔离 Consumer 和空 `Library` 运行 Local base；受影响 adapter 加 matching positive profile；
- 允许诊断脏工作树，但必须记录 dirty 且 `promotable=false`；
- 检查 compile、expected full names、optional absence/presence、forbidden logs 和 mutation guard；
- 所有 artifact 与主 report 建 hash 关联。

### E2 Immutable Candidate Matrix

- fetch 后确认完整 SHA 远端可达；在干净 detached candidate checkout 运行 E0 与 Local 行；
- 对相同 SHA 运行 PinnedGit base 与 matching optional rows；
- Consumer 每行使用空 `Library`，从 PackageCache/lock 读取 resolved identity；
- 聚合器验证所有 required rows 的 candidate/package tree/version 一致、schema valid、tests complete、logs clean、mutation clean；
- 两次对相同输入生成的稳定摘要和 hash 应一致。

### E3 Receipt Candidate

- 仅引用完整通过的 E2 profile/matrix report hash；
- 记录 candidate、package tree、version、Unity、harness、fixture 和 required matrix；
- schema validator 可离线复核所有交叉引用；
- 标记为待 P8 legal/tag/Player/upgrade 审核，不得在 P7 宣布 promoted release。

## 9. 失败与回滚

- schema/runner 迁移失败：保留旧兼容入口用于诊断，修复后以新 commit 重跑；不得把旧 schema 与新行混合晋升。
- candidate/revision/tree mismatch：整份 matrix fail-closed，选择或生成新的完整 candidate，不改写 report identity。
- dirty Local、mutation 或 artifact hash mismatch：降为不可晋升诊断或失败；恢复隔离环境后从该行重新执行。
- network、credential、Unity license、LFS 或远端不可用：记录 `evidence-unavailable`，不降级为通过。
- optional positive 失败：base 结果可保留为诊断，但 required matrix 不完整；不得移除 required row 掩盖失败。
- FMOD 或 Magica Cloth owner 决策缺失：维持 O0，不视为 P7 blocker；若有人擅自扩大 support claim，回滚该 profile/metadata 变更。
- 回滚只撤销本阶段 runner/schema/report 改动；不得移动 tag、删除历史报告或清理用户工作区。失败候选报告保留外部 artifact 以供审计。

## 10. 退出证据

UPM-P7 只有在以下证据全部存在且独立复核后，才能从 `Validated` 进入 `Complete`：

- profile report、matrix report 和 E3 receipt candidate schema 及其正/负测试；
- 同一完整 SHA 的 schema-valid E0、non-promotable E1 Local inputs 和 immutable PinnedGit rows；只有聚合后的完整 E2 matrix 可为 `promotable=true`；
- Local/PinnedGit 的 base + 三项 matching optional 行身份一致且全部 required tests 执行；
- Player 三态样例证明未请求为 `null`，失败与通过不混淆；
- optional provenance、resolved revision、adapter DLL、artifact hash 和 log policy 可机器复核；
- package/Consumer mutation guard 的通过证据及故意变更的 fail-closed 测试；
- dirty Local、revision mismatch、缺测试、缺 artifact、schema mismatch 的负向测试均不能得到 promotable；
- FMOD 与 Magica Cloth receipt 明确为 O0，或附有 owner 决策和各自独立 positive profile 证据；
- Current ledger 与阶段台账按真实结果更新，不复制活动数字到本文。

## 11. 移交 UPM-P8

P7 向 P8 移交：

- 已冻结并验证的 report/matrix/receipt schema；
- 一个远端可达、干净、完整 SHA 的 E2 matrix 及全部 report hash；
- candidate package name/version/tree hash、harness SHA、fixture hash 和 required row manifest；
- Player build/run 三态与尚未执行的 P8 gates 清单；
- optional support tier 与 FMOD/Magica Cloth O0 决策状态；
- mutation guard、失败分类和 artifact retention 规则。

移交不包含 license、canonical repository URL、tag 或 stable 决策。P8 必须重新确认 candidate 未变化；任一 package tree、version、required matrix 或 owner 决策变化都使旧移交只能作为历史输入，必须重跑受影响证据。
