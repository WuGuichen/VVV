# Items Interface

> Status: Current
>
> Version: 0.4.0 | 2026-10-01

Items 是通用物品定义、掉落解析、world item、slot-based 库存、底层获取事务和 Character.Application 装备投影基础。当前提供 noEngine 纯 C# 生命周期能力；Unity 场景拾取/3D 掉落表现、完整商店 UI/商品目录、交易、制作、UI 背包、耐久、随机词条或真实消耗品效果仍不属于 Items core。

## Assemblies

| Assembly | 依赖 | 职责 |
| --- | --- | --- |
| `MxFramework.Items` | `Core` / `Config` / `Resources` | `ItemDefinition`、稳定 id、类型、标签、堆叠策略、资源引用、profile 引用和定义注册表。 |
| `MxFramework.Items.Loot` | `Items` / `Runtime` | `LootTableDefinition`、分组抽取掉落、`LootContext` 和确定性 `LootResolver`。 |
| `MxFramework.Items.Runtime` | `Items` / `Runtime` | `InventoryContainer`、`InventorySlot`、`ItemStack`、命令请求、库存 runtime module、事件、hash 和 SaveState。 |
| `MxFramework.Items.Acquisition` | `Items` / `Items.Runtime` / `Runtime` | 低频获取事务底座：直接发放、购买式 cost + grant、幂等 ledger、命令请求、hash 和 SaveState。 |
| `MxFramework.Items.World` | `Items.Runtime` / `Runtime` | `WorldItemStack`、world item store/service/runtime module、pickup、过期、hash 和 SaveState。 |
| `MxFramework.Items.Network` | `Items.Runtime` / `Runtime` / `Network` | 可选网络桥：compact inventory command codec、owner authorizer、bootstrap/resync full snapshot participant 和 rollback participant。 |
| `MxFramework.Items.CharacterApplicationBridge` | `Items` / `Items.Runtime` / `Character.Application` | 将装备容器投影成兼容 `WeaponConfig[]` 和 `EquipmentLoadoutConfig` 的桥接层。 |

`MxFramework.Items` 不依赖 `Runtime`、`Gameplay`、`Character.Application` 或 Unity。`Items.Loot`、`Items.Acquisition` 和 `Items.World` 是 sibling runtime 能力，不让 definition core 反向依赖它们。`Items.Runtime` / `Items.Acquisition` / `Items.World` 不依赖 `Gameplay` 或 Unity。`Items.Network` 不依赖 Gameplay、CharacterTest 或 Unity；它只接 Network core 已暴露的 command authorizer、snapshot participant 和 rollback participant 钩子。桥接层是 sibling adapter，不让 Items core 反向依赖 Character.Application 或 Network。

## Lifecycle Model

### CharacterTest player transaction boundary

`CharacterTacticalPlayerService` is the existing single owner of card instances,
versions and bindings. Its optional ordinary-item checkpoint is stored in the same
`tactical-player` slot and existing `CharacterTacticalSaveStore` envelope/CAS backend.
Cards are granted through that owner; they never become a second Items inventory.
`ConfigureItemInventory` supplies explicit definitions and empty container layouts.
It restores a detached candidate first, rejects missing content/layout changes,
does not grant anything, and does not rewrite an existing v2 save. Display reads
return detached inventories, preventing collection aliases from changing authority.

`ExecuteItemTransactionAtTick` accepts an explicit acquisition request, optional
exact-version card grants, optional positive settlement id, expected frame and
Simulation tick context. The composition root must call it from its ordered
authoritative command drain. Wrong stages/frames and older frames are rejected.
The adapter must resume its monotonic item command clock from
`LastItemTransactionFrame`; a per-battle clock that restarts at zero needs an
explicit logical frame mapping before this boundary is connected.
Inventory costs/grants, the acquisition ledger, settlement marker, card allocator
and card entries are prepared and validated together, then published synchronously
as one owner revision. A failed candidate changes none of them. Duplicate request
or settlement ids do not repeat costs or rewards. Card grants use existing exact
catalog/pool rules and allocate instances through the existing card owner; they do
not invent action-slot bindings or modify battle-preparation presets/loadouts.

The opt-in `InventoryTransactionService.Execute(request, hasExternalGrant)` supports
card-only/cost-plus-card outer candidates. Its caller must validate and publish the
external grant and ledger together. Ordinary `Execute(request)` still rejects a
transaction with no item grant. This overload alone is not a cross-owner transaction.

