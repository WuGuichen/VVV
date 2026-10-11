# VFX 美术资产创作与语义管理设计

> 当前通用手动组装、Development Agent 创作任务和人工晋升流程见 [VFX Development Agent 执行规范](VFX_DEVELOPMENT_AGENT_EXECUTION.md)。语义属性、Component/Element/Shader Profile、Composition Draft v3 与精确重定时的完整开发合同见 [VFX 语义属性管理与精确重定时开发设计](VFX_SEMANTIC_PROPERTY_RETIMING_DESIGN.md)。正式创作不再由程序 Provider 生成；程序 Provider 仅保留为测试夹具。

> Status: Design
>
> 本文定义 MxFramework 的 VFX 美术资产语义、复用、发布、预览和 Development Agent 管理边界。当前 Runtime 事实仍以 `Docs/Interfaces/Vfx.md`、源码和测试为准；本文中的 Authoring Catalog、Recipe、validator、VFX Graph adapter 和自动预览能力在实现并同步 Current 文档前均属于目标设计。

## 1. 背景与当前基线

项目已经具备以下运行时和预览基础：

- `VfxDefinitionRegistry`、`VfxRuntimeModule`、独立 command buffer、runtime event、hash 和 SaveState。
- Gameplay、Combat、CharacterAction、projectile 与 Unity 表现之间的 VFX bridge。
- `UnityVfxPresenter` 对 prefab、`ParticleSystem[]`、预热 pool、spawn、stop 和 anchor follow 的支持。
- `Assets/Scenes/MxVfxShowcase.unity` 对 RuntimeDefinition 与 `domain.vfx` Catalog Prefab 的集中预览。
- `Assets/Scenes/CharacterTestSkillShowcase.unity` 对真实 Ability -> CharacterAction -> Combat/Buff -> VFX 链路的验证。
- `com.unity.visualeffectgraph` 17.5.0 已安装，并与 URP 17.5.0 对齐。

当前缺口属于 Authoring 和资产治理，而不是基础播放能力：

- 资源分类仍主要依赖 ResourceKey、目录和名称推断，缺少稳定的用途、锚点、生命周期和参数语义。
- 相似效果通常以完整 prefab 出现，没有统一表达可复用 Primitive、Recipe 和主题覆盖。
- Resource Catalog 负责加载事实，`VfxDefinition` 负责 Runtime 定义，但两者都不应承担完整美术语义。
- Showcase 能播放资源，但不能解释组合结构、质量 fallback、预算或评审状态。
- VFX Graph package 已安装，可选 adapter 与首个 package-template `.vfx` 技术样板已经接入；该样板仍为 `ReviewNeeded` 并保留 ParticleSystem fallback，不能将“技术链路可用”描述为“Graph 资源已经通过美术验收”。

### 1.1 Current Implementation Status（2026-07-12）

第一阶段基础合同已经落地：

- `Assets/Config/MxFramework/Vfx/vfx_authoring_catalog.json` 是当前项目级 VFX Authoring truth，当前 37/37 direct VFX 已覆盖并启用 `strictCoverage=true`；覆盖不等于艺术质量 Ready。
- `VfxAuthoringCatalogDocument`、Primitive/Recipe/Effect DTO、受控词表、evidence、load result 和 validation report 位于 `MxFramework.Authoring.Core`，全部是 Authoring-only `LowFreqAlloc` 合同。
- `VfxAuthoringCatalogJson` 支持可恢复 JSON/file load、camelCase 稳定序列化；`VfxAuthoringCatalogNormalizer` 负责 null collection、路径、文本和稳定顺序归一化。
- `VfxAuthoringCatalogValidator` 已实现 `VFXAUTH001`..`VFXAUTH014` 的 schema、stable id、Recipe 依赖/循环、参数类型与范围、Recipe/Primitive backend/Anchor 兼容性、Anchor/lifecycle、ResourceKey/Definition evidence、VFX Graph fallback、preview、stale、coverage、审批证据和 Ready 传递依赖校验；Ready 的 stale 输入会升级为阻断错误。
- 初始 Catalog 曾只登记 Fireball、Laser Lance path、Chain Lightning arc、Poison Bottle pool 和 Burn status aura 五个 `ReviewNeeded` 样板；其余 32 个 direct VFX 后续已以 `ReviewNeeded` coverage candidate 导入。
- `VfxAuthoringProjectScanner` 已扫描 Prefab/VFX Graph 路径、Resource Catalog、生成的 VFX Definition 和 Graph package availability，并输出不含时间戳与绝对路径的稳定 evidence。
- `VfxAuthoringReportWriter` 已生成稳定 JSON/Markdown report；当前扫描识别 37 个直接 VFX 资源并全部覆盖，自动导入项仍保留人工迁移与评审责任。
- `VfxAuthoringOperationPlanner` 已提供 catalog-only、expected-hash guarded dry-run save plan，使用 `VFXOP001`..`VFXOP005` 报告可执行、校验阻断、stale hash、无变化与 v1 只读迁移状态，不写 Unity 资产。
- CLI 已提供 `vfx validate` 与 `vfx plan-save`；`MxFramework.Authoring.Tests` 覆盖 JSON roundtrip、无效 JSON、跨类型重复 id、Recipe cycle、参数绑定、VFX Graph fallback、strict coverage、stale Ready gate、scanner/report、dry-run plan、CLI 和仓库种子 Catalog。
- `MxVfxShowcase` 已以只读方式消费 Authoring Catalog：覆盖项按首个 role 分组，搜索包含 role/theme/anchor/backend/review/tags，详情展示 Recipe 组合摘要、预算与 fallback；未覆盖资源继续使用名称分类 fallback。该 sidecar 不替代 Resource Catalog 或 Runtime Definition。
- scanner 已为 `Assets/VFX` 下 Prefab 生成稳定、项目相对的组件结构 fingerprint，并按组件数量与材质引用数量形成只读复用候选组；当前 46 个 Prefab 形成 6 组候选。候选必须继续检查 role、外观、参数和依赖，不能自动等同为可合并资产。
- Authoring Core 已提供 Recipe bake input hash 和 `VfxAuthoringBakePlanner`：hash 覆盖 baker id/version、Recipe 依赖图、有序 layers、Primitive 技术/参数合同、layer bindings/overrides 与 Effect overrides；`VFXBAKE001`..`VFXBAKE005` 区分缺少 Recipe、缺少发布路径、校验阻断、需要 Unity 写入和产物已同步。`vfx plan-bake` 只输出计划，不写 Prefab。
- `VfxAuthoringRecipePrefabBaker` 已提供 ParticleSystem baker v1：通过 Unity `PrefabUtility` 按 Recipe 顺序嵌套 Primitive Prefab，支持受控 `active`、`localPosition`、`localEulerAngles`、`local_scale` 静态 override，拒绝未知 override、未解析 bindings 和非 ParticleSystem backend；保存完整 Published Prefab，并在 `Assets/MxFrameworkGenerated/Vfx/AuthoringBakeEvidence/` 写派生 evidence。
- 首个真实样板为 `vfx.primitive.projectile.fire_core` → `vfx.recipe.projectile.fire_core.large` → `vfx.magic.fireball_burst.flight`。`Fireball_Burst.prefab` 现在嵌套 `Fireball.prefab` 共享核心并应用 `local_scale=1.15`；evidence 与 `sha256:eac9...60a9` 一致，`vfx plan-bake` 返回 `VFXBAKE005`。
- 可选 `MxFramework.Vfx.Unity.VfxGraph` adapter 已落地：独立 asmdef 通过 VFX Graph package version define 启用，`UnityVfxGraphController` 缓存 Exposed Property id，把现有 `VfxParameterBlock.Value0..3` 映射为 Float/Int/UInt/Bool，池化复用时执行 `Stop/Reinit/Play`。`UnityVfxPresenter` 只依赖通用 backend controller 接口；Graph asset、compute shader 或图形设备不可用时继续播放 Prefab 内 ParticleSystem fallback，Graph 可用且配置允许时抑制该 fallback。
- 成熟治理已启用：Catalog 现在覆盖全部 37 个 direct `domain.vfx` Effect 并设置 `strictCoverage=true`；自动迁移项保持 `ReviewNeeded`。稳定报告输出 100% semantic coverage、2.7% Recipe-backed Effect rate、100% shared Primitive reuse rate、80.4% structural duplicate-candidate asset rate、2 组 exact preview-hash candidates。
- `VfxAuthoringThumbnailGenerator` 通过 Unity `PreviewRenderUtility` 在 `Temp/MxFramework/VfxAuthoring/Thumbnails/` 生成 37/37 个 256x256 PNG 与稳定 content-hash manifest；scanner 将其作为可重建 evidence。静态组件并发成本门禁按 Low/Medium/High tier 检查 renderer/ParticleSystem/LineRenderer/VisualEffect/custom view 与推荐并发量，当前 0 finding。
- `vfx search` 已提供 role/theme/Anchor/backend/review/text 的稳定查询和 Effect → Recipe → Primitive 复用优先级；`vfx impact` 在替换/合并/删除前列出 Recipe consumers、Effect consumers、fallback consumers、ResourceKey 和 Definition binding。两者只读，不自动删除、合并或改变 `Ready` 状态。
- `MxVfxShowcase` 现在展示完整有序 Recipe layer → Primitive 树，并按 Primitive 参数合同为 `RuntimeSlot0..3` 自动生成 slider；`StaticBake`、`PreviewOnly` 和 backend-default 参数不会错误写入 Runtime slot。当前首个 Recipe 的 `local_scale` 是 StaticBake，因此只显示在组合树而不生成运行时控件。
- Effect `preview` preset 会驱动 Showcase 的 target/path distance 与 camera scale；实时播放仍使用现有 anchor resolver 和 Published Prefab，不把预览状态写入 Runtime authority。

