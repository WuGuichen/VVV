# Mx 关卡编辑器第二阶段：定义驱动玩法闭环开发指南

> Status: Current
>
> Implementation state: P2.0-P2.5 implemented; P2.5 final Playable boundary V4 validated; P3.0 common authoring references implemented and V4 accepted independently
>
> Parent design: [定义驱动玩法闭环设计](../LEVEL_EDITOR_DEFINITION_DRIVEN_GAMEPLAY_DESIGN.md)
>
> Last verified: 2026-07-28

## 1. 文档用途

本文是第二阶段的实施记录和验收台账。实现过程中必须持续更新，不能等功能完成后再凭记忆补写。

P3.0 不计入 P2.5 完成度。目标、效果归因和目录引用的独立实现记录见 [P3.0 通用创作引用开发指南](LEVEL_EDITOR_COMMON_AUTHORING_REFERENCES_DEVELOPMENT.md)。

本文负责记录：

- API 复用计划。
- 计划修改的程序集与文件职责。
- schema、迁移和兼容策略。
- 可独立提交的开发增量。
- 每个增量的最低测试与人工验收。
- 实际提交、验证结果、已知限制和后续边界。

产品目标、交互原则、权威分层和非目标以父设计为准。若实现发现父设计与当前源码冲突，应先更新父设计并说明证据，再改变代码方向。

## 2. 开发合约

### 2.1 交付等级

第二阶段最终交付等级为 `Playable`：

- CharacterTest 场景中存在明确可操作入口。
- 用户能通过 Level Editor 创建定义、放置实例、配置区域 Trigger。
- Play Mode 中 Trigger 由 Combat Sensor 驱动并产生可见或可诊断结果。

中间增量可以标记为 `Framework Feature` 或 `Runtime Slice`，但不得在完整 Scene/Runtime 闭环完成前声称第二阶段 Playable。

### 2.2 高频路径声明

计划中的 UI、JSON、Catalog 查询、Compiler、ResourceKey 解析、Scene 投影和保存均为 Editor/初始化低频路径，允许按现有 `AllocByDesign` 或 `LowFreqAlloc` 契约分配。

以下属于高频路径：

- `GameplayTriggerCombatSensorSystem.Tick`。
- Trigger evaluation/action dispatch。
- CharacterTest/Gameplay/Combat fixed Tick。
- Runtime diagnostics source 的采样入口。

实现不得在这些路径新增：

- JSON 解析。
- AssetDatabase、Transform、GameObject 或 Unity scene 查找。
- LINQ、临时集合、反射或字符串拼接。
- 每帧 UI view model 或 Scene Gizmo 文本构建。

新增 runtime 数据必须在 session bootstrap 时编译、排序、冻结并预分配容量。

### 2.3 现有工作保护

- 保留当前 Level Editor 的放置弹层、右键菜单、保存/放弃、Undo/Redo 和未登记检测行为。
- 保留现有 `mx.level.authoring.v3` worldObjects/characters payload。
- 不修改未知 module payload。
- 不批量替换当前 Scene GameObject。
- 不删除 built-in definition fallback，直到迁移等价测试与 Player 路径通过。
- 不手写 `.unity`、Prefab 或 `.asset` YAML；需要资产变更时使用 Unity Editor、Unity MCP 或现有 Editor 菜单。
- P2.3 只拆验收，不拆出新的窗口、程序集、模板插件系统或保存框架。
- P2.3 不修改 Trigger Runtime、Combat Physics 或现有动作桥；Runtime 组合统一留到 P2.4。

## 3. API 复用计划

编码前和每次改变范围时复核此表：

| 需求 | 复用模块/API | 使用方式 | 不新增什么 |
|---|---|---|---|
| Level Editor 模块和窗口 | `IMxLevelEditorModule`、structured/placement/scene/save contracts | definition workspace 保持现状；Trigger 编辑并入 World Object module | 不创建第二个窗口、第三个顶层模块或独立保存入口 |
| Level 文档 | `MxLevelAuthoringDocument` / module payload | 添加 `characterTest.gameplayTriggers` payload | 不建立平行关卡主文档 |
| 定义引用校验 | `ILevelAuthoringDefinitionProvider` | 保留按 ID 解析，Editor 聚合列表 | 不把 window list 当 Catalog |
| 配置 | Mx Config、schema/reference validation | 定义 JSON、Trigger wrapper、结构化 diagnostics | 不把规则数据只放 Inspector |
| 资源 | `ResourceKey`、Resource Catalog、Editor validator、`IResourceManager` | Prefab/缩略图解析和 Player runtime binding | 不保存 AssetDatabase path 或 `Resources.Load` |
| 世界对象 runtime | `CharacterTestWorldObjectDefinitionCatalog` | Authoring compiler 仍生成现有 runtime definition | 不创建第二套 world object runtime |
| 角色 | Character.Application / RuntimeSpawn | 枚举已发布角色并只读展示 | 不在 Level Editor 创作角色包 |
| 物理 | `CombatPhysicsWorld`、Combat shape/material/query | Definition 物理 facet 与 Region AABB descriptor | 不用 Unity Physics 作权威 |
| Trigger Config | `GameplayTriggerAuthoringDocument`、`GameplayTriggerConfigCompiler` | canonical serializer + CharacterTest 薄草稿投影 + compile | 不在 Editor 重写条件/策略编译，不建立通用 Trigger 编辑框架 |
| Trigger 感知 | `GameplayTriggerCombatSensorCatalog/System` | 由 Region 生成 fixed AABB descriptor | 不使用 `OnTriggerEnter` |
| Trigger 动作 | Gameplay Buff bridge、Story bridge | 首版 typed action templates | 不支持任意 Unity callback |
| 生命周期 | `RuntimeHost`、Gameplay system ordering | CharacterTest session composition | 不新增 Update 驱动 runtime |
| SaveState/Replay/Hash | Trigger/Runtime 现有 contracts | 注册既有 participant 和 hash | 不保存 GameObject runtime snapshot |
| 诊断 | Trigger/Combat typed result、Diagnostics/DebugUI | Editor Play Monitor 低频映射 | 不在 Tick 拼中文字符串 |
| Editor UI | UI Toolkit、现有 Level Editor USS/token | 虚拟化列表、临时工作区、Inspector 能力卡 | 不引入 player-facing UI 技术 |

## 4. 计划程序集与文件职责

下面是计划职责，不要求机械地使用相同文件名。实际拆分变化必须在本节更新，不能把所有逻辑继续塞入现有大文件。

### 4.1 CharacterTest definition Authoring

计划位置：

```text
Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/World/Definitions/
```

职责建议：

