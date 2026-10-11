# MxFramework 接口索引

> Status: Current
>
> 版本 0.3.17 | 2026-07-20
>
> 本文件只做接口导航、跨模块规则和依赖矩阵。具体模块接口不要继续堆在这里，必须拆到 `Docs/Interfaces/`。

## 阅读顺序

1. `Docs/USAGE.md`：先看怎么接入。
2. 本文件：确认模块边界和依赖方向。
3. `Docs/Interfaces/<Module>.md`：查看具体模块接口。
4. `Packages/com.vincent.wgameframework/Tests/<Module>/`：查看可运行样例。

## 模块接口文档

本表定位当前工程接口、代码和测试入口；“测试入口”是定位信息，不表示本轮已运行或全部验收通过。按模块页标明的实现子集、禁止项和未交付边界读取，计划中的 API 不能提前提升为当前合同。

操作步骤先读 [USAGE](USAGE.md) 或 [README 分类入口](README.md#分类入口)，专项开发走 [PROJECT_INDEX](PROJECT_INDEX.md)；[设计与决策](README.md#指南和设计)、[未完成工作](README.md#未完成与活跃工作)和[历史证据](README.md#证据与历史)分别读取，不复制进模块接口表。

| 模块 | 文档 | 主要代码 | 测试入口 |
|------|------|----------|----------|
| Combat Design Authoring（外部只读投影） | `Docs/Interfaces/CombatDesign.Authoring.md` | `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/CombatDesign/`, `Tools/MxFramework.EditorHub/web/` | 独立 CLI fixture 与 Playwright 只读页面验收 |
| CharacterTest TacticalCard（项目层） | `Docs/Interfaces/CharacterTest.TacticalCard.md` | `Assets/Scripts/WGameFrameworkProject/CharacterTest/TacticalCard/` | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/TacticalCard/`（`TacticalCardEngineTests`、`TacticalTimeCoordinatorTests`、`TacticalExecutionAdapterTests`、`TacticalCardDemoAuthoringTests`） |
| Core | `Docs/Interfaces/Core.md` | `Packages/com.vincent.wgameframework/Core/` | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Core/` |
| Events | `Docs/Interfaces/Events.md` | `Packages/com.vincent.wgameframework/Events/` | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Events/` |
| Attributes | `Docs/Interfaces/Attributes.md` | `Packages/com.vincent.wgameframework/Attributes/` | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Attributes/` |
| Buffs | `Docs/Interfaces/Buffs.md` | `Packages/com.vincent.wgameframework/Buffs/` | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Buffs/` |
| Modifiers | `Docs/Interfaces/Modifiers.md` | `Packages/com.vincent.wgameframework/Modifiers/` | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Modifiers/` |
| Config | `Docs/Interfaces/Config.md` | `Packages/com.vincent.wgameframework/Config*/` | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Config/` |
| Resources | `Docs/Interfaces/Resources.md` | `Packages/com.vincent.wgameframework/Resources/` | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Resources/` |
| Items | `Docs/Interfaces/Items.md` | `Packages/com.vincent.wgameframework/Items*/` | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Items/` |
| Animation | `Docs/Interfaces/Animation.md` | `Packages/com.vincent.wgameframework/Animation*/` | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Animation/` |
| Audio | `Docs/Interfaces/Audio.md` | `Packages/com.vincent.wgameframework/Audio*/` | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Audio/` |
| Camera | `Docs/Interfaces/Camera.md` | `Packages/com.vincent.wgameframework/Camera*/` | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Camera/` |
| Rendering | `Docs/Interfaces/Rendering.md` | `Packages/com.vincent.wgameframework/Rendering*/` | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Rendering/` |
| VFX | `Docs/Interfaces/Vfx.md` | `Packages/com.vincent.wgameframework/Vfx*/` | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Vfx/` |
| AI | `Docs/Interfaces/AI.md` | `Packages/com.vincent.wgameframework/AI/` | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/AI/` |
| Diagnostics | `Docs/Interfaces/Diagnostics.md` | `Packages/com.vincent.wgameframework/Diagnostics/` | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Diagnostics/` |
| Debug UI | `Docs/Interfaces/DebugUI.md` | `Packages/com.vincent.wgameframework/DebugUI*/` | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/DebugUI/` |
| Logging | `Docs/Interfaces/Logging.md` | `Packages/com.vincent.wgameframework/Runtime/RuntimeLogger.cs`, `Packages/com.vincent.wgameframework/Runtime.Unity/UnityRuntimeLogger.cs` | Compile `MxFramework.Runtime`, `MxFramework.Runtime.Unity`, `MxFramework.Demo` |
| Runtime | `Docs/Interfaces/Runtime.md` | `Packages/com.vincent.wgameframework/Runtime/` | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Runtime/` |
| Runtime fixed layout hash | `RuntimeFixedLayoutHash64.Hash16(long, long, ulong)`；固定 LE64 × 2 / XXH64，见 `Docs/Interfaces/Runtime.md` | `Packages/com.vincent.wgameframework/Runtime/RuntimeFixedLayoutHash64.cs` | `RuntimeFixedLayoutHashTests` |
| Platform | `Docs/Interfaces/Platform.md` | `Packages/com.vincent.wgameframework/Platform/`, `Packages/com.vincent.wgameframework/Platform.Unity/` | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Platform/`, `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Platform.Unity/` |
| Application Startup Core | `Docs/Interfaces/Application.md` | `Packages/com.vincent.wgameframework/Application/` | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Application/` |
| Application Candidate SessionBridge | `Docs/Interfaces/ApplicationSessionBridge.md` | `Packages/com.vincent.wgameframework/Application.SessionBridge/` | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Application/ApplicationCandidateSessionBridgeTests.cs` |
| Application Unity Boot | `Docs/Interfaces/ApplicationUnity.md` | `Packages/com.vincent.wgameframework/Application.Unity/`, `Packages/com.vincent.wgameframework/Application.Unity.Validation/` | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Application.Unity/`, `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Application.Unity.Validation/`, `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Application.Unity.Editor/`, `Packages/com.vincent.wgameframework/Tests/PlayMode/Application/` |
| Network | `Docs/Interfaces/Network.md` | `Packages/com.vincent.wgameframework/Network/` | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Network/` |
| Story | `Docs/Interfaces/Story.md` | `Packages/com.vincent.wgameframework/Story/` | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Story/` |
| Story Runtime | `Docs/Interfaces/Story.Runtime.md` | `Packages/com.vincent.wgameframework/Story.Runtime/` | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Story.Runtime/` |
| Story Config | `Docs/Interfaces/Story.Config.md` | `Packages/com.vincent.wgameframework/Story.Config/` | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Story.Config/` |
| Story Gameplay Bridge | `Docs/Interfaces/Story.GameplayBridge.md` | `Packages/com.vincent.wgameframework/Story.GameplayBridge/` | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Story.GameplayBridge/` |
| Story Resources Bridge | `Docs/Interfaces/Story.ResourcesBridge.md` | `Packages/com.vincent.wgameframework/Story.ResourcesBridge/` | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Story.ResourcesBridge/` |
| Story Runtime AI Planner Bridge | `Docs/Interfaces/Story.RuntimeAiPlannerBridge.md` | `Packages/com.vincent.wgameframework/Story.RuntimeAiPlannerBridge/` | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Story.RuntimeAiPlannerBridge/` |
| Story Unity | `Docs/Interfaces/Story.Unity.md` | `Packages/com.vincent.wgameframework/Story.Unity/` | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Story.Unity/` |
| Story Editor | `Docs/Interfaces/Story.Editor.md` | `Packages/com.vincent.wgameframework/Story.Editor/` | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Story.Editor/` |
| App / Scene Flow | `Docs/Interfaces/AppFlow.md` | `Packages/com.vincent.wgameframework/Runtime*/` | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Runtime/` |
| Persistence | `Docs/Interfaces/Persistence.md` | `Packages/com.vincent.wgameframework/Persistence*/` | `Packages/com.vincent.wgameframework/Tests/Persistence*/` |
| Input | `Docs/Interfaces/Input.md` | `Packages/com.vincent.wgameframework/Input/` | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Input/` |
| Voice Input | `Docs/Interfaces/VoiceInput.md` | `Packages/com.vincent.wgameframework/VoiceInput*/` | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/VoiceInput/` |
| Settings | `Docs/Interfaces/Settings.md` | `Packages/com.vincent.wgameframework/Settings*/` | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Settings/` |
| UI / FairyGUI | `Docs/Interfaces/UI.md`, `Docs/Interfaces/UI.FairyGUI.md`, `Docs/PLAYER_GUIDANCE_PROMPT_UI_DESIGN.md`, `Docs/FAIRYGUI_UI_COMPOSER.md`, `Docs/Guides/FAIRYGUI_BINDER_GENERATION_GUIDE.md` | `Packages/com.vincent.wgameframework/UI/`, `Packages/com.vincent.wgameframework/UI.FairyGUI*/`, `Tools/MxFramework.FairyGuiUiComposer/` | `Packages/com.vincent.wgameframework/Tests/UI/`, `Tools/MxFramework.NoEngineTests/UI.Feedback.Tests/`, `Tools/MxFramework.NoEngineTests/UI.Guidance.Tests/`, `Tools/MxFramework.Authoring/tests/` |
| UI Toolkit | `Docs/Interfaces/UI.Toolkit.md` | `Packages/com.vincent.wgameframework/UI.Toolkit/` | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/UI.Toolkit/` |
| Gameplay | `Docs/Interfaces/Gameplay.md` | `Packages/com.vincent.wgameframework/Gameplay/` | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Ability/`, `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Gameplay/` |
| Gameplay Triggers | `Docs/Interfaces/Gameplay.Triggers.md` | `Packages/com.vincent.wgameframework/Gameplay.Triggers/`、`Packages/com.vincent.wgameframework/Gameplay.Triggers.CombatBridge/`、`Packages/com.vincent.wgameframework/Gameplay.Triggers.GameplayBridge/` | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Gameplay/Triggers/` |
| Combat | `Docs/Interfaces/Combat.md` | `Packages/com.vincent.wgameframework/Combat/` | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Combat/` |
| Character Application | `Docs/Interfaces/CharacterApplication.md` | `Packages/com.vincent.wgameframework/Character.Application/` | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/CharacterApplication/` |
| Character Control | `Docs/Interfaces/CharacterControl.md` | `Packages/com.vincent.wgameframework/CharacterControl*/` | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/CharacterControl/` |
| Character Action | `Docs/Interfaces/CharacterAction.md` | `Packages/com.vincent.wgameframework/CharacterAction/` | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/CharacterAction/` |
| Character Runtime Spawn | `Docs/Interfaces/Gameplay.md` 末节 + `Docs/Interfaces/CharacterApplication.md` | `Packages/com.vincent.wgameframework/Character.RuntimeSpawn*/` | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/CharacterRuntimeSpawn/` |
| Character Cloth | `Docs/Interfaces/CharacterCloth.md` | `Packages/com.vincent.wgameframework/Character.Cloth/`, `Packages/com.vincent.wgameframework/Character.Cloth.Magica.Unity/` | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/CharacterCloth/` |
| Editor | `Docs/Interfaces/Editor.md` | `Packages/com.vincent.wgameframework/Editor/` | Unity Editor / MCP |

Character Cloth 的角色组会话生命周期、CharacterTest 暂停接入、显式传送 Reset，以及 Magica 全局预算/单角色碰撞质量的作用域见 [CharacterCloth.md](Interfaces/CharacterCloth.md) 第 8 节。

`CharacterClothPresentationService.SetLodPaused(ICharacterClothGroup, bool)` 的注册约束、会话/LOD 请求合成与保守阴影边界见同页第 9 节。CharacterTest 会话内观察缓存与 HeadPad 接入见 [Rendering.md](Interfaces/Rendering.md) 的“CharacterTest 表现 LOD 首阶段”；协调器为项目内部实现，不是新的 package 公共调度契约。

## 文档规则

- 每个模块页只写本模块公开接口、默认实现、使用约定和禁止事项。
- 模块页应给出测试入口，不复制整份源码。
- 公共 API 改动时，必须同步对应模块页和 `Docs/USAGE.md` 中的示例。
- 旧接口、计划中接口、未实现接口不得混进当前契约；计划内容放 `ROADMAP.md`。
- 小型模块 EditMode 测试默认编译进 `MxFramework.Tests`；只有测试模式、平台、第三方依赖或依赖隔离确实不同，才新增独立测试 asmdef。

## 跨模块依赖矩阵

`MxFramework.Input` 是 Unity 适配模块，依赖 Unity Input System；它不进入 noEngine 依赖矩阵，也不应被 Core / Runtime / Gameplay 等内层模块反向引用。游戏层、Demo 或本地多人组合根按需引用 `IInputProvider`。

`MxFramework.Gameplay.Triggers.CombatBridge` 是 noEngine sibling bridge，依赖 Trigger/Config、Combat/Combat.GameplayBridge、Gameplay、Runtime 和 Core。它在 Gameplay `Simulation/950` 用固定世界空间 Combat query 生成 complete occupancy，并严格早于 Trigger Evaluate `Simulation/1000`；Trigger core 不反向引用 Combat，Combat core 与既有 Combat Gameplay bridge 也不引用 Trigger。

`MxFramework.Gameplay.Triggers.GameplayBridge` 是 noEngine sibling bridge，依赖 Trigger、Gameplay、Runtime 和 Core。19.3C1 把 action kind `1001010/1001011` 以 `SameFrameReserved` 投递到 Gameplay-owned `GameplayBuffRequestBuffer` 的固定 reservation lane；19.3C2 把 action kind `1001004/1001006/1001007/1001008` 以 `ReliableDeferred` 经 hard-bounded trusted-derived Runtime command gate 投递 ComponentEntity Destroy、Attribute Set/Add 与 direct Ability Cast。两条路径都把目标域 terminal result relay 为 Trigger `ActionResult` presentation event；Trigger core 不引用 bridge，Gameplay core 也不引用 Trigger。request/result/correlation 是派生瞬态，不进入 Trigger 或 Gameplay authority hash、SaveState 和 replay input。Unity/network adapter 与 Playable 仍未交付。

`MxFramework.Gameplay.Triggers.StoryBridge` 是 noEngine sibling bridge，依赖 Trigger、Runtime、Story 与 Story.Runtime。19.3C3 把唯一 action kind `Story.RaiseTrigger=1003001` 以 `ReliableDeferred` 投递到 exact bounded Story command composition，并把 Story-owned flat numeric terminal result严格关联回 Trigger `ActionResult`。bounded admission与Story module drain共享exact default validator实例；command/result/correlation均为derived transient，不改变Trigger/Story hash、SaveState或replay wire。Trigger core与Story/Story.Runtime均不反向引用bridge；Unity/network adapter与Playable仍未交付。

`MxFramework.VoiceInput` 是 noEngine 语音输入事实层，依赖 Runtime，用 `VoiceInputEvent` / `VoiceInputSnapshot` 暴露 session、audio activity、转写和错误事实；它不依赖 Audio、Input、Gameplay 或 Unity。`MxFramework.VoiceInput.Unity` 是 Unity 麦克风 adapter，只负责设备/权限/采样活动，不做语义解析，也不把语音直接伪装成 `InputIntent`。后续语音命令映射应放在项目组合根或可选 bridge 中。

`MxFramework.Settings` 是 noEngine 用户偏好核心；`MxFramework.Settings.Audio` 只桥接 `IAudioService`，`MxFramework.Settings.Input` 只桥接 Input 重绑定和显示服务，`MxFramework.Settings.Graphics` 只桥接 Unity runtime / URP / Rendering 表现设置。Settings 不进入 Runtime SaveState、Replay、Runtime hash 或网络同步。

`MxFramework.Platform` 是 zero framework dependency、noEngine 宿主事实层，提供 typed environment/location/lifecycle/permission/wall-clock contracts、fixed-capacity lifecycle buffer、completed permission operation 和 BCL UTC clock。`MxFramework.Platform.Unity` 只单向依赖 Platform + UnityEngine，缓存 environment/location facts、规范化 composition-owned lifecycle callbacks，并通过单 active request + main-thread Pump 暴露 microphone authorization。两者都不执行保存/暂停/场景/UI policy，也不进入 Runtime authority、Replay、hash 或 Gameplay Trigger；当前 consumer 尚未迁移。具体 API、线程、分配和 overflow resync 语义见 `Docs/Interfaces/Platform.md`。

`MxFramework.Persistence.FileSystem` 实现 writable `FileSystemPath` 预检、严格 ID 路径布局、root/backend/probe-time capability evidence，以及 B2 Commit/Delete/Read/List generation journal。只有 `FileSystemSaveGameStoreFactoryCreateResult.Success` 表示完整预检成功；单个 evidence flag 不是跨卷、跨平台或断电 durability 保证。成功 factory 可通过 `CreateStore()` 创建独立 capacity=8 的 `ISaveGameStore`；同 slot 跨实例写入由 slot 目录内 `.writer.lock` 协调，不同 slot 可并发发布。Current 只对有界损坏允许 LKG 下扫，Exact 永不回退，Tombstone/unsupported/IO/permission 形成恢复屏障；List 对任一严格 head 失败采取整体失败；发布后 retention 保留 head 与最近一个完整旧记录，清理失败只产生 typed warning，不回滚发布。具体 API、分配与证据边界见 `Docs/Interfaces/Persistence.md`。

```text
                    Core  Events  Attr  Modif  Buffs  AI    Config  Resources  Anim  Diag  Logging  Runtime  Gameplay
          Core      -     -       -     -      -      -     -       -          -     -     -        -        -
          Events    ✓     -       -     -      -      -     -       -          -     -     -        -        -
          Attr      ✓     ✓       -     -      -      -     -       -          -     -     -        -        -
          Buffs     ✓     ✓       ✓     -      -      -     -       -          -     -     -        -        -
          Modif     ✓     ✓       ✓     -      ✓*     -     -       -          -     -     -        -        -
          AI        ✓     -       -     -      -      -     -       -          -     -     -        -        -
          Config    ✓     -       -     -      -      -     -       -          -     -     -        -        -
          Resources ✓     -       -     -      -      -     -       -          -     -     -        -        -
          Anim      -     -       -     -      -      -     -       ✓          -     -     -        -        -
          Diag      ✓     -       -     -      -      -     -       -          -     -     -        -        -
          Logging   ✓     -       -     -      -      -     -       -          -     -     -        -        -
          Runtime   -     -       -     -      -      -     -       -          -     -     -        -        -
          Gameplay  ✓     ✓       ✓     ✓      ✓      -     -       -          -     -     -        -        -
          Editor    ✓     ✓       ✓     ✓      ✓      ✓     ✓       ✓          ✓     ✓     ✓        ✓        ✓

          ✓* = Modifiers → Buffs 只允许通过 IBuffPipeline 等接口访问。
```

Logging current state: the implemented API is `IRuntimeLogger` in `MxFramework.Runtime` plus `UnityRuntimeLogger` in `MxFramework.Runtime.Unity`. There is no standalone `MxFramework.Logging` assembly, `LogBuffer`, or `Logging.Diagnostics` adapter in the current codebase.

`MxFramework.DebugUI` 只依赖 Diagnostics / Core；跨 Runtime、Resources、Gameplay、Combat、CharacterControl、Config Runtime 的接入放在 Debug UI adapter 层或组合根中，保持被观察模块不反向依赖 Debug UI。`MxFramework.DebugUI.Input` 是可选 Unity/Input 桥，不进入 noEngine 依赖矩阵。Performance counters、Simulation Harness、hot reload observation 与 command gate diagnostics 归入观察 / 报告 API，默认不改变 Runtime authority、Replay、SaveState 或 hash。

`MxFramework.Network` 是 noEngine 网络抽象，只依赖 `MxFramework.Runtime`。网络层传输无关，当前提供 LAN `NetworkRoomSessionRunner`（join / ready / start / handoff）、gameplay `NetworkSessionRunner`、object `LoopbackNetworkTransport`、`INetworkMessageCodec` JSON wire codec、`CodecLoopbackNetworkTransport` byte loopback，以及独立 Unity-facing 的 `MxFramework.Network.UnityTransport` 适配；CharacterTest 组合根已支持 object loopback、同进程本机 UTP loopback、UTP server/client 单端入口、多 client ownership 和 UTP Room MVP 验证。模块状态通过 `INetworkSnapshotParticipant` / `INetworkPeerSnapshotParticipant` 暴露为 `RuntimeCustomState`，网络核心不读取 Gameplay / CharacterTest / Combat 私有字段；server 可通过 command authorizer 在入队前按 gameplay ownership 拒绝未授权 command。`RemoteBundleProvider` 仍归 Resources 资源分发，不属于 gameplay session 网络；Animation presentation sync 只消费项目网络层提供的权威表现状态。

`MxFramework.Items` 是 noEngine 物品定义层，依赖 Core / Config / Resources，不依赖 Runtime、Gameplay、Character.Application 或 Unity；非装备 definition 必须声明正数 stack policy，装备默认归一为单件堆叠。`MxFramework.Items.Loot` 是 noEngine 掉落解析层，依赖 Items / Runtime，用 `IDeterministicRandom` 把掉落表解析成 `LootItemQuantity[]`，不改库存或生成地上物；`MxFramework.Items.Runtime` 是 noEngine 库存 runtime，依赖 Items / Runtime，拥有自己的 command buffer、event queue、hash contributor、SaveState provider 和原子插入 planner；只有 custom state 内容等价的非实例 stack 才能合并，SaveState restore 在临时 store 完整验证后才替换 live state。`MxFramework.Items.Acquisition` 是 noEngine 获取事务底座，依赖 Items / Items.Runtime / Runtime，用 request idempotency ledger 把直接发放和购买式 cost+grant 原子提交到库存；`MxFramework.Items.World` 是 noEngine world item runtime，依赖 Items.Runtime / Runtime，负责 active world item、pickup、过期、hash 和 SaveState。`MxFramework.Items.Network` 是可选 sibling bridge，提供 network-safe inventory command codec、owner authorizer、bootstrap/resync snapshot participant 和 reusable rollback state participant；网络 payload 只传 definition hash、短 id 和 slot/stack 变化，不传 catalog/profile/resource 内容，acquisition request handle 也不跨网络。rollback participant 在构造时缓存 definition hash，固定布局 warmup 后 capture 为 `NoAlloc`，因此 registry 必须在 participant 创建前加载完成并在 session 内保持不变。`MxFramework.Items.CharacterApplicationBridge` 是可选 sibling bridge，只把装备容器投影为 Character.Application 兼容的 `WeaponConfig[]` / `EquipmentLoadoutConfig`，不能让 Items core 反向依赖 Character.Application。框架内置 profile type、resource usage、inventory/world/acquisition module、常用装备 slot/tag id 使用公开常量访问；wire string 保持兼容。

`WeaponTypeDefinition` 与 `WeaponTypeCatalog` 的公共字段/查询及 `WeaponConfig.WeaponTypeId` 见 [Character Application](Interfaces/CharacterApplication.md)；唯一类型源与动作 requirement 的完整契约见 [武器配置 3.4](CHARACTER_WEAPON_CONFIGURATION.md#34-实体武器类型与动作准入)。该新增实现目前在隔离候选中，原生发布与 Player 验收待放行。

`MxFramework.Character.Application` 是应用层配置与纯解析器契约，当前只依赖 `MxFramework.Config`。它保存 Character 聚合所需的静态表、typed id、schema 元数据、resolved profile 和 diagnostics，不拥有 Runtime / Gameplay / Combat / Resources / Animation / CharacterControl 的权威状态；Runtime Spawn 也必须保持下层模块不反向依赖 Character Application。

`MxFramework.Character.Cloth` 是 noEngine 表现控制契约，不引用 Unity 或布料供应商；`MxFramework.Character.Cloth.Magica.Unity` 是受 `MAGICACLOTH2` 保护的可选 Unity adapter，只把 Requested State 与 System Override 映射为 Magica Cloth 组件的运行、暂停、Reset 禁用和 Keep 禁用。布料结果不进入 Character / Gameplay / Combat authority、Runtime hash、SaveState、Replay 或网络同步，也不得驱动命中挂点和战斗碰撞。

`MxFramework.Camera` 是表现层 noEngine 模块，只依赖 Core。Unity Camera 应用放在 `MxFramework.Camera.Unity`；Animation 表现事件到相机 request 的桥接放在 `MxFramework.Camera.Animation`；Debug UI 只通过 adapter 读取 `MxCameraDebugSnapshot`。Camera 默认不进入 Gameplay / Combat authority、Runtime hash、Replay hash 或 SaveState。

`MxFramework.Rendering` 是 Unity + URP-facing 表现层模块，允许引用 UnityEngine、UnityEngine.Rendering 和 UnityEngine.Rendering.Universal，但不进入 noEngine 依赖矩阵。Core / Runtime / Gameplay / Combat / Buffs / Resources / Runtime AI Planner 等 noEngine 模块不得引用 Rendering；Rendering 自身也不得引用 Gameplay、Combat、Character、Buffs、Animation 或 Camera。源模块到 Rendering 的接入必须放在可选 bridge 程序集（如 `MxFramework.Rendering.GameplayBridge`、`MxFramework.Rendering.CombatBridge`、`MxFramework.Rendering.CharacterBridge`、未来可选 `MxFramework.Rendering.CameraBridge`）或组合根中。`MxFramework.Rendering.GameplayBridge` 当前只实现 Phase 15.6 生命周期子集：从 `GameplayRuntimeModule.DrainEvents(...)` 和公开 `GameplayRuntimeEvent` payload 读取 ComponentEntity created/destroyed 与 legacy EntityDespawned 事件，映射到 `IRenderDataPublisher.PublishSubjectLifecycle`，并延后 despawn 的 source-id 到 `MxRenderSubjectId` 映射释放，使真实 `RenderDataPublisher` 当前帧仍可观察 Despawned 事件；Combat / Character / Camera bridge、MaterialBindingHub 写入、SharedRT 写入、VolumeBlender、Demo、Runtime authority、Replay hash 和 SaveState 仍保持 deferred。Rendering diagnostics 使用 `MxFramework.Diagnostics.IFrameworkDebugSource`，不依赖 `MxFramework.DebugUI`。

`MxFramework.Vfx` 是 noEngine VFX 意图 / 实例层，依赖 Runtime / Resources / Diagnostics，不依赖 Gameplay、Combat、Rendering 或 Unity。Gameplay / Combat / Rendering / Unity 接入分别放在 `MxFramework.Vfx.GameplayBridge`、`MxFramework.Vfx.CombatBridge`、`MxFramework.Vfx.RenderingBridge` 和 `MxFramework.Vfx.Unity`。VFX runtime 使用独立 `RuntimeCommandBuffer`，active instances、pending requests、anchor、参数、definition hash 进入 Runtime hash 和 SaveState；Unity prefab / ParticleSystem / VFX Graph / Unity Physics 只属于表现 adapter，不进入 replay/hash/SaveState。

`MxFramework.Audio` 是 noEngine 通用播放契约，不依赖 Combat、Unity 或 FMOD；`MxFramework.Audio.Physical` 是可选 noEngine 表现桥，可以读取 Combat contact facts 并调用 Audio，但 Combat 不反向依赖 Audio。当前公开 API 以 `Interfaces/Audio.md` 为准。已接受的 Physical Interaction → Basis Sound 目标边界见 `AUDIO_PHYSICAL_EXPRESSION_DESIGN.md` 和 ADR-007；尚未落地的类型不得提前当作 Current API 使用。

Story 依赖规则见 `Docs/Decisions/ADR-004-story-module-scope.md` 和 `Docs/Decisions/ADR-005-story-runtime-command-boundary.md`。Story core 只依赖 Core + Events；Story.Runtime 独立接 Runtime；Story.Config 只依赖 Story + Config；Story.GameplayBridge、Story.ResourcesBridge、Story.RuntimeAiPlannerBridge 都是 sibling bridge，不能让 Story core 反向依赖 Runtime、Gameplay、Config、Resources 或 Runtime AI Planner。Story.Unity 是 Unity-facing runtime adapter，只 enqueue Story `RuntimeCommand` 或 consume `StoryRuntimeEvent`，不引用 `UnityEditor`。Story.Editor 是 Editor-only 只读 debug surface，可引用 DebugUI / Diagnostics / UnityEditor，但不能被 runtime assembly 引用。Story 与 Gameplay 必须使用独立 `RuntimeCommandBuffer` drain owner；Story bridge 只能 enqueue Gameplay commands，不能 drain Gameplay command buffer。

Story external authoring tools live under `Tools/MxFrameworkStoryAuthoring/` and emit `.story.json` drafts for Story.Config handoff. They are not runtime assemblies and must not add Unity, Runtime AI Planner, Authoring AI Assist execution, Yarn, Ink, Articy, or WGame production narrative dependencies.

## AI Terminology

> 本项目中的 "AI" 一词指代四种不同的概念。为避免混淆，所有文档、任务、接口和目录命名必须遵循以下四域区分：

| 域 | 推荐名称 | 含义 | 不要与什么混淆 |
|----|---------|------|--------------|
| **Runtime AI Planner** | 运行时规划器 | `MxFramework.AI` 模块：`AiWorldState`、`IAiGoal`、`IAiAction`、`IAiPlanner`、`SequentialPlanner`。游戏内 NPC 的轻量行为决策引擎。 | LLM、Agent 工作流、配置数据 |
| **AIAction Config** | AIAction 配置迁移 | 旧 WGame 的 AI 行为配置数据：`AIActionIndex`/`AIActionGraph`、`AIConfig`/`AIConfigDefense`/`AIGoals`、`TbCharacterAI` 以及 `TalentTree→AIAction` 多态引用。所有属于 Config 体系的 AI 数据迁移。 | 运行时 Planner 本身、LLM |
| **Authoring AI Assist** | 创作 AI 辅助 | `AiStepContext`、LLM 辅助编辑功能。读取当前编辑步骤上下文，解释字段、推荐值、生成修复建议。 | 游戏内 NPC AI 决策 |
| **Development Agent** | 开发 Agent | 参与源码、测试、文档或工具开发的 AI Agent；相关能力导航见 `Docs/USAGE.md §22`。 | Runtime AI 或 Authoring AI |

### 命名约束

1. **文档标题**禁止仅用 "AI" 作为独立名词。必须叠限定词：Runtime / AIAction / Authoring / Agent。
2. **任务命名**禁止 `AI_*` 裸前缀。必须带域：`RUNTIME_AI_PLANNER_*`、`AI_ACTION_MIGRATION_*`、`AUTHORING_AI_ASSIST_*`、`DEVELOPMENT_AGENT_*`。
3. **每篇涉及 AI 的文档**开头应标明'本文的 AI 指哪一域'。
4. 旧有任务文件名（如 `AI_ACTION_MIGRATION_PILOT_*`、`AUTHORING_EDITOR_04_AI_ASSIST`）保留，域已隐含在名称中，无需重命名。

## 总原则

- Runtime 不引用 `UnityEditor`。
- Framework 不引用 WGame、Entitas、Luban、CrashKonijn 或项目私有插件。
- 下层模块不引用上层模块。
- 跨模块协作优先通过接口、工厂、快照或组合根完成。
- Editor 只读取 Runtime 暴露的接口和 Debug Snapshot，不读取模块私有字段。
> Current Buff v2 note (2026-06-12): the old `Packages/com.vincent.wgameframework/Buffs/` assembly is removed. `Docs/Interfaces/Buffs.md` now documents component-native Buff v2 under `Packages/com.vincent.wgameframework/Gameplay/` and `Packages/com.vincent.wgameframework/Combat/GameplayBridge/`.
