# Audio 接口

> Status: Current
>
> 状态：Runtime Catalog + Audio Workbench 闭环已实现；Phase 17.1 foundation、Phase 17.2 Workbench 规划/捕获闭环、CharacterTest one-shot resolver bridge、受控 managed Event 管线与 Physical publish gates 已落地
> 来源：`Docs/AUDIO_SYSTEM_FMOD.md`
> 实现边界：`MxFramework.Audio` noEngine contract、`NullAudioBackend` 和基础测试已落地；`MxFramework.Audio.FMOD` 已引用官方 FMOD Unity Integration，并在 `MXFRAMEWORK_FMOD` 下编译真实后端路径；`AudioRuntimeModule` 和 FMOD Demo Runner 已接入 RuntimeHost。缺测试 bank / Demo bank 时仍未声明真实出声验收。
> Phase 17：物理交互双方声学属性、Basis Sound、Physical Catalog 和统一 resolver 的接受设计见 `Docs/AUDIO_PHYSICAL_EXPRESSION_DESIGN.md`；本页只把已经落地的类型列为 Current。
> UI feedback：noEngine feedback/outcome/policy registry/command route、binding contract v2、generated policy、Composer 只读展示和可选 `MxFramework.UI.Audio` bridge 已作为 Current 能力落地，见 `Docs/Interfaces/UI.md`；CharacterTest Settings 已完成首个 player-facing runtime composition 和真实 outcome 接入。正式可听 Click/Back、结果 Confirm/Error 仍缺四个可发布 Cue、Error Event 的完成/审听与 Unity 验收，目标设计见 `Docs/UI_AUDIO_FEEDBACK_AUTOMATION_DESIGN.md`。

## 职责

Audio 提供纯 C# 的音频意图、播放请求、句柄、Bus 状态、后端抽象和诊断快照契约。`MxFramework.Audio` 必须保持 `noEngineReferences=true`，不依赖 `UnityEngine`、`UnityEditor`、`FMODUnity` 或 `FMOD.Studio`。

FMOD 接入放在独立的 `MxFramework.Audio.FMOD` 适配层中。Gameplay、Combat、Ability、Buff、UI 等模块只通过 `IAudioService` 或项目层 cue mapper 表达音频意图，不直接持有 FMOD `EventInstance`，也不直接调用 `RuntimeManager`。

## 模块边界

| 模块 | 状态 | 依赖 | 职责 |
|------|------|------|------|
| `MxFramework.Audio` | Implemented | BCL only | 稳定 ID、请求、结果、句柄、定义 provider、后端接口、Null 后端、诊断 DTO |
| `MxFramework.Audio.FMOD` | Runtime Scaffold | Audio、Runtime、FMODUnity；程序集要求 `MXFRAMEWORK_FMOD` | FMOD event / bus / VCA 适配，提供 `AudioRuntimeModule`；未定义 symbol 时整个 adapter 程序集不编译，基础 consumer 使用 `MxFramework.Audio` / `NullAudioBackend` |
| `MxFramework.Audio.Physical` | Implemented | Audio、Combat、Core；noEngine | 保留 Combat contact one-shot 规则表，并提供双方声学事实、Basis 定义、确定性 resolver、response curve 和低频 trace |
| `MxFramework.Audio.Physical.Config` | Implemented | Audio.Physical、Newtonsoft.Json；noEngine | 启动阶段解析 `mx.audioPhysicalRuntimeCatalog.v2`；v1 仅整份转换为内存 Pair/Variant 索引 |
| `MxFramework.Audio.Config` | Implemented | Audio、Newtonsoft.Json；noEngine | 启动阶段解析 `mx.audioRuntimeCatalog.v1` 并构建 `AudioDefinitionCatalog` |
| `MxFramework.Audio.FMOD.Editor` | Implemented | Audio、Audio.FMOD、FMODUnityEditor、UnityEditor | FMOD snapshot 导出和 Audio Workbench Preview Bridge |
| `MxFramework.Authoring Audio Workbench` | Implemented | Authoring Core、FMOD snapshot、vanilla Web UI | 维护 Semantic v2、Generation v3 和 Physical v2 manifest，提供全局公共 Pair 矩阵、Basis/情境预览并发布双 Runtime Catalog |
| `MxFramework.Game.CharacterTest` Physical bridge | Implemented | Audio.Physical.Config、Audio Config、CharacterAction/Combat/project facts | 启动时校验成对 Catalog/hash；脚部 `BodyPartContact Started` 统一生成 Footstep/Step，显式 Landing 仅作兼容；ObjectImpact、WeaponHit/Block、AirDisplacement 走统一 resolver，目录缺失或损坏时整份 legacy fallback |
| `MxFramework.Demo` | Scaffolded | Audio、Audio.FMOD、Runtime | `FmodAudioDemoRunner`，用于填入 FMOD event / bus 后验证 one-shot、loop、parameter、bus volume |