| 文件职责 | 内容 |
|---|---|
| Authoring contracts | schema identity、mutable JSON DTO、presentation binding DTO |
| JSON | strict load/save、canonical settings、错误路径 |
| Validator | ID、stable ID、typed facet、ResourceKey、状态校验 |
| Compiler | Authoring DTO -> `CharacterTestWorldObjectDefinitionCatalog` + presentation catalog |
| Runtime catalog loader | generated/StreamingAssets runtime catalog 加载与 hash 复验 |
| Migration/export | built-in catalog -> 首份 Authoring JSON 的确定性转换 |

首版可继续放在 `MxFramework.Game.CharacterTest` assembly，因为 runtime definition 已在该程序集。类本身不得依赖 `UnityEditor`；若未来出现第二个游戏消费者，再评估提取 noEngine sibling assembly，不在本阶段提前搬迁全部类型。

### 4.2 Level Editor 内容投影

计划位置：

```text
Packages/com.vincent.wgameframework/Editor/Levels/
Assets/Scripts/WGameFrameworkProject/CharacterTest/Editor/
```

职责建议：

| 层 | 内容 |
|---|---|
| Editor shell control | 搜索、分类、virtualized result list、selection contract |
| CharacterTest provider adapter | 世界对象 writable entries、点位/区域、definition diagnostics |
| Character provider adapter | Character.Application / RuntimeSpawn read-only entries |
| Definition workspace | 新建、编辑、停用、dirty/undo；实例复制统一走 Scene 右键 |
| Presentation resolver | ResourceKey -> Prefab/thumbnail；只在 Editor |
| Placement adapter | Prefab/explicit primitive preview 与创建 |

实施顺序要求：

1. 先在 CharacterTest 模块中验证 view model 与 provider。
2. 世界对象和角色两个 provider 都实际消费相同 UI 后，才把控件提取到 `MxFramework.Editor.Levels`。
3. 不因预想未来模块而先设计庞大 public SDK。

### 4.3 Gameplay Trigger domain 持久化

计划位置：

```text
Packages/com.vincent.wgameframework/Gameplay.Triggers.Config/
Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Gameplay/Triggers/
```

职责：

- `GameplayTriggerAuthoringDocument` canonical JSON round-trip。
- strict schema、unknown/duplicate/missing/null 字段诊断。
- immutable authoring array 的无损排序/语义顺序保留。
- public 低频 API 的分配标注。
- serializer 只负责现有 domain DTO，不承载 CharacterTest 区域引用、显示名称或界面状态。

计划新增跨程序集 public API 时：

- 必须标为 `AllocByDesign`。
- 必须补 XML 注释。
- 必须更新 `Docs/Interfaces/Gameplay.Triggers.md`。
- 不改变 Runtime wire、catalog hash、SaveState 或 replay schema，除非另开设计。
- 除 canonical serializer 的必要入口外，不为 P2.3 增加 builder、provider、repository 或 template registry 公共 API。

### 4.4 CharacterTest Level Trigger integration

计划位置：

```text
P2.3:
Packages/com.vincent.wgameframework/Gameplay.Triggers.Config/
Assets/Scripts/WGameFrameworkProject/CharacterTest/Scene/LevelTriggers/
Assets/Scripts/WGameFrameworkProject/CharacterTest/Editor/

P2.4:
Assets/Scripts/WGameFrameworkProject/CharacterTest/Composition/
```

职责：

| 层 | 内容 |
|---|---|
| Level wrapper | `characterTest.gameplayTriggers` payload、region binding、ID state |
| Adapter/validator | envelope read/write、worldObjects region cross-reference、candidate compile |
| Editor integration | 现有 World Object Inspector 中的 Trigger card、三个固定模板、Gameplay Buff 生成目录选择与手动 ID 后备、只读诊断、选中区域 AABB |
| P2.4 scene cache | canonical Trigger source、fixed sensor descriptor、hash |
| P2.4 runtime composition | Trigger runtime、Combat sensor、action bridge、SaveState/checkpoint、hash 与 replay-compatible identity |
| Tests | P2.3 只覆盖 authoring、reference、save/undo 和 UI；cache/runtime integration 留给 P2.4 |

P2.3 优先放入现有 CharacterTest 与 CharacterTest.Editor 程序集，不创建新 asmdef。只有代码实际形成两个以上独立职责时才按现有目录增加文件；不得仅为计划中的 P2.4 预建空目录、接口或适配层。P2.3 只增加 Trigger Config/Core 与首版模板确实需要的 bridge 引用，不提前接入 Combat Sensor Runtime。不得让 Trigger core 反向引用 CharacterTest。

P2.3 不注册第三个顶层 Level Editor module，也不增加独立保存按钮。Trigger 的领域 payload 仍然独立，但编辑体验、dirty 状态、Undo snapshot 和保存入口并入现有 CharacterTest World Object module，因为 Trigger card 只依附当前选中的 Region。预计首先只需要以下职责文件；文件尚未增长前不继续拆接口：

| 文件职责 | 建议落点 |
|---|---|
| Domain DTO canonical JSON | `Gameplay.Triggers.Config/GameplayTriggerAuthoringJson.cs` |
| CharacterTest wrapper、normalize、ID state | `Game/CharacterTest/Scene/LevelTriggers/CharacterTestLevelGameplayTriggerAuthoring.cs` |
| Envelope adapter、reference validation、candidate compile | `Game/CharacterTest/Scene/LevelTriggers/CharacterTestLevelGameplayTriggerAuthoringAdapter.cs` |
| Trigger draft、Inspector card、SceneView AABB 与现有保存/Undo 接线 | `Game/CharacterTest.Editor/CharacterTestWorldObjectLevelEditorModule.Triggers.cs` |
| 数据与界面 focused tests | 复用现有 Trigger/CharacterTest tests 目录 |

## 5. Schema 与文件计划

### 5.1 世界对象定义 Authoring

计划 Authoring 文件：

```text
Assets/Config/MxFramework/Levels/CharacterTest/world_object_definitions.json
```

计划 identity：

```text
format: mx.characterTest.worldObjectDefinitions.authoring.v1
schemaVersion: 1
```

目标结构：

```json
{
  "format": "mx.characterTest.worldObjectDefinitions.authoring.v1",
  "schemaVersion": 1,
  "definitions": [
    {
      "enabled": true,
      "definitionId": 101,
      "stableId": "movable.natural.slick_light",
      "displayName": "Slick Light",
      "objectKind": "MovableNaturalObject",
      "category": "natural",
      "presentation": {
        "kind": "PrefabResource",
        "resourceKey": "project:model:level.movable.slick_light:"
      },
      "material": {},
      "shape": {},
      "potency": {},
      "movableNaturalObject": {},
      "staticCollider": {},
      "point": {},
      "region": {}
    }
  ]
}
```

