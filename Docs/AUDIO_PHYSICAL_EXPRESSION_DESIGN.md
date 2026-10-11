# 物理交互驱动音频表达设计

> Status: Design
>
> Date: 2026-07-14
>
> Implementation status: Physical Authoring/Runtime Catalog 已升级到 v2 公共无序材质 Pair 模型；Phase 17 one-shot resolver、CharacterTest bridge、Workbench 公共材质矩阵与运行时激活面板、Unity 真实事实捕获、sweep/热图、managed Event、发布门禁、Catalog mode/hash 诊断和 CharacterTest Player preflight 均已接入。v1 只作为整份迁移输入，启动时合成为 Pair/Variant 索引，不与有效 v2 或 legacy CharacterTest 规则混合。当前内容仍待用户完成 Active Cue/Basis 的采用与真实试听批准后原子发布；动态装备切换、身体部位、Collider/地面属性刷新和 FMOD automation evidence 仍待后续。

## 1. 目标

游戏音效中的脚步、落地、物体碰撞、武器命中、格挡和武器破空，本质上都由“两个声学对象在某种接触过程中交换能量”产生。音频编辑器的目标不是只生成一份孤立 WAV，而是创作一组能被运行时正确选择、调制、试听和追溯的基础音效。

核心链路固定为：

```text
游戏中的双方属性与交互事实
  -> PhysicalAudioInteractionFact
  -> PhysicalAudioResolver
  -> Basis Sound + playback parameters
  -> AudioPlayRequest / IAudioService
  -> FMOD Event 内分层与 take 微变化
```

全局公共 `Material Pair` 是两种材质的无序身份；`Family` 只表达动作语义和调制；`Basis Sound` 是一个 `Pair × Variant` 的可播放实现，对应一个 gameplay Cue 和一个 FMOD Event。Event 内部可以包含 transient/body/tail 轨道和多个 take；采用版本只是该 Basis 的不可变素材历史，不创建新的 gameplay 身份。

第一阶段只覆盖 one-shot：Footstep、Landing、Object Impact、Weapon Hit、Weapon Block 和 Air Displacement。Scrape、Roll、持续挤压及其 loop 生命周期进入 Phase 18。

## 2. 权威边界

| 层 | 拥有内容 | 不拥有内容 |
| --- | --- | --- |
| Gameplay / Combat | 命中、动作、部位、速度、压力、冲量等事实 | FMOD path、Basis 选择、音高和音量公式 |
| 项目组合根 / property provider | 武器、身体部位、Collider、装备和地面到声学属性的映射 | 音频后端对象 |
| `MxFramework.Audio.Physical` | 双方声学事实、Basis 选择、fallback、响应曲线和解析 trace | Combat authority、Unity/FMOD 类型 |
| `MxFramework.Audio` | `AudioPlayRequest`、参数和播放服务 | 碰撞业务语义 |
| FMOD Event | 分层、take 微变化、空间化和 DSP automation | 根据 gameplay 属性选择不同语义 Basis |
| Audio Workbench | Semantic/Generation/Physical 三份 authoring truth、预览和发布门禁 | 自动艺术批准、Gameplay authority |

音频始终是表现层，不写回 Combat、SaveState、网络状态或 Runtime result hash。为调试和重放可以记录 `PhysicalAudioInteractionFact`、选中 Basis 和最终参数摘要，但 FMOD Event 内的 take 微随机不进入权威 hash。

## 3. 声学数据模型

### 3.1 稳定 ID

Physical Audio 使用五套独立的 typed int ID：

- `AcousticMaterialId`
- `AcousticMaterialPairId`
- `PhysicalAudioFamilyId`
- `AudioBasisId`
- `PhysicalAudioResponseProfileId`

ID 单调递增，删除后进入 retired 集合且不得复用。字符串名称只用于 Authoring、日志和诊断，运行时选择不依赖字符串。

### 3.2 双方声学属性

`AcousticBodyProperties` 同时描述 impactor 和 receiver：

- 声学材质；
- 质量等级或归一化质量；
- 接触面积；
- 表面粗糙度；
- 实心、空心、薄壳、软填充等构造；
- 必要时由材质 profile 补充硬度、密度、阻尼和共振。

声学材质目录独立于 `CombatSurfaceMaterialKind`。Combat 枚举保持当前碰撞判定职责，项目 provider 显式映射到更细的声学材质；不能为了音频反向扩大 Combat authority。

