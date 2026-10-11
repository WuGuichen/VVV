# UPM-P3 Test Surface Split 任务规格

> Status: Current
>
> Task State: Complete
>
> Phase: UPM-P3 / R2-R3 / L2
>
> Depends On: UPM-P2 CharacterTest Extraction (`Complete`)
>
> Hands Off To: UPM-P4 Demo and Samples Split

## 1. 阶段身份与依赖

UPM-P3 在 CharacterTest 已迁出后，把 package contract tests 与 repository-validation tests 分开。前置阶段 UPM-P2 已 `Complete`，CharacterTest owner、测试移交清单和 Consumer contract 已稳定；P3 已满足退出条件并移交 UPM-P4。

P3-S1-S9 已完成：owner/coverage 冻结、repository extraction、四条 package contract、依赖/testables 清理、Test Framework owner 迁移、Unity/Consumer 验证、P3 前 repository baseline parity，以及同一远端完整 SHA 的四档 PinnedGit E2 均已闭合。活动测试数量、expected full names、runner 状态和 package tree 只读取 [UPM_CURRENT_STATUS.md](../../UPM_CURRENT_STATUS.md)。

## 2. 目标结果

- 每个测试 assembly 恰好属于 `package-test` 或 `repository-validation`；
- package root 只保留最小、自包含、可由 Consumer `testables` 启用的 `Tests/Editor` 与 `Tests/Runtime` contract；
- 聚合、CharacterTest、项目场景、性能、authoring 和工具链回归保留在 package root 外并继续运行；
- Consumer fixture 显式拥有 Test Framework/testables 依赖，不借主包传递依赖；
- 生产 assembly 不引用 NUnit、`UnityEditor.TestRunner` 或 repository test assembly；
- 测试缩面通过“移动并保持覆盖”完成，不允许靠删除测试缩包。

## 3. 权威输入

- [UPM_PHASE_TASKS.md](../../UPM_PHASE_TASKS.md)：P3 范围、非目标与退出条件；
- [UPM_CURRENT_STATUS.md](../../UPM_CURRENT_STATUS.md)：唯一 Current 测试/Consumer 事实；
- [UPM_OPTIMIZATION_ROADMAP.md](../../UPM_OPTIMIZATION_ROADMAP.md)：R2-R3 顺序与验证门禁；
- [UPM_PACKAGE_SURFACE_LAYOUT_OPTIMIZATION.md](../../UPM_PACKAGE_SURFACE_LAYOUT_OPTIMIZATION.md)：package test contract、L2、Test Framework 移除前提；
- [UPM_CONSUMER_BASELINE_EVIDENCE_OPTIMIZATION.md](../../UPM_CONSUMER_BASELINE_EVIDENCE_OPTIMIZATION.md)：expected full names、E0/E1/E2、runner 与 fail-closed 语义；
- [UPM_OPTIONAL_INTEGRATION_OPTIMIZATION.md](../../UPM_OPTIONAL_INTEGRATION_OPTIMIZATION.md)：base absence、adapter 最小 contract 与 matching positive profile；
- P1 Surface Manifest 与 P2 移交的测试/依赖清单；
- 根 `AGENTS.md`、受影响测试路径的 `AGENTS.md`、相关 Interface 与 [QUALITY_GATE.md](../../QUALITY_GATE.md)。

## 4. 进入条件

1. P2 已完成，CharacterTest assembly/测试位于 project-owned surface，package -> project 反向依赖为 0。
2. 枚举全部测试 assembly、测试源码、fixture/scene/config/resource、category、platform、EditMode/PlayMode、被测 contract 和依赖闭包。
3. 冻结分类判据：package test 只能验证 package public/internal contract 且不依赖仓库外围；其它均为 repository validation。
4. 建立“现有测试 -> 新 owner/path -> 覆盖 contract”的一对一映射，任何删除、合并或替代必须显式说明等价覆盖和审批。
5. 从 Consumer runner/fixture 读取当前 expected test full names、manifest/testables 与 Test Framework source，不把旧数字写进本规格。
6. 识别生产 assembly 对 NUnit/TestRunner 的非法引用和 package manifest 中 Test Framework 的真实依赖者，但在分类证据完成前不删除依赖。

## 5. 明确交付物

