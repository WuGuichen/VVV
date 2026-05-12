# GAMEPLAY_ECS_STYLE_19_COMPONENT_RUNTIME_VERTICAL_SLICE

## 目标

收口当前 component gameplay runtime 阶段，做一个最小但完整的垂直切片，证明以下链路可以稳定工作：

```text
RuntimeHost
-> RuntimeCommandBuffer
-> GameplayRuntimeModule
-> GameplaySystemPipeline
-> Spawn component actor
-> Attribute state
-> Ability targeting
-> Cooldown / cost rules
-> Attribute delta effect
-> Lifecycle death cleanup
-> RuntimeEventQueue
-> Runtime hash
-> SaveState roundtrip
```

本批次的目标是验收和收口，不是继续扩功能。

## 背景

前置批次已经陆续建立：

- generation-safe component entity
- component store / registry / world
- schema diagnostics / hash / SaveState
- lifecycle cleanup
- spawn definition
- attribute runtime
- component ability command bridge
- component ability targeting
- cooldown / cost rules

现在需要一个最小可运行切片，把这些能力串起来，避免基础设施只在孤立单元测试里成立。

## 切片场景

建议场景：

```text
Hero casts Strike on Enemy
```

实体：

- Hero
  - team = 1
  - lifecycle = Alive
  - attributes:
    - HP = 30
    - Mana = 10
    - Attack = 6
  - tags/status 可为空

- Enemy
  - team = 2
  - lifecycle = Alive
  - attributes:
    - HP = 12
    - Mana = 0

Ability：

- `Strike`
  - ability id = test-local stable id
  - target = explicit single enemy
  - effect = enemy HP -6 或 -12，按测试需要选择
  - cost = Hero Mana -3
  - cooldown = 2 frames

推荐第一版直接使用当前已有的 component ability / attribute delta ability；如果它只能固定 delta，不要在本批次引入复杂 formula。可以用测试专用 ability 表达 “target HP -12”。

## 范围

建议新增：

- `GameplayComponentRuntimeSliceTests`
- 必要的 test fixture / helper builder
- 可选 `GameplayComponentRuntimeSlice` 测试用组合根
- 文档同步

可以新增少量测试 helper，但不要新增大功能模块。

建议复用：

- `GameplayComponentSpawnRegistry`
- `GameplayComponentSpawnCommandSystem`
- `GameplayAttributeCommandSystem`
- `GameplayComponentAbilityRegistry`
- `GameplayComponentAbilityRequestStore`
- `GameplayComponentTargetingService`
- `GameplayComponentAbilityCommandSystem`
- `GameplayLifecycleCleanupSystem`
- `GameplayComponentWorldHashContributor`
- `GameplayComponentWorldSaveStateProvider`

## 不做

本批次不要做：

- 新 Ability 编辑器
- 新 Demo 场景
- Unity UI
- Combat bridge
- Buff / Modifier runtime
- cast time / interrupt
- projectile / hit window
- AI decision
- networking / rollback
- 配置文件导入

如果需要可视化 Demo，等本切片测试稳定后另开任务。

## 组合根要求

测试中显式搭建完整 runtime：

```csharp
var componentWorld = new GameplayComponentWorld();
RegisterCoreSchemas(componentWorld.Schemas);

var spawnRegistry = new GameplayComponentSpawnRegistry();
var abilityRegistry = new GameplayComponentAbilityRegistry();
var requestStore = new GameplayComponentAbilityRequestStore();

var commandBuffer = new RuntimeCommandBuffer();
var module = new GameplayRuntimeModule(
    new GameplayWorld(),
    new GameplayAbilityRegistry(),
    commandBuffer,
    componentWorld: componentWorld,
    configureDefaultPipeline: pipeline =>
    {
        pipeline.Add(new GameplayComponentSpawnCommandSystem(spawnRegistry));
        pipeline.Add(new GameplayAttributeCommandSystem());
        pipeline.Add(new GameplayComponentAbilityCommandSystem(
            abilityRegistry,
            requestStore,
            new GameplayComponentTargetingService()));
        pipeline.Add(new GameplayLifecycleCleanupSystem());
    });

var host = new RuntimeHost(...);
host.AddModule(module);
```

具体构造函数按当前实现调整，但原则是：

- 通过 `RuntimeHost.Tick()` 驱动。
- 通过 `RuntimeCommandBuffer.Enqueue()` 输入。
- 通过 `GameplayRuntimeModule.DrainEvents()` 观察输出。
- 不直接调用 system private API 完成主路径。

## 流程要求

测试流程建议：