上例只冻结顶层结构和职责；facet 字段必须逐项映射当前 typed definition，不得凭 UI 文案重新命名或丢失 raw fixed-point 数据。正式 schema 在 P2.1 编码前由当前定义类型和迁移等价测试反向确认，并先更新本节。

P2.1 冻结的正式字段如下：

- `enabled`、`deprecated`、`definitionId`、`stableId`、`displayName`、`objectKind`、`category`。
- `presentation.kind` 为 `PrefabResource` 或 `PrimitiveAuthoring`。
- `presentation.resourceKey` 使用结构化 `id`、`typeId`、`variant`、`packageId`，不保存 Asset path；Prefab 的 `typeId` 必须为 `GameObject`。
- `presentation.primitiveKind` 为 `Cube`、`Sphere` 或 `Capsule`，且只在 `PrimitiveAuthoring` 时有效。
- `material` 保存 `kind`、`frictionRaw`、`elasticityRaw`、`fixedBounceHeightRaw`。
- `shape` 保存 `kind`、`halfExtentXMillimeters`、`halfExtentYMillimeters`、`halfExtentZMillimeters`。
- `potency` 保存 `baseMotion`、`baseCohesion`、`baseScatter`，以及 profile 的七个开关和五个阈值。
- `movableNaturalObject` 保存 `enabled`、`massGrams`；`staticCollider` 保存 `enabled`、`combatLayer`、`colliderId`。
- `point` 保存 `enabled`、`pointKind`、`characterId`、`teamId`、`controlOwner`；`region` 保存 `enabled`、`regionKind`。
- `description`、`tags` 是 Authoring 搜索元数据；不进入 gameplay definition equality，但进入 Authoring source hash。
- JSON 使用 strict unknown-member rejection。Compiler 只把 `enabled && !deprecated` 的条目放入 Runtime Catalog，并按 numeric ID、stable ID 确定性排序。

计划 runtime 输出：

```text
Assets/MxFrameworkGenerated/Levels/CharacterTest/world_object_definitions.runtime.json
Assets/StreamingAssets/MxFramework/CharacterTest/RuntimeCatalogs/Levels/world_object_definitions.runtime.json
```

Player mirror 是否同时需要 binary companion，必须先检查当前 CharacterTest runtime catalog loader 的正式要求；没有启动或性能证据时不新增格式。

### 5.2 Level Trigger module

计划 module：

```text
moduleId: characterTest.gameplayTriggers
format: mx.characterTest.levelGameplayTriggers.v1
schemaVersion: 1
```

目标 payload：

```json
{
  "format": "mx.characterTest.levelGameplayTriggers.v1",
  "schemaVersion": 1,
  "idState": {
    "nextSourceDefinitionId": 1,
    "nextConditionId": 1,
    "nextDefinitionId": 1,
    "nextRouteId": 1,
    "nextBindingId": 1,
    "nextSourceBindingId": 1,
    "retiredIds": []
  },
  "authoring": {
    "schemaId": "mx.gameplayTriggers.authoring.v1",
    "schemaVersion": 1,
    "sources": [],
    "conditions": [],
    "definitions": [],
    "bindings": []
  },
  "entries": [],
  "regionBindings": []
}
```

职责：

- `authoring`由 `Gameplay.Triggers.Config` 的 canonical serializer 拥有。
- `entries`保存用户可读 stable ID、显示名、template ID 与关联 typed IDs。
- `regionBindings`保存 source-binding ID、region instance stable ID、provider、layer/capacity。
- 几何、Transform、Unity object identity 不进入 payload。
- `entries`与 `regionBindings`只是 CharacterTest 包装元数据，不复制 condition、policy 或 action 语义；Compiler 只读取 `authoring`。
- `regionBindings`不作为独立可编辑内容展示，只由选中区域的 Trigger card 创建和维护。
- `idState`是 wrapper 内的普通可序列化 DTO，不新增通用 allocator service。分配只执行单调递增，不扫描空洞。
- `retiredIds`直接保存现有 `GameplayTriggerRetiredIdDescriptor` 的 `idKind/primaryId/secondaryId` 语义；Route 使用 definition ID 作为 primary、route ID 作为 secondary。
- level 只拥有 SourceDefinition、Condition、Definition、Route、Binding 与 SourceBinding 的 ID。ActionKind 和 Provider 来自可信领域注册表，不能由关卡分配或 retired。
- 删除后 typed ID 进入 `retiredIds`，同一关卡内不复用；Undo/Redo 必须连同整个 `idState` 一起恢复。

### 5.3 Trigger scene cache

本节属于 P2.4。P2.3 不创建空 cache 类型、Scene 组件或预留序列化字段。

P2.4 当前 cache：

```text
CharacterTestGameplayTriggerSceneCache
```

最小字段：

- cache、level 与 module schema。
- Authoring source/content hash 与 Trigger entry count。
- canonical Trigger envelope JSON。
- fixed Combat AABB sensor descriptor rows。
- Trigger/sensor/combined catalog hash。
- last applied version/time。

cache 是 Unity Scene 派生产物，不作为手工配置 API。任何 Inspector 都只读显示其状态；session bootstrap 必须从 cache 重建并复验，不把序列化 DTO 直接当成可信 Runtime。

## 6. 增量与提交边界

### P2.0 基线与迁移护栏

目标：在改变定义来源前冻结当前可观察行为。

修改：

- 为 built-in definition catalog 建立逐字段 snapshot/equality fixture。
- 记录全部现有 definition ID/stable ID/kind/facets。
- 为当前 26 个 level rows 建立 definition resolution 回归。
- 确认至少一个可通过正式 Resource Catalog 解析的世界对象 Prefab；若没有，记录资源缺口，不伪造 Asset path。
- 为当前放置 primitive、真实 duplicate preview、保存/关闭恢复保留 focused tests。

最低验收：

- 当前 built-in catalog、现有关卡 JSON 和 Scene cache 结果未变化。
- 没有新增 public API/schema。
- 只提交测试护栏和必要文档。

P2.0 冻结基线（2026-07-26）：

- built-in catalog 共 20 个定义：
  - 可移动物体 7 个：`101`、`102`、`103`、`104`、`105`、`110`、`120`。
  - 静态碰撞体 8 个：`1001`、`1002`、`1011`、`1012`、`1013`、`1014`、`1021`、`1022`。
  - 关卡点 3 个：`2001`、`2002`、`2003`。
  - 区域 2 个：`3001`、`3002`。
