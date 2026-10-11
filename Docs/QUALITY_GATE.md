# MxFramework 质量门禁

> Status: Current
>
> 版本 0.3.10 | 2026-09-01
>
> 本文档定义每批迁移、每个模块和每次发布必须满足的验收条件。

## 1. 批次完成定义

每个迁移批次必须满足：

- 源文件和目标文件在 `MIGRATION.md` 有一一对应记录。
- 每个迁移文件保留来源注释。
- 无 WGame 命名空间引用。
- 无 Entitas、Luban、CrashKonijn 等项目依赖。
- asmdef 依赖符合 `ARCHITECTURE.md`。
- 公共 API 已写入 `INTERFACES.md`。
- 至少有一个覆盖核心行为的测试或 Editor 验证说明。
- 剩余风险明确记录，不用“后续处理”替代具体事项。

## 2. 模块验收

### 2.1 Core

- `MxFramework.Core` 默认无 UnityEngine 依赖。
- Unity 类型扩展放入 `MxFramework.Core.Unity` 或标记为待拆分。
- 集合类边界行为有测试：空、满、重复移除、延迟删除、索引更新。
- 位运算类有正负数和边界值测试。
- 字符串工具有 IL2CPP/AOT 风险说明。

### 2.2 Events

- 订阅、取消订阅、重复订阅、防重入行为明确。
- 发布事件时一个 handler 抛异常的策略明确。
- 高频发布无分配。
- 支持 `IDisposable` 订阅句柄。

### 2.3 Attributes

- 区分 base value、final value、modifier value。
- 修改器排序稳定，排序规则写入文档。
- 属性不存在、重复注册、移除修改器的行为明确。
- 变更事件只在 final value 变化时发送，除非文档另行声明。
- 支持调试快照展示每个属性的计算链。

### 2.4 Modifiers

- 条件、效果、计数器、生命周期彼此解耦。
- `ModifierContext` 支持池化并能安全清理残留字段。
- 工厂创建失败有明确错误。
- 修改器不能直接依赖 Buff 实现类或游戏实体。
- 支持暂停、禁用、移除时的资源释放。

### 2.5 Buffs

- 堆叠策略、刷新策略、覆盖策略可配置。
- 永久 Buff 与限时 Buff 行为明确。
- Tick 顺序稳定。
- 移除 Buff 必须解除属性修改器和事件订阅。
- Buff 子类不能隐藏公共生命周期状态。

### 2.6 Runtime AI Planner（AI 模块）

- 只提供接口和轻量默认实现。
- 不绑定 GOAP 插件。
- Planner 可替换。
- `IAiWorldState` 不暴露游戏实体实现。

### 2.7 Config

- 不绑定 Luban。
- 缺失配置、重复 ID、热重载失败有明确结果。
- 支持至少一个默认内存 Provider，便于单元测试。

### 2.8 Editor

- Editor 程序集不进入 Runtime build。
- 所有验证结果可导出文本或 JSON。
- Debug 面板使用快照接口，不直接依赖实现私有字段。
- Test/Sandbox 不污染项目真实配置和场景对象。
- 关卡编辑器必须把已登记对象的直接 Transform 修改和受管场景的其他脏状态标记为未保存；保存动作先预检全部脏模块，成功写入模块 JSON 与场景缓存后再保存 Unity 场景，校验失败不得提前保存场景。
- 关卡场景根下未被任一模块登记的顶层对象必须可见且可定位；已登记对象的模型、碰撞体等子节点不得重复误报。
- 关闭存在未保存内容的关卡编辑器必须提供保存、放弃和取消；放弃后重新载入最近保存的场景与模块配置，未命名场景不得伪装成已恢复。
- Editor 目录引用控件必须直接消费业务正式目录或编译结果：0 个候选只显示诊断，1 个只读，2 个及以上可选；未登记当前值在用户显式重选前不得被清零或替换。
- 通用 Editor 引用层不得建立第二份 Runtime 目录、全局 Provider 注册或反射发现；业务适配器负责候选，通用层只负责显示、兼容和变更回调。
- Trigger 的`作用目标`、`触发对象`、Buff `来源机制`（底层效果归因）和 Runtime `命令来源`必须保持语义隔离；命令授权/溯源身份不得被当作 Buff `SourceId`候选，来源选择也不得暗示会执行被引用 Aura / Mechanic Source 的 Effect。
- 目标语义必须分离来源、`GameplayEntityId`实例身份、definition/variant 身份、基础身份/能力、关系、Tag 和 Status；不得把 Owner/Subject/Instigator 当身份，也不得把 Player/Enemy/NPC/Boss/Summon 固化为万能基础枚举。
- 新增基础身份或能力必须有正式 Runtime consumer 和测试证据；target query 热路径只允许常量时间枚举/位运算，不得引入 LINQ、临时集合、反射或 Unity 对象查找。
- public target query 必须在构造阶段拒绝未定义 relation、identity 或 capability 值；未知 classification 只能在没有对应筛选时兼容通过，配置筛选后必须给出 typed rejection。
- CharacterTest Combat 区域 Subject Filter 必须在 complete occupancy 提交前执行，并复用 Gameplay 的 classification/team/lifecycle 与共享 NoAlloc evaluator；不得另建关卡专用关系枚举、Team Registry 或 Unity Physics 筛选。
- P3.2 的 CharacterTest trigger module/document/cache v2 必须覆盖 v1 默认迁移、v2 roundtrip、关系/参照队伍/额外能力 Undo/Redo、filter-sensitive hash，以及 same-team/enemy/neutral 与运行时变更产生 Exited 的 focused tests。
- P3.3 顶层 Trigger workspace 必须复用 owner module 的唯一文档与保存参与者；workspace 本身不得拥有 `Save()`、第二份脏状态或独立 Undo baseline。
- P3.3 的搜索、分类、选择、折叠和运行时检查不得产生脏状态；名称、启用、目标筛选、领域参数、复制和删除必须走同一文档事务并覆盖 Undo/Redo。
- P3.3 主编辑区必须使用紧凑事件、条件和动作条款；只读事实不得伪装成禁用输入框，同一身份/阵营规则不得在条件和独立目标卡中重复。控件存在和无几何溢出测试不能替代标准/窄窗口的实际视觉验收。
- P3.3e 的`且 / 或 / 非`必须写入并由正式 Compiler 消费现有 Condition Tree，不能只改变摘要文字。切换根逻辑、逐条取反、增删最后一项、Undo/Redo、JSON 往返与编译至少各有一项 focused 覆盖。
- P3.4 教学引导必须作为独立 `characterTest.guidance` 模块共享唯一 `mx.level.authoring.v3` envelope 和保存协调器；不得建立第二份关卡主文档、第二个 workspace owner 或独立保存入口。
- P3.4 只能开放已注册且同时具备 Editor、validator、compiler 与 Runtime observer factory 的 typed completion condition；不得接受任意事件名、字段反射、逻辑表达式、Gameplay 命令或格式化脚本。新建入口必须把六个玩家意图预设与五类技术完成条件分开，预设只创建新步骤，不覆盖当前步骤。
- P3.4 的步骤选择、等待/完成预览、拒绝警告预览、预览语言、预设菜单、折叠和滚动不得产生脏状态；文案、参数、启停、增删、复制、排序和完成条件切换必须分别形成可撤销的文档事务。条件切换只替换 type/schema/payload，并用新条件完整默认 payload；其他作者字段与数组位置必须保持，Undo/Redo 精确恢复旧 type/payload。
- P3.4 的作者界面只编辑简中源文案；六个本地化 Key 必须由 stable ID 与固定 slot 确定生成，复制或改名时同步更新，作者不得手填。正式 TSV 必须覆盖当前正式步骤的 `en-US`，并用 `zh-CN` 保存翻译源快照；缺译或源快照与 Authoring 不一致必须显示 warning。locale revision 只重投影当前 Card，不得重置步骤、observer 或完成状态。
- P3.4 保存必须重新读取最新共享 envelope，仅 upsert Guidance payload，并在同一协调事务中提交 payload、GameManager Scene cache 与受管 Scene；首次 Scene commit 前任一阶段失败必须恢复 JSON 与原 cache，不得丢失其他模块 payload 或留下半套正式输入。
- P3.4 Runtime 遇到缺失或非法模块、未知 schema/condition、非法参数以及 cache source/catalog/schema mismatch 时必须显式禁用 Guidance 并返回 typed diagnostic；Editor Playable / Player build preflight 还必须用磁盘 Guidance module hash 阻断自洽但过期的 Scene cache，Player Runtime 不得为此读取 Editor 文件。不得静默回退到旧硬编码六步；作者显式设置 `enabled=false` 是正常关闭。
- P3.4 Level Editor 读取未知 module schema 或无法解析的 payload 时必须进入只读阻断状态，禁止用 reference 六步替换、标脏或保存覆盖原 payload；缺失模块必须显示根关闭、零步骤且不标脏。合法 module schema 中的未知 completion type 必须保留原 type/payload 并允许从正式条件下拉框修复，不得在 load/normalize 时静默替换。
- P3.4 空白步骤必须建立唯一 stable ID 与六个系统 Key，但 Input Action、六项源文案和 completion type 保持未配置；`未命名步骤`与`未选择完成条件`仅为 UI 占位，不得写入 JSON。文档只要含未完成草稿，无论根或步骤是否启用，都必须在 prepare 阶段阻止保存，且不得写 envelope、cache 或 Scene。
- P3.4 语义预览只验证 Guidance Card 的信息结构，不加载 FairyGUI package，也不能替代已发布 FairyGUI package 的 PlayMode 测试和实际 Game 帧视觉验收。
- P3.4 当前回归入口是 `CharacterTestGuidanceAuthoringTests`、`CharacterTestIntegrationContractTests` 中命名为 `GuidanceSequence_*` 的 focused tests、`PlayerGuidancePresentationPlayModeTests` 与 `Tools/MxFramework.NoEngineTests/UI.Guidance.Tests/`；不得用整类宽泛测试产生的无关失败或仅测试源码存在替代这些精确证据。
- 条件编辑器不得向不兼容 Source 暴露无意义的 scalar、payload 或 instigator 条件；不能无损展平的历史树必须保持原值并显示兼容警告。
- Region Inspector 必须只保留关联 Trigger 入口；工作区效果句必须是确定性 Editor 投影，不能保存为 Authoring 或反向解析为配置。
- 未接入正式 Source/Action Adapter、目录、validator、compiler 和 Runtime consumer 的 Signal、Threshold 或动作不得出现在候选中。
- 关卡编辑器`保存`必须同时提交 Authoring 与对应 Scene cache；全部模块预检或编译失败时必须阻止整个保存事务，不得发布半套缓存。

