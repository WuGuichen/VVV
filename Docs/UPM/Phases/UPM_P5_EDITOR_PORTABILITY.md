# UPM-P5 Editor 所有权与可移植性契约

> Status: Current
> Task State: Complete
> 2026-10-05：完成阶段保留的现行归属、能力与副作用规则；本次核对项目目录和 canonical locator，不重新执行 Unity／Consumer 验收。

## 当前所有权

| 项目专属能力 | 项目 owner |
| --- | --- |
| Combat Editor | `Assets/Scripts/WGameFrameworkProject/Editor/Combat/`；`MxFramework.Combat.Editor` identity 保持 |
| VFX Graph Demo Editor | `Assets/Scripts/WGameFrameworkProject/Demos/Editor/Optional/VfxGraph/`；`MxFramework.Vfx.Unity.VfxGraph.Editor` identity 保持 |
| CharacterTest VFX Prefab builders | `Assets/Scripts/WGameFrameworkProject/Editor/CharacterTestVfx/` |
| 项目 FairyGUI art slice／atlas 工具 | `Assets/Scripts/WGameFrameworkProject/Editor/FairyGUI/` |

依赖 CharacterTest、主工程布局、`Tools`、`FGUIProject`、项目 Config／Scene／VFX／Bundles 的实现由项目拥有，或通过明确 host capability/provider 接入；package 不反向引用项目 Editor assembly。外部 Authoring CLI 继续在 Unity Editor 外。逐文件 GUID／内容映射见[冻结 owner manifest](../Evidence/UPM-P5/editor-owner-move-manifest.json)与[portability inventory](../Inventories/UPM_P5_EDITOR_PORTABILITY.csv)，不能为迁移重新生成身份。

`MxFrameworkPackageAssetLocator` 是包资源根解析入口；当前 canonical root 为 `Packages/com.vincent.wgameframework`，缺包时返回 unavailable。旧 P5 的单一 development fallback 仅是 P6 cutover 之前的过渡约束，当前源码已移除，不能作为现行要求恢复 `Assets/Scripts/MxFramework` fallback。通用 CharacterAction、attachment、timeline 等资源通过 locator 解析，不绑定开发工作目录。

## 可移植能力与副作用

- 每个 Editor assembly／initializer／菜单／窗口有唯一 owner，activation、requiredInputs、missingInputBehavior、writes 与 optional gate 须在 surface inventory 中精确分类。
- capability absent 时返回稳定 unavailable／empty 或禁用入口；缺少未启用的项目数据不属于损坏，不应 exception、`LogError` 或假成功。已经明确启用且声明必需的数据损坏可以 fail closed。
- import、domain reload、菜单 discovery、窗口创建与无配置 play-mode transition 不隐式创建／覆盖 Consumer Assets 或修改 ProjectSettings；不依赖当前工作目录。
- 写入只能由用户显式命令触发，展示输入、目标、影响并具备冲突保护与撤销／恢复；明确 Temp 请求由其会话拥有。不能自动覆盖未知文件，也不能把 `Tools/` 当成包内容。
- VFX Published Binding 的 reload bootstrap 只做 discovery，不自动生成／重写投影；缺项目 catalog 时 Resource Library 提供稳定 unavailable workspace。Rendering 默认 renderer 菜单缺项目资产 capability 时禁用，显式路径工具保持可复用。
- optional 类型不得泄漏到 base assembly／public API；采用 gated assembly、version define 或明确的低频 capability bridge，不用高频反射补救依赖污染。Runtime 不引用 UnityEditor，noEngine 不引用 UnityEngine。

## 后续变更的验证规则

1. E0／initializer classification、project-path、optional-source、反向依赖闭包、写入点与 API 边界检查通过；GUID／asmdef identity 与序列化引用一致。
2. 主工程受影响菜单、窗口与 Authoring 正常；干净 Consumer 覆盖 import／reload／discovery／窗口／play-transition，日志无未解释异常。
3. 分别缺 project data、Tools、FGUIProject 与 optional package 时稳定降级；启用能力的 positive profile 单独验证，不把 optional 变成 base 前提。
4. Consumer 文件与 ProjectSettings snapshot 无隐式变化；显式写入另测确认、冲突保护、撤销与回滚。
5. 同候选 Local／PinnedGit base 与 matching profiles、mutation guard 和差异检查通过；保存 candidate/tree hash 与测试 full names，不沿用历史通过结果。

不能安全降级的实现先迁为 project-only；出现隐式写入或身份错误时阻断候选，恢复对应快照／带 `.meta` 的内容后重验。不能用 catch-and-ignore、降低日志级别或复制主工程数据掩盖失败，也不为 Editor 搬迁新增公共 Runtime API。

活动数量、cutover 和 readiness 状态统一读取[UPM 当前状态](../../UPM_CURRENT_STATUS.md)、[阶段边界](../../UPM_PHASE_TASKS.md)、[包表面规范](../../UPM_PACKAGE_SURFACE_LAYOUT_OPTIMIZATION.md)、[Consumer 证据规则](../../UPM_CONSUMER_BASELINE_EVIDENCE_OPTIMIZATION.md)、[可选集成规则](../../UPM_OPTIONAL_INTEGRATION_OPTIMIZATION.md)与[拓扑 ADR](../../Decisions/ADR-015-upm-embedded-package-topology.md)。P5 历史不可变门禁见[同候选 matrix](../Evidence/UPM-P5/e2-matrix.md)；它不代表当前候选重新通过。