空气不是虚构的 Collider。Air Displacement 使用独立 interaction kind，并把 receiver 表达为稳定的 Gas/Air medium profile；来源对象仍提供材质、质量、尺寸和运动速度。

### 3.3 交互事实

`PhysicalAudioInteractionFact` 是只读、noEngine 的单次交互输入，至少包含：

- interaction kind / mode / phase；
- impactor 和 receiver 声学属性；
- relative、normal、tangential speed；
- pressure、impulse、angle 和 intensity；
- playback position/orientation、独立的单位接触法线、frame、sequence 和 emitter identity。`AudioTransform.Forward` 只描述发声器朝向，不能承载接触几何；`AirDisplacement` 没有接触面时，法线字段明确保存扫掠/排空气流方向。

当前 CharacterTest adapter 把配置脚部碰撞点产生的 `BodyPartContact Started` 统一生成为 Footstep/Step 事实，接触速度只进入响应参数，不再把高速脚部接触改写为专用 Landing；通用事实和 resolver 仍兼容显式 Landing 输入。Object Impact、Weapon Hit/Block 与 CharacterAction 挥击峰值驱动的 Air Displacement 同样生成该事实；成功进入播放队列的事实和 resolver 结果会写入固定容量的 `PhysicalAudioLatestFactCapture`，供 Unity Editor Bridge 低频读取。这个捕获器不反向参与 gameplay，也不会在高频路径构造 JSON；`CombatPhysicalContactEvent` 本身不增加 FMOD 或 Audio 类型依赖。

### 3.4 公共 Material Pair、Variant、Family 与 Basis

`Material Pair` 是全项目公共的无序材质对。按稳定 Material ID 规范化后，`Metal × Wood` 与 `Wood × Metal` 只分配一个 Pair ID；同材质 Pair 固定由 impactor 映射 Side A、receiver 映射 Side B。运行时原始事实仍有方向，质量、面积、速度和响应参数不会因为 Pair 无序而丢失。

Pair 内使用 `Impact / Step / Landing / Hit / Block / Sweep` 动作 Variant。一个 Variant 可以包含多个 Basis，分别覆盖轻重、速度、面积和角度范围。

一个 `PhysicalAudioFamily` 把 interaction kind/mode 映射到固定 Variant，并定义选择权重、冷却、优先级、响应曲线和必需 FMOD 参数。Family 不拥有材质矩阵，也不复制 Pair。

一个 `Basis Sound` 定义：

- 唯一的 Cue runtime ID；
- 当前 adoption set 和绑定证据；
- `materialPairId + variantKind`；
- 规范化 Pair Side A/B 声学锚点；
- 质量、面积、速度、角度和强度适用范围；
- lifecycle、审听和 binding hash。

响应曲线是有界分段线性曲线，把有效能量、速度、擦碰比例、硬度和阻尼映射为 volume、pitch 及可选 FMOD parameters。曲线编译后只包含数值点，不在 Tick 中查名称或解释 JSON。

## 4. Authoring 与 Runtime Catalog

项目级 Physical Authoring truth：

```text
Assets/Config/MxFramework/Audio/audio_physical_manifest.json
schema: mx.audioPhysicalAuthoringManifest.v2
```

它包含 `idAllocation`、带显式 fallback 的 `materialProfiles[]`、公共 `materialPairs[]`、`families[]`、`basisSounds[]`、`responseProfiles[]`、场景预设和 Required Pair×Variant 覆盖要求。Generation Recipe 的有向 `collisionProfile` 继续服务于素材生成；只有显式“提升为基础音效”时，才规范化为公共 Pair 并把不可变物理描述复制进 Physical Manifest。

发布输出：

```text
schema: mx.audioPhysicalRuntimeCatalog.v2
Assets/MxFrameworkGenerated/Audio/audio_physical_runtime_catalog.json
Assets/StreamingAssets/MxFramework/Audio/audio_physical_runtime_catalog.json
```

Runtime Catalog 只包含稳定 ID、预编译索引、范围、曲线、Cue runtime ID 和 Global Audio Catalog hash；不携带 Prompt、Recipe、WAV 路径、候选指标或 provenance。