### 2.9 Input

- 业务代码依赖 `IInputProvider`，不直接读取键盘、手柄、触屏或 XR 控件。
- Action 按意图命名，具体设备输入只出现在 `.inputactions` bindings 中。
- `InputService` 初始化后缓存 Action 引用，不在 Update 中字符串查找。
- 上下文切换通过 `InputContextStack`，避免业务到处 Enable/Disable Action Map。
- 重绑定 operation 完成、取消和销毁时必须 Dispose。
- 本地多人必须使用 `PlayerInput.actions` 的私有副本，不读全局 project-wide actions。
- 至少覆盖上下文栈、命令队列和可替换输入源的 EditMode 测试。

### 2.10 Rendering

- 固定方向阴影分区必须覆盖中心/周边的总纹理预算、中心配置指纹失效、缺失任一深度图时拒绝绑定、同一光线上的跨区投影物体保留，以及交界过渡带内的遮挡合并。导数在共同光照坐标中、分区选择之前计算；数学与编译检查不替代实际交界画面的验收。

- `MxFramework.Rendering` 只能作为 Unity + URP-facing 表现层程序集存在，不得被 Core / Runtime / Gameplay / Combat / Buffs / Resources / Runtime AI Planner 等 noEngine 模块引用。
- `MxFramework.Rendering` 不依赖 `MxFramework.DebugUI`；诊断只通过 `MxFramework.Diagnostics.IFrameworkDebugSource` 暴露。
- `MxFrameworkUniversalRenderer.asset` 上唯一允许的框架级 Renderer Feature 是 `MxRenderingPipelineFeature`。
- 具体渲染能力必须通过 `IMxRenderPass` / `IMxRenderPassProvider` 注册到 FeaturePipeline，不能新增独立框架 `ScriptableRendererFeature`。
- GlobalFrameContext 不写入 camera-derived 值；CameraRenderContext 不覆盖 GlobalFrameContext 已拥有的 shader property id。
- SharedRTRegistry 的 R-RT-01 至 R-RT-08 冲突规则必须有对应测试或明确验收记录。
- MaterialBindingHub 是 Rendering / bridge 材质属性写入入口；不得从框架 Rendering 代码直接绕过为 `Renderer.SetPropertyBlock(...)`。
- RenderDataPublisher 只接收 generic Rendering semantic events；source module entity id 必须先映射为 `MxRenderSubjectId`。
- VolumeBlender 当前验收边界是 request arbitration、stable tie-breaker、global/per-camera isolation、diagnostics 与 final applied blend-state snapshot；不得声称已实现 runtime URP `Volume` object application。
- 后续 VolumeBlender runtime application 只能通过 URP Volume Framework 执行运行时效果；不得替代 URP Volume Framework，不得新增独立框架 `ScriptableRendererFeature`，不得引入 legacy post-processing。
- Rendering state 不进入 runtime authority、Replay、SaveState 或 Runtime hash。
- 公共 Rendering API 不暴露游戏业务词；source module entity id 必须通过 `MxRenderSubjectId` 映射进入 Rendering。
- Rendering authoring 规则以 `Docs/RENDERING_AUTHORING_GUIDE.md` 为唯一入口，避免在 `USAGE.md` 或能力清单中复制第二份 guide。

### 2.11 Voice Input

- noEngine `MxFramework.VoiceInput` 不引用 Unity、Audio、Input、Gameplay、Combat 或 Editor。
- Unity 麦克风采集只放在 `MxFramework.VoiceInput.Unity`，Unity 类型不得泄漏到 noEngine provider/service 契约。
- Voice Input 只输出语音输入事实，不直接生成 `InputIntent`、gameplay command、Runtime AI Planner 行为或自然语言语义。
- `VoiceInputService.Tick(...)` 和 provider 稳态 tick 不得引入每帧 LINQ、临时集合、字符串拼接、反射查找或 Unity 对象查找。
- 真实麦克风 provider 必须覆盖无设备、权限拒绝、启动失败、录音中断和停止释放路径。
- 后续 ASR provider 必须通过 `PartialTranscript` / `FinalTranscript` 事件接入，并把凭证、网络、隐私和平台限制记录在对应 adapter 文档中。

### 2.12 VFX Authoring

