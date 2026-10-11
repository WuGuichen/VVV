# 游戏音频管理全面评审与优化计划

> Status: Guide
>
> Date: 2026-07-16
>
> Review baseline: `main` @ `c5166ba8f61f`，与 `origin/main` 同步，Unity `6000.5.0f1`
>
> Scope: `MxFramework.Audio`、`MxFramework.Audio.FMOD`、`MxFramework.Audio.Physical`、Audio Workbench、FMOD Bank/Snapshot、Settings.Audio 与 CharacterTest 音频接入。
>
> Implementation: Runtime Closure Implemented / Content Publication Blocked by Human Audition

## 1. 文档目的

本文把 2026-07-16 对游戏音频管理的全面评审整理为长期可维护的优化计划。它同时记录：

- 当前实现与资产的可核验基线；
- 已确认缺陷、交付阻断项、风险候选和已规划能力；
- 运行时、混音、内容、资源、平台、诊断和验证的优化工作；
- 可拆成 Gitea Issue 的工作包、依赖顺序和退出条件；
- PR、nightly、发布和人工审听的验收矩阵。

本文不是新的接口权威，也不替代已有设计：

- 当前公共契约以 `Docs/Interfaces/Audio.md` 和源码为准；
- FMOD 长期架构以 `Docs/AUDIO_SYSTEM_FMOD.md` 为准；
- 物理音频心智模型以 `Docs/AUDIO_PHYSICAL_EXPRESSION_DESIGN.md` 和 ADR-007 为准；
- 当前能力以 `Docs/CAPABILITIES.md` 为准；
- 本文负责记录差距、排序优化和定义验收。

实施任一 C# 工作包前，仍必须读取 `Docs/API_STANDARDS.md`、`Docs/QUALITY_GATE.md` 和相关 `Docs/Interfaces/*.md`。涉及 Tick、事件发布、循环参数、批量实体或 FMOD 更新时，按高频路径处理，默认遵守 `NoAlloc after initialization`。

## 2. 状态口径

本文使用以下状态，避免把设计、骨架和真实可交付能力混在一起：

| 状态 | 含义 |
| --- | --- |
| Confirmed | 已由当前源码、资产或运行结果直接确认。 |
| Release Blocker | 若目标是宣称对应功能已可交付，则必须先关闭。它不等同于必现崩溃。 |
| Risk | 源码存在明确风险，但尚缺最小复现、Profiler 或目标平台证据。 |
| Planned | 已在设计或 Roadmap 中声明，但当前没有完整运行实现。 |
| Accepted Boundary | 有意保留的架构边界，不应被“优化”破坏。 |

任何后续状态更新都必须附当前 checkout、测试、Player 或 FMOD Profiler 证据，不能只根据文档中的 `Implemented`、`Active` 或 Event 已存在来判断完成。

## 3. 总体结论

这套音频系统不是“从零开始”的初级实现。它已经具备良好的 noEngine 契约、FMOD 适配边界、确定性 Physical Resolver、稳定 ID、Authoring Workbench、原子发布门禁和 CharacterTest one-shot 路由。

运行时治理已完成一轮实质收口：固定 command buffer、confirmed presentation drain、rollback 抑制、生命周期控制、移动 emitter、voice budget/优先级抢占、分组混音、玩家设置恢复、暂停 snapshot、音乐状态、环境持续 Event、Bank manifest/生命周期和真实 FMOD smoke 均已有实现与 focused 验证。FMOD 产品内容也已补入 Music、UI、Ambience、Voice，以及 Scrape/Roll/Compression 三类 3D 参数化持续 Event。

当前剩余阻断集中在内容治理而非运行时代码：Global/Physical Runtime Catalog 仍未通过 Workbench 成对发布，20 个既有 Active Cue 缺少当前版本的人工试听批准，18 个 Active Physical Basis 中多数缺少 Applied adoption set 或匹配批准证据。因此不能把“运行时可用”和“内容已发布”混为一谈。

综合判断：

| 维度 | 成熟度 | 当前判断 |
| --- | ---: | --- |
| 契约与依赖边界 | 8/10 | Gameplay 与 FMOD 解耦正确，noEngine 边界清楚。 |
| Authoring 与发布治理 | 8/10 | 三 manifest、expected hash、adoption、人工批准和回滚边界成熟。 |
| CharacterTest 表现集成 | 8/10 | one-shot、command drain、rollback 抑制、音乐/环境/设置链已接线。 |
| 生命周期、空间化和 voice 管理 | 8/10 | 句柄生命周期、移动 emitter、Bank scope、voice budget 与抢占已有实现和测试。 |
| 混音与玩家设置 | 7/10 | 9 个语义 Bus、snapshot、音量/静音恢复与 CharacterTest 设置面已接入；仍缺目标设备调音。 |
| 实际声音内容覆盖 | 6/10 | 已有 Music/UI/Ambience/Voice 和 3 类持续物理素材；大量 managed Physical Event 仍为空骨架。 |
| 测试与运行观测 | 8/10 | Unity 编译、focused EditMode、真实 Bank PlayMode smoke 和 voice budget smoke 已通过。 |

核心结论：音频运行时与 FMOD 技术链已达到可集成阶段，约 7–8/10；内容发布成熟度仍约 5–6/10。下一步不是继续堆第二套运行时，而是完成 Workbench 人工试听、adoption 和 Global/Physical Catalog 成对发布。

### 3.1 2026-07-16 收口证据

- Audio Core、Physical、FMOD、CharacterTest 和 Tests 工程编译：0 error。
- Unity refresh/compile 后 Console：0 error。
- focused EditMode：9/9 passed，覆盖 Bank manifest、Bank lifecycle、StreamingAssets 路径和连续物理音频无分配更新。
- focused PlayMode：2/2 passed，真实 Bank 自动 smoke 与 voice budget/priority steal 均通过。
- FMOD snapshot：123 Events、11 loop Events、3 parameterized Events、2 Banks、0 diagnostics、`cacheStale=false`。
- Product Music/Menu、Exploration、Combat 与 Ambience/Wind 均为 8 秒 loop。
- PhysicalContinuous/Scrape、Roll、Compression 均为 3D、4 秒 loop、Master Bank、5 个连续参数。
- Desktop Bank 与 `Assets/StreamingAssets` mirror hash 一致；`audio_bank_manifest.json` 由确定性生成器刷新。
- 发布诊断仍报告 20 个 Active Cue 缺少当前人工 audition approval；该门禁保留，不通过伪造证据绕过。

## 4. 必须保持的架构边界

### 4.1 权威与依赖

| 层 | 拥有 | 不拥有 |
| --- | --- | --- |
| Gameplay / Combat / CharacterAction | 动作、接触、命中、交互、帧和实体事实 | FMOD Event 路径、Bank、take、DSP、音频随机细节 |
| `MxFramework.Audio.Physical` | 确定性双方声学事实、Basis 选择、响应参数 | Unity Transform、FMOD API、素材文件 |
| `MxFramework.Audio` | Cue/Event/Bus/Parameter 契约、播放请求、句柄、结构化结果 | UnityEngine、UnityEditor、FMOD 类型 |
| `MxFramework.Audio.FMOD` | FMOD EventInstance、Bus/VCA、Bank/Listener/Emitter 适配 | Gameplay 规则和交互权威 |
| CharacterTest composition | 把实体、场景、Camera、Settings 和 Audio Service 组合起来 | 新的第二套音频规则或硬编码 FMOD 调用 |
| Audio Workbench / Authoring | 语义 Cue、Physical Basis、生成候选、采用、审听、发布 | Runtime gameplay authority |

必须继续遵守：

1. Gameplay 只通过稳定 Cue/Event ID 和 `IAudioService` 请求音频。
2. FMOD 类型、Event 路径和 Bank 不进入 noEngine 公共 API。
3. Physical Basis 选择与响应参数可复现；FMOD take 微随机不进入 Combat authority、Replay、SaveState 或 Runtime hash。
4. 音频是表现输出。Rollback 可以抑制、去重或延迟提交音频，但不能让 FMOD 状态成为模拟权威。
5. Authoring truth、生成候选、Physical Basis 和 runtime catalog 权威不能混写。
6. Active、Approved、Bank hash、snapshot 和实际可听内容必须是同一版本证据。

### 4.2 目标运行链

```text
Gameplay / Combat / CharacterAction facts
  -> surface-specific fact builder
  -> PhysicalAudioResolver or semantic Cue resolver
  -> preallocated AudioIntent / command buffer
  -> confirmed presentation drain
  -> IAudioService
  -> FMOD backend
  -> Event / Bus / VCA / Bank / listener-emitter lifecycle
```

### 4.3 目标发布链

```text
Semantic Authoring Manifest
Generation Manifest
Physical Authoring Manifest
  -> validate expected hashes
  -> generate / adopt / human audition
  -> FMOD managed Event materialization
  -> Build Banks
  -> export FMOD snapshot
  -> verify Bank/Snapshot/Approval evidence
  -> publish Global + Physical Runtime Catalogs
  -> mirror Generated and StreamingAssets outputs
  -> Player smoke
```

## 5. 当前可核验基线

### 5.1 Authoring 与 Physical 数据

