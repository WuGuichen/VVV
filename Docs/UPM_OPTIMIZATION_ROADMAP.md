# MxFramework UPM 优化路线

> Status: Current
>
> Revised: 2026-08-27

本文只维护 UPM 后续工作的顺序、决策边界和完成定义。当前数字、HEAD、Consumer 测试计数和证据状态统一读取 `UPM_CURRENT_STATUS.md`；8 个阶段任务的边界、依赖与状态读取 `UPM_PHASE_TASKS.md`；Embedded Package 决策由 `Decisions/ADR-015-upm-embedded-package-topology.md` 冻结；Phase A-C 的历史执行记录保留在 `UPM_EXTERNAL_CONSUMPTION_IMPLEMENTATION_PLAN.md`。

## 1. 当前结论

MxFramework 已具备可工作的 preview UPM 边界，但还不是收敛的可发布框架包：

- 基础包与三个 optional positive profile 已有历史 immutable 证据；
- P1-P5 已完成 package surface 收敛，CharacterTest、仓库回归、完整 Demo 和 project-only Editor 均已迁出；
- P1-P7 已完成：开发工程使用 `Packages/com.vincent.wgameframework` embedded root，固定 Local/PinnedGit 八行矩阵已生成可晋升的 P7 evidence；P8 因 owner 发布决策与 legal/Player/upgrade/compatibility 门禁保持 `Blocked`；
- build-only Player gate 已落地，但 Player run、N-1 upgrade 和 release receipt 尚未落地；
- FMOD 仍没有批准的外部 Consumer source/profile。

因此下一阶段不继续扩大 package surface，也不优先拆多个 UPM 包。核心目标是：

```text
Classify surface
  -> move project-owned content out
  -> converge portable Editor and samples
  -> switch development to embedded Packages layout
  -> refresh immutable evidence
  -> establish release train
```

路线到阶段任务的映射：

| 路线 | 阶段任务 |
| --- | --- |
| R1 | UPM-P1 Surface Manifest |
| R2-R3 | UPM-P2 CharacterTest、UPM-P3 Tests、UPM-P4 Demo/Samples、UPM-P5 Editor |
| R4 | UPM-P6 Embedded Package Cutover |
| R5-R6 | UPM-P7 Consumer Evidence/Optional Matrix、UPM-P8 Preview Release Readiness |

R0 文档权威收口已完成，不计入 8 个实施阶段。

## 2. 目标拓扑

最终开发工程与 Consumer 使用同一种 package asset path：

```text
Packages/
  com.vincent.wgameframework/
    package.json
    Runtime and portable modules
    Editor/
    Tests/Editor/
    Tests/Runtime/
    Samples~/
    Documentation~/

Assets/
  Scripts/WGameFrameworkProject/
    CharacterTest/
    Editor/
    Tests/
```

仓库可以继续保持 monorepo。Embedded Package 解决的是 Unity 目录和消费边界一致性，不要求立即拆 Git repository。

## 3. 实施路线

### R0：恢复文档与机器事实一致性

- `UPM_CURRENT_STATUS.md` 成为唯一 Current 状态入口；
- 历史实施计划标记 Archive；
- README、PROJECT_INDEX 和 Multi-project workflow 只引用 Current 状态，不复制活动数字；
- Consumer report schema 明确 Player 证据和 promotable 语义。

完成条件：文档中不再同时存在互相冲突的“当前测试数量”“当前 Player 状态”或“当前 baseline”。

### R1：建立 surface manifest

> Phase status: Complete（2026-08-26）；实施证据读取 `UPM/Phases/UPM_P1_SURFACE_MANIFEST.md`。

- 新增 `upm-surface.json`；
- 对全部 package asmdef 标记 `base-runtime`、`portable-editor`、`optional-adapter`、`package-test`、`package-sample` 或 `repository-validation`；
- 对全部 Editor initializer 标记 owner 和 absent-capability 行为；
- E0 使用严格模式阻断未分类 assembly/initializer；
- 记录 asmdef identity、GUID 和 reverse dependency baseline。

R1 不移动文件，先把“谁属于包”变成机器可检查的决策。

### R2：迁出项目 surface

> Phase status: UPM-P2 Complete；CharacterTest/code/test move、Unity MCP 编译、原 9 项回归、完整 CharacterTest PlayMode `39/39` 与远端四档 PinnedGit E2 matrix 已通过。下一阶段为 UPM-P3 Test Surface Split。

按以下顺序实施，每一步单独验证和提交：

1. `Game/CharacterTest*` 与对应 Editor/测试；
2. repository-only 聚合测试；
3. 主工程 Demo 与验证场景；
4. 依赖 `Tools/`、`FGUIProject/`、项目 Config/Scene/VFX/Bundles 的 Editor 实现。