依赖方向：

```text
Core
  <- Audio
      <- Audio.FMOD
          <- Demo / Preview.Runtime / project composition root

Runtime
  <- project composition root
      -> Audio.FMOD RuntimeModule
```

`MxFramework.Audio` 不引用 `Runtime`。需要生命周期和 Tick 的后端模块放在 `Audio.FMOD`，由 Unity composition root 注册进 `RuntimeHost`。

## 公开接口

| 接口/类型 | 状态 | 用途 |
|-----------|------|------|
| `AudioEventDefinition` | Implemented | EventId 到 FMOD path / guid、kind、bus、3D、loop、参数和 labels 的定义 |
| `AudioBusDefinition` | Implemented | Bus / VCA 稳定 ID 到 FMOD bus path / VCA path 的映射 |
| `AudioParameterDefinition` | Implemented | 参数 ID、名称、默认值和值域定义 |
| `AudioPlayRequest` | Implemented | 播放请求，包含 event、显式 2D/3D spatial mode、位置、emitter、caller-owned 参数、priority、trace 和 play mode |
| `AudioTransform` | Implemented | noEngine 位置和朝向数值，不暴露 `Vector3` |
| `AudioHandle` | Implemented | loop 或可控事件句柄，不暴露 FMOD 原生对象 |
| `AudioResult` / `AudioPlayResult` | Implemented | 非异常成功/失败结果和错误码 |
| `AudioErrorCode` | Implemented | `InvalidEvent`、`NotInitialized`、`InvalidParameter` 等结构化错误 |
| `IAudioService` / `AudioService` | Implemented | 业务侧入口，负责基础校验和后端转发 |
| `IAudioBackend` | Implemented | 可替换后端契约，首个真实后端为 FMOD |
| `NullAudioBackend` | Implemented | noEngine 测试、服务器和未安装 FMOD 环境的空后端 |
| `IAudioDefinitionProvider` | Implemented | Event / Bus / Parameter 定义查询入口 |
| `IAudioCueKeyDefinitionProvider` | Implemented | 通过 gameplay-facing `audioCueId` 查询 Cue，不暴露 FMOD 绑定 |
| `AudioDefinitionCatalog` | Implemented | 启动阶段防御性复制并构建 Cue/Event/Bus/Parameter 只读目录和字典，运行时查询 NoAlloc |
| `AudioDebugSnapshot` | Implemented | active events、bus states、recent errors 和计数 |
| `AudioRuntimeModule` | Implemented | RuntimeHost PostSimulation 阶段统一 tick `IAudioService` |
| `PhysicalAudioCueRule` / `PhysicalAudioCueRuleTable` | Implemented | `CombatPhysicalContactEvent` 到 audio event id、cooldown、priority 的 noEngine 映射表；key 支持 contact kind、body part、contact point、surface material、source slot 和 intensity |
| `PhysicalAudioPlaybackSystem` / `PhysicalAudioPlaybackSummary` | Implemented | 消费 `CombatPhysicalContactEventBuffer`，按规则和 cooldown 调用 `IAudioService.PlayOneShot` |
| `AcousticMaterialId` / `AcousticMaterialPairId` / `PhysicalAudioFamilyId` / `AudioBasisId` / `PhysicalAudioResponseProfileId` | Implemented | Physical Catalog 的稳定 typed int identity；无字符串运行时选择 |
| `AcousticBodyProperties` / `PhysicalAudioInteractionFact` | Implemented | 双方材质、归一化质量/面积、表面、构造和交互速度、压力、冲量、角度、独立单位接触法线、播放位姿等 noEngine 输入；`AudioTransform.Forward` 不复用为接触法线 |
| `PhysicalAudioContactDynamicsInput` / `PhysicalAudioContactDynamics` / `PhysicalAudioContactDynamicsResolver` | Implemented | NoAlloc 的 SI 单位接触动力学计算；支持脚点位移估算与解算前后速度差两类来源，并输出法/切向速度、有效质量、冲量、平均压力和入射角 |
| `IAcousticBodyPropertyProvider` | Implemented | 项目层 subject id 到声学属性的 NoAlloc 映射扩展点 |
| `AcousticMaterialProfileDefinition` / `PhysicalAudioRange` / `PhysicalAudioSelectionWeights` | Implemented | 归一化材质特征、适用范围锚点和确定性距离权重 |
| `AcousticMaterialPairDefinition` / `PhysicalAudioVariantKind` | Implemented | 全局无序材质 Pair 与 Impact/Step/Landing/Hit/Block/Sweep 动作身份 |
| `PhysicalAudioFamilyDefinition` / `PhysicalAudioBasisDefinition` / `PhysicalAudioResponseProfileDefinition` | Implemented | Family 选择 Variant/调制，Basis 实现 Pair×Variant，有界分段线性参数响应 |
| `IPhysicalAudioDefinitionProvider` / `PhysicalAudioDefinitionCatalog` | Implemented | 启动阶段构建候选索引；运行时整数查询 NoAlloc |
| `PhysicalAudioResolver` / `PhysicalAudioResolvedPlayback` | Implemented | 从 Started interaction 确定性选择唯一 Basis/Cue，并写入 caller-owned parameter span |
| `PhysicalAudioResolveTraceSnapshot` | Implemented | AllocByDesign 的低频候选、排除原因、fallback、得分和参数解释，不得在 Tick 调用 |
| `PhysicalAudioLatestFactCapture` / `PhysicalAudioLatestFactSnapshot` | Implemented | 固定容量、低频读取的最近成功播放 fact/result/parameter 诊断快照；供 Unity Editor capture bridge 使用，不参与 Gameplay authority |
| `PhysicalAudioRuntimeCatalogJson` | Implemented | LowFreqAlloc 的 v2 JSON loader；兼容整份 v1 迁移输入，不合并其他定义源 |
| `AudioDebugSource` | Implemented | 将 `AudioDebugSnapshot` 与 presentation drain 统计接入 Diagnostics 的只读、LowFreqAlloc debug source；CharacterTest Debug UI 已消费 |

