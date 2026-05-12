# GAMEPLAY_COMPONENT_RUNTIME_V0_CLOSEOUT

## 目标

收口 Component Gameplay Runtime v0，把 09-19 批次形成的 component runtime 能力整理成一个可提交、可验证、可被 agent 继续使用的稳定版本。

本任务不是新增玩法功能，而是做阶段验收：

```text
component schema
-> component world hash / SaveState
-> lifecycle cleanup
-> spawn definitions
-> attribute runtime
-> component ability command / targeting / rules
-> vertical slice
-> docs / interfaces / capabilities
-> clean commit
```

## 背景

当前 component gameplay runtime v0 已具备最小闭环：

```text
RuntimeHost
-> RuntimeCommandBuffer
-> GameplayRuntimeModule
-> GameplaySystemPipeline
-> SpawnComponentEntity
-> GameplayAttributeSetComponent
-> CastComponentAbilityRequest
-> component targeting
-> cooldown / cost rules
-> attribute delta effect
-> lifecycle cleanup
-> event queue
-> hash
-> SaveState roundtrip
```

下一步不应继续扩 Buff、Combat、cast time 或 UI。先把当前阶段的代码、测试和文档对齐，避免后续 agent 在脏工作树和半同步文档上继续叠功能。

## 范围

本任务只做 closeout：

- 确认新增源码、测试、`.meta` 文件都纳入 SVN。
- 确认 `Docs/Interfaces/Gameplay.md`、`Docs/README.md`、`Docs/CAPABILITIES.md` 同步当前 v0 能力。
- 运行 focused tests 和 build。
- 处理明显命名 / 文档 / 测试不一致。
- 提交一个或少数几个按范围拆分的 SVN revision。
- 生成简短 closeout 记录。

## 不做

本任务不要做：

- Buff / Modifier component runtime。
- Combat bridge。
- cast time / interrupt / timeline。
- UI / playable demo scene。
- Config / authoring import。
- API 大重命名。
- 默认 pipeline 行为大调整。

如果 closeout 过程中发现这些需求，只记录为后续任务，不混进本任务。

## 必查文件组

### Runtime source

需要纳入或确认的主要文件组：

- Component schema / hash / SaveState:
  - `GameplayComponentWorldHashContributor`
  - `GameplayComponentWorldSaveState`
  - `GameplayComponentWorldSaveStateProvider`
  - schema registry capability updates
- Lifecycle:
  - `GameplayLifecycleCleanupSystem`
  - `GameplayLifecycleEvents`
- Spawn:
  - `GameplayComponentSpawnDefinition`
  - `GameplayComponentSpawnRegistry`
  - `GameplayComponentSpawnInitializer`
  - `GameplayComponentSpawnCommandSystem`
  - `GameplayComponentSpawnEvents`
- Attribute:
  - `GameplayAttributeValue`
  - `GameplayAttributeSetComponent`
  - `GameplayAttributeCommandSystem`
  - `GameplayAttributeEvents`
  - `GameplayAttributeComponentSchemaDescriptors`
- Component ability:
  - `GameplayComponentAbility`
  - `GameplayComponentAbilityRegistry`
  - `GameplayComponentAbilityCommandSystem`
  - `GameplayComponentAttributeDeltaAbility`
  - `GameplayComponentAbilityEvents`
  - `GameplayComponentAbilityRequest`
  - `GameplayComponentAbilityRequestStore`
- Component targeting:
  - `GameplayComponentTargetCandidate`
  - `GameplayComponentTargetCandidates`
  - `GameplayComponentTargetQuery`
  - `GameplayComponentTargetingResult`
  - `GameplayComponentTargetingService`
- Rules:
  - `GameplayAbilityCooldownComponent`
  - `GameplayAbilityCooldownEntry`
  - `GameplayAbilityCooldownComponentSchemaDescriptors`
  - `GameplayAbilityCost`
  - `GameplayComponentAbilityRuleSet`
  - `GameplayComponentAbilityRuleResult`
  - `GameplayComponentAbilityRules`
- Shared command / event updates:
  - `GameplayRuntimeCommandIds`
  - `GameplayRuntimeCommandFactory`
  - `GameplayRuntimeEvent`
  - `GameplayComponentRegistry`
  - `GameplayEntityLifecycle`

### Tests

需要纳入或确认的测试：

- `GameplayComponentSchemaRegistryTests`
- `GameplayComponentWorldHashContributorTests`
- `GameplayComponentWorldSaveStateTests`
- `GameplayLifecycleCleanupSystemTests`
- `GameplayComponentSpawnDefinitionTests`
- `GameplayAttributeRuntimeTests`
- `GameplayComponentAbilityCommandSystemTests`
- `GameplayComponentAbilityTargetingTests`
- `GameplayComponentAbilityRulesTests`
- `GameplayComponentRuntimeSliceTests`

### Docs

需要确认：

- `Docs/Interfaces/Gameplay.md`
- `Docs/README.md`
- `Docs/CAPABILITIES.md`
- task docs 09-19

## 验证命令

至少运行：

```bash
git diff --check
Tools/GitNexus/gitnexus.sh detect-changes
dotnet build MxFramework.Gameplay.csproj --no-restore
dotnet build MxFramework.Tests.csproj --no-restore
dotnet test MxFramework.Tests.csproj --no-build --filter GameplayComponentRuntimeSliceTests
```

