# UPM Consumer 基线与证据优化方案

> Status: Policy
>
> Revised: 2026-08-23
>
> 本文只定义 Consumer 证据的新鲜度、runner 分层、报告语义和 baseline 晋升流程。活动 HEAD、测试计数和 evidence ledger 统一读取 `UPM_CURRENT_STATUS.md`；包目录迁移见 `UPM_PACKAGE_SURFACE_LAYOUT_OPTIMIZATION.md`，版本/tag/升级发布见 `UPM_RELEASE_UPGRADE_READINESS_PLAN.md`。

## 1. 当前 runner 与历史问题

现有 `run_upm_consumer_smoke.ps1` 已能证明：

- local `file:` 与 pinned Git subpath 能解析；
- pinned requested/resolved revision 完全一致；
- base profile 不解析 optional packages；
- optional reverse-reference closure 全部具备预期 gate；
- Unity 编译、Editor log 与 focused tests 通过；
- positive profile 的 optional package、adapter DLL 和最小行为存在。
- base 可选择追加 build-only Player gate，但尚不启动 Player。

活动 runner 的精确测试名单、数量和最近执行结论只在 `UPM_CURRENT_STATUS.md` 与机器报告中维护。

缺口不是 runner 没有价值，而是证据与候选提交之间没有自动的新鲜度关系。以下是 2026-08-01 的历史审计快照，不是 Current：

```text
reviewed package baseline = ff984c808e3b35f41420d1f52e16490f4d5a6c4b
current HEAD              = dbcd3ccad1ecdc06dac0b67d2a9bc9e4bb5b89c5
distance                  = 9 commits
committed package diff    = 420 files, +55,067/-1,523
dirty package paths       = 22
```

schema 3 report 中：

- `package.requestedRevision` / `resolvedRevision` 表示 pinned candidate；
- `repositoryCommit` 表示运行 harness 的本地工作区 HEAD；
- `repositoryDirty` 表示 harness 工作区是否脏。
- Player build 结果仍位于独立 `player-build-report.json` 和 Markdown，没有进入主 `report.json`。

这三个概念都有价值，但字段名容易让人误把 harness commit 当作 candidate commit。Local 模式可以在脏工作区用于诊断，却不应产生“可晋升 baseline”的结论。

## 2. 目标

建立三层证据：

```text
Static Boundary Evidence
  -> Local Consumer Diagnostic
    -> Immutable Candidate Matrix
      -> Promoted Baseline / Release Receipt
```

每层都明确回答：

1. 验证了哪个 package tree；
2. 使用了哪个 harness/fixture；
3. 从什么来源解析；
4. 跑了哪些 profile；
5. 结果是否允许晋升；
6. 与当前 `main` 的距离是多少。

## 3. 证据等级

### E0：Static Boundary Evidence

适用于所有 package-root 改动，必须快速、跨平台、不启动 Unity。

最低检查：

- `package.json` JSON 与 SemVer；
- mandatory dependency 精确集合；
- 所有 asmdef JSON 可解析且 assembly name 唯一；
- FMOD/FairyGUI/Transport/VFX Graph reverse closure；
- `defineConstraints` / `versionDefines` 完整性；
- uGUI 等非 mandatory package 的直接源码引用是否只出现在受控 optional assembly closure；
- package root 禁止路径和意外生成文件；
- package README/CHANGELOG/LICENSE/manifest metadata 状态；
- package surface 清单是否出现未分类 asmdef；
- 新增或修改的 Editor auto-load 入口是否登记 portable/optional/project-only 分类。

已建立跨平台入口：

```text
Tools/Harness/check_upm_package_boundary.py
```

该脚本已接管 package manifest、mandatory dependency 精确集合、全部 asmdef、四类 optional reverse closure、非 mandatory uGUI 源码引用、package tree hygiene 和确定性 JSON 输出。PowerShell Consumer runner 调用同一份结构化实现，并对 PinnedGit 实际解析到 `Library/PackageCache` 的 package 运行，不再维护第二套闭包规则。脚本只读 package root，不创建 Consumer 项目；显式 `--report-json` 必须指向 package root 外。

R0 的默认 preview policy 对尚未实施的 surface manifest 与 release metadata 记录 readiness gap，但不把它们误报为当前边界违规。L0 建立 `upm-surface.json` 后使用 `--require-surface-manifest`；release candidate 使用 `--require-release-metadata`。这两个严格开关让后续路线收紧门禁，而不让 R1/R3 反向阻塞“先刷新当前 preview 证据”的 R0 顺序。

