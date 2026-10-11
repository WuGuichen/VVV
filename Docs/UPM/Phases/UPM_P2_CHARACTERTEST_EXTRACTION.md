# UPM-P2 CharacterTest Extraction 任务规格

> Status: Current
>
> Task State: Complete
>
> Started: 2026-08-26
>
> Phase: UPM-P2 / R2 / L1
>
> Depends On: UPM-P1 Surface Manifest (`Complete`)
>
> Hands Off To: UPM-P3 Test Surface Split

## 1. 阶段身份与依赖

UPM-P2 把 CharacterTest 从基础 package surface 迁到 repository-validation owner。UPM-P1 已 `Complete`，strict Surface Manifest、identity/reverse dependency baseline 和 owner contract 已交付，因此 P2 当前为 `Complete`。

S1-S8 已完成：目标根、move manifest、Runtime/Editor/tests 移动、surface 更新、dependency closure、Unity MCP 编译、完整 CharacterTest 图形 PlayMode 验证和远端 PinnedGit E2 matrix 均已落地。初次完整套件的 9 个失败已修复，并由同一程序集 `39/39` 关闭；远端候选 `9080f66586db5e2043046085de35f045d7f4b07e` 的 base 与三个 optional profile 全部通过。活动路径数量、package tree 和 Consumer 结果统一读取 [UPM_CURRENT_STATUS.md](../../UPM_CURRENT_STATUS.md)。

## 2. 目标结果

- CharacterTest Runtime、Editor、测试和直接 project-owned 资源归属 package root 外的明确项目根；
- CharacterTest 继续作为仓库验证游戏依赖框架 package assembly；
- package assembly 对 CharacterTest/project assembly 的反向引用为 0；
- 移动前后的 `.meta`、GUID、asmdef identity 和 Unity 序列化引用保持；
- 基础 Consumer 不再安装 CharacterTest assembly 或项目资源，同时受影响 optional profile 无新增回归；
- CharacterTest 的关键编译、focused tests 与正式场景验证继续有效。

## 3. 权威输入

- [UPM_PHASE_TASKS.md](../../UPM_PHASE_TASKS.md)：P2 边界和退出条件；
- [UPM_CURRENT_STATUS.md](../../UPM_CURRENT_STATUS.md)：唯一 Current 数字与证据；
- [UPM_OPTIMIZATION_ROADMAP.md](../../UPM_OPTIMIZATION_ROADMAP.md)：R2 迁移顺序和最低验证；
- [UPM_PACKAGE_SURFACE_LAYOUT_OPTIMIZATION.md](../../UPM_PACKAGE_SURFACE_LAYOUT_OPTIMIZATION.md)：L1、project-owned surface 和资产移动约束；
- [UPM_CONSUMER_BASELINE_EVIDENCE_OPTIMIZATION.md](../../UPM_CONSUMER_BASELINE_EVIDENCE_OPTIMIZATION.md)：package 变化的 E0/E1 触发与证据语义；
- [UPM_OPTIONAL_INTEGRATION_OPTIMIZATION.md](../../UPM_OPTIONAL_INTEGRATION_OPTIMIZATION.md)：base absence 和 matching positive profile；
- P1 的 `upm-surface.json`、assembly/GUID inventory 与 reverse dependency baseline；
- 根 `AGENTS.md`、`Assets/AGENTS.md`、受影响路径上的 `AGENTS.md`，以及 CharacterTest 命中的 Interface/质量门禁。

## 4. 进入条件

1. P1 已完成，strict E0 对当前候选通过，CharacterTest 相关 assembly 均为 `repository-validation`。
2. 冻结唯一 project-owned 目标根。优先评估布局方案建议的 `Assets/Scripts/WGameFrameworkProject/CharacterTest/`，但必须根据现有 owner/path rules 形成明确决策后才能移动。
3. 生成完整移动清单，不能只按路径名匹配：覆盖 `Game/CharacterTest*`、对应 Editor、CharacterTest 测试、共享 Editor 中 CharacterTest 专属实现及其直接项目资源。
4. 对每个源资产记录 source path、target path、`.meta` path、GUID、asset kind、asmdef owner、序列化/代码引用者和回滚组。
5. 冻结关键场景、focused EditMode/PlayMode 测试和 optional profile 名单；测试全名从当前机器/runner 获取，不复制旧数量。
6. Unity Editor 状态可用于受控资产移动；若 Editor/MCP 不可用，不得手写序列化 YAML 或执行裸文件大搬迁。

