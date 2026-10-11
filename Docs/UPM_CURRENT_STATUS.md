# MxFramework UPM 当前状态

> Status: Current
>
> Snapshot: 2026-08-27
>
> Audited implementation candidate: `50036e76a0eb4ff9ca248965a548949173faffaa`
>
> Audited package identity: UPM-P7 canonical Git tree `05095ca820b4d819dba8ada303c994105e0193913cf7007b1c6406e2e524d72e`
>
> Active phase: UPM-P8 `Blocked` by missing owner release decisions

本文是 MxFramework UPM 的唯一 Current 事实入口。它回答“当前包边界、最近证据和下一项工作是什么”；历史执行过程、目标设计和发布政策由下方链接文档维护，不在多份文档中重复 Current 数字。

## 1. 当前允许的声明

```text
MxFramework candidate 50036e76a0eb4ff9ca248965a548949173faffaa passed the fixed eight-row Local/PinnedGit matrix.
The schema-valid P7 matrix is promotable evidence, while its E3 receipt candidate remains releaseReady=false.
The moving main branch is not a release channel.
```

当前不能声明稳定发布，也不能把历史 immutable baseline、主工程编译或脏 Local Consumer 结果解释为当前 `main` 的可晋升证据。

## 2. Package 身份与静态边界

| 项目 | 当前事实 |
| --- | --- |
| Package name | `com.vincent.wgameframework` |
| Display name | `WGame Framework` |
| Version | `0.8.0-preview.1` |
| Unity baseline | `6000.5` |
| Canonical package root | `Packages/com.vincent.wgameframework` embedded package |
| Mandatory dependencies | 14；Test Framework 已转为 Consumer/main-project 显式测试依赖 |
| Package files | 3,209；由候选 Git tree 直接计数 |
| asmdef | 117 |
| Editor/runtime initializer files | 14 |
| E2 canonical candidate tree hash | `05095ca820b4d819dba8ada303c994105e0193913cf7007b1c6406e2e524d72e`（P7 八行一致；resolved physical tree 另记） |

当前 E0 policy v3 结果：

- 0 blocking failure；
- optional reverse closure：FMOD `2/0`、Magica Cloth `1/0`、FairyGUI `1/0`、Unity Transport `1/0`、VFX Graph `1/0`；
- uGUI direct source boundary：`0/0`；
- 3 个 release-readiness gap；category、keywords 与 `Documentation~` 已由本轮审计修复。

E0 只证明静态 package 边界，不证明 Unity import、Player 或升级通过。

## 3. 当前 package surface

当前 package root 已移出 CharacterTest、repository tests 和完整 Demo，只保留框架、最小 package tests 与一个显式导入 sample。以下是排除 `.meta` 后的路径计数：

| Surface | 文件数 | 目标 owner |
| --- | ---: | --- |
| `Game/` | 0 | CharacterTest 已迁至 `Assets/Scripts/WGameFrameworkProject/CharacterTest/` |
| `Tests/` | 4 | 两个 asmdef + 两个 C#，提供 9 条自包含 package contract tests |
| `Demo/` | 0 | 已迁至 `Assets/Scripts/WGameFrameworkProject/Demos/` |
| `Samples~/RuntimeHostMinimalLifecycle/` | 5 | 唯一显式 Import 的 base sample |
| `Editor/` | 287 | portable Editor；项目路径通过 host provider 注入，project-only Combat、CharacterTest VFX、FairyGUI fallback 与 VFX Graph sample builder 已迁出 |
| `Documentation~/` | 1 | 最小 package 文档入口；完整发布文档仍由 P8 审核 |

CharacterTest Runtime、Editor、aggregate EditMode tests 和 CharacterTest PlayMode tests 已移出 package root。UPM-P3 已 `Complete`：10 个旧 test assemblies、31 个 C#、183 个测试属性已按原 asmdef/GUID 迁至 `Assets/Scripts/WGameFrameworkProject/Tests/Repository/`；package 保留两个 contract test 文件，当前执行 9 条契约。Test Framework 已从主包 mandatory dependencies 移除，并由 Consumer/main project 显式拥有。Repository exact baseline parity 证明迁移新增失败为 0；随后两项 Audio 契约、ResourceSceneFlow 自包含前置、FairyGUI pending-key 诊断和 PreviewScene 全局状态隔离已修复。三个 repository EditMode assemblies 当前为 `162/162`，五个 repository PlayMode assemblies 为 `21/21`。

## 4. Consumer 证据台账

