# MxFramework VFX Showcase

> Status: Guide
>
MxFramework VFX Showcase 是用于集中浏览、重播和检查现有 VFX prefab 的 Play Mode 场景。它不是新的 VFX 编辑器，也不新增通用 VFX runtime API；它只把 `VfxDefinition`、`ResourceCatalog` 和 Unity presenter 编排成一个可观察入口。

## 入口

- 场景：`Assets/Scenes/MxVfxShowcase.unity`
- 重新生成菜单：`MxFramework / VFX / Generate VFX Showcase Scene`
- 运行时脚本：`Assets/Scripts/WGameFrameworkProject/Demos/VfxShowcase/VfxShowcaseDemoBootstrap.cs`
- HUD：`Assets/UI/MxFramework/VfxShowcase/VfxShowcaseHud.uxml` / `.uss`

## 覆盖能力

- RuntimeDefinition：`VfxDefinitionRegistry` 中的正式定义通过 `VfxRuntimeModule`、`VfxRuntimeEventFanout` 和 `UnityVfxPresenter` 播放。
- Catalog Prefab：只进入 `ResourceCatalog` 的 `domain.vfx` prefab 通过 `ResourceManager` + `MemoryResourceProvider` 直接实例化播放。
- 自动列表：从 `TempImportedResourceCatalog` 的 `domain.vfx` 条目构建展示列表，不维护手写 VFX 清单。
- 参数化预览：Laser / Beam / Chain arc 等路径类效果使用 length slider，把长度参数传入 `VfxParameterBlock.Value0`，prefab 不需要烘焙固定长度。
- 场景锚点：muzzle、target、endpoint、ground marker 用于 charge、impact、pool、beam/laser/arc 的统一预览位置。
- UI Toolkit：搜索 resource id / stable id / label，按 Magic、Status Aura、Projectile Flight、Beam/Laser、Area、Poison 等分组滚动浏览。

## 手测流程

1. 如需重建场景，执行 `MxFramework / VFX / Generate VFX Showcase Scene`。
2. 打开 `Assets/Scenes/MxVfxShowcase.unity`，直接 Play。
3. 在左侧列表搜索 `fireball`、`laser`、`chain`、`poison` 或 `status_aura`。
4. 点击条目或使用 `Play` / `Replay` 播放当前 VFX。
5. 对 `vfx.magic.laser_lance.laser`、`vfx.magic.chain_lightning.arc`、beam 类效果调整 `Length`，观察路径长度变化。
6. 对 impact、status aura、pool/zone 类效果调整 `Target Distance`，观察 target/ground 预览锚点。
7. 使用 `Stop/Clear` 清理当前播放对象；`Loop` 重复当前条目，`Auto Advance` 自动切换当前筛选结果。

## 验收清单

- 场景包含 Main Camera、Directional Light、`MxVfxShowcaseRoot`、`UIDocument` 和 `VfxShowcaseDemoBootstrap`。
- `VfxShowcaseDemoBootstrap` 的 serialized VFX refs 覆盖所有 `domain.vfx` GameObject catalog 条目。
- Fireball、Laser path、Chain Lightning arc、Poison pool、Status aura 均能从列表选择并重播。
- RuntimeDefinition 与 Catalog Prefab 都能播放，且清理后不保留活动实例。
- length / target distance slider 会更新详情面板参数并影响路径类效果预览。

## 自动验证

- `VfxShowcaseDemoTests.Catalog_SeparatesRuntimeDefinitionsFromCatalogPrefabs`
- `VfxShowcaseDemoTests.SceneGeneratorSmoke_BindsUiStageAndSerializedVfxRefs`
- `VfxShowcaseDemoTests.PlaybackController_ReplaysDefinitionCatalogPrefabAndLengthParameterizedArc`
- `VfxShowcaseDemoTests.Scene_PlayModeInitializesListAndReplaysRepresentativeVfx`

这些测试覆盖 catalog 分类、场景引用、UXML 控件、serialized prefab refs、RuntimeDefinition 播放、Catalog Prefab 播放和 PlayMode 初始化。具体粒子观感仍以手测流程为准。

## Target Design：语义化预览

VFX 美术资产语义、Primitive/Recipe、Published Effect、预算和 Development Agent 管理的完整目标设计见 `Docs/VFX_ASSET_AUTHORING_DESIGN.md`。当前已经完成第一轮只读语义消费：

- 从 VFX Authoring Catalog 读取 role、theme、backend、Anchor、review status 和 quality，并按首个 role 动态分组。
- 搜索会匹配语义字段和 tags；详情显示有序 Recipe layer → Primitive 组合树、fallback 和 budget。
- Primitive 参数合同中使用 `RuntimeSlot0..3` transport 的 Float 参数会自动生成 slider 并写入对应 `VfxParameterBlock` slot；StaticBake/PreviewOnly 参数不会生成运行时控件。
- 仍保留 ResourceKey 名称启发式 fallback，防止未来新增但尚未同步 Catalog 的资源让预览入口失效；当前 37 个 direct VFX 已全部有语义覆盖。

Effect `preview` preset 现在会自动设置 target/path distance 和 camera scale。37 张缩略图及 dHash 感知候选 evidence 可由独立 Unity 生成器写入 Temp；UI Toolkit `MxFramework/VFX/VFX Resource Library` 负责资源卡片、隔离实时预览、组合/依赖/评审工作区和受控 Catalog 草稿，Showcase 继续作为独立场景播放与验收入口。

Showcase 的长期职责仍是消费、播放和验收；它不写 Authoring Catalog、不批准艺术质量、不生成 gameplay 规则，也不替代 CharacterTest Skill Showcase 的真实技能链验证。

### Resource Library / 创作实验室边界

`MxFramework / VFX / VFX Resource Library` 现在提供“资源库 / 创作实验室”双模式：资源库浏览 Effect、Recipe、Primitive 及其依赖；创作实验室从 Creative Brief 先做复用检索，再通过正式 Editor executor 生成隔离 Draft 候选。Fireball 起始模板覆盖 Charge → Flight → Impact，并为每阶段生成六个 Round 0 方向。

- Resource Library 内的 Preview Scene 用于单资源预览，不要求打开 Showcase 场景，也不得修改当前编辑场景。创作实验室当前只把每个方向最新的真实单帧捕获显示在候选卡片上；同步播放和分屏动态比较尚未接入，相关控件保持禁用。
- 创作实验室只有在一个 Authoring Core 阶段的全部 Unity 候选都取得有效本地回执后，才导出该阶段固定的 Core manifest/receipt；随后可显式生成完整质量证据。当前目标平台性能 Provider 为 `NotMeasured`，所以证据会正常落盘但门禁保持失败，不能借此晋升。
- Candidate Sandbox、相机、收藏、比较播放状态、临时参数和评分 evidence 都不是 Showcase、Catalog 或 Runtime authority。
- 候选只有拿到 generation receipt、完整 evaluation report 和人工选择后才能生成 `ReviewNeeded` 晋升计划；UI 不提供自动 Ready。
- Showcase 只消费已经注册的 Published Effect/ResourceKey/Definition。未晋升 Draft 不进入 Showcase 列表，创作实验室也不能伪造 RuntimeDefinition。