E0 只能证明静态边界，不能声明 UPM import 通过。

### E1：Local Consumer Diagnostic

用于开发中快速验证当前 working tree：

- 允许 `repositoryDirty=true`；
- source 必须为 `local`；
- 默认 clean Consumer `Library`；
- 至少运行 `base`；
- adapter 相关改动加 matching positive profile；
- 报告明确 `promotable=false`。

Local 模式证明“当前磁盘内容在这台机器上能被另一个项目消费”，不证明远端可重现，也不能成为 Current immutable baseline。

#### 2026-08-01 R0 实测结果

本轮把 E0、失败的固定候选和修复后的 E1 明确分开：

| 证据 | Package identity | 结果 | 可晋级 |
| --- | --- | --- | --- |
| E0 policy v1 clean Git tree | `343b1d084747879df8c0ebfadf59e7cc79800726` / tree `c12ce447...` | 15 dependencies、130 asmdef、四类 optional closure 0 违规、8 readiness gap；尚未检查 uGUI 源码引用 | 否，仅静态且发生漏检 |
| PinnedGit `base` | 同一 `343b1d...` | Git/lock 解析成功，Unity 编译因 locator 静态依赖 `UnityEngine.EventSystems` 失败 | 否，候选拒绝 |
| E0 policy v2 回放 | 同一 `343b1d...` | `optional-source-ugui`，1 blocking failure；在 Unity 前复现同一边界错误 | 否，候选拒绝 |
| E0 policy v2 current tree | 脏工作树 / tree `4e062130...` | 2 个直接 uGUI 引用均在 FairyGUI reverse closure，0 违规；8 readiness gap | 否，仅静态 |
| Local `base` | 脏工作树 / tree `4e062130...` | compile 通过，测试 `1/1`，optional packages absent | 否，E1 |
| Local `with-fairygui` | 同一脏工作树 | compile 通过，测试 `2/2` | 否，E1 |
| Local `with-unity-transport` | 同一脏工作树 | compile 通过，测试 `2/2` | 否，E1 |
| Local `with-vfx-graph` | 同一脏工作树 | compile 通过，测试 `2/2` | 否，E1 |

locator 修复已经由主工程 Unity 编译和 focused EditMode `3/3` 补充验证。四份 Local report 都已用 E0 policy v2 重跑，并记录 `repositoryDirty=true`，不能与旧 SHA 的 E0 合并成 E2。下一次可晋级运行需要先把精确修复形成新的远端完整 SHA，再从 clean E0 和 PinnedGit base 重新开始。

本轮还验证了 runner 可在 macOS 上通过显式 `-UnityPath`、`-ConsumerRoot` 和 portable PowerShell 执行。PowerShell 解析 `python3` 时必须确定性选择单个 executable；当 `package.json.repository.url` 在当前网络不可达时可以显式传 `-GitUrl` 做诊断，但 release metadata 仍需冻结一个长期 canonical URL，不能让机器私有覆盖值成为无记录权威。

### E2：Immutable Candidate Matrix

用于新的 preview baseline、外部项目升级候选和 release candidate：

- candidate 必须是远端可达的完整 40 位 SHA；
- source 必须为 `git`；
- requested SHA 与 lock hash 完全一致；
- Consumer 必须从空 `Library` 创建；
- harness 工作区允许位于另一提交，但必须记录 harness/fixture identity；
- required matrix 全部通过；
- candidate package version 与计划 train 一致；
- E2 matrix report 明确 `promotable=true`，但尚未自动改 Current 文档；其中的 Local profile reports 仍属于 E1，必须保持 `promotable=false`。

当前无 FMOD 支持声明时的 required matrix：

| Source | Profile | Tests | 用途 |
| --- | --- | ---: | --- |
| Local | base | 1 | 诊断候选 checkout 与 source fixture |
| Local | with-fairygui | 2 | local optional source |
| Local | with-unity-transport | 2 | registry optional source |
| Local | with-vfx-graph | 2 | registry optional source |
| PinnedGit | base | 1 | immutable base boundary |
| PinnedGit | with-fairygui | 2 | 同 SHA 双 subpath |
| PinnedGit | with-unity-transport | 2 | immutable framework + registry adapter |
| PinnedGit | with-vfx-graph | 2 | immutable framework + registry adapter |