当前还包括：UI Toolkit `MxFramework/VFX/VFX Resource Library` 三栏工作台，提供语义分类、虚拟化缩略图卡片、隔离实时预览、组合/参数/依赖/评审 tabs。评审控件只修改选中 Effect 的内存 view model，`Save Draft` 从不可变 Catalog 快照生成单 Effect 草稿与稳定字段 diff；Apply 先运行 authoritative `vfx plan-save` validation/dry-run 与 expected-hash guard，再显示人工确认，并在确认瞬间再次检查 hash，始终只允许写 Catalog。thumbnail manifest 生成 64-bit dHash，scanner 以 Hamming distance <= 8 输出感知相似候选。`vfx.magic.fireball_burst.impact` 是首个接入 `VisualEffect` controller 并保留 ParticleSystem fallback 的 `.vfx` 技术样板，仍为 `ReviewNeeded`。

尚未实现：更多 Primitive/Recipe 迁移、工作区更多语义字段的结构化编辑、远程审批服务，以及 Graph 样板的人工美术验收。Development Agent 的只读搜索/影响分析已可用；ParticleSystem baker v1 当前只支持静态 transform/active override。所有自动覆盖条目仍需人工美术评审，strict coverage 只证明目录完整，不证明艺术质量 Ready。

### 1.2 Development Agent VFX 双向创作飞轮（2026-07-14）

当前实现把 Resource Library 扩展为“资源库 / 创作实验室”双模式，并将组合复用与基础效果生成放进同一套 Authoring-only 治理链路。该链路不扩展公共 Runtime API；所有类型均为 `LowFreqAlloc` 或 `AllocByDesign`，不进入 Tick、Runtime hash、SaveState、网络或 gameplay authority。

```text
CreativeBrief
  -> Ready exemplar / Effect / Recipe / Primitive 检索
  -> ReuseDecision
  -> GenerationPlan（Round 0 固定六方向）
  -> Unity Candidate Receipt
  -> EvaluationReport
  -> PromotionPlan / PromotionReceipt
  -> guarded Catalog / Resource / Definition registration plan
```

当前边界与实现状态如下：

- `vfx_authoring_catalog.json` 已迁移到 `mx.vfx.authoring.v2`。v1 仍可读取；v2 增加 `visualMaturity=LegacyPlaceholder|Candidate|Production`、`exemplar` 与绑定 asset/dependency、Recipe input、evaluation report、quality profile 的人工审批证据。现有 37 Effect、1 Recipe、1 Primitive 的 stable id 和运行时合同保持不变，全部明确为 `LegacyPlaceholder + ReviewNeeded`。语义覆盖为 37/37，Production 视觉覆盖为 0/37。
- Ready 依赖沿 Recipe/Primitive/fallback 传递：Ready Recipe 的传递依赖必须 Ready；Ready Effect 的 Recipe、Primitive 与 fallback 必须 Ready，Anchor、space、lifecycle 兼容。观测 hash 与审批 hash 不一致时审批即 stale。GameplayImportant/CriticalTelegraph 还要求 gameplay/UX 人工审批。
- `Assets/Config/MxFramework/Vfx/vfx_generation_templates.json` 是独立的生成模板事实源，保存本地优先 Provider、Style Profile、认证模板定义、17.5.0 实验 Graph adapter 版本约束和版本化质量阈值。只有人工 Ready 且显式 `exemplar=true` 的条目可进入正向 Exemplar Index；当前列表为空。
- noEngine Authoring Core 提供 `VfxReusePlanner`、`VfxGenerationPlanner`、`VfxPromotionPlanner`、published registration planner、稳定 JSON/hash 与 receipt validator。CLI 的 plan/report 命令不得直接写 Unity 序列化资产。
- Ready 校验与候选晋升都具有显式 authority verifier 边界。当前项目没有远程、签名或操作系统身份审批服务，production scanner/CLI 不注入 verifier，因此即使 JSON 中自填 reviewer、authority id 和 SHA-256 字段也会有意 fail-closed。未来只能由认证 host 集成校验外部 content-addressed receipt 后注入 verifier；不能在 Catalog 或普通计划字段中添加“跳过审批”开关。
- Scanner 只有在 EvaluationReport 自身 hash/plan/receipt binding 有效、目标 `stableId` 与 PromotionPlan 一致，且该 plan 的确切 candidate 同时 `HardGatesPassed=true` 与 `Shortlisted=true` 时，才把 report/profile 计为 Ready evidence。全失败、未入围、只提供 report 路径或未绑定 PromotionReceipt 的报告一律不可用于 Ready。
- 当前 Unity Editor executor 只接受白名单 `ParticleSystemPrimitiveSpec` 与 `LineRendererPrimitiveSpec`。Catalog 可以描述认证 Graph 模板和 17.5.0 实验 adapter，但本轮未提供安全的 Graph wrapper/exposed-property/fallback 生成器，因此两条 Graph 生成路径均 fail-closed；实验拓扑也禁止晋升。
- `mx.vfx.provider.procedural` 已从正式创作路径禁用，只保留测试夹具身份；其产物不得进入 receipt、评测、晋升或 Exemplar。缺少支持贴图时由 Development Agent 图像生成能力创建带 provenance 的候选 PNG/Flipbook；能力不可用时输出 `TextureBrief` 并阻断。
- Candidate Sandbox 的协议名称保留 `Assets/VFX/.mx-candidates/<sessionId>`，但 Unity 会忽略 `Assets` 下以点开头的目录，因此实际可导入目录固定为 Git 忽略的 `Assets/VFX/MxCandidates/<sessionId>`。所有路径都做 project-relative、sandbox ownership 与 traversal 校验。
- 晋升通过 `AssetDatabase.MoveAsset` 保留 GUID，并以 expected receipt hash、逐资产 content hash/GUID、目标冲突检查和事务 receipt 保护；失败执行逆序回滚，未完成事务可显式恢复。executor 不改 Catalog，也不能设置 Ready。
- Fireball 首个垂直切片只执行 Impact 六方向：`合同忠实 / 高辨识度 / 低预算 / 强冲击 / 主题变体 / 新 Primitive 探索`。Charge 与 Flight 在 Impact 人工选定方向后制作，避免一次生成 18 个彼此不协调的占位 Draft。
- UI Toolkit 创作实验室提供 Creative Brief、Style/Profile/Provider、复用建议、六方向候选状态和受控清理/评测/晋升入口。工作台读取通过校验的 Agent receipt 后支持候选实时播放与 A/B 比较；没有有效 receipt 时不以单帧截图伪装成动态预览。