Semantic、Generation 和 Physical 三份 manifest 使用独立 expected hash，并在一次保存事务中原子写入。旧客户端未提交 Physical Manifest 时保持原有 Semantic/Generation 保存行为，但不能发布需要 Physical Catalog 的新 Basis。

## 5. 运行时解析

`PhysicalAudioResolver.TryResolve(...)` 固定执行：

1. 根据 interaction kind/mode 取得 Family 和固定 Variant。
2. 按稳定 Material ID 规范化原始有向双方材质，并查找精确公共 Pair。
3. Pair 缺少该 Variant 时沿双方显式 material fallback 链搜索；按总深度、最大单侧深度、稳定 Pair ID 排序并去重。
4. 在命中 Pair×Variant 的 Basis 中排除质量、面积、速度、角度和强度越界项并计算归一化加权距离。
5. 按 Family priority、Basis priority、匹配分、稳定 `AudioBasisId` 选择唯一结果。
6. 使用 response profile 计算并钳制播放参数，再解析 Cue runtime ID 到 Event ID。

相同输入和 Catalog 必须产生相同 Basis、fallback 路径和参数。解析 trace 需要能回答：输入事实、被排除候选及原因、得分、最终 Basis、fallback 和参数值。

JSON、字典和 Cue/Event/Parameter 联接只在启动阶段执行。Resolver、Contact Tick 和 Weapon/Air bridge 使用整数、数组、调用方提供的 `Span<AudioParameterValue>` 和预设容量；缓冲区满时计数并安全跳过，不在 Tick 中扩容。

## 6. Audio Workbench

工作流升级为：

```text
定义交互 -> 生成并应用素材 -> 提升为基础音效
  -> 配置覆盖范围 -> 模拟游戏属性 -> Unity 真实试听 -> 发布
```

Workbench 顶层分为“Cue 与素材”和“公共材质矩阵”。公共矩阵对 14 种 Active 材质显示 105 个上三角无序组合，并按 Variant、Required、缺失、fallback-only 和 Ready 筛选。Cue/Basis 页面只显示所属 Pair/Variant 与深链，不再维护私有材质矩阵。

Workbench 增加四个互相联动的创作面：

- **基础音效库**：Basis、Cue/Event、当前素材版本、双方物理描述和审听状态。
- **公共材质矩阵**：Pair×Variant、Required、Ready 和显式 fallback；显示覆盖空洞、部分动作和共享引用。
- **选择与调制**：selector 权重、冷却、fallback 和响应曲线。
- **游戏情境预览**：调整真实游戏属性，显示选中 Basis、得分、trace、Event 和最终参数；支持从 Unity 捕获事实、单参数 sweep 和二维热图。

浏览器多层混音仍是快速近似。权威预览通过 Unity Bridge 加载请求对应的临时 Runtime Catalog，调用与游戏相同的 `PhysicalAudioResolver`，核对 Basis/Cue/hash 后播放真实 FMOD Event；只有该结果可以形成 Approved 证据。

当前工具还提供两类低频分析，不在浏览器复制 runtime 选择规则：

- “从 Unity 捕获真实交互”读取最近一次成功进入 Physical Audio 播放队列的 fact/result/parameter snapshot；没有真实来源、材质 ID 无法唯一反向映射或 Unity 未连接时，返回明确的 unavailable/failed 状态，不生成模拟事实。
- 单参数 sweep 与二维覆盖热图把每个采样事实提交到 Authoring server 的 `/audio/physical/resolve`；单次最多 64 点、并发 4。结果记录 Basis、得分、fallback、未命中和参数，并在 Physical Manifest 或基准事实变化后标记 stale。

新建 Basis 可以通过显式 managed 操作在受控命名空间创建 Basis/Cue/Event：

```text
event:/MxFramework/Generated/Physical/<family>/<basisId>
```

Authoring server 校验 Semantic/Physical expected hash、单调分配 Basis/Cue/Event ID，并从显式模板继承 Bank、Bus、3D 和距离事实。随后依次备份 FMOD/Bank/Snapshot、运行固定 FMOD 脚本、构建 Desktop Banks、镜像 StreamingAssets、请求 Unity 刷新并校验 snapshot；成功后才原子写入 Semantic/Physical manifest，失败则回滚。模板化创建只管理该命名空间，不创建或修改未知外部 Event，也不与普通素材 adoption 任务并发。