Local profile 无论干净与否都只算 E1，单行不得晋升。若 Local 使用 candidate 的干净 checkout，它可以作为 E2 matrix 的 required matching diagnostic input；如果 checkout 不是 candidate SHA，E2 matrix 必须如实标记该行不匹配且不可晋升。最简单、最清楚的流程是在候选的干净 detached worktree 中运行 non-promotable Local 行，再对相同 SHA 运行 immutable PinnedGit 行；只有完整聚合的 E2 matrix 可以 `promotable=true`。

FMOD 获得支持后，增加 local/pinned `with-fmod`，但是否要求它进入所有 preview matrix，由 `UPM_OPTIONAL_INTEGRATION_OPTIMIZATION.md` 的 support tier 决定。

### E3：Promoted Baseline / Release Receipt

E3 不是再次运行测试，而是把一组 E2 结果晋升为可审计事实：

- receipt 只引用全部通过的原始 report hash；
- receipt 记录 candidate SHA、package tree hash、package version、Unity version、harness SHA、fixture hash 和 required matrix；
- receipt 不包含凭据、绝对路径、机器用户名、license token 或易漂移时间字段；
- Current 文档只引用 receipt，不手工拼接测试数字；
- baseline 替换必须是显式文档改动，不能由普通 Local smoke 自动完成。

建议受版本控制的位置：

```text
Docs/Reports/UPM/
  0.8.0-preview.2/
    <candidate-sha>/
      matrix.json
      matrix.md
      reports.sha256
```

原始 Unity log、NUnit XML 和完整 Consumer `Library` 继续留在外部 artifact 位置；仓库只保留稳定、脱敏、可复核的摘要和哈希。

## 4. Trigger 分类

旧触发条件集中在 manifest、asmdef 和包路径变化，但历史 VFX bootstrap 问题说明：普通 Editor 源码变化也能破坏 clean Consumer。新的最小策略如下。

| 变化 | E0 | Local base | Matching positive | Pinned matrix |
| --- | --- | --- | --- | --- |
| package root 任意受控文件 | 必须 | 按下列规则 | 按下列规则 | 晋升时 |
| `package.json` / README / CHANGELOG / LICENSE | 必须 | 必须 | 依 dependency 变化 | 必须 |
| asmdef 新增、删除、引用、constraint/version define | 必须 | 必须 | 受影响 profile 必须 | 必须 |
| Runtime/Core/bridge C# | 必须 | 编译或公共 API 风险时必须 | 仅相关 adapter | 晋升时 |
| Editor auto-load / AssetPostprocessor / package initializer | 必须 | 必须 | 受影响 profile | 晋升时 |
| Optional adapter C# | 必须 | base + 对应 positive | 必须 | 晋升时 |
| Demo/CharacterTest/repository Tests | 必须，直到迁出 package | 涉及编译闭包时 | 涉及闭包时 | 晋升时 |
| 仅注释或不影响编译的文档 | 必须 | 可不跑 | 不跑 | 晋升时统一跑 |

E0 应输出确定性的 change classification，不能依赖实施者凭感觉判断是否触发。

## 5. Report schema 优化

下一版建议从 schema 3 升为 schema 4，至少包含：

```json
{
  "schemaVersion": 4,
  "candidate": {
    "sourceMode": "pinned-git",
    "requestedCommit": "<sha>",
    "resolvedCommit": "<sha>",
    "packageTreeHash": "<hash>",
    "packageName": "com.vincent.wgameframework",
    "packageVersion": "0.8.0-preview.2"
  },
  "harness": {
    "repositoryCommit": "<sha>",
    "repositoryDirty": false,
    "fixtureHash": "<hash>"
  },
  "execution": {
    "profile": "base",
    "unityVersion": "6000.5.0f1",
    "cleanLibrary": true,
    "evidenceLevel": "E2-row",
    "eligibleForMatrix": true
  },
  "tests": {
    "expectedFullNames": ["<full-name>"],
    "passed": 7,
    "failed": 0
  },
  "player": {
    "buildRequested": true,
    "buildPassed": true,
    "runRequested": false,
    "runPassed": null,
    "buildReportHash": "<sha256>",
    "runReportHash": null
  }
}
```

字段约束：