| 等级 | Package identity | 当前结论 |
| --- | --- | --- |
| E0 Static | P7 candidate `50036e76a...` | strict 通过；14 dependencies、117 asmdef、14 initializer、0 blocking、3 readiness gaps |
| E1 Local | P7 clean detached candidate | base `18/18`，三个 optional profile 各 `19/19`；四行均 schema-valid、eligible，但单行不可晋升 |
| E2 Immutable matrix | `50036e76a0eb4ff9ca248965a548949173faffaa` / canonical tree `05095ca8...` | Local + PinnedGit 固定八行全部通过；candidate equality 与 artifacts/mutation/log gates 通过，matrix `promotable=true` |
| E3 receipt candidate | 版本化，尚未晋升 | `releaseReady=false`；等待 P8 legal/tag/Player/upgrade/compatibility gates；见 `UPM/Evidence/UPM-P7/` |

当前 runner 的精确 Consumer 测试契约为：

- base：18 个测试（9 Consumer-owned + 9 package-owned）；
- `with-fairygui`：19 个测试；
- `with-unity-transport`：19 个测试；
- `with-vfx-graph`：19 个测试；
- `-IncludePlayerBuild`：只支持 base，构建但不启动 Player。

主 `report.json` 为 schema 4，统一 candidate/harness、expected/observed full names、optional provenance、Player 三态、artifact hash、mutation 与 log policy。profile 不含 `promotable`；只有完整 matrix 自动计算该字段。

## 5. Release-readiness gaps

当前静态检查报告以下 3 项，均需要 owner 的法律或发布决定：

1. manifest `license` 或 `licensesUrl`；
2. package `LICENSE.md`；
3. `Third Party Notices.md`。

此外，稳定发布仍缺 tag source smoke、PinnedGit Player、Player run、optional Player、N-1 upgrade、migration fixtures、public API compatibility policy 和 E3 receipt。

## 6. 已接受的下一阶段顺序

活动执行边界由 `UPM_PHASE_TASKS.md` 维护，固定为 8 个阶段任务：

1. UPM-P1 Surface Manifest；
2. UPM-P2 CharacterTest Extraction；
3. UPM-P3 Test Surface Split；
4. UPM-P4 Demo and Samples Split；
5. UPM-P5 Editor Portability；
6. UPM-P6 Embedded Package Cutover；
7. UPM-P7 Consumer Evidence and Optional Matrix；
8. UPM-P8 Preview Release Readiness。

阶段状态和当前可启动项只以 `UPM_PHASE_TASKS.md` 为准，本文不建立第二份状态台账。阶段启动后才拆文件级实施切片；不得把多个阶段的大规模移动合并成一次不可审计变更。

在第 1-4 步完成前，不为了目录形式拆成大量独立 UPM 包。Optional adapter 是否独立发布应由依赖、消费需求和版本节奏决定。

## 7. 文档与机器事实源

| 主题 | 权威 |
| --- | --- |
| Current 状态 | 本文 |
| 总体实施顺序 | `UPM_OPTIMIZATION_ROADMAP.md` |
| 阶段任务、依赖与状态 | `UPM_PHASE_TASKS.md` |
| 各阶段交付物、切片、验证与回滚 | `UPM/Phases/README.md` |
| Package surface 与 Embedded Package 迁移 | `UPM_PACKAGE_SURFACE_LAYOUT_OPTIMIZATION.md` |
| Embedded Package accepted decision | `Decisions/ADR-015-upm-embedded-package-topology.md` |
| Consumer 证据等级和晋升规则 | `UPM_CONSUMER_BASELINE_EVIDENCE_OPTIMIZATION.md` |
| Optional integration | `UPM_OPTIONAL_INTEGRATION_OPTIMIZATION.md` |
| Release、Player、升级和兼容政策 | `UPM_RELEASE_UPGRADE_READINESS_PLAN.md` |
| 日常多项目接入 | `MULTI_PROJECT_WORKFLOW.md` |
| Phase A-C 历史 | `UPM_EXTERNAL_CONSUMPTION_IMPLEMENTATION_PLAN.md` |

机器事实源：

```text
Packages/com.vincent.wgameframework/package.json
Tools/Harness/check_upm_package_boundary.py
Tools/Harness/run_upm_consumer_smoke.ps1
Tools/PackageConsumerSmoke/UnityProject
Docs/Reports/UPM/                # 目标 E3 receipt 位置，当前尚未建立
```

## 8. 维护规则

- Package root、manifest、asmdef、Editor initializer、Consumer runner、expected tests、Player gate 或证据 baseline 变化时必须同步本文。
- Current 数字优先由 checker/report 生成；专项设计文档不再复制活动 HEAD、测试计数或 tree hash。
- 历史 baseline 只进入 evidence ledger/receipt，不覆盖 Current implementation identity。
- 更新本文不等于晋升 baseline；只有完整 E2 matrix 和受版本控制 E3 receipt 才能改变可发布声明。
