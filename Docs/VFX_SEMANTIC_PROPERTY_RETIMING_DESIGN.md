# VFX 语义属性管理与精确重定时开发设计

> Status: Current
>
> Accepted target: 2026-07-15
>
> Implementation scope: ParticleSystem v3 authoring workstation; remaining fail-closed boundaries are listed in section 0.2.
>
> 本文是 VFX 手动组装中“语义属性、成员绑定、精确重定时、预览编译、Candidate 烘焙和运行时属性驱动”的唯一详细设计事实源。VFX 资源语义、Primitive/Recipe/Published Effect、Piloto 原材料库与 Development Agent 总体边界仍以 [VFX 美术资产创作与语义管理设计](VFX_ASSET_AUTHORING_DESIGN.md) 为准；已实现 Runtime 契约仍以 [VFX 接口](Interfaces/Vfx.md)、源码和测试为准。
>
> 本文同时记录已经落地的 ParticleSystem v3 工作台和仍待完成的 fail-closed 边界。`mx.vfx.composition-draft.v3`、95 项受控 Property Registry、内容寻址 Source Snapshot、Profile 基础设施、六种 Retime Mode、完整 MinMaxCurve/MinMaxGradient、四页 Inspector、成员矩阵、事务保存/恢复/三方重映射、Candidate v3 receipt、Preview/Bake 编译等价证据、烘焙 Layer Scheduler 与 MaterialPropertyBlock 属性轨道已经进入 Current。这里的“95 项”只表示 Registry 合同已登记，不表示 95 项在任意素材上都可写；真实可写能力必须同时具备 Profile Binding、Adapter 和 UI/Raw 编辑入口，并通过模块安全检查。自动建议 Profile 可以直接用于 Candidate 烘焙；用户点击烘焙即接受本次解析结果，系统把 Profile hash、字段 diff、源证据与编译 hash 冻结到 Candidate receipt。将建议发布成跨草稿共享 Profile 是独立、可选且带影响分析的操作。Trails 已完成完整快照和首个 `PreserveNormalizedLifetime` 重定时合同切片，但任意 Trail 属性编辑、SubEmitter/Collision 以及高耦合 Limit Velocity 仍未完整实现；VFX Graph Adapter 与像素级 Golden 自动审批也未进入 Current。任何 Current 声明都必须以本节状态矩阵、源码和 focused tests 的交集为准。

## 0. 实现状态与事实边界

### 0.1 当前已落地的 ParticleSystem v3 工作台

| 能力 | 状态 | 当前事实 |
| --- | --- | --- |
| Draft schema | Current | 手动组合保存为 `mx.vfx.composition-draft.v3`；四个 Fireball/Lightning 示例已事务迁移，使用整数帧、`timing`、稀疏 `semanticOverrides`、成员参与和 Profile 依赖。 |
| v2 迁移 | Current | 仅默认粗倍率可自动迁移；先保存原始字节和 receipt，再临时文件验证与原子替换。发现非默认 `playbackSpeed/emissionScale/lifetimeScale/sizeScale/colorTint` 时整份草稿 fail-closed。 |
| Property Registry | Current contract | 95 项受控属性覆盖 Timing、Appearance、Emission、Motion、Shape、Render；每项固定 ID、类型、单位、时间域、允许运算、推荐范围和 backend 能力。Registry 只定义可讨论的属性集合；没有 Profile Binding 或 Adapter 的属性必须禁用或 fail-closed，不能把 Registry 数量当成可写覆盖率。 |
| Source Snapshot/Profile | Current infrastructure / curation pending | 工作台按选中 Element 延迟抓取完整 ParticleSystem/Renderer/Material/Custom Data 快照并写内容寻址压缩 Blob。AutoSuggested Component/Shader Profile 可直接参与预览和 Candidate 编译，本次解析结果由 receipt 冻结，不要求先写入 Manifest。成员页仍显示 Component/Shader/Element 的共享发布覆盖率；用户可选择发布当前成员或整个 Element 为仓库共享 Profile，发布采用影响分析、expected hash、批事务和回滚。只有持久化共享 Profile 才要求 Manifest 与 Profile 文件双方均为 `Confirmed`。“复制缺口报告”生成不含时间戳和本机路径的 `mx.vfx.profile-gap-report.v1`，分别列出真实编译阻断、共享发布缺口与能力限制，但绝不改变评审状态。 |
| 精确重定时核心 | Current supported set | `PlaybackWindow/ExtendBody/ExtendTail/RepeatBody/EventRemap/WholeSpeed/Fixed` 七种模式。`PlaybackWindow` 只调度 Layer 播放窗口，不修改子 VFX 的 duration、Burst、延迟、生命周期或速度；其余模式继续使用 Pre-roll/Entry/Body/Exit/Cleanup、事件锚点、最小 Body、周期吸附、单调分段映射和切线修正。复合成员默认跟随 Layer 主模式，可在成员页显式选择独立模式；未知值 fail-closed。 |
| ParticleSystem Adapter | Current supported set | Main、Emission、Shape、Color/Size/Velocity/Rotation over Lifetime、Force、Noise、Texture Sheet Animation、Custom Data 的已声明字段可从源快照精确编译；MinMaxCurve/MinMaxGradient 保留完整模式、Key、切线、权重和 Gradient mode。Trails 已完整记录 lifetime、width、color、空间、ribbon 和清理字段；已确认 `PreserveNormalizedLifetime` 合同允许 `ExtendTail` 缩放主粒子 Start Lifetime，同时保持归一化 Trail lifetime、width/color 不变。运动语义修改仍会阻断。SubEmitter/Collision 等继续只读。 |
| Renderer/Material Adapter | Current supported set | ParticleSystemRenderer 的 mode/alignment/sort/pivot/size/material/mesh 可类型化编辑；UberFXSG Shader Profile、Vertex Stream/Custom Data 校验、MaterialPropertyBlock 动态值、纹理/RenderQueue/Cull/ZWrite/Blend 派生 Material 和关键字已接入。源 Material 永不原地修改。 |
| 工作台 Inspector | Current supported set | `语义/成员/原始/差异` 四页、可编辑 CurveField、完整 MinMaxCurve 面板、Source/Semantic/Final 三层曲线、成员 × 属性参与矩阵、成员独立 Retime、原始 Unity 字段和稳定 Diff 已进入 UI Toolkit。语义页可明确选择“Layer 主控”或具体成员作为编辑目标；成员未设置的属性继承 Layer，同属性成员值替换 Layer 值，清除后恢复继承。Layer 时间分段始终属于整层合同，不会随成员编辑目标切换。成员卡区分“候选绑定”与“共享绑定”；发布按钮随编辑目标切换为当前成员或整个 Element。成员页的逐属性能力矩阵把 95 项合同实时分类为共享绑定、部分成员、候选绑定、只读、不支持或预留；相同结果反向控制语义控件，缺 Binding/Adapter 的控件禁用，候选绑定允许预览编辑与 Candidate 烘焙并由 receipt 冻结。材质局部时间和六类贴图槽仍只有在对应 Shader Binding 存在且验证通过时可编辑。 |
| Draft 保存与恢复 | Current | 保存执行 expected file hash、临时文件、roundtrip、原子替换和回滚；未保存会话写入 Git 忽略恢复日志。磁盘与会话同时变化时使用 base/disk/session 三方合并，数组或同字段冲突显式列出。 |
| stale 重映射 | Current | Source Index、Curation、Registry、Profile Manifest 或成员变化后生成逐 Layer 重映射计划；仅稳定成员 ID/唯一等价成员可自动映射，不确定项阻断，应用前再次检查全部 hash。 |
| Preview 增量刷新 | Current | 自动预览可关闭；关闭时只记录 dirty 与恢复日志，不反复重建。开启后只从只读源重新编译；相机、角色、Socket、当前帧、显隐/Solo 和其它 Layer 独立保持。 |
| Candidate Bake | Current | Receipt 使用 `mx.vfx.composition-bake-receipt.v3`；记录 Profile/Registry/源依赖、字段 Diff、Preview/Bake compile hash、源完整性前后 hash。任一编译不等价或 Piloto 源变化即删除输出并回滚。 |
| 烘焙调度与属性轨道 | Current | 一个 `UnityVfxBakedLayerScheduler` 统一执行 Layer 起止帧；`UnityVfxBakedPropertyDriver` 使用缓存 Renderer、Shader Property ID 与 MaterialPropertyBlock 驱动 float/color 曲线。初始化后 Tick 不查找组件。 |
| Fixture/性能证据 | Current contract | 七个版本化 Fixture 覆盖 Glow、火焰、烟雾、符文周期、多 Burst、Flash、UberFX；仓库已包含字段编译、Preview/Bake 等价、成员覆盖与耦合模块门禁的 focused EditMode/PlayMode 覆盖，Scheduler/PropertyDriver 4096 次稳态调用已有 0 managed bytes 证据。具体通过数量必须以当前 Unity Test Runner 结果为准，不在设计文档中固定。 |
| Runtime/GamePlay 边界 | Unchanged | Draft、Profile、Snapshot、曲线编辑和 Preview Context 不进入 SaveState、网络或 gameplay hash；Runtime 只消费烘焙 Prefab、阶段合同、Layer schedule 和属性轨道。 |

### 0.2 仍属于 Design、不得提前宣称完成

- Trails 的通用属性编辑仍未完成；Current 只包含完整 Snapshot/原始展示和经确认的 `PreserveNormalizedLifetime` 时间重定时合同。SubEmitter、Collision 与高耦合 Limit Velocity 行为仍需要专用可写语义 Adapter；当前在无法证明安全时阻断相关操作。
- Profile 基础设施已经具备，但当前仓库尚未提交任何共享 Component/Element/Shader Profile。实际编辑和 Candidate 烘焙可以使用自动建议并由 receipt 冻结；只有希望跨草稿复用同一套规则时才需要发布到 Manifest，自动建议仍不能被描述为已经策展的共享能力。
- 除 UberFXSG 外的 Shader 仍需建立经过 Adapter 校验的明确绑定；字段名相似不能自动建立 opacity、dissolve、local time 或 Custom Data 语义。自动建议只有在绑定与实际材质属性、Vertex Stream 和 Custom Data 合同均验证通过时才能进入 Candidate，发布为共享 Shader Profile 则是后续可选操作。
- 七个 Fixture 已有字段级 Preview/Bake 等价、源 hash 和确定性编译证据；跨暗色/亮色/复杂/同色冲突背景的像素级视觉 Golden 仍由版本化质量证据流水线生成，不能用字段等价替代审美评审。
- 全部约 200 个精选 Element 的共享 Profile 策展不是启动前批量硬要求；被编辑成员可以使用通过安全校验的候选绑定完成预览和 Candidate 烘焙，只有需要跨草稿复用时才发布共享 Profile。
- VFX Graph 正式语义 Adapter；当前只保留接口和 fail-closed 能力声明。

### 0.3 Current 与 Design 的判定规则

本文件后文为了保存完整产品合同，会同时描述最终 UI 和全部 Adapter。出现“必须”“应当”不自动表示源码已经具备该能力；实现状态以 0.1/0.2、`Docs/CAPABILITIES.md`、`Docs/QUALITY_GATE.md`、源码与测试为准。若状态冲突，采用更保守的结论并阻断晋升。

### 0.4 2026-07-16 实现审计与本轮补齐

本轮从 Registry、Profile、Compiler、Adapter、Inspector、Bake 和测试重新逐层核对，结论如下：

| 审计项 | 结论 | 本轮处理 |
| --- | --- | --- |
| Registry 与真实可写能力 | 不能按 95/95 宣称完成 | 明确改为 `Registry ∩ Profile Binding ∩ Adapter ∩ UI/Raw ∩ safety validation` 的交集；未绑定控件禁用或编译阻断。当前静态审计中，95 个 Registry ID 有 94 个进入类型化 Inspector/曲线入口，唯一没有 UI 的 `render.material.profile` 是未实现的预留合同；ParticleSystem 自动 Component Profile 声明 61 个原生 Binding，阶段分段由 Retime Compiler 另行处理。UberFXSG 自动建议会提出 14 个 Shader 语义 Binding，它们可作为候选级绑定进入 receipt，但只有发布后才计入共享 Profile 覆盖。以上数字是结构覆盖，不是素材级可写率。 |
| 成员级属性覆盖 | 原实现会由 Adapter 读取，但 Compiler 只校验 Layer 级覆盖，且 Inspector 没有成员值编辑入口；“Override”只是状态标签，无法完成实际配置 | 语义页新增 Layer/成员编辑目标。成员覆盖对同 Property ID 使用替换优先级，其余属性继承 Layer；清除成员值恢复继承。写入成员值时同步记录属性级 `Override`，仅标记 `Override` 却没有独立值时产生 `VFXSEM023`。成员覆盖执行与 Layer 相同的 Registry、运算、值、范围、Binding、只读和 Primary Opacity 校验；未知成员、重复作用域/属性及 `Unsupported/Unconfirmed/Excluded` 状态均稳定阻断。 |
| 成员级 Retime | 草稿协议已有 `memberOverrides[].retimeMode`，但 Adapter 仍统一读取 Layer 模式 | 成员页增加“跟随 Layer/独立模式”选择；Compiler 校验成员模式，Adapter 和耦合模块门禁使用成员最终模式。 |
| SubEmitter/Trails/Collision/Limit Velocity | SubEmitter 曾被作为无条件 blocker，导致未修改源效果也无法正常预览；其它耦合模块缺少操作粒度门禁 | 未修改源效果允许预览；Trails 增加结构化 Snapshot、原始字段展示、source Trail hash/dieWithParticles 合同校验和 `PreserveNormalizedLifetime` 的 ExtendTail 精确应用：主粒子 Lifetime 延长，Trail 的归一化 lifetime、width/color 保持不变；改变 speed/lifetime/travelRange 的任意语义修改仍阻断。SubEmitter/Collision 继续按操作粒度 fail-closed；`motion.drag` 的精确写入仍标记 Experimental，直到动力学 Fixture 通过。 |
| Shader 贴图与局部时间 | Adapter/派生材质路径已有部分能力，但 Inspector 缺少明确入口 | 增加 Main/Mask/Noise/Ramp/Normal/Flipbook 槽与材质局部时间控件；只有 Shader Profile 明确声明 Binding 的槽可编辑，未知槽保持禁用。 |
| 持久化 Profile 策展 | 基础设施完成，但 Manifest 当前为空 | 不自动把 AutoSuggested 写成 Confirmed。它不再阻断 Candidate；当前缺口只影响跨草稿共享、集中维护和复用推荐。 |

本轮没有把尚未完成的 Profile 策展、像素级 Golden、VFX Graph 或高耦合模块 Adapter 伪装成 Current。后续完成度必须以实际 Manifest 条目、Fixture 和 focused Unity tests 更新本表。

## 1. 背景与问题定义

### 1.1 当前粗倍率为什么不够

当前 Layer 编辑把一个复杂 VFX 近似成少量统一倍率：

- `playbackSpeed`
- `emissionScale`
- `lifetimeScale`
- `sizeScale`
- `colorTint`
- `startOffsetSeconds`

这些字段适合快速预览，但不能表达真实美术意图。以 `layer_01_glow` 为例，用户把 Layer 的显示长度从 90 帧延长到 180 帧时，不能简单把所有时间乘以 2：

- 12 帧淡入若变成 24 帧，亮点出现会变钝。
- 18 帧淡出若变成 36 帧，尾部会拖沓。
- Burst 若按比例移动，原本锁定在第 0 帧或命中帧的事件会错位。
- 粒子生命周期、发射窗口、材质 Dissolve、UV Flipbook 和 Noise 可能处于不同时间域，不能共用一个缩放因子。
- 复合 Element 中 Glow、Core、Sparks、Smoke 可能需要不同的延长机制。
- 在已经修改过的结果上继续乘倍率会累积误差，无法稳定还原源数据。

因此，“Layer 长度”必须是语义合同的结果，而不是反向粗暴修改所有 Unity 字段的原因。

被替换的 v2 粗倍率行为记录如下，只供 v3 迁移和回归测试使用：

| v2 字段/操作 | 当前 Unity 写入 |
| --- | --- |
| 拖动 Layer 时间条右侧手柄 | 用“目标显示时长 / 当前估算时长”继续乘 `lifetimeScale`，范围约束为 `0.05–3`。 |
| `playbackSpeed` | 对该 Element 下每个 ParticleSystem 执行 `main.simulationSpeed *= playbackSpeed`。 |
| `startOffsetSeconds` | 把每个 ParticleSystem 的 `startDelay` 改为 `constantMax + startOffsetSeconds`。 |
| `lifetimeScale` | 对每个 ParticleSystem 执行 `main.startLifetime.curveMultiplier *= lifetimeScale`。 |
| `sizeScale` | 对每个 ParticleSystem 执行 `main.startSize.curveMultiplier *= sizeScale`。 |
| `colorTint` | 统一 Tint 每个 ParticleSystem 的 `main.startColor`。 |
| `emissionScale` | 只对 `emission.rateOverTime.curveMultiplier` 乘倍率，不覆盖 Rate over Distance 或 Burst。 |
| `sortingOrderOffset` | 对每个 ParticleSystemRenderer 的 sorting order 加偏移。 |
| 当前时长估算 | 取各 ParticleSystem `(startDelay.constantMax + duration + startLifetime.constantMax) / simulationSpeed` 的最大值并换算为 60 FPS 帧。 |