- PinnedGit 的 `candidate.requestedCommit == candidate.resolvedCommit`；
- Local 模式 `candidate.requestedCommit/resolvedCommit` 为空，另记 source tree hash；
- `repositoryDirty` 只描述 harness checkout，不描述远端 candidate；
- profile report 只计算 `evidenceLevel` 与 `eligibleForMatrix`；Local 仍是 E1，即使 matching clean row 可作为 matrix input，也不能自行声明晋升；
- `promotable` 只存在于聚合 matrix/receipt candidate，由 source mode、clean state、candidate identity、完整 required rows、测试和 schema 校验共同计算，调用者不能覆盖；
- optional package 的 source、version、resolved commit 和 adapter DLL 分别记录；
- report 必须记录全部预期 test full name，不能只写汇总数字；
- 主 JSON 必须分别表达 Player build/run 是否请求；未执行的 gate 使用 `null`，不得伪装成失败或通过，并通过 hash 关联实际存在的详细 artifact；
- report 必须记录 forbidden log pattern 版本，以便后续解释判定变化。

## 6. Runner 分层

建议把现有单文件 runner 拆成以下职责，但保留一个兼容入口：

```text
run_upm_consumer_smoke.ps1
  -> check_upm_package_boundary.py
  -> prepare consumer project
  -> launch Unity
  -> inspect lock/cache/log/tests
  -> write profile report

run_upm_consumer_matrix.ps1
  -> invoke required profiles
  -> verify candidate identity across rows
  -> write matrix receipt candidate
```

跨平台策略：

- E0 必须可在 macOS、Windows、Linux 执行；
- E1/E2 的首个权威执行平台可继续是 Windows Unity `6000.5.0f1`；
- PowerShell runner 不应默认把 `C:\...` 当作唯一合法根，应根据平台选择安全的外部临时根，或把 Windows-only support policy 写入 report；
- Python discovery 必须返回单个可执行文件；PATH 中存在多个 `python3` 时确定性选择首个，显式 `PYTHON_BIN` 优先；
- PinnedGit 可接受不含凭据的显式 `-GitUrl`，但 report/receipt 必须区分运行时 override 与 manifest canonical URL；
- 在正式声明 macOS/Linux Consumer 支持前，增加对应平台的 clean compile/Player 行，而不是仅让脚本“能启动”。

## 7. Baseline 刷新操作

活动 `main` 与最后 reviewed immutable baseline 的距离由 `UPM_CURRENT_STATUS.md` 维护。刷新顺序固定如下：

1. 选择已经完成、验证、推送且 package tree 明确的候选完整 SHA；
2. fetch `origin`，确认 candidate 远端可达；
3. 在 candidate 的干净 detached worktree 运行 E0；
4. 对同一 candidate checkout 运行 Local `base + 3 positive`；
5. 对同一完整 SHA 运行 PinnedGit `base + 3 positive`；
6. 按 release train 追加 PinnedGit Player、Player run、optional Player 或 upgrade 行；
7. 确认所有 required 行使用同一 package tree、版本与 adapter baseline；
8. 生成 matrix candidate，人工复核 report hash、LogError、test full names、Player evidence 和 optional closure；
9. 新增 E3 receipt；
10. 只通过 E3 receipt 更新 `UPM_CURRENT_STATUS.md` 和 `MULTI_PROJECT_WORKFLOW.md` 的 reviewed baseline。

不得为了刷新 baseline 临时提交、丢弃或混入用户未完成工作，也不得把旧 E0、脏 E1 和新 candidate 的部分行拼成同一 receipt。

## 8. 失败语义

以下任一情况 fail-closed：

- candidate SHA 不完整、远端不可达或 lock hash 不一致；
- Consumer 复用了未知 Library；
- package metadata 从开发工作区读取而不是 resolved PackageCache；
- expected test 没有执行，即使 Unity 退出码为 0；
- optional package 意外进入 base lock；
- adapter package 已解析但 adapter DLL 未生成；
- Editor log 出现编译错误、package resolution failure 或 `Debug.LogError`；
- report schema/receipt hash 不一致；
- Local dirty report 被标为 promotable；
- Current baseline SHA 没有对应受版本控制 receipt。

网络、凭据、Unity license 或远端 LFS 暂时不可用属于“未得到证据”，不能降级为通过。

## 9. 完成定义

- E0 有单一跨平台实现，并由 PowerShell runner 复用；
- runner report 明确区分 candidate、harness 和 fixture identity；
- 有一键 required matrix 聚合入口；
- Current baseline 有稳定、脱敏、受版本控制的 receipt；
- 文档可以机器检查 baseline SHA 是否落后于指定 package-boundary change；
- 当前批准候选通过 required Local/PinnedGit matrix；
- 任意脏 Local 运行都不能产生 promotable receipt；
- 原始日志与仓库摘要之间有哈希关联，但仓库不保存敏感或巨大的生成目录。