## 5. 明确交付物

| 交付物 | 预期位置或类型 | 内容要求 |
| --- | --- | --- |
| 目标根决策 | `Docs/UPM/Evidence/UPM-P2/target-root.md` | 选定路径、备选、owner、允许依赖、非目标和回滚边界 |
| 移动清单 | `Docs/UPM/Evidence/UPM-P2/character-test-move-manifest.json` | source/target、GUID、`.meta`、类型、assembly、引用、切片与状态 |
| Unity 资产移动 | 清单冻结后的 project-owned target root | 资产与 `.meta` 成对移动；保持 GUID、序列化引用和 asmdef name |
| asmdef/dependency 修正 | 被清单点名的 CharacterTest/project asmdef 与必要 package asmdef | project -> package 单向依赖；package -> project 为 0 |
| Surface manifest 更新 | `Assets/Scripts/MxFramework/upm-surface.json` | 删除已迁出 package root 的陈旧条目，不伪造 package 外 inventory |
| 审计报告 | `Temp/MxFramework/UPM/UPM-P2/` 下的 GUID、reference、reverse-dependency、E0、Consumer 报告 | 绑定同一候选树并可复核 |
| 完成登记 | `Docs/UPM_PHASE_TASKS.md`、`Docs/UPM_CURRENT_STATUS.md` | 只登记持久结论、候选身份与证据链接/摘要 |

新增证据目录/文件属于 P2 实施交付，不是本任务规格已经完成的事实。

## 6. 实施切片及顺序

1. **P2-S1 owner freeze**：确定目标根，复核 CharacterTest assembly、Editor、测试和直接资源 owner。
2. **P2-S2 reference inventory**：生成移动 manifest；扫描 asmdef name/GUID reference、C# assembly reference、Unity YAML GUID reference、场景/Prefab/ScriptableObject 与项目路径输入。
3. **P2-S3 Runtime + Editor move**：按强连通依赖组使用 Unity Editor、Unity MCP 或受控 Unity 资产移动，资产与 `.meta` 同步；每组移动后立即检查 GUID/reference diff。
4. **P2-S4 direct project resources**：只迁移清单确认由 CharacterTest 独占的资源；共享资源保持原 owner，并通过稳定引用消费，不复制资产规避所有权。
5. **P2-S5 CharacterTest tests**：把 CharacterTest 专属测试迁到 repository-validation owner，但不开始 P3 的聚合测试全面拆分。
6. **P2-S6 dependency closure**：修正 asmdef，使 package assembly 不引用 project assembly；更新 Surface Manifest 和 E0 inventory。
7. **P2-S7 staged validation**：先静态/GUID，再 Unity compile，再 focused tests/场景，最后 clean Consumer；任何层失败先在当前切片修复或回滚。
8. **P2-S8 completion**：审计 package tree 不含 CharacterTest owner 后，登记 Current/阶段状态并移交 P3。

不要把所有移动做成一个不可审计批次。每个移动组必须有独立 before/after 清单和明确回滚点。

## 7. 范围与非目标

范围：CharacterTest Runtime、CharacterTest Editor、CharacterTest 专属测试、共享目录内明确的 CharacterTest 专属实现、直接 project-owned 资源及其必要依赖修正。

非目标：

- 不重构 CharacterTest gameplay、Combat、UI、Runtime AI Planner 或 authoring 行为；
- 不借迁移改变 public Runtime API、schema 或确定性语义；
- 不完成 repository 聚合测试与全部 Tests surface 拆分；
- 不迁移 Demo/Samples 或全面收敛 portable Editor；
- 不切换到 Embedded Package root；
- 不新增/提升 optional integration 支持等级。

## 8. 关键不变量

