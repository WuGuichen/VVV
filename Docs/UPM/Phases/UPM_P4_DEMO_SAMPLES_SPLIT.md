# UPM-P4 Demo 与 Sample 所有权契约

> Status: Current
> Task State: Complete
> 2026-10-05：完成阶段保留的现行所有权与验证规则；旧执行流水从活动文档移除。本次只核对目录、manifest 与链接，未重新执行 Consumer／Unity 验收。

## 当前所有权

- 项目 Demo 位于 `Assets/Scripts/WGameFrameworkProject/Demos/`；既有 Scene、Config、VFX、Bundles、UI、FGUIProject 等输入继续由项目拥有，不复制进包的 sample。
- `package.json` 只登记一个 sample：`Samples~/RuntimeHostMinimalLifecycle`；当前正式物理根为 `Packages/com.vincent.wgameframework/Samples~/RuntimeHostMinimalLifecycle/`。它验证真实 RuntimeHost 的 Initialize → Start → Tick → Stop → Dispose 生命周期，不替代 RuntimeVerticalSlice 的 Entity → Ability → Target → Effect 闭环或主工程验证。
- `MxFramework.Demo`、`MxFramework.Demo.Editor`、`MxFramework.Demo.FairyGUI` 的程序集身份保持项目归属；package assembly 不得反向引用它们。sample assembly 是 `package-sample`，项目 Demo 是 `repository-validation`，每项只有一个 owner。
- 移动已有 Unity 内容必须保留文件／文件夹 `.meta`、GUID、asmdef identity 与序列化引用，不借迁移重构 Demo 语义。历史逐文件映射可读[冻结 inventory](../Evidence/UPM-P4/demo-sample-inventory.json)；旧开发根不是运行时 fallback。

## Sample 输入与副作用

- 仅通过 Package Manager 显式 Import 进入 Consumer；不自动修改 ProjectSettings，不覆盖 Consumer 文件。必要 setup 必须显式触发、展示目标与影响，并可撤销。
- sample glue 只调用真实 package API，不复制 Runtime 实现；运行所需非包输入由 sample 自有，逐项维护代码及场景、Prefab、纹理、音频、字体等来源／许可，没有第三方输入也须明确记录。
- base Consumer 未安装 optional package 时仍能编译运行。禁止依赖 CharacterTest、主工程 `Assets/Config`／`Assets/VFX`／`Assets/Bundles`、`FGUIProject`、`Tools`、FMOD bank 或其它 optional integration；不从主工程偷取资产来使 smoke 通过。
- 增加第二个 sample、把 CharacterTest 包装成 sample、提升 optional support tier 或扩大发布范围，均不属于已完成 P4 的默认后续动作。

## 后续变更的验证规则

1. 唯一 owner、asmdef／manifest／来源清单、依赖闭包与 E0 检查通过，package → project Demo 反向依赖为零。
2. 迁移前后 GUID／asmdef identity 与序列化引用一致；主工程受影响 Demo 的原验证能力保留。
3. 干净 Local Consumer 明确 Import、编译、打开场景、运行冻结生命周期并完成对象／订阅清理，日志无异常；base absence 成立。
4. 同一完整 candidate SHA 执行 PinnedGit base／sample import-run；仅对被触及的 adapter 加 matching positive profile，不拼接不同候选结果。
5. mutation guard、sample tree hash、差异检查通过，报告保存 candidate identity、测试 full names、预期副作用与来源审计。

GUID／引用失败时按冻结清单恢复带 `.meta` 的原内容，不重新分配 GUID；Demo 能力下降只回滚对应切片。出现隐式写入或 Consumer 异常时阻断候选，修复后重新获取证据，不能沿用失败／回滚前的通过结果。

当前活动数量、readiness gap、证据新鲜度与发布状态统一读取[UPM 当前状态](../../UPM_CURRENT_STATUS.md)、[阶段边界](../../UPM_PHASE_TASKS.md)、[包表面规范](../../UPM_PACKAGE_SURFACE_LAYOUT_OPTIMIZATION.md)、[Consumer 证据规则](../../UPM_CONSUMER_BASELINE_EVIDENCE_OPTIMIZATION.md)与[可选集成规则](../../UPM_OPTIONAL_INTEGRATION_OPTIMIZATION.md)。P4 历史不可变门禁见[同候选 sample matrix](../Evidence/UPM-P4/e2-sample-matrix.md)，不把该历史回执写成当前候选已验收。