这条 v2 路径没有识别 Entry/Body/Exit、Burst、MinMaxCurve 时间域、材质时间、复合成员职责或只读模块，所以已经退出 Current 编辑路径；它只用于识别迁移输入和建立“旧行为不应继续扩张”的历史基线。

### 1.2 目标

本设计要建立以下完整链路：

```text
只读 Source Snapshot
  → Component / Element / Shader Authoring Profile
  → Composition Draft v3 稀疏语义覆盖
  → Semantic / Retime Compiler
  → 隔离 Preview 实例
  → 稳定 Unity 字段 Diff
  → Candidate Prefab + Bake Receipt v3
  → 生命周期控制器与烘焙属性驱动
```

具体目标：

- 使用者通过“淡入 12 帧”“主体持续 120 帧”“发射密度 1.25”“世界重力 3 m/s²”“发光 +1.5 EV”等可理解属性创作。
- 编译器把语义操作精确映射到 ParticleSystem、Renderer、Material 和 Custom Data 字段。
- 时间变化保持 Entry/Exit、关键事件、完整周期和清理行为，不产生累计倍率误差。
- 原始 Unity 字段、曲线、Gradient、材质属性和最终差异始终可审计。
- Piloto 源资产保持只读；每次预览和烘焙都从绑定的源快照重新编译。
- 复合 Element 能声明成员如何参与宏控，而不是把同一倍率盲目应用到所有成员。
- 预览结果与 Candidate 烘焙结果使用同一编译器和同一输入 hash。
- Authoring 可以分配；烘焙后的生命周期和属性驱动在初始化后保持 NoAlloc。

### 1.3 非目标

- 不把所有 Unity ParticleSystem 字段都包装成“简单滑杆”。原始页继续完整展示技术事实。
- 不承诺任意素材可以安全变成任意长度；缺少能力或会破坏结构时必须阻断。
- 不修改 Piloto Studio 源 Prefab、材质、贴图、Mesh、Shader Graph 或 `.vfx`。
- 不让 Runtime 读取 Draft、Profile、Snapshot、Recipe 或 Source Index。
- 不新增 Gameplay 公共参数 API，不改变 SaveState、网络或 gameplay authority。
- 第一版不正式实现 VFX Graph 语义编辑，只保留 Adapter 扩展点。
- 自动分析、视觉评分和 Development Agent 建议不替代 Profile 确认或人工 Ready 审批。

## 2. 设计原则

### 2.1 语义优先，原始字段可见

“语义”不是隐藏底层细节，而是提供稳定、可理解的主控。每个主控必须能追溯到：

1. 使用了哪个 Profile Binding。
2. 读取了哪些 Source Snapshot 字段。
3. 采用了 Multiply、Add、Remap 还是显式 Replace。
4. 最终修改了哪些 Unity 字段。
5. 哪些成员不参与、只读或不支持。

### 2.2 永远从源快照重新编译

任何编辑都不得在上一次编译结果上继续乘倍率。固定顺序为：

```text
Source Snapshot
→ Component Profile defaults
→ Element Profile macro responses
→ Layer semantic overrides
→ Member participation / member overrides
→ Experimental overrides
→ Final Unity field set
```

Undo、Redo、重开草稿、切换 A/B 和烘焙都使用同一顺序，从而避免累计误差。

### 2.3 默认非破坏，Replace 必须显式

默认运算只有：

- `Multiply`：按 Profile 响应曲线乘源值。
- `Add`：按单位增加可解释的偏移。
- `Remap`：通过单调映射改变时间或值域。
- `PaletteMap`：把语义调色板映射到已确认的颜色绑定。

`Replace` 会丢弃源表达，必须在详情中显式选择，并进入醒目的 Diff 与 Review 诊断。

### 2.4 时间域必须明确

每个可编辑值必须声明时间域：

- `LayerFrame`：Layer 的 60 FPS 艺术时间。
- `PhaseFrame`：Build/Rebuild/Loop/Complete/Dissipate 的阶段时间。
- `EmitterNormalized`：发射器 0–100% 归一化时间。
- `ParticleNormalized`：单粒子生命周期 0–100% 时间。
- `CycleLocal`：已确认周期内的局部帧。
- `MaterialLocalTime`：由 Authoring Adapter 提供的材质局部时间。
- `Static`：不随播放变化。

没有明确时间域的曲线不能参与自动重定时。

### 2.5 能力必须由 Profile 声明

扫描器可以提出绑定建议，但只有已确认 Profile 才能赋予语义编辑能力。未知 Shader 属性、Custom Data、Vertex Stream 或复杂模块不能靠名称猜测后直接写入。

### 2.6 预览与烘焙同源

Preview 和 Bake 必须调用同一个 Semantic Compiler、Retime Compiler、Adapter 集合与 canonical hash 逻辑。Preview 不能有一套“看起来对”的临时算法，Bake 再使用另一套 Unity 写入逻辑。

## 3. 术语

| 术语 | 定义 |
| --- | --- |
| Source Snapshot | 对一个源组件及其允许依赖的完整、只读、内容寻址技术快照。 |
| Property Registry | 受控语义属性 ID、类型、单位、时间域、运算和后端能力目录。 |
| Component Profile | 单个 ParticleSystem/Renderer/Material 组合的语义绑定和时间分段。 |
| Element Profile | 单层或复合 Element 的成员参与规则、宏控响应、调色板和预算合同。 |
| Shader Profile | Shader 属性、Custom Data、Vertex Stream、渲染状态和材质派生合同。 |
| Semantic Override | Draft 中对受控属性的稀疏修改。 |
| Retime | 在保持时间合同和事件锚点的前提下改变艺术长度或节奏。 |
| Artistic Duration | `Pre-roll + Entry + Body + Exit`；不含纯技术 `Cleanup`。 |
| Cleanup | 停止发射后为粒子、Trail 或材质残留保留的技术清理区间。 |
| Member | 复合 Element 内的单个源 ParticleSystem/Renderer 单元。 |
| Primary Opacity Binding | 一个成员唯一的主透明度控制入口。 |
| Experimental | 超出 Profile 推荐范围或使用未完全确认能力的可预览/可烘焙候选状态；不可晋升。 |

## 4. 总体架构与分配边界

```text
┌──────────────────────── Authoring-only / AllocByDesign ────────────────────────┐
│ Source Scanner → Snapshot Store → Profile Store → Property Registry            │
│                                         │                                      │
│ Composition Draft v3 → Semantic Compiler + Retime Compiler → Stable Diff       │
│                                         │                                      │
│                          Preview Adapter / Unity Bake Executor                  │
└─────────────────────────────────────────┬──────────────────────────────────────┘
                                          │ Candidate Prefab + Receipt
┌──────────────────────── Presentation Runtime ──────────────────────────────────┐
│ UnityVfxLifecycleController + baked Layer schedule + baked property tracks      │
│ NoAlloc after initialization; no Draft/Profile/Snapshot lookup                  │
└─────────────────────────────────────────────────────────────────────────────────┘
```

分配分类：

| 组件 | 分类 | 约束 |
| --- | --- | --- |
| Snapshot 扫描、压缩、解压 | `AllocByDesign` | Editor 低频；按需加载，不阻塞首屏。 |
| Profile/Draft load、validate、compile、diff | `AllocByDesign` | Editor 低频；结果可缓存。 |
| Preview 重建与定点重模拟 | `AllocByDesign` | 15–30 FPS 节流，只重建受影响成员。 |
| Candidate Bake | `AllocByDesign` | 显式用户操作；只通过 Unity Editor API。 |
| Lifecycle Tick | `NoAlloc after initialization` | 不做 LINQ、组件查找、集合创建、字符串拼接或反射。 |
| Baked Property Driver Tick | `NoAlloc after initialization` | 缓存数组、Shader Property ID、MaterialPropertyBlock 和曲线引用。 |

## 5. 事实源、文件布局与权威边界

以下路径是目标布局，落地前均视为 Planned：

```text
Temp/MxFramework/VfxAuthoring/Snapshots/
  manifest.json
  blobs/<sha256>.json.gz

Assets/Config/MxFramework/Vfx/Profiles/
  manifest.json
  Components/<componentProfileId>.json
  Elements/<elementProfileId>.json
  Shaders/<shaderProfileId>.json

Assets/Config/MxFramework/Vfx/Compositions/
  <draftId>.json

Temp/MxFramework/VfxAuthoring/Recovery/
  <draftId>.journal.json

Assets/VFX/MxCandidates/Manual/<draftId>/
  <candidate>.prefab
  receipt.json
```

| 事实 | 权威来源 | 是否提交 | 是否进入 Runtime |
| --- | --- | --- | --- |
| Unity 原始模块、曲线、Gradient、材质、Mesh、Shader 属性 | Unity asset + Source Snapshot | Unity 源资产提交；Snapshot 忽略 | 只通过烘焙 Prefab |
| 语义属性定义与运算规则 | Property Registry | 是 | 否 |
| 单成员语义绑定 | Component Profile | 是 | 否 |
| 复合成员宏控与参与规则 | Element Profile | 是 | 否 |
| Shader/CustomData/VertexStream 合同 | Shader Profile | 是 | 否 |
| 当前作品修改 | Composition Draft v3 | 是 | 否 |
| 未保存恢复状态 | Recovery Journal | 否 | 否 |
| 最终 Unity 字段与依赖证据 | Bake Receipt v3 | Candidate 阶段忽略；晋升后按发布策略 | 否 |
| 最终播放内容 | Published Effect Prefab | 是 | 是 |
| Gameplay 触发、Anchor、实例身份 | VfxDefinition/Trigger/Runtime | 是 | 是 |

Snapshot、Profile、Draft、Diff、预览证据和缩略图均不得进入 Runtime hash、SaveState、正式网络 snapshot 或 gameplay authority。

## 6. Property Registry

### 6.1 Registry 合同

Registry 固定属性 ID、显示信息、数据类型、单位、时间域、允许运算、推荐范围和 backend 能力。属性 ID 一旦进入提交的 Draft/Profile 不得改名；废弃时保留 alias/migration，而不是静默复用旧 ID。

代表性结构：

```json
{
  "schemaVersion": "mx.vfx.property-registry.v1",
  "registryId": "mxframework.vfx.properties",
  "properties": [
    {
      "propertyId": "timing.entry.frames",
      "displayName": "淡入长度",
      "group": "Timing",
      "valueType": "Int",
      "unit": "Frame60",
      "timeDomain": "LayerFrame",
      "operations": ["Replace"],
      "minimum": 0,
      "backendCapabilities": ["ParticleSystem", "MaterialAdapter"]
    },
    {
      "propertyId": "appearance.emission.exposureEv",
      "displayName": "发光强度",
      "group": "Appearance",
      "valueType": "Float",
      "unit": "ExposureEV",
      "timeDomain": "Static",
      "operations": ["Add", "Replace"],
      "recommendedMinimum": -4.0,
      "recommendedMaximum": 4.0,
      "backendCapabilities": ["MaterialPropertyBlock"]
    }
  ]
}
```

### 6.2 第一版受控属性组

#### 时间与节奏

- `timing.preRoll.frames`
- `timing.entry.frames`
- `timing.body.frames`
- `timing.exit.frames`
- `timing.cleanup.frames`
- `timing.retime.mode`
- `timing.cycle.frames`
- `timing.event.<eventId>.frame`
- `timing.materialLocalTime.scale`
- Layer `visualStart.mode`
- Layer `visualStart.manualLeadInFrames`

`visualStart` 属于 Layer 时间合同，而不是直接 Unity 字段覆盖。它解决从完整源 Prefab
提取的 ParticleSystem/复合 Element 自带公共延迟、单独使用时出现无意义空白的问题：

- `AutoNormalize`：检测所有启用成员的共同首个发射帧，将公共空白归零。
- `PreserveSource`：完整保留源 `startDelay` 与 Burst 时间。
- `ManualLeadIn`：先移除公共空白，再把首个可见发射放到指定非负帧。

复合 Element 必须整体平移发射时间；禁止分别将每个成员归零。成员原始相对节奏必须保持，
例如源成员从 `30/36/42/50` 帧发射，自动归零后为 `0/6/12/20`，手动前导 `12`
帧后为 `12/18/24/32`。编译器优先从 `Main.startDelay` 消耗前移量；若公共空白还包含
Burst 自身的时间偏移，再平移 Burst time。Piloto 源资产不发生修改。

#### 整体外观

- `appearance.opacity`
- `appearance.opacityEnvelope`
- `appearance.palette.primary`
- `appearance.palette.secondary`
- `appearance.palette.detail`
- `appearance.emission.color`
- `appearance.emission.exposureEv`
- `appearance.dissolve`
- `appearance.erosion`

#### 发射

- `emission.density`
- `emission.rate.time`
- `emission.rate.distance`
- `emission.burst.scale`
- `emission.burst.<burstId>.count`
- `emission.window.startFrame`
- `emission.window.endFrame`
- `emission.capacity.maxParticles`

#### 运动

- `motion.speed`
- `motion.travelRange`
- `motion.gravity.world`
- `motion.gravity.local`
- `motion.force.vector`
- `motion.noise.strength`
- `motion.noise.frequency`
- `motion.drag`
- `motion.rotation.speed`

#### 形态

- `shape.composition.scale`
- `shape.particle.size`
- `shape.emitter.scale`
- `shape.radius`
- `shape.angle`
- `shape.arc`
- `shape.direction.spread`

#### 渲染

- `render.sortingOrder.offset`
- `render.material.profile`

`render.material.profile` 当前仅保留为受控 Registry 预留 ID，尚无 Material Variant Profile 数据模型或安全 Adapter，因此不能写入 Draft。材质资产替换使用已落地的 `render.renderer.material`；纹理、渲染状态和 Shader 参数必须通过已经过材质属性、关键字、Vertex Stream/Custom Data 校验的 `DerivedMaterial` Binding。不得把 `ProfileId` 误当成 Material GUID 加载。
- `render.flipbook.speed`
- `render.texture.main`
- `render.texture.mask`
- `render.texture.noise`
- `render.texture.ramp`
- `render.texture.normal`
- `render.texture.flipbook`

自由 tags 只用于搜索，不能注册为属性或控制编译行为。

### 6.3 值类型

第一版允许：

- `Bool`
- `Int`
- `Float`
- `Enum`
- `Vector2/3/4`
- `ColorHdr`
- `Gradient`
- `Curve`
- `AssetReference`

颜色同时保存语义 sRGB/HDR/曝光表达和最终 linear/Shader 原生值。曲线与 Gradient 的完整合同见第 7 节。

## 7. Source Snapshot

### 7.1 目的

Source Index 适合搜索，但不足以精确编辑。Snapshot 必须能在不再次读取整套源 Prefab 的情况下重建被编辑成员的全部允许字段，并证明未参与字段没有变化。

### 7.2 存储策略

- Manifest 首屏只加载 component ID、source hash、blob hash、schema version、module flags 和大小摘要。
- 选中 Element 后按需解压对应 Blob。
- Blob 使用 canonical JSON 后 gzip；文件名使用 canonical SHA-256。
- 同内容 Snapshot 去重；源内容 hash 变化时生成新 Blob，不覆盖旧 Blob。
- Snapshot 目录 Git 忽略，可由 Source Library 和 Unity Editor 重建。

### 7.3 Snapshot 内容

每个成员至少记录：

- source package、Prefab GUID/hash、component local fileID、Transform path。
- Transform、Simulation Space、Scaling Mode、random seed 合同。
- Main、Emission、Shape、Color/Size/Velocity/Rotation over Lifetime、Noise、Texture Sheet、Custom Data、Renderer 的完整序列化值。
- Trail 的 lifetime、width、color、空间、ribbon、dieWithParticles 和清理相关结构化快照。
- Force 与 Limit Velocity 的当前受控曲线，以及 SubEmitter、Collision、Inherit Velocity 等未结构化模块的启用标记和只读 `rawEditorJson` 证据；后者不能宣称已完成逐字段 Snapshot。
- Renderer Mode、Mesh、Material、Trail Material、Vertex Streams、sorting 和 shadow/light probe 设置。
- Shader GUID/hash、所有属性类型和值、keyword、render queue、blend/cull/zwrite 状态。
- 贴图 GUID/hash 与 Import Settings hash。

### 7.4 曲线合同

`MinMaxCurve` 必须保存：

- mode：Constant、TwoConstants、Curve、TwoCurves。
- scalar/curveMultiplier。
- min/max constant。
- min/max curve。
- preWrapMode、postWrapMode。
- 每个 Key 的 time、value、inTangent、outTangent、inWeight、outWeight、weightedMode、tangentMode evidence。

`MinMaxGradient` 必须保存：

- mode：Color、TwoColors、Gradient、TwoGradients、RandomColor。
- min/max Color，使用 linear RGBA 和原始 HDR evidence。
- color keys、alpha keys、key time、gradient mode 和 color space evidence。

Snapshot 不对曲线做语义解释；解释只存在于 Profile。

## 8. Authoring Profile

### 8.1 Manifest