- `character_test_scene.world_objects.json` 的 `characterTest.worldObjects` 模块共 26 条登记：可移动物体 7、单个静态碰撞体 9、静态批次 3、关卡点 6、区域 1。
- `CharacterTestScene` 当前 Scene cache 展开结果保持为：可移动物体 7、静态碰撞体 32、关卡点 6、区域 1；P2.0 不重写 JSON、Scene 或 cache hash。
- `CharacterTestWorldObjectDefinitionBaselineTests` 冻结定义的 numeric ID、stable ID、display name、kind、category，以及 material、shape、potency、movable、static、point、region 的全部当前字段；同时检查 26 条登记的定义存在性与 usage。
- 继续保留 `LevelEditorModule_PlacementPreviewUsesRoleDefinitionShape`、`LevelEditorModule_DuplicateAndRepositionShareOneStepUndoRedo`、`LevelEditorWindow_DestroyFallbackRemovesUnsavedDuplicatedWorldObject`、`MxLevelEditorSaveCoordinatorTests` 和 `OfficialCharacterTestScene_WorldObjectJsonMatchesGameManagerCache` 作为放置、真实复制预览、撤销、保存/关闭恢复和 Scene cache 护栏。
- 当前正式 Resource Catalog 中没有可认定为关卡世界对象的 Prefab。这是 P2.2 的资源缺口；不得用武器、VFX 或手写 Asset path 伪造通过。
- 本增量只新增 Editor 测试和文档，不新增 public API，不修改 schema、Runtime、关卡 JSON、Scene 或 Prefab。

### P2.1 数据化世界对象定义

目标：Authoring JSON 能无损生成当前 runtime catalog。

修改：

- Authoring DTO、JSON、validator、compiler。
- built-in export/migration。
- generated runtime catalog 与 Player mirror loader。
- presentation binding projection。
- fallback 与 fail-closed diagnostics。

最低验收：

- 导出的 Authoring JSON 重新编译后，与 built-in definitions 逐字段等价。
- duplicate numeric/stable ID、非法 shape/material/facet、未知字段和坏 ResourceKey 被结构化拒绝。
- canonical JSON round-trip 与 hash 稳定。
- Player/Editor loader 对同一内容生成相同 runtime catalog/hash。
- 现有 CharacterTest session 行为不变。

提交边界：不包含 UI 重做和 Trigger。

### P2.2 统一内容选择与真实资源放置

目标：无需修改代码即可新建、编辑定义并创建实际 Prefab 场景实例。

修改：

- Editor-only content projection。
- 搜索/分类/虚拟化内容选择器。
- definition management workspace。
- ResourceKey Prefab/thumbnail resolver。
- Prefab placement/preview 与 explicit primitive path。
- definition save participant、Undo/Redo、关闭恢复。
- Character.Application / RuntimeSpawn 角色 read-only provider。

最低验收：

- 新建 definition、修改名称/ResourceKey/typed facets、保存、重开均一致；场景实例复制继续使用 Scene 右键“复制并放置”。
- 普通 Prefab 定义缺资源时阻止放置；不静默创建 primitive。
- 有效 Prefab 定义创建真实 Prefab instance、保留关联并登记到 level document。
- duplicate/reposition/delete/save/undo 现有行为不回归。
- 角色列表不再只依赖单个硬编码 ID；只显示已发布、可 spawn 或带明确诊断的定义。
- definition 表现变更不会自动破坏已有 Scene object。

提交边界：不包含 Trigger Runtime。

### P2.3 Trigger 持久化与 Level Editor Authoring

目标：Trigger Region 配置可以进入 level envelope、Undo/保存和 Compiler。

实施原则：拆分的是验收点，不是架构。P2.3 只增加一份 module payload、一个薄 adapter/validator、现有 Inspector 的一张卡片和必要测试；不创建独立 Trigger 工作台、通用草稿仓库或模板插件系统。

#### P2.3a 最小数据闭环

实现状态（2026-07-26）：已实现。当前交付 strict canonical domain JSON、`characterTest.gameplayTriggers` payload、wrapper/reference/ID/capacity/target 校验、可信 Gameplay/Story action requirements、正式 Config Compiler candidate compile、现有 World Object participant 的统一保存/Undo/关闭恢复，以及 region 删除/移除登记保护。空 payload 合法且不伪造 runtime catalog；尚未提供 Trigger 配置卡或运行时执行。

范围：

- 为现有 `GameplayTriggerAuthoringDocument` 增加 strict canonical JSON round-trip。
- 增加 CharacterTest Trigger module payload，以及直接读写该 payload 的 adapter/validator。
- ID 分配和 retired state 作为 payload 内部状态实现，不抽成跨模块服务。
- 接入现有 `MxLevelEditorSaveCoordinator`、文档事务快照和关闭恢复；不增加第二套保存或 Undo 系统。
- 保存前校验 region cross-reference，并调用正式 `GameplayTriggerConfigCompiler`。
- 被 Trigger 引用的 region 阻止删除；删除 Trigger 只移除 Trigger 数据，不删除 region。

最低验收：

- 空 payload、新建一条 Trigger、删除后 retired、保存重开均 canonical round-trip。
- Undo/Redo 一次恢复完整 authoring、包装元数据和 `idState`。
- 未知 module payload 保持不变。
- 缺失 region、重复 stable/typed ID、未知 action、非法 target/capacity 阻止保存且不产生半写入。
- candidate compile 与正式 `GameplayTriggerConfigCompiler` 结果一致。

提交边界：只交付数据、事务、引用保护和 focused tests；没有 Trigger 配置 UI，也不声明可运行。

#### P2.3b 最小使用闭环

实现状态（2026-07-27）：已实现。现有 World Object Inspector 只在选中且启用的 `TriggerRegion` 上显示渐进式玩法触发器编辑区；三种固定模板直接构造正式 `GameplayTriggerAuthoringDocument`，没有模板注册表、独立工作台或第二套运行时。对象区已收成`选择 / 定位 / 重新放置 / 更多`一行；Trigger 区按`行为摘要 -> 触发条件 -> 作用目标 -> 执行动作`排列，普通视图优先显示启用状态、state scope/presence 策略和 Story/Buff 领域选择。新建 Buff Trigger 使用未选择状态，必须从已有 Gameplay Buff 目录明确选择；目录外手动 ID 只作为旧数据迁移后备。cooldown 与 max activations 收进`高级规则`，先选择`无冷却 / 自定义`和`不限 / 1 次 / 自定义`，只有自定义模式显示原始正整数。typed ID 与 Compiler 细节继续折叠到只读技术信息。时机、目标 selector 和动作种类仍由固定模板决定；当前 Combat Sensor 不支持的关系筛选不会在 UI 中伪装成可编辑字段。新增、启用/策略/payload 修改、`更多 > 复制`和删除分别绑定一个文档 Undo 事务；payload 编辑与复制会保留现有启用状态和 route policy。SceneView 为 AABB 区域绘制低透明度实体面，选中区域可直接拖动六个面手柄调整范围，拖动中的候选实体面实时跟随，通过正式 Compiler preflight 的 Trigger Region 追加 compiled candidate AABB。范围调整继续在鼠标松开时一次性写入现有 Scene Transform/scale source，并与文档快照绑定为一个 Undo；非法旋转、缺失 Transform、容量和 Compiler 错误进入卡片内诊断并阻止保存。P2.3 仍不生成 Runtime cache，也不执行 Trigger。