### 1.3 Piloto Studio 原材料库与 Agent 组装（2026-07-14）

- `Assets/Piloto Studio` 是项目内长期保留、不可修改的 `LicensedReusable` 原材料库，不是可以绕过 Catalog 直接发布的完整特效目录。Source Package 分为 `piloto.super-realistic-fx-bundle`、`piloto.super-realistic-fx-bundle-02` 与共享支持资产包 `piloto.shared`；既有 11 个 Primitive 继续兼容原 package id。
- `mx.vfx.source-library.v1` 使用 Unity 深度扫描而非固定数量基线。文件层记录 Prefab、材质、贴图、Mesh、Shader、GUID、内容 hash 和依赖；Editor 层记录 GameObject 层级、ParticleSystem 模块/曲线/Gradient、Renderer、Transform、Simulation Space、时序与 component local fileID。稳定组件 ID 由 `source package + Prefab GUID + local fileID` 构成，Transform path 只用于展示。
- 可重建索引写入 Git 忽略且可跨 Editor 重启保留的 `Library/MxFramework/VfxSourceLibrary/index.json`；旧 `Temp/MxFramework/VfxSourceLibrary/index.json` 仅作为一次性兼容迁移输入。仓库保存 `Assets/Config/MxFramework/Vfx/vfx_source_curation.json` 与 `AuthoredElements/` 中的人工策展和手工派生 Element。版本化 Element 决定素材卡可见性，Library 深度索引只决定成员预览、语义编辑、源导入、提取与烘焙能力；索引缺失时卡片仍可浏览，深度操作必须 fail-closed 并提供显式重建入口。CLI 的 `source scan/search/plan-extract/validate-extraction/plan-compose` 只验证、搜索或生成计划，不写 Unity 资产。
- Primitive Prefab 保存所选 ParticleSystem 配置和相对 Transform。未修改的 Piloto 材质、贴图、Mesh 和 Shader 默认保持直接 GUID 引用；只有调色或渲染状态确需改变时才创建可追踪派生副本。提取器不复制 MonoBehaviour、演示控制器、Camera 或未知组件；职责层存在脚本依赖时 fail-closed，要求正式 adapter。
- ExtractionPlan/Receipt 必须绑定 Source Library hash、成员组件 ID、源 Prefab/content/dependency hash、依赖策略和 extractor 版本。Catalog 中 `LicensedThirdParty` Primitive 同样保存这些证据；任何源输入变化都会使关联 Primitive 与消费者 stale。相同技术指纹/成员集合只允许一个规范 Primitive，颜色差异优先由 Gradient、Color 或 Material Profile 表达。
- UI Toolkit 创作实验室内并列 `手动组装` 与 `Development Agent 创作`，不创建效果家族专用编辑器。手动组装采用“Element/源组件/Production Primitive → 隔离预览与 Layer Stack → Inspector/诊断/烘焙”三栏结构，支持检索、Solo、拖入、顺序、撤销/重做、Transform、时序、Emission/Lifetime/Size、Tint 和渲染顺序覆盖。单击 Element 保持独立浮动小窗预览；小窗可将当前模拟帧与当前镜头取景保存为本地 `icon.png`，素材卡即时刷新并在下次打开时优先使用该图标，原有多时间点自动捕获仍完整保留。选中后按 `S` 添加/移除独立 Editor-only 中央临时层，按 `A` 切换“仅组件 / 与 Layer Stack 一起播放”，用于检查组件自身或与当前组合、角色和 Socket 的共同表现。临时层和 `A` 的可见性状态不进入 Draft、Undo、预算、恢复日志或 Candidate，双击/拖拽才正式加入当前生命周期阶段。
- 手动组装采用“预览优先”垂直布局：中央 Preview 弹性占用全部剩余高度，Layer Stack 使用可拖动、可收起且会保存到 EditorPrefs 的独立高度；空间辅助和角色上下文是低频折叠抽屉。“最大化预览”只隐藏工作台左右栏、草稿工具条和 Layer Stack，不改变草稿、播放帧、相机、角色或 Layer 状态；“还原布局”恢复默认 260 px Layer Stack 和折叠上下文。
- `mx.vfx.source-curation.v2` 保留通用 `AutomaticCurated` 与 `DraftLocalOnly` 能力。当前项目使用 `ExplicitAuthored`：素材栏显示受版本控制的手工派生全局 Element、当前草稿本地 Element 和当前草稿实际引用的全局 Element，不自动生成精选目录。Element 是稳定来源描述而非项目 Prefab；固定 seed 多时间点预览属于可重建 evidence。
- 仓库内 `vfx_source_extraction_registry.json` 是防重复提取事实源。它把 Primitive、源 Component ID、技术指纹、等价来源簇、依赖 hash 和提取器版本绑定起来；exact 成员或等价技术指纹已登记时必须复用现有 Primitive，复合 Element 只提取未覆盖成员。只有具有稳定 override diff/hash 的材质或渲染 Variant 才允许产生新 Primitive。当前 11 个 Piloto Primitive、18 个直接源组件及等价簇已完成回填。
- Element 浏览采用正交分类而不是单一目录：快速分类包含职责、主题、家族、Anchor、形态、生命周期、质量档和提取状态；“更多分类”包含用途、运动方式、节奏阶段、模拟空间、Renderer Mode、粒子预算层级和单层/复合/Primitive 粒度。分类由 Source evidence 动态派生；工作台允许以受控选项修正组件显示名称、最多三项职责、主题、家族、最多两项用途、形态、Anchor、节奏、运动和质量档，并记录首选、弃用与策展备注。Simulation Space、Renderer Mode、Loop 和粒子预算仍是只读技术事实；修正只写 curation，不复制或改变 Piloto 资产。
- `mx.vfx.composition-draft.v3` 保存项目内有序五阶段组合草稿并绑定 Catalog/Source Index/Curation/Property Registry/Profile hash。Layer 使用 `Build/Rebuild/Loop/Complete/Dissipate`；`Loop` 必填并声明 `OneShot/Continuous`，其余阶段可为空。工作台在当前阶段顶部直接显示总帧数：自动模式取所有启用 Layer 的最晚艺术结束帧，手动模式写入阶段帧覆盖；阶段时间轴和播放终点使用同一帧合同，越界 Layer 只警告而不被隐式裁剪。v2 默认粗倍率草稿可事务迁移；非默认粗倍率必须人工转换。预览阶段只创建隔离实例；显式烘焙时才按需提取未落地 Element、编译语义属性并生成含五阶段根、`UnityVfxLifecycleController`、统一 Layer Scheduler 和可选属性轨道的完整 Candidate Prefab 与 v3 receipt。
- Draft 到真实技能的发布边界固定为 `Draft -> Consumer Contract variant -> Candidate -> LocalPlaytested -> ReviewNeeded -> ReadyApproved -> ProductionActive`。技能始终引用稳定 ResourceKey，不读取 Draft、Recipe 或 Candidate path。`vfx_published_bindings.json` 是当前正式 Prefab 绑定唯一事实源；`vfx_published_unity_bindings.asset` 只是 Unity 对象投影，Resource Catalog 继续拥有加载地址和 provider。
- 首个 consumer binding set 为 `vfx.skill.fireball_burst`：占位资源调整期的 Charge 绑定 action 0–29 共 30 帧，Flight 绑定最长 90 帧 Projectile，Impact 绑定 Definition 12002 的 30 帧；对应 Skill Action 临时统一为至少 90 帧。发布变体必须保持 Entry/Exit 绝对帧并通过语义重定时调整 Body、Emitter Window、Residence、周期和材质局部时间；Profile/Adapter 不足或最短艺术长度超限时 fail-closed，原始 Draft 不被隐式修改。
- 本机真实技能试玩由 Editor-only `Library/MxFramework/VfxPlaytestOverrides.json` 装饰生成 binding source。覆盖持久化到 Unity 重启后，但绑定 Candidate GUID/content、Composition/receipt 和 Consumer Contract hash；任一输入变化即失效。单槽覆盖用于迭代，三段覆盖用于标准评审；人工实战检查不自动授予 Ready。
- 人工实战检查后，工作台以 GUID-preserving `AssetDatabase.MoveAsset` 将单段 Candidate 晋升到 Review 目录，并只登记为 `Candidate + ReviewNeeded`。随后必须由用户在工作台显式确认 Ready，生成独立 `mx.vfx.ready-approval.v1` 回执；发布计划重新校验回执、当前 Prefab、Profile/Composition/Preview Evidence 与 Consumer Contract，不能用 Catalog 字段自填替代审批。
- 三段正式激活使用 guarded activation plan：展示旧/新 Prefab、expected Manifest/Authoring hash、合同和授权证据；确认后事务移动到 `Assets/VFX/MxFramework/Production/FireballBurst/`，更新 Authoring Catalog、Published Manifest、Resource Catalog 与 Unity 投影并写 activation receipt。失败必须反向移动资产并恢复全部 JSON/投影；旧正式 Prefab 保留到独立影响分析证明无消费者。
- 手动组装预览支持 `纯 VFX / 角色挂载`。角色目录延迟读取 `Assets/MxFrameworkGenerated/CharacterPackages` 的 Ready Unity 导入回执，复用 CharacterAction 的角色与 AnimationClip 发现结果，并且只接受具有有效 Preview Prefab、Animator、Avatar 和真实 `Socket_<socketId>` Transform 的角色。整套 VFX 根节点以零局部 Pose 挂到单一 Socket，Layer 继续保持原有相对变换；角色动画与 VFX 共用 60 FPS 时间轴，拖帧、暂停和重播同步生效。`previewContext` 的平铺字段是五阶段共享默认值，`phaseOverrides` 为 Build/Rebuild/Loop/Complete/Dissipate 保存字段级稀疏差异；阶段切换时从阶段局部第 0 帧采样动画并切换 Socket，同角色复用缓存预览实例，不同角色按 Prefab 隔离缓存。上下文进入草稿/预览证据 hash，但从 Composition/Candidate 内容 hash 中排除；角色层级、Animator、AnimationClip、Socket 和阶段预览绑定永不写入 Candidate Prefab。相机、背景、根位置、网格、角色显隐和 Socket 辅助轴仍只属于 Editor 本地状态。
- Development Agent 固定先搜索 Production Primitive/Recipe，再搜索 Source Components；缺口通过 Unity Editor API 提取，随后生成有序 Recipe 并烘焙项目 Candidate Prefab。Runtime 只加载最终项目 Prefab，不读取 Source Library 索引、Recipe、curation 或 Piloto 语义数据。
- 首批 11 个 `Candidate + ReviewNeeded` Primitive 与 Fireball Impact/Flight/Charge 三个 Recipe 保持稳定 ID，并迁移到通用成员证据。授权只解决使用与再分发边界，不等于艺术或性能审批；自动流程仍不能设置 `Ready`。