Existing `mx.character-test.tactical-player.v2` checkpoints remain readable and
stay v2 until the first successful explicit item transaction. That candidate
becomes v3 and embeds `mx.character-test.items.v1` containing Inventory schema 3,
Acquisition schema 1, logical frame and settlement markers. Existing card identity,
counter, versions and bindings are preserved. An old binary that only reads v2
cannot read v3; this is forward migration, not downgrade support. V3 without an
available item catalog remains preserved; ordinary-item configuration is blocked
until its definitions and layout match. Structural/ledger validation happens on
player load; definition/hash/capacity validation happens on item configuration.

Success means the complete logical candidate is published. `HasUnsavedChanges`,
`SaveIssue` and `FlushAsync` separately report persistence. Write failures keep the
complete logical state dirty; retry does not repeat grants. Restart before a failed
write restores the complete prior checkpoint and permits replay of the same stable
request once. An external reward producer therefore must retain stable request and
settlement ids for replay. Async completion only updates persistence status; it does
not order or mint gameplay. Provider timestamps are omitted from the logical item
checkpoint. Filesystem CAS conflicts require reopening/reconciliation rather than
blindly overwriting another writer.

This is application-owner/transaction/persistence infrastructure with explicit
fixture content. It does not register an active battle command producer, ordinary
item catalog, reward source, shop or consumable effect in the CharacterTest slice.
Those adapters require concrete definitions/profile mappings, reward-source rules
and (when enabled) existing ability/effect ids. Equipment application remains the
existing Character.Application bridge; this transaction does not change combat
equipment or the separate battle-preparation store.

### Reliability hardening (2026-10-01)

Inventory add/move/merge overflow checks compare against remaining capacity before
addition. Insertion capacity uses Int64 accumulation and saturates the existing
Int32 plan fields at `int.MaxValue`; valid Int32 requests retain exact fit decisions.
Loot validation rejects total group weights beyond Int32 and variable ranges whose
exclusive upper bound would overflow, before consuming deterministic random state.

Item Lifecycle Showcase load prepares inventory and world stores, required container
layout and equipment projection in a detached candidate. It publishes their services
and container references together only after validation. A failed participant leaves
both live stores and the current command host unchanged. This is an in-memory
showcase restore guarantee; it does not establish durable multi-slot transactions.

Authority remains synchronous at explicit frame/command boundaries. UTC timestamps
are existing save metadata only, not instance identity, request ordering or hash input.

### Instance identity and compatibility

`InventoryStore` is the ordinary-item instance allocator owner. `TryAllocateInstanceId`
advances a saved monotonic counter; exhaustion returns false. Importing an occupied
positive id advances the counter past it. `AddStack`, `TryInsertStack` and populated
container registration reject duplicate positive instance ids within that store;
move/swap transfer the existing identity. Negative ids are invalid. World spawn
checks its world store and destination inventory store for duplicate identities and
advances the same allocator; pickup transfers through the existing atomic insertion.
Raw inventory/definition ids remain trusted authority inputs, not a client mint API.

Inventory SaveState and network snapshot are v3; inventory hash schema is v2.
The instance counter participates in hash, SaveState, full snapshot and rollback,
including allocated identities that currently occupy no slot. Acquisition rollback
retains the counter. Readers accept v2 inventory/snapshot fixtures and derive the
next counter from occupied positive ids without renumbering them. Historic deleted
identities cannot be recovered from v2 because that format had no allocator counter.
v3 counters behind occupied ids, beyond the exhausted sentinel (`int.MaxValue + 1`),
or duplicate saved identities fail before live-store publication. Existing v1
string-owner saves remain outside the accepted v2/v3 numeric-owner contract.

New v3 output and hash v2 require coordinated reader/replay rollout; older binaries
are not declared compatible with the new output. No user save is rewritten by this
library change alone. Tactical card instance identity and loadout ownership remain
with `CharacterTacticalPlayerService`, independent of this ordinary-item allocator.

物品状态按职责拆分，不存在到处流转的万能 `Item` 对象：

- `ItemDefinition`：静态定义，只回答“这是什么物品”。
- `LootItemQuantity`：瞬时掉落结果，只回答“将要产出多少”。
- `WorldItemStack`：世界状态，只回答“物品在哪里、谁能捡、何时过期”。
- `ItemStack`：库存状态，只回答“哪个容器槽位里有多少”。
- 获取事务：`ItemAcquisitionRequest` 只回答“本次权威来源要扣什么成本、发什么物品、是否已提交过”。
- 装备态：仍通过 equipment `InventoryContainer` 表达，再由 Character bridge 投影。
- 使用态：`UseItem` 当前仍是 unsupported diagnostic，不执行消耗品/Buff/Gameplay 效果。