| 交付物 | 预期位置或类型 | 内容要求 |
| --- | --- | --- |
| 测试 owner manifest | `Docs/UPM/Evidence/UPM-P3/test-surface-manifest.json` | assembly、test source、旧/新 path、owner、mode、fixture、contract、状态 |
| 覆盖守恒报告 | `Docs/UPM/Evidence/UPM-P3/test-coverage-map.md` | 每个被移动/合并测试的原 contract、继任测试与差异；未批准删除为 0 |
| Package tests | `Assets/Scripts/MxFramework/Tests/Editor/` 与 `Assets/Scripts/MxFramework/Tests/Runtime/` | 最小 package contract；具体路径以过渡 root 为准，保留 `.meta`/GUID |
| Repository tests | P2 冻结的 project-owned `Tests/` 根 | 聚合、CharacterTest、项目场景、性能、authoring、工具链回归 |
| asmdef/testables 修正 | 对应 test asmdef、主工程 `Packages/manifest.json`、`Tools/PackageConsumerSmoke/UnityProject/Packages/manifest.json` | owner 单一、依赖显式、expected tests 可运行 |
| Consumer runner 预期名单 | `Tools/Harness/run_upm_consumer_smoke.ps1` 及必要 fixture test asmdef/source | 使用精确 full names；不以汇总数字代替执行确认 |
| 执行报告 | `Temp/MxFramework/UPM/UPM-P3/` 下的分类、repository tests、package tests、E0、Consumer 报告 | 绑定同一候选树，区分未运行与失败 |
| 完成登记 | `Docs/UPM_PHASE_TASKS.md`、`Docs/UPM_CURRENT_STATUS.md` | 只登记持久边界和真实证据 |

若审计证明当前过渡 root 需要不同的最小目录布局，必须先更新布局权威并说明原因，不能把 repository tests 伪装到 `Samples~` 或 Consumer fixture。

## 6. 实施切片及顺序

1. **P3-S1 inventory/classification**：生成测试 owner manifest 和 dependency graph；禁止在该切片移动或删除测试。
2. **P3-S2 coverage freeze**：对每个 assembly/test 建立 contract 映射；标记 package contract 最小集合与 repository-only 集合。
3. **P3-S3 repository extraction**：按 assembly/fixture 强连通组将聚合、场景、性能、authoring、工具链测试迁出 package；资产与 `.meta` 成对移动。
4. **P3-S4 package contract layout**：建立最小 `Tests/Editor`、`Tests/Runtime`，保留 RuntimeHost、public API sentinel、portable Editor absent-data 和 optional adapter 小型合同等真实 package 契约。
5. **P3-S5 dependency cleanup**：修正 test asmdef、`InternalsVisibleTo`、production TestRunner/NUnit 引用；禁止为了编译把 repository assembly 重新引用进 package。
6. **P3-S6 Consumer ownership**：让 Consumer fixture 显式声明 Test Framework/testables；更新精确 expected full names 和 runner 预期。
7. **P3-S7 Test Framework reevaluation**：只有所有必要条件有证据时才从主包 mandatory dependencies 移除 Test Framework；否则保留并登记 blocker。
8. **P3-S8 layered verification**：repository tests、package test Consumer、base/optional Consumer 分层运行，核对 coverage map 与 mutation guard。
9. **P3-S9 completion**：确认无未经说明的测试删除/降级，更新 Current/阶段台账并移交 P4。

## 7. 范围与非目标

范围：测试分类、测试/fixture 的 owner 迁移、最小 package tests、repository test 保持、asmdef/testables/runner 预期和 Test Framework 依赖评估。

非目标：

- 不重新设计全部测试框架、命名规范或 CI；
- 不通过删除、跳过、扩大 ignore/category filter 或降低断言缩小 package；
- 不把 repository-only tests 伪装成 `Samples~`、Consumer smoke 或 package contract；
- 不迁移 Demo/Samples，不全面收敛 Editor portability；
- 不执行 Embedded Package cutover；
- 不把 P7 report schema/matrix 升级夹入本阶段。

## 8. 关键不变量