> Current gate: 本节验收 `mx.vfx.composition-draft.v3` ParticleSystem 工作台。95 项 Property Registry、内容寻址 Snapshot、共享 Profile 批事务、六种 Layer/成员重定时、完整 MinMaxCurve/Gradient、四页 Inspector/成员矩阵、Renderer/Material/Custom Data 首批 Adapter、guarded Draft/恢复/三方重映射、Preview/Bake 等价 receipt、Layer Scheduler 和 MaterialPropertyBlock 轨道已经进入 Current。Registry 只定义属性合同；每个实际写入必须同时存在 Profile Binding 和 Adapter，并通过模块耦合、来源 hash 与编译诊断。AutoSuggested Profile 可以进入 Candidate，但必须把实际 Profile hash、字段 diff、源证据和编译 hash 冻结到 receipt；发布仓库共享 Profile 是带影响分析的独立可选操作。Trails 当前只验收结构化 Snapshot、合同 stale 检查与 `PreserveNormalizedLifetime` ExtendTail 切片；SubEmitter/Collision、Trail 通用属性、未建立安全绑定的 Shader、像素级视觉 Golden 自动审批与 VFX Graph 仍按 `VFX_SEMANTIC_PROPERTY_RETIMING_DESIGN.md` 的 Design remainder 验收。

- `mx.vfx.source-curation.v2` 支持 `AutomaticCurated` 与 `DraftLocalOnly`。自动模式的精选 Element 总数必须保持在 180–240，复合职责组至少 40 个；`DraftLocalOnly` 必须保持全局 Element/覆盖为空、禁止自动补齐，并只展示当前草稿私有 Compound Element，成员仅在详情中可见。两种模式都必须保留稳定来源 hash 与完整 Source Library 索引。
- 完整 Source Element 索引必须按技术指纹稳定聚类并折叠等价来源；规范代表按人工 Preferred、已策展、已提取、合理低预算、稳定 Component ID 的顺序选择。索引计数以当前 Source Library hash 为准，不硬编码 9,191。
- 所有提取计划在 Unity 写资产前必须通过 `vfx_source_extraction_registry.json`：exact 成员或等价技术指纹已登记时强制 `ReuseExisting`，复合组只提取缺失成员；Primitive ID、输出路径或规范指纹冲突必须阻断。Variant 必须提供可验证的 override diff/hash；成功提取必须同步 Catalog、receipt、registry 和 curation 映射。
- `mx.vfx.composition-draft.v3` 必须稳定 roundtrip，并绑定 Source Index、Curation、Catalog、Property Registry 和逐 Profile hash；v1 明确拒绝烘焙，v2 只允许默认粗倍率事务迁移。迁移必须先写原始字节和 receipt；任何非默认 `playbackSpeed/emissionScale/lifetimeScale/sizeScale/colorTint` 必须阻断并要求人工转换。每个草稿必须有至少一个启用的 `Loop` Layer，`Build/Rebuild/Complete/Dissipate` 可为空；五阶段帧数覆盖、`OneShot/Continuous`、Layer phase、`startFrame`、time segments、Retime Mode 和 semantic overrides 必须进入稳定 hash/receipt。stale 草稿允许打开和试预览，但必须阻断语义保存与烘焙。角色挂载的角色、姿势、AnimationClip、Socket 和同步参数属于可共享评审上下文，可进入可选 `previewContext` 与草稿/预览证据 hash，但必须从 Candidate 内容 hash、Prefab、Runtime authority、SaveState、网络和 gameplay hash 排除；个人相机、收藏、角色显示开关、辅助轴和其它临时预览参数不得进入草稿。
- Layer 时间条右手柄必须编辑 `timing.targetArtisticFrames`，不得继续累计 `lifetimeScale`。`ExtendBody` 必须保持 Entry/Exit 绝对帧；StartLocked/EndLocked/CycleLocal/BodyNormalized 事件必须按合同映射。Preview 和 Bake 必须从相同 Source Snapshot/Profile 重新编译；Bake 的 `startFrame` 由统一 Scheduler 执行，不得再叠加 ParticleSystem `startDelay`。
- 当前 ParticleSystem Adapter 必须验证 Main、Emission、Shape、Color/Size/Velocity/Rotation over Lifetime、Force、Noise、Texture Sheet、Renderer 和已确认 Custom Data/Shader 绑定的精确应用；MinMaxCurve/Gradient 必须保留模式、Key、切线、权重与 Gradient mode。Trail `PreserveNormalizedLifetime` 必须验证 source Trail hash、dieWithParticles，并证明延长主粒子 Lifetime 时归一化 Trail lifetime、width/color 保持不变；其它 Trail 属性和 SubEmitter/Collision 等没有安全 Adapter 的字段必须只读或阻断，不得靠名称猜测。
- Layer 级与成员级语义覆盖必须经过同一 Registry/Binding/范围/只读校验；`Unsupported/Unconfirmed/Excluded` 成员属性不得通过成员覆盖绕过门禁。Inspector 必须明确区分 Layer 主控与成员独立值；同 Property ID 的成员值替换 Layer 值，未设置属性继续继承 Layer，清除后恢复继承。属性标记为 `Override` 却没有成员独立值时必须以 `VFXSEM023` 阻断。成员 Retime 默认跟随 Layer，显式覆盖后必须进入 Compiler、耦合模块门禁和最终 Adapter。Shader 贴图槽和材质局部时间只有在实际 Shader Binding、材质属性和所需 Vertex Stream/Custom Data 合同通过验证时才可编辑。
- 共享 Profile 发布必须显示 Component/Shader/Element 覆盖率和影响面；选择具体成员时只能发布该成员 Component/Shader Profile，选择 Layer 主控时才允许发布完整 Element。发布后的依赖必须合并写回 Layer，不得覆盖或遗失其它绑定。Manifest `reviewState` 与 Profile 文件 `status` 必须同时为 `Confirmed` 才算共享规则；未发布的 AutoSuggested 绑定仍可用于 Candidate，但必须冻结在 receipt 且不得伪装成共享策展结果。
- Profile 缺口报告必须是稳定、无时间戳、无绝对路径的 `mx.vfx.profile-gap-report.v1`；必须把“当前语义烘焙阻断”与“尚未实现/预留的能力限制”分开，不能把未使用的 Registry 属性误报为当前 Candidate blocker，也不能通过生成报告自动改变任何评审状态。
- 工作台必须按当前 Layer/成员目标展示逐属性能力状态。没有 Binding/Adapter、只读或预留的属性控件必须禁用；AutoSuggested Binding 可以用于预览编辑与 Candidate 烘焙，UI 必须标为“候选绑定”并说明其尚未发布为跨草稿共享规则；部分成员可用时必须提示检查成员矩阵，不能伪装成全成员安全写入。
- Candidate receipt 必须同时携带 Preview/Bake compile hash、稳定字段 Diff hash、源资产完整性前后 hash；任一不一致必须回滚 Candidate，不能只写警告。
- Runtime Scheduler/Property Driver 在初始化与 warm-up 后必须以至少 4096 次稳态调用证明当前线程 managed allocation 为 0 B；PlayMode 还要验证共享浮点帧、精确起止和退出清理。
- Candidate Prefab 的 `UnityVfxBakedLayerScheduler` 与 `UnityVfxBakedPropertyDriver` 必须在初始化时缓存依赖；稳态 Tick 不得执行组件查找、LINQ、临时集合或字符串拼接。Material float/color 动态轨道必须通过缓存 Shader Property ID 和 MaterialPropertyBlock 驱动，不能每帧实例化材质。
- 角色挂载预览只允许发现 Unity 导入回执为 Ready、Preview Prefab 存在且 Animator/Avatar 有效的生成角色包；Socket 必须同时存在于 `geometry_binding.json` 和生成 Prefab 的真实 `Socket_<socketId>` Transform。角色与 VFX 必须共享 60 FPS 播放/暂停/重播/拖帧状态，Layer 重建、显隐和 Solo 不得重新加载角色或重置相机；Candidate Prefab 中不得出现角色 Renderer、Animator、AnimationClip 或外部角色层级。
- Candidate Prefab 必须包含一个根对象、五个阶段子根和 `UnityVfxLifecycleController`。首次播放为 `Build → Loop`，显式重建为 `Rebuild → Loop`；默认停止和自然到期走 `Complete`，显式 `Dissipate` 走独立退出层。Runtime 必须立即移除权威 active state，Presenter 只在 view-only 尾迹清理结束后回池；稳态阶段 Tick 为 `NoAlloc after initialization`。
- 手动组合预览不得修改 Piloto 源资产或当前场景。显式烘焙只能通过 Unity Editor API 写入 `Assets/VFX/MxCandidates/Manual/<draftId>/`，生成依赖 receipt，并保持 Draft/ReviewNeeded；不得在预览时持续制造 Prefab。
- VFX 创作入口必须保持通用。Fireball、Lightning 等只允许作为普通 Brief/Composition Draft 示例，不能拥有专用 EditorWindow、菜单、序列化协议或发布捷径。
- Composition Draft/Candidate 不能直接成为技能资源权威。真实技能接入必须显式选择 consumer slot，并绑定稳定 ResourceKey、Consumer Contract hash、Composition/Profile hash、Candidate receipt、Prefab GUID/content hash 和人工实战检查。Charge/Flight/Impact 的技能帧优先；占位调整期 Charge 合同为 30 帧，仍禁止通过整体比例缩放把 480 帧效果压入短前摇。
- `ReviewNeeded -> Ready` 必须从工作台执行显式人工审批，生成独立 `mx.vfx.ready-approval.v1` 回执；回执绑定当前 Prefab GUID/content、Composition、Candidate receipt、Profile dependency、Preview Evidence 和 Consumer Contract。仅手写 `visualMaturity=Production` / `reviewStatus=Ready` 或修改 approval JSON 不能通过发布计划。
- `vfx_published_bindings.json` 必须覆盖全部正式 VFX ResourceKey、无重复键，并绑定 Prefab GUID/content hash、合同 hash 和 activation evidence。生成 Unity Binding Asset、Resource Catalog 和 Authoring Catalog 必须与 Manifest 一致。本机 `Library` 覆盖不得修改这些正式事实源，也不得进入 Player。
- 原子 binding set 不能部分发布。`vfx.skill.fireball_burst` 的 Charge、Flight、Impact 必须全部 `Production + Ready`，审批与试玩证据均未 stale，才允许一次事务切换；任一步失败必须回滚资产路径、Authoring Catalog、Published Manifest、Resource Catalog 和生成投影。
- `ReviewNeeded` 的 Development Agent 候选必须通过 `mx.vfx.agent-task.v1` / receipt 校验、Unity 序列化与 provenance 门禁，并由用户实际播放后使用一次性内存授权晋升；JSON 自填字段不能授予权限。
- `ReviewNeeded` 首次人工检查不要求完整目标平台性能矩阵。正式性能、fallback 等价性、可读性和授权审批仍是 `Ready` 前置条件，任何 Agent 或自动评分都不能写 `Ready`。
- `mx.vfx.provider.procedural` 只允许作为测试夹具或旧会话标识，其产物不得进入晋升证据。
- 当前 37 Effect、1 Recipe、1 Primitive 必须保持 `LegacyPlaceholder`，直到原创替换完成；语义覆盖率和 Production 覆盖率必须分开报告。`LegacyPlaceholder` 不得成为 Ready、Exemplar 或 Candidate/Production Recipe 的视觉输入。
- 本机 Piloto Studio 包只能以逻辑 ID `reference.piloto.arpg-realistic-demon-vs-angels` 和 `ReferenceOnly` manifest 参与任务。绝对路径只允许出现在 Git 忽略的 Temp registration；参考资产复制、项目依赖或 receipt 中的直接/传递依赖必须阻断。