建议首版服务契约：

```csharp
public interface IAudioService
{
    AudioPlayResult PlayOneShot(in AudioPlayRequest request);
    AudioPlayResult StartEvent(in AudioPlayRequest request, out AudioHandle handle);
    AudioResult Stop(AudioHandle handle, AudioStopMode stopMode);
    AudioResult SetParameter(AudioHandle handle, int parameterId, float value);
    AudioResult SetBusVolume(int busId, float volume);
    AudioResult SetBusMuted(int busId, bool muted);
}
```

建议首版后端契约：

```csharp
public interface IAudioBackend
{
    AudioResult Initialize(IAudioDefinitionProvider definitions);
    AudioPlayResult Play(in AudioPlayRequest request, out AudioHandle handle);
    AudioResult Stop(AudioHandle handle, AudioStopMode stopMode);
    AudioResult SetParameter(AudioHandle handle, int parameterId, float value);
    AudioResult SetBusVolume(int busId, float volume);
    AudioResult SetBusMuted(int busId, bool muted);
    AudioDebugSnapshot CaptureSnapshot();
    void Tick(float deltaTime);
    void Dispose();
}
```

定义查询契约：

```csharp
public interface IAudioDefinitionProvider
{
    bool TryGetEvent(int eventId, out AudioEventDefinition definition);
    bool TryGetBus(int busId, out AudioBusDefinition definition);
    bool TryGetParameter(int eventId, int parameterId, out AudioParameterDefinition definition);
}
```

## 使用约定