版本化 `windows.d3d12.1080p60/v1` 质量合同固定 `1/60s`、seed `17/73/211`、OneShot `0/10/25/50/75/100%` 切片、Loop warm-up/steady/cleanup、三类视角、四类背景、20 次 ghost cycle、空间/fallback 阈值、零 steady-state GC 和 Low/Medium/High CPU/GPU P95 预算。当前 `VfxCreativeQualityEvidenceRunner` 会执行完整捕获矩阵、输出 11 个 Unity 机器 gate，并由 Core 根据测量值补 `PerformanceProfilePassed`；创作实验室的“生成质量证据”入口只在固定 Core receipt 成功后开放。缺少目标平台 CPU/GPU/GC Provider 时仍会导出 `NotMeasured` 的 fail-closed 报告并阻断晋升，绝不以单帧预览或默认值替代性能证据。

视觉评分只用于排序：Brief 25、可读性 20、节奏 20、空间运动 15、材质色彩 10、完成度 10；三位评审取中位数，总分至少 80、每维至少 60%、极差不超过 15 才可进入 shortlist。任何分数都不能授权 Ready；自动流程的最高目标状态是 `ReviewNeeded`。

### 1.4 语义属性与精确重定时目标（2026-07-15）

旧 `mx.vfx.composition-draft.v2` 的 `playbackSpeed/emissionScale/lifetimeScale/sizeScale/colorTint` 只作为迁移输入，不再是 Current 编辑路径。Current v3 工作台已经以内容寻址 Source Snapshot、Component/Element/Shader Profile 基础设施、95 项 Property Registry、`mx.vfx.composition-draft.v3` 稀疏语义覆盖和 `mx.vfx.composition-bake-receipt.v3` 建立审计链；支持六种 Layer/成员精确重定时、完整 MinMaxCurve/Gradient、成员矩阵、Renderer/Material/Custom Data 首批 Adapter、恢复/三方重映射、Preview/Bake 字段等价与源完整性证明。95 项 Registry 只代表受控词表，真实可写属性取决于人工确认 Profile Binding 与 Adapter；当前持久化 Profile Manifest 为空，AutoSuggested 仅支持源预览，Candidate 烘焙必须先完成实际成员 Profile 确认。Trails 当前完成了结构化 Snapshot 与经确认的 `PreserveNormalizedLifetime` ExtendTail 切片，通用 Trail 属性、SubEmitter/Collision、未确认 Shader 和 VFX Graph 仍按 fail-closed Design 边界处理。