Manifest 只保存 Profile ID、种类、路径、schema version、content hash、source dependencies、review state 和适用 backend。具体绑定放在独立文件，避免单个巨大 JSON 成为冲突热点。

Profile 状态：

- `AutoSuggested`：扫描器建议，可用于预览、语义编辑和 Candidate；实际绑定随 receipt 冻结，但不构成跨草稿共享事实。
- `Confirmed`：已发布到共享 Manifest，可被多个草稿稳定引用和集中维护。
- `Deprecated`：保留旧草稿解析，不用于新建。
- `Stale`：源或依赖变化，禁止保存新语义覆盖和烘焙。

### 8.2 Component Profile

Component Profile 描述单成员：

- 时间分段与最短 Body。
- 已确认周期和周期起点。
- Burst/事件锚点。
- Primary Opacity Binding。
- Dissolve/Erosion 独立绑定。
- 发射、运动、形态、颜色、Renderer 和材质属性绑定。
- 各语义属性的推荐范围和响应曲线。
- 不参与字段、只读模块和阻断条件。

代表性结构：

```json
{
  "schemaVersion": "mx.vfx.component-profile.v1",
  "profileId": "vfx.component.piloto.glow.001",
  "sourceComponentId": "piloto.bundle:<prefabGuid>:<localFileId>",
  "sourceSnapshotHash": "sha256:...",
  "status": "Confirmed",
  "timing": {
    "preRollFrames": 0,
    "entryFrames": 12,
    "bodyFrames": 48,
    "exitFrames": 18,
    "cleanupFrames": 30,
    "minimumBodyFrames": 1,
    "cycleFrames": 0
  },
  "events": [
    { "eventId": "initial.flash", "anchor": "StartLocked", "frame": 0 }
  ],
  "bindings": [
    {
      "propertyId": "appearance.opacity",
      "adapter": "ParticleColorOverLifetime",
      "target": "alpha",
      "operation": "Multiply",
      "primaryOpacity": true
    },
    {
      "propertyId": "appearance.emission.exposureEv",
      "adapter": "MaterialPropertyBlock",
      "shaderProfileId": "vfx.shader.uberfxsg.v1",
      "target": "emissionExposure",
      "operation": "Add"
    }
  ]
}
```

### 8.3 Element Profile

Element Profile 负责复合语义：

- 成员列表和职责，例如 Core、Glow、Sparks、Smoke。
- 每个宏控的 `Follow / Override / Excluded / Unsupported / Unconfirmed` 状态。
- 成员响应权重或响应曲线。
- 成员级默认重定时模式。
- 语义调色板映射。
- 发射区域缩放时“保持总量”或“保持密度”。
- 预算模型和 Experimental 阈值。
- 最少/最多成员参与规则。

整体强度为 1.5 时，不要求所有成员都乘 1.5。例如 Core emission 可响应 1.0，Glow 响应 0.7，Smoke 响应 0.25，而固定 Decal 可不参与。

### 8.4 Shader Profile

Shader Profile 记录：

- Shader GUID/hash 和兼容版本。
- Color、Alpha、Emission、Dissolve/Erosion、局部时间等语义槽位。
- Shader Property 类型、原始名称与缓存 Property ID 生成规则。
- Custom Data channel 到 Shader 输入的映射。
- 必需 Vertex Streams 及顺序。
- 哪些属性可由 MaterialPropertyBlock 动态驱动。
- 哪些属性必须创建派生 Material。
- Texture slot 语义与 Import Settings 约束。
- keyword、blend、cull、zwrite、queue 的安全组合。

首批策展优先覆盖现有材质占比最高的 UberFXSG。Profile 必须由实际 Shader Graph/Material 扫描结果确认，不在本文硬编码具体属性名。

### 8.5 Profile 保存门禁

Profile 保存与 Draft 保存是两个独立操作：

1. 生成 proposed Profile 和稳定 diff。
2. 列出所有 Draft、Element、Primitive、Candidate consumer。
3. 运行 validator。
4. 检查 expected hash。
5. 人工确认影响面。
6. 事务写入并更新 manifest。
7. 二次 hash guard 失败时回滚。

Draft 编辑不能偷偷修改共享 Profile。

## 9. Composition Draft v3

### 9.0 从完整源 Prefab 反向生成私有 Compound Element

工作台允许从已经进入 Piloto Source Index 的纯 ParticleSystem Prefab 创建普通 v3 草稿。该能力用于“把现成完整效果先准确还原，再在成员页拆解和语义化”，不是把源 Prefab 直接发布为 Runtime 资源。

- `localElements` 是 Draft v3 的可选字段；ID 固定使用 `vfx.element.local.source-prefab.<prefabGuid>`，解析优先级高于全局 Element，但禁止覆盖全局 ID。
- 私有 Element 保存 Prefab GUID/content/dependency hash、成员 Component ID/content hash、原始成员顺序和自动语义建议。建议不参与首次视觉编译。
- 导入器只通过 `PrefabUtility.LoadPrefabContents`、Source Index、Snapshot/Profile Adapter 和 Unity 序列化 API 读取；不解析或写入 Unity YAML。
- 首次结果固定为一个 `Loop / OneShot` Compound Layer，`startFrame=0`、`visualStart=PreserveSource`、`retimeMode=Fixed`，不生成语义倍率或曲线覆盖。导入器将草稿级 `timing.minimumBodyFrames` 初始化为 1；源分析得到的完整发射窗口只作为建议和证据，不再自动成为不可修改的艺术下限。
- 源播放窗口同时考虑 `main.duration`、start delay、发射窗口、Burst 与粒子驻留；纯技术清理单独记录。阶段长度不以单个 `main.duration` 粗略替代完整成员计算。创作者可在 Layer 时间 Inspector 通过“最短主体”滑杆覆盖建议下限，重复导入必须保留该人工值。
- Missing Script、未知组件、Animator/Animation、外部或尚不能重映射的 SubEmitter 会阻断导入。错误不能降级成“尽量复制”。
- 重复导入同 GUID 时更新同一 Draft ID。仅当成员 ID 与顺序完全可映射时保留 Layer Transform、语义覆盖、成员参与、成员覆盖和 Preview Context；成员增删或歧义时保留原草稿与恢复副本并 fail-closed。
- 私有 Element 随草稿、恢复日志和 Draft hash 保存，不进入 Curation、Runtime、Candidate 内容公共合同或其他草稿依赖。Candidate 烘焙仍逐成员生成完整项目 Prefab，不把 Piloto 根 Prefab 直接当作 Runtime 资产。

首个验证样板为 `Hit_Nova_Light.prefab`（GUID `34a4dd93a152d8a4a842b0b06e1834af`）：7 个 ParticleSystem/Renderer 成员、1 个私有 Compound Element、1 个 Layer，源窗口至少 300 帧。固定 seed 与多帧视觉 Golden 仍属于后续人工视觉验收；当前自动门禁证明成员顺序、Transform、ParticleSystem、Renderer 与材质引用的序列化复制一致。

### 9.1 设计目标

Draft v3 只保存用户意图和稀疏覆盖，不复制完整 Unity 字段。未覆盖字段始终来自绑定 Snapshot/Profile。

### 9.2 代表性结构

```json
{
  "schemaVersion": "mx.vfx.composition-draft.v3",
  "draftId": "vfx.composition.sample.fireball.flight",
  "displayName": "火球·飞行",
  "framesPerSecond": 60,
  "loopPlaybackMode": "Continuous",
  "dependencies": {
    "catalogHash": "sha256:...",
    "sourceIndexHash": "sha256:...",
    "curationHash": "sha256:...",
    "propertyRegistryHash": "sha256:...",
    "profileManifestHash": "sha256:..."
  },
  "phaseDurationOverrides": {
    "Build": null,
    "Rebuild": null,
    "Loop": 180,
    "Complete": 30,
    "Dissipate": 45
  },
  "previewContext": {
    "mode": "CharacterAttached",
    "characterStableId": "codex_flow_test",
    "packageId": "codex_flow_test",
    "characterEvidenceHash": "sha256:...",
    "socketId": "mainHand",
    "animationResourceKey": "...",
    "animationLoop": true,
    "animationSpeed": 1.0,
    "phaseOverrides": {
      "Build": {
        "posePresetId": "Cast",
        "animationResourceKey": "animation.codex_flow_test.cast",
        "animationLoop": false,
        "socketId": "offHand"
      },
      "Complete": {
        "posePresetId": "Idle",
        "animationResourceKey": "animation.codex_flow_test.idle",
        "socketId": "mainHand"
      }
    }
  },
  "layers": [
    {
      "layerId": "layer_01_glow",
      "phase": "Loop",
      "enabled": true,
      "source": {
        "kind": "Element",
        "stableId": "vfx.element.fire.glow.001"
      },
      "profileDependencies": [
        { "profileId": "vfx.element.fire.glow.001", "hash": "sha256:..." },
        { "profileId": "vfx.shader.uberfxsg.v1", "hash": "sha256:..." }
      ],
      "startFrame": 0,
      "transform": {
        "position": [0.0, 0.0, 0.0],
        "rotationEuler": [0.0, 0.0, 0.0],
        "scale": [1.0, 1.0, 1.0]
      },
      "timing": {
        "retimeMode": "ExtendBody",
        "targetArtisticFrames": 150,
        "protectedEvents": ["initial.flash"]
      },
      "semanticOverrides": [
        {
          "propertyId": "appearance.emission.exposureEv",
          "operation": "Add",
          "value": 1.5
        },
        {
          "propertyId": "emission.density",
          "operation": "Multiply",
          "value": 1.2
        }
      ],
      "memberParticipation": [
        { "memberId": "core", "state": "Follow" },
        { "memberId": "smoke", "state": "Excluded" }
      ],
      "memberOverrides": []
    }
  ]
}
```

### 9.3 稳定性规则

- 所有创作时间使用整数帧，默认 60 FPS。
- Layer 顺序有意义，canonical JSON 不按 ID 重排 Layer。
- 无覆盖时不写默认对象，避免噪声 diff。
- `null` 阶段长度表示自动计算，不使用 `-1`。
- Profile ID 与 hash 必须同时记录。
- Preview Context 进入 Draft 与预览证据 hash，但从 Candidate 内容 hash 排除。
- 相机、浮动预览卡位置、收藏、当前选中项、自动预览开关属于 EditorPrefs，不进入 Draft。

## 10. Canonical Hash 与 Bake Receipt v3

### 10.1 独立 Hash

必须分别计算：

- `sourceSnapshotHash`
- `profileContentHash`
- `profileDependencyHash`
- `draftContentHash`
- `compositionInputHash`
- `previewEvidenceHash`
- `candidateContentHash`
- `receiptHash`

Canonical JSON 使用 UTF-8、固定属性顺序、稳定数字格式、无绝对路径、无时间戳、无机器名和无随机扫描顺序。

### 10.2 Receipt 内容

`mx.vfx.composition-bake-receipt.v3` 至少记录：

- Draft v3 hash 和所有 Profile/Snapshot dependency hash。
- 每个 Layer 的 phase、start/end frame 和成员列表。
- 源字段、语义操作、最终字段的稳定 diff。
- 创建或复用的 Primitive、派生 Material/Texture/Shader Adapter。
- MaterialPropertyBlock binding 与静态材质派生 binding。
- 五阶段总帧数、Loop 模式和 Cleanup 上限。
- Candidate Prefab GUID/path/content hash。
- 编译器、Adapter、提取器和 Unity 版本。
- Piloto 源资产 hash-before/hash-after 不变证据。
- validation level、diagnostics 和 Experimental 状态。

Receipt 不能包含人工可伪造的 Ready 结论；晋升仍走现有人工授权流程。

## 11. 精确重定时合同

### 11.1 五段时间模型

单成员时间固定分为：

```text
Pre-roll → Entry → Body → Exit → Cleanup
```

- `Pre-roll`：允许负起始语义的准备区，但 Draft 中仍以 Layer startFrame 调度。
- `Entry`：出现、淡入、聚集、启动峰前准备。
- `Body`：持续、巡航、保持或可重复周期。
- `Exit`：淡出、收束、离开主体视觉。
- `Cleanup`：停止发射后等待残留粒子/Trail 清理；不计入艺术长度。

艺术长度：

```text
artisticFrames = preRollFrames + entryFrames + bodyFrames + exitFrames
```

阶段边界必须满足：

```text
phaseFrames >= max(enabledLayer.startFrame + enabledLayer.artisticFrames)
```

禁止通过阶段总长度隐式裁剪 Layer。

### 11.2 事件锚点

| 锚点 | 行为 |
| --- | --- |
| `StartLocked` | 相对 Layer 起点保持绝对帧，常用于初始 Flash、首个 Burst。 |
| `EndLocked` | 相对艺术终点保持绝对帧距离，常用于结束 Spark、收束事件。 |
| `BodyNormalized` | 在 Body 内保持 0–1 归一化位置。 |
| `CycleLocal` | 保持在已确认周期内的局部帧，只随完整周期重复。 |

人工保护点使用相同锚点模型。未知事件不能自动移动。

### 11.3 最短长度

最短艺术长度由以下最大约束共同决定：

- Pre-roll + Entry + Exit。
- 草稿 Layer 显式设置的 `timing.minimumBodyFrames`；未设置时才回退到 Profile/源分析建议。
- StartLocked/EndLocked 事件最小间距。
- 不可拆周期的完整周期长度。
- 不可重定时模块声明的最短稳定窗口。

目标长度低于最短长度时阻断，而不是自动压坏曲线。

### 11.4 模式零：原速播放窗口 `PlaybackWindow`

`PlaybackWindow` 将“播放多久”与“内部如何重定时”分离。编译器保持源 `TargetSegments` 和恒等时间映射，
仅把 `targetArtisticFrames` 编译为 Layer Scheduler 的结束边界。ParticleSystem Adapter 不修改
`main.duration`、`startDelay`、`startLifetime`、Burst、Emission 曲线或 `simulationSpeed`。

窗口结束策略：

- `StopEmitting`：结束帧停止发射，已有粒子自然消散。
- `StopAndClear`：结束帧停止并立即清理全部粒子。
- `DissipateThenClear`：在结束前 `dissipateFrames` 停止发射，结束帧清理残留粒子。

工作台在此模式下显示源内容长度、播放窗口和结束策略，隐藏最短主体及派生的 Entry/Body/Exit 编辑展示。
预览和 Candidate 烘焙必须通过同一份 `UnityVfxBakedLayerScheduleEntry` 执行该合同。

### 11.5 模式一：延长主体 `ExtendBody`

默认保持 Entry 和 Exit 绝对帧，只改变 Body。编译器按成员机制处理：

- `EmitterWindow`：延长发射器持续窗口。
- `ParticleResidence`：保持发射节奏，允许更多粒子在 Body 驻留；必要时同步提高容量。
- `MaterialEnvelope`：保持淡入/淡出局部形状，把 Exit 移到新 Body 之后。
- `FixedEvent`：StartLocked/EndLocked 不变，BodyNormalized 事件重新映射。

对 `layer_01_glow` 的示例：

```text
Source: Entry 12 + Body 48 + Exit 18 = 78 艺术帧，Cleanup 30
Target: 150 艺术帧
Result: Entry 12 + Body 120 + Exit 18 = 150，Cleanup 30
```

Opacity 淡入仍为 12 帧，淡出仍为 18 帧；只扩展确认过的 Body 表达。粒子生命周期归一化颜色曲线默认不变，发射窗口和材质 emitter-time envelope 按 Profile 重编译。

### 11.6 模式二：延长尾迹 `ExtendTail`

用于烟雾、残影、光带和衰减层。Profile 必须为每种通道选择策略：

- `Hold`：保持末值。
- `Continue`：按末端趋势继续，但必须有限制范围。
- `Repeat`：重复已确认周期。
- `EndLocked`：保持终点曲线形状并把它移动到新终点。
- `Unchanged`：该通道不参与尾迹延长。

Alpha、Size、Velocity、Noise、Texture Sheet、Material Local Time 可以选择不同策略。没有明确策略时不允许 ExtendTail。

### 11.7 模式三：重复主体 `RepeatBody`

- 只重复 Profile 已确认的完整周期。
- 默认把目标 Body 吸附到周期整数倍。
- StartLocked Entry 只播放一次，EndLocked Exit 只播放一次。
- CycleLocal Burst、曲线和材质事件随每个周期重复。
- 非完整末周期只有在 Profile 明确允许且标记 Experimental 时可用。

### 11.8 模式四：事件重排 `EventRemap`

使用严格单调的分段时间映射：

```text
(sourceFrame0 → targetFrame0)
(sourceFrame1 → targetFrame1)
...
```

要求：

- source/target key 均严格递增。
- StartLocked/EndLocked 不能被跨越。
- Burst、Emitter-time Curve、Gradient event 和材质事件使用同一映射。
- 映射边界处必要时插入等值 Key，保证曲线连续。
- 非单调输入直接阻断。

### 11.9 模式五：整体变速 `WholeSpeed`

统一缩放：

- Layer 调度。
- ParticleSystem duration/startDelay/startLifetime/startSpeed 等已确认字段。
- Emitter-time 材质局部时间。
- Burst 和事件帧。

该模式会按比例改变 Entry/Exit，UI 必须明确提示“整体节奏改变”，不能作为普通延长的默认方式。