| 项目 | 当前值 | 解释 |
| --- | ---: | --- |
| Semantic Cue | 108 | `20 Active / 88 Draft` |
| Semantic Event Definition | 108 | 每个 Cue 已有或预分配 Event 身份。 |
| Cue audition evidence | `108 Needed` | 当前 manifest 中没有 Cue 达到已审听状态。 |
| Bus | 9 | Master、Music、SFX、Combat、Ability、Physical、UI、Ambience、Voice。 |
| Acoustic material profile | 14 | 包含当前 Physical 材质模型。 |
| 显式 material pair | 90 | 当前 manifest 中实际持久化数量。 |
| Physical family | 6 | Footstep、Landing、Impact、Hit/Block、AirDisplacement 等。 |
| Physical Basis | 103 | `18 Active / 85 Draft`。 |
| 有 adoption set 的 Basis | 3 | 大多数 Active Basis 尚没有采用素材证据。 |

事实源：

- `Assets/Config/MxFramework/Audio/audio_semantic_manifest.json`
- `Assets/Config/MxFramework/Audio/audio_generation_manifest.json`
- `Assets/Config/MxFramework/Audio/audio_physical_manifest.json`

### 5.2 Runtime Catalog

当前 Global Runtime Catalog：

| 项目 | 当前值 |
| --- | ---: |
| Bus | 1 |
| Event | 9（上次已发布目录） |
| Cue | 19 |
| 3D Event | 9 |
| Loop Event | 0 |

当前覆盖集中在：

- Light Attack；
- Fireball Explosion；
- 4 个 Sole Contact；
- 4 个 Landing；
- 4 个 Object Collision；
- 5 个 Weapon Hit / Block。

上次已发布 Runtime Catalog 仍没有：

- Music / BGM；
- UI click / navigation / confirm / cancel；
- Ambience / environmental bed；
- Voice / dialogue；
- 新产品可控 loop 和持续物理 Event。它们已存在于 FMOD 与组合根 fallback，但尚未通过 Workbench 发布进新 Runtime Catalog。

运行目录还存在明显的内容复用：

- Light Attack 与 Fireball Explosion 仍共享 Demo OneShot3D Event；
- Landing 和多个 WeaponHit Cue 复用 ObjectCollision Event；
- 这些复用可以作为临时 fallback，但不能代表最终语义辨识度。

事实源：

- `Assets/MxFrameworkGenerated/Audio/audio_runtime_catalog.json`
- `Assets/StreamingAssets/MxFramework/Audio/audio_runtime_catalog.json`
- `Assets/Scripts/WGameFrameworkProject/CharacterTest/Presentation/Audio/CharacterTestAudioCueCatalog.cs`

### 5.3 Physical Runtime Catalog 缺口

当前两份预期输出都不存在：

```text
Assets/MxFrameworkGenerated/Audio/audio_physical_runtime_catalog.json
Assets/StreamingAssets/MxFramework/Audio/audio_physical_runtime_catalog.json
```

`CharacterTestRuntimeSessionBuilder.LoadAudioCueCatalog()` 要求 Global 与 Physical Catalog 同时存在、可解析、hash 匹配且能完成 binding；任何失败都会整份返回 `CreateLegacyFallback()`，不会部分合并。

因此当前事实是：

- 新 Physical v2 Resolver、103 Basis 和响应曲线已经有设计、数据与测试；
- CharacterTest 的条件路径已经接入；
- 当前 Player 实际运行仍整份使用 legacy fallback；
- 不能把“条件路径已实现”描述为“新物理音频已经交付”。

### 5.4 FMOD Snapshot 与 Bank

当前 FMOD snapshot（本节数据已按收口结果刷新）：

| 项目 | 当前值 |
| --- | ---: |
| Event | 123 |
| Loop Event | 11 |
| 参数化 Event | 3 |
| `lengthMs = 0` Event | 90 |
| `lengthMs = 30000` 的 non-loop Event | 5 |
| Bank | 2 |
| diagnostics | 0 |
| cache | `valid=true`, `stale=false` |

86 个 0ms Event 主要是 managed Physical 技术骨架，不是可听内容。5 个 30 秒 non-loop Event 包括部分 ObjectCollision、SoleContact、FireballExplosion 和 LightAttackHit；这不一定已经证明 voice 泄漏，但在缺少 voice budget 的前提下必须用 FMOD Profiler 验证真实生命周期。

当前 Air Displacement Event：

```text
event:/MxFramework/Generated/Physical/
  audio.family.air_displacement/
  audio.basis.air_displacement.light_attack
```

其 snapshot `lengthMs=0`，Semantic metadata 为 `contentStatus=Draft`，adoption set 为空，audition 为 `Needed`。它只有 OneShot/3D/Bank/距离技术身份，没有可交付声音。

Bank 镜像当前一致：

| 文件 | 大小 | SHA-256 |
| --- | ---: | --- |
| `Master.bank` | 以最新 `audio_bank_manifest.json` 为准 | 由 `generate_audio_bank_manifest.py` 确定性生成 |
| `Master.strings.bank` | 以最新 `audio_bank_manifest.json` 为准 | 由 `generate_audio_bank_manifest.py` 确定性生成 |

FMOD `Build/Desktop` 与 `Assets/StreamingAssets` 的对应文件内容一致。这证明镜像没有漂移，不证明每个 Event 有声音、Bank 可在全部目标平台加载，或参数 automation/DSP 已正确连接。

## 6. 当前强项

### 6.1 noEngine 核心边界正确

`IAudioService`、`IAudioBackend`、稳定整数 ID、结构化结果、Null backend 和 FMOD adapter 已形成清楚分层。当前没有发现 Gameplay/Combat 直接调用 `RuntimeManager`；CharacterTest 对 `FMODUnity` 的直接依赖只位于组合根 listener 装配。

### 6.2 CharacterTest one-shot 汇流方向正确

动作、AirDisplacement、物理接触、WeaponHit 和 interaction audio 先进入固定容量 `CharacterTestAudioIntentQueue`，最后由一个 drain 提交。参数 scratch 按数量预分配，队列溢出有 `DroppedCount`，这比各系统随处直接调用 FMOD 更易治理。

### 6.3 Physical Resolver 可复现

双方材质、质量、接触面积、表面、构造、速度、强度、角度与 Variant 进入确定性选择；Basis tie-break、fallback 和 response 参数已具备纯 C# 测试。FMOD 只负责 Event 内的层、take 和空间表现，权威边界正确。

### 6.4 Authoring 与发布门禁成熟

当前已有：

- Semantic / Generation / Physical 三 manifest expected hash；
- typed ID 分配与退休 ID 不复用；
- adoption set 与 Basis string/runtime ID 绑定；
- FMOD managed namespace；
- Bank build、mirror、snapshot、rollback；
- 人工 Preview/Approved 证据；
- Global/Physical Catalog 成对发布；
- 目录无效时 all-or-nothing fallback。

后续重点不是再造一套 Authoring，而是让现有门禁真正产出 Player 可用目录，并把可听验收纳入交付。

## 7. 优化总表

| ID | 优先级 | 状态 | 问题 | 目标 Phase |
| --- | --- | --- | --- | --- |
| AUD-REL-001 | P0 | Release Blocker | Physical Runtime Catalog 两份输出缺失，实际整份 legacy fallback。 | 1 |
| AUD-REL-002 | P0 | Release Blocker | AirDisplacement Event 为 0ms Draft 空骨架。 | 1 |
| AUD-REL-003 | P0 | Confirmed | 已保存的音量/静音在启动和 Session 重建后不恢复。 | 1 |
| AUD-REL-004 | P0 | Confirmed | 没有真实 FMOD Standalone Player 可听验收。 | 0–1 |
| AUD-RUN-001 | P1 | Confirmed | 暂停没有音频 pause/duck/snapshot 语义。 | 2 |
| AUD-RUN-002 | P1 | Confirmed | Rollback replay 会再次提交不可撤销 one-shot。 | 2 |
| AUD-RUN-003 | P1 | Confirmed | Session/场景切换没有 StopAll、fade 或 persistent scope。 | 2 |
| AUD-RUN-004 | P1 | Confirmed | Framework PostSimulation 模块只 Tick backend，没有统一音频 command drain。 | 2 |
| AUD-RUN-005 | P1 | Confirmed | `Priority` 未使用，无 voice/concurrency budget 和通用限流。 | 3 |
| AUD-RUN-006 | P1 | Confirmed | 3D 只写启动位置，Emitter 不跟随，没有 velocity/orientation 更新。 | 3 |
| AUD-RUN-007 | P1 | Risk | active handle Tick 在枚举字典时写回，需在当前 Unity 运行时验证。 | 3 |
| AUD-RUN-008 | P2 | Confirmed | Runtime stop/dispose、stale handle 和 Null/FMOD 语义不一致。 | 2–3 |
| AUD-RUN-009 | P2 | Confirmed | 公共接口未声明 main-thread-only，也没有线程切换 command queue。 | 2 |
| AUD-RUN-010 | P1 | Confirmed | 3D Event 可接受 2D 请求并静默落在世界原点，空间请求策略不明确。 | 1–3 |
| AUD-MIX-001 | P1 | Confirmed | 只有 Master Bus，没有产品级 mix hierarchy。 | 4 |
| AUD-MIX-002 | P1 | Confirmed | 缺 Music/UI/Ambience/Voice 和对应内容生命周期。 | 4 |
| AUD-MIX-003 | P1 | Confirmed | 多个语义 Cue 复用 Demo/Collision Event，辨识度不足。 | 1、4 |
| AUD-MIX-004 | P1 | Confirmed | 默认 Bus 状态未应用；VCA mute 后调 volume 会产生状态分裂。 | 1–2 |
| AUD-RES-001 | P1 | Confirmed | 没有显式 Bank/sample warmup、卸载、场景作用域和内存预算。 | 5 |
| AUD-RES-002 | P1 | Confirmed | Bank validator 只查文件名/存在性，1-byte 假 Bank 也可通过。 | 0、5 |
| AUD-PLAT-001 | P1 | Confirmed | 只定义 Standalone FMOD；Player Catalog 使用普通文件 API。 | 5 |
| AUD-OBS-001 | P1 | Confirmed | Snapshot 看不到 one-shot、recent commands、Bank/listener/voice/DSP。 | 3、5 |
| AUD-OBS-002 | P2 | Confirmed | Service 层拒绝的错误不进入 backend snapshot。 | 3 |
| AUD-PERF-001 | P1 | Confirmed | SetParameter 诊断字符串、GUID 解析等不满足持续参数 NoAlloc。 | 3 |
| AUD-DATA-001 | P2 | Confirmed | Runtime Catalog 数组可被外部修改，不是真正 immutable。 | 3 |
| AUD-DATA-002 | P2 | Confirmed | 参数 min/max/kind 未在 Service 层校验。 | 3 |
| AUD-TEST-001 | P1 | Confirmed | 测试偏 mock/解析，缺真实 Event/Bus/loop/listener/player 覆盖。 | 0–5 |
| AUD-TOOL-001 | P2 | Confirmed | Workbench smoke 是源码 token smoke，不是浏览器 E2E。 | 5 |
| AUD-DEMO-001 | P2 | Confirmed | `FmodAudioDemoRunner` 没有可打开的场景入口。 | 0–1 |