详细的数据结构、Current/Design 状态矩阵、六种重定时模式、曲线/Gradient/颜色空间合同、ParticleSystem/Renderer/Material/Custom Data Adapter、工作台四页 Inspector、增量预览、Candidate 烘焙、NoAlloc 运行时属性驱动、v2 事务迁移、诊断和测试矩阵统一由 [VFX 语义属性管理与精确重定时开发设计](VFX_SEMANTIC_PROPERTY_RETIMING_DESIGN.md) 定义。能力是否可用以该文档 0.1/0.2 的状态矩阵、源码和 focused tests 的交集为准。

## 2. 目标与非目标

### 2.1 目标

- 建立 Development Agent、技术美术、程序和设计共同可读的 VFX 语义事实源。
- 通过 Primitive 与 Recipe 提高通用视觉层的发现率和实际复用率。
- 保持一个 Published Effect 对应一个可独立加载、池化和验证的最终 prefab。
- 让新建、替换、组合、废弃和迁移操作具备 dry-run、影响分析和稳定 diagnostics。
- 让 `MxVfxShowcase` 能按真实语义预览，而不是长期依赖文件名启发式分类。
- 为 ParticleSystem、LineRenderer、Custom View 和可选 VFX Graph 后端提供统一的 Authoring 合同。
- 以 CharacterTest 和 `mxframework.samples` 为首个样板，但契约保持框架通用，不写入具体游戏业务规则。

### 2.2 非目标

- 不让 VFX 决定伤害、命中、Buff、移动、投射物落点或其它 gameplay authority。
- 不在 Runtime 按 Primitive 动态搭积木；组合和烘焙发生在 Authoring/发布阶段。
- 不把 Unity prefab、`VisualEffect`、粒子模拟或 Unity instance id 放入 replay hash、SaveState 或网络状态。
- 不让外部 Authoring 工具手写 Unity scene/prefab YAML、GUID 或 `.vfx` 序列化数据。
- 不以自动规则替代人工美术质量评审。
- 不要求所有支持材质、纹理、Mesh 和 Subgraph 都拥有可直接加载的 ResourceKey。

## 3. 核心模型

```text
Support Asset
  texture / material / mesh / shader / VFX Graph subgraph
                    |
                    v
Primitive ------------------------------------------------+
  reusable flash / ring / smoke / spark / trail / decal   |
                    |                                      |
                    v                                      |
Recipe                                                     |
  ordered layers + parameter bindings + defaults           |
                    |                                      |
                    v                                      |
Published Effect Prefab <----------------------------------+
  one loadable and poolable Unity GameObject
                    |
                    v
ResourceKey(GameObject)
                    |
          +---------+----------+
          |                    |
          v                    v
VfxDefinition          Catalog-only presentation binding
          |
          v
Trigger Map / CharacterAction / Combat / Gameplay bridge
```

### 3.1 Support Asset

材质、纹理、Mesh、Shader、Subgraph 等只服务上层 Primitive 或 Published Effect。默认作为依赖资源，不获得业务可直接请求的 ResourceKey。Scanner 记录其依赖关系和技术指纹，Catalog 不复制完整 VFX 语义。

### 3.2 Primitive

Primitive 是具有单一视觉职责、可跨多个效果复用的 Authoring 单元，例如径向闪光、冲击环、火花、软烟、地面余辉或路径拖尾。

拆分标准：

- 能独立描述视觉职责，并被至少两个 Recipe 复用，或已被明确规划为共享基础件。
- 拥有稳定参数合同和兼容 Anchor。
- 可以是嵌套 prefab、ParticleSystem 根、LineRenderer 模板、VFX Graph Subgraph 或受控 Custom View 模板。
- 总是共同出现、参数高度耦合且单独复用价值低的粒子层应留在同一 Primitive，避免过度拆分。
- Primitive 默认不是 Runtime 直接加载单位，也不要求进入 Resource Catalog。

### 3.3 Recipe

Recipe 是有序 Primitive 层、参数绑定、默认覆盖和组合依赖的 Authoring 配方。Recipe 不保存 gameplay 触发，不拥有 Runtime instance，也不在运行时展开。

Recipe 必须满足：

- `recipeId` 全局稳定且唯一。
- layer 顺序稳定，`layerId` 在 Recipe 内唯一。
- 只能引用已存在且 backend/anchor 兼容的 Primitive。
- 依赖图无环；Recipe 不允许递归包含自身。
- 参数绑定只能引用已声明的 Recipe 参数和 Primitive 参数。
- 同一输入和工具版本生成相同结构的 Published Effect。

### 3.4 Published Effect

Published Effect 是 Runtime 和 Showcase 实际加载的最终 `GameObject` prefab。它可以由 Recipe 生成，也可以是被语义目录登记的手工 prefab。

- 一个 Published Effect 必须拥有稳定 `effectId` 与 `ResourceKey(GameObject)`。
- 需要 `VfxRuntimeModule` 管理生命周期的效果再绑定 `VfxDefinitionId`。
- projectile flight、状态 view 或其它 catalog-only 表现可以没有 `VfxDefinitionId`，但仍需要语义条目。
- 发布产物必须完整包含 Runtime 播放所需的组件和依赖，不要求 Runtime 重新解析 Recipe。
- Recipe 或 Primitive 更新后，validator 必须能识别 Published Effect 已过期，而不是静默继续标记 Ready。

## 4. 权威边界

| 事实 | 权威来源 | 说明 |
| --- | --- | --- |
| ParticleSystem、VisualEffect、LineRenderer、材质、纹理和 Mesh 的实际内容 | Unity asset / prefab / VFX Graph | 技术实现事实；外部工具只读扫描，不伪造 Unity 序列化内容。 |
| 用途、语义、组合、参数、Anchor、预算、fallback 和预览默认值 | VFX Authoring Catalog | Authoring truth；供 Development Agent、工具和 Showcase 查询。 |
| provider、address、package、variant、通用 labels 和加载依赖 | Resource Catalog | 通用资源加载事实；不扩张为 VFX 专用 schema。 |
| Runtime definition id、prefab ResourceKey、默认 lifetime 和 trigger 映射 | `vfx_definitions` / `vfx_trigger_maps` | Runtime 编译/注册事实。 |
| gameplay 触发帧、目标、命中和技能规则 | CharacterAction / Gameplay / Combat / project runtime | VFX 只消费其表现请求，不反向修改规则。 |
| 源美术 provenance 和制作交接 | ArtSource manifest（存在时） | VFX Catalog 可引用稳定源资产 id，不复制源制作历史。 |
| 依赖图、结构指纹、相似度、缩略图和报告 | Scanner / generated report | 可重建证据；不手写，不作为 Authoring truth。 |
| 最终艺术质量批准 | 人工评审状态 | Development Agent 可提出建议和诊断，不能自行批准为 Ready。 |