- `AudioEventDefinition.Id` 使用稳定 int，建议 ID 空间为 `500000-599999`；`Name` 只用于调试。
- 音频配置保存 `AudioEventDefinition.Id`、FMOD event path / guid、bus path、参数名和必要的 Unity 资源 key，不保存场景对象引用。
- `FmodEventGuid` 优先用于运行时解析；`FmodEventPath` 用于 Editor 可读性和 fallback。
- FMOD event path / guid 不是 generic `ResourceKey`；Gameplay、Combat、Ability、Buff、UI 等模块通过 audio event id 或项目层 cue mapper 引用声音。
- `Assets/Config/MxFramework/Audio/audio_semantic_manifest.json` 是项目级 Audio Authoring Manifest v2；Cue 表达“为什么播放”，Event Definition 表达“播放哪个 FMOD event”，多个 Cue 可以共享 Event Definition。
- FMOD snapshot 是技术证据：event path / guid、bank、参数、2D/3D、loop、stream、长度和 stale 诊断都来自 snapshot。语义清单不能替代 FMOD Studio、Unity import flow 或 bank 构建。
- 发布生成 `Assets/MxFrameworkGenerated/Audio/audio_runtime_catalog.json` 和 `Assets/StreamingAssets/MxFramework/Audio/audio_runtime_catalog.json`；package `audio_cue_manifest` v2 是带 global catalog hash 的 Cue 子集，运行时继续兼容 v1。
- `MxFramework.Audio` base contract 不依赖 Combat；物理接触音频桥接必须放在 `MxFramework.Audio.Physical` 或项目组合根中。
- `PhysicalAudioPlaybackSystem` 只消费 `CombatPhysicalContactPhase.Started` 的接触事实；`Stayed` / `Ended` 默认作为诊断事实忽略，不触发 one-shot。
- `PhysicalAudioContactDynamics` 是 PresentationOnly 数据，不扩展 `CombatPhysicalContactEvent`，也不进入 Replay、SaveState、网络或 Gameplay/Combat 确定性 hash；项目桥通过同索引固定缓冲把 contact event 与 dynamics 关联。
- `PhysicalAudioCueRuleTable` 中 `SurfaceMaterialKind.Default`、`sourceSlotId=0`、`bodyPartId=0` 和 `contactPointId=0` 作为 fallback 规则；具体材质、slot、body part 和 contact point 规则优先。
- `PhysicalAudioPlaybackSystem.Tick(...)` 是高频路径，构造后规则查找和 cooldown 判断为 `NoAlloc after initialization`；cooldown key 包含 kind、pair、slot、contact point、body part 和 event id，首次出现的新 key 可能在预设容量不足时扩容。
- `CombatPhysicalContactKind.BodyPartContact` 是通用部位接触事实；脚、爪、手、膝、尾巴或拖地武器都只是项目规则表中的 body part / contact point 映射。`PhysicalAudioPlaybackSummary.BodyPartContactPlayedCount` 统计新语义；``PhysicalAudioPlaybackSummary` 不提供专用脚步计数字段；脚底声音由项目层的 sole contact point 规则映射到 `BodyPartContactPlayedCount`。
- CharacterTest LCC-M4 只在 `CharacterBodyPartContactPointConfig` stable id 与 geometry v6 的显式 `AttachmentKind.ContactPoint` 同时存在时，把同一 resolved authority pose channel 投影为 Physical Audio 的 PresentationOnly motion fact；缺失或无效 attachment 保持 baked/static base，不读取 Unity Animator、bone 或 Transform 作为 authority，也不进入 replay/hash/save/network。正式 Dragon14 当前为 3 个 PresentationOnly ContactPoint config、0 个 authority-pose ContactPoint attachment；这些点可以使用 base/presentation motion 产生接触音频与调试事实，但不消费 terrain resolved delta，也不产生角色阻挡、推挤或伤害。
- CharacterTest LCC-M5 的 Started/Stayed/Ended appendage phase 是从 gameplay soft-contact constraint 派生的 PresentationOnly 事实；它不把既有 `body_part_contact_points` 音频球升级为 authority，也不回写 Combat、network、SaveState 或 replay。当前候选没有把这些 phase 接到正式 appendage Cue/VFX；因此头尾推出已存在不等于已有可听接触音效，现有 3 个 Dragon14 ContactPoint 仍维持 LCC-M4 所述的独立表现职责。
- Unity `AudioClip` 样例可使用普通 Resource Catalog 和 `ResourceTypeIds.AudioClip`；FMOD bank `.bank` 文件不作为普通 `ResourceCatalogEntry`，也不伪装成 `AudioClip`。
- `IsLoop=true` 的 event 必须通过 `StartEvent` 返回 handle，并通过 `Stop` 释放。
- `PlayOneShot` 默认不返回可持有 handle；需要后续控制时使用 `StartEvent`。
- `AudioPlayRequest.Create2D` 只能请求 2D Event，`Create3D` 只能请求 3D Event；空间模式与 Event Definition 不匹配时返回 `RequestRejected`，禁止把 3D Event 静默放到世界原点。
- `AudioPlayRequest.Parameters` 是 caller-owned 的只读视图，构造请求和提交后端均为 `NoAlloc`；调用方必须保证该集合在 `PlayOneShot` / `StartEvent` 返回前不变。需要跨帧保存的系统必须复制到自己拥有的固定缓冲区。
- Event/Cue/Catalog 构造是 `LowFreqAlloc`：输入数组会在初始化阶段防御性复制，公共集合只暴露只读视图；调用方不能通过保留源数组修改运行时定义。
- 初始参数和 controlled parameter 更新会在进入 backend 前检查 event-local ID、有限数值、定义值域、离散/标签参数整数值以及同一请求内重复 ID；失败返回 `InvalidParameter`。
- `Stop(handle, stopMode)` 幂等；重复停止或释放应返回结构化结果并写入诊断，不抛异常。
- 3D event 必须提供 emitter 绑定或 `AudioTransform`；缺失时按配置返回失败或降级到 listener 位置。
- Unity 侧 `AudioEmitterBinding` 可绑定 `Transform`、`GameObject`、`Rigidbody` 或 `Rigidbody2D`，但这些类型不能进入 `MxFramework.Audio`。
- Bus volume 范围统一为 `0..1`；mute 与 volume 分开记录。
- 音频失败默认不打断 gameplay 帧；Editor / CI 校验中缺失 event、parameter、bus、bank 应失败。
- 音频不参与 Runtime result hash；Replay 可记录 command 摘要用于诊断，也可以忽略实际音频输出。
- `AudioDebugSnapshot` 是低频调试接口，不作为高频状态同步入口。
- Gameplay / Combat 源码不得直接引用 `FMODUnity` 或 `FMOD.Studio`。

## Audio Authoring 与发布链

```text
Audio Authoring Manifest v2
Generation Manifest v3       -> validate / atomic save
Physical Authoring Manifest v2
  -> mx.audioRuntimeCatalog.v1
  -> mx.audioPhysicalRuntimeCatalog.v2
  -> package audio_cue_manifest v2 subset
  -> project composition root / PhysicalAudioResolver
  -> IAudioService / FMOD