## 8. Release Blocker 细节

### 8.1 AUD-REL-001：Physical Runtime Catalog 缺失

当前证据：

- `CharacterTestRuntimeSessionBuilder.cs:857-951` 明确要求 Global 与 Physical Catalog 成对加载；
- 当前两份 `audio_physical_runtime_catalog.json` 均不存在；
- `Docs/Interfaces/Audio.md:179` 和 `Docs/AUDIO_PHYSICAL_EXPRESSION_DESIGN.md:214` 同样记录当前 checkout 使用 legacy fallback。

玩家影响：

- 新的 Pair/Variant、Basis、双侧材质 fallback 和 response profile 不进入实际选择；
- 动态事实即使已经被 CharacterTest 构建，也无法走新 Physical Catalog；
- Workbench 的 103 Basis 覆盖不能改变当前 Player 播放结果；
- 新旧目录不能合并，因此缺一份文件就会整份回退。

目标状态：

1. Semantic、Generation、Physical 三 manifest hash 当前；
2. 所有准备发布的 Active Cue/Basis 都有有效 adoption、snapshot、Bank 和人工批准证据；
3. 生成 Generated Physical Catalog；
4. 镜像到 StreamingAssets，内容与 Generated 完全一致；
5. Global Catalog hash 与 Physical Catalog 声明一致；
6. CharacterTest 启动日志明确报告成对目录加载成功；
7. release build 禁止静默进入 legacy fallback。

验收：

- Editor 和 Standalone Player 都加载新目录；
- 用至少 Footstep、Landing、ObjectImpact、WeaponHit/Block 和 AirDisplacement 各一个真实场景证明 resolver 结果进入实际播放；
- 删除或篡改任一目录时，测试能观察到结构化错误和预期 fallback；
- Release 配置把 fallback 计入失败或显式降级指标，而不是只写普通 warning。

### 8.2 AUD-REL-002：Air Displacement 空骨架

当前证据：

- CharacterAction 三段轻攻击已经发出 `sfx.air_displacement.light_attack`；
- Semantic Cue/Event/Basis 身份已分配为 Active；
- FMOD snapshot 对应 Event `lengthMs=0`；
- metadata `contentStatus=Draft`；
- adoption set 为空；
- audition evidence 为 `Needed`；
- 当前发布门禁会正确阻止它进入 runtime catalog。

这说明门禁正在按设计工作，问题不应通过放宽门禁解决。正确动作是完成内容，而不是把空 Event 标记 Ready。

目标状态：

- 按现有 recipe 生成候选；
- 完成校准与人工选择；
- adoption set 进入 Applied；
- FMOD Event 含真实可听 source/instrument；
- snapshot 时长、Bank、Event binding 当前；
- Unity Preview 和目标 CharacterTest 场景均人工审听；
- 生成的 Physical Catalog 将该 Basis 与 Cue/Event 正确绑定。

验收重点：

- 破空声必须与命中声分离；空挥也能听到破空，命中时两层语义不混淆；
- 不应借用 `sfx.light_attack` 的命中 Event；
- 声音在规定的自然衰减窗口内结束，无 baked ambience、长混响尾或第二次冲击；
- 3 次挥击峰值的触发时序符合 CharacterAction presentation frame；
- 快速连击时受 voice/rate policy 管理，不出现堆叠爆音。

### 8.3 AUD-REL-003：Settings.Audio 启动恢复缺陷

当前证据：

- `CharacterTestSettingsRuntime.Create()` 会加载保存的 profile；
- `CharacterTestSettingsRuntime.AttachAudioService()` 只确保默认 Bus、注册 applier 并保存 service 引用；
- Attach 后没有调用 `ApplyCurrentProfile()` 或等价路径；
- `CharacterTestRuntimeSessionBuilder` 每次重建 Session 都创建新的 Audio Service，然后只调用 Attach。

`Docs/Interfaces/Settings.md` 当前明确规定 profile 不默认自动应用，因此这不是违反现有 Settings 公共契约的实现回归，而是 CharacterTest 产品发布要求与现有默认策略之间的缺口。若决定让 Audio 在启动/重建后自动恢复，必须同步更新 Settings 契约，并明确 Graphics/Input 是否继续保持“不自动应用”；不能只在 CharacterTest 内暗改全局语义。

玩家影响：

- 保存静音后重启游戏会恢复出声；
- 切换 Offline/Host/Client 或重建对局时音量恢复默认；
- UI 工作 profile 与 FMOD 实际 Bus 状态分裂；
- snapshot 显示的 Bus 状态可能与玩家期望不一致。

目标状态：

- Attach 新 service 后立即、幂等地应用当前 working/current profile；
- Session rebuild、网络模式切换和场景重建使用同一条恢复路径；
- 应用失败进入可见诊断，但不破坏 Session 创建；
- profile 中未知 Bus 有明确迁移或忽略规则。

验收：

1. 设置 `Master=0.25` 且 muted，保存；
2. 重建 Session；
3. 断言新 Audio Service 收到 volume 与 mute；
4. 重启 CharacterTest；
5. Standalone Player 实听和 snapshot 都保持相同状态。

### 8.4 AUD-REL-004：真实可听验收缺失

当前自动化能够证明：

- Catalog JSON 可解析；
- Cue/Event 映射正确；
- Null backend、resolver、cooldown 和 intent queue 行为正确；
- FMOD compile symbol 下 backend 可以初始化；
- Authoring/Workbench/Generation pipeline 的结构化规则通过。

当前自动化不能证明：

- Bank 在目标 Player 中真实加载；
- Event 有声音而不是 0ms/静音骨架；
- listener 距离、朝向和 3D 衰减正确；
- moving emitter、loop、stop/fade 正确；
- pause、settings reload、Session rebuild 正确；
- voice flood 时没有爆音、截断错误或内存增长；
- 不同平台的 Bank 与 StreamingAssets 访问正确。

优化方向不是让测试判断“听感好坏”，而是建立两层门禁：

1. 自动 Player smoke 证明 Event/Bus/lifecycle 可执行且有可测信号；
2. 人工审听证明素材、层次、响度、空间和游戏反馈满足内容要求。

## 9. Runtime 生命周期与确定性表现

### 9.1 AUD-RUN-001：暂停语义

`GameManager.Update()` 在 `_context.Paused` 时停止普通 simulation/presentation Tick，但没有调用音频 pause、Bus mute、VCA、snapshot 或 ducking。FMOD 自身仍会继续更新，已启动声音不会跟随 RuntimeHost 暂停。

需要先定义产品语义：

| 声音类别 | 暂停菜单目标行为 |
| --- | --- |
| Gameplay SFX | 暂停或快速 fade；恢复时继续或结束由 Event scope 决定。 |
| UI | 继续播放。 |
| Music | 通常继续并 duck，具体由 snapshot 决定。 |
| Ambience | 可暂停、duck 或保持，必须显式配置。 |
| Voice | 根据剧情/联网语义决定，不使用全局硬编码。 |

目标实现应使用稳定 mix/snapshot policy，不让 GameManager 直接操作 FMOD 原生对象。`IAudioService` 可以增加低频 presentation-state API，或通过专用 Audio AppFlow adapter 接收 Pause/Menu/Scene 状态。

验收：

- 打开暂停菜单后 gameplay SFX 行为一致；
- UI 声仍可用；
- 恢复不产生双播、瞬时满音量或丢失 Bus 设置；
- 网络面板允许继续 Tick 的特殊路径有独立规则。

### 9.2 AUD-RUN-002：Rollback 重复播放

`ReplayRollbackFrame()` 会直接调用真实 `Slice.Tick()`。当前 `SetNetworkRollbackReplayActive(true)` 只抑制 LocalInputCommands，不抑制音频 intent，也没有 confirmed-frame 提交或去重。one-shot 一旦提交给 FMOD 不能回滚。

候选策略：

1. **Replay suppression**：Rollback replay 期间只重建 authority，不提交表现音频；
2. **Confirmed-frame drain**：音频 intent 等到 confirmed presentation frame 再提交；
3. **Stable dedupe key**：以 `frame + emitterId + cue/event + ordinal/trace` 去重；
4. **Hybrid**：本地即时反馈允许预测播放，回滚重演抑制，并在误预测时接受不可撤销表现误差。