VFX Authoring Catalog、Recipe、Preview metadata、scanner report 和 Unity 表现对象均不进入 `MxFramework.Vfx` Runtime hash、SaveState、正式网络 snapshot 或 gameplay authority。

## 5. Authoring Catalog

### 5.1 当前路径

当前第一版项目级事实源：

```text
Assets/Config/MxFramework/Vfx/vfx_authoring_catalog.json
```

外部 package 后续可以提供 package-local VFX Authoring Catalog；导入时按 `catalogId + packageId` 合并并检查 stable id 冲突。第一阶段只实现项目级 catalog，不提前扩张 Runtime package 协议。

### 5.2 顶层结构

```json
{
  "schemaVersion": "mx.vfx.authoring.v2",
  "catalogId": "mxframework.vfx.authoring",
  "taxonomyVersion": "mx.vfx.taxonomy.v1",
  "strictCoverage": false,
  "primitives": [],
  "recipes": [],
  "effects": []
}
```

- `schemaVersion`：JSON 合同版本；未知 major version 必须拒绝加载。
- `catalogId`：目录稳定身份，不使用本机路径或时间戳。
- `taxonomyVersion`：受控词表版本；词表升级必须产生可审查 diff。
- `strictCoverage`：为 `false` 时缺失语义条目只报告迁移 warning；为 `true` 时所有 `domain.vfx` direct `GameObject` 都必须有 Effect 条目。
- 数组序列化按 stable id 排序，报告和 hash 不依赖文件系统枚举顺序。

### 5.3 代表性结构

```json
{
  "schemaVersion": "mx.vfx.authoring.v2",
  "catalogId": "mxframework.vfx.authoring",
  "taxonomyVersion": "mx.vfx.taxonomy.v1",
  "strictCoverage": false,
  "primitives": [
    {
      "primitiveId": "vfx.primitive.impact.radial_flash",
      "displayName": "径向命中闪光",
      "description": "可复用的一次性径向闪光层。",
      "assetRef": {
        "kind": "UnityAsset",
        "path": "Assets/VFX/MxFramework/Samples/Primitives/Impact/RadialFlash.prefab"
      },
      "backend": "ParticleSystem",
      "roles": [ "Impact" ],
      "compatibleAnchors": [ "World", "HitPoint", "Target" ],
      "parameters": [
        {
          "parameterId": "intensity",
          "valueType": "Float",
          "unit": "Normalized",
          "defaultValue": 1.0,
          "minValue": 0.0,
          "maxValue": 2.0,
          "transport": "StaticBake"
        }
      ],
      "reviewStatus": "ReviewNeeded",
      "exemplar": false,
      "tags": [ "flash", "impact" ]
    }
  ],
  "recipes": [
    {
      "recipeId": "vfx.recipe.projectile_impact.small",
      "displayName": "小型投射物命中",
      "parameters": [ "intensity" ],
      "layers": [
        {
          "layerId": "flash",
          "primitiveId": "vfx.primitive.impact.radial_flash",
          "order": 10,
          "bindings": {
            "intensity": "intensity"
          },
          "overrides": {}
        }
      ],
      "reviewStatus": "ReviewNeeded",
      "exemplar": false
    }
  ],
  "effects": [
    {
      "effectId": "vfx.magic.fireball",
      "displayName": "火球命中",
      "description": "小型火系投射物的命中表现。",
      "resourceKey": {
        "id": "vfx.magic.fireball",
        "typeId": "GameObject",
        "variant": "",
        "packageId": "mxframework.samples"
      },
      "definitionId": 12001,
      "recipeId": "vfx.recipe.projectile_impact.small",
      "roles": [ "Impact" ],
      "themeIds": [ "fire" ],
      "anchorContract": {
        "anchor": "HitPoint",
        "space": "World"
      },
      "lifecycle": {
        "mode": "OneShot",
        "defaultLifetimeFrames": 24,
        "stopPolicy": "Expire"
      },
      "parameterOverrides": {
        "intensity": 1.0
      },
      "quality": {
        "costTier": "Medium",
        "fallbackPolicy": "UseResourceVariant",
        "fallbackEffectId": ""
      },
      "budget": {
        "prewarmCount": 1,
        "recommendedMaxConcurrent": 16,
        "priority": "GameplayImportant",
        "cullDistanceMeters": 40.0
      },
      "preview": {
        "anchorPreset": "HitPoint",
        "targetDistanceMeters": 4.0,
        "durationSeconds": 1.0,
        "cameraScaleMeters": 3.0
      },
      "reviewStatus": "ReviewNeeded",
      "exemplar": false,
      "tags": [ "projectile", "small", "fire" ]
    }
  ]
}
```

示例表达目标合同，不代表该 Catalog、Primitive prefab 或 Recipe 当前已经存在；`vfx.magic.fireball` 仅用于展示如何关联现有 ResourceKey/Definition。

## 6. 受控词表

### 6.1 固定行为字段

| 字段 | 第一版值 | 用途 |
| --- | --- | --- |
| `backend` | `ParticleSystem`, `LineRenderer`, `CustomView`, `VfxGraph` | 决定技术扫描、预览和发布 adapter；`VfxGraph` 在第二阶段启用。 |
| `roles` | `Charge`, `Cast`, `Flight`, `Impact`, `Telegraph`, `Beam`, `Aura`, `Status`, `Zone`, `Environment`, `Transition` | 表达视觉职责；允许多值但必须有一个 primary role。 |
| `anchor` | `World`, `Caster`, `Target`, `Socket`, `HitPoint`, `Path`, `Ground` | 决定预览布置和 binding 兼容性。 |
| `space` | `Local`, `World` | 表达模拟和变换空间预期。 |
| lifecycle `mode` | `OneShot`, `Loop`, `Persistent`, `ManualStop` | 决定发布校验和回收策略。 |
| `reviewStatus` | `Draft`, `ReviewNeeded`, `Ready`, `Deprecated` | 人工 Authoring 状态；scanner 不得自动写成 `Ready`。 |
| budget `priority` | `Cosmetic`, `Normal`, `GameplayImportant`, `CriticalTelegraph` | 供未来预算、裁剪和降级策略使用，不影响 gameplay authority。 |
| quality `costTier` | `Low`, `Medium`, `High` | 粗粒度技术成本档，不替代真实 profiler 数据。 |

`themeIds` 使用可扩展稳定词表。框架样例可以登记 `fire`、`ice`、`lightning`、`poison`、`physical`、`arcane`，项目和 Mod 可以扩展，但不能把 theme 值写成 Runtime 分支条件。`tags` 允许自由文本，仅用于搜索和推荐。

### 6.2 参数合同