- 测试总覆盖不得出现未经说明的删除或降级；测试移动不是删除测试的授权。
- package test 不依赖 WGameFramework 外围 Scene、Config、FGUIProject、Tools、FMOD bank 或 CharacterTest。
- repository test 可依赖 package assembly；package/production assembly 不得反向依赖 repository test。
- package production assembly 不泄漏 NUnit或 `UnityEditor.TestRunner` 类型。
- Test Framework dependency 只能在 clean base、package-test Consumer、Consumer smoke 与 repository tests 都证明自有依赖后移除。
- expected test 必须按 full name 验证；Unity 退出码 0 但预期测试未执行仍然 fail-closed。
- `.meta`、GUID、asmdef identity 和序列化 fixture 引用遵循 Unity 资产移动不变量。

## 9. 逐层验证

1. **分类层**：每个测试 assembly 唯一 owner；manifest 无漏登、重复或悬空 fixture。
2. **覆盖层**：coverage map 覆盖全部移动/合并项；删除数为 0，或每个例外都有 owner 批准和等价/替代证据。
3. **依赖层**：package tests 只依赖 package contract；production assembly 无 NUnit/TestRunner；package -> repository reverse dependency 为 0。
4. **资产层**：移动的 scene/fixture/script 与 `.meta` 成对，GUID 和序列化引用保持。
5. **Unity 层**：refresh/compile、domain reload、Editor ready、Console error 0。
6. **repository 层**：运行按模块/场景冻结的 focused EditMode 与 PlayMode suites，确认 CharacterTest 和项目回归仍在主工程生效。
7. **package-test 层**：在真实 Consumer 中通过显式 `testables` 与显式 Test Framework dependency 运行 `Tests/Editor`、`Tests/Runtime` 精确 full names。
8. **Consumer 层**：clean Local base 和受影响 matching optional profiles 通过；expected full names 全部执行；optional absence、adapter DLL 与 mutation guard 正确。
9. **静态/候选层**：中间分类切片可先用 Local 诊断；阶段完成候选必须提交并推送，在同一远端完整 SHA 上通过 strict E0、PinnedGit base 与受影响 matching optional profiles，并通过 `git diff --check`，不能拼接旧结果。

## 10. 失败与回滚

- 测试无法唯一分类：保留在 repository-validation 候选并标记 blocker，不能默认留作 package test。
- 移动后覆盖缺口：恢复原测试/fixture 或先补等价继任测试；在 coverage map 闭合前不删除旧项。
- package tests 依赖项目数据：迁回 repository owner，或另做自包含 fixture；禁止复制私有项目资产进 package。
- Test Framework 移除导致任一环境失败：恢复主包 dependency，并记录尚未满足的必要条件，不使用传递依赖补洞。
- expected full name 未执行：视为失败，修复 asmdef/testables/filter/runner；不能下调 expected count。
- GUID/fixture 引用变化：回滚最近移动组并恢复原 `.meta`；保留失败报告。
- 所有回滚仅作用于本阶段确认文件，不清理用户既有改动、缓存或 Recovery 状态。

## 11. 退出证据

- 测试 owner manifest 显示每个测试 assembly 恰好一个 owner；
- package root 只保留经 coverage review 接受的最小 package contract tests；
- repository-only tests 在主工程继续运行，CharacterTest/场景/authoring/工具链覆盖无未经说明删除或降级；
- package tests 在真实 Consumer 中通过显式 `testables` 和 Test Framework dependency 运行；
- package/production assembly 对 repository tests、NUnit、TestRunner 的非法依赖为 0；
- 同一完成候选的 clean Local/PinnedGit base 与受影响 optional profiles 无新增回归，expected full names 和 mutation guard 通过；
- strict E0 为 0 blocking failure，Surface Manifest 与实际路径一致；
- Test Framework 若被移除，所有必要条件均有证据；若未移除，Current 如实登记 blocker，不影响其余已完成边界的描述；
- Current/阶段台账已更新，未把本地诊断冒充 immutable/release 证据。

## 12. 实施与完成证据（2026-08-26）