推荐先采用 suppression + stable dedupe，再根据网络体验决定是否需要 confirmed-frame 延迟。不得把 FMOD EventInstance 放进 rollback snapshot。

验收：

- 人为制造 rollback，原本已播放的同一命中不会重复；
- 新确认的音频事实仍能播放；
- 本地预测攻击的反馈延迟有明确预算；
- 去重缓存有固定容量、稳定淘汰和 NoAlloc 行为。

### 9.3 AUD-RUN-003：Session/场景作用域

普通 one-shot 在 `start()` 后立即 `release()`，不会进入 active handle table。释放只是把 FMOD 实例所有权交还系统，并不保证 Session 重建时立即停止。因此：

- Dispose 只能主动停止仍在 handle table 中的可控 Event；
- one-shot 可能跨 Session、场景或游戏状态继续尾音；
- 当前没有 persistent Music、scene ambience 和 transient gameplay 的作用域区别；
- 没有 StopAllByScope、fade 或 scene transition policy。

目标作用域：

| Scope | 示例 | 生命周期 |
| --- | --- | --- |
| TransientFrame | hit、footstep、UI click | 启动后自然结束；Rollback 需去重。 |
| Entity | charge、aura、projectile flight | 跟随 emitter，实体销毁时 stop/fade。 |
| Scene | ambience、weather、room tone | 场景进入 warmup，离开 fade/unload。 |
| AppPersistent | music、global voice chat | 跨场景，由 AppFlow 所有。 |

验收：

- Session rebuild 不残留旧 gameplay 声；
- Scene 离开后 Scene scope 声音按策略 fade；
- AppPersistent 声音不被 gameplay slice 错误 Dispose；
- owner 销毁后 Entity scope handle 不泄漏。

### 9.4 AUD-RUN-004：统一提交边界

CharacterTest 内部已经把多个系统汇流到一个固定 intent queue，但 drain 仍发生在 CharacterRuntime 的 Simulation Tick 中并同步调用 `IAudioService.PlayOneShot()`。Framework `AudioRuntimeModule` 的 PostSimulation 职责只是转发 backend `Tick()`。

当前影响：

- `Frame` 与 `Priority` 只是请求字段，没有 framework 调度意义；
- 不同 game slice 需要重复实现 drain、排序、限流和 rollback policy；
- service 调用时机仍依赖调用方；
- 不能统一统计提交前 dropped/suppressed/merged intents。

目标状态：

- caller-owned 或预分配 Audio Command Buffer；
- Simulation 只写稳定表现 intent；
- PostSimulation/PresentationConfirm 阶段统一排序、去重、限流和提交；
- command result summary 区分 requested、deduped、rate-limited、voice-rejected、backend-failed；
- 保留当前 CharacterTest 固定队列的 NoAlloc 优点。

### 9.5 AUD-RUN-008/009：Stop、Dispose 与线程契约

当前存在的语义漂移：

- 文档倾向把 Stop 描述为可幂等操作；FMOD backend 对 stale handle 返回 `InvalidHandle`；
- Null backend 对已不存在的有效 handle Stop 返回成功；
- Null `SetParameter` 不验证 handle 是否仍活跃；
- `AudioRuntimeModule.Dispose()` 在 service registry 解析路径中不会 Dispose `_resolvedAudioService`；
- Audio Service/Backend 使用无锁 Dictionary/Queue 并同步调用 FMOD Unity API，但公共接口没有声明 main-thread-only。

优化要求：

1. 固定 Stop stale-handle 契约并让 Null/FMOD 一致；
2. 固定 Service owner 和 Dispose 责任；
3. 对 registry-injected 与 constructor-injected 路径都测试；
4. 公共 API 明确 main-thread-only，或所有跨线程请求进入固定 command queue；
5. 禁止后台网络/Authoring callback 直接调用 FMOD Unity API。

## 10. Controlled Event、Emitter 与 Voice 管理

### 10.1 AUD-RUN-005：Voice/concurrency budget

当前 `AudioPlayRequest.Priority` 没有被 backend 使用。系统没有：

- 全局 real/virtual voice 预算；
- 每 Event 最大实例数；
- 每 emitter 最大实例数；
- 每帧/时间窗 rate limit；
- 距离、重要性或年龄裁剪；
- 相同 Cue 合并；
- voice steal 原因诊断。

CharacterTest 的部分物理系统有局部 cooldown，但它不是通用 voice policy，无法保护 UI、技能、投射物、环境声或未来持续事件。

目标策略至少包含：

| 层 | 建议控制 |
| --- | --- |
| Global | real/virtual voice 上限、硬安全阈值 |
| Bus/Category | Music、SFX、UI、Ambience、Voice 各自预算 |
| Event | max instances、cooldown、steal oldest/quietest/farthest |
| Emitter | 同类声音实例上限 |
| Request | priority、distance、critical flag、scope |

具体数值应由 FMOD Profiler 基线决定，本文不预设未经测量的 voice 数量。

### 10.2 AUD-RUN-006/010：移动 emitter 与空间请求策略

当前 3D 播放只调用一次 `set3DAttributes(position)`：

- `AudioTransform.Forward` 未使用；
- 没有 velocity；
- `EmitterId` 只进入 handle/诊断元数据；
- 没有 `AttachInstanceToGameObject` 或等价 binding；
- 没有实体销毁解绑；
- listener 只在 Session 构建时检查一次 `Camera.main`。

此外，当前请求没有“是否真的提供空间位置”的显式语义：

- `AudioPlayRequest` 始终携带一个 `AudioTransform`；
- `Create2D()` 把 transform 写成 Origin；
- `AudioService.ValidateRequest()` 不检查 3D definition 与请求类型、emitter 或 transform 的匹配；
- 因此 3D Event 可以通过 2D 请求静默播放在世界原点；
- backend 忽略 `set3DAttributes()` 返回的 FMOD RESULT；
- `AudioEventDefinition.MaxDistance` 没有被 backend 消费。

需要先明确空间 policy：3D Event 收到无空间请求时应拒绝、使用 listener-relative fallback，还是要求 composition 提供显式 transform。若距离完全由 FMOD Event authoring 权威控制，`MinDistance/MaxDistance` 应明确为 snapshot/validation evidence，而不是看似会驱动 backend 的运行配置。

这对静态 one-shot 尚可，但不适用于：

- 飞行投射物；
- 持续施法/蓄力；
- 角色光环；
- 机械、瀑布、风场；
- Scrape/Roll/持续挤压；
- Camera 切换或多 listener 场景。

目标状态：

- composition layer 维护 `emitterId -> transform/velocity` adapter；
- backend controlled handle 每 Tick 更新 3D attributes；
- listener 切换、禁用和销毁可恢复；
- noEngine 请求继续不持有 Unity `Transform`；
- emitter 更新使用预分配表，无每帧 Unity 对象查找。

验收还必须覆盖：

- 3D Event + 2D request 按约定拒绝或采用明确 fallback；
- `set3DAttributes` 失败进入结构化错误和 diagnostics；
- FMOD Event 内距离事实与 Catalog evidence 漂移时发布失败；
- 世界原点没有因缺失 transform 出现意外声音聚集。

### 10.3 AUD-RUN-007：active-handle Tick 风险

`FmodAudioBackend.Tick()` 当前在 `foreach (handles)` 期间执行 `handles[pair.Key] = active`。不同 .NET/Mono 实现对枚举期间覆盖已有值的版本变化行为可能不同；现有测试没有真实 controlled Event 进入这个分支。

处理原则：

- 先在当前 Unity/Mono 与目标 Player 上写最小 StartEvent/Tick/Stop 回归；
- 实现上避免依赖集合实现细节，可使用预分配 key buffer、可变引用表或独立 age/state 数组；
- 不能通过删除 Age 更新来绕过；Age/trace 应进入诊断；
- 修复后验证多 handle 同时停止和 backend error 路径。

### 10.4 AUD-PERF-001：持续参数 NoAlloc

当前高频风险包括：

- 每次成功 PlayOneShot、StartEvent、Stop、SetParameter、SetBusVolume 和 SetBusMuted 都会为 `PushCommand()` 拼接诊断字符串；
- GUID 播放路径 `Guid.ToByteArray()`；
- handles、bus cache 和 diagnostic queue 没有显式容量准备；
- 未来 emitter update 若直接格式化 trace 或查 Unity 对象，会扩大分配。

`recentCommands` 当前又没有进入 snapshot，所以这些成功路径分配既增加密集 one-shot 的 GC，也没有形成可消费的运行诊断。

优化要求：

- 初始化期解析 EventReference/GUID 并缓存；
- 成功路径诊断使用计数器、固定 ring buffer 或 debug-gated 格式化；
- handle/emitter/voice 容量可配置并预热；
- 持续 parameter/emitter Tick warmed 后为 0 B managed allocation；
- 失败路径允许低频分配，但必须有频率抑制，避免错误风暴。

## 11. 混音、设置与内容

### 11.1 AUD-MIX-001：Bus hierarchy

当前只有 `Master -> bus:/`，玩家只能控制一个总音量/静音。目标至少建立：

```text
Master
  Music
  SFX
    Combat
    Ability
    Physical
  UI
  Ambience
  Voice
```

具体是否全部使用 Bus 或部分使用 VCA，应由 FMOD 混音需求决定。稳定 runtime ID 和 Settings schema 必须先设计，不能把 FMOD 路径直接持久化为玩家配置。

设置迁移要求：