每个参数声明：

- `parameterId`、`valueType`、单位、默认值、可选范围。
- `transport`：`RuntimeSlot0`..`RuntimeSlot3`、`StaticBake`、`PreviewOnly` 或 `BackendDefault`。
- backend binding：ParticleSystem module/property、LineRenderer/custom view property 或 VFX Graph Exposed Property。
- 参数是否允许 Recipe override、Effect override 和运行时 override。

第一阶段不扩展公共 Runtime API。动态参数只能映射到现有 `VfxParameterBlock.Value0..Value3` 的整数槽，并在 Catalog 中声明缩放/单位。Color、Vector 或其它富类型若不能稳定映射，应使用 `StaticBake`、`PreviewOnly` 或 `BackendDefault`；新增公共传输类型必须另行设计、标注分配行为并通过 Runtime API 评审。

## 7. 组合、生成与发布

### 7.1 Authoring-time Composition

- Development Agent 或工具读取 Catalog、scanner evidence 和现有引用，生成只读候选与 dry-run plan。
- ParticleSystem Recipe 可以通过嵌套 Primitive prefab 或正式 Unity Editor builder 生成 Published Effect。
- VFX Graph 在第二阶段优先通过 Subgraph/Operator 复用；最终仍由带 `VisualEffect` 的 Published Effect prefab 对外加载。
- 外部 Authoring Core 可以校验 JSON、依赖图和计划，但不得创建或修改 Unity YAML、`.vfx` 或 `.asset`。
- 任何 Unity 序列化资产变更必须由 Unity Editor、Unity MCP 或仓库内正式 Editor 菜单执行。

### 7.2 Deterministic Bake Contract

同一 Catalog、Primitive 技术指纹、Recipe 和生成器版本必须产生稳定的：

- layer 顺序和对象命名；
- Published Effect 目标路径；
- Recipe input hash；
- ResourceKey/Definition 绑定建议；
- 生成报告和 stale 判断。

生成报告不得包含绝对路径、机器名、随机 GUID、扫描耗时或当前时间。Unity 自己生成的必要 asset GUID 属于 Unity 技术事实，不写进语义 hash。

### 7.3 Runtime Boundary

Runtime 只看到 Published Effect 的 `ResourceKey(GameObject)`、可选 `VfxDefinition`、trigger、anchor、lifetime 和现有参数块。它不读取 Primitive/Recipe、reviewStatus、缩略图、相似度或 Authoring budget。

## 8. Backend 设计

### 8.1 ParticleSystem

- 第一阶段默认后端，沿用 `UnityVfxPresenter` 的 prefab handle、预热 pool 和缓存 `ParticleSystem[]`。
- Published Effect 必须能在 spawn 时重新播放，在 stop/expire 时停止并清理残留状态。
- Recipe 拆层不得造成无意义的 GameObject、Renderer 或 Draw Call 增长；validator 报告组件数和 renderer 数，但首阶段只作为 evidence/warning。

### 8.2 LineRenderer 与 Custom View

- 路径、beam、chain、zone 等已有表现可以登记为 `LineRenderer` 或 `CustomView`。
- Custom View 必须声明公开的参数合同和 reset 行为，不能要求 scanner 读取私有运行时状态。
- 这些 view 仍是表现层，不得反向修正 endpoint、target、radius 或规则生命周期。

### 8.3 VFX Graph 第二阶段

当前状态：package 17.5.0 已安装，可选 `VisualEffect` backend 与首个技术样板已接入；样板仍保留 ParticleSystem fallback 并处于 `ReviewNeeded`，不代表美术已验收。

目标 adapter 必须：

- 位于 Unity-facing 可选程序集，不让 `MxFramework.Vfx` noEngine core 依赖 `UnityEngine.VFX`。
- 对 pooled instance 执行明确的 `Play`、`Stop`、`Reinit` 和 exposed property reset。
- 将参数合同映射到稳定 property id，缺失或类型不匹配时给出 diagnostic，不能静默忽略。
- 在平台、渲染管线、Compute Shader 或质量档不满足时使用 Resource Variant 或 `fallbackEffectId`。
- 没有 fallback 的 VFX Graph Effect 只有在明确 platform whitelist 下才能标记 Ready。
- 保持 VisualEffect 模拟状态、GPU buffer 和 Unity instance id 不进入 Runtime hash、SaveState 或网络。

## 9. Development Agent 管理流程

### 9.1 新建前决策顺序

1. 按 primary role、theme、Anchor、lifecycle、参数合同和质量档查询现有 Effect。
2. 无完全匹配时查询 Recipe，优先通过参数或主题覆盖生成新 Effect。
3. 无合适 Recipe 时查询 Primitive，优先组合现有 Primitive 创建 Recipe。
4. 只有现有 Primitive 无法表达独立视觉职责时才提出新 Primitive。
5. 输出 reuse/new decision、候选、影响引用、目标文件类别、风险和验证入口。
6. 用户或人工评审接受 plan 后，才通过 Unity Editor/MCP 执行资产生成或修改。
7. 运行 validator、刷新 Showcase、生成视觉证据，再由人工更新 reviewStatus。

### 9.2 允许的自动化

- 读取、过滤和比较 Catalog。
- 扫描 Resource Catalog、VfxDefinition、Trigger Map、prefab 组件与依赖。
- 生成 Draft/ReviewNeeded 候选、Recipe 建议、重复候选和 dry-run plan。
- 生成稳定 report、结构指纹和可重建预览证据。
- 在用户明确授权后调用正式 Unity Editor/MCP 生成入口。

### 9.3 禁止的自动化

- 仅凭名称、目录、截图或相似度自动删除/合并资源。
- 自动把 `Draft`/`ReviewNeeded` 提升为 `Ready`。
- 绕过 Unity 写 prefab、scene、`.vfx`、`.mat` 或其它序列化 YAML。
- 把语义相似解释成视觉等价，或把结构相同解释成可以安全替换。
- 在没有影响分析时改 stable id、ResourceKey、Definition ID、Trigger Map 或 quality fallback。

## 10. Scanner、相似度与验证

### 10.1 派生证据

Scanner 可以生成：

- backend、组件、renderer、ParticleSystem/VFX Graph 参数与依赖摘要；
- ResourceKey、Definition、Trigger 和 gameplay/presentation 引用点；
- Primitive/Recipe input hash 与 Published Effect stale 状态；
- 结构指纹、依赖指纹和可选预览 perceptual hash；
- 语义覆盖率、复用率、重复候选和预算摘要。

相似度只用于排序建议。结构相同但视觉主题或时序不同、截图相似但 Anchor/生命周期不同，都不能自动合并。

### 10.2 稳定 diagnostics