范围：

- 只在现有 World Object Inspector 选中 `TriggerRegion` 时显示一张 Trigger card。
- 普通视图以摘要和三组表单显示实际行为；策略和领域 payload 可编辑，固定模板语义只读；cooldown、activation 上限与手动 ID 后备集中在`高级规则`，typed 技术信息单独折叠。
- 模板使用 CharacterTest 内部的三个固定映射函数直接生成 domain authoring rows，不建立 template registry/provider。
- SceneView 为 AABB region 绘制低透明度实体面；选中项显示六个面手柄并直接修改现有 Scene Transform，合法 Trigger Region 额外绘制编译 AABB；不增加节点连线或常驻 overlay。
- 添加和删除使用卡片操作；若提供复制，放在卡片 `更多` 上下文菜单，不增加常驻“复制”按钮。

首版模板：

```text
region.story.raise_trigger.enter_once.v1
region.gameplay.apply_buff.enter_once.v1
region.gameplay.apply_remove_buff.presence.v1
```

最低验收：

- 三个模板分别生成与预期完全一致的 source、condition、definition、route、binding rows。
- 添加、修改、上下文复制和删除 Trigger entry 均保持稳定 typed IDs；取消操作不修改文档。
- 修改启用状态和 route policy 均占用一个 Undo；之后修改 payload 或复制 entry 不得恢复模板默认 policy。
- Inspector 只在合法 `TriggerRegion` 上出现，切换选择后不残留错误草稿。
- 360px Inspector 中对象操作、摘要、策略、Buff、来源与折叠区不横向越界；新建 Buff 显示明确的未选择状态，冷却和次数的自定义输入只在`高级规则`对应模式下出现；不显示无 Runtime 支持的关系筛选。
- 非单位旋转 AABB、缺失 region 和编译错误以就地诊断显示并阻止保存。
- 选中区域 AABB 与 candidate descriptor 一致；未选中区域只保留普通低透明度范围面，不绘制 compiled candidate 或调整手柄。
- 六面手柄调整中心/尺寸只产生一个 Undo；`Esc` 取消无修改，定义尺寸首次调整显式切换为 Scene Transform 尺寸。
- 保存、重开、Undo/Redo 和关闭时放弃修改保持 P2.3a 行为。

提交边界：P2.3 完成后仍只标记为 `Framework Feature`；不执行 Trigger、不接 RuntimeHost，也不声明 Playable。

UI 收紧验证（2026-07-27，2026-07-28 补充）：新建 Buff Trigger 的未选择状态会触发正式 `CT_TRIGGER_TEMPLATE_PAYLOAD_INVALID`，从生成目录选择 Buff 后清除；普通视图不再显示原始 cooldown/max 数值，`高级规则`的语义模式切换、折叠状态保留、policy 更新与 Undo/payload 保留通过 focused EditMode。Trigger 字段刷新会保存外层 Inspector `ScrollView` 的当前位置，并用刷新 revision 阻止旧延迟回调覆盖后续选择；切换对象或 Inspector 页签不沿用旧位置。2026-07-28 补充验证为 `MxFramework.Tests.csproj` 编译 0 error、scroll/policy/payload/Undo focused EditMode 3/3、清理 Test Runner 结果提示后 Unity Console 0 error，4 个相关一方 C# 文件 standards report 无 finding。

### P2.4 CharacterTest Runtime 组合与诊断

目标：level-authored Trigger 在 CharacterTest 正式 RuntimeHost 中执行。

实现状态（2026-07-27，2026-08-03 更新保存语义）：已实现为 CharacterTest offline session 的 `Runtime Slice`。统一`保存`会把 canonical Trigger payload、fixed Combat AABB sensor rows、三类 catalog hash 与版本信息写入 `GameManager.GameplayTriggerSceneCache`，并与 Authoring、受管 Scene 一起提交；session bootstrap 只从该缓存重建并复验正式 Trigger plan、Combat sensor catalog、动作 bootstrap、SaveState participant 与 result hash contributor。缓存 schema/content/hash、source binding 或动作组合不一致时整份 fail-closed，不回落 Unity Physics，也不发布半套 Runtime。当前场景区域无需 Unity Collider。

运行时继续使用现有 `GameplayTriggerRuntime`、`GameplayTriggerCombatSensorSystem`、Gameplay Buff same-frame bridge 和 Story reliable bridge。属性区的`运行时检查`以 5 Hz 低频读取 typed status/result，显示实际 Combat AABB、sensor 命中/映射、Entered/Stayed/Exited 和动作结果；它不从 Scene Collider 或标签反推状态。Gameplay/Trigger/Story 分别以 `100/200/300` 的稳定顺序加入 checkpoint，恢复后清除非权威瞬态队列并重采样，避免重复 Entered；result hash 合并 Gameplay、Trigger、Story 与关卡 Trigger catalog identity。

当前边界：只接入 CharacterTest offline `GameSlice`；网络会话与运行中热重载仍属于后续迁移。P2.5 已在 CharacterTest 正式场景交付并验证示例 Trigger。修改 Authoring 或区域 Transform 后只需点击一次`保存`，随后重建 Play session。

修改：

- Region -> fixed `CombatAabbQuery` scene cache projection。
- Trigger scene cache 与 hash。
- CharacterTest session bootstrap。
- Combat Sensor、Trigger evaluation、Gameplay/Story action bridge。
- Trigger SaveState/Replay/Hash participant 注册。
- Editor Play Monitor typed diagnostics。

最低验收：

- 没有 Unity Collider 的 Trigger Region 仍能通过 Combat Sensor 产生 Entered/Stayed/Exited。
- Trigger action 只经现有 sink/transfer 执行。
- Runtime cache hash 错误、action bootstrap 不完整、unknown source binding 均 fail-closed。
- SaveState restore 不重复 Entered；Replay 产生相同 result hash。
- Tick 不读取 JSON/Scene/AssetDatabase，focused allocation test不新增 steady-state 分配。
- Editor 显示实际 compiled query、sensor status、occurrence 和 action result。

提交边界：Runtime 组合、测试和诊断同一可验收提交；不夹带其他 Level Editor 功能。

### P2.5 可玩验收与收口

目标：完整第二阶段闭环。

实现状态（2026-07-28）：已在 `CharacterTestScene` 的 `region.start` 写入正式 `区域内维持 Buff` 示例。Authoring stable ID 为 `trigger.region.start.1`，目标为区域 Subject，Buff 为 `601012 · Poisoned`，效果归因为 `501012 · 毒瓶池`。Apply route 监听 `Entered | Stayed` 并以 1 帧间隔刷新既有 Buff，Exit route 监听 `Exited` 并移除同一 Buff；两条 route 继续通过现有 Gameplay Trigger bridge 执行，不新增 Runtime 动作类型。