- 旧 Profile 只有 Master 时保持兼容；
- 新分类有明确默认值；
- 未知/退休 Bus 有迁移策略；
- Apply 顺序不产生短暂爆音；
- mute 与 volume 分开保存并保持幂等。

### 11.2 AUD-MIX-004：Bus/VCA 状态缺陷

确认问题：

1. `EnsureBusCached()` 把 `DefaultVolume/DefaultMuted` 写入缓存，但 Resolve 后没有把默认值实际应用给 FMOD；
2. VCA mute 通过 `setVolume(0)` 模拟；muted 状态再调用 `SetBusVolume()` 会直接设置非零 VCA volume，实际恢复出声，但缓存中的 `Muted` 仍为 true；
3. snapshot 返回缓存状态，不是 FMOD 实际查询状态。

当前 catalog 使用 Bus path，因此第 2 项是潜在 VCA 缺陷，不是当前 Master Bus 的必现路径。但在引入多分类 VCA 前必须修复。

验收：

- 初始化后 FMOD 实际状态等于 catalog 默认状态；
- `mute -> volume change -> unmute` 的真实输出和 snapshot 始终一致；
- Bus 与 VCA 分支使用同一组契约测试；
- Settings apply 的 volume/mute 顺序不会改变最终语义。

### 11.3 AUD-MIX-002/003：产品内容面

当前内容只能支撑 CharacterTest 的局部战斗反馈。产品化需要单独规划，而不是继续扩大 fallback 复用。

建议内容域：

| 域 | 最小覆盖 |
| --- | --- |
| Combat | swing、hit、block、projectile、ability、death、status |
| Physical | footstep、landing、impact、scrape、roll、compression |
| UI | hover、confirm、cancel、error、tab、notification |
| Music | menu、exploration、combat、result，含过渡策略 |
| Ambience | room tone、weather、biome、local emitter |
| Voice | dialogue、bark、narration，含 ducking/priority |

第一阶段先消除当前占位复用：

- LightAttack hit 与 FireballExplosion 不再共享 Demo Event；
- Landing 与 WeaponHit 不再只依赖 ObjectCollision；
- AirDisplacement 有独立真实素材；
- 每个 active semantic Cue 都有可听、采用和批准证据。

### 11.4 Physical response 参数

当前 runtime response 只正式启用 `MxPhysicalVolume` 和 `MxPhysicalPitch`，由 backend 原生 `setVolume/setPitch` 应用。LowPass、Brightness、Scrape 当前没有 automation/DSP 证据，不应描述为已经生效。

后续若启用非原生参数：

- FMOD snapshot 必须证明 Event 暴露参数；
- 还需单独证明 parameter 驱动了预期 automation/DSP，而不是只有空名字；
- Service 层校验 min/max/kind、NaN 和 Infinity；
- 缺失参数不能静默成功，除非 catalog 明确标记 Optional 并计入诊断；
- 持续参数更新必须通过 controlled handle 与 NoAlloc 路径。

## 12. Bank、平台与资源生命周期

### 12.1 AUD-RES-001：Bank/sample 所有权

当前 FMOD Settings：

- `AutomaticEventLoading=1`；
- `BankLoadType=All`；
- `AutomaticSampleLoading=0`；
- backend `PreloadBusIds` 只解析 Bus/VCA，不加载 Bank 或 sample data。

当前库较小时可以运行，但扩展后会出现：

- 首次播放加载尖峰；
- 全量 Bank 内存不可控；
- Scene/AppPersistent 资源无法分开；
- DLC/Mod/平台 Bank 没有版本与卸载 owner；
- 缺 Bank 的失败发生在首次播放而不是 warmup 阶段。

目标状态：

- 明确 Master/strings 与内容 Bank 的装载顺序；
- 按 AppPersistent、Scene、Feature/Character 建立 Bank group；
- 场景进入前 preload 必需 Bank/sample；
- 场景离开按引用计数或 owner scope unload；
- 记录 Bank size、sample memory、load time 和失败原因；
- Player build 验证 Bank manifest/hash 与目标平台一致。

### 12.2 AUD-RES-002：Validator 深度

`FmodAudioSetupValidator` 当前主要检查：

- FMOD Settings；
- runtime bank root；
- `.bank` 文件存在；
- `Master.bank` / `Master.strings.bank` 文件名；
- BankLoadType 配置。

测试使用 1-byte 文件也可以得到“无错误”，所以它只是布局 validator，不是 Bank 完整性或可播放性 validator。

应增加分层门禁：

1. **Layout**：保留现有快速检查；
2. **Artifact**：检查 LFS pointer、大小下限、Build/Streaming hash、平台目录；
3. **FMOD load**：Player/Editor 真实 load Bank，检查 FMOD result；
4. **Event resolution**：对 published Event path/GUID 创建实例；
5. **Audibility signal**：测试 Event 有可测 timeline/signal，不是 0ms 空骨架；
6. **Human audition**：内容质量、响度和空间人工批准。

### 12.3 AUD-PLAT-001：目标平台

当前 `MXFRAMEWORK_FMOD` 只配置在 Standalone。由于 UPM preview 边界把该 symbol 提升为 asmdef compile gate，其他 BuildTarget 会排除 `MxFramework.Audio.FMOD` 及其依赖闭包，而不是继续编译 backend unavailable 路径；需要 FMOD 的目标平台必须逐个 Build Profile 配置 symbol 并完成 adapter 验证。CharacterTest Player Catalog 还直接使用：

```csharp
File.Exists(Application.streamingAssetsPath/...)
File.ReadAllText(Application.streamingAssetsPath/...)
```

Android jar、WebGL URL 等平台不能把 StreamingAssets 当普通文件系统使用。

优化要求：

- 明确支持平台矩阵；
- 每个平台有对应 FMOD Bank build；
- Catalog 读取通过平台适配或 Resource 系统，而不是散落 File API；
- 异步读取不把 Unity/FMOD 类型泄漏到 noEngine Core；
- unsupported 平台在构建或启动阶段给出明确错误，不静默无声。

### 12.4 AUD-DEMO-001：可打开验证入口

`FmodAudioDemoRunner` 提供 Inspector scaffold，但当前没有发现可直接打开的场景引用。后续应提供一个专门的 Audio Validation surface，至少包含：

- listener 与可移动 3D emitter；
- 2D/3D one-shot；
- controlled loop start/stop/fade；
- parameter sliders；
- Bus/VCA volume/mute；
- pause/snapshot；
- voice flood；
- Bank missing/error injection；
- snapshot/diagnostics 面板。

该入口属于 Validation surface，继续使用 UI Toolkit 即可；它不是 player-facing UI。

## 13. Diagnostics、数据契约与测试

### 13.1 AUD-OBS-001/002：运行观测

当前 `AudioDebugSnapshot` 只有：

- initialized；
- total play/stop requests；
- controlled active handles；
- cached Bus states；
- backend recent errors。

缺少：

- 已加载 Bank 与 sample 状态；
- listener 数量与 active listener；
- one-shot/real/virtual voice；
- Event age、priority、scope、trace；
- recent commands；
- dedupe/rate-limit/voice rejection 统计；
- FMOD CPU、memory、channel；
- Bank load/unload timeline；
- Service 层 validation error。

backend 虽保存 recentCommands、AgeSeconds 和 TraceId，但 snapshot 没有暴露。Service 层提前拒绝的 InvalidEvent/Bus/Parameter 也不会进入 backend recentErrors。

目标诊断模型应区分：

```text
Requested
Resolved
Queued
Deduped
RateLimited
VoiceRejected
BackendStarted
BackendFailed
Stopped
Completed
```

诊断采集必须有固定容量和 debug/telemetry 开关，不能让每次成功播放都分配字符串。

### 13.2 AUD-DATA-001：Catalog 不可变性

`AudioDefinitionCatalog` 注释为 immutable-after-construction，但构造器直接保存调用方数组，公开属性继续返回可写数组。Event/Cue 内部的 Parameters/Labels 也需要检查复制策略。

当前可变范围还包括：

- `AudioPlayRequest.Parameters`；
- `AudioEventDefinition.Parameters` / `Labels`；
- `AudioCueDefinition.DefaultParameters` / `Labels`；
- Catalog 顶层 Buses/Events/Cues 数组。

风险：

- 外部修改公开数组后，数组内容与构造期 Dictionary 索引分裂；
- 同一 Session 内 lookup 结果不再有单一事实；
- 调试或测试代码可能无意修改 runtime truth。

目标：

- 构造时防御性复制，或只暴露 `ReadOnlySpan<T>` / `IReadOnlyList<T>`；
- 内部 Definition 的引用字段同样不可变；
- 保持启动期 LowFreqAlloc，lookup NoAlloc；
- 增加外部修改输入数组不会影响 catalog 的测试。

### 13.3 AUD-DATA-002：参数范围

`AudioParameterDefinition` 已有 Kind、Default、Min、Max，但 `AudioService.ValidateRequest()` 只检查 parameter ID 是否存在，没有检查：

- NaN / Infinity；
- min/max；
- Discrete 整数语义；
- Labeled 合法值；
- duplicate parameter ID；
- 同一参数的最后写/拒绝策略。

目标：

- 在 noEngine Service 层统一验证；
- 原生 Volume/Pitch clamp 与 catalog 范围保持一致；
- invalid request 返回结构化错误并进入统一诊断；
- hot path 不使用异常和临时集合。

### 13.4 AUD-TEST-001：现有测试边界

当前强覆盖：