- Unity 序列化资产必须由 Unity Editor、Unity MCP 或仓库受控生成器写入；资产与 `.meta` 是不可分割的版本化内容。
- 每个移动项 GUID 保持不变；Prefab、Scene、ScriptableObject、Animator、Material 等序列化引用不得因移动失联。
- asmdef `name`/identity 默认保持；若确需变化，必须独立审批并提供 migration/引用修正，不得夹在目录移动中。
- 依赖方向只允许 repository CharacterTest -> package；package -> project/CharacterTest 必须为 0。
- CharacterTest 仍是正式 repository validation game，不得通过删除能力或测试让 package 变小。
- package ID、过渡 package root 和 Consumer Git subpath 在本阶段不变。
- base Consumer 不借主工程或 optional package 的传递依赖获得编译能力。

## 9. 逐层验证

1. **清单层**：移动 manifest 无重复 source/target/GUID、无遗漏 `.meta`，所有 target 位于冻结根。
2. **资产身份层**：逐项比较 before/after GUID；检查 deleted/new `.meta` 配对；序列化 GUID 引用仍可解析。
3. **assembly 层**：asmdef name、references、define/version constraints 符合基线；package -> project reverse dependency 为 0。
4. **E0 层**：strict Surface Manifest 与 package boundary 通过，CharacterTest 不再计入 package owner/closure。
5. **Unity 层**：按根规则执行 refresh/compile，等待 domain reload 与 Editor ready，Console error 为 0。
6. **CharacterTest 层**：执行启动时冻结的 focused EditMode、PlayMode、关键正式场景/Playable 验证；报告源码、测试与实际场景证据的边界。
7. **Consumer 层**：clean Local base；受 dependency/optional closure 影响的 matching positive profiles；Consumer `Assets` mutation guard 通过。
8. **候选层**：中间移动切片可先用 Local 诊断；阶段完成候选必须提交并推送，在同一远端完整 SHA 上通过 PinnedGit base 与受影响 matching optional profiles，并通过 `git diff --check`，不能拼接旧证据。

## 10. 失败与回滚

- GUID 或序列化引用变化：立即停止该移动组，按 manifest 将资产和原 `.meta` 成对移回，重新导入后确认原引用恢复。
- 发现 package assembly 依赖 CharacterTest：先隔离/反转依赖；若需新增公共 package API，视为范围变化，暂停并请求审批。
- 发现资源并非 CharacterTest 独占：从移动清单移除或单独建立 shared project owner，禁止复制形成双权威。
- Unity compile/focused scene 失败：只回滚最近移动组，不撤销用户既有改动；保留失败日志和 before/after report。
- Consumer 回归：依据 package closure 定位；不得把 optional 包加为 mandatory 来掩盖缺失依赖。
- 未经证实不得删除旧路径；只有 target 导入、引用和验证均通过后，旧路径删除才可随同一受控移动提交。

## 11. 退出证据

- 冻结移动清单全部条目状态可审计，无 orphan `.meta`、重复 GUID 或未解释资源复制；
- package root 不再包含 CharacterTest assembly 和清单确认的项目资源；
- package assembly 对 project/CharacterTest assembly 的反向依赖为 0；
- GUID、asmdef identity 与序列化引用审计通过；
- Unity compile、focused CharacterTest tests 和正式场景验证通过；
- 同一完成候选的 clean Local/PinnedGit base 与受影响 matching positive profiles 无新增回归，mutation guard 通过；
- strict E0 对候选 package tree 为 0 blocking failure；
- Current/阶段台账只按实际证据更新，未把本地或脏 E1 描述为可晋升结果。

## 12. 本地实施证据（2026-08-26）