区域缓存使用 64 个 Combat 查询候选和 8 个可映射主体。两个容量职责分离：前者必须足以覆盖 `CombatPhysicsWorld` 的查询输入，后者继续限制 Trigger/关系状态上限。区域场景对象只有 Transform，没有 Unity Collider，正式玩法查询仍由 fixed Combat AABB sensor 完成。

最终保存与重开验证确认 JSON、`GameManager.GameplayTriggerSceneCache`、Trigger/sensor/combined identity 和区域 Transform 保持一致；实际 CharacterTest Play session 中角色生成后，sensor 命中并映射主体，Buff action 返回 `Succeeded`，`601012` 在远超过自身 360 帧持续时间后仍由 Presence route 正常维持。离开区域的 remove 行为由同一模板与 focused PlayMode 回归覆盖。

修改：

- CharacterTest 现有场景或专用验证入口中的定义/Prefab/Trigger 示例。
- 最终 UI spacing、窄窗口和空/错状态。
- 用户指南、USAGE、CAPABILITIES、Interfaces、矩阵和本台账。
- 删除实现中不再需要的兼容路径，只在迁移证据允许时进行。

最低验收：

- 按父设计“阶段完成定义”完成一次人工全流程。
- Unity compile、Console、focused EditMode/PlayMode 与实际窗口视觉检查通过。
- 未实现能力明确保留在 Design，不写成 Current。

实际验收证据：

- 保存并重新打开 `CharacterTestScene` 后场景为 clean，cache 仍为 `1 trigger / 1 sensor / query 64 / subjects 8 / HasRuntimeContent=true`。
- 实际窗口为 `1440x900`，导航、列表、`360px` 属性栏和底部校验区均可见；区域属性卡完整显示模板摘要、作用目标、Buff、效果归因和运行时页签。
- 相关 EditMode fixtures 共 `34/34`，无 Collider PlayMode smoke `1/1`；Unity Console `0 error`。
- P2.2 的正式 Resource Catalog / Prefab 放置回归与 P2.3/P2.4 的 Authoring、Undo、SaveState、Replay、hash、NoAlloc 证据继续作为第二阶段累计验收的一部分。
- Windows 自动截图通道在本次会话不可用，因此视觉证据采用已打开窗口的 UI Toolkit 实际布局与可见控件检查；不把它描述为 DPI 像素截图复验。

## 7. 状态台账

实施者必须在每个提交后更新：

| ID | 增量 | 状态 | 提交 | 验证证据 | 未完成 |
|---|---|---|---|---|---|
| P2-DOC | 设计与开发文档 | Ready | — | 2026-07-28：设计、开发指南、使用说明、能力矩阵与接口文档已同步；Docs Health、链接检查和 `git diff --check` 通过 | 无 |
| P2.0 | 基线与迁移护栏 | Validated | — | 20 个 definition 逐字段 fixture、26 条 row resolution fixture 与 P2.1 baseline focused tests 通过；2026-07-28 正式场景 cache 已保存、重开并确认 hash/条目/Transform 保持一致，Unity Console 0 error | 无第二阶段迁移遗留 |
| P2.1 | 数据化世界对象定义 | Implemented | — | 2026-07-26：built-in 20 条逐字段等价；strict/canonical/hash、invalid input、stale fallback、Editor/Player mirror 6 项与 P2.0 baseline 2 项 focused EditMode 共 8/8 通过；Unity refresh 后 Console 0 编译错误；Game、Editor、Tests `.csproj` 0 error；lightweight harness 通过 | 首次类级筛选带跑 49 项，47 通过；另 2 项属于既有官方场景 URP MonoBehaviour 基线与 Scene cache content hash 不一致，留待 P2.0 场景护栏处理 |
| P2.2 | 内容选择与真实资源放置 | Implemented | — | 2026-07-26：对象定义进入现有虚拟化树/搜索列表；支持新建、停用、类型化属性与 ResourceKey 编辑，并与独立 Undo/Redo、统一保存/关闭恢复绑定；按使用反馈移除容易与实例复制混淆的“复制为变体”，场景复制统一走 Scene 右键“复制并放置”；正式 Resource Catalog 新增世界对象 Prefab，101 定义通过 ResourceKey 创建并预览真实 Prefab 实例且保持 Prefab 关联；缺失资源阻止放置，无 primitive 静默回退；场景角色选择改读 Character Application/RuntimeSpawn 发现的 registry；Unity 编译与 Console 0 error，P2.2 focused 4/4、P2.1/原放置回归 12/12、Resource Catalog 2/2 通过；实际窗口已打开且渲染`对象定义`入口；P2.5 再次确认正式 Catalog、Prefab 与放置回归作为累计 Playable 路径的一部分 | 无第二阶段遗留；定义硬删除、全资源浏览器仍是非目标 |
| P2.3 | Trigger 持久化与 Authoring | Implemented | — | 2026-07-27：P2.3a strict/canonical JSON、module payload、正式 Compiler preflight、单调/retired ID、统一保存/Undo/关闭恢复和 region 删除保护保持不变；P2.3b 新增选中 TriggerRegion 的渐进配置卡、Story Enter Once、Buff Enter Once、Buff Presence 三个固定映射、`更多`中的复制/删除、字段级事务与选中区域 compiled candidate AABB；AABB region 已补充低透明度实体面和 Scene 六面手柄，定义尺寸首次调整会切换为实例 Scene Transform，范围与文档变更保持一个 Undo；Inspector 使用`摘要 / 条件 / 目标 / 动作`编辑层级，Buff 新建时要求目录选择，手动 ID 只作迁移后备，cooldown/max activations 通过折叠的语义模式渐进显示，payload 修改和复制保持 route policy；Unity 编译、Console、focused UI/policy/payload/Undo、360px 结构、三种模板 Compiler、Authoring JSON、Config Compiler 和事务组合回归证据见本节后续验证记录；P2.5 正式场景样例完成保存重开与实际窗口复验 | 自由节点图、任意表达式仍是非目标 |
| P2.4 | Runtime 组合与诊断 | Implemented | — | 2026-07-27：scene cache、Combat AABB sensor、RuntimeHost、Buff/Story bridge、SaveState/checkpoint/hash 和 typed Play Monitor 已接入 CharacterTest offline session；P2.4 + P2.3 Authoring + Combat/Buff/Story/SaveState 相邻 focused EditMode 121/121、PlayMode smoke 1/1，Unity 编译与清理 Test Runner 结果提示后的 Console 0 error；含 cache/hash/source/action fail-closed、restore/equivalent replay hash、Inspector 实际 query 与 64 Tick 后稳态 0 B；Git Bash lightweight harness 通过，64 个变更一方代码文件无 standards finding，`git diff --check` 通过；全仓 report-only 扫描 3030 个一方代码文件的 151 个 finding 均位于既有文件，本次未新增或扩大 `unsafe` | 网络会话与运行中热重载未接入 |
| P2.5 | Playable 验收与收口 | Validated | — | 2026-07-28：`region.start` 正式 Presence Buff 示例、query 64/subject 8 容量修正、Entered+Stayed 持续刷新、Exited 实际移除、Story waiting presentation 事件顺序修复；保存重开 scene cache 保持 `1/1` 且 clean；实际 CharacterTest session 中 sensor、occurrence、Buff action 与长期维持成功；相关 EditMode 34/34、PlayMode 1/1、Console 0 error；1440x900 实际窗口结构与 360px 属性卡可见性通过 | 网络 session 注入、活动 session 热重载和运行时 Scene overlay 未纳入第二阶段 |