- Owner manifest：`Docs/UPM/Evidence/UPM-P3/test-surface-manifest.json`；10 个旧 test assemblies、31 个 C#、183 个测试属性全部登记为 `repository-validation`，asmdef GUID 全部保持，删除数 0；
- Repository target：`Assets/Scripts/WGameFrameworkProject/Tests/Repository/`；额外识别并迁出旧生产式路径中的 `MxFramework.Tests.Application.SessionBridge.Unity.Editor`；
- Package contract：`MxFramework.PackageTests.Runtime` 与 `MxFramework.PackageTests.Editor`，共 4 条精确测试；Unity MCP job `4d3571977acd4b119dca8de6457fe860` 为 `4/4`；
- Unity compile：MCP job `f67062336c1a48e69bdf1fc230fe8c2a` 通过 refresh/compile/domain reload/Editor ready，Console error 0；
- Test Framework ownership：主包 dependency 已移除；main `Packages/manifest.json` 保留显式依赖，Consumer manifest 显式写入 `com.unity.test-framework=1.7.0` 与 `testables=[com.vincent.wgameframework]`；实际 lock 中 Test Framework depth 为 0，resolved package 不再声明该依赖；
- strict E0：14 dependencies、121 asmdefs、18 initializers、0 blocking、7 readiness gaps；package tree `1ccdaded2cff24bd442438af2e792c58e5000daa9ffe872250427f6d8d93a0c8`；
- Local Consumer：base `11/11`，FairyGUI/Unity Transport/VFX Graph 各 `12/12`；四档 Consumer/package mutation guard 通过；
- Repository suites：迁移验收时 EditMode assemblies 为 `160/162`，PlayMode assemblies 为 `18/21`；P3 前候选精确复跑证明当时的资源释放顺序、两项 Audio 与 ResourceSceneFlow 三类失败消息一致，迁移新增回归为 0。后续修正过期 Audio Event 契约、FMOD smoke 的 controlled-handle 判据，并为 Offline ResourceSceneFlow 增加可恢复的 prebuild/cleanup fixture；五个 PlayMode assemblies 在最终 MCP job `75f5e158df594c34a29fede7ada78419` 为 `21/21`。FairyGUI 同步 pending 诊断改为保留真实 pending key，UiFoundation Preview 在运行前重置受污染的 Stage/Package 且不销毁后续测试共享 Font；三个 EditMode assemblies 在 MCP job `46f73ed825c0411f94c5db8013e1b6e8` 为 `162/162`；
- Baseline XML SHA-256：EditMode `3f8d29a25ee78a120b2412ed619581fddf2622393c89609de7abf37ef4ff2fc2`、PlayMode `5af2612917b04043646319f04425f7bbb7cf82bb99c8522d7227bdfa2c9704fc`；当前 exact EditMode MCP job `ee9e452efd1048149ddf02a83ba1ec44`；
- Local report SHA-256：base `61a766e29845688590511152d49c3420717980440f731f713c66c9034b9a1922`、FairyGUI `eed0ff793a8b45e3b5926129aa9db88fbaac5cba1a34f292c7f20ab80108924f`、Unity Transport `044df0d4eedfaa682dcbbc00bb9464c1f605e397ee32ad3257f072f32cd40104`、VFX Graph `dcca43874fe31120baed7c747cee5153b48a5a806517b7977a9a6fb2592f0e98`；
- Historical Clean Local：已被后续修复取代的候选 `a48ad5c1660c3ccd8fd19006b99e6141db1b4745` detached worktree 为 `repositoryDirty=false`；base `11/11`、三个 optional profile 各 `12/12`，Consumer/package mutation guard 全部通过。这些结果只作为历史 E1，不与当前候选 E2 拼接；
- Immutable E2：修复 Repository 回归后的候选 `f8958d7cb66764e77a4fa79814494b14dc46c7dc` 在同一远端完整 SHA 上通过 PinnedGit base `11/11`、三个 optional profile 各 `12/12`，requested/resolved SHA 一致，四档 package tree 均为 `04fe8219728c654a3b60d13077d837ea4d28cd1c39ee1cc66209503f601ebe88`；完整哈希见 [e2-matrix.md](../Evidence/UPM-P3/e2-matrix.md)；
- P3 已 `Complete`；P4 只进入 `Ready`，尚未开始 Demo/Samples 迁移。

## 13. 移交 UPM-P4

向 P4 移交：稳定的 package-test/repository-test 边界、可运行的 Consumer testables fixture、测试依赖 owner、保留在 package 内的最小 contract 清单、repository Demo/scene 测试关联，以及尚未解决的 sample 候选依赖。P4 选择 Sample 时必须复用这些边界，不能把 repository test fixture 当作 consumer-ready sample。