## Definitions

核心公开类型：

- `ItemDefinitionId`：positive integer id，`Value > 0` 才有效。
- `ItemDefinition`：保存 `DefinitionId`、`StableId`、`LocalizedTextKey` 名称 / 描述、`ItemKind`、`ItemTagSet`、`ItemStackPolicy`、`ItemResourceRef[]` 和 `ItemProfileRef[]`。
- `ItemKind`：`Custom`、`Consumable`、`Equipment`、`Material`、`Currency`、`Quest`、`Container`。
- `ItemStackPolicy`：声明最大堆叠数；装备类物品允许使用默认值并归一为 `maxStackCount = 1`，其它类型必须显式提供正数，否则 definition 构造失败。
- `ItemDefinitionRegistry`：按 numeric id 和 stable id 注册，拒绝重复 id / stable id，并提供稳定 definition hash。
- `ItemProfileTypeIds`：框架内置 profile type id，例如 `CharacterEquipment` 和 `Generic`。
- `ItemResourceUsageIds`：框架内置资源用途 id，例如 `Icon`。
- `ItemRuntimeIds`：框架内置 runtime id，例如 `InventoryModule`。
- `ItemRuntimeIds.AcquisitionModule`：获取 ledger 的 module/custom state type id，wire string 为 `mxframework.items.acquisition`。

`ItemResourceRef` 只保存 `ResourceKey` 与用途标记，不保存 Unity path 或 `UnityEngine.Object`。Item Lifecycle Showcase 用 Iron Sword 验证了 `ItemResourceUsageIds.Icon` 到 `ResourceTypeIds.Sprite` 的 demo catalog 解析链路；Sprite 仍只存在于 Unity-facing view/resource manager，不进入 Items core 状态。`ItemProfileRef` 是 profile 类型和 id 的松耦合引用；V0 不解释具体 profile 内容。

分配行为：

- 定义构造、schema 创建、注册表加载和 stable hash 计算为 `LowFreqAlloc`。
- 库存 runtime 只读查询 definition registry；不要在高频 Tick 中注册定义或重算 definition hash。

## Loot

`MxFramework.Items.Loot` 只负责确定性解析掉落结果，不改库存、不生成地上物、不读取 Unity 资源：

- `LootTableId` / `LootTableDefinition`：掉落表 id、stable id 和 group 列表。
- `LootGroupDefinition`：分组抽取规则，包含 roll count、activation chance basis points、empty weight 和 entries。
- `LootEntryDefinition`：item definition id、整数 weight、count range、level 区间和 required/blocked context tags。
- `LootContext`：source stable id、source level、context tags、drop-rate multiplier、quantity multiplier。
- `LootResolver.Resolve(...)`：输入 table + context + `IDeterministicRandom`，输出稳定排序后的 `LootItemQuantity[]` 和 diagnostics。

掉落权重使用整数；概率 UI 可以在工具层显示为百分比，但运行时配置主形态保持 weight / basis points。`LootResolver` 是 `LowFreqAlloc`，用于宝箱、死亡、奖励结算等低频事件。

## Inventory Runtime

`InventoryContainer` 是固定 slot list。每个 slot 只能持有一个 `ItemStack`；`ItemStack` 保存 `ItemDefinitionId`、count、`ItemInstanceId` 和 custom state。实例分配、单个 store 内跨容器唯一占有和计数恢复已实现；耐久、绑定、词条的业务行为仍未启用。普通非实例栈可以继续使用默认实例 ID 0。

`InventoryService.TryInsertStack(...)` 使用 `InventoryInsertionPlanner` 先计算容量，再原子插入。默认策略是 `MergeThenFirstEmpty`：先合并同物品、非实例且 custom state 内容等价的栈，再按 slot 顺序拆入空槽；不同 custom state 不会合并或覆盖。容量不足返回 `InsufficientCapacity`，不产生部分修改。装备 `maxStackCount = 1` 仍按多个 slot 拆分，不会形成 count > 1 的装备栈。

写入入口优先走命令：

| Command | Id |
| --- | --- |
| `AddStack` | `1003001` |
| `RemoveStack` | `1003002` |
| `MoveStack` | `1003003` |
| `SplitStack` | `1003004` |
| `MergeStack` | `1003005` |
| `SwapSlots` | `1003006` |
| `UseItem` | `1003007` |