- Audio Service + Null backend；
- Runtime module stage/registry/tick；
- Cue/Catalog JSON；
- Physical resolver、fallback、cooldown、NoAlloc；
- CharacterTest intent queue、事实构建与桥接；
- Authoring manifest/compiler/validator/publish；
- Workbench、Physical matrix、Generation Host。

当前弱覆盖：

- FMOD backend Play/Start/Stop/Tick/Parameter/Bus；
- listener/emitter 真实 3D；
- moving loop；
- pause、Session、scene、rollback；
- player settings reload；
- Bank load/sample warmup；
- voice budget/flood；
- Android/WebGL Catalog；
- FMOD Profiler 和可听信号。

`Tools/Harness/run_lightweight_checks.sh` 当前只做 shell syntax、forbidden paths、Unity meta、docs health、code standards 和 `git diff --check`，不会执行任何音频、Unity 或 FMOD 验证。后续需要独立音频检查入口，不能把所有重型 Player smoke 塞进 lightweight harness。

### 13.5 AUD-TOOL-001：Workbench E2E

当前 Workbench smoke 主要验证源码 token/端点存在和核心脚本流程，不是浏览器交互 E2E。后续高风险 Authoring 变更需要补：

- 启动 Workbench server；
- 加载真实 manifest/snapshot fixture；
- 搜索 Cue/Basis；
- resolve scenario；
- 修改 draft；
- expected-hash 冲突；
- preview/adoption/publish gate；
- 错误恢复和不会写坏三 manifest 的浏览器测试。

## 14. 目标运行时结构

### 14.1 Audio Intent / Command

目标不是立即扩大 `IAudioService`，而是先固定提交模型。建议 command 至少表达：

| 字段 | 用途 |
| --- | --- |
| Frame | 去重、trace 和 confirmed presentation 提交。 |
| CueId/EventId | 稳定播放身份。Gameplay 优先写 Cue。 |
| EmitterId | 实体跟随和每 emitter concurrency。 |
| Transform snapshot | 静态 one-shot 的启动位置。 |
| Scope | TransientFrame、Entity、Scene、AppPersistent。 |
| Priority | voice arbitration。 |
| Dedupe key/ordinal | 同帧同实体的稳定区分。 |
| Parameters | caller-owned 固定缓冲或内联小数组。 |
| TraceId | 低频诊断关联，不进入热路径字符串构造。 |
| Prediction policy | Immediate、ConfirmedOnly、SuppressDuringReplay。 |

不要在公共 command 中暴露：

- Unity `Transform` / `GameObject`；
- FMOD EventInstance、Bus、VCA；
- pointer 或裸内存所有权；
- Authoring manifest DTO；
- 可变 List/Dictionary。

### 14.2 Runtime 分工

| 组件 | 目标职责 |
| --- | --- |
| Game-specific collectors | 把 gameplay facts 转成稳定 audio intents。 |
| Audio command buffer | 固定容量、稳定顺序、记录 dropped。 |
| Presentation drain | rollback policy、dedupe、rate limit、priority 和 scope 决策。 |
| `AudioService` | definition/parameter/handle 契约验证与统一结果。 |
| FMOD backend | 创建实例、Bus/VCA、listener/emitter、Bank、真实 voice 状态。 |
| Audio lifecycle owner | Pause/AppFlow/Scene/Session scope。 |
| Diagnostics source | 汇总 requested 到 completed 全链状态。 |

### 14.3 Listener / Emitter Adapter

noEngine Core 继续只使用数值 transform 和 emitter ID。Unity adapter 负责：

- 注册/注销 emitter；
- 缓存 Transform/Rigidbody 访问，不每帧查找对象；
- 提供 position、forward、up、velocity；
- 处理 teleport 和 inactive；
- Camera/listener 切换；
- owner 销毁后的 handle 清理。

FMOD backend 可以使用 `AttachInstanceToGameObject` 或自有更新表，但选择必须经过当前 FMOD Unity Integration API 验证和 Player 测试。

### 14.4 Mix 与 Settings

目标 Settings 仍保存稳定 Bus ID，不保存 FMOD path。建议 profile 至少支持：

- Master；
- Music；
- SFX；
- UI；
- Ambience；
- Voice。

Combat/Ability/Physical 可以作为内部混音 Bus，而不一定全部暴露给玩家。Pause/ducking 使用命名 snapshot/state policy，不把临时 duck 值写回玩家 profile。

### 14.5 Bank Manifest

建议为 Player 增加低频 build-time Bank manifest，包含：

- platform；
- bank stable name；
- content hash；
- size；
- required master/strings；
- scope/group；
- catalog/snapshot linkage；
- optional sample preload policy。

该 manifest 是构建和加载证据，不替代 FMOD `.bank` 本身，也不进入 Gameplay authority。

## 15. 分阶段优化计划

### Phase 0：可复现基线与交付门禁

目标：先让“通过”真正代表当前 Catalog、Bank 和 Player 音频链可以执行。

行动：

1. 增加脚本化资产审计：
   - Semantic/Physical lifecycle 和 audition 统计；
   - Generated/Streaming Catalog SHA；
   - Physical Catalog 成对存在、schema、link hash；
   - FMOD Build/Streaming Bank SHA；
   - LFS pointer 与最小 artifact 完整性；
   - snapshot `cacheValid/cacheStale/diagnostics`；
   - Active/Ready Event 的 0ms、异常长 timeline 和 parameter evidence。
2. 创建可打开的 Audio Validation scene/surface。
3. 增加最小真实 FMOD smoke：2D、静态 3D、Start/Stop、Bus volume/mute。
4. 增加 Standalone Player smoke，不只在 Editor TestRunner 验证。
5. 记录 FMOD Profiler 初始 baseline：first-play latency、voice、memory、CPU。

依赖：

- 现有 Desktop Bank；
- 当前 Unity/FMOD Integration；
- Validation surface 资产应通过 Unity Editor/MCP 生成，不手写 YAML。

退出条件：

- PR 可以发现 Catalog/Bank/Snapshot 漂移；
- Player smoke 能创建真实 Event 并控制 Bus；
- 0ms Active published Event 会失败；
- 测试明确区分结构验证、信号验证和人工审听。

### Phase 1：当前 one-shot 交付闭环

目标：让当前 CharacterTest one-shot 功能真正使用新 Physical Catalog，并修复玩家可见缺陷。

行动：

1. 修复 Settings.Audio Attach 后自动应用当前 profile；
2. 完成 AirDisplacement 候选、adoption、FMOD materialization 和人工审听；
3. 完成当前 Active Basis 的 adoption/approval，或把未完成项明确降回 Draft；
4. 发布 Global + Physical Catalog 成对输出；
5. 启动日志与 diagnostics 区分 generated path 和 legacy fallback；
6. LightAttack、Fireball、Landing、WeaponHit 消除最明显的 Demo/Collision 占位复用；
7. 把 fallback 设为开发降级，而不是 release 的无声成功。

退出条件：

- Editor/Player 都不再因缺文件进入 legacy fallback；
- AirDisplacement 空挥真实可听，且与命中声分离；
- 保存的音量/静音跨重启、Session rebuild 和网络模式切换保持；
- 当前声明 Active 的 Cue/Basis 全部有有效 adoption、Bank、snapshot 和人工审听证据；
- CharacterTest focused EditMode + PlayMode + Standalone smoke 通过。

### Phase 2：Pause、Session、Scene 与 Rollback

目标：固定音频表现生命周期，不让 one-shot/loop 跨边界失控或在 rollback 中重复。

行动：

1. 定义 Audio Scope 和 owner；
2. 建立统一 presentation command drain；
3. 实现 rollback replay suppression/dedupe；
4. 为 Pause/Menu/Scene 状态增加 mix/snapshot policy；
5. 增加 StopAllByScope 或等价内部能力；
6. 固定 Stop stale-handle 与 Dispose 契约；
7. 声明并测试 main-thread boundary。

退出条件：

- rollback 不双播；
- Pause 行为按类别一致；
- Session/Scene 切换不残留旧 gameplay 声；
- persistent 音乐不会被 gameplay slice 错误停止；
- Null/FMOD backend handle 测试语义一致。

### Phase 3：Controlled Event、Emitter、Voice 与性能

目标：安全支持移动/持续声音，并让密集战斗受预算管理。

行动：

1. 修正/替换 active-handle Tick 的枚举期写回；
2. 实现 emitter registry 与每 Tick 3D attributes 更新；
3. 实现 listener lifecycle；
4. 实现 global/category/event/emitter voice budget；
5. 消费 request priority，定义 voice steal；
6. controlled parameter 和 emitter update warmed 后 0 B；
7. recent command/error 使用固定 ring buffer；
8. 增加 loop、moving emitter、entity destroy、voice flood 测试。

退出条件：

- 移动 loop 跟随位置、朝向和速度；
- owner 销毁后无 handle 泄漏；
- voice flood 不超过预算，裁剪原因可诊断；
- controlled Event 多实例 Tick/Stop 在 Editor 和 Standalone 无异常；
- 目标场景 steady-state managed allocation 为 0 B，或剩余分配有外部归因证据。

### Phase 4：产品混音与内容面

目标：从“只有战斗 one-shot”扩展为完整游戏音频产品面。

行动：

1. 建立 Master/Music/SFX/UI/Ambience/Voice hierarchy；
2. 迁移 Settings schema；
3. 建立 Pause/Menu/Combat/Dialogue snapshot 与 ducking；
4. 增加 Music state/transition adapter；
5. 增加 UI、Ambience、Voice 的语义 Cue 和内容生产流程；
6. 在 FMOD 中完成响度、距离、priority 和 virtualization 混音；
7. 更新 player-facing 设置 UI。