- 目标根：`Assets/Scripts/WGameFrameworkProject/CharacterTest/`；Runtime、Editor、`MxFramework.Tests` aggregate 和 CharacterTest PlayMode assembly 名称保持不变；
- Move manifest：`Docs/UPM/Evidence/UPM-P2/character-test-move-manifest.json`，1780 条 move，source/target/GUID 均唯一，applied manifest SHA-256 `00a343a752613894b041758ff0ba850b22edb81247e8bd98900bb95ac4d16068`；
- 移动规模：Runtime 779、Editor 140、aggregate EditMode 843、CharacterTest PlayMode 18；post-move target/meta/GUID failure 为 0；
- 新 container meta：6 个，由 Unity 生成并在 manifest 中登记 GUID；
- Project owner policy：新增 `Assets/Scripts/WGameFrameworkProject/AGENTS.md` 及 Unity 生成 `.meta`（GUID `d26009a524818454ca8804e04638dbf4`），冻结 package → project 禁止依赖和 CharacterTest/Test owner；
- Standards coverage：`check_code_standards.py` 已覆盖 `Assets/Scripts/WGameFrameworkProject`，扫描 1614 个移动后的 C#/asmdef，报告 3 个既有 finding、未新增 fail gate；
- 依赖：package → project asmdef reference 为 0；strict E0 policy v3 为 129 asmdef、18 initializer、0 blocking、7 readiness gaps；remote PinnedGit package tree hash `edfb7c5c5fc22bbd80bd670ffdd34f9db0aad3617f5902355d2ffffdfd56e2cf`；
- Unity import：识别 1780 moved assets，脚本编译 0 error，BatchMode import 返回 0；
- Local Consumer：base `7/7`，FairyGUI/Unity Transport/VFX Graph 各 `8/8`，四档 Consumer/package mutation guard 全部通过；
- 历史 dirty Local report 只保留为迁移诊断，不参与晋升；同 SHA clean detached Local 再次通过 base `7/7` 与三个 optional `8/8`，但因 UPM 的 `.gitignore` 排除、Windows resolved CRLF 和 `package.json._fingerprint` 注入产生不同 source-mode tree 表示，仍按 E1/non-promotable 处理；
- EditMode baseline parity：P1 baseline 与 P2 working tree 均为 113 total / 84 passed / 29 failed；29 个失败名和消息完全一致，因此无新增迁移回归；
- Headless PlayMode baseline parity：两边均为 4 total / 2 passed / 2 failed，失败名和消息一致；两项通过覆盖 large-character presence motion/rollback 与 obstacle/semantic isolation；
- Unity MCP `validate_unity_scripts` job `a84bb137d6e445a5b45ba811367f6efb` 通过：refresh、compilation、domain reload、Editor ready 均已观测，Console error 为 0；
- 图形 Editor 下由 Unity MCP 运行 Dragon14 两项正式 PlayMode 用例，job `4bc6bb9f7a7b4c848db25dbd65250f4e` 为 `2/2` passed、0 failed，截图已更新；MCP 生成的 `TestResults.xml` SHA-256 为 `bb3d43ad8e5c5d3f9e05812581d229464510c1ee86f1e488a87e4065c22e73bb`，详见 [mcp-validation.md](../Evidence/UPM-P2/mcp-validation.md)；
- Unity MCP 初始完整 CharacterTest PlayMode job `58196aaf868b4f368828b5b6d4847704` 暴露 9 个失败；修复后原失败集合 job `abc805ad1511456d890787d7d7f57474` 为 `9/9`，完整程序集 job `266644a21f0442b8962a2987723ccac3` 为 `39/39` passed；
- 修复覆盖陈旧 Profile/数值 Action ID/UI timeline 测试契约、LandingProfile 生成输入、动画 playback speed 投影、rollback last-completed frame 派生重建、process-local revision 与 authority hash 分离、FairyGUI package name/id 清理，以及 P2 后生成器旧路径；新增 5 条 authority/cache/config EditMode 聚焦回归为 `5/5`；
- 完整 aggregate 在 BatchMode 中触发 `CharacterTestRuneTurretVfxView` 的 URP ParticleSystem 原生崩溃，仍是已记录的非迁移 residual；Dragon14 的 `-nographics` 限制已由上述图形 MCP 运行关闭；
- PinnedGit E2：candidate `9080f66586db5e2043046085de35f045d7f4b07e`，base `7/7`、FairyGUI/Unity Transport/VFX Graph 各 `8/8`，四份 requested/resolved SHA 与 package tree 一致；report/XML hash 见 [e2-matrix.md](../Evidence/UPM-P2/e2-matrix.md)；
- 当前 P2 声明 `Complete`；E3 release receipt 仍未建立，也不属于本阶段退出条件。

## 13. 移交 UPM-P3

向 P3 移交：迁移后的 project-owned CharacterTest 根、CharacterTest 测试清单、仍留在 package `Tests/` 的 assembly owner、测试依赖闭包、Test Framework 使用点、当前 Consumer fixture/testables 合同，以及任何因 P2 保留在聚合测试中的临时兼容项。P3 不得重新把 CharacterTest 测试放回 package test。