状态只允许：

- `Not started`
- `In progress`
- `Blocked`
- `Implemented`
- `Validated`
- `Ready`

`Implemented` 表示代码存在但未完成该行全部验证；只有证据填写完整后才能改为 `Validated/Ready`。

## 8. 保存与事务实施检查

### 8.1 Candidate preflight

保存前必须使用内存 candidate 执行与当前 dirty participant 有关的检查：

- definition dirty 时：definition schema/semantic validation、Resource Catalog 解析与 Editor resource validation。
- worldObjects/characters dirty 时：definition reference validation。
- P2.3 Trigger dirty 时：wrapper/reference validation、Trigger Config compile，以及所选 Region 的 AABB 可编译性检查。
- P2.4 Runtime/cache dirty 时：Combat sensor descriptor create/validate、scene cache hash compute。

任何一步失败都不能写文件或保存 Scene。

### 8.2 Commit/rollback

P2.3 直接复用现有 `MxLevelEditorSaveCoordinator` 的文档快照和失败恢复，不增加通用 staging、repository 或两阶段提交框架。Trigger payload 由现有 World Object save participant 与 worldObjects payload 一起写入同一 envelope。

P2.3 保存顺序：

1. World Object participant 在内存 clone 中同时 upsert worldObjects 与 gameplayTriggers 两个 module payload。
2. `TryPrepareSave` 完成全部 validation/compile，期间不写文件、不改 Scene。
3. coordinator 使用现有能力记录 level envelope 原始文件快照。
4. `SavePreparedChanges`重新读取当前 envelope、保留未知 module，再一次写入两个 CharacterTest payload。
5. 只有 Scene 序列化状态确实变化时才由 coordinator 保存一次 Scene。
6. 成功后同时重置 worldObjects、Trigger draft 与各自 Undo baseline。

异常时：

- 由 coordinator 恢复 level envelope 原始 bytes。
- P2.3 不写 Trigger scene cache，因此不产生额外 cache rollback。
- 保留 Trigger draft、选择和 dirty 状态。
- 不吞异常，只把结构化错误映射到 UI。

只有 focused test 证明现有 coordinator 无法保持 envelope 或 Scene 原子性时，才对其做最小修复；不为假设中的未来多文件事务预建新框架。

### 8.3 关闭与放弃修改

- 关闭提示明确列出“对象定义”“世界对象/角色”“Trigger”“Scene”哪些有修改。
- 只修改 definition 时，选择“不保存”重新加载 definition source，不必无条件 reload Scene。
- Scene 或 level module 有改动时沿用当前 Scene reload 恢复。
- 放弃过程中任何 reload 失败时保持窗口打开并报告，不产生半恢复状态。

## 9. Definition 迁移检查

P2.1 必须建立以下等价矩阵：

| 字段组 | 比较要求 |
|---|---|
| Identity | numeric ID、stable ID、display name、kind、category |
| Material | kind、friction raw、elasticity raw、fixed bounce height raw |
| Shape | kind、三个 half extents |
| Potency | base potency 与 profile 全字段 |
| Movable | enabled、mass 与当前全部 typed fields |
| Static | enabled 与当前全部 typed fields |
| Point | enabled、point kind、character/control defaults |
| Region | enabled、region kind |
| Ordering | runtime catalog 规范排序完全一致 |
| Hash | canonical catalog/hash 与相同输入稳定 |

禁止只比较：

- 条目数量。
- display name。
- JSON 字符串外观。
- 当前场景实际用到的子集。

## 10. Trigger 编译检查

### 10.1 编译环境

Editor candidate compile 与 Runtime bootstrap 必须复用同一 CharacterTest environment builder，内容包括：

- source providers/capabilities。
- action requirement catalog。
- retired ID registry。
- capacity/options。
- Gameplay/Story target domain 合法性。

不允许 Editor 为了“先通过”创建宽松 environment，而 Runtime 使用另一套严格 environment。

### 10.2 Region descriptor

每个 region binding 必须验证：

- region instance stable ID 唯一且存在。
- row 是 enabled `TriggerRegion`。
- Transform reference 可解析。
- definition 支持 region。
- rotation 符合首版 AABB 限制。
- scale/half extents 为有限正数。
- layer mask 非零。
- query/source-binding/provider 与 compiled source requirement 匹配。
- `maxQueryHits`、`maxMappedSubjects` 与 compiler capacity 精确匹配。

### 10.3 Action template

模板只是 CharacterTest 内部的固定映射函数，只构造 domain authoring rows，不直接执行，也不注册为可扩展插件：

| 模板 | Occurrence/route | Action |
|---|---|---|
| Story Enter Once | Entered + per-instance once | `Story.RaiseTrigger` reliable deferred |
| Buff Enter Once | Entered + configured scope/count | Gameplay Apply Buff |
| Buff Presence | Entered + Stayed 按 1 帧间隔 Apply/Refresh，Exited Remove | Gameplay Buff Apply/Remove ordered routes |

P2.3 Inspector 负责 wrapper/reference/Compiler 诊断；P2.4`运行时检查`单独显示 startup/cache reject、sensor reject 和 action result/reason。三类状态使用不同 typed source，不归并成笼统的“触发失败”。

## 11. 测试矩阵

以下 fixture 名为计划名称，可按仓库现有命名调整，但覆盖不能删除。

### 11.1 Definition Core

计划 focused EditMode/noEngine tests：

- `CharacterTestWorldObjectDefinitionAuthoringTests`
- `CharacterTestWorldObjectDefinitionMigrationTests`
- `CharacterTestWorldObjectDefinitionRuntimeCatalogTests`

覆盖：

- canonical round-trip。
- duplicate/invalid/unknown/missing schema。
- built-in 等价迁移。
- ResourceKey validation。
- fallback 与 stale generated catalog。
- Editor/Player mirror hash 等价。