退出条件：

- 玩家能独立调整主要分类；
- Pause、对白和战斗转换没有突变；
- UI、环境、音乐和语音有最小垂直切片；
- 所有新 Cue 经过现有 adoption/审听门禁；
- 混音在目标扬声器/耳机配置完成人工验收。

### Phase 5：Bank 生命周期、平台与观测

目标：让音频可在正式构建、更多平台和长期运营中管理。

行动：

1. 拆分 Bank group 与 scope；
2. 增加 Bank manifest、warmup、sample preload、引用和 unload；
3. 适配非普通文件系统 StreamingAssets；
4. 增加目标平台 Bank build/校验；
5. 补 Browser E2E 和 managed publish failure recovery；
6. 实现 Audio diagnostics source、Debug UI 和 telemetry；
7. 发布流程增加 nightly/player/profiler 门禁。

退出条件：

- 每个支持平台都能加载正确 Bank/Catalog；
- Scene/App scope Bank 内存可测、可释放；
- 首次关键声音延迟在已定义预算内；
- missing/stale Bank 在启动或 warmup 阶段可诊断；
- Debug UI 能看到 Bank、listener、voice、scope、errors 和 recent decisions。

### Phase 6：Continuous Physical Audio

目标：在 Phase 0–5 的生命周期和 voice 基础上实施设计中的 Phase 18。

范围：

- Scrape；
- Roll；
- 持续挤压；
- Started/Stayed/Ended；
- 持续参数更新；
- moving emitter；
- voice arbitration。

进入条件：

- controlled Event、emitter、scope、voice budget 和 NoAlloc 已验收；
- 不能在当前 one-shot backend 上直接堆持续事件；
- Physical automation/DSP evidence 已能证明参数真实影响声音。

## 16. 建议实现切片

下表只描述技术拆分、依赖和验证范围。

| 实现切片 | 技术影响 | 覆盖 Finding | 依赖 | 最小验证 |
| --- | --- | --- | --- | --- |
| Audio 资产/Bank/Catalog 审计脚本 | Focused | REL-001/002/004、RES-002 | 无 | console tests + changed fixture |
| Audio Validation Scene + Player smoke | Unity/Player integration | REL-004、DEMO-001、TEST-001 | 审计脚本 | Unity compile、PlayMode、Standalone |
| Settings.Audio session restore | Cross-module | REL-003、MIX-004 | 无 | focused EditMode + session rebuild PlayMode |
| Physical Catalog 发布闭环 | Content integration | REL-001/002、MIX-003 | 内容采用/审听 | Authoring + Workbench + Unity + Player |
| Audio command drain + rollback policy | Runtime authority | RUN-002/004/009 | 现有 intent queue | EditMode、rollback PlayMode、NoAlloc |
| Pause/Scene/Session scope | Runtime lifecycle | RUN-001/003/008 | command drain | PlayMode + Player transition smoke |
| Controlled handle + emitter lifecycle | Runtime integration | RUN-006/007/008 | scope | moving loop、destroy、camera switch |
| Voice budget + priority | Runtime performance | RUN-005、PERF-001 | controlled handle | flood tests + FMOD Profiler |
| Bus/VCA + Settings schema | Schema and audible behavior | MIX-001/004 | settings restore | migration tests + audible mix smoke |
| Music/UI/Ambience/Voice 垂直切片 | Product integration | MIX-002/003 | Bus hierarchy | playable + human audition |
| Bank scope/platform loader | Platform integration | RES-001、PLAT-001 | Bank manifest | supported platform Player builds |
| Audio diagnostics/Debug UI | Tooling integration | OBS-001/002 | command/voice lifecycle | snapshot tests + UI Toolkit smoke |
| Catalog immutability/parameter contract | Contract | DATA-001/002 | 无 | unit/EditMode + allocation check |
| Workbench browser E2E | Tooling | TOOL-001 | stable server fixture | browser E2E + rollback/no-write proof |

建议依赖顺序：

```text
asset audit -> Player smoke
settings restore -> Bus/Settings schema
Physical publish -> current one-shot release closure
command drain -> rollback + scope -> controlled emitter -> voice budget
Bus hierarchy -> Music/UI/Ambience/Voice
Bank manifest -> platform loader
all runtime work -> diagnostics + release gates
```

## 17. 验收矩阵

### 17.1 PR 快速门禁

| 检查 | 触发条件 | 通过标准 |
| --- | --- | --- |
| `git diff --check` | 所有改动 | 无 whitespace error。 |
| Docs health | 文档改动 | 状态、链接和索引健康。 |
| Catalog parse/schema | manifest/catalog 改动 | 全部可解析，format/schema 正确。 |
| Generated/Streaming SHA | runtime catalog 改动 | 两份镜像一致。 |
| Physical pair link | Physical publish | Global hash 匹配，无缺 Cue/Event/Basis。 |
| Bank Build/Streaming SHA | FMOD/Bank 改动 | 目标平台镜像一致，不是 LFS pointer。 |
| Snapshot gate | Event/Bank 改动 | cache valid、非 stale、无 diagnostics。 |
| Active content gate | Active Cue/Basis 改动 | 无 0ms、无 Needed、adoption/approval 当前。 |
| Focused EditMode | C#/catalog 改动 | 相关 Audio/CharacterTest tests 全通过。 |
| Workbench/Generation | Authoring 改动 | 对应 console/smoke/matrix/host tests 通过。 |

### 17.2 Unity/FMOD focused 验收

| 场景 | 自动断言 | 人工检查 |
| --- | --- | --- |
| 2D one-shot | 创建、start、complete、无 error | 响度和语义 |
| 静态 3D one-shot | 位置、距离、listener 变化 | 衰减和方向感 |
| 3D Event + 2D request | 按 policy 拒绝或显式 listener fallback，不静默落原点 | 不出现原点异常聚集 |
| 移动 3D loop | emitter 跟随、stop、destroy cleanup | 运动连续性/Doppler 策略 |
| Parameter | 范围、更新、automation evidence | 音色/强度变化正确 |
| Bus/VCA | volume/mute/snapshot 状态一致 | 无爆音、无错误恢复出声 |
| Pause | 类别策略、恢复幂等 | 音乐/环境/UI 体验 |
| Settings reload | 重启/Session rebuild 后值一致 | UI 与听感一致 |
| Scene transition | scope stop/fade、Bank unload | 无残留和硬切 |
| Rollback | 同一事实不双播 | 延迟可接受 |
| Voice flood | 不超预算、steal 可诊断 | 无明显爆音/抽吸 |
| Missing Bank/Event | 结构化失败、fallback policy | 不出现无解释静音 |

### 17.3 Nightly / 发布门禁

Nightly：

- Standalone development Player；
- 真实 Bank load；
- 2D/3D/loop/listener movement；
- pause/settings/session/scene；
- voice flood；
- catalog/bank/snapshot hash；
- diagnostics artifact。

Release candidate：

- 全部支持平台 Player；
- FMOD Profiler capture；
- 音频内存、voice、CPU、first-play latency；
- 人工审听清单；
- 音量/响度与可访问性；
- 无 legacy fallback；
- 无 Draft/Needed Active publish；
- Bank/Catalog build provenance 可追溯。

## 18. 指标与预算

### 18.1 正确性指标

| 指标 | 目标 |
| --- | --- |
| Release 中 legacy fallback | 0 |
| Active published 0ms Event | 0 |
| Active Cue/Basis audition Needed | 0 |
| Global/Physical link mismatch | 0 |
| Build/Streaming Bank hash mismatch | 0 |
| Settings reload mismatch | 0 |
| Rollback duplicate audio | 0 |
| owner-destroy 后 orphan controlled handles | 0 |

### 18.2 性能指标

首次基线采集前不填写拍脑袋的毫秒/voice 目标。必须测量：

- Bank load time；
- sample preload time；
- first-play latency；
- real/virtual voices；
- FMOD DSP/Studio CPU；
- Bank/sample memory；
- audio command drain cost；
- parameter/emitter Tick managed allocation；
- voice rejection/rate limit 数；
- Session/scene unload 后内存回落。

固定原则：

- 初始化后 command drain、parameter update、emitter update 目标为 0 B managed allocation；
- snapshot/debug capture 是 `AllocByDesign` 低频路径；
- 错误路径允许 LowFreqAlloc，但要防止重复错误风暴；
- 不用关闭系统、减少真实声音内容或绕过规则制造性能收益。

### 18.3 内容指标

- 每个 Active Cue 都能追溯到 Event、Bank、adoption 和 audition；
- 每个产品内容域有最小可玩覆盖；
- semantic Cue 不因临时方便长期复用不相干 Event；
- Physical Basis coverage 与 runtime-use scenario 对齐；
- 响度、距离和优先级由 mix review 验收，不只看文件存在。

## 19. 风险登记

| 风险 | 影响 | 缓解 |
| --- | --- | --- |
| 为赶发布放宽 adoption/audition 门禁 | 空声音进入 Player | 保持门禁，完成内容或降回 Draft。 |
| 先做 Phase 18 持续音频 | 放大 handle/emitter/voice 缺陷 | 先完成 Phase 2–3。 |
| 在 Gameplay 中直接调用 FMOD | 破坏 noEngine、回放和测试 | 所有请求继续经过 Cue/Service。 |
| 把 FMOD instance 放入 rollback/save | 外部表现状态污染 authority | 只保存稳定 gameplay facts。 |
| 只依赖 Editor 测试 | Player/Bank/平台问题漏检 | 增加 Standalone 和目标平台 smoke。 |
| 全量 Bank 自动加载继续扩张 | 内存和首播延迟不可控 | Bank group、warmup、unload、Profiler。 |
| 调试字符串进入成功热路径 | 持续 GC | 固定计数器/ring buffer、按需格式化。 |
| 多 Bus/VCA 扩展前不修状态语义 | 设置与真实混音分裂 | 先修默认 apply 和 VCA mute/volume。 |
| 自动测试替代人工听感 | 技术通过但内容失败 | 保留人工 audition 和 mix review。 |