如果 build/test 环境不可用，必须在 closeout 记录里写明原因和替代验证。

建议额外运行：

```bash
dotnet test MxFramework.Tests.csproj --no-build --filter GameplayComponent
```

## 验收标准

- `svn status` 中没有本阶段相关的未纳入 `?` 文件。
- Runtime source、tests、docs、`.meta` 成对提交。
- Component runtime v0 focused tests 通过。
- `GameplayComponentRuntimeSliceTests` 覆盖：
  - spawn
  - attribute
  - targeting
  - ability rules
  - cooldown reject
  - lifecycle cleanup
  - hash
  - SaveState roundtrip
  - request store 不进入 SaveState
- 文档明确：
  - 当前 component gameplay runtime v0 已 closed。
  - 旧 `RuntimeEntity` route 仍保留。
  - Component ability rules policy 已锁定：cost 在 effect 前提交；effect failure 不自动 refund；cooldown 只在 effect success 后启动；过期 cooldown 是 cast 前惰性清理。
  - 下一阶段才考虑 Buff / Modifier、Combat、cast time、UI / playable demo。
- 提交消息能看出是 v0 closeout，而不是继续扩功能。

## Known follow-ups

这些是 v0 closeout 后保留的已知语义和后续工作，不阻塞本阶段验收：

- `GameplayComponentAbilityRequestStore` 是 transient input store，不进入 ComponentWorld hash / SaveState；save 发生在 request 入队后、command 执行前时，pending request 不由 ComponentWorld SaveState 捕获。
- `GameplayComponentAbilityRequestStore.Clear()` 只清 pending requests，不重置 allocator index / generation；旧 handle 在 clear 后仍然失效。
- `GameplayRuntimeEvent` 已承载旧 Ability、component entity、attribute、component ability、command rejection 等多类事件；后续 target list、cost/cooldown detail、cast time、interrupt 等复杂信息不应继续无边界加字段，优先引入 event detail / custom state / typed event stream。
- Component ability cost policy 当前是 `ConsumeOnCastAttempt` 风格：cost 在 ability effect 前提交，effect failure 不退还 cost；cooldown 只在 effect success 后启动。
- Cooldown cleanup 当前是 cast 前惰性清理；闲置 entity 的过期 cooldown 可能继续留在 hash / SaveState 中，直到该 entity cast 或未来 cleanup system 处理。
- Enemy HP 到 0 不会自动触发 death；vertical slice 通过测试 helper 标记 lifecycle `PendingDestroy`，完整 DeathSystem 属于后续任务。
- Spawn definition、ability registry、request store 都是组合根输入依赖，不是 world state；restore 只恢复 ComponentWorld 结果状态，继续 cast 需要重新提供 runtime registries。

## 建议提交策略

如果改动很多，建议拆成 2-3 个 SVN revision：

1. Runtime source + `.meta`
2. Tests + `.meta`
3. Docs closeout

如果当前工作树已经是同一批完整实现，也可以单次提交，但提交前必须确认没有混入无关文件。

## 后续阶段候选

Closeout 完成后，再选择下一阶段：

1. `GAMEPLAY_COMPONENT_BUFF_MODIFIER_01`
   - component-native buff / modifier state。
2. `GAMEPLAY_COMPONENT_COMBAT_BRIDGE_01`
   - damage / hit result / combat event bridge。
3. `GAMEPLAY_COMPONENT_RUNTIME_SHOWCASE_01`
   - playable or diagnostic showcase scene。
4. `GAMEPLAY_COMPONENT_CAST_TIMELINE_01`
   - cast time / interrupt / pending operation。

推荐优先顺序：

```text
Closeout
-> Runtime Showcase
-> Buff / Modifier or Combat bridge
```

原因：当前 runtime v0 已经能跑通测试闭环，下一步最好先做一个可观察、可演示、可诊断的 showcase，再决定继续补 Buff/Combat 哪条业务链。

## Closeout record

日期：2026-05-12

本次 closeout 已确认：

- 09-19 产生的 component runtime source、tests、Unity `.meta` 和文档改动纳入 SVN 提交范围。
- `Docs/Interfaces/Gameplay.md`、`Docs/README.md`、`Docs/CAPABILITIES.md` 已同步 component runtime v0 能力。
- `GameplayComponentAbilityRulesTests` 的 request-target fixture 已补齐 `GameplayLifecycleComponent.Alive`，避免 `requireAlive` query 把测试目标按 dead 过滤。
- 本阶段不新增 Buff / Modifier、Combat bridge、cast timeline 或 showcase UI。

验证结果：

```text
Tools/GitNexus/gitnexus.sh detect-changes
  Changes: 51 files, 133 symbols
  Affected processes: 0
  Risk level: low

dotnet build MxFramework.Gameplay.csproj --no-restore
  0 warnings, 0 errors

dotnet build MxFramework.Tests.csproj --no-restore
  0 errors
  existing Demo serialization warnings only

dotnet test MxFramework.Tests.csproj --no-build --filter GameplayComponentRuntimeSliceTests
  exited 0, but Unity-generated project did not enumerate tests outside Unity Test Runner

Unity Test Runner CLI
  blocked because another Unity Editor instance already had this project open

Temp/ComponentTestRunner
  Executed 111 component runtime NUnit tests
  Passed 111
```

说明：`Temp/ComponentTestRunner` 是 closeout 验证用临时 runner，不属于提交内容。
