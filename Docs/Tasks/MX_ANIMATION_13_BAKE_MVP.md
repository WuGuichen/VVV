# MxAnimation 13 - Bake MVP

> 来源：Gitea Issue #112
> 状态：Implemented

## Scope

- 在 `MxFramework.Animation` noEngine 层定义 bake profile、artifact、fixed-frame weapon/root/event reference、hash、quantization 和 validation diagnostics。
- 在 `MxFramework.Editor.Animation` 提供 `MxFramework/MxAnimation/Bake Selected Animation Clip MVP` 菜单入口，从选中的 `AnimationClip` 采样曲线和 events，生成 `.mxbake.txt` 派生 artifact 报告。
- 在 `MxFramework.Combat` noEngine 层增加 `CombatBakedWeaponTraceAdapter`，将 baked reference + runtime weapon profile + character scale 合成为现有 `WeaponTraceFrame` / `CombatCapsuleQuery`。

## Boundaries

- Bake output 是派生缓存 / authoring artifact，不是唯一事实来源。
- Runtime damage、movement、cancel、Replay hash、network sync 不读取 Unity Animator、PlayableGraph 或 bone pose 当前状态。
- source clip hash、bake profile hash、skeleton/avatar profile hash 和 artifact hash mismatch 必须产生明确 diagnostics，不能静默使用过期 artifact。
- 动态武器、角色尺寸、socket offset、weapon radius/length 等必须作为显式 runtime profile 输入参与最终 query 计算。
- IK / 动态骨骼默认只影响表现；如果要影响权威命中，必须先建模为 Combat 可复现输入或确定性 solver 数据。

## Out Of Scope

- 不实现完整 retargeting / skeleton compatibility matrix。
- 不实现 Timeline/Scrubber 全量预览。
- 不把 root motion 作为 runtime authority movement。
- 不实现 Addressable / Bundle / RemoteBundle bake 发布流水线。

## Validation

- `MxAnimationBakeTests` 覆盖 artifact hash 稳定性、source/profile/skeleton/artifact mismatch diagnostics、quantization policy 和 Editor clip curve/event bake。
- `MxAnimationBakeWeaponTraceAdapterTests` 覆盖 baked reference + runtime weapon profile + character scale 到 `WeaponTraceFrame` / `CombatCapsuleQuery` 的确定性结果。
- noEngine boundary scan 保持 `MxFramework.Animation` / `MxFramework.Combat` 不引用 UnityEngine / UnityEditor。