| Code | 默认级别 | 条件 |
| --- | --- | --- |
| `VFXAUTH001` | Error | schema/taxonomy version 无效或必填字段缺失。 |
| `VFXAUTH002` | Error | Primitive、Recipe、Effect stable id 重复。 |
| `VFXAUTH003` | Error | Unity asset、ResourceKey、Definition 或引用目标缺失。 |
| `VFXAUTH004` | Error | Recipe layer 缺失、重复、循环或顺序不稳定。 |
| `VFXAUTH005` | Error | 参数绑定不存在、类型不匹配或 runtime slot 冲突。 |
| `VFXAUTH006` | Error | Anchor、space、lifecycle 与 Primitive/backend 不兼容。 |
| `VFXAUTH007` | Warning/Error | backend/平台不支持或 VFX Graph fallback 缺失；Ready/发布门禁时为 Error。 |
| `VFXAUTH008` | Warning | Preview 配置缺失、范围非法或无法生成视觉证据。 |
| `VFXAUTH009` | Warning | Published Effect 与 Recipe/Primitive input hash 不一致。 |
| `VFXAUTH010` | Warning/Error | `domain.vfx` 资源缺少语义覆盖；`strictCoverage=true` 时为 Error。 |
| `VFXAUTH011` | Warning | 结构/依赖/预览相似，存在可复用或重复候选。 |
| `VFXAUTH012` | Error | `Ready` 条目存在阻断诊断、stale bake 或未批准替换。 |
| `VFXAUTH013` | Error | Ready 审批缺少人工角色、asset/dependency/Recipe/evaluation/quality 不可变证据、受信 approval-authority receipt/verifier，证据已经 stale，或 evaluation 未绑定到通过硬门禁且入围的实际晋升候选。 |
| `VFXAUTH014` | Error | Ready Recipe/Effect 的传递 Primitive、Recipe 或 fallback 尚未 Ready，或 Anchor/lifecycle 不兼容。 |

报告按 `code -> catalogId -> effectId/recipeId/primitiveId -> sourcePath` 稳定排序，不包含时间戳、绝对路径、机器名或随机数据。

## 11. Showcase 集成

`MxVfxShowcase` 保持唯一的纯 VFX prefab/表现预览场景；真实技能链继续由 CharacterTest Skill Showcase 验证，不新增第三个场景。

第一阶段目标：

- 读取 Authoring Catalog 后按 role、theme、backend、Anchor、reviewStatus 和 quality 分组/过滤。
- 对已覆盖 Effect 使用 `preview` 配置布置 caster、target、socket、path 和 ground anchor。
- 根据参数合同生成受控 slider/toggle/颜色或目标距离控件；当前 Runtime 只传递现有参数槽。
- 显示 Recipe、Primitive layers、ResourceKey、Definition、backend、fallback、budget 和 diagnostics。
- 未覆盖条目继续使用当前 ResourceKey 名称启发式分类，并显示 `VFXAUTH010` 迁移提示。
- Showcase 只消费语义和播放资源，不写 Catalog、不批准 reviewStatus、不代替 profiler 或真实技能验收。

预览证据默认生成到 `Temp/MxFramework/VfxAuthoring/`，不作为事实源、不进入普通提交。成熟阶段若需要发布可版本化缩略图，应另行定义 canonical build 环境和 artifact policy。

## 12. CharacterTest / Samples 迁移

第一批迁移不移动或删除现有资源：

1. 扫描 `mxframework.samples` Resource Catalog 中所有 `domain.vfx` `GameObject`。
2. 关联 `Assets/MxFrameworkGenerated/Vfx/vfx_definitions.json`、trigger maps、projectile presentation binding 和现有 CharacterTest 引用。
3. 从 prefab 组件与依赖生成技术 evidence；从名称推断的 role/theme 只能作为 Draft 建议。
4. 为 Fireball、Laser/Beam、Chain Lightning、Poison Pool、Status Aura 等代表类型建立首批人工复核条目。
5. 将可确认的共享 flash/ring/smoke/trail/ground layer 建模为 Primitive，并用 Recipe 表达复用关系。
6. 初始 `strictCoverage=false`；缺失语义只报告 warning，不阻塞现有 Player/Showcase。
7. 所有现有 direct VFX 已覆盖、validator 稳定且人工完成评审后，才允许提案启用 `strictCoverage=true`。

迁移脚本不得自动重命名 ResourceKey、Definition ID、Prefab 或 Trigger。任何去重都必须先列出调用方、质量差异和替换计划。

## 13. 分阶段路线

### 第一阶段：语义、复用与验证

- 固定 `mx.vfx.authoring.v2` schema 和受控词表；保持 v1 兼容读取，不自动提升迁移条目。
- 实现 noEngine catalog loader、normalizer、validator、scanner/report 与 dry-run operation plan。
- 为 ParticleSystem/LineRenderer/Custom View 建立 Primitive、Recipe 和 Published Effect 合同。
- 迁移 CharacterTest/`mxframework.samples` 样板，保持 `strictCoverage=false`。
- 让 Resource Library/Development Agent 可查询语义、引用和重复候选。
- 扩展 `MxVfxShowcase` 消费语义、参数和 diagnostics。

### 第二阶段：可选 VFX Graph Adapter

- 增加 Unity-facing `VisualEffect` backend、池化 reset 和 Exposed Property binding。
- 建立 VFX Graph Subgraph 复用、平台能力和质量 fallback 校验。
- 为至少一个代表性 Effect 提供 Graph 高质量 variant 与 ParticleSystem fallback。
- 使用同一 Showcase 对两种 backend 做可切换预览和回收验证。

### 成熟阶段：严格治理与度量

- 在覆盖率与工具链稳定后启用 strict coverage。
- 将 budget、并发、裁剪和质量档接入 profiler evidence 与发布门禁。
- 生成可重建缩略图、结构/视觉相似度和复用率趋势。
- 只有在确有 Runtime 需求时才单独评审新的公共参数 API；不得由 Authoring schema 反向驱动 Runtime 膨胀。

## 14. 验收标准

设计进入实现后，每一阶段至少满足：

- Catalog、报告和 diagnostics 可由无 Unity 依赖的测试验证。
- Unity 写入只发生在明确的 Editor/MCP bridge，且有 dry-run plan 与 stale hash 检查。
- Published Effect 能在 `MxVfxShowcase` 播放、停止、清理并重复播放。
- 语义分组不再要求解析 stable id；未迁移资源有明确 fallback 和诊断。
- Recipe 修改会使过期 Published Effect 失效，不能继续显示 Ready。
- Development Agent 能说明为什么复用 Effect、Recipe、Primitive 或为什么需要新增。
- 自动化不会删除资源、批准艺术质量或改变 gameplay authority。
- VFX Graph 第二阶段必须证明 Graph variant 与 ParticleSystem fallback 的参数、Anchor、生命周期和池化行为一致。

## 15. 文档维护

- 本文是 VFX Authoring 设计事实源；不要在 `Docs/Interfaces/Vfx.md`、Resource Catalog 文档或 Showcase Guide 重复完整 schema。
- `Docs/Interfaces/Vfx.md` 只记录已实现 Runtime/API 事实和 Authoring 边界。
- `Docs/Demo/MX_VFX_SHOWCASE.md` 只记录已实现的场景操作与验收；计划能力必须明确标为 Target Design。
- `Docs/CAPABILITIES.md`、`Docs/USAGE.md`、`Docs/AUTHORING_WORKFLOW.md` 和 `Docs/QUALITY_GATE.md` 只在对应实现落地后同步。
- 若未来把“Recipe 必须 Authoring-time 烘焙，禁止 Runtime 动态拼装”升级为不可绕过的项目决策，再新增短 ADR；详细模型仍引用本文。
