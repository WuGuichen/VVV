# CharacterTest Skill Showcase

> Status: Guide
>
CharacterTest Skill Showcase 是用于预览真实 CharacterTest 技能链路的 Play Mode 场景。它和 `MxVfxShowcase` 分工不同：VFX Showcase 只看资源和表现 prefab；Skill Showcase 会启动一个独立的 CharacterTest runtime，技能释放、动作时间轴、Projectile / HitResolve、Buff、冷却、VFX 和 telemetry 都走现有 gameplay 链路。

## 入口

- 场景：`Assets/Scenes/CharacterTestSkillShowcase.unity`
- 重新生成菜单：`MxFramework / CharacterTest / Generate Skill Showcase Scene`
- 运行时脚本：`Assets/Scripts/WGameFrameworkProject/CharacterTest/Showcase/CharacterTestSkillShowcaseBootstrap.cs`
- HUD：`Assets/UI/MxFramework/CharacterTestSkillShowcase/CharacterTestSkillShowcaseHud.uxml` / `.uss`

## 覆盖能力

- 技能列表来自 CharacterTest `AbilityDefinitionRegistry`，不是手写清单。
- 近期技能默认置顶：Beam Lance、Channel Beam、Laser Lance、Chain Lightning、Poison Bottle；其余按 ability catalog 顺序展示。
- 播放时把当前技能写入预览 runtime 的专用 slot，再通过现有 `CastAbilitySlot` command 释放，不写玩家持久热栏配置。
- 场景内生成 caster、多个 hostile target、可选 friendly target，并使用真实角色视图和已有 VFX/resource catalog。
- UI Toolkit 面板提供搜索、滚动列表、Play、Replay、Stop、Step Frame、Loop、Reset Actors、目标数量、目标距离、目标间距、锁定目标、友方目标、背景色和 orbit stage 视角。
- Telemetry 显示 runtime frame、冷却、锁定目标、活动 VFX 数、角色 HP、最近伤害以及 `STOP` / `POIS` 等状态。

## 手测流程

1. 如需重建场景，执行 `MxFramework / CharacterTest / Generate Skill Showcase Scene`。
2. 打开 `Assets/Scenes/CharacterTestSkillShowcase.unity`，直接 Play。
3. 在左侧搜索 `beam`、`laser`、`chain` 或 `poison`，选择技能。
4. 调整 Targets、Distance、Spacing、Lock Target 和 Friendly Target，点击 `Replay`。
5. 在舞台区域拖拽旋转视角，双击或右键重置视角。
6. 观察右侧 telemetry，确认命中数量、伤害、冷却和 Buff 状态符合对应技能规则。

## 验收清单

- 场景包含 Main Camera、Directional Light、`CharacterTestSkillShowcaseRoot`、`UIDocument`、`CharacterTestSkillShowcaseBootstrap` 和大平台。
- UI 能搜索并滚动选择技能，且包含 `910014`、`910015`、`910016`、`910017`、`910018`。
- Replay 会重置预览 runtime 后释放技能，不污染玩家持久热栏。
- Beam Lance、Chain Lightning、Poison Bottle 在场景中能走真实 action/cooldown/hit/VFX/Buff 路径。
- 调整目标数量和友方目标后，hostile-only、命中上限和范围规则仍由现有 CharacterTest runtime 决定。

## 自动验证

- `CharacterTestSkillShowcaseTests.SceneGeneratorSmoke_BindsUiStageProfileAndBootstrap`
- `CharacterTestSkillShowcaseTests.SkillShowcase_BuildsRecentSkillChoicesAndReplaysThroughPreviewSlot`
- `CharacterTestSkillShowcaseTests.SkillShowcaseScene_PlayModeReplaysRepresentativeAbilities`

这些测试只覆盖 scene generator、UI 绑定、技能列表、预览 slot 和代表技能 replay smoke。具体技能数值仍由各自 CharacterTest focused tests 负责。