- VFX Authoring Catalog 必须通过 `mx.vfx.authoring.v2` schema/taxonomy、stable id、Recipe graph、参数、Anchor/lifecycle、ResourceKey/Definition evidence 和 review gate 校验；v1 只作为兼容读输入，正式保存输出 v2。
- `strictCoverage=false` 时未登记的 `domain.vfx` direct `GameObject` 只报告迁移 warning；启用 `strictCoverage=true` 前必须证明所有 direct VFX 已有 Effect 条目。
- Development Agent、scanner 或生成器不得自动把条目提升为 `Ready`；Ready 条目存在阻断 diagnostic 或 stale bake 时必须由 `VFXAUTH012` 拒绝。
- `mx-authoring vfx validate` 生成的 JSON/Markdown report 必须稳定、不得包含生成时间或仓库绝对路径；permissive coverage 只能产生 warning，启用 `strictCoverage` 后遗漏必须阻断。
- catalog 保存前必须先执行 expected-hash guarded dry-run plan；`VFXOP001`..`VFXOP005` 只描述 catalog 写入影响与 v1 迁移阻断，不得暗示或执行 Prefab、Scene、材质、纹理或 VFX Graph YAML 写入。
- Recipe-backed Effect 必须声明 Published Effect asset path、baker id/version 和 Recipe input hash；`vfx plan-bake` 的 `VFXBAKE001`..`VFXBAKE005` 只证明计划与输入状态，只有正式 Unity `VfxAuthoringRecipePrefabBaker` 生成的 Prefab 及 `AuthoringBakeEvidence` 才能证明产物已烘焙。scanner 必须同时匹配 Catalog expected hash、evidence observed hash、output path 和实际 Unity asset path。
- VFX Graph Effect 必须通过独立 optional adapter 启用，声明有效 Exposed Property 类型/slot/scale，并在 pool reuse 执行 `Stop/Reinit/Play`；compute shader、Graph asset 或图形设备能力不足时不得抑制 ParticleSystem/fallback。package 已安装不等于具体 Graph Effect 已通过艺术评审。
- VFX thumbnail manifest 的 content hash 与 64-bit dHash 都属于可重建 evidence；感知距离候选只能触发人工复核，不得自动合并、删除或把条目提升为 `Ready`。UI Toolkit Resource Library 可以生成内存草稿，但 Apply 前必须由 authoritative CLI 验证 Temp candidate、检查 expected Catalog hash 并二次确认；窗口只允许写 Catalog，不得写 Prefab、Graph、材质、纹理或其他 Unity YAML。
- `strictCoverage=true` 时 direct VFX coverage 必须为 100%；自动 coverage proposal 只能产生 `ReviewNeeded`。发布报告必须包含 semantic coverage、Recipe Effect、Primitive reuse、structural duplicate-candidate、thumbnail 和 exact preview-hash 指标。
- 自动缩略图必须写入 `Temp/MxFramework/VfxAuthoring/Thumbnails/`，manifest 不含时间戳和绝对路径；缺失 Prefab/thumbnail 必须可见。静态预算 gate 的 finding 在 `Ready` 时阻断，在评审阶段至少告警；它不能替代真实 profiler evidence。
- Development Agent 新建 VFX 前必须执行 `vfx search`，替换/合并/删除前必须执行 `vfx impact`；只有 `Production + Ready` 的 Effect → Recipe → Primitive 可以成为视觉复用建议，LegacyPlaceholder 只作为合同/影响证据。工具不能自动删除资源、改 fallback 或提升 `reviewStatus`/`visualMaturity`。
- Primitive/Recipe 是 Authoring 合同；Runtime 只加载 Published Effect prefab，不在 Tick 中动态组合层。
- 外部工具不得手写 prefab、scene、`.vfx`、`.mat` 或 Unity YAML；Unity 资产写入必须经过 Editor/MCP bridge 并有对应验证。
- VFX Graph Effect 在正式发布前必须证明 adapter 可用，并提供受验证 fallback 或明确 platform whitelist。
- Ready Recipe 的传递 Primitive/Recipe 依赖必须全部 Ready；Ready Effect 的 Recipe、Primitive、fallback 必须 Ready 且 Anchor/space/lifecycle 兼容。审批必须绑定 asset、dependency、Recipe input、evaluation report、quality profile hash/version 和版本化 approval-authority receipt；任一输入变化都按 stale 阻断。`approvedBy`/`reviewerId`/`selectedBy` 自填字符串不能解锁 Ready 或 Promotion；没有受信 verifier 时必须 fail-closed。
- 正向 Exemplar 只能引用人工 Ready 且 `exemplar=true` 的 Effect/Recipe/Primitive；ReviewNeeded 只能用作候选或负面证据。Development Agent、模型评分、迁移和自动覆盖不得设置 Ready。
- Candidate Unity 资产只允许位于 Git 忽略的 `Assets/VFX/MxCandidates/<sessionId>/`。计划/receipt 必须拒绝绝对路径、`..` 逃逸、跨 session 引用、未知 Provider/version/license 和篡改内容；晋升必须做 expected receipt hash、目标冲突、逐资产 content hash/GUID 校验并支持事务回滚/恢复。
- VFX Graph 正式生成只允许认证模板、System/Block/Operator Subgraph 和 Exposed Property；`unity-vfxgraph-17.5-experimental` 任意拓扑 adapter 必须精确版本匹配、默认关闭且结果永远保持 Draft。
- `windows.d3d12.1080p60/v1` 评测固定 `1/60s` 和 seed `17/73/211`。OneShot 捕获 `0/10/25/50/75/100%`，Loop 覆盖 warm-up/steady/Stop/cleanup；至少覆盖 Gameplay、正面 3/4、侧面和暗/浅/复杂/同色冲突背景。
- 完整晋升硬门禁要求：0 Console/render error、0 粉色材质/NaN/缺帧；crop ≤ 0.5%、Anchor error ≤ 5%、path endpoint error ≤ 2%、Telegraph IoU ≥ 0.90；Stop 后 1 秒无活粒子/明显残留且 20 次 Play/Stop 无 ghost；Graph/fallback 主峰差 ≤ 10%、可见范围差 ≤ 15%；目标 Profile steady-state GC 为 0 B/frame，并通过版本化 CPU/GPU P95 预算。
- 单帧 preview coverage/亮度检查只能作为早期候选诊断，不能替代上述完整评测。视觉评分只用于 shortlist：三位评审中位数，总分 ≥ 80、各维 ≥ 60%、极差 ≤ 15；评分永远不能豁免技术、生命周期、fallback、可读性或性能硬门禁。
- Scanner 计入 Ready evaluation evidence 前必须验证 report hash、generation/receipt binding、PromotionPlan/PromotionReceipt target binding，并确认实际晋升 candidate 同时通过硬门禁且进入 shortlist；全失败或未入围报告不得作为 Ready 证据。