预分配的 Basis 也可以在不改变 Basis/Cue/Event runtime ID 的前提下原地 materialize。当前 Light Attack 的 Air Displacement 使用独立身份 `audio.basis.air_displacement.light_attack` / `sfx.air_displacement.light_attack` / `event:/MxFramework/Generated/Physical/audio.family.air_displacement/audio.basis.air_displacement.light_attack`，不再借用 `sfx.light_attack` 的命中 Event。Cue/Event/Basis lifecycle 已预分配为 `Active`，受控 Event 也已建立 OneShot/3D/Bank/距离技术骨架；但素材仍为空、`metadata.contentStatus` 仍为 `Draft`，且没有 Applied adoption set 或人工审听，因此不能发布，也不能把它描述为已完成的破空音效。

## 7. FMOD 与发布门禁

C# 选择一个完整 Basis Event；FMOD 在该 Event 内负责 transient/body/tail、Single/Multi Instrument、空间化和 take 微变化。一次 one-shot 交互只启动一个 Event。

Volume/Pitch 使用后端已有直接映射。LowPass、Brightness、Scrape 等可选参数只有在 FMOD snapshot 证明参数和 automation 存在时才能进入必需参数集合；缺失时发布失败，不能把运行时静默忽略描述成已生效。

当前 seed runtime response 只启用 `500251 MxPhysicalVolume` 与 `500252 MxPhysicalPitch`，由 FMOD backend 原生 `setVolume` / `setPitch` 应用，不要求 Event-local parameter。Authoring publish 会检查其他必需参数是否出现在对应 Event 的 FMOD snapshot；Active Basis 的批准有效性绑定 Basis string/runtime ID 与当前 Applied adoption set，Bank hash 由既有 adoption diagnostics 独立校验。证据仍记录 Physical manifest hash 供审计，但该字段不参与审批有效性判断。snapshot DTO 尚不能证明 automation/DSP 连接，因此 LowPass/Brightness/Scrape 与 automation evidence 仍是 17.4 未完成项。

Physical 发布必须同时满足：

- 三份 manifest 已保存且 hash 当前；
- 必需场景全部可解析，无覆盖空洞、fallback 环或同分歧义；
- 当前 Basis adoption set 为 Applied；
- FMOD Build、StreamingAssets mirror、snapshot 和 Event binding 一致；
- Unity/FMOD 人工批准匹配当前 Basis string/runtime ID 与 adoption set；Bank hash 由 adoption diagnostics 独立校验，Physical manifest hash 仅保留为证据审计字段。

有效 v2 Physical Catalog 整份优先。v1 Catalog 只在启动时整份转换成合成 Pair/Variant 索引；缺失或损坏时才允许 CharacterTest 使用旧硬编码目录，并输出明确 warning。三者不得自动合并。

## 8. 公共矩阵素材制作基线

公共素材库的首轮目标覆盖为实体材质的无序 `Impact`、实体材质与空气的 `Sweep`，以及 Landing、Hit、Block seed。Footstep 不再维护独立的 Step 素材分支：它保留 Step 触发语义，但通过鞋底与地面材质 Pair 选择公共 `Impact` Basis。已有 4 个可听 sole seed 已迁入 ObjectImpact family 作为公共 Pair Basis，稳定 Cue key 仅为兼容保留；批量生成的 0 ms managed Impact Basis 保持 Draft。当前运行时发布 18 个有 Applied 采用版本的 Active Basis；其中既有 Air Displacement seed 仍为 0 ms，是正式发布前必须补齐的独立内容阻断。`Auto` 只参与显式 fallback；`Air × Air` 为静音，不创建伪音效。

首轮使用 wg200 上固定 revision 的 Stable Audio 服务，每个启用 layer 生成 3 条候选并由人选择 1 条。Impact、Step、Sweep 默认 BodyOnly；Landing/Hit 使用 TransientBody；Block 使用 TransientBodyTail。单样本采用生成 Single Instrument，响应变化交给 runtime response profile。批处理入口为：

```bash
python3 Tools/MxFramework.AudioWorkbench/scripts/physical_matrix_pipeline.py plan
python3 Tools/MxFramework.AudioWorkbench/scripts/physical_matrix_pipeline.py prepare
python3 Tools/MxFramework.AudioWorkbench/scripts/physical_matrix_pipeline.py calibrate
python3 Tools/MxFramework.AudioWorkbench/scripts/physical_matrix_pipeline.py generate --scope impact --calibration-approved
```