```

- Authoring truth：`Assets/Config/MxFramework/Audio/audio_semantic_manifest.json`。
- FMOD technical evidence：`Assets/MxFrameworkGenerated/Audio/fmod_audio_library.json`。
- Runtime catalog：Generated 与 StreamingAssets 两份镜像必须内容一致。
- Physical authoring truth：`Assets/Config/MxFramework/Audio/audio_physical_manifest.json`；其 Generated/StreamingAssets 输出为 `audio_physical_runtime_catalog.json`，并用 Global Audio Catalog hash 联接 Cue/Event 目录。
- `idAllocation` 为 Cue、Event、Bus、Parameter 维护四套单调递增 ID；删除的 ID 进入 retired 列表，不复用。
- 保存草稿只写 Authoring manifest，允许 Draft 未完成；三份 manifest 同时提交时进入同一原子事务。正式发布分别校验 Semantic、Generation、Physical expected hash，snapshot 必须非 stale，所有 Active Cue 必须 Ready。Audio Workbench 另提供开发发布全部：保持三份 expected hash、FMOD snapshot 和 Cue→Event 技术解析门禁，将全部非 Archived Cue 编入成对 Runtime Catalog；未批准、未采用状态只保留为诊断，不会伪造或修改 Authoring 审批状态。创作负责人也可显式执行“全部批准”；该操作以 `BulkOperatorApproval` 记录批次批准方式和当前 Event/snapshot hash，区别于逐 Cue 真实试听证据。
- Runtime activation 表示 Generated 与 Player 的 Global/Physical Catalog pair 已完整联接且镜像一致；它与正式内容发布门禁分离。未完成的 Basis adoption/audition 继续阻止严格发布，但不应把一个可被 CharacterTest 加载的已联接 Catalog pair 标记为 `LegacyFallback`。
- Active Basis 可在 Workbench 直接采用当前 FMOD Event：有原始素材时创建或更新 `OriginalBaseline` adoption set 并记录 source/hash；Event 当前为空时创建 `CurrentEventState / EmptyEvent` 静音占位。两者都标记 `Applied`、绑定 Basis，并以批量负责人批准更新当前 Bank/snapshot 证据。该开发期快速激活流程不要求先生成候选，静音占位和原始素材后续都可被新 adoption set 替换。
- Workbench 必须区分候选、FMOD 应用和游戏同步状态。Cue 状态投影使用 `CandidateOnly`、`FmodApplied`、`BankOutOfSync`、`SilentPlaceholder`、`Synced`；只有 `Applied` set 的 `BankHash` 与 `StreamingAssets` Bank hash 相等，且 `activeSetId == publishedSetId`、Generated/Player Catalog mirror 一致时，才能显示“已同步游戏”。
- “一键同步到游戏”发布当前可解析 Cue 与 Physical Catalog，并仅为 Bank hash 已匹配的 Applied set 写入 `publishedSetId`。未选择候选不会被自动采用，Bank 不匹配的版本保持待同步并在结果中计数。
- `Approved` / `Rejected` 必须关联成功的 Preview Bridge request、当前 Event binding hash、snapshot hash 和时间；Active Basis 的 Approved 证据还必须匹配 Basis string/runtime ID 与当前 Applied adoption set。证据会记录 `PhysicalManifestHash` 供审计，但它不参与审批有效性判断；Bank hash 一致性由既有 adoption diagnostics 独立校验。参与有效性判断的绑定变化会退回 `ReviewNeeded`。
- CharacterTest Editor 优先读 Generated catalog，Player 读 StreamingAssets mirror。诊断明确区分 `GeneratedLinked`、`LegacyFallback` 和 `Invalid`，并显示 Global/Physical manifest hash 与联接 hash。Editor 文件缺失、损坏或 hash 不匹配时记录 warning 并整份使用 legacy fallback；禁止把两份目录自动合并。官方 CharacterTest Player 构建入口会在四份目录不完整、mirror 不一致、解析失败或 hash/link 无效时预检失败，不把无效目录带入 Player。
- JSON 解析、字典构建、Cue/Event/Parameter 名称解析均只发生在启动阶段；Physical/Weapon Tick 路径继续 `NoAlloc after initialization`。
- Resource Manager Audio 页只读汇总并深链 Audio Workbench，不再提供第二套保存入口。
- Workbench 不创建或修改未知外部 FMOD Studio Event；显式 managed 操作只允许在 `event:/MxFramework/Generated/Physical/<family>/<basisId>` 受控命名空间中按模板创建 Basis/Cue/Event。该管线校验 expected hash，自动执行 FMOD 脚本、构建 Desktop Banks、同步 runtime mirror、请求 Unity 刷新和校验 Snapshot，成功后才原子写入 Semantic/Physical manifest，失败时回滚。对已有 Event 的 source set 替换继续复用同类自动应用管线。完整任务流程见 `Docs/AUDIO_WORKBENCH_GUIDE.md`。
- `Tools/MxFramework.AudioGenerationHost` 与生成 brief/job/candidate/provenance/adoption DTO 均为 Authoring-only；不修改 `MxFramework.Audio`、`IAudioService` 或 Runtime Catalog 公共结构。
- 生成候选只在 Host/项目 `Temp` 中流转；正式采用写 `Assets/Audio/MxFramework/Generated` 和 generation manifest。FMOD adoption、Unity Preview、人工 Approved 是顺序门禁，任何自动评分都不能替代人工审听。
- `ReverbLike` 属于 Authoring-only 软门禁：Workbench 可显式请求 Host 计算最短尾音渐隐、重新渲染并执行削波/静音/时长/尾部能量复检；只有返回 `Ready` 的修复结果才会解锁并勾选，原始尾音指标、修复后指标与渐隐时长保留为证据。硬错误候选不进入该修复入口。
- generation Recipe 按 layer 区分 `takes`（生成候选数）与 `adoptionTakes`（FMOD 采用目标）；每层 `1–12`、单 Job 最多 36 个，旧 `adoptionCountPerLayer` 仅作为迁移输入。
- Footstep、Landing、Object Collision、Weapon Hit 的 generation Recipe 使用 Authoring-only `collisionProfile` 描述碰撞体、受体和共同接触过程；双方材质、质量、接触面积、表面和构造，以及强度、速度、角度、方式均以稳定选项 ID 持久化。旧 `material/weight/intensity` 只作为迁移输入和兼容投影，不进入 Runtime Catalog。
- `collisionProfile` 仍是 Generation Recipe，不会自动成为运行时权威。用户通过显式 promote 把 adoption set 和一份不可变双方物理描述写入 Physical Manifest。
- 当前 Authoring Core 已实现 Physical v1→v2 一次迁移、公共无序 Pair/Variant validate/compile、typed ID 区间与退休 ID 不复用、Required Pair×Variant coverage、低频 resolve trace 和 adoption set → Basis 草稿提升；保存后输出 v2，Runtime v1 只作为整份兼容输入。
- 当前 seed response 只发布 `500251 MxPhysicalVolume` 与 `500252 MxPhysicalPitch`，FMOD backend 分别通过 `EventInstance.setVolume` / `setPitch` 原生应用，不要求 Event-local parameter。Family 若声明其他必需参数，publish 会用 FMOD snapshot 检查对应 Event 是否真实暴露该参数；snapshot 尚不证明 automation/DSP 绑定正确，因此 LowPass/Brightness/Scrape 仍不进入当前 runtime response。
- CharacterTest 只在 Global Audio Catalog 与 Physical Catalog 同时有效且 hash 匹配时启用统一 resolver；配置脚部碰撞点产生的 `BodyPartContact Started` 统一生成 `Footstep/Step`，不切换为专用 Landing 事实，通用 resolver 仍兼容显式 Landing 输入。普通脚步从脚点逐帧位移、接触法线与单脚有效质量估算动力学；空中落地用角色解算前后速度差计算总冲量，并只分配给当帧实际 Started 的脚。首次已接地、配置刷新、传送、帧不连续或姿态来源切换只重建运动基线并静音。原始冲量以 `200 N·s`、压力以 `100 kPa` 在 CharacterTest 表现桥归一化；Footstep response 的 `MxPhysicalVolume` 由 `Impulse` 驱动，`MxPhysicalPitch` 仍由 `RelativeSpeed` 驱动。ObjectImpact、WeaponHit/Block 与 AirDisplacement 已接入，参数随 intent 进入现有 Audio 播放链。Footstep Family 选择公共 Pair 的 `Impact` Variant；四个已有可听 sole seed 已迁入 `audio.family.object_impact`，作为公共材质 Pair Basis 使用，稳定 Cue key 仅为兼容保留，不再拥有独立 Step 选择分支。批量生成的 0 ms Impact Basis 保持 Draft，禁止这些静音占位进入游戏；既有 AirDisplacement seed 仍是 0 ms，作为正式发布前的独立内容阻断保留。组合根在角色 spawn 时从实体物理量缓存 Body/Foot，并缓存默认 katana Weapon profile；Combat hit/contact surface 仍是 receiver 材质权威。动态装备切换、身体部位、Collider/地面属性刷新尚未完整接入。AirDisplacement 消费 CharacterAction 的挥击峰值表现事实，不伪造 Combat Collider 命中。任一目录缺失、损坏或 hash 不匹配时整份使用 legacy fallback，不合并规则。Audio Workbench 的“运行时激活”汇总以 Authoring Core 为权威，依次展示 Active adoption、Unity/FMOD 审听、coverage、原子发布和 CharacterTest 加载；浏览器不自行推导 Ready。当前 checkout 已生成并同步成对 Physical Runtime Catalog，18 个 Active Basis 均绑定当前 Applied 版本，coverage 无缺失或歧义；CharacterTest 实际加载 `GeneratedLinked`。正式内容发布仍独立受 Cue 主观审听门禁约束。
- AirDisplacement 拥有独立的 `AudioBasisId`、Cue runtime ID `500304` 和 Event runtime ID `500314`；正常目录与 legacy fallback 都不会把它改写为 Light Attack 命中 Cue。Cue/Event/Basis lifecycle 已预分配为 Active，但当前受控 FMOD Event 只完成空 OneShot/3D 技术骨架，`contentStatus` 仍为 Draft、adoption set 为空、审听为 Needed，发布门禁会继续阻止它进入可交付 runtime catalog。
- 当前 Workbench 顶层提供“Cue 与素材 / 公共材质矩阵”；公共矩阵显示 14 种 Active 材质的 105 个上三角 Pair、Variant、Required/Ready/fallback 状态，Cue/Basis 只保留引用与深链。游戏情境解析、Unity fact capture、sweep/热图和权威预览均运行同一 resolver。

## FMOD 后端约定

- `FmodAudioBackend` 是唯一直接调用 FMOD Unity Integration 的运行时适配层。
- One-shot 可在后端内部使用 `RuntimeManager.PlayOneShot`，但业务层不直接调用。
- Loop、蓄力、引导、aura、环境声等可控事件使用 `CreateInstance -> start -> handle table -> Stop -> release`。
- `AudioHandle.Id` 由后端分配，映射到内部 `EventInstance` 表。
- `AudioRuntimeModule` 默认运行在 `RuntimeTickStage.PostSimulation`，使同一帧 gameplay 先产生命令，再统一转为音频输出。
- Bank 首期使用 FMOD Settings 自动加载 + 显式 warmup。当前 demo 的 `FMOD/.../Build/Desktop/Master*.bank` 是版本化 authoring build output / release input，`Assets/StreamingAssets/Master*.bank` 是版本化 runtime mirror；Catalog 化 bank 后续再接入 Resources。
- `Assets/Plugins/FMOD/Cache/Editor/FMODStudioCache.asset` 当前是版本化的 FMOD Unity generated metadata，用于记录 Editor 可见 bank、event、guid 和路径；刷新后可提交，但不能把它当作手写配置源。
- `Audio.FMOD` 可以依赖 Runtime 以提供 `AudioRuntimeModule`，但 `Audio` contract 不依赖 Runtime。

## 诊断约定

`AudioDebugSnapshot` 建议包含：

```text
Initialized
TotalPlayRequests
TotalStopRequests
ActiveEventCount
BusStates[]
ActiveEvents[]
RecentErrors[]
```

Debug 面板和导出工具只能读取 snapshot，不读取 `FmodAudioBackend` 私有 handle table。

## 测试入口

状态：M1 tests implemented，M2 runtime scaffold verified。

建议测试入口：

```text
Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Audio/
```

M1 noEngine 契约测试已覆盖：

- `NullAudioBackend` 可初始化、播放、停止并输出 snapshot。
- 未注册 parameter 返回 `AudioErrorCode.InvalidParameter`。
- loop event 必须返回有效 handle。
- one-shot loop event 被 `AudioService` 拒绝，要求走 `StartEvent`。
- bus volume clamp / validation 行为稳定。
- `AudioService` 不依赖 Unity 或 FMOD 类型。
- 定义 `MXFRAMEWORK_FMOD` 且存在 FMODUnity 时，`FmodAudioBackend` availability 测试验证真实 adapter 初始化；未定义时由 UPM base Consumer Smoke 验证 FMOD 程序集及其依赖闭包被排除。源码中的 `BackendUnavailable` 分支不再代表基础 consumer 可引用该程序集。
- `AudioRuntimeModule` 测试覆盖默认 PostSimulation stage、从 Runtime service registry 取 `IAudioService` 并转发 tick。
- `PhysicalAudioPlaybackSystemTests` 覆盖接触 kind/material/intensity 到 event id 的规则解析、3D one-shot 请求、pair/slot cooldown、missing rule / missing service skip 和 `Stayed` phase ignore。
- `PhysicalAudioResolverTests` 覆盖双方严格锚点、priority/distance/stable Basis ID 决胜、family fallback、参数缓冲区不足、低频 trace 和初始化后批量 resolve 零托管分配。
- `PhysicalAudioRuntimeCatalogJsonTests` 覆盖 Physical Runtime Catalog 启动解析、hash/format 校验和无效 JSON 拒绝。
- Authoring focused tests 覆盖 seed manifest 编译与 coverage、Authoring/runtime 同构选择 fixture、typed ID 退休/不复用、三 manifest hash/原子保存、原生 Volume/Pitch 证据豁免、非原生 FMOD 参数存在性证据、Basis string/runtime ID 与 adoption set 审批绑定、Physical manifest hash 审计字段，以及 Applied adoption set 的 Basis 提升门禁。

M2 FMOD 后端测试建议覆盖：

- FMOD package 缺失时不影响 `MxFramework.Audio` 编译。
- 已安装 FMOD 时可创建 `MxFramework.Audio.FMOD` asmdef 并编译。
- one-shot、loop start / stop、parameter、bus volume 可通过 fake 或最小 bank 验证。
- `FmodAudioDemoRunner` 已可通过 Inspector 填 event path/guid、bus path/VCA path、parameter name；真实播放仍要求 FMOD bank。
- listener / emitter binding 不把 Unity 类型泄漏到 noEngine contract。
- FMOD event / bus / parameter 缺失返回结构化错误并进入 snapshot。

## 索引接入状态

当前状态：已接入 `Docs/INTERFACES.md`。