### 2.13 Physical Audio

- `MxFramework.Audio` 与 `MxFramework.Audio.Physical` 的 noEngine public contract 不得暴露 Unity、FMOD、WAV 路径、Prompt、Recipe 或 adoption provenance 类型；Combat 不得反向依赖 Audio。
- Physical Audio resolver、contact Tick 和 weapon/air bridge 在初始化及容量 warm-up 后必须 `NoAlloc`；不得在高频路径解析 JSON、按字符串查 Cue/Event/Parameter、使用 LINQ、创建临时集合或动态扩容。
- 同一 `PhysicalAudioInteractionFact` 与同一 Catalog 必须稳定选择相同 Basis、fallback 路径和参数；tie-breaker 固定为 Family priority、Basis priority、匹配分和稳定 Basis ID，不依赖集合枚举偶然顺序。
- 声学材质目录独立于 `CombatSurfaceMaterialKind`；项目映射必须显式、可替换，Air Displacement 不得伪造成 Combat Collider 命中。
- Physical Authoring Manifest 必须校验 typed ID 唯一且不复用、fallback 无环、范围和曲线有界、必需场景无覆盖空洞或同分歧义、Cue/Basis/response 引用有效。
- Runtime Physical Catalog 不得包含 Prompt、Recipe、候选指标、WAV 路径或 provenance；Generated 与 StreamingAssets mirror 必须匹配，并与 Global Audio Catalog hash 联接。
- Workbench 浏览器混音只作为近似预览；发布批准必须来自 Unity 使用当前 Catalog 解析事实并播放当前 FMOD Event。Active Basis 的审批有效性绑定 Basis string/runtime ID 与当前 adoption set；Bank hash 由既有 adoption diagnostics 独立校验，Physical manifest hash 仅作为证据审计字段。
- `500251 MxPhysicalVolume` 与 `500252 MxPhysicalPitch` 是 backend-native control，分别通过 FMOD `setVolume` / `setPitch` 应用，不要求 Event-local parameter。LowPass、Brightness、Scrape 等扩展参数只有在 snapshot 证明 FMOD 参数与 automation 存在时才能列为已生效；缺失的必需参数必须阻断发布，不能依赖后端静默忽略。
- Generated Physical Catalog 缺失或损坏时，只允许整份切换到带 warning 的 legacy fallback；禁止把 generated 和 legacy 规则自动合并。
- Phase 17 one-shot 与 Phase 18 continuous lifecycle 必须分开验收；未实现 loop start/update/stop 时不得把 `Stayed` / `Ended` 诊断事实描述为持续音频能力。

### 2.14 Gameplay Triggers

- `MxFramework.Gameplay.Triggers` 保持 `noEngineReferences=true`，只依赖 Core / Runtime / Gameplay，不引用 Unity、Events、Story、Combat 或项目层。
- complete occupancy/scalar batch 的 normalize/reduce 必须原子；不完整、重复 sample、错误 source kind 或容量不足保留旧 source truth。
- unordered input permutation 不改变 occurrence/activation/intent 顺序；ordered Signal 必须校验严格递增 sequence。
- condition、policy、exclusive arbitration 或 action reservation reject 不回滚已接受 source truth，也不消耗未提交 count/cooldown。
- action dispatch 必须满足 Prepare 全成功才 Commit，失败时逆序幂等 Abort，禁止只做 capacity peek 伪装 reservation。
- Runtime 初始化后 submit/evaluate/enable/reset 高频路径必须 `NoAlloc`，所有 authority capacity 显式且 overflow 结构化失败。
- Phase 1 最低门是 noEngine compile + focused reducer/policy/transaction/capacity/NoAlloc tests；Phase 2+ 的 hash/save/replay/outbox/bridge/Playable 不得由 kernel 测试代替。
- Development Agent 测试 Trigger 只能存在于 Editor / `UNITY_INCLUDE_TESTS` 测试程序集；测试 Action 不得进入正式 catalog、Player、SaveState、Replay 或 authority hash。
- 测试 function ActionSink 的 reserve/abort/commit/dequeue 必须 bounded 且初始化后 `NoAlloc`；handler 只能在完整 Host Tick 后执行，独立 Probe 必须提供至少一个不依赖函数返回值的 oracle。
- 内部 Scenario 通过但仍声明 Unity MCP、PlayMode、场景、截图或 custom-tool 证据时，状态必须保持 `PendingExternalEvidence`，不得报告为完整通过。
- CharacterTest 关卡区域不得用 Unity Collider/PhysX 代替正式 Combat sensor；query candidate capacity 与 mapped subject capacity 必须分别验证，不能假设命中上限等于世界查询输入上限。
- Presence Buff 模板必须覆盖 Entered/Stayed 的 Apply/Refresh 和 Exited 的 Remove，并用短持续时间 Buff 回归证明停留期间不会提前失效；正式 Playable 验收还必须覆盖保存重开、Scene cache identity、实际 Runtime action result 和无 Collider PlayMode smoke。