### 11.2 Editor content/placement

计划 focused Editor tests：

- `CharacterTestLevelEditorContentPickerTests`
- `CharacterTestLevelEditorDefinitionWorkspaceTests`
- 现有 Level Editor placement/save/undo fixtures。

覆盖：

- provider 聚合、搜索、分类、只读/可写状态。
- virtualized row selection 保持。
- new/duplicate/edit/disable/undo/redo。
- Prefab preview/create、primitive explicit path、missing resource block。
- 窄窗口、空结果、无缩略图、诊断状态。
- 世界对象/角色模块状态切换后不丢选择。

### 11.3 Trigger Authoring

计划 focused tests：

- `GameplayTriggerAuthoringJsonTests`
- `CharacterTestLevelGameplayTriggerAuthoringTests`

覆盖：

- Trigger canonical JSON semantic order。
- module payload round-trip 与未知 module 保留。
- 单调 ID、retired、duplicate 与完整 snapshot Undo/Redo。
- enabled、state scope、presence limit、cooldown 与 max activations 的编辑、Undo/Redo，以及 payload 修改和复制后的 policy 保持。
- region binding、删除保护和 Trigger 删除不删除 region。
- template -> domain authoring exact mapping。
- candidate environment/compile diagnostics。
- 360px Inspector 的摘要、策略、冷却、次数、Buff、来源和折叠技术信息完整存在且不横向越界。
- 选中 Region 的 AABB 可编译性与 SceneView 预览。
- 保存、重开和关闭时放弃修改。

先把上述覆盖放在两个 fixture 内；只有测试职责或 setup 明显分离后才新增第三个 fixture，不按功能名机械拆文件。

### 11.4 Runtime integration

计划 focused EditMode/PlayMode：

- `CharacterTestLevelGameplayTriggerRuntimeTests`
- 复用 `GameplayTriggerCombatSensorBridgeTests`
- CharacterTest SaveState/Replay/Network/hash 相关 focused regression。
- 一个无 Unity Collider 的 PlayMode trigger smoke。

覆盖：

- Entered/Stayed/Exited。
- Buff/Story action success/rejection。
- SaveState suffix 不重复 Enter。
- Replay/hash 一致。
- bad cache/bootstrap fail-closed。
- steady-state allocation 不回归。

### 11.5 Editor visual/manual

人工检查：

1. 打开 `MxFramework/Level Editor`。
2. 通过“管理定义”复制一个定义并绑定有效 Prefab ResourceKey。
3. 保存定义，返回放置弹层并搜索新定义。
4. 放置真实 Prefab，移动、Undo/Redo、保存、关闭并重开。
5. 新建/选择 Trigger Region。
6. 配置 Story 或 Buff Trigger；确认摘要优先显示行为，启用、策略、冷却、次数、Buff 与来源可编辑，手动 ID 和技术信息保持折叠，输入期间不会因自动刷新失去焦点。
7. 在 360px Inspector 以及 100%、125%、150% DPI 下检查字段无横向裁切，不出现无 Runtime 支持的关系筛选。
8. 观察 compiled Combat AABB，并在 SceneView 拖动六面范围手柄。
9. Play Mode 进入/离开区域，检查 occurrence、action result 和可见结果。
10. 删除被 Trigger 引用的 region，确认阻止并显示来源。
11. 制造坏 ResourceKey/非法旋转/未知 action，确认保存前阻止。

## 12. 验证等级

| 增量 | 风险 | 最低验证 |
|---|---|---|
| P2.0 | V2 测试护栏 | 相关 build + focused tests + diff check |
| P2.1 | V4 schema/public low-frequency API/Player catalog | noEngine build、Unity compile、focused tests、lightweight harness、standards report |
| P2.2 | V3 Editor UI/Prefab/Scene interaction | Unity refresh/compile、Console、focused EditMode、manual window/Scene check |
| P2.3 | V4 Trigger authoring schema/public API | domain build、Unity compile、Trigger + Level Editor focused tests、lightweight harness、standards report |
| P2.4 | V4 cross-module Runtime/SaveState/Replay/hash | focused EditMode/PlayMode、allocation regression、Console、lightweight harness、standards report |
| P2.5 | V4 final Playable boundary | full risk-relevant set、manual playable flow、docs/link/diff checks |

不默认运行全仓无关测试；前一层 focused 测试失败、公共契约影响面扩大或用户要求完整验收时再升级。

## 13. 文档更新矩阵

| 发生变化 | 必须同步 |
|---|---|
| Definition schema/compiler/runtime catalog | 本指南、父设计、`Interfaces/Config.md` 或 CharacterTest Current 文档、`USAGE.md`（仅实现后） |
| ResourceKey/presentation binding | `Interfaces/Resources.md`、本指南 |
| Level Trigger module payload | 本指南、父设计、`Interfaces/Gameplay.Triggers.md` |
| Runtime composition/SaveState/Replay/hash | `Interfaces/Gameplay.Triggers.md`、`Interfaces/Runtime.md`、CharacterTest 开发标准 |
| Level Editor 用户操作 | `USAGE.md`、`LEVEL_EDITOR_QUICK_PLACEMENT_GUIDE.md` |
| 能力状态 | `LEVEL_EDITOR_CAPABILITY_REUSE_MATRIX.md`、`CAPABILITIES.md` |
| 新 public API | 对应 `Interfaces/*.md`、XML 分配注释、质量门禁证据 |
| 实际提交/测试 | 本文“状态台账” |

Design 中的未来能力不能提前加入 `CAPABILITIES.md` 或 Current 使用说明。

## 14. 每个增量的收口模板

完成一个增量后，在状态台账填写，并在提交说明/最终回复中记录：

```text
增量 ID：
交付等级：
修改文件：
复用模块/API：
新增 public API/schema：
高频路径影响：
运行检查：
测试结果：
Unity Console：
人工验证：
文档同步：
已知限制：
提交：
```

任何“通过”必须对应实际运行证据；没有运行的检查写“未运行”和原因，不能引用计划数字。

## 15. 开始编码前的最终检查

- [ ] P2.0 baseline 已提交且可独立回退。
- [ ] 当前 worktree 与远端状态已确认，未混入无关改动。
- [ ] `Docs/API_STANDARDS.md`、`Docs/QUALITY_GATE.md` 和本任务涉及的 Interfaces 已重新读取。
- [ ] 已输出当前增量的 API 复用计划。
- [ ] 已说明是否修改高频路径。
- [ ] schema identity、source/generation/mirror path 已在本文冻结。
- [ ] 新 public API 的分配分类和测试已明确。
- [ ] Unity 序列化资产创建方式已确定，不手写 YAML。
- [ ] 验收范围与当前增量匹配，不把后续阶段混入。