复杂请求通过 `InventoryRequestStore` 保存，`RuntimeCommand` 只携带 `InventoryRequestHandle` 的 index / generation。过期 handle、command id 与 request kind 不匹配、槽位越界、堆叠溢出、不同物品合并、实例物品堆叠等都会返回结构化 `InventoryOperationStatus`，不会产生部分修改。

`UseItem` 在 V0 只做诊断占位：能定位 stack，但返回 `UnsupportedUse`，不执行 Buff、Attribute、Gameplay 或资源效果，也不改变库存状态。

分配行为：

- `InventoryService` 的库存操作目标是初始化后 `NoAlloc`。
- `InventoryRuntimeModule.Tick` 在 command/event buffer 容量足够时为 `NoAlloc after initialization`。
- `InventoryRequestStore.Add` 属于请求构建路径，dictionary 增长和 request 对象创建为 `LowFreqAlloc`。

## Acquisition Transactions

`MxFramework.Items.Acquisition` 是商店购买、任务奖励、邮件领取、GM 发放等上层系统可以复用的底层事务入口。它不保存商品目录、不判断折扣/限购、不接平台支付 receipt，也不要求物品先生成 `WorldItemStack`。

核心公开类型：

- `ItemAcquisitionRequestId`：positive `long` id，用作幂等键；同一个成功 request id 再次执行会返回 `AlreadyCommitted`，不会重复发放。
- `ItemAcquisitionSourceKind`：`DirectGrant`、`ShopPurchase`、`Reward`、`Mail`、`Debug` 等来源分类，只用于诊断和上层分流。
- `ItemCostLine`：从一个 source container 中移除指定 `ItemDefinitionId` / count；V0 只消费无 `ItemInstanceId` 且无 custom state 的普通 stack，适合作为库存内 currency 或材料成本。
- `ItemGrantLine`：把指定 `ItemDefinitionId` / count 自动插入 target container，插入策略与 `InventoryService.TryInsertStack(...)` 一致。
- `InventoryTransactionService.Execute(...)`：先用库存快照 dry-run cost remove + grant insert，再 commit；成本不足、目标容量不足、定义缺失或非法请求都会在 commit 前失败，状态不变。
- `ItemAcquisitionLedger`：记录已成功提交的 request id，参与 hash / SaveState，防止存档恢复后重复领奖或重复落购买物。

本地命令入口：

| Command | Id |
| --- | --- |
| `ItemAcquisitionCommandIds.Commit` | `1003301` |

`ItemAcquisitionRuntimeModule` 使用 request-store 模式，`RuntimeCommand` 只携带 `ItemAcquisitionRequestHandle` 的 index / generation；stale handle 返回 `StaleRequest` 诊断。

`ItemAcquisitionHashContributor` 按已提交 request id 的升序贡献 hash。`ItemAcquisitionSaveStateProvider` 保存 custom state：

```text
moduleId/typeId = mxframework.items.acquisition
schemaVersion = 1
payload = committedRequestIds[]
```

分配行为：

- `InventoryTransactionService.Execute(...)` 是 `LowFreqAlloc`，会创建 dry-run working copy 和 rollback snapshot，不用于每帧 Tick。
- `ItemAcquisitionRuntimeModule.Tick` 的 command/event buffer 在容量足够时为 `NoAlloc after initialization`，但执行 acquisition request 本身仍是低频事务分配。
- acquisition SaveState JSON 是 `AllocByDesign`。

## World Items And Pickup

`MxFramework.Items.World` 表达地上可拾取物的 noEngine authority 状态，不包含 Unity trigger、Collider 或表现对象：

- `WorldItemId`：positive integer id。
- `WorldItemPosition`：纯整数位置，Showcase V0 用作逻辑格子；3D 版可由上层适配 Combat physics 查询结果。
- `WorldItemStack`：world item id、`ItemStack`、position、owner、access policy、spawn frame、expire frame。
- `WorldItemStore`：按 world item id 稳定排序保存 active world items。
- `WorldItemService`：提供 `Spawn`、`Pickup`、`Expire` 和 `ExpireDue`。
- `WorldItemRuntimeModule`：拥有 request-store command path、`RuntimeCommandBuffer` 和 `RuntimeEventQueue<WorldItemRuntimeEvent>`。

World item command ids：

| Command | Id |
| --- | --- |
| `SpawnWorldItem` | `1003101` |
| `PickupWorldItem` | `1003102` |
| `ExpireWorldItem` | `1003103` |