### 2.15 Platform

- `MxFramework.Platform` 保持 zero framework dependency、`noEngineReferences=true`、`allowUnsafeCode=false`，不引用 Unity、Runtime、Gameplay、Persistence、Settings、Resources 或项目代码。
- environment/location/lifecycle/permission 的 public enum 必须保持 `Unknown=0` 与已冻结数值；default permission operation 不得被解释为 Pending。
- FileSystemPath、URI 和 ProviderOwned location 必须显式区分；unknown、空白、相对 path/URI 返回结构化失败，不能静默转成成功路径。
- lifecycle buffer 初始化后 report/current/drain 为 NoAlloc；duplicate callback 去重，focus/suspend 正交，quitting sticky，overflow 返回覆盖完整 lost range 的单一 resync；revision 耗尽后更新 latest snapshot 并强制 resync，不生成重复 revision 的普通 edge；revision 与 lost/drop counter 饱和不 wrap。
- permission request 永不返回 null；Succeeded/Failed/Cancelled terminal fact/error invariants、second-request Busy 和 cooperative cancellation 语义必须有 focused tests。
- `IWallClock` 只用于非权威 metadata；Local/Unspecified 和 provider failure 必须 fail-closed，不自动转时区或替换 clock，不参与 ordering/generation/replay/hash/SaveState conflict resolution。
- Platform Core 的 focused gate 至少包含 noEngine compile、location/lifecycle/permission/clock tests、warm lifecycle report/drain 0 B、forbidden-reference scan、standards report-only 和 diff-check。Unity adapter 与 consumer migration 必须在各自 phase 另行验证，不能由 Core tests 代替。

### 2.16 Character Cloth

- `MxFramework.Character.Cloth` 保持 noEngine，不引用 UnityEngine、Magica 或其它布料供应商类型。
- 第三方布料只允许出现在单向可选 adapter；Magica adapter 必须受 `MAGICACLOTH2` 约束，Runtime 程序集不得引用 `UnityEditor`。
- System Override 不得覆盖各控制器的本地 Requested State；清除覆盖后必须恢复原请求。
- Running、Paused、DisabledReset、DisabledKeep 必须有明确映射，并在 Magica 异步 Build 完成后重新应用当前有效状态。
- 布料只属于表现层，不进入 Character / Gameplay / Combat authority、Runtime hash、SaveState、Replay、网络同步、命中挂点或战斗碰撞。
- 角色资源正式接入时必须在 Play Mode 验证四态往返、换装和对象池重新启用；仅编译通过不能替代该资源级验证。
- package-local Magica 配置必须由 Authoring import 复制并经 Editor-only 可选后处理器投影到 Preview/Runtime 两份 Prefab；根骨路径、Preset、Collider 父骨或尺寸非法时必须阻断生成，禁止依赖手工编辑生成 Prefab。

### 2.17 CharacterAction Authoring 与角色运行上下文

- CharacterAction 唯一身份必须是 `CharacterActionId` 包装的全局正整数；动作定义不得再保存字符串 StableId，`0` 只表示空引用。DisplayName、Tags、Category、Intent、动画 Key、CombatActionId 与事件 StableEventId 均不得被当作动作身份。
- `CharacterActionSource/action_id_catalog.json` 必须通过 schema、catalog hash、非重叠保留号段、owner library、源路径、全局唯一 allocation 与 active/retired 状态校验。Unity Editor 与 Authoring CLI 必须走 expected revision/hash 事务；复制分配新号，删除后不复用，角色包创建先分配号段。
- `CharacterActionSource/Libraries/Common/character_actions.json` 与角色包 `config/character_actions.json` 是动作正文写入权威；`Assets/MxFrameworkGenerated/CharacterActions` 只允许保存确定性编译投影。
- Action Library 只接受 schema `0.3.0`，Profile/ActionSet/Reaction/Landing 只接受 `0.4.0`；AI Lab logic/draft/published contract 分别为 v10/v3/7，CharacterTest network snapshot/Planner state/Replay 分别为 30/13/3。v9 行为卡必须先按旧哈希认证再确定性迁移 SubjectScope；旧字符串动作身份必须明确拒绝，不能保留运行时别名层。
- Character Application 8.0 的 `actionConfiguration.actions[]` 只写 `{ actionRole, actionId }`；Profile 与 ActionSet ID 由编译器生成，CharacterStudio 不得提供手填 ID 或可编辑整段动作 JSON 的入口。
- 公共组合只允许引用 Common；角色完整覆盖可引用 Common 或自己的角色库。动作 ID 必须全局唯一，引用必须存在且通过 Native / Humanoid 重定向兼容校验。
- 同一 intent 不能同时出现在 `commandBindings` 与 `commandSequences`；结构化连段至少覆盖 1～N 段、末段循环、切换窗口、排队规则和旧单动作绑定兼容。
- Unity 打开/保存请求必须精确携带 Profile、ActionSet、Library、Action ID、读写模式和源 hash；Common 只读，磁盘 hash 变化必须拒绝覆盖，并把打开、保存、冲突、失败与超时状态回传 CharacterStudio。
- CharacterTest 必须按实体 `CharacterStableId + EquipmentStateStableId` 命中精确 ActionSet 与对应 plan cache；身份明确却缺失 ActionSet 时报告角色、包和 EquipmentState，不得回退到 Codex fixture。
- 验收至少覆盖源库加载与迁移、权限/重复/缺失引用、复制事务回滚、Unity 精确定位与 hash 冲突、不同实体 ActionSet 隔离，以及 Combat Girl `LightAttack` 三段真实动作解析。
- 静态门禁必须禁止 `CharacterActionConfig.StableId`、字符串 CharacterAction `ActionId`、`TryGetAction(string)` 与按 `"light_attack"` 判断动作身份；允许其它领域 Stable ID、Tags、显示文本以及显式的一次性迁移 fixture。

### 2.18 Character 原材料导入