### 11.10 模式六：固定长度 `Fixed`

成员艺术长度不可修改，只允许移动 Layer startFrame。适用于固定 Flash、不可安全拆分的多 Burst Impact 或依赖外部 Shader 时间的素材。

### 11.10 曲线与切线修正

当某分段时间轴采用 `t' = a*t + b` 时：

- Key time 映射为 `t'`。
- Key value 保持不变，除非同时存在值域语义操作。
- Hermite 切线按 `slope' = slope / a` 修正。
- Weighted tangent 的时间手柄按分段尺度调整，值手柄保持原表达。
- 分段映射边界插入采样等值 Key 后分别修正左右切线。

时间保真门禁：Entry/Exit 局部曲线值误差不超过 `1e-5`，Start/End/Cycle 事件误差为 0 帧。

### 11.11 不同时间域的处理

- `ParticleNormalized` 曲线通常不随发射器 Body 延长；它描述单粒子从生到灭。
- `EmitterNormalized` 曲线随发射器时间重映射。
- `MaterialLocalTime` 只有存在 Shader Adapter 时可重映射。
- `Static` 值不参与重定时。
- 无法识别时间域的曲线只读，并阻断依赖它的语义操作。

## 12. 语义属性编译

### 12.1 绑定优先级

```text
Registry type/range
→ Shader/Component capability
→ Element member participation
→ Layer override
→ Member override
→ Validation and Experimental classification
```

同一成员同一语义槽只能有一个 primary binding。Opacity 与 Dissolve/Erosion 分开；不能用 Dissolve 偷偷充当统一透明度。

### 12.2 强度

“整体强度”是 Element 宏控，不直接等于 emission rate。Element Profile 为每个成员和属性定义响应曲线，例如：

| 成员 | 发射 | 发光 EV | 尺寸 | Opacity |
| --- | ---: | ---: | ---: | ---: |
| Core | 1.0 | 1.0 | 0.3 | 0.2 |
| Glow | 0.4 | 0.8 | 0.5 | 0.6 |
| Sparks | 0.8 | 0.2 | 0.0 | 0.0 |
| Smoke | 0.2 | 0.0 | 0.2 | 0.1 |

响应值代表宏控影响权重，不是最终 Unity 值。

### 12.3 发射、Burst 与容量

- Rate over Time、Rate over Distance 和 Burst 分开编辑。
- “发射密度”由 Profile 决定映射到哪些来源。
- Shape 放大时必须声明保持总量或保持空间密度。
- 编译器按最坏并发粒子估算所需 `maxParticles`；超过源容量时给出建议，不能静默截断。
- 容量改变进入预算和 Diff。

### 12.4 运动、重力与行程

- `motion.speed` 只修改已确认速度来源，不盲目同时乘 Start Speed 和 Velocity over Lifetime。
- `motion.travelRange` 根据速度、Lifetime、阻力和 Profile 模型求解；无法可靠求解时只显示估算并要求原始编辑。
- 世界重力先转换到成员 Simulation Space，再写入 gravity/force binding。
- 局部/自定义受力分别建模，不能统一叫“重力”。
- Noise 强度与频率分开，避免改变强度时同时改变运动尺度。

### 12.5 尺寸与构图

以下控制必须分开：

- 整体 Transform scale。
- 粒子 Start Size / Size over Lifetime。
- Shape/Emitter 尺寸。
- 运动行程。
- Trail 宽度。

“让效果更大”在 UI 中是宏控，但详情必须显示它对上述各项的响应，不能只缩放根 Transform。

### 12.6 颜色、HDR 与发光

- 语义调色板至少支持 Primary、Secondary、Detail。
- Particle Gradient、Start Color、Material Color 和 Emission Color 分别绑定。
- 发光颜色与发光强度分离。
- 强度以 EV 作为主编辑单位，详情显示 linear 倍率和 Shader 原生值。
- HDR 转换使用明确 color space，不允许在 sRGB/linear 之间重复转换。

### 12.7 Experimental

以下情况进入 Experimental：

- 超出 Profile 推荐范围。
- 使用 Replace 丢弃源曲线。
- 使用非完整周期。
- 使用尚未 Confirmed 的绑定。
- 预算超过目标 Quality Profile。
- 依赖只读高风险模块的近似处理。

Experimental 可以预览、保存和烘焙到 Candidate，但不能晋升 ReviewNeeded，直到 Profile/范围/预算得到确认。

## 13. ParticleSystem、Renderer 与 Material Adapter

### 13.1 第一版正式覆盖

| Unity 区域 | 支持范围 | 典型语义 |
| --- | --- | --- |
| Main | duration、loop、startDelay、startLifetime、startSpeed、startSize、startColor、gravityModifier、simulationSpeed、maxParticles | 时间、速度、尺寸、颜色、重力、容量 |
| Emission | rate over time/distance、Bursts | 密度、窗口、Burst 规模与事件 |
| Shape | 类型只读，已确认参数可编辑 | 发射区域、半径、角度、弧度、方向扩散 |
| Color over Lifetime / by Speed | 完整 MinMaxGradient | Opacity、调色板、粒子颜色 |
| Size over Lifetime / by Speed | 完整 MinMaxCurve | 粒子尺寸包络 |
| Velocity over Lifetime / by Speed | 完整轴曲线 | 运动、行程、局部受力 |
| Rotation over Lifetime / by Speed | 完整轴曲线 | 旋转速度 |
| Noise | strength、frequency、scroll、octave 等已确认字段 | 噪声强度和频率 |
| Texture Sheet Animation | frame curve、cycles、start frame | Flipbook 速度和周期 |
| Custom Data | 仅确认的 channel/binding | Shader 语义输入 |
| Renderer | render mode、sorting、mesh/material/streams 合同 | 排序、依赖、渲染能力 |
| Material | 确认的 Shader Profile 属性 | Alpha、Emission、Dissolve、局部时间 |

### 13.2 首版只读或阻断

Trails 已具备结构化 Snapshot 和首个时间合同，但尚未提供 width/color/ribbon 等通用语义属性编辑。SubEmitter、Collision、Trigger、External Forces、Lights、Inherit Velocity 等模块当前仅有启用标记与原始序列化证据，仍需逐字段 Snapshot 和专用 Profile/Adapter。Limit Velocity 当前只对 `motion.drag` 提供 Experimental 精确写入。一个语义操作若会破坏这些模块的已知关系，validator 必须阻断。

### 13.3 MaterialPropertyBlock 与派生 Material

动态属性优先使用 MaterialPropertyBlock：

- Alpha。
- Emission Color/Intensity。
- Dissolve/Erosion。
- Authoring Adapter 提供的局部时间。

以下改变必须创建去重、可追踪的派生 Material：

- Texture 替换。
- Keyword。
- Blend/Cull/ZWrite。
- Render Queue。
- Shader 变更。

派生 Material 的 ID 由源 Material hash + override canonical hash + adapter version 决定，同样输入只能生成一个派生结果。

### 13.4 Custom Data 与 Vertex Stream

只有 Shader Profile 同时确认：

- Custom1/Custom2 channel 的语义。
- Vector/Color 分量布局。
- ParticleSystemRenderer Vertex Stream 顺序。
- Shader 消费属性。

工作台才开放编辑。任何一环不一致时只读并报告诊断。

### 13.5 VFX Graph 扩展点

保留 `IVfxSemanticPropertyAdapter` 和 `IVfxRetimeAdapter` backend 接口。VFX Graph 实现必须单独位于 package-gated Unity-facing 程序集，并满足 Exposed Property、Subgraph、fallback、池化 reset 和平台能力合同；第一版不得把 ParticleSystem 规则强套到 Graph。

## 14. 工作台交互设计

### 14.1 Layer Inspector 四页

#### 语义

按“时间、外观、发射、运动、形态、渲染”折叠分组。默认只显示 Profile 确认且对当前 Layer 有意义的属性。每个控件显示：

- 当前语义值与单位。
- 推荐范围。
- 参与成员数量。
- 自动/手动/Experimental 状态。
- 快速恢复源值。
- 进入详情的绑定入口。

#### 成员

矩阵行是成员，列是语义宏控。单元格状态：

- 跟随 `Follow`
- 成员覆盖 `Override`
- 不参与 `Excluded`
- 不支持 `Unsupported`
- 待确认 `Unconfirmed`

选择单元格后显示 Component Profile binding、响应曲线、源字段和成员覆盖。

#### 原始

完整只读展示 Source Snapshot 和最终值；允许在有 Adapter 的字段上进入“高级原始编辑”。原始编辑仍写 v3 稀疏 override，不直接改 Preview 实例或源 Prefab。

#### 差异

按以下链路展示：

```text
语义操作
→ Element 宏控响应
→ Component/Shader Binding
→ Source 值
→ Final Unity 值
→ Preview/Bake 写入目标
```

支持按成员、模块、材质和风险过滤，并突出 Replace、派生资产、Experimental 和预算变化。

### 14.2 时间线

Layer 条必须显示：

- Pre-roll。
- Entry。
- Body 或 Cycle。
- Exit。
- Cleanup。
- Burst/Event 标记。
- StartLocked/EndLocked/BodyNormalized/CycleLocal 锚点。
- 当前预览帧指示。

拖动手柄与精确帧输入双向同步。拖动艺术终点时先选择或沿用 Retime Mode，不再修改 `lifetimeScale`。

### 14.3 曲线轨道

中央可折叠曲线编辑器固定显示：

- 灰色：Source Curve。
- 蓝色：Semantic modification layer。
- 白色：Final Curve。
- 可选半透明线：复合成员曲线。

Layer/Emitter 曲线使用帧；Particle lifetime 曲线使用 0–100%，同时显示 Min/Default/Max 粒子生命帧标尺。编辑 Key 必须保留切线、权重和 MinMax 模式。

### 14.4 阶段总长度

- 选择单一生命周期阶段时，顶部显示当前阶段总帧数和自动/手动状态。
- 自动值取所有启用 Layer 的最晚艺术结束帧，Loop 最少 1 帧。
- 手动值小于 Layer 艺术结束帧时显示越界警告并阻断烘焙，不自动裁剪。
- 完整生命周期模式显示五阶段摘要；Continuous Loop 显示“持续”，不伪造有限总长度。

### 14.5 预览更新

- 自动预览默认开启，可关闭。
- 更新节流到 15–30 FPS。
- 只重编译和重模拟受影响成员。
- 保持当前帧、相机、角色、Socket、其它 Layer、A/B 条件和选择状态。
- 若修改发生在当前帧之前，从固定 seed 状态重模拟到当前帧。
- 若只修改静态 MPB 属性，可直接更新，不重建粒子系统。

### 14.6 A/B

- A 默认是源效果。
- B 默认是当前 Draft 编译结果。
- 两侧共享角色、动画、Socket、相机、背景、seed、帧和播放状态。
- 允许把 A 锁定为上一次保存版本或另一个候选，但必须明确显示基线 hash。

### 14.7 恢复与预设

- Draft 只有显式“保存草稿”才写项目文件。
- 未保存变更写 Git 忽略的 Recovery Journal。
- 重开窗口时可恢复会话，但恢复不等于保存。
- 自定义属性预设采用一次性展开快照；应用后写普通 override，不永久链接预设。

## 15. Preview 编译与增量重模拟

### 15.1 编译输入

Preview key 由以下内容组成：

- Snapshot hash。
- Profile dependency hash。
- Layer semantic override hash。
- Member participation/override hash。
- phase/startFrame/retime hash。
- seed、preview platform profile。

相机、角色显示、辅助网格不改变 VFX compile key。

### 15.2 增量策略

修改分类：

| 变化 | 处理 |
| --- | --- |
| MPB 静态颜色/Emission | 更新缓存 MPB，不重建实例。 |
| ParticleSystem 模块值 | 恢复该成员源快照并重应用最终值，重模拟该成员。 |
| Layer start/phase/retime | 重建该 Layer schedule，重模拟该 Layer。 |
| Element 成员参与 | 只创建/释放变化成员。 |
| Shader/Material/Texture | 重建相关 Renderer/material binding。 |
| Source/Profile hash | 标记 stale，进入重映射流程。 |

所有路径不得调用会重置组合相机或角色上下文的全局 Preview rebuild。

## 16. Candidate 烘焙与运行时属性驱动

### 16.1 Bake 流程

```text
Draft v3 load
→ dependency/stale validation
→ Snapshot/Profile resolve
→ semantic + retime compile
→ exact Unity field diff
→ extraction/material derivation plan
→ expected-hash confirm
→ Unity Editor API write
→ prefab rescan
→ receipt v3
→ preview/bake equivalence validation
```

禁止手写 Unity YAML、`.mat`、`.vfx` 或 Shader Graph 序列化数据。

### 16.2 Prefab 结构

Candidate 继续包含一个根节点和五个生命周期阶段根：

```text
EffectRoot
  Build
  Rebuild
  Loop
  Complete
  Dissipate
```

每个 Layer 保存烘焙后的 start/end frame、成员引用和属性轨道。Runtime 不根据子对象名字猜测阶段，序列化 Controller 持有明确引用。

### 16.3 统一调度

一个 `UnityVfxLifecycleController` 或其受控协作者统一推进浮点帧：

- 不给每个 Layer 创建 `Update`。
- 初始化时缓存 Layer schedule、ParticleSystem、Renderer、MaterialPropertyBlock、Shader Property ID 和曲线。
- Tick 只遍历固定数组。
- 到 startFrame 激活/Play，到 endFrame 停止发射或执行 Profile 声明的停止策略。
- Cleanup 完成后再归还 Presenter pool。

### 16.4 Baked Property Driver

原生 ParticleSystem 无法表达的时间属性由通用烘焙驱动负责：

- Material Alpha。
- Emission Color/EV。
- Dissolve/Erosion。
- Shader Adapter local time。

Driver 使用生命周期控制器的同一浮点帧，不单独读取 `Time`。MaterialPropertyBlock 每个 Renderer 初始化一次并复用；`Shader.PropertyToID` 只在初始化执行。

### 16.5 Runtime 边界

- Runtime 只消费 Prefab、序列化 Layer schedule 和烘焙曲线。
- 不加载 Registry/Profile/Snapshot/Draft。
- 不改变现有 VfxDefinition、Trigger、Anchor、ResourceKey 和参数块合同。
- 不把视觉曲线或粒子状态写入 gameplay hash、SaveState 或网络。
- 运行时重建/停止仍遵循五阶段生命周期合同。

## 17. Validation 等级与诊断

### 17.1 四级 Validator

1. `StructuralValid`：JSON、ID、引用、类型和 canonical hash 有效。
2. `Previewable`：Snapshot/Profile/Adapter 足够生成隔离预览。
3. `BakeableCandidate`：无 stale、无阻断模块、路径与依赖合法，可写 Candidate。
4. `PromotableReviewNeeded`：无 Experimental、预算和技术门禁通过，具有人工播放证据。

### 17.2 当前稳定诊断族

早期设计中的 `VFXPROPxxx/VFXRT00x` 占位命名不再作为 Current 契约；实现使用下列稳定诊断族，新增诊断不得复用已发布 Code：

| Code/范围 | 级别 | 当前条件 |
| --- | --- | --- |
| `VFXSEM001–029` | Error/Warning | Registry 缺失、属性/运算/值无效、推荐范围、Binding/只读/Primary Opacity、成员引用与参与状态；`VFXSEM023` 表示成员属性标记为 Override 却没有独立值，`024–029` 校验外部 Draft 中缺失/重复/未知成员和非法参与状态。 |
| `VFXPRO001–005`、`VFXPRO100–108` | Error | Component/Element/Shader Profile、Snapshot、hash、source component 或确认状态不满足当前验证等级。 |
| `VFXRET001–070` | Error/Warning | 目标长度、六种 Retime Mode、周期、事件映射、成员独立模式、最短 Body 和切线/时间映射合同。 |
| `VFXMOD001–005` | Error/Warning | SubEmitter、Trails、Collision、Limit Velocity 等高耦合模块的操作粒度门禁。 |
| `VFXCURVE001–007`、`VFXGRD001` | Error | MinMaxCurve/Gradient 类型、时间域或精确模式无法安全应用。 |
| `VFXCUS001–003` | Error | Custom Data channel 与 Vertex Stream/Shader Profile 合同不一致。 |
| `VFXSHD001–005`、`VFXMAT001–006` | Error/Warning | Shader Binding、局部时间、贴图槽、派生材质和源材质合同。 |
| `VFXREN001–003`、`VFXMOT001`、`VFXTIME001` | Error/Warning | Renderer、运动求解或时间字段无法按已确认 Profile 精确应用。 |
| `VFXBAKE101–103` | Error | Preview/Bake 编译不等价、未参与字段变化或 Piloto 源/依赖 hash 变化。 |

报告按 `code → draftId → layerId → memberId → propertyId` 稳定排序。

## 18. v2 → v3 事务迁移

### 18.1 迁移原则

- 仓库中仍存在的 v2 草稿原地迁移，不保留双写。
- 迁移前把原文件完整字节复制到 `Temp/MxFramework/VfxAuthoring/Migrations/<draftId>/`。
- 写迁移 plan 和 receipt，记录原 hash、新 hash、工具版本和转换项。
- 先写临时文件，完成 v3 parse/validate/canonical roundtrip 后原子替换。
- 任何失败恢复原始字节。