## 20. 非目标

本文不要求：

- 把 FMOD 替换为 Unity AudioSource；
- 让音频进入 deterministic simulation hash；
- 在没有证据时采用 `unsafe` 优化；
- 让 Workbench 直接修改未知外部 FMOD Event；
- 把 player-facing 音频设置迁回 UI Toolkit；
- 自动算法替代人工审听；
- 在 Phase 2–3 之前实现完整 Scrape/Roll/持续挤压；
- 一次性制作全部游戏音乐、环境和对白内容。

## 21. 完成定义

### 21.1 当前 one-shot 垂直切片完成

- Physical Catalog 成对发布并在 Player 加载；
- AirDisplacement、Footstep、Landing、ObjectImpact、WeaponHit/Block 有真实可听素材；
- Active Cue/Basis 证据完整；
- Settings 跨重启和 Session rebuild 恢复；
- Standalone smoke 与人工审听通过；
- Release 不进入 legacy fallback。

### 21.2 Runtime 音频管理完成

- 统一 command drain、rollback policy 和 scope；
- pause/session/scene 生命周期明确；
- controlled handle、moving emitter 和 listener lifecycle 稳定；
- voice budget/priority/rate limit 可配置、可诊断；
- 高路径 NoAlloc；
- Null/FMOD 契约一致。

### 21.3 产品级音频系统完成

- Music/SFX/UI/Ambience/Voice mix hierarchy；
- 玩家分类设置与迁移；
- 音乐、UI、环境、语音最小垂直切片；
- Bank scope/warmup/unload 和平台 loader；
- Player、Profiler、Diagnostics、人工审听进入发布门禁；
- 内容、运行时和构建 provenance 可追溯。

## 22. 2026-07-16 验证记录

本轮收口执行：

| 验证 | 结果 |
| --- | --- |
| Audio Core / Physical / FMOD / CharacterTest / Tests 工程 | 0 error |
| Unity refresh/compile + Console | ready，0 error |
| focused EditMode | 9/9 passed |
| 真实 Bank focused PlayMode | 2/2 passed |
| FMOD snapshot | 123 Events / 11 loops / 3 parameterized / 2 Banks / 0 diagnostics |
| FMOD Build/Streaming Master banks | SHA-256 一致 |
| Bank manifest deterministic check | passed |
| Lightweight harness | passed，code standards 0 finding |
| Audio asset release audit | 56 blockers / 5 warnings；阻断为人工试听、Physical adoption/evidence、Catalog 未发布 |
| Git worktree | 保留用户既有大量未提交改动；本轮未提交、未推送 |

### 22.1 FMOD Studio 2.03.13-2.03.14 Bank 关系崩溃处置

2026-07-16 在 macOS 26.5.1 ARM64 上确认 FMOD Studio 2.03.13 与
2.03.14 存在原生崩溃：为新 Event 添加 Bank relationship 时，
`ManagedRelationshipScriptPrototype::func_AddDestination` 会进入
Bank browser/tree model 更新并触发 `SIGSEGV`。该问题可由脚本 API、
原生 Duplicate、最小命令行平台和隔离工程复现；删除 `.user` workspace
状态不能消除，因此不按项目元数据损坏处理。

当前仓库采取以下受控规避：

- FMOD 脚本不得在 2.03.13-2.03.14 调用
  `event.relationships.banks.add(bank)`；所有现存入口都有默认关闭的硬保护。
- Event 的创建、轨道、音源、参数、自动化和空间化仍由 FMOD Studio
  scripting 完成。
- FMOD 完全退出后，使用
  `Tools/FMOD/AssignEventBanksOffline.mjs` 只写 Event XML 的
  `<relationship name="banks">` destination。
- 离线写入后必须依次执行 FMOD `-diagnostic`、Desktop Master Bank
  build、GUID export，并确认 Build/Desktop 与 StreamingAssets mirror
  的 SHA-256 一致。
- 在 Firelight 发布明确修复且完成同等回归验证前，不重新开启脚本 Bank
  relationship 写入；FMOD GUI 中也不要手工把新 Event 拖入 Bank。

本次规避验证：

- `Scrape`、`Roll`、`Compression` 均为唯一 Event，分别包含 2 个
  GroupTrack、2 个循环 SingleSound、1 个 Spatialiser、1 个 Lowpass 和
  3 条 automation。
- FMOD 项目诊断输出 `Project validation completed successfully`。
- Master Bank build 与 GUID export 成功，三个
  `event:/MxFramework/Product/PhysicalContinuous/*` 路径均进入
  `Build/GUIDs.txt`。
- `Build/Desktop/Master*.bank` 与
  `Assets/StreamingAssets/Master*.bank` 哈希一致。
- 普通 FMOD GUI 重新打开主工程后进程保持存活，未产生新的 Sentry crash
  report。

本次未执行：

- 人工听感评审；
- Standalone Player 真实出声 smoke；
- controlled loop/moving emitter；
- pause/session/rollback 音频验证；
- FMOD Profiler capture；
- Android/WebGL 或其他非 Standalone 构建。

因此上述通过结果只证明契约、解析、resolver、Authoring 和当前 CharacterTest 路由稳定，不证明生产可听质量或完整 FMOD 生命周期。

## 23. 落地记录格式

后续每个完成阶段按以下格式追加，不在本文中粘贴大段测试日志：

```text
Date:
Finding IDs:
Issue / PR / Commit:
Changed boundary:
Before evidence:
After evidence:
Validation:
Remaining risk:
Status: Verified | Partial | Reopened
```

只有同时提供对应 Finding ID、当前代码/资产和验证证据，才能把表中状态改为 Verified。

## 24. 证据索引

设计与契约：

- `Docs/Interfaces/Audio.md`
- `Docs/Interfaces/Settings.md`
- `Docs/AUDIO_SYSTEM_FMOD.md`
- `Docs/AUDIO_PHYSICAL_EXPRESSION_DESIGN.md`
- `Docs/Decisions/ADR-007-audio-physical-expression-pipeline.md`
- `Docs/ROADMAP.md` Phase 17/18

核心运行时：

- `Packages/com.vincent.wgameframework/Audio/IAudioService.cs`
- `Packages/com.vincent.wgameframework/Audio/AudioService.cs`
- `Packages/com.vincent.wgameframework/Audio/AudioPlayRequest.cs`
- `Packages/com.vincent.wgameframework/Audio/AudioTransform.cs`
- `Packages/com.vincent.wgameframework/Audio/AudioEventDefinition.cs`
- `Packages/com.vincent.wgameframework/Audio/AudioDefinitionCatalog.cs`
- `Packages/com.vincent.wgameframework/Audio/NullAudioBackend.cs`
- `Packages/com.vincent.wgameframework/Audio.FMOD/FmodAudioBackend.cs`
- `Packages/com.vincent.wgameframework/Audio.FMOD/AudioRuntimeModule.cs`
- `Packages/com.vincent.wgameframework/Audio.FMOD/FmodAudioBackendOptions.cs`

CharacterTest：

- `Assets/Scripts/WGameFrameworkProject/CharacterTest/Composition/CharacterTestRuntimeSessionBuilder.cs`
- `Assets/Scripts/WGameFrameworkProject/CharacterTest/Presentation/Audio/CharacterTestAudioCueCatalog.cs`
- `Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems/Character/CharacterTestCharacterRuntimeModule.cs`
- `Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems/Character/CharacterTestPhysicalAudioSystem.cs`
- `Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems/Character/CharacterTestWeaponHitAudioSystem.cs`
- `Assets/Scripts/WGameFrameworkProject/CharacterTest/UI/Runtime/CharacterTestSettingsRuntime.cs`
- `Assets/Scripts/WGameFrameworkProject/CharacterTest/Network/Transport/CharacterTestUnityTransportEndpointRunners.cs`

Authoring 与资产：

- `Assets/Config/MxFramework/Audio/audio_semantic_manifest.json`
- `Assets/Config/MxFramework/Audio/audio_generation_manifest.json`
- `Assets/Config/MxFramework/Audio/audio_physical_manifest.json`
- `Assets/MxFrameworkGenerated/Audio/audio_runtime_catalog.json`
- `Assets/MxFrameworkGenerated/Audio/fmod_audio_library.json`
- `Assets/StreamingAssets/MxFramework/Audio/audio_runtime_catalog.json`
- `Assets/StreamingAssets/Master.bank`
- `Assets/StreamingAssets/Master.strings.bank`
- `FMOD/MxFrameworkAudioDemo/MxFrameworkAudioDemo/Build/Desktop/`

测试与工具：

- `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Audio/`
- `Packages/com.vincent.wgameframework/Tests/PlayMode/Audio/AudioRuntimeCatalogPlayModeTests.cs`
- `Packages/com.vincent.wgameframework/Audio.FMOD.Editor/FmodAudioSetupValidator.cs`
- `Tools/MxFramework.AudioWorkbench/`
- `Tools/MxFramework.AudioGenerationHost/`
- `Tools/Harness/run_lightweight_checks.sh`