1. 通过 `SpawnComponentEntity` command 创建 Hero。
2. 通过 `SpawnComponentEntity` command 创建 Enemy。
3. 验证 spawn event 输出 component entity id。
4. 创建 ability request，candidate = Enemy。
5. enqueue `CastComponentAbilityRequest`。
6. tick host。
7. 验证 Enemy HP 被扣。
8. 验证 Hero Mana 被扣。
9. 验证 Hero cooldown 被启动。
10. 再次 enqueue 同一 ability，验证 cooldown rejected。
11. 推进到 cooldown 结束 frame。
12. 再次 cast，Enemy HP 到 0 或以下。
13. 将 Enemy lifecycle 标记为 `PendingDestroy`。
14. tick resolution cleanup。
15. 验证 Enemy entity 被 destroy，registered stores 已清理。
16. 验证事件顺序稳定。
17. 验证 hash 在关键状态变化后改变。
18. 捕获 SaveState、JSON roundtrip、restore 到新 world。
19. 验证 restore 后 hash 与保存点一致。

如果当前 ability effect 不负责自动把 HP <= 0 转成 `PendingDestroy`，本批次可以用测试 command / helper 明确设置 lifecycle pending destroy。不要在垂直切片里顺手实现完整 DeathSystem。

## 事件顺序

成功 cast 推荐稳定顺序：

```text
ComponentAttributeChanged   // cost
ComponentAttributeChanged   // ability effect
AbilityCastSucceeded
```

Cooldown rejected：

```text
AbilityCastFailed / ComponentAbilityOnCooldown
```

Death cleanup：

```text
ComponentEntityDestroyed / PendingDestroyCleanup
```

测试应断言关键 event type、reason、ability id 和 component entity id。

如果当前实现的 cost / effect event 顺序不同，以第 18 批文档和实现的固定顺序为准，但必须在本批次锁定并测试。

## Hash 验证

至少验证：

- 初始空 world hash。
- spawn 后 hash 改变。
- cast 后 hash 改变。
- cooldown rejected 不应改变 component world state hash，除非规则实现会清理过期 cooldown。
- cleanup 后 hash 改变。
- SaveState restore 后 hash 等于保存点 hash。

Hash 测试不要断言具体数值，只断言相等 / 不等和稳定 roundtrip。

## SaveState 验证

SaveState 验证点：

- 保存 Hero / Enemy component entity。
- 保存 attribute set。
- 保存 lifecycle / team / identity。
- 保存 cooldown state。
- 不保存 transient ability request store。
- Restore 后不需要 spawn registry / ability registry 也能恢复世界状态。
- Restore 后如果继续 cast，则需要重新提供 ability registry / request store，这是 runtime input dependency，不是 SaveState 内容。

## Replay / command flow

本批次不要求实现完整 replay playback fixture，但至少要验证 command-driven flow：

- 不直接调用 component ability cast 完成主流程。
- 输入都通过 command buffer。
- 同一命令序列在 fresh world 上产生同等 hash。

可选新增：

- `ComponentRuntimeSlice_ReplayingSameCommandsProducesSameHash`

如果现有 Replay harness 容易接入，可以接入；否则先保留为 focused command-flow regression。

## 测试要求

至少新增：

- `ComponentRuntimeSlice_SpawnCastRulesAndCleanupFlow`
- `ComponentRuntimeSlice_EventsAreEmittedInStableOrder`
- `ComponentRuntimeSlice_CooldownRejectDoesNotApplyEffect`
- `ComponentRuntimeSlice_SaveStateRoundtripRestoresHash`
- `ComponentRuntimeSlice_CommandDrivenFlowProducesStableHash`
- `ComponentRuntimeSlice_RequestStoreIsNotSaved`

可选：

- `ComponentRuntimeSlice_ReplayingSameCommandsProducesSameHash`
- `ComponentRuntimeSlice_RestoreThenContinueCastWorksWithRuntimeRegistries`

## 文档同步

同步：

- `Docs/Interfaces/Gameplay.md`
- `Docs/README.md`

文档应说明：

- 当前 component gameplay runtime v0 已形成最小闭环。
- 旧 `RuntimeEntity` route 仍保留，不是本切片 source of truth。
- Buff / Modifier / Combat / cast time / UI demo 是下一阶段，不属于当前阶段。

## 验收

- RuntimeHost 能驱动完整 component gameplay slice。
- Spawn、attribute、targeting、ability、rules、cleanup 串成一条 command-driven flow。
- 关键事件稳定输出。
- Hash 在状态变化时稳定变化。
- SaveState JSON roundtrip 恢复同等 component world hash。
- Transient request store 不进入 SaveState。
- 不引入新大型系统。
- 不依赖 UnityEngine / UnityEditor / WGame 私有数据。
- 当前 component gameplay runtime 阶段可以标记为 v0 closed。

## 阶段收口说明

第 19 批完成后，当前阶段停止继续扩 runtime 基础功能。

后续新阶段再考虑：

- Buff / Modifier component runtime
- Combat bridge
- cast time / interrupt / timeline
- UI / Diagnostics view
- Config / Authoring integration
- playable demo scene

这些不应混进第 19 批。
