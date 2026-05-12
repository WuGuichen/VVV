# GAMEPLAY_COMPONENT_RUNTIME_SHOWCASE_01

## 目标

把已 closeout 的 Component Gameplay Runtime v0 做成可观察 showcase，验证它不仅能在单元测试里跑通，也能被 Demo / agent / 项目组合根按固定方式复用。

本任务的重点是展示现有 v0 能力，不继续扩 Gameplay 核心。

```text
RuntimeHost
-> RuntimeCommandBuffer
-> GameplayRuntimeModule
-> GameplaySystemPipeline
-> GameplayComponentWorld
-> spawn / target / ability rules / cleanup
-> RuntimeEventQueue
-> hash / SaveState
```

## API 复用计划

| 需求点 | 使用模块 | 说明 |
| --- | --- | --- |
| Runtime loop | `RuntimeHost` / `GameplayRuntimeModule` | showcase 不自建 tick loop |
| Commands | `RuntimeCommandBuffer` / `GameplayRuntimeCommandFactory` | spawn 和 ability cast 都通过 command |
| Component state | `GameplayComponentWorld` / component stores | entity、attributes、cooldown、lifecycle 只有 component world 一个 source of truth |
| Spawn | `GameplayComponentSpawnRegistry` / `GameplayComponentSpawnCommandSystem` | hero / enemy 由 definition 初始化 |
| Ability | `GameplayComponentAbilityRegistry` / `GameplayComponentAbilityCommandSystem` | Strike ability 走 component-native ability |
| Targeting | `GameplayComponentAbilityRequestStore` / `GameplayComponentTargetingService` | explicit target request 演示 generation-safe target |
| Rules | `GameplayComponentAbilityRuleSet` / `GameplayAbilityCooldownComponent` | 演示 cost、cooldown accept/reject |
| Cleanup | `GameplayLifecycleCleanupSystem` | `PendingDestroy` entity 在 Resolution phase 清理 |
| Hash | `GameplayComponentWorldHashContributor` | UI 和测试显示 runtime hash |
| SaveState | `GameplayComponentWorldSaveStateProvider` / `RuntimeSaveStateJson` | 保存、恢复、恢复后继续 cast |
| UI | UI Toolkit UXML / USS + runner | C# 只做组合根、按钮绑定和 snapshot 显示 |

## 交付范围

新增：

- `GameplayComponentRuntimeShowcase`
  - 纯 runtime showcase 组合根。
  - 提供 `SpawnActors()`、`CastStrike()`、`MarkEnemyPendingDestroyAndTick()`、`Save()`、`Restore()`。
  - 输出 `GameplayComponentRuntimeShowcaseSnapshot`。
- `GameplayComponentRuntimeShowcaseRunner`
  - Unity `MonoBehaviour` composition root + UI Toolkit binding。
  - UI button 只调用 showcase API；状态变化仍走 runtime command path。
- `GameplayComponentRuntimeShowcase.uxml` / `.uss`
  - 显示 frame、hero/enemy attributes、cooldown、hash、SaveState 状态、runtime events。
- `CreateGameplayComponentRuntimeShowcaseScene`
  - Editor 菜单生成 scene / PanelSettings / UIDocument 绑定。
- focused tests
  - spawn -> cast -> cooldown reject -> second cast -> cleanup。
  - save -> restore -> continue cast。

## 不做

- 不新增 Buff / Modifier component runtime。
- 不接 Combat bridge。
- 不做 cast time / interrupt。
- 不把旧 `RuntimeEntity` 和 component world 双写。
- 不手写 `.unity`、Prefab 或 ScriptableObject YAML。

## 当前交付等级

`Runtime Slice`

原因：本任务新增了 Unity runner、UXML/USS 和 Editor scene generator，但当前环境中 Unity Editor 已经打开同一项目，CLI 无法独占打开工程生成并保存 `.unity` 场景。因此本提交不声称 Playable 完成。

生成可打开场景的方式：

```text
Unity Menu:
MxFramework/Gameplay Component Runtime/Create Showcase Scene
```

生成后场景路径：

```text
Assets/Scenes/GameplayComponentRuntimeShowcase.unity
```

## 手测流程

1. 在 Unity 中运行菜单 `MxFramework/Gameplay Component Runtime/Create Showcase Scene`。
2. 打开 `Assets/Scenes/GameplayComponentRuntimeShowcase.unity`。
3. Play。
4. 使用按钮：
   - `Spawn`：通过 spawn command 创建 hero / enemy。
   - `Cast Strike`：消耗 mana，伤害 enemy，启动 cooldown。
   - 再次 `Cast Strike`：应输出 cooldown rejected，不改变 hp / mana。
   - 等到下一次 frame 后 `Cast Strike`：enemy hp 到 0。
   - `Cleanup`：标记 enemy `PendingDestroy` 并由 `GameplayLifecycleCleanupSystem` 清理。
   - `Save` / `Restore`：保存 component world，恢复后继续 cast。
   - `Run Full Flow`：自动跑完整展示链。

## 验收标准

- Demo runtime tests 通过。
- `MxFramework.Demo` / `MxFramework.Tests` 编译通过。
- `Docs/USAGE.md` 写清菜单和当前交付等级。
- `Docs/CAPABILITIES.md` 标记为 Runtime Slice，不误报 Playable。
- 后续如果环境允许生成 scene，需要补 Play Mode smoke，并把交付等级升级为 `Playable`。

## 验证记录

日期：2026-05-12

```text
dotnet build MxFramework.Demo.csproj --no-restore
  passed, existing Demo serialization warnings only

dotnet build MxFramework.Demo.Editor.csproj
  passed, existing Demo serialization warnings only

dotnet build MxFramework.Tests.csproj --no-restore
  0 warnings, 0 errors

Temp/ShowcaseTestRunner
  Executed 2 showcase tests
  Passed 2

dotnet test MxFramework.Tests.csproj --no-build --filter GameplayComponentRuntimeShowcaseTests
  exited 0, but Unity-generated project did not enumerate tests outside Unity Test Runner

Unity executeMethod scene generation
  blocked because another Unity Editor instance already had this project open
```

## 后续

Showcase 可打开并通过 Play Mode smoke 后，再选择：

1. `GAMEPLAY_COMPONENT_BUFF_MODIFIER_01`
2. `GAMEPLAY_COMPONENT_COMBAT_BRIDGE_01`
3. `GAMEPLAY_COMPONENT_CAST_TIMELINE_01`