### 18.2 字段转换

- `startOffsetSeconds` 按 60 FPS 转成整数 `startFrame`，必须使用固定舍入规则。
- 默认 `playbackSpeed=1`、`emissionScale=1`、`lifetimeScale=1`、`sizeScale=1`、白色 `colorTint` 不生成 v3 override。
- 非默认旧倍率不能自动猜成语义属性；该草稿停止迁移并要求人工转换。
- phase、loop mode、phase duration override、Transform、source reference 和 previewContext 保持。

### 18.3 Stale 重映射

Profile 或 Source 变化后，工作台显示三方比较：

```text
旧 Source/Profile
新 Source/Profile
用户 Draft Override
```

Stale Draft 可以查看、A/B 和试预览新源，但完成逐层重映射前禁止保存新的语义覆盖和烘焙。

## 19. 测试与质量门禁

### 19.1 Core/EditMode

- canonical JSON/hash 稳定。
- Registry 类型、单位、运算和范围校验。
- v3 roundtrip、稀疏默认和事务迁移。
- 六种 Retime Mode、事件锚点、最短 Body、完整周期和切线修正。
- MinMaxCurve/Gradient 所有模式和 color space。
- Profile dependency、stale 和三方重映射。
- Diff/Receipt 防篡改。
- 从 Source Snapshot 反复编译，无累计倍率误差。

### 19.2 Unity EditMode

- Main、Emission、Shape、Color/Size/Velocity/Rotation over Lifetime、Noise、Texture Sheet、Custom Data、Renderer 和 Material Adapter 精确应用。
- 未参与字段 canonical hash 完全一致。
- Preview 与 Bake 输出一致。
- MaterialPropertyBlock 复用。
- 派生 Material/Texture 去重。
- Custom Data/Vertex Stream 校验。
- Piloto 源 hash 始终不变。

### 19.3 UI Toolkit

- Inspector 四页。
- 成员矩阵状态与成员覆盖。
- 曲线轨道、MinMax、切线和帧/百分比时间域。
- 时间分段手柄和精确帧输入。
- Undo/Redo、Recovery Journal、显式保存。
- A/B、自动预览开关和增量更新。
- 相机、角色、Socket 和当前帧保持。

### 19.4 PlayMode

- Layer 精确起止。
- Build/Rebuild/Loop/Complete/Dissipate。
- OneShot/Continuous、周期重复和尾迹清理。
- Presenter pool 复用无 ghost。
- 生命周期与属性驱动稳态 0 B/frame。
- 20 次 Play/Rebuild/Complete/Dissipate 后无残留对象或材质状态泄漏。

### 19.5 时间保真硬门禁

- Entry/Exit 局部曲线值误差 `≤ 1e-5`。
- StartLocked、EndLocked、CycleLocal 事件误差 0 帧。
- 未参与字段 canonical hash 完全一致。
- 相同 Snapshot/Profile/Draft/Seed 产生相同编译结果。
- Preview 与 Bake 的最终字段 diff 一致。

### 19.6 性能目标

- 工作台首屏可交互 `≤ 1 s`。
- 已缓存 Element 切换 `≤ 300 ms`。
- 单 Element 冷 Snapshot/Profile load `≤ 2 s`。
- 自动预览 15–30 FPS 节流，不持续全工作台重建。
- Runtime Layer schedule/property Tick 初始化后 0 B/frame。

## 20. 第一批垂直切片 Fixture

按以下顺序落地，覆盖不同技术难点：

1. `Glow ExtendBody`：保持淡入/淡出绝对帧，只延长主体。
2. `Continuous Flame Emitter Window`：延长发射窗口并验证容量预算。
3. `Smoke ExtendTail`：Alpha/Size/Velocity 使用不同尾迹策略。
4. `Pulse/Rune RepeatBody`：吸附完整周期并重复 CycleLocal 事件。
5. `Multi Burst Impact EventRemap`：单调重排多 Burst，事件误差 0 帧。
6. `Fixed Flash`：只允许移动起点，长度编辑被正确阻断。
7. `UberFX Adapter`：材质局部时间、Primary Alpha 与 Emission EV。

每个 Fixture 必须保存 Source、修改后 Preview、Bake 结果和字段 Diff，并在暗色、亮色和复杂背景下固定 seed 捕获。

## 21. 分阶段交付

### Phase A：合同与无 Unity 编译核心

- Property Registry DTO/validator/canonical hash。
- Profile manifest 和 Component/Element/Shader Profile DTO。
- Draft v3、Receipt v3、Diff model。
- Retime Compiler 纯算法与 Fixture 单测。

### Phase B：Snapshot 与首批 Profile

- Unity Source Snapshot writer/store/lazy loader。
- 完整曲线、Gradient、Renderer、Material、Custom Data 扫描。
- 七个垂直 Fixture Profile。
- UberFXSG 首批 Shader Profile。

### Phase C：工作台 v3

- Inspector 四页、成员矩阵、曲线轨道和新时间线。
- v3 Draft 编辑、Undo/Redo、Recovery Journal。
- 自动预览开关、节流、增量重模拟和 A/B。

### Phase D：Unity Adapter 与 Candidate Bake

- 第一批 ParticleSystem/Renderer/Material Adapter。
- 派生 Material/Texture 去重。
- Candidate Prefab、Layer schedule、Property Driver 和 Receipt v3。
- Preview/Bake equivalence。

### Phase E：迁移与发布门禁

- v2 → v3 事务迁移。
- Stale 三方重映射。
- focused EditMode/PlayMode、视觉 Golden、性能/GC 门禁。
- Current 文档、Capabilities、Authoring Workflow 和 Quality Gate 同步为已实现状态。

### Phase F：规模化策展

- Fixture 通过后再分析约 200 个精选 Element。
- 只要求被实际修改的 Profile 完成 Confirmed binding。
- 用 Development Agent 生成绑定建议和影响分析，不自动确认 Profile。

## 22. 预期源码职责

命名可在实现时按现有程序集调整，但职责必须保持分离：

| 目标职责 | 建议类型 |
| --- | --- |
| Registry load/validate | `VfxSemanticPropertyRegistry` |
| Snapshot storage | `IVfxSourceSnapshotStore` |
| Profile storage | `IVfxAuthoringProfileStore` |
| Property backend | `IVfxSemanticPropertyAdapter` |
| Retime backend | `IVfxRetimeAdapter` |
| 语义编译 | `VfxSemanticCompiler` |
| 时间编译 | `VfxRetimeCompiler` |
| 稳定差异 | `VfxAuthoringDiffBuilder` |
| 四级校验 | `VfxCompositionV3Validator` |
| v2 迁移 | `VfxCompositionDraftMigratorV2ToV3` |
| Candidate 计划/执行 | `VfxCompositionBakePlannerV3` / Unity executor |
| 运行时属性轨道 | `UnityVfxBakedPropertyDriver` |

Authoring 类型为 `AllocByDesign`；运行时驱动类型必须在接口注释和实现中标记 `NoAlloc after initialization`。

## 23. 已确定决策与禁止的捷径

- 使用 v3 稀疏语义覆盖，不继续扩大 v2 粗倍率。
- 淡入/淡出默认保持绝对帧；普通延长默认改变 Body，不整体缩放时间。
- Cleanup 不计入艺术长度。
- 一个成员只能有一个 Primary Opacity Binding；Dissolve/Erosion 独立。
- 复合 Element 必须显式声明成员参与，不能统一乘倍率。
- Preview/Bake 从源快照重编译，禁止累计修改。
- 动态材质值用 MPB，静态渲染状态用可追踪派生 Material。
- 未知 Shader/Custom Data/复杂模块 fail-closed。
- 超范围可以成为 Experimental Candidate，但不能晋升。
- Candidate 只包含 VFX，不包含预览角色、动画、Socket 或 Editor 状态。
- Runtime 不读取任何 Authoring JSON。
- 不为追求“完美延长”引入每 Layer Update、运行时反射或每帧对象查找。
- 不通过手写 Unity YAML 实现 Prefab、Material、Shader Graph 或 `.vfx` 修改。

## 24. 文档维护

- 本文只描述语义属性与精确重定时详细设计，不复制 VFX Catalog、Primitive/Recipe、Piloto Source Library 或 Development Agent 全部 schema。
- [VFX 美术资产创作与语义管理设计](VFX_ASSET_AUTHORING_DESIGN.md) 负责总体模型和入口，并链接本文。
- [VFX 接口](Interfaces/Vfx.md) 只在 Runtime API/Controller 实际落地后记录 Current 契约。
- `CAPABILITIES.md`、`AUTHORING_WORKFLOW.md` 和 `QUALITY_GATE.md` 只在对应能力实际实现并验证后同步为 Current。
- 实现中若改变本文已确定的时间合同、Profile authority、Preview/Bake 同源或 Runtime NoAlloc 边界，必须先更新本文并说明兼容/迁移影响。

## 25. 完整创作流程与用户心智

### 25.1 从 Element 到可烘焙 Layer

一次正常手动创作必须按以下顺序发生：

1. 用户在左侧 Element 库单击素材，继续使用独立浮动小窗动态检查源效果；这一步不影响中央组合预览。小窗的“截为图标”使用当前模拟帧、当前小窗相机和当前空间辅助显示生成 `512 × 288` 的手工图标，并记录 Element、捕获时间、播放时间和镜头参数。手工确认图标固定写入 Git 可追踪的 `Assets/VFX/MxFramework/Authoring/ElementIcons/<stable-element-hash>/icon.png`，按稳定 Element ID 寻址，不依赖 Source Index 或临时 evidence 路径；自动 `t010/t025/t050/t075/t100` 捕获继续位于可清理、可重建的 `Temp/MxFramework/VfxSourceLibrary/ElementPreviews/`。素材卡读取顺序固定为“持久手工图标 → 旧版 Temp 手工图标（发现后自动迁移）→ 自动捕获 → 空白占位”，自动预览重建不得覆盖手工图标。截图必须复用小窗的 `PreviewRenderUtility.BeginPreview/EndPreview` 渲染路径，并在 PNG 回读前将 linear/HDR 结果显式解析到 sRGB；禁止使用独立 `Camera.targetTexture` 直接回读，以免发光、曝光和整体亮度与实时预览不一致。
2. 选中 Element 后按 `S`，才把它作为 Editor-only 临时试放层叠加到中央组合预览；再次按 `S` 移除。按 `A` 在“仅显示选中组件”和“选中组件 + Layer Stack 一起播放”之间切换。临时层共享当前相机、角色、Socket 和播放时钟，但不写入 Draft、Undo、恢复日志、预算或 Candidate；`A` 只切换组合 Renderer 的预览可见性，不禁用、不删除也不改写任何 Layer。Layer 显隐、Solo 和组合重编译不得把临时试放误写为正式 Layer。
3. 双击或拖拽 Element 时才把它加入当前生命周期阶段的 Layer Stack，并移除同一临时试放，避免画面重复。
4. 工作台解析 Snapshot、Profile 和成员能力，不直接把所有 ParticleSystem 字段暴露成一组无差别滑杆。
5. 右侧默认打开“语义”页，显示该 Element 已确认且适用的属性。
6. 用户先调整意图，例如“主体 120 帧”“淡入 12 帧”“发光 +1 EV”“保持发射密度”。
7. Compiler 生成最终字段，中央 Preview 在当前帧增量重模拟。
8. 用户可进入“成员”页调整哪些成员跟随宏控；进入“原始”页检查 Unity 字段；进入“差异”页审计写入。
9. 用户显式保存 Draft。保存不修改共享 Profile，也不制造 Prefab。
10. 用户显式烘焙 Candidate，系统才生成 Extraction/Derived Asset/Bake Plan、确认 diff，并通过 Unity Editor API 写入。
11. Candidate 必须人工播放检查后才能进入 ReviewNeeded；语义编辑本身不能批准 Ready。

### 25.2 编辑 Layer 长度的标准流程

用户拖动时间条右边缘或修改“目标艺术长度”时，UI 不直接改变 `lifetimeScale`，而是：

1. 读取当前 Layer 的 Profile 能力。
2. 若只有一个安全模式，直接使用并在控件旁显示，例如“延长主体”。
3. 若存在多个合理模式，第一次操作弹出轻量选择：延长主体、延长尾迹、重复主体、整体变速。
4. 后续拖动沿用该 Layer 的模式，直到用户从 Inspector 修改。
5. 时间条实时显示 Entry/Body/Exit 如何变化。
6. Burst 和保护点显示目标位置；无效映射立刻变红，不等到烘焙才报错。
7. 松开指针时形成一个 Undo 操作，而不是每个 PointerMove 都写一条历史。

当前 `layer_01_glow` 的实际首版执行链如下，后续实现不得退回粗倍率：

```text
时间条右手柄 90f → 180f
  → 只修改 layer.timing.targetArtisticFrames
  → 从绑定的 Source Snapshot/Profile 重新 Compile
  → ExtendBody 保留 Entry/Exit 绝对帧
  → 生成单调 timeMap
  → 重映射 Emission normalized curve、切线和 Burst frame
  → Preview 对新的只读源实例应用最终字段
  → Bake 把 layer.startFrame 写入统一 Scheduler
  → ParticleSystem 不再叠加相同的 startDelay
```

这意味着第二次把 180f 改回 120f 时，编译器仍从源 90f 快照计算；它不会拿“已经延长到 180f 的结果”再乘 `120/180`。`Entry`、`Exit`、StartLocked/EndLocked 事件和未参与字段因此能够做精确比较，并避免累计浮点与曲线切线误差。

### 25.3 “简单编辑”和“高级编辑”不是两套数据

- 简单编辑只显示语义主控。
- 高级编辑展开同一个主控的成员响应、曲线和最终字段。
- 原始页仍写入同一个 Draft v3 override 模型。
- 简单与高级切换不重新解释或丢失数据。
- 如果原始编辑使语义主控无法再可靠反推，主控显示“自定义”，而不是伪造一个近似数值。

### 25.4 Profile 确认与作品编辑分开

当扫描器不知道某个 Glow 的真实透明度来自 Start Color、Color over Lifetime、Material Alpha 还是 Custom Data 时：

- 普通创作者只能查看“待确认”绑定。
- 技术美术进入 Profile 模式，A/B 验证候选绑定。
- 确认后保存共享 Component/Shader Profile。
- 普通 Draft 自动获得该语义能力，但 source/profile hash 变化会触发重新编译。

工作台不得在普通 Layer 编辑中悄悄把一次临时猜测写成全局 Profile。

## 26. 工作台最终信息架构

### 26.1 总体布局

目标继续使用现有 UI Toolkit 三栏，不新建独立效果家族编辑器：

```text
┌ 顶部：资源库 / 创作实验室 / 手动组装 / Development Agent / Draft ┐
├───────────────┬─────────────────────────────────────┬──────────────────────┤
│ Element 素材库 │ Preview + 生命周期 + 时间线 + 曲线区 │ Layer Inspector      │
│ 320–420 px     │ 弹性宽度，最低 760 px               │ 420–540 px           │
│ 搜索/分类/卡片 │ 播放、角色、相机、A/B、阶段           │ 语义/成员/原始/差异   │
│ 独立快速预览   │ Layer Stack 与帧轨道                 │ 诊断、保存、烘焙      │
└───────────────┴─────────────────────────────────────┴──────────────────────┘
```

窗口过窄时按以下顺序降级：

1. Inspector 变成右侧可折叠抽屉。
2. Element 列缩到单列卡片。
3. 工具栏文字按钮改为图标 + tooltip。
4. Preview 始终保留最低可用面积，不能被表单完全挤没。

### 26.2 顶部上下文栏

从左到右：

- 当前 Draft 下拉：显示名称、stable ID、保存状态。
- 新建、打开、保存草稿。
- Source/Profile stale 状态。
- 自动预览总开关与实际更新频率。
- 语义覆盖、Production 覆盖和 diagnostics。
- 烘焙 Candidate 主操作。

未保存状态使用标题后的实心圆点，不用频繁弹窗。stale、Experimental、越界和错误必须分别显示，不合并成一个模糊“有问题”。

### 26.3 Preview 工具栏

第一行只负责播放语义：

- 首次播放。
- 触发重建。
- 完成停止。
- 消散停止。
- 播放、暂停、停止、重播。
- Loop 开关。
- 速度。
- A/B。

第二行负责空间与评审上下文：

- 根节点位置与归零。
- 辅助网格。
- 角色预览上下文。
- 背景。
- 透视、俯视、正面、侧面。
- Frame。
- 自动预览。

播放状态只在按钮组内体现，不再额外占用一个重复的长状态条。播放按钮必须有默认、Hover、Pressed、Focus、Playing、Paused 和 Disabled 状态。

角色预览上下文采用“共享默认 + 阶段稀疏覆盖”：

