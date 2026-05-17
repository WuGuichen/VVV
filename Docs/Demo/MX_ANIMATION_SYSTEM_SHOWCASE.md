# MxAnimation System Showcase

MxAnimation System Showcase 是动画系统第二、三阶段能力的可视化手测入口。它不是新的运行时核心，只负责把已有能力编排到同一个可观察场景中。

## 入口

- 场景：`Assets/Scenes/MxAnimationSystemShowcase.unity`
- 重新生成菜单：`MxFramework / MxAnimation / Generate System Showcase Scene`
- 运行时脚本：`Assets/Scripts/MxFramework/Demo/MxAnimationShowcase/MxAnimationShowcaseDemoBootstrap.cs`
- HUD：`Assets/UI/MxFramework/MxAnimationShowcase/MxAnimationShowcaseHud.uxml` / `.uss`

## 覆盖能力

- 1D locomotion blend：idle / walk / run speed parameter。
- 2D directional blend：forward / back / left / right directional parameter。
- Layer + AvatarMask：upper body attack layer weight 和 mask 状态。
- Combat bridge：`CombatMxAnimationUnityBridge` 从 Combat action 触发表现动画和 presentation event。
- Resource warmup：`ResourceCatalog`、`ResourceManager`、`MxAnimationWarmupService`、package validation。
- Compatibility validation：clip / mask / skeleton compatibility profile。
- Bake artifact：显示 bake report hash 和导入状态。
- Mod override / fallback diagnostics：演示 mapping override 合并和缺失 clip 请求诊断。
- Playable cache diagnostics：展示 cache hit/miss、resident、cached、active playable 数。

## 手测流程

1. 如需重建场景，执行 `MxFramework / MxAnimation / Generate System Showcase Scene`。
2. 打开 `Assets/Scenes/MxAnimationSystemShowcase.unity`，直接 Play。
3. 默认 auto cycle 会持续切换 1D locomotion speed、2D direction 参数，并周期性触发 upper-body action。
4. 按 `I` / `O` / `P` 手动设置 1D idle / walk / run。
5. 按 `WASD` 或方向键驱动 2D directional blend。
6. 按 `Space` 触发 `CombatMxAnimationUnityBridge` action，观察 upper layer weight、AvatarMask 状态和 presentation event log。
7. 按 `M` 播放经过 `MxAnimationModOverrideMerger` 合并后的 override binding。
8. 按 `F` 请求缺失 clip，观察 fallback / recent request diagnostics。
9. 按 `H` 切换 auto cycle，按 `R` 重置。

## 验收清单

- 四个可见 actor 都必须是真实 Skeleton 模型，不是几何体替身。
- HUD 中 `Package`、`Compatibility`、`Bake`、`Warmup` 应显示 OK / ready，不应出现初始化错误。
- `Resources` 的 failed count 应为 0。
- `Cache` 的 hit/miss/resident/cached/active 会随切换变化。
- 所有 clip、AvatarMask、bake report、compatibility profile 都通过 `ResourceCatalog` / `ResourceManager` / `MxAnimationWarmupService` 进入场景，不从 backend 直接读取 Unity object。
- Mod override 只改变表现 mapping 和 package expectation，不修改 Combat hit、damage、cancel、invulnerability 或 authority。

## 自动验证

- `MxAnimationShowcaseDemoTests.ShowcaseScene_BindsFormalResourceReferencesAndUi`
- `MxAnimationShowcaseDemoTests.ShowcaseScene_InitializesActorsBackendWarmupAndHud`

这两个测试覆盖场景引用完整性、正式资源加载、warmup、mod override、HUD 绑定和 runtime 初始化合同。视觉表现仍以本文件的手测流程为准。