Package assembly 不得反向依赖迁出的 project assembly。CharacterTest 继续作为主仓库的框架验证游戏，而不是基础包内容。

### R3：建立 package-owned surface

> Phase status: UPM-P3～UPM-P5 Complete；P5 的 29 个 project-only Editor 文件迁移、portable absent/no-write 与同 SHA 四档 PinnedGit matrix 已通过。UPM-P6 Embedded Package Cutover 当前为 In Progress。

- 只保留最小 `Tests/Editor` / `Tests/Runtime` contract；
- 选择一个 self-contained vertical slice 进入 `Samples~`；
- portable Editor 在缺少项目数据与 optional integration 时静默 unavailable，不抛异常、不写 `LogError`、不隐式修改 Consumer；
- 重新评估 Test Framework、URP、Input System 是否仍必须是 base manifest dependency；
- 把“大一统 Editor”按 portable core、authoring 和 optional adapter 拆成清晰 asmdef。

### R4：Embedded Package cutover

> Phase status: Complete；候选 `c046158f5` 的 root move、identity audit、embedded load、Unity/Repository tests、Local E1 与新 subpath PinnedGit E2 均已通过。

- 通过 Unity Editor、Unity MCP 或受控资产移动，把 package root 从 `Assets/Scripts/MxFramework` 移到 `Packages/com.vincent.wgameframework`；
- 保留 `.meta` 和 GUID；
- 更新 local `file:`、Git subpath、runner、文档与 CI；
- 迁移窗口明确标记 preview breaking change，并提供 Consumer manifest 迁移说明；
- 验证 package asset locator 只使用 canonical package root 后，删除 `Assets/Scripts/MxFramework` 开发回退。

R4 不与 CharacterTest/Tests 大搬迁合并为一次提交。

### R5：Optional integration 收敛

- FairyGUI、Unity Transport、VFX Graph 继续维护 base absence + matching positive profile；
- FMOD 先冻结许可、分发、LFS、依赖、平台二进制和 bank owner，再实现 `with-fmod`；
- adapter minimum/validated version、define、assembly 和 support tier 建立单一机器权威；
- 只有出现独立消费、版本或维护节奏时才拆成单独 UPM adapter package。

### R6：Preview/stable release train

- 补齐 LICENSE、Third Party Notices、Documentation 和 manifest metadata；
- report schema 统一 candidate、harness、fixture、test full names 与 Player evidence；
- 增加 PinnedGit/TaggedGit、Player run 和 N-1 upgrade matrix；
- 建立 public API/Schema compatibility 与 deprecation policy；
- 对同一干净远端 SHA 生成受版本控制 E3 receipt；
- 先晋升新的 preview，再评估 `1.0.0-rc` 和 stable。

## 4. 每个迁移切片的验证

目录、asmdef 或 public boundary 变化最低要求：

- `.meta`/GUID 与 asmdef identity 审计；
- E0 package boundary；
- Unity refresh/compile、Editor ready、Console error 为 0；
- 受影响 EditMode/PlayMode tests；
- clean Local base 和 matching optional profiles；
- package/Consumer mutation guard；
- 候选提交的 PinnedGit matrix；
- public API 或安装 URL 变化时提供 migration note 和版本判断；
- `git diff --check`。

历史通过结果不能与新 package tree 拼接成同一 receipt。

## 5. 决策边界

- Package ID、canonical repository URL 和框架许可证必须由 owner 明确批准；Development Agent 不猜测。
- 移动的 `main`、dirty Local report 和主工程测试不构成 release evidence。
- `MxFrameworkPackageAssetLocator` 的 development fallback 是迁移桥，不是最终架构。
- 项目专用工具可以继续存在，但 owner 必须位于 package root 外，或通过显式 host capability 接入。
- 不为了目录整齐制造无收益的 Runtime/Editor 二次嵌套；R4 的收益是开发/消费路径一致，而不是模仿模板树。

## 6. 完成定义

- Current 状态由单一文档和机器报告维护；
- package root 内全部 assembly/initializer 都有 owner；
- CharacterTest、repository tests 和 project-only Editor 不随基础包安装；
- 开发工程和 Consumer 都从 `Packages/com.vincent.wgameframework` 读取 package asset；
- package-owned tests、Samples 和 Documentation 使用 Unity package 约定；
- base 不解析未声明 optional integration；每个支持 adapter 有 matching evidence；
- manifest、许可、tag、Player、upgrade、API compatibility 和 receipt 门禁完整；
- package version、tag、resolved commit、tree hash 和 E3 receipt 一致；
- 任意历史或本地诊断证据都不会被表述成当前 stable claim。