- 选择具体生命周期阶段时，编辑目标固定为该阶段；选择完整生命周期时默认跟随当前播放阶段，也可显式切换为“共享默认”。
- 继承字段在控件标签显示“继承”，顶部同时显示当前生效阶段、覆盖字段数量和“恢复本阶段默认”。恢复操作删除整个阶段覆盖，空 `phaseOverrides` 不序列化。
- 播放中开始编辑会立即暂停主预览和 A/B，并锁定当前阶段，避免阶段边界跨越后把修改写入错误阶段；再次播放解除锁定并恢复跟随。
- 完整预览在阶段边界解析并应用该阶段上下文，动画以阶段局部帧采样；Socket 同步切换，但相机、背景、根位置、网格和全局显示开关不变。
- 角色身份必须以 `characterStableId/packageId/characterEvidenceHash` 成组覆盖。纯 VFX 阶段可以保留但忽略角色证据。
- 同一角色跨阶段不重新实例化，只切换 Clip、Socket、循环和速度；不同角色实例按 Prefab 缓存在 Preview Session，窗口/草稿生命周期结束时统一释放。

### 26.4 Inspector 页签状态

选中页签必须同时具备：

- 高对比背景。
- 底部 2 px 强调线。
- 文本颜色变化。
- 键盘焦点轮廓。

只依赖文本颜色不够；深色主题下必须在低亮度显示器上仍可辨认。

### 26.5 底部操作区

操作分为三个明确层级：

- `保存草稿`：只写 Draft v3。
- `发布为共享 Profile`：可选的跨草稿复用操作，只在 Profile 模式出现并带影响分析；不属于 Candidate 烘焙前置步骤。
- `烘焙 Candidate`：执行 plan/diff/confirm/hash guard 后写 Unity 资产。

不能把三种写入合成一个“应用”按钮。

## 27. UI 视觉与交互规范

### 27.1 基础色板

建议色值用于建立一致视觉，不要求绕过 Unity 主题系统硬编码：

| 用途 | 建议色 |
| --- | --- |
| 工作台底色 | `#0F151B` |
| 面板底色 | `#151E26` |
| 次级面板 | `#1C2731` |
| 边框 | `#344654` |
| 主文字 | `#E7EDF2` |
| 次文字 | `#93A3AF` |
| 交互蓝 | `#38A9E8` |
| Hover 蓝 | `#55B9F0` |
| 成功/可用 | `#70C94B` |
| 警告 | `#E8A23A` |
| 错误 | `#E65B5B` |
| Experimental | `#C58AF0` |

### 27.2 生命周期颜色

| Phase | 颜色 | 时间条语义 |
| --- | --- | --- |
| Build | `#4DA6E8` | 首次出现 |
| Rebuild | `#8174E8` | 已有实例重建 |
| Loop | `#45C878` | 主体循环/单次主体 |
| Complete | `#E8A13D` | 正常完成 |
| Dissipate | `#D47FE8` | 取消/消散 |

Phase 颜色只表达生命周期，不复用为错误或选中颜色。

### 27.3 曲线颜色

- Source：`#73818B`，1 px。
- Semantic layer：`#38A9E8`，2 px。
- Final：`#F2F5F7`，2 px。
- Ghost member：成员主题色，30% alpha。
- 越界：`#E8A23A` 斜线填充。
- 无效区间：`#E65B5B` 半透明背景。

### 27.4 控件反馈

所有 Button、Tab、Card、Timeline handle、Curve key 必须有：

- Hover：亮度或边框变化。
- Pressed：向下 1 px 或背景加深。
- Focus：清晰 1–2 px 轮廓。
- Disabled：降低饱和度和对比度，但文字仍可读。
- Tooltip：说明行为、快捷键和禁用原因。

### 27.5 数值控件

语义数值采用“Slider + 精确输入 + 单位 + 恢复”组合：

```text
淡入长度    ─────●────  [12] 帧   ↶
```

- Slider 用于感受变化。
- 输入框用于严谨值。
- 单位始终可见。
- 恢复按钮恢复 Source/Profile 默认，不是写 0。
- 超出推荐范围时 Slider 进入紫色 Experimental 区，不静默 clamp。

## 28. 语义属性编辑器详细合同

### 28.1 时间与节奏控件

| UI 名称 | 属性 ID | 默认控件 | 精确行为 |
| --- | --- | --- | --- |
| Layer 起始帧 | Layer `startFrame` | IntField + 时间条拖动 | 只移动 Layer，不改变内部曲线。 |
| 视觉起点 | Layer `visualStart.mode` | Dropdown | 自动移除公共空白、保留源时间或手动前导；默认自动归零。 |
| 手动前导 | Layer `visualStart.manualLeadInFrames` | IntField | 仅手动前导模式可编辑；表示归一化后首个可见内容所在帧，不允许负值。 |
| 目标艺术长度 | `timing.targetArtisticFrames` | Slider | 唯一的总时长编辑权威；调用当前 Retime Mode。 |
| 淡入长度 | 编译结果 | 只读分段条 | 由目标艺术长度、源合同和当前 Retime Mode 推导。 |
| 主体长度 | 编译结果 | 只读分段条 | 由目标艺术长度、最短主体约束和当前 Retime Mode 推导。 |
| 淡出长度 | 编译结果 | 只读分段条 | 由目标艺术长度、源合同和当前 Retime Mode 推导。 |
| 清理长度 | 编译结果 | 只读分段条 | 默认由最大残留估算；不计艺术长度。 |
| 重定时方式 | `timing.retime.mode` | Dropdown | 七种受控模式；`PlaybackWindow` 只裁切 Layer 播放窗口，其余模式改变内部时间合同。不可用项显示原因。 |
| 周期长度 | 编译结果 | 只读分段条 | 来自已确认周期，并随支持该行为的 Retime Mode 编译。 |

`targetArtisticFrames` 是 Layer 总时长的唯一编辑权威。历史草稿中的
`timing.preRoll/entry/body/exit/cleanup/cycle.frames` 稀疏覆盖不再参与编译；创作者修改目标长度、重定时方式或最短主体时，Inspector 会清理这些旧覆盖，避免局部分段反向覆盖总长度。

Entry/Exit 还提供曲线预设：线性、Ease In、Ease Out、Smooth、保持源曲线、自定义。选择预设写完整曲线 override，不永久链接预设。

Inspector 必须同时显示只读的“源前导空白”和“最终首个可见帧”。`Layer startFrame`
负责 Element 在生命周期阶段中的整体位置；`visualStart` 负责 Element 启动后的可见起点；
`Pre-roll` 仍表示有艺术内容的准备段，三者不得合并。需要跳过实际内容时必须使用未来的
显式裁剪合同，不能用负手动前导实现。

### 28.2 外观控件

| UI 名称 | 语义 | Profile 要求 | 写入候选 |
| --- | --- | --- | --- |
| 整体可见度 | `appearance.opacity` | Primary Opacity Binding | Particle Gradient 或 MPB Alpha。 |
| 淡入/淡出包络 | `appearance.opacityEnvelope` | 明确 Layer/Emitter time | 烘焙曲线或属性驱动。 |
| 主色/辅色/细节色 | palette | Element member palette map | Start Color、Gradient、Material Color。 |
| 发光颜色 | emission color | Shader Profile | MPB/派生 Material。 |
| 发光强度 | exposure EV | Shader Profile + linear conversion | MPB float/color。 |
| Dissolve | dissolve | 独立 Shader binding | MPB curve。 |
| Erosion | erosion | 独立 Shader binding | MPB curve。 |

如果一个成员同时有 Particle Alpha 和 Material Alpha，Profile 必须选择 Primary；另一个可以声明为从属响应，但 UI 不能把两者都当成 100% 主透明度重复衰减。

### 28.3 发射控件

| UI 名称 | 来源 | 规则 |
| --- | --- | --- |
| 发射密度 | RateTime/RateDistance/Burst response | Element Profile 分配权重，不能盲目全乘。 |
| 每秒粒子 | Rate over Time | 保留 MinMaxCurve mode，修改 multiplier 或曲线层。 |
| 每米粒子 | Rate over Distance | 只对路径/移动效果开放。 |
| Burst 规模 | Burst count | 保持时间和周期，修改 count MinMaxCurve。 |
| 发射窗口 | duration/window binding | 与 Body/Loop 合同协同。 |
| 容量上限 | maxParticles | 默认 Auto，根据并发估算；允许向上取整手动覆盖。 |

“发射密度”是宏控，“每秒粒子/每米粒子/Burst”是详情。宏控修改后详情必须显示真实变化。

### 28.4 运动控件

| UI 名称 | 语义区分 | 注意事项 |
| --- | --- | --- |
| 初始速度 | Spawn velocity | 与 Velocity over Lifetime 分开。 |
| 持续速度 | Velocity over Lifetime | 按 Local/World/Custom Space 展示。 |
| 目标行程 | 结果导向宏控 | 只有 Profile 能可靠求解时开放。 |
| 世界重力 | World acceleration | 编译时转到 Simulation Space。 |
| 局部受力 | Local force | 不能标记为世界重力。 |
| Noise 强度 | displacement amplitude | 与 frequency 分开。 |
| Noise 频率 | spatial/temporal frequency | 不随整体强度隐式变化。 |
| 阻尼 | velocity decay | 无确认 Adapter 时只读。 |
| 旋转速度 | angular velocity | X/Y/Z 和 3D rotation mode 保持。 |

### 28.5 形态控件

| UI 名称 | 修改对象 | 默认策略 |
| --- | --- | --- |
| 整体构图大小 | 多成员宏控 | 由 Element Profile 分配到 Transform/粒子/Shape/行程。 |
| 根缩放 | Layer Transform | 只缩放实例根。 |
| 粒子尺寸 | Start Size + Size over Lifetime binding | 保持源曲线形状。 |
| 发射区域 | Shape 参数 | 选择保持总量或保持密度。 |
| 运动范围 | 速度/Lifetime/Force binding | 只有可解模型开放。 |
| 方向扩散 | Shape angle/random direction | 与粒子尺寸无关。 |

### 28.6 渲染控件

| UI 名称 | 类型 | 写入策略 |
| --- | --- | --- |
| 排序偏移 | Dynamic integer | Renderer sorting order offset。 |
| 材质 Profile | Static reference | 可能创建/复用派生 Material。 |
| 主贴图/Mask/Noise/Ramp/Normal/Flipbook | Asset reference | 按 Shader Profile slot 筛选。 |
| Flipbook 速度 | Curve/float | Texture Sheet 或 Material local time。 |
| Blend/Cull/ZWrite/Queue | Static enum | 只能创建派生 Material，不能 MPB。 |

## 29. MinMaxCurve 与 Gradient 编辑体验

### 29.1 MinMaxCurve 模式不能被扁平化

Inspector 必须保留 Unity 原模式：

- Constant：一个值。
- Two Constants：Min/Max 两个值。
- Curve：一条曲线和 multiplier。
- Two Curves：Min/Max 两条曲线和 multiplier。

语义主控作用于 `curveMultiplier`、Key value、时间映射或组合层，具体方式由 Binding 声明。不能因为 UI 只显示一条线就丢弃第二条曲线。

### 29.2 Gradient 模式

- Color。
- Two Colors。
- Gradient。
- Two Gradients。
- Random Color。

调色板操作默认做色相/明度/饱和度的受控映射，并保留 Alpha keys。若用户选择完全替换，必须显式显示 Replace 风险。

### 29.3 Key 编辑

- 单击选择。
- 双击新增 Key。
- Delete 删除，但每条曲线必须保持 Unity 有效的最少 Key。
- Shift 拖动限制单轴。
- Alt 拖动复制 Key。
- 数值区精确编辑 time/value/tangent/weight。
- 框选支持多 Key 一次平移。
- 任何编辑都是一个可撤销事务。

### 29.4 时间域提示

曲线标题必须显示：

```text
Opacity · Particle Lifetime 0–100%
Emission Envelope · Layer Frame 0–150f
Noise Strength · Emitter 0–100%
```

不能只写“Curve”，否则用户无法判断延长 Layer 是否应该移动该曲线。

## 30. Retime Compiler 模块级精确规则

### 30.1 Main Module

| 字段 | ExtendBody | ExtendTail | RepeatBody | WholeSpeed |
| --- | --- | --- | --- | --- |
| duration | 仅 EmitterWindow 成员扩展 | 通常不变 | 按完整周期扩展 | 按速度因子缩放 |
| startDelay | StartLocked，通常不变 | 不变 | 不重复 Entry delay | 按速度因子缩放 |
| startLifetime | 仅 ParticleResidence/尾迹策略允许修改 | 可按 Tail Profile 修改 | 通常不变 | 按速度因子缩放 |
| simulationSpeed | 默认不变 | 不变 | 不变 | 统一变速时修改 |
| maxParticles | 根据并发估算 | 根据尾迹驻留估算 | 根据周期并发估算 | 重新估算 |

### 30.2 Emission Module

- Rate over Time 的曲线若是 EmitterNormalized，随新的 Emitter window 重采样。
- 恒定 Rate 保持粒子/秒时，延长 Body 会增加总量；预算同步增长。
- 选择“保持总粒子数”时，Rate 按 `sourceBody/targetBody` 反向调整。
- Rate over Distance 不受静止时间延长影响。
- Burst 使用事件锚点；Count 与 Time 分开。
- Burst cycle 只有 Confirmed cycle 才能重复。

### 30.3 Color/Size/Velocity/Rotation over Lifetime

这些模块默认属于 ParticleNormalized，不随发射器 Body 变化。只有 Profile 明确标记为 emitter-driven approximation 时才参与 Layer retime。Particle Lifetime 变化时曲线仍保持 0–1 形状，实际帧标尺随 Lifetime 更新。

### 30.4 Noise

- Strength 可作为 ParticleNormalized 或 EmitterNormalized，必须由 Profile 指定。
- Scroll speed 在 WholeSpeed 时按局部时间一致性调整。
- Frequency 是空间频率，普通时间延长不改变。
- Octave/quality 属于结构/预算，不参与时间重定时。

### 30.5 Texture Sheet Animation

- `cycles` 表示粒子生命内周期时，跟随 Particle Lifetime，不跟随 Layer Body。
- Frame over Time 保持 ParticleNormalized。
- 若 Shader Adapter 使用 Material Local Time，则由单独 binding 处理。
- RepeatBody 不重复单粒子 Flipbook，除非 Profile 明确声明 emitter-synchronized flipbook。

### 30.6 Trails

Current 首个安全切片：

- Snapshot 精确记录 Trail lifetime、width/color curves、ratio、空间、ribbon 和 dieWithParticles。
- Component Profile 以 source Trail hash 和 expected dieWithParticles 绑定合同；输入变化后 Profile stale。
- 人工确认 `PreserveNormalizedLifetime` 合同后，`ExtendTail` 缩放粒子 Start Lifetime；Trail lifetime 是归一化比例，保持原值即可随粒子驻留时间同比延长，width/color 同样保持原值。
- `WholeSpeed` 继续由 ParticleSystem simulationSpeed 统一推进，不单独二次缩放 Trail。
- 改变 speed、startLifetime 或 travelRange 的任意语义属性仍可能改变可见尾迹长度，因此继续阻断。
- 没有合同、合同未知或 source Trail hash 不一致时 fail-closed。

仍未完成：Trail lifetime/width/color/ribbon 的独立语义编辑、材质 Trail 槽策展，以及对应视觉 Golden。

### 30.7 SubEmitter

SubEmitter 的触发事件必须成为依赖事件。父粒子 Lifetime、Death/Collision trigger 或 Burst 被重排时，需要专用 Adapter 同步；第一版没有 Adapter 时阻断相关重定时。

### 30.8 Collision、Limit Velocity 和外力

这些模块会改变实际行程，不能只靠速度 × Lifetime 估算。存在时：

- `motion.travelRange` 默认只读。
- WholeSpeed 必须验证动力学等价性。
- Profile 可提供经过验证的近似模型，但结果进入 Experimental，直到 Fixture 通过。

## 31. 复合 Element 解析规则

### 31.1 成员职责

每个成员必须有一个 primary responsibility，例如：

- Core
- Glow
- Flash
- Sparks
- Smoke
- Trail
- Decal
- Shockwave
- Debris
- Detail

职责用于默认宏控响应，但不能代替明确 Profile。

### 31.2 参与状态优先级

从高到低：

1. `Unsupported`：Adapter 不支持，用户不能强行覆盖。
2. `Unconfirmed`：扫描建议未确认，只能 Profile 模式处理。
3. `Excluded`：明确不参与当前宏控。
4. `Override`：成员使用自己的值或响应。
5. `Follow`：使用 Element Profile 默认响应。

### 31.3 示例：火球飞行复合层

假设一个 Layer 包含 Core、Glow、Sparks、Smoke：

| 属性 | Core | Glow | Sparks | Smoke |
| --- | --- | --- | --- | --- |
| ExtendBody | EmitterWindow | MaterialEnvelope | RepeatBody | ExtendTail |
| 整体强度 | 100% | 70% | 80% | 20% |
| 发光 EV | 100% | 80% | 20% | Excluded |
| 构图大小 | 40% | 60% | 30% | 80% |
| 主色 | Primary | Secondary | Detail | Secondary desaturated |

同一个 Layer Retime 可以解析成不同成员策略，但所有成员仍共享 Layer 起点、阶段和目标艺术终点。若成员 Cleanup 超出艺术终点，只延长技术清理，不强迫其它成员继续可见。

## 32. `layer_01_glow` 完整数值示例

### 32.1 Source