`PickupWorldItem` 是原子操作：只有 world item active、未过期、权限通过且目标库存 `TryInsertStack` 成功时才移除 world item。库存满、权限不符、过期或目标 container 不存在时，world item 保持不变并发出 rejected event。

## Save And Hash

`InventoryHashContributor` 先贡献下一实例计数，再按 container id 排序后贡献 container id、owner id、container stable id、slot index、item id、count、instance id 和 custom state envelope。`WorldItemHashContributor` 按 world item id 排序后贡献 active world item 的 id、stack、position、owner、access、spawn/expire frame。hash contributor 不创建 snapshot，不拼接动态 key，目标是初始化后 `NoAlloc`。

`InventorySaveStateProvider` 使用 `RuntimeCustomState` 保存模块状态：

```text
moduleId/typeId = mxframework.items.inventory
schemaVersion = 3
```

代码中应使用 `ItemRuntimeIds.InventoryModule` 访问该 id；wire string 保持 `mxframework.items.inventory`。

payload 包含 definition hash、owner、container、slot 和 stack。Restore 前会比较当前 `ItemDefinitionRegistry.ComputeStableHash()`；definition hash 不一致时返回 `CustomStateMismatch`，不写入 store。

Save / restore JSON 是 `AllocByDesign`，只用于低频存档、加载和测试 roundtrip。Inventory restore 会先在临时 store 中完整验证 container id、slot、definition、count、instance 和 stack policy，只有全部成功才替换 live store；非法 payload 返回 `InvalidDocument` 并保留原状态。

`WorldItemSaveStateProvider` 使用 `RuntimeCustomState` 保存模块状态：

```text
moduleId/typeId = mxframework.items.world
schemaVersion = 2
```

payload 包含 definition hash 和 active world item 列表；picked / expired items 只进入事件流，不长期保存。Restore 前会比较当前 `ItemDefinitionRegistry.ComputeStableHash()`；definition hash 不一致时返回 `CustomStateMismatch`，不写入 store。

## Network Sync

Items 网络同步遵守 `Catalog rich, runtime thin, network command/delta only`：

- 双方预加载同一份 `ItemDefinitionRegistry` / profile catalog；网络只用 `definitionHash` 校验一致性，不传 `ItemDefinition`、localized text、resource key、profile refs 或 definition JSON。
- 常规 gameplay 同步走 server authoritative command stream；full snapshot 只用于首次连接、late join、hash mismatch resync 或 rollback correction。
- 本地 `1003001..1003007` request-store command 仍保留，`InventoryRequestHandle` 不跨网络。
- acquisition request handle 也不跨网络。客户端只能把购买/领取意图交给上层业务协议，server 验证后构造 `ItemAcquisitionRequest` 并执行；成功后的库存变化继续通过现有 authoritative inventory command 或 full snapshot 同步。

Network-safe command id：

| Command | Id | Client 默认权限 |
| --- | --- | --- |
| `AddStack` | `1003201` | 拒绝，除非来源显式拥有 create/destroy authority。 |
| `RemoveStack` | `1003202` | 拒绝，除非来源显式拥有 create/destroy authority。 |
| `MoveStack` | `1003203` | 允许操作自己可访问的 source / destination container。 |
| `SplitStack` | `1003204` | 允许操作自己可访问的 source / destination container。 |
| `MergeStack` | `1003205` | 允许操作自己可访问的 source / destination container。 |
| `SwapSlots` | `1003206` | 允许操作自己可访问的 source / destination container。 |
| `UseItem` | `1003207` | 允许操作自己可访问的 source container；V0 仍只返回 unsupported use 诊断。 |

`InventoryNetworkCommandCodec` 把以上命令编码到 `RuntimeCommand.targetId/payload0-2`，只携带 container id、slot index、item definition id 和 count。V0 不允许 client 创建 `ItemInstanceId` 或写入 `RuntimeCustomState`；唯一实例和 custom state 只能由 server 状态或 full snapshot 下发。

`InventoryNetworkCommandAuthorizer` 适配 `NetworkSessionRunner` 的 command authorizer hook。默认 `InventoryOwnerNetworkAuthority` 用 `peerId -> InventoryOwnerId` 映射判断 container ownership，跨 owner/container move 默认拒绝；`AddStack` / `RemoveStack` 需要额外授予 stack create/destroy authority。

`InventoryNetworkSnapshotParticipant` 输出 `RuntimeCustomState`：

```text
typeId = mxframework.items.inventory
schemaVersion = 3
payload = definitionHash + nextItemInstanceId + container/owner/slot/stack/custom state envelope
```

