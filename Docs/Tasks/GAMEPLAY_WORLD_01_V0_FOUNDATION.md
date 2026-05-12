# Gameplay World 01：v0 Foundation

> 状态：Completed / Verified 2026-05-10
> 日期：2026-05-10
> 优先级：P0
> 前置：`PHASE11_RUNTIME_GAMEPLAY_GOAL.md`、`RUNTIME_FOUNDATION_04_V1_PARALLEL_CLOSEOUT.md`

## 目标

把现有 `RuntimeEntity` / `SimpleAbility` / Target / Effect 切片推进到世界级玩法运行层。GameplayWorld v0 不追求完整战斗系统，而是提供可注册、可查询、可 tick、可诊断、可 hash 的实体世界基础，让后续 Ability Graph、AI、Authoring Preview 和 Combat bridge 有统一接入口。

## 公共契约冻结

- `MxFramework.Gameplay` 继续保持纯 C#，不得引用 `UnityEngine` 或 `UnityEditor`。
- Runtime 不反向引用 Gameplay；Gameplay 如需 Runtime hash contract，只能由本批 01E 明确接入 `MxFramework.Runtime`。
- 不把 Demo 类型提升为框架 API。
- 不迁移 WGame 真实数据，不引入 WGame 命名空间或私有插件。
- 现有 `RuntimeEntity`、`SimpleAbility`、`ITargetSelector`、`IAbilityEffect` 行为保持兼容。
- 子任务只写自己的文件范围；遇到需要改公共文件时，先在结果中说明，不直接扩大范围。

## 并行任务

| 任务 | 状态 | 负责人范围 | 任务文档 |
|------|------|------------|----------|
| 01A GameplayWorld Contract | Completed | World / entity registry / lifecycle / tick | `GAMEPLAY_WORLD_01A_WORLD_CONTRACT.md` |
| 01B Team Tag Status | Completed | Team relation / tag set / status set | `GAMEPLAY_WORLD_01B_TEAM_TAG_STATUS.md` |
| 01C Targeting Service | Completed | Query spec / filtering / explanation | `GAMEPLAY_WORLD_01C_TARGETING_SERVICE.md` |
| 01D Ability Runtime Adapter | Completed | Ability registry / cast request / cast service | `GAMEPLAY_WORLD_01D_ABILITY_RUNTIME_ADAPTER.md` |
| 01E Diagnostics Hash | Completed | Gameplay hash contributor / world diagnostics extension | `GAMEPLAY_WORLD_01E_DIAGNOSTICS_HASH.md` |

## 集成顺序

1. 01A 先稳定 world 和 registry API。
2. 01B 提供 team/tag/status 基础数据结构。
3. 01C 接入 01A/01B 的查询能力，必要时先用接口解耦。
4. 01D 将 ability cast service 接入 world registry 和 targeting。
5. 01E 把 world 或 entity 集合接入 Runtime hash contract，并扩展诊断。

## 非目标

- 不做 Ability Graph、可视化编辑器、Timeline、Animation Event、Projectile。
- 不做完整公式 DSL、装备、背包、任务、经济系统。
- 不做网络同步、rollback 或服务器权威模拟。
- 不做 Unity 场景绑定、GameObject lifecycle 或 Addressables 资源绑定。

## 验收

- 可以创建 `GameplayWorld`，注册 / 移除 / 查询实体，并用稳定顺序 tick。
- Team / Tag / Status 能被独立测试，并能支持目标过滤。
- Targeting service 可以按基本 alive/team/tag/status 条件筛选并输出失败原因。
- Ability runtime adapter 可以通过 request/service 释放现有 `IAbility`。
- Gameplay hash contributor 对同一世界状态输出稳定 hash，状态变化时 hash 变化。
- `dotnet build MxFramework.Tests.csproj --no-restore` 通过。

## 分发规则

子代理开始前必须读取本文件和对应子任务文档。所有子代理都不是独自在代码库中工作，不能回退或覆盖其他人的改动；如果遇到未提交改动，应保留并围绕它们实现。

## 2026-05-10 实现记录

- 01A 已完成 `GameplayWorld`、`RuntimeEntityRegistry`、`GameplayWorldSnapshot`、稳定 entity id 枚举、world tick 和 snapshot copy。
- 01B 已完成 `GameplayTeamRelation` / `GameplayTeamRelations`、`GameplayTagId` / `GameplayTagSet`、`GameplayStatusId` / `GameplayStatusSet`。
- 01C 已完成 `GameplayTargetQuery`、`GameplayTargetCandidate`、`GameplayTargetingService`、`GameplayTargetingResult` 和 rejected reason。
- 01D 已完成 `GameplayAbilityRegistry`、`GameplayAbilityCastRequest`、`GameplayAbilityRuntimeService`、`GameplayAbilityRuntimeResult` 和 failure code。
- 01E 已完成 `GameplayHashContributor`、`GameplayWorldDiagnostics`，并让 `MxFramework.Gameplay` 引用 `MxFramework.Runtime` 以接入 runtime hash contract；`noEngineReferences=true` 保持不变。
- 父级验证：`dotnet build MxFramework.Gameplay.csproj --no-restore` 通过，0 warning / 0 error。
- 父级验证：`dotnet build MxFramework.Tests.csproj --no-restore` 通过，0 warning / 0 error。
- Unity EditMode：`GameplayWorldContractTests`、`GameplayTeamTagStatusTests`、`GameplayTargetingServiceTests`、`GameplayAbilityRuntimeAdapterTests`、`GameplayDiagnosticsHashTests` 共 38/38 passed；Unity Console error 0。