```text
Phase: Loop
Start: 0f
Pre-roll: 0f
Entry: 12f
Body: 48f
Exit: 18f
Cleanup: 30f
Artistic end: 78f
Technical end: 108f
```

绑定：

- Primary Opacity：Material Alpha envelope。
- Emission：Rate over Time，保持粒子/秒。
- Particle Color：ParticleNormalized，不参与 Layer Body retime。
- Material Local Time：Emitter-time Adapter。
- Start Burst：StartLocked 0f。
- End Spark：EndLocked，距艺术终点 4f。

### 32.2 用户操作

用户把目标艺术长度从 78f 改为 150f，模式选择 `ExtendBody`，淡入和淡出保持自动锁定。

### 32.3 编译结果

```text
Entry: 12f            不变
Body: 120f            48f → 120f
Exit: 18f             不变
Cleanup: 30f          不变
Start Burst: 0f       不变
End Spark: 146f       74f → 146f，仍距终点 4f
```

Unity 字段差异示例：

- ParticleSystem `duration`: `0.8s → 2.0s`，仅因为 Component Profile 声明 EmitterWindow。
- Rate over Time：保持源曲线/秒，不改变 multiplier；预算总粒子增加。
- Start Lifetime：不变。
- Color over Lifetime：不变。
- Material Alpha Entry curve：0–12f 原值完全一致。
- Material Alpha Body：12–132f 保持峰值。
- Material Alpha Exit curve：移动到 132–150f，局部值和切线一致。
- Material local time：Body 区间重新映射；Entry/Exit 使用各自局部时间。
- Cleanup 到 180f 结束，但阶段艺术长度仍是 150f。

### 32.4 UI 表现

- 时间条中 Entry/Exit 宽度不变，Body 明显变长。
- Source 灰线和 Final 白线在 Entry/Exit 局部坐标完全重合。
- Diff 页显示 6 个真实写入项，而不是一个 `lifetimeScale=1.923`。
- 预算从源估算更新为目标估算。
- A/B 同帧显示源效果已进入 Exit，而修改后仍处于 Body。

### 32.5 缩短失败示例

若用户输入 20f，而 Entry 12f + 最小 Body 1f + Exit 18f = 31f：

- 输入框变为错误状态。
- 时间条 0–31f 显示不可压缩区域。
- 提示“最短艺术长度 31 帧；淡入 12、主体最少 1、淡出 18”。
- Preview 保持上一次有效结果。
- Draft 可以保留无效输入用于继续编辑，但禁止保存为可烘焙状态。

## 33. Preview 状态机与失效范围

### 33.1 状态

```text
Unloaded
→ LoadingSnapshot
→ Compiling
→ ReadyPaused / ReadyPlaying
→ IncrementalRecompile
→ ReadyPaused / ReadyPlaying
→ Stale / Error
→ Disposed
```

### 33.2 失效层级

| 级别 | 示例 | 重建范围 |
| --- | --- | --- |
| UIOnly | 折叠面板、选择 Tab | 不编译、不重模拟。 |
| BindingValue | MPB Color/EV | 更新绑定，不重建 ParticleSystem。 |
| MemberSimulation | Emission、Lifetime、Size | 单成员恢复源状态并重模拟。 |
| LayerSchedule | startFrame、retime、phase | 当前 Layer 全成员。 |
| Composition | Layer 排序、组、跨层依赖 | 所有受影响 Layer。 |
| Source/Profile | hash 变化 | 标记 stale，禁止普通保存/烘焙。 |

### 33.3 当前帧保持

重模拟固定流程：

1. 记录当前 preview frame 和 play/pause 状态。
2. 重置受影响成员到固定 seed。
3. 从成员有效起点以固定 `1/60s` 模拟到当前帧。
4. 恢复播放状态。
5. 不修改相机、角色动画、Socket、根位置、其它 Layer 或 Element 快速预览。

## 34. Draft、Undo、恢复与并发保存

### 34.1 Undo/Redo 单位

以下每项形成一个操作：

- 完成一次 Slider 拖动。
- 完成一次时间条拖动或缩放。
- 完成一组多 Key 曲线编辑。
- 切换一个成员参与状态。
- 应用一次预设。
- 接受一次 Development Agent diff。

播放、相机、选择、展开状态不进入 Draft Undo。

### 34.2 Recovery Journal

Journal 保存：

- base draft hash。
- 有序操作列表或稳定当前快照。
- 最近有效编译 hash。
- stale/error 状态。

恢复时必须先加载磁盘 Draft，再显示恢复 diff。不能自动覆盖磁盘文件。

### 34.3 Expected-hash 保存

保存 Draft：

1. 比较打开时 base hash 与磁盘 current hash。
2. 相同则写临时文件、parse/validate、原子替换。
3. 不同则进入三方合并：base、disk、session。
4. Layer 顺序或同一属性冲突必须人工选择。
5. 保存成功后更新 base hash 和 Recovery Journal。

### 34.4 Development Agent 协作

Development Agent 只能提交 proposed Draft diff：

- 推荐 Element。
- 补齐职责缺口。
- 提出 Profile binding suggestion。
- 修改语义属性或成员参与。
- 优化预算。

用户逐项接受后才进入当前 Session；Agent 不能直接覆盖 Draft 或确认共享 Profile。

## 35. Authoring 接口详细合同

以下为职责级伪接口，具体命名可按程序集约定调整。它们都是 Editor/Authoring-only `AllocByDesign`，除最后的烘焙运行时数据外不进入公共 Runtime API。

```csharp
internal interface IVfxSourceSnapshotStore
{
    VfxSnapshotManifest LoadManifest();
    bool TryLoad(string snapshotHash, out VfxSourceSnapshot snapshot, out VfxDiagnostic diagnostic);
    VfxSnapshotWritePlan PlanWrite(VfxSourceSnapshot snapshot);
}

internal interface IVfxAuthoringProfileStore
{
    VfxProfileManifest LoadManifest();
    bool TryResolve(string profileId, string expectedHash, out VfxAuthoringProfile profile);
    VfxProfileSavePlan PlanSave(VfxAuthoringProfile proposed, string expectedHash);
}

internal interface IVfxSemanticPropertyAdapter
{
    string AdapterId { get; }
    bool Supports(VfxPropertyBinding binding, VfxSourceSnapshot snapshot);
    VfxCompiledFieldSet Compile(VfxPropertyCompileContext context);
}

internal interface IVfxRetimeAdapter
{
    string AdapterId { get; }
    VfxRetimeCapability Analyze(VfxMemberProfile member);
    VfxRetimeResult Compile(VfxRetimeContext context);
}

internal interface IVfxCompositionCompiler
{
    VfxCompositionCompileResult Compile(VfxCompositionDraftV3 draft, VfxCompileOptions options);
}

internal interface IVfxAuthoringDiffBuilder
{
    VfxStableDiff Build(VfxSourceSnapshot source, VfxCompositionCompileResult compiled);
}
```

接口结果必须返回稳定 diagnostics，不能以异常作为普通校验分支。

## 36. 编译结果与运行时烘焙数据

### 36.1 编译结果

每个成员编译结果至少包含：

- member stable ID。
- source snapshot hash。
- final transform。
- final ParticleSystem module field set。
- final renderer/material binding。
- dynamic property tracks。
- start/end/cleanup frame。
- warnings、Experimental reasons 和预算。

### 36.2 烘焙 Layer Schedule

运行时序列化数据应等价于：

```text
LayerSchedule
  phase
  startFrame
  stopEmissionFrame
  artisticEndFrame
  cleanupEndFrame
  memberStartIndex
  memberCount
```

数组按 phase、startFrame、稳定 layer order 预排序，初始化后不再排序。

### 36.3 烘焙 Property Track

```text
BakedPropertyTrack
  targetRendererIndex
  shaderPropertyId
  valueType
  startFrame
  endFrame
  curveIndex / constantValue
```

曲线可以保存为 AnimationCurve 或版本化采样表；实现选择必须以视觉误差、内存和 0 B/frame 测量决定。若使用采样表，采样率、插值和最大误差必须进入 receipt。

## 37. 错误反馈与修复入口

每个诊断必须包含：

- 发生位置：Draft、Layer、Member、Property。
- 原因。
- 是否阻断 Preview、Save、Bake 或 Promotion。
- 推荐修复入口。
- 相关 Profile/Source hash。

代表性快速修复：

| 问题 | 快速入口 |
| --- | --- |
| 缺少 Profile | 打开 Profile 模式并定位成员。 |
| Layer 越界 | 切换到对应阶段并 Frame 时间条。 |
| 目标长度过短 | 设置为计算出的最短长度。 |
| 容量不足 | 应用建议 maxParticles，并查看预算。 |
| Shader binding stale | 重新扫描 Shader Profile。 |
| Source stale | 打开三方重映射。 |
| Experimental 超范围 | 恢复推荐最大值或申请 Profile 扩展。 |

禁止只有 Console 文本而没有工作台内定位。

## 38. 键盘与鼠标交互

| 输入 | 行为 |
| --- | --- |
| Space | Preview 播放/暂停；素材列表聚焦且存在临时试放时控制中央组合与临时层同步播放。 |
| R | 重播当前组合；素材列表聚焦且存在临时试放时只重播临时层。 |
| S | 素材列表或 Element 小窗聚焦时，添加/移除当前选中 Element 的中央临时试放。 |
| A | 存在临时试放时，在“仅选中组件”和“选中组件 + Layer Stack”之间切换。 |
| F | Frame 当前 VFX/角色联合 Bounds。 |
| 1–5 | 切换 Build/Rebuild/Loop/Complete/Dissipate。 |
| 0 | 完整生命周期。 |
| Ctrl+S | 保存 Draft，不烘焙。 |
| Ctrl+Z / Ctrl+Y | Draft Undo/Redo。 |
| Delete | 删除选中 Layer 或 Curve Key，依据焦点上下文。 |
| Double-click Element | 添加一次 Layer。 |
| Drag Element | 拖入指定 Layer Stack 位置。 |
| Alt+Drag Key | 复制 Curve Key。 |
| Shift+Drag | 锁定时间条/曲线单轴。 |
| Esc | 优先移除中央临时试放层；否则关闭旧浮动 Element 预览或取消当前拖动。 |

所有快捷键必须服从焦点，不能在文本输入时触发播放或删除。

## 39. 视觉验收场景

### 39.1 Glow 延长主体

- A 为源 78f，B 为 150f。
- 0–12f 视觉与曲线一致。
- 132–150f 的 Exit 与源 60–78f 局部一致。
- B 只增加 Body 持续，不产生亮度突变。

### 39.2 Smoke 延长尾迹

- Alpha EndLocked。
- Size Continue，但不超过 Profile 最大值。
- Velocity Hold。
- Noise frequency 不变。
- Cleanup 正确延长且回池后无残留。

### 39.3 Rune 重复周期

- 周期 30f。
- 目标 Body 默认吸附 30 的整数倍。
- CycleLocal 闪光每周期同帧发生。
- Entry/Exit 只播放一次。

### 39.4 多 Burst Impact

- 三个 Burst 的目标帧可编辑。
- 时间映射严格单调。
- 子发射器存在时首版正确阻断，而不是只移动父 Burst。

### 39.5 角色挂载

- VFX 根持续跟随真实 Socket Transform。
- 修改 Layer 属性、显隐、Solo 或 Retime 不重置相机。
- 拖动时间轴时角色动画和 VFX 同帧。
- Candidate 不包含角色或 Animator。

## 40. 预期 UI 效果图规格

本设计配套四类高保真目标图，生成图只表达产品方向，不作为像素级验收来源：

1. `语义编辑总览`：三栏工作台、角色挂载预览、Layer Stack 时间分段、右侧语义属性。
2. `成员绑定矩阵`：Core/Glow/Sparks/Smoke 对强度、颜色、发射、尺寸、时间的 Follow/Override/Excluded 状态。
3. `精确重定时曲线`：`layer_01_glow` 从 78f 延长到 150f，Source/Semantic/Final 三线、事件锚点和 Entry/Body/Exit 分段。
4. `原始字段与差异审计`：语义操作到 Unity 字段、派生 Material、预算和 Experimental/阻断信息。

图片必须沿用当前 Unity 深色 UI Toolkit 工作台，而不是生成独立网页或科幻 HUD；中文标签清晰，控件密度接近真实 Editor 工具。

### 40.1 语义编辑总览

![VFX 语义编辑总览](Images/VfxSemanticPropertyRetiming/01-semantic-overview.png)

验收重点：

- 保留现有三栏工作台和顶部创作模式，不创建平行编辑器。
- Preview、角色挂载、生命周期、Layer Stack 和语义 Inspector 同屏。
- Layer 条直接显示 Entry/Body/Exit/Cleanup，不再用单一长度条掩盖内部时间合同。
- 右侧按时间、外观、发射、运动、形态和渲染分组；Slider、精确值、单位和恢复入口同时存在。

### 40.2 复合成员绑定矩阵

![VFX 复合成员绑定矩阵](Images/VfxSemanticPropertyRetiming/02-member-binding-matrix.png)

验收重点：

- `成员` Tab 显示成员 × 语义属性矩阵。
- 每个交叉项明确为跟随、覆盖、不参与、不支持或待确认。
- 选中成员后展示 Primary Opacity、Retime Adapter、响应权重、Profile 和 source hash。
- Profile 模式是共享绑定确认入口，不能混入普通 Draft 保存。

### 40.3 精确重定时与曲线

![VFX 精确重定时曲线](Images/VfxSemanticPropertyRetiming/03-exact-retiming-curves.png)

验收重点：

- A/B 使用相同 seed、相机、角色、背景和当前帧。
- StartLocked/EndLocked 事件、阶段分段、当前帧和目标总帧处于同一时间轴。
- Source 灰线、语义修改蓝线和 Final 白线可同时检查。
- `layer_01_glow` 延长主体时 Entry/Exit 绝对帧不变，事件误差为 0 帧。

### 40.4 Unity 字段差异审计

![VFX Unity 字段差异审计](Images/VfxSemanticPropertyRetiming/04-field-diff-audit.png)

验收重点：

- 每个语义操作都能追踪到 Profile Binding 和最终 Unity 字段。
- 未参与字段 hash、事件误差、Piloto 源 hash、预算和 Preview/Bake hash 独立显示。
- Experimental 示例不能污染当前有效修改的晋升状态。
- 导出 Diff、查看 Receipt 和烘焙 Candidate 是三个明确动作。

效果图是产品目标和信息架构证据，不替代真实 UI Toolkit smoke、不同窗口宽度验收或 Unity 版本下的最终像素检查。

## 41. 开发任务拆分与完成定义

### 41.1 每个任务的最小交付

- 一段可独立测试的合同或 Adapter。
- 对应 Fixture。
- 稳定 diagnostics。
- Preview 与 Bake 一致性证据。
- 文档 Current/Design 状态核对。

### 41.2 禁止“大爆炸式”一次替换

v3 不能通过把 v2 类整体改名后一次接管全部 200 个 Element。必须按：

1. 纯合同和算法。
2. 单成员 Fixture。
3. 复合成员 Fixture。
4. UI 编辑闭环。
5. Candidate Bake。
6. v2 迁移。
7. 扩大 Profile 覆盖。

逐阶段落地。每阶段失败时仍能打开旧 Draft 或恢复备份。

### 41.3 全量设计完成定义

0.1 所列首个垂直切片可以独立标记为 Current；但只有同时满足以下条件，才能把本文件整体从“Current initial vertical slice + Design remainder”更新为“Current”：

- Draft v3 与 Receipt v3 schema、canonical hash 和事务迁移已实现。
- 七个 Fixture 全部通过 Preview/Bake 等价验证。
- 四页 Inspector、曲线轨道、成员矩阵和时间条可实际操作。
- `layer_01_glow` 示例证明 Entry/Exit 保真且无累计倍率误差。
- ParticleSystem/Renderer/Material/Custom Data 第一批 Adapter 覆盖通过。
- Runtime schedule/property driver 测得初始化后 0 B/frame。
- Piloto 源资产 hash 不变。
- focused EditMode/PlayMode、Console、代码规范和文档健康检查通过。
- Current 文档明确列出已支持和仍只读的模块。

## 42. 首个垂直切片实现索引与验证证据

精选原材料库重建还必须遵守一条草稿稳定性约束：已被版本化 Composition Draft 引用、且其源成员仍然有效的 Element ID，不得因为配额重平衡或重复簇重选而消失。策展器先生成新的 200 项集合，再从上一版策展中恢复这些仍有效的草稿依赖，并只从未被引用、主题配额充足的单层 Element 中回收名额。这保证重新策展不会悄悄破坏 Fireball、Lightning 或用户保存的工作草稿；源成员真正失效时则仍按 stale/remap 流程阻断，不伪造兼容。

### 42.1 源码职责映射