批处理只负责 Pair/Basis/Recipe 物化和候选生成，不采用、不批准、不发布。校准批次必须先由人确认材质辨识和整体声音方向；随后才显式传入 `--calibration-approved` 分 Family 继续。Seed 由制作计划 revision、Basis、Variant 和 Recipe revision 稳定派生。

新制作 Recipe 使用自然衰减时长策略。Stable Audio 的 5 秒输出只是稳定生成画布，材质与动作决定最大窗口；Host 根据 10ms RMS 包络寻找起音和持续 50ms 的自然结束点，不把短促碰撞填满到窗口。软质 Impact 默认 0.22 秒、普通硬质 0.30 秒、脆性 0.38 秒、颗粒 0.42 秒、金属短共振 0.45 秒、魔法能量 0.50 秒，Sweep 默认 0.45 秒。未在上限内衰减或窗口末端能量过高的结果标记为 `ReverbLike` 并禁止采用；人工快捷剪辑不能绕过该门禁。旧 Recipe 和 Job 保留 `LegacyFixed`，历史条件不被静默改写。

## 9. 分阶段落地

- **17.0 文档与 ADR**：固定心智模型、边界和验收。
- **17.1 Catalog/Compiler/Resolver**：Physical v2 manifest、公共 Pair/Variant runtime catalog、双侧 fallback 启动索引、统一响应曲线和测试。
- **17.2 Workbench**：公共 105 单元上三角矩阵、Pair inspector、Cue 深链、Basis 库、selector/curve、场景模拟、Unity 真实事实捕获、单参数 sweep 和二维热图已落地。
- **17.3 Runtime one-shot**：Object Collision、Weapon Hit/Block、Footstep/Landing 和 Air Displacement 已在有效成对 Catalog 下接入统一 resolver。CharacterTest 的脚部 `BodyPartContact Started` 统一生成 Footstep/Step，不从 grounded transition 或接触速度合成 Landing；显式 Landing 仍是通用 resolver 的兼容输入。Footstep 的 Step 事实复用公共 Pair 的 Impact Variant；四个稳定 sole Cue key 对应的可听 seed 已迁入 ObjectImpact family，不再形成独立 Step 分支。CharacterTest 组合根在角色 spawn 时从实体物理量缓存 Body/Foot，并缓存默认 katana Weapon profile；Air Displacement 消费 CharacterAction 在挥击峰值发出的表现事实并配对稳定 Air/Gas profile。Debug UI 报告 `GeneratedLinked` / `LegacyFallback` / `Invalid` 与成对 hash；官方 Player 构建对不完整或失联目录提前失败。当前 checkout 已同步 Generated/Player Catalog pair，18 个 Active Basis 均有 Applied 版本且 coverage 完整；批量生成的 0 ms Impact Basis 保持 Draft，既有 Air Displacement 0 ms seed 仍需补齐，正式内容发布的 Cue 主观审听门禁仍独立保留。动态装备切换、部位、Collider/地面属性刷新仍待续。
- **17.4 FMOD/发布**：三 manifest 保存/发布 hash、原生 Volume/Pitch、非原生参数存在性、Basis ID/runtime Basis ID/adoption set 审批绑定、独立 Bank adoption diagnostics，以及受控 managed Basis/Cue/Event 创建和完整失败回滚管线已接；Physical manifest hash 仅随证据记录供审计。LowPass/Brightness/Scrape automation evidence 待续。
- **Phase 18**：仍未接入游戏事实生产链。下一步先定义稳定 contact identity，再接 Started/Stayed/Ended producer、Scrape/Roll/挤压 loop、移动 emitter、voice arbitration 和持续参数更新；不得直接复用逐帧变化的 contact event sequence 作为持续 voice identity。

## 10. 验收

- 新增或替换 Basis 后，不修改 Gameplay C# 即可改变实际选择和播放。
- Workbench 与 Unity runtime 对同一事实选择相同 Basis 和参数。
- 任意播放都能输出可解释 trace。
- 高频路径无字符串查询、JSON、LINQ、临时集合和托管分配。
- 不安装 FMOD 时 noEngine resolver 仍可编译和测试；音频不会改变 Combat、Replay 或 Runtime authority。