payload 不包含 SaveState wrapper，也不包含 catalog 内容。Apply 前比较 definition hash；不匹配时结构化失败且不写入 store。

`InventoryRollbackParticipant` V1 不走 JSON，也不复用 full snapshot DTO 做每帧 capture。它维护固定容量的 `InventoryRollbackState` ring；每个 state 内部用预分配 struct 数组保存 container entry 和非空 slot entry。组合根应把 `historyCapacity` 配成不小于 `NetworkPredictionRollbackCoordinator` 的 rollback history capacity，否则超过 ring 容量后较旧历史会被复用覆盖。participant 构造时缓存 definition hash，因此 definition registry 必须在 participant 创建前完成加载，并在 rollback session 期间保持不变。固定 container/slot 布局下，rollback capture 目标是初始化后 `NoAlloc`；restore 仍通过 public inventory runtime API 重建 container，属于 correction 低频 `AllocByDesign` 路径。

分配行为：

- compact command encode/decode/authorization/execute 目标是 `NoAlloc after initialization`。
- network full snapshot JSON capture/apply 是 `AllocByDesign`，只用于 bootstrap/resync。
- rollback state capture 在容量和库存布局固定后目标是 `NoAlloc after initialization`；容量扩张是 `LowFreqAlloc`，restore 是 `AllocByDesign` correction 路径。

## Character Equipment Bridge

`CharacterItemEquipmentProfileConfig` 以 `ItemDefinitionId` 为根，保存生成 legacy `WeaponConfig` 所需字段：兼容 weapon id、category、tags、occupies slots、ability loadout、presentation/combat/resource profile、resource keys、trace bindings 和 base modifiers。

装备 slot 和常用 weapon tag 通过 Character.Application 公共常量访问：

- `CharacterEquipmentSlotIds.MainHand` / `OffHand` / `TwoHand` / `Head` / `Body` / `Back` / `Accessory` / `NaturalWeapon`
- `CharacterWeaponTagIds.Blade` / `OneHand` / `Shield` / `Guard`
- `EquipmentMatchRuleToken.Create(slotId, token)` 用于生成 resolver 仍然消费的 `"slot:token"` match rule 字符串。

装备本身用一个特殊 `InventoryContainer` 表达，slot index 对齐 `EquipmentSchemaConfig.Slots` 顺序，slot id 仍来自 Character.Application schema。背包装备物品就是把 stack 从背包 container move 到装备 container。

`CharacterItemEquipmentProjector.Project(...)` 会从 equipment container + item registry + profile registry 生成：

- `WeaponConfig[]`
- `EquipmentLoadoutConfig`
- `CharacterDiagnostic[]`

成功结果可直接交给现有 `EquipmentStateResolver.Resolve(...)`。现有手写 `WeaponConfig` / `EquipmentLoadoutConfig` 仍兼容，V0 不迁移或删除旧路径。

桥接失败条件包括缺 item definition、缺 equipment profile、装备 stack count 不是 1、slot count 与 schema 不匹配、slot category/tag 不兼容。Projection 是 `LowFreqAlloc`，用于 loadout rebuild，不放在每帧 Tick。

## Tests

Focused tests 位于 `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Items/`：

- `ItemDefinitionTests`
- `LootRuntimeTests`
- `InventoryRuntimeTests`
- `ItemAcquisitionTests`
- `CharacterTacticalItemTransactionTests` (same source file; player-owner and isolated filesystem fixtures)
- `WorldItemRuntimeTests`
- `InventoryNetworkTests`
- `ItemEquipmentBridgeTests`
- `ItemLifecycleShowcaseTests`

测试覆盖 definition schema / duplicate / hash / default stack policy、Loot 分组抽取 / 条件 / invalid config、库存 add/remove/move/split/merge/swap/insert、custom-state merge compatibility、失败 atomic、request handle stale generation、`UseItem` 诊断、acquisition 直接发放 / cost+grant / 幂等 ledger / SaveState / stale command、world item spawn / pickup / expire、SaveState JSON roundtrip、definition hash mismatch、非法 Inventory payload 原子拒绝、network-safe command encode/decode/execute、owner authorizer、snapshot roundtrip、rollback state ring 复用 / 历史恢复 / warm-path NoAlloc，以及 item-backed equipment projection 到 `EquipmentStateResolver` 和 Item Lifecycle Showcase 的 command path / Iron Sword icon `ResourceKey` 流转 / `ResourceManager.Load<Sprite>` 路径。