| 文件 | Current 职责 | 分配分类 |
| --- | --- | --- |
| `Packages/com.vincent.wgameframework/Editor/VfxSemanticAuthoringModels.cs` | Property Registry、Profile、Snapshot、Fixture catalog、semantic override、diagnostic、canonical hash 数据模型与存储。 | Editor-only `AllocByDesign` |
| `Packages/com.vincent.wgameframework/Editor/VfxSemanticRetimeCompiler.cs` | 六种 Retime Mode、time segments、事件锚点、单调映射、曲线 frame remap 和稳定 compile result。 | Editor-only `AllocByDesign` |
| `Packages/com.vincent.wgameframework/Editor/VfxSemanticParticleSystemAdapter.cs` | ParticleSystem/Renderer/Custom Data 快照、Profile 解析、首批完整模块字段应用、MinMaxCurve/Gradient、字段 Diff 和属性轨道生成。 | Editor-only `AllocByDesign` |
| `Packages/com.vincent.wgameframework/Editor/VfxSemanticMaterialDerivation.cs` | MaterialPropertyBlock 动态属性、去重派生 Material、纹理/RenderQueue/Cull/ZWrite/Blend/Keyword 编译与回滚。 | Editor-only `AllocByDesign` |
| `Packages/com.vincent.wgameframework/Editor/VfxSemanticProfileConfirmation.cs` | 共享 Component/Shader/Element Profile 的 expected-hash、消费者影响分析、整批文件 + Manifest 事务保存和回滚。 | Editor-only `AllocByDesign` |
| `Packages/com.vincent.wgameframework/Editor/VfxCompositionV3Migration.cs` | v2 默认粗倍率草稿的字节备份、阻断检查、v3 roundtrip、原子替换和 receipt。 | Editor-only `AllocByDesign` |
| `Packages/com.vincent.wgameframework/Editor/VfxSemanticLayerInspector.cs` | 四页 Inspector、CurveField/MinMaxCurve、Source/Semantic/Final 曲线、成员矩阵、Renderer/Material 类型化控件、原始字段与 Diff。 | Editor-only `AllocByDesign` |
| `Packages/com.vincent.wgameframework/Editor/VfxCompositionAuthoring.cs` | Draft v3 guarded save、恢复日志、三方合并、stale 重映射、Candidate v3 receipt、Preview/Bake 证据与源完整性检查。 | Editor-only `AllocByDesign` |
| `Packages/com.vincent.wgameframework/Editor/VfxManualCompositionStudio.cs` | Layer 时间条、目标艺术帧、Preview dirty 范围、生命周期/角色/相机状态和 Inspector 装配。 | Editor-only `AllocByDesign` |
| `Packages/com.vincent.wgameframework/Vfx.Unity/UnityVfxBakedLayerScheduler.cs` | 按阶段浮点帧在精确整数边界 Play/Stop 烘焙 Layer；初始化缓存 ParticleSystem 数组。 | Configure `AllocByDesign`; Tick `NoAlloc after initialization` |
| `Packages/com.vincent.wgameframework/Vfx.Unity/UnityVfxBakedPropertyDriver.cs` | 缓存 Renderer、Shader Property ID 与 MaterialPropertyBlock，按烘焙曲线驱动 float/color。 | Configure `AllocByDesign`; Tick `NoAlloc after initialization` |

两个 Runtime MonoBehaviour 必须各自位于同名 `.cs` 文件。Unity 可以在内存中给不匹配文件名的类型 `AddComponent`，但 Prefab 序列化可能丢失脚本引用；Candidate 测试必须重新加载保存后的 Prefab 验证组件仍存在，不能只检查保存前实例。

### 42.2 数据与配置入口

| 路径 | 内容 | Git/Authority |
| --- | --- | --- |
| `Assets/Config/MxFramework/Vfx/AuthoringProfiles/vfx_property_registry.json` | 95 项受控 Property Registry。 | Git；Authoring contract |
| `Assets/Config/MxFramework/Vfx/AuthoringProfiles/vfx_semantic_retime_fixtures.json` | 七类代表 Fixture、属性/Adapter 依赖和不变量。 | Git；validation contract |
| `Assets/Config/MxFramework/Vfx/AuthoringProfiles/vfx_profile_manifest.json` | Profile manifest、profile hash 和 source hash。 | Git；Authoring contract |
| `Assets/Config/MxFramework/Vfx/AuthoringProfiles/Components/` | 确认后的 Component Profile。 | Git；共享技术美术事实 |
| `Assets/Config/MxFramework/Vfx/Compositions/` | Draft v3；当前四个样板。 | Git；作品草稿事实 |
| `Temp/MxFramework/VfxSemantic/Snapshots/` | 内容寻址 Source Snapshot blob。 | Git ignored；可重建证据 |
| `Temp/MxFramework/VfxSemantic/Migrations/` | v2 原始字节与 migration receipt。 | Git ignored；恢复证据 |
| `Library/MxFramework/VfxSemantic/Recovery/` | 未保存会话、base draft/hash，以及跨 domain reload 和 Editor 进程重启的恢复日志；兼容迁移旧 `Temp` 日志。 | Git ignored；持久本地恢复证据 |
| `Temp/MxFramework/VfxSemantic/ProfileTransactions/` | 共享 Profile 整批写入的临时字节、事务备份和回滚范围。 | Git ignored；事务证据 |
| `Assets/VFX/MxCandidates/Manual/` | 烘焙 Candidate Prefab。 | Git ignored；晋升前候选 |
| `Temp/MxFramework/VfxSourceLibrary/CompositionReceipts/` | Candidate bake receipt v3。 | Git ignored；可验证证据 |

### 42.3 当前 focused 验证快照

2026-07-16 属性能力与 Profile 缺口审计追加验证：

- Unity 6000.5.0f1 完成脚本 refresh/domain reload，Console 0 error。
- `VfxManualCompositionTests` EditMode job `6b61098d93c94fc0bf2dafbbbdbcf676`：83/83 通过，包含逐属性能力矩阵、成员/Layer 编辑范围、Profile 双重确认、依赖合并、确定性 `mx.vfx.profile-gap-report.v1`、七类 Fixture、Trail Snapshot/合同重定时、Preview/Bake 与源完整性回归。
- `MxFramework.Editor.csproj` 与 `MxFramework.Tests.csproj` build 0 error；Editor build 的 42 个 CS0649 均来自既有 CharacterAction JSON DTO，不属于 VFX 变更。
- `git diff --check` 与 UXML XML 解析通过。

2026-07-15 ParticleSystem v3 工作台完成以下 focused 验证：

- Unity 6000.5.0f1 refresh/compile，Console 0 error。
- `VfxManualCompositionTests` EditMode：59/59 通过，覆盖 v3 roundtrip、guarded save、恢复日志、三方合并、stale 重映射、Profile 批事务、七类 Fixture、六种重定时、完整 MinMaxCurve/Gradient、ParticleSystem/Renderer/Material/Custom Data、派生材质关键字、四页 UXML、Candidate Prefab、Preview/Bake hash 和源资产不变。
- `VfxLicensedSourceLibraryTests` EditMode：5/5 通过，覆盖当前 AssetDatabase Source Library 深度索引和授权来源边界。
- `UnityVfxLifecyclePlayModeTests` PlayMode：2/2 通过，覆盖连续 20 次 Build/Rebuild/Complete/Dissipate 无 ghost，以及 Scheduler/Property Driver 共用浮点帧、精确起止和清理。
- `BakedSchedulerAndPropertyDriver_AllocateZeroManagedBytesAfterInitialization` 在初始化和 warm-up 后执行 4096 次 Scheduler + Property Driver 稳态调用，当前线程分配差值为 0 B。
- `MxFramework.Editor.csproj` build 0 error；存在与本次 VFX 无关的既有 DTO 未赋值 warning。
- `MxFramework.Vfx.Unity.csproj` build 0 warning / 0 error。
- Candidate 测试实际捕获并修复了 Scheduler/PropertyDriver 脚本文件名导致的 Prefab 序列化丢失，当前重新加载 Prefab 后组件存在。

该快照证明 0.1 所列 ParticleSystem v3 工作台；它不把 0.2 所列只读模块、其它 Shader、像素级视觉 Golden 或 VFX Graph 提前描述为完成。

## 43. Current 实现的事务、证据与故障恢复细节

本节记录已经进入源码的精确行为，避免只留下产品级描述而无法诊断实际保存、重映射或烘焙问题。

### 43.1 共享 Profile 批事务

点击“发布为共享 Profile”不是 Candidate 烘焙步骤，也不是逐文件保存。发布服务先在内存中生成全部 Component Profile、可用 Shader Profile、Element Profile 和新的 Manifest，然后交给一个批事务：

1. 为本次事务生成唯一 transaction id，并在 `Temp/MxFramework/VfxSemantic/ProfileTransactions/<id>/` 建立证据目录。
2. 对每个目标记录目标路径、原文件是否存在、原始字节和 proposed bytes。
3. 重新检查打开时的 Manifest expected hash；过期即在写任何文件前失败。
4. 每个 proposed Profile 先 canonical serialize、回读并计算 content hash；Manifest 只引用这批已经验证的 hash。
5. 写入临时文件并替换目标；Manifest 永远最后写入。
6. 任一文件写入、parse 或 import 失败时，按相反顺序恢复所有原始字节；原本不存在的目标被删除。
7. 事务成功后，Draft 只绑定落盘后重新计算的 Profile hash，不绑定内存建议值。

影响分析列出：

- 将写入的 Component/Shader/Element Profile ID。
- 当前 Manifest hash 与 expected hash。
- 直接使用该 Element 的草稿。
- 通过共享 Component/Shader Profile 间接受影响的其它草稿。
- 旧状态、新状态、source snapshot hash 和消费者数量。

Profile 确认不会自动把任何 Effect 或 Primitive 设置为 Ready；它只确认“这个技术字段具有什么创作语义”。

### 43.2 Draft guarded save 与 Recovery Envelope

`Save Draft` 固定执行：

```text
session draft
→ compare loaded BaseFileHash with disk hash
→ serialize proposed bytes
→ write .tmp
→ deserialize + schema validate + canonical hash
→ move current file to .bak
→ atomically replace target
→ import through AssetDatabase
→ update LoadedPath/BaseFileHash
```

窗口内发生任何语义、Layer、阶段或 Preview Context 修改后，0.5 秒 debounce 到期会写 Recovery Envelope；即使关闭“实时刷新”也会写，因为关闭实时刷新只停止昂贵的 VFX 重模拟，不停止数据保护。Envelope 包含：

| 字段 | 用途 |
| --- | --- |
| `draftId` | 选择正确恢复会话。 |
| `sessionDraft` / `sessionHash` | 未保存的完整 v3 Draft 与 canonical hash。 |
| `diskPath` / `baseFileHash` | 打开时磁盘基线。 |
| `baseDraft` | 当 base hash 仍可证明时保存的三方合并基线。 |
| `writtenUtc` | 只用于提示新旧，不进入作品 hash。 |

成功保存后删除 Recovery Envelope；窗口关闭或 domain reload 前会强制 flush，而不是等待 debounce。

### 43.3 base/disk/session 三方合并规则

恢复或保存发现磁盘已变化时，每个 JSON 节点使用以下确定性规则：

| base | disk | session | 结果 |
| --- | --- | --- | --- |
| `disk == base` | 未被外部修改 | session 任意 | 取 session。 |
| `session == base` | disk 任意 | 用户未修改 | 取 disk。 |
| `disk == session` | 两边相同 | 两边相同 | 取该值。 |
| 对象的不同子字段分别变化 | 可递归合并 | 可递归合并 | 逐属性合并。 |
| 同一 scalar/array 两边不同 | 冲突 | 冲突 | 记录 JSON path 并阻断自动保存。 |

Layer 数组默认视作有序作品结构。外部改变顺序、用户同时增删 Layer 或两边修改同一 Layer 数组时不猜测意图，必须人工选择。

### 43.4 stale 依赖重映射

重映射计划比较 Draft 保存的 Source Index、Curation、Registry、Profile Manifest 和逐 Layer Profile dependency hash。逐层状态包括：

- Element 是否仍存在、是否 stale。
- 原成员 ID 是否仍存在。
- 原成员是否出现在唯一等价来源中。
- Component/Shader/Element Profile 是否存在及 hash 是否变化。
- 新 Registry 是否仍包含 Draft 使用的 property ID。

自动映射只接受两种无歧义情况：稳定 member ID 原样存在；或旧单成员只对应一个技术等价的新成员。多个相似候选、成员集合折叠、Profile 语义变化或属性被移除时保持 `Unresolved`。点击应用时再次读取所有当前 hash；计划生成后任何输入变化都会使该计划 stale，不能继续应用。

### 43.5 Renderer、Shader、Custom Data 与派生 Material

Renderer 类型化字段直接写 Candidate/Preview 实例：render mode、alignment、sort mode、pivot、min/max particle size、material GUID 和 mesh GUID。无效 GUID、错误资产类型和 Shader 不支持会产生稳定 Error diagnostic。

材质处理分两路：

- 动态且不改变渲染状态的 float/color 曲线编译成 MaterialPropertyBlock track；不实例化每帧 Material。
- Texture、Keyword、RenderQueue、Cull、ZWrite、SrcBlend、DstBlend 等静态状态进入去重派生 Material。派生 key 包含源材质 GUID/hash、Shader Profile hash、属性值和 Keyword 条件；相同 key 只产生一个派生资产。

UberFXSG 当前确认的合同包括 `_WhiteColor/_MidColor/_LastColor` 发光颜色、`_Alpha_Clip_Threshold` 侵蚀、`_MainTex` 主贴图、`_DetailNoise` 噪声和 `_AlphaOverride + _USEALPHAOVERRIDE` 遮罩路径。`@RenderQueue` 是 Profile 虚拟字段，映射到 `Material.renderQueue`，不能拿它调用 `Material.HasProperty`。Custom Data 只有 Shader Profile 明确声明 stream/channel 且 Renderer Vertex Stream 包含对应项时才能编辑；当前未确认的 UberFX Custom1 XYZW 保持只读。

### 43.6 Preview/Bake 等价与源完整性 Receipt

烘焙不是“预览看起来差不多就保存”。Candidate 保存前从同一 Draft 再建立一个独立、无场景副作用的 Preview evidence compile；两个编译都：

- 从新的只读源实例开始。
- 使用同一 Registry、已确认 Profile 和 Semantic Compiler。
- 应用同一 Material derivation 规则。
- 在 Material derivation 添加最终 Diff 后重新计算 Layer compile hash。

Receipt v3 的关键证据字段为：

| 字段 | 含义 |
| --- | --- |
| `previewCompileHash` | 独立 Preview evidence pass 的有序 Layer compile hash。 |
| `bakeCompileHash` | 实际进入 Candidate 的有序 Layer compile hash。 |
| `previewBakeEquivalent` | 两者严格相等；为 false 时不允许保留 Candidate。 |
| `fieldDiffHash` | 所有成员/属性/Unity path 排序后的字段 Diff hash。 |
| `sourceIntegrityHashBefore/After` | 启用 Layer 涉及的源 Prefab 文件和 dependency hash。 |
| `sourceAssetsUnchanged` | 前后完全相等；不等时删除 Candidate 并回滚派生资产。 |
| `candidateContentHash` | Unity 保存并重新 import 后的 Candidate 文件 hash。 |

`previewEvidenceHash` 绑定 Preview Context 与 `previewCompileHash`；角色、相机和 Socket 可进入评审证据，但仍不进入 `compositionContentHash` 或 Candidate 内容。

### 43.7 七类 Fixture 的机器合同

`vfx_semantic_retime_fixtures.json` 不是示例说明，而是版本化测试输入：

| Fixture | 模式 | 必须证明 |
| --- | --- | --- |
| Glow 主体延长 | ExtendBody | Entry/Exit 绝对帧、无累计倍率、Preview/Bake 等价。 |
| 持续火焰发射窗口 | ExtendBody | 只延长发射窗口/Body，粒子驻留和首尾不被整体缩放。 |
| 烟雾尾迹 | ExtendTail | 峰值与 Entry 保持，Alpha/Size/Velocity 尾迹策略显式。 |
| 脉冲符文 | RepeatBody | 只重复 48 帧完整周期，CycleLocal 事件不漂移。 |
| 多 Burst Impact | EventRemap | 单调分段映射、Burst 顺序稳定、事件误差 0 帧。 |
| 固定 Flash | Fixed | 内部 12 帧不可改，只能移动 Layer startFrame。 |
| UberFX Adapter | WholeSpeed | 局部时间、Opacity、Emission、RenderQueue、Vertex Stream 和源材质不变。 |

测试首先校验所有 `requiredPropertyIds` 都真实存在于当前 95 项 Registry，再逐 Fixture 编译两次并比较 compile hash。字段级等价是技术硬门禁；暗/亮/复杂/同色背景的像素捕获仍进入人工视觉评审，不因 hash 相同自动批准 Ready。

### 43.8 NoAlloc 实测边界

`UnityVfxBakedLayerScheduler` 在 Configure/Awake 缓存每个 Layer 的 ParticleSystem 数组；`UnityVfxBakedPropertyDriver` 缓存 Renderer、Shader Property ID 和每 Track 的 MaterialPropertyBlock。测试完成初始化和 128 次 warm-up 后，调用 GC、记录 `GC.GetAllocatedBytesForCurrentThread()`，再执行 4096 次 Scheduler + Driver 稳态 Advance；当前验证差值为 0 B。

这个结果只覆盖已经烘焙并初始化的 Tick：Editor 编译、Profile/Snapshot 加载、Candidate 烘焙和 Material 派生继续明确属于 `AllocByDesign`。