- `ArtSource/Characters/<characterId>/character_sources.json` 是角色私有原材料事实源；package-local `config/import_materials_state.json` 是工具状态与证据，不得反向成为人工清单。
- 功能上线后新建角色必须由 `Authoring/CharacterPackages/character_id_catalog.json` 分配六位字符串 ID；`packageId`、`characterId`、`character-<id>`、`ArtSource/Characters/<id>` 与派生 ResourceKey/StableId 必须一致。`000000`、手填语义 ID、目录/清单不一致或 catalog 缺项必须阻断；legacy 角色只允许来自显式 exemption。
- 数字角色归档后 ID 必须进入 retired 且永久不复用。创建事务失败且 lease 未提交时可以回收；catalog expected revision 或文件 hash 冲突必须拒绝写入。
- 数字角色必须具有 `authoring_manifest.json`，但 `.blend`、模型图、milestone、audit 和 provenance 均为可选。未声明不阻断 Compile/Unity Import；已声明文件必须存在、保持 SHA-256 当前、位于标准角色根并使用 Git LFS，非法路径、hash 漂移或进入交付范围的禁止备份文件必须阻断制作清单门禁。Git 忽略的本地备份与既有历史发布快照按[数字角色制作权威](Guides/WGAME_NUMERIC_CHARACTER_AUTHORING.md#制作清单审计与本地备份)区分，不通过删除备份或刷新未经核实的 hash 消除诊断。
- `.blend1/.blend2`、autosave、recovery、`*_backup_*.blend` 不得进入版本管理；正式可选 Blender 文件只允许滚动 work、M0～M3 固定 milestone 与 Final authority。
- 贴图必须声明或确定性推断 `Color / Normal / Mask` 语义和材质 slot。同一 slot 的同语义贴图不得重复；存在贴图的 slot 必须有 `Color`。
- 角色材质表面模式当前只接受 `Opaque / AlphaClip`。`AlphaClip` 必须显式声明 `alphaCutoff=0..1`，生成材质必须启用 `_ALPHATEST_ON`、写入 `_AlphaClip/_Cutoff`、使用 `TransparentCutout` 与 AlphaTest queue，并在 Forward、ShadowCaster、DepthOnly 三个 Pass 使用同一 BaseMap Alpha 裁剪；不得仅靠 renderQueue 或颜色 Alpha 冒充支持。
- 未声明 `mx.characterPaletteBinding.v5` 的传统生成角色继续以 `_BaseMap * _Color` 作为基础色唯一权威，必须完整进入受光路径。只有显式启用 Binding v5 的角色可进入规律色板分支；Binding v1–v4 必须返回 `FaceDetailTextureMigrationRequired`，Usage v1 必须返回 `PaletteRepaintRequired`。该分支必须使用项目共享 `256×256` RGB24 Color Palette、逐顶点 `UV1 / PaletteUV`、RGBA `PaletteData` 和 `wgame.character_palette_stylized_lit`。ColorIndex `0..65535` 必须严格满足 RGB565 双向公式，Atlas 必须有 65,536 个唯一 RGB8 与 `aliasCount=0`；Surface Profile 由 `PaletteData.A` 的 16 个区间中心表达，RGB 保持 AO / Rim / Emission。`PaletteUV` 越界、非像素中心、三角形内 Color/Profile 不恒定、Alpha 非中心、Usage 复合 Token 不一致、RGB565 公式/唯一性失败、Catalog/Atlas/receipt hash 漂移或 Importer 丢弃输入 Alpha时必须 fail closed。主表面仍只允许一次 Color Palette Fragment 采样，不得增加材质 LUT 采样、Pass 或 Draw Call。FaceDetail 启用时只允许当前角色包专属 512×512 Direct RGBA 与直接 UV2 采样，缺图、共享/跨包 Key、非法 Alpha/UV/import settings/manifest/hash 均阻断；关闭时不要求贴图或 UV2。
- 色板角色和 `paletteMode=sharedWGameAtlas` 公共武器必须共享材质与 Atlas，通过 `MaterialPropertyBlock` 写入整行索引；不得调用 `renderer.material`，不得生成角色专属颜色贴图或运行时材质实例。色板行只属于表现状态，不进入战斗权威、Replay、SaveState 或 Runtime hash。
- `mx.characterPaletteBinding.v5` 的 FaceDetail 是可选角色私有资源：启用时必须同时提供第三套 `UV2 / FaceDetailUV` 和当前角色包内 `char.<packageId>.texture.face_detail` 对应的单张 512×512 sRGB Direct RGBA；RGB 为最终细节颜色、A 为覆盖率。贴图固定 Bilinear、Clamp、Mipmap、Alpha FromInput、不可读、Android ASTC 6×6，并通过 `MaterialPropertyBlock` 绑定；不得使用共享 FaceDetail Atlas、Preset、Palette Mask、跨包或公共 FaceDetail 资源。面部 UV 只能位于 canonical 0..1 或使用非承载哨兵 `(-1,-1)`；缺 UV2、缺贴图、错误尺寸/Alpha、资源归属错误或越界 UV 必须 fail closed。Binding v1–v4 必须返回 `FaceDetailTextureMigrationRequired`，但不得要求重画现有 PaletteToken。
- 完成态必须同时证明源文件 hash、material descriptor、`model --usesMaterial--> material --usesTexture--> texture` 依赖图及 source-material-name 绑定；不得因为所有文件行都是 `Clean` 就报告原材料成功。
- `config/import_materials_state.json` schema 2.0 必须持久化 `integrityReceipt`、逐材质 `surfaceMode/alphaCutoff` 证据、稳定 diagnostic code、message 和 suggested action。同步完成后的修复日志必须在重新加载 CharacterStudio 后仍可见。
- 手写 material descriptor 没有工具 owner 标记时不得覆盖；工具生成 descriptor 必须带 owner，才能在后续同步中确定性更新。
- CharacterStudio 必须把完整性 blocker 纳入 Compile / Unity Import gate，并显示 `MATERIAL_DESCRIPTOR_MISSING`、`DEPENDENCY_GRAPH_INCOMPLETE`、`RENDERER_BINDING_INCOMPLETE` 等直接原因。
- 新前端连接未返回 `integrityReceipt` 的旧 Authoring 服务时必须以 `INTEGRITY_RECEIPT_UNAVAILABLE` 失败关闭，提示重启服务；不得把证据缺失解释为 `NotApplicable`。
- 原材料阶段只验收 FBX source material name 与声明的材质表面模式；Unity rig-aware receipt 必须另行证明有效 Avatar、生成材质、`materials[]` 中的最终表面模式，以及全部 Renderer 节点/槽位覆盖。两份回执不得互相替代。
- 聚焦测试至少覆盖：完整两材质/多语义生成、清洁源文件下关系缺失仍阻塞、无重复复制的关系修复、持久化日志、手写 descriptor 保留、旧 Humanoid 与 Generic 流程兼容，以及 CharacterStudio descriptor-first 预览。

## 3. 自动检查建议

可先用脚本实现以下检查；当前仓库级检查入口见 `../Tools/Harness/README.md`：

```text
check-no-wgame-reference
check-no-forbidden-package-reference
check-asmdef-dag
check-runtime-no-unityeditor
check-core-no-unityengine
check-public-api-documented
check-source-comment-for-migrated-files
```

### 3.1 代码与权威说明同批交付

每个功能维护一份现行工程说明，与所属模块接口互相链接；索引与USAGE路由只链接这一说明，不重复复制测试数或完成状态。实现owner同时负责说明，代码与说明在同一变更交付并通过内容评审后才完成。文档纠正现状要对照真实源码与适用版本的执行回执；只读源码／测试定义不证明运行通过。设计改变明确写“待实现”，候选、主合、发布、原生验收和未覆盖范围分别判断。没有新行为影响时说明复核理由，不为使检查通过编造变更或通过结论。

字段／默认值／枚举／卡描述以既有schema、编译器、发布投影和生成器为准，能复用生成时不手工维护第二张数值表。历史Task／ADR仍保留历史性质，完成记录不能代替当前说明。

在权威说明中用引用行声明`Code-Doc`，值为反引号包围的仓内路径或glob。`Tools/Harness/check_docs_health.py <changed-paths-file> --base-ref <同一diff基线>`扫描声明：关联路径变化而该说明未在同一变更处理时失败；只改索引不能代替说明。既有轻量Harness已调用此检查，并纳入删除／重命名前后路径；新增关联按实际功能逐步登记，不要求一次给所有文件建清单。

检查只验证声明范围内的交付关联、格式和声明存续，不理解代码或自然语言语义，**不能证明文档真实**；仅改空白、错改一句话仍可能通过，评审必须核实。未声明关联的代码明确不在本次自动覆盖内。没有安装Git hook或改变安全设置；仓内没有启用CI workflow时不宣称CI已经强制执行。用法与测试见[既有Harness](../Tools/Harness/README.md#code-and-authority-document-delivery)。

## 4. Unity 验证清单

每批代码迁移后在 Unity 中验证：

1. 打开项目无编译错误。
2. Console 无新增错误。
3. asmdef 编译顺序符合预期。
4. 运行 EditMode tests。
5. 如涉及 Runtime Tick，运行 PlayMode smoke test。
6. 打开 `MxFramework > Framework Manager`，验证依赖图和模块状态。
7. 如涉及场景、材质、Shader、Volume、相机或可玩 Demo，按 `Docs/RENDERING_PIPELINE.md` 确认 URP 资产仍为项目基线，且无粉色材质或渲染管线错误。
8. 如涉及 Rendering 框架能力，确认 `MxFrameworkUniversalRenderer.asset` 没有新增独立框架 Renderer Feature，所有能力都通过 `MxRenderingPipelineFeature` 下的 pass/provider 接入。

### 4.1 UI Toolkit Runtime Demo 验收

新增或改动可玩 Demo / Runtime HUD 时必须额外验证：

- `UIDocument.panelSettings` 必须指向正式 `PanelSettings` 资产，不使用临时运行时 instance 作为最终场景配置。
- `PanelSettings.themeUss` 必须绑定 Runtime Theme，不允许为了消除 warning 把 no-theme warning 关掉后留空。
- UXML/USS 必须有一个明确且可验证的绑定 authority：可由场景 `UIDocument.sourceAsset` 绑定 UXML、
  再由 UXML `<Style>` 声明精确 USS dependency；仅当 Demo 脚本本身拥有这些资源时，才在脚本中
  重复序列化 `VisualTreeAsset` / `StyleSheet`，不得建立两个可漂移的并列 authority。
- Play Mode 后检查 visual tree 中关键 root、HUD、按钮、状态 Label 存在。
- Play Mode 后至少读取一个标题 Label、一个 Button、一个动态状态 Label 的 `text` 和 `resolvedStyle.color`，确认文本非空且 alpha > 0。
- Console error / warning 为 0；UI Toolkit warning 不可用“看起来能跑”跳过。
- 如果 USS 或 Theme 在当前 Unity 版本下不稳定，Demo controller 必须像 Tetris / Breakout 一样在运行时对关键 `Label` / `Button` 设置 font、font size、color、alignment 作为兜底。

### 4.2 游戏功能 / 小游戏 / Runtime Showcase 验收

新增或改动游戏功能、小游戏、Playable Demo、Runtime Showcase 或场景验证时，必须额外满足 `Docs/AGENT_GAME_CREATION_GUIDE.md`：

- 编码前已有 API 复用计划，说明 Runtime、Input、Command、Replay、SaveState、UI、资源、Gameplay、Combat 等模块是否使用及原因。
- 游戏功能必须先映射到现有框架能力和接口；绕过既有模块时必须记录缺口，不能生成平行系统。
- 核心规则优先保持纯 C#，可由 EditMode 测试直接实例化。
- Unity `MonoBehaviour` 只承担组合根、输入适配、视图同步、场景 bootstrap 或 debug probe 职责，不承载权威玩法规则。
- 输入必须经意图 / command buffer 进入 runtime tick，UI 回调不得直接修改权威状态。
- 固定帧或命令驱动 Demo 必须提供 replay hash、diagnostics 或等价确定性验证。
- 有可保存状态时必须接入 Runtime SaveState；暂不接入时要记录原因和补齐任务。
- 任务最终必须声明交付等级：`Framework Feature` / `Runtime Slice` / `Playable` / `Blocked`。
- 目标是 Playable 时，必须有可打开场景或入口、已绑定 UI/配置/资源，并完成 Play Mode 或 Unity MCP smoke 验证；否则只能标记为 Runtime Slice。
- 新增场景、Prefab、ScriptableObject 等 Unity 序列化资产优先通过 Unity Editor、Unity MCP 或现有 Editor 菜单生成，不手写 YAML。

## 5. 发布清单

CharacterTest 半即时卡牌 Task 1 的规则验收入口为 `TacticalCardEngineTests`，覆盖补齐至 3 张、手牌保留、弃牌回收/移除、动势边界与预留、单回合 Action 限额、重复提交/过期完成回执和预热后循环零分配；接入边界与证据见 [CharacterTest.TacticalCard](Interfaces/CharacterTest.TacticalCard.md)。该规则切片不替代后续时停、执行器和场景验收。

Task 3 执行桥接入口为 `TacticalExecutionAdapterTests`：验证 Unity 暂停/可配置流速、Prepare/Admit 拒绝保留预留、执行 ID/卡版本、真实 Session/命令/Gateway 自然完成、未起手清理等待回执、支撑接触点相对速度、空中与缺失采样拒绝停稳、正常补牌及异常暂停不补牌。必须同时回归 Task 1/2；场景全局时钟门控、HUD 与 1v1 三回合视觉验收属于 Task 4。

T4-B 入口为 `TacticalStageFlowCoordinatorTests`：双方落地稳定三 Tick 前不得初始化 Ready、缺失/过期 generation 不得开战、Story/Spawn 重复通知幂等、每个固定子步暂停门控、暂停十秒无追赶、暂停选卡/切换/取消/Utility/确认只提交一次、菜单暂停独立所有权、显式预算故障及授权恢复、Story 延迟派发与释放、bounded 命令/回执背压。必须同时回归原 71 项 TacticalCard 测试及受影响的固定帧测试；单元测试通过不替代 T4-C/D/E 的真实世界装配与可玩验收。

Task 2 的规则验收入口为 `TacticalTimeCoordinatorTests`，覆盖完整帧停稳、滞回、腾空顶点、非法速度、Tick 连续性、正常回合补牌、暂停原因所有权、挂起恢复、过期回执和预热后完整回合零分配。该纯 C# 适配端口验收不代表 Unity 世界时停或完整动作执行已经接通。

T4-C 入口为 `TacticalStageFlowTests`：当前正式 AttackPrimary 连续三回合的起手/Finished/释放/三 Tick 停稳/补牌链路；取消窗口与受击/中断不得完成；腾空、无有效接触、移动/旋转支撑和缺帧/倒序反例；玩家单侧停稳；权威命中奖励去重、同卡回洗版本和旧执行回执。必须同时回归全部 TacticalCard EditMode 测试。隔离 Runtime 与受控端口结果不替代 T4-D/E 的 Trigger 可靠交接及真实可玩场景证据。

T4-D 入口为 `MxFramework.Tests.TacticalStageEndTriggerTests`：原子双方 HP Signal、单方/双杀互斥、事务关闭双方终局绑定、第三稳定帧终局优先、Story 背压/关闭/语义拒绝、observer 溢出隔离、实体 generation/属性完整性、关键 producer/evaluation 漏跑、300 Tick 一次性保护、暂停命令恢复与旧会话清理隔离。必须回归既有 TacticalCard、GameplayTrigger 与受影响 Story tests；本次验证范围和基线对照证据见 `Docs/Interfaces/CharacterTest.TacticalCard.md`。真实场景与下一波 Spawn/清理装配仍由 T4-E 验收。

发布或提交前必须确认：

- `git status` 中没有意外的 `Library/`、`Temp/`、`Logs/`、`UserSettings/`、`.codex/`。
- 新增 `.meta` 文件与资产文件同时提交。
- 文档版本号已更新。
- `MIGRATION.md` 的批次状态与代码一致。
- 已记录无法自动验证的事项。
