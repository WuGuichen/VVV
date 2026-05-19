# Character Resource Package 角色创作方案

> 状态：草案
> 范围：角色资源包、外部 3D 装配编辑器、Authoring Compiler、Unity 导入桥、Runtime Spawn 垂直切片
> 目标读者：框架开发者、工具开发者、技术策划
> 工程落地：`Docs/CHARACTER_RESOURCE_PACKAGE_IMPLEMENTATION_PLAN.md`

## 结论

这套方案可以做。它的核心不是把 Unity Editor 包一层外壳，而是把角色做成一个可独立创作、可校验、可导入 Unity、可被 Runtime 生成的资源包。

最终形态接近“角色资源包 / 角色 Mod 包”：

```text
Character Resource Package
  -> 外部 3D 装配编辑器
  -> Authoring Compiler 预检和编译
  -> Unity Importer Bridge 导入资源和配置
  -> Runtime Spawn 生成可活动角色
```

第一版目标不是替代建模软件，也不是做完整动画状态机编辑器。第一版只负责把一个角色进入框架所需的数据可视化装配好，并保证导入 Unity 后 Runtime 能用。

## 最终效果

设计者拿到一个角色资源包，例如 `IronVanguard.mxchar` 或目录包：

```text
IronVanguard.mxchar/
  manifest.json
  resource_catalog.json
  resources/
    models/
    textures/
    materials/
    animations/
    audio/
    vfx/
  previews/
  config/
  geometry/
  validation/
```

外部编辑器打开这个包后，用户看到的是角色装配工作台：

- 左侧是包资源树：模型、贴图、动画、武器、配置、身体几何、碰撞体、挂点、trace。
- 中央是 3D 视口：显示角色模型、胶囊体、身体部位碰撞框、hit zone、武器 socket、武器 trace。
- 右侧是 Inspector：编辑选中对象的稳定字段，例如 collider shape、partId、hitZoneId、socketId、bone path、local pose、trace radius。
- 底部是预检报告：能否保存、能否导入 Unity、能否 Runtime Spawn、缺哪些资源、哪个引用或部位映射有问题。

完成后点击“导入 Unity”，Unity Importer Bridge 会把包内模型、贴图、动画等资源导入 Unity 项目，生成或更新 ResourceCatalog 映射、Character 配置 patch、geometry binding 和导入报告。Runtime Spawn 再通过 `CharacterPackageResolver` 解析角色，生成可活动实例。

## 设计边界

Character Resource Package 是源头。Unity 项目不是外部编辑器的前置输入。

这意味着：

- 外部编辑器直接打开角色资源包，不要求 Unity 先导出模型或骨骼工作包。
- 模型、贴图、动画、武器资源和角色配置在同一个包内组织。
- Unity Importer 负责把包内资源导入项目，并把包内 ResourceKey 映射到 Unity 项目的 ResourceCatalog。
- Runtime 只消费导入后的配置、资源映射和 binding，不直接把未导入的资源包当运行时资源源头。

这也意味着第一版必须坚持以下限制：

- 外部 authoring DTO 不引用 `UnityEngine` 或 `UnityEditor`。
- Runtime 配置不保存 `UnityEngine.Object`、prefab、`AnimationClip` 或 material。
- 角色配置只保存初始值、规则和引用关系；当前 HP、冷却、Buff、装备实例、资源 handle 属于运行时状态。
- 外部编辑器不复制 Gameplay / Combat / CharacterControl 运行时逻辑，只调用纯 resolver 和 compiler 做预检。
- Unity Importer 不私自解释 authoring 数据，转换权威属于 Authoring Compiler。

## 关键对象

| 对象 | 职责 |
| --- | --- |
| `CharacterResourcePackage` | 角色创作和交换的主载体，包含资源、配置、geometry 和校验报告。 |
| `package-local resource catalog` | 包内资源索引，记录 ResourceKey、relative path、type、variant、hash 和 import hints。 |
| `CharacterAuthoringGeometry` | 身高、半径、默认 capsule、身体部位 collider、socket、weapon attachment、trace 等 3D 装配数据。 |
| `CharacterAuthoringCompiler` | 把角色资源包编译为 runtime config patch、geometry binding、resource mapping 和 gate report。 |
| `Unity Character Importer Bridge` | 在 Unity 侧执行导入、资源落盘、ResourceCatalog 映射和写入计划。 |
| `CharacterResolvedProfile` | Workstation、Spawn、Debug、测试共用的角色解析结果。 |

## 资源包结构

第一版建议使用目录包作为权威格式，归档文件只作为分发格式。

```text
CharacterPackage/
  manifest.json
  resource_catalog.json
  resources/
    models/
      iron_vanguard.glb
      iron_sword.glb
      kite_shield.glb
    textures/
    materials/
    animations/
    audio/
    vfx/
  previews/
    thumbnail.png
    preview_model.json
  config/
    character.json
    equipment.json
    abilities.json
    combat_actions.json
  geometry/
    body_geometry.json
    body_colliders.json
    sockets.json
    weapon_attachments.json
    traces.json
  validation/
    last_report.json
```

`manifest.json` 至少包含：

| 字段 | 说明 |
| --- | --- |
| `packageId` | 包短 ID，例如 `iron_vanguard`。 |
| `stableId` | 长期稳定 ID，例如 `charpkg.iron_vanguard`。 |
| `version` | 包版本。 |
| `kind` | 第一版固定为 `character`。 |
| `schemaVersion` | 包 schema 版本。 |
| `authoringSchemaVersion` | 3D authoring 数据版本。 |
| `coordinateConvention` | 坐标系、up/forward、单位、旋转存储格式。 |
| `dependencies` | 依赖的框架版本、共享资源包或基础包。 |
| `hashes` | source package hash、resource hash、generated config hash。 |

`resource_catalog.json` 至少包含：

| 字段 | 说明 |
| --- | --- |
| `resourceKey` | 包内稳定资源 key，例如 `char.iron_vanguard.model.body`。 |
| `localId` | ResourceKey 生成用的包内局部 ID，例如 `model.body`、`anim.combat`。 |
| `stableId` | 长期稳定资源 ID，用于跨版本、导入冲突、诊断和迁移。 |
| `typeId` | model、texture、material、animation、audio、vfx、preview。 |
| `variant` | 可选变体，例如 `default`、`lod0`、`combat`。 |
| `usage` | characterModel、weaponModel、animationClipGroup、previewThumbnail 等用途。 |
| `sourceFormat` | gltf、glb、png、jpg、tga、json、wav、ogg 等包内源格式；fbx 为 future / optional。 |
| `relativePath` | 包内相对路径。 |
| `hash` / `hashes.contentHash` | 内容 hash；`hash` 是兼容字段，C0.5 使用 `hashes` 作为权威结构。 |
| `hashes.importHash` | 由 source format 和 import hints 计算的导入语义 hash。 |
| `hashes.dependencyHash` | 由依赖边和依赖资源 content hash 计算的依赖语义 hash。 |
| `importHints` | Unity target path policy、target relative path、scale、材质策略、动画切分策略、坐标提示、collision/physics policy。 |
| `dependencies` | 该资源依赖的其他包内 ResourceKey，形成 package-local dependency graph。 |
| `conflictPolicy` | 同 stable id、hash 未变、hash 变化时的 skip/report/upgrade/variant 策略。 |
| `preview` | thumbnail、preview mesh、placeholder 和 camera preset 元数据。 |
| `provenance` | source tool、source file、license、origin、createdBy、modifiedBy 等来源信息。 |

C0.5 固定 glTF / GLB 为 v1 模型和动画组的目标格式。FBX 可以出现在 catalog 中，但只作为 future / optional 触发 warning。Unity 6 项目内的 glTF/GLB 实际导入能力不在 C0.5 中假设，必须在 #222 / #224 通过 importer package、转换步骤或 placeholder 策略补齐。

## 3D Authoring 数据

角色资源包必须能表达不同形态的角色：人形、奇幻生物、简单几何体 Actor 都使用同一套抽象。

第一版关键数据：

| 数据 | 示例 |
| --- | --- |
| body geometry | 角色身高、半径、默认 capsule center、模型缩放、质量、坐标系。 |
| body parts | `head`、`torso`、`left_hand`、`tail`、`core`、`front_face`。 |
| colliders | 每个部位一个或多个 capsule / box / sphere。 |
| hit zone binding | `hit.head` -> `head`，包含优先级、弱点、倍率。 |
| sockets | `mainHand`、`offHand`、`back`、`headVfx`，绑定 bone path 或 locator path。 |
| weapon attachment | 武器挂在哪个 socket，local grip pose 是什么。 |
| trace | 剑刃 trace 起点、终点、半径、采样规则。 |

第一版 collider shape 只支持：

- capsule
- box
- sphere

convex 和 custom mesh 可以保留枚举值，但 v1 校验必须标记为 unsupported。

## 坐标和单位

坐标规则必须先稳定，否则外部编辑器和 Unity 导入会出现不可调试的偏差。

第一版固定：

- 存储单位默认 `1 unit = 1 meter`。
- quaternion 是旋转权威格式，Euler 只作为 UI 展示。
- 每个 local pose 必须声明父空间：model root、bone、locator、body part 或 socket。
- manifest 必须记录 up axis、forward axis、unit scale 和 handedness。
- Unity Importer 必须按同一规则转换，不能让每个 importer 自己猜。
- roundtrip 目标：authoring local pose -> Unity import -> 重新读回 authoring local pose，在误差阈值内稳定。

## 外部编辑器

外部编辑器第一版叫做 Character Resource Package 装配工作台。它不是表格数据库 UI，而是 3D 装配工具。

默认工作流：

```text
打开角色资源包
  -> 检查 manifest / resource catalog
  -> 加载包内模型和预览资源
  -> 编辑 body geometry / colliders / sockets / traces
  -> 切换 loadout 预览装备状态
  -> 运行 resolver 和 compiler 预检
  -> 保存 package
  -> 可选：调用 Unity Importer Bridge 导入 Unity
```

核心交互：

- 选择 collider，3D 视口显示 transform gizmo，右侧 Inspector 显示 shape、partId、hitZoneId、size、local pose。
- 选择 socket，视口显示轴向和父骨骼，Inspector 显示 socketId、bone path、usage、handedness。
- 选择 weapon attachment，视口显示武器模型和 grip pose，Inspector 显示 equip slot、attach socket、trace start/end/radius。
- 切换空手、单剑、剑盾 loadout，视口和 resolver 输出同时更新。
- 点击 validation issue，视口定位到对应 3D 对象或包内资源。

外部编辑器能做：

- 打开和保存角色资源包。
- 编辑 3D 装配数据。
- 预览装备状态和资源依赖。
- 显示 resolver / compiler diagnostics。
- 调用 Unity Importer Bridge 导入 Unity。

外部编辑器不做：

- 建模、蒙皮、复杂动画曲线编辑。
- 完整 Combat action timeline 编辑。
- Gameplay / Combat 运行时模拟。
- 直接写 Unity 项目资产。

## Authoring Compiler

Compiler 是外部编辑器和 Unity Importer 的共同权威。

输入：

- `CharacterResourcePackage`
- package-local resource catalog
- 包内 dependency graph / hash report
- existing config source index
- existing project ResourceCatalog summary 或可选 Unity import target context
- compiler options：strict、allow warnings、target output format、Unity import path policy

输出：

- generated Character Application config patch
- body collider / hit zone / socket / trace binding
- package resource catalog mapping
- Unity import/write plan
- resource dependency report
- validation/gate report
- deterministic hashes
- source mapping：package path -> generated config field / import target

Compiler 必须是 noEngine 纯逻辑，不读取 Unity 场景对象，不写 Runtime world。

## Gate Policy

所有失败必须结构化输出，不允许静默 fallback。

| Gate | 含义 |
| --- | --- |
| `ExportBlocked` | 外部编辑器不得保存为可导入产物，但可以保存草稿。 |
| `ImportBlocked` | Unity Importer 不得写入项目资源或配置。 |
| `SpawnBlocked` | Unity 可以导入，但 Runtime 不得生成角色实例。 |
| `WarningOnly` | 可以继续，但必须进入报告。 |

每个 issue 至少包含：

- severity
- stable code
- gate
- source package path
- source object path
- field
- message
- suggested fix

v1 gate 字面值已经固定：

- `Unknown`：未知或旧版本无法解释的 gate。
- `ExportBlocked`：禁止保存为可导入 / 可分发产物，但不禁止保存 editor draft。
- `ImportBlocked`：Unity Importer 不得写入项目资源或配置。
- `SpawnBlocked`：可以导入，但 Runtime Spawn 不得生成角色实例。
- `WarningOnly`：允许继续，但必须进入报告。

后续新增 gate 必须使用保留位升级或提升 authoring schema version。

示例 code：

- `CHARPKG_MISSING_MODEL_RESOURCE`
- `CHARPKG_RESOURCE_HASH_MISMATCH`
- `CHARPKG_UNSUPPORTED_COLLIDER_SHAPE`
- `CHARPKG_SOCKET_BONE_MISSING`
- `CHARPKG_UNMAPPED_HIT_ZONE`
- `CHARPKG_IMPORT_PATH_CONFLICT`
- `CHAR_EQUIPMENT_STATE_TIE`

## Unity Importer Bridge

Unity Importer Bridge 的职责是把角色资源包导入 Unity 项目。

它执行：

```text
选择 Character Resource Package
  -> 读取 manifest / resource_catalog / resources / geometry / config
  -> 调用 Authoring Compiler
  -> 检查 ImportBlocked
  -> 按 import/write plan 导入模型、贴图、材质、动画、预览资源
  -> 生成或更新 Unity project ResourceCatalog mapping
  -> 写入 config patch / package cache / adapter asset
  -> 输出导入报告
```

Importer 不做：

- 不成为主创 UI。
- 不私自实现一套 authoring-to-runtime 转换。
- 不把 `UnityEngine.Object` 写进 noEngine config。
- 不把 runtime current state 写回配置。
- 不要求包内资源已经存在于 Unity 项目。

## Runtime Spawn

Runtime Spawn 只消费导入后的产物：

- Character Application config patch
- ResourceCatalog mapping
- geometry binding
- weapon attachment / trace binding
- compiler gate result

生成流程：

```text
CharacterSpawnRequest
  -> CharacterPackageResolver
  -> CharacterResolvedProfile
  -> ResourceDependencyReport preload
  -> Gameplay entity
  -> Combat body / colliders / hit zones
  -> CharacterRuntimeBinding
  -> Equipment runtime state
  -> Presentation view / animation binding
  -> CharacterControl command source
```

运行时状态独立保存：

- 当前 HP、资源值、姿态压力。
- 当前装备实例、武器耐久、弹药、强化。
- Buff、冷却、动作状态。
- 部位损伤、断肢或护甲破坏状态。
- view 实例、resource handle、animation actor id。

配置表只保存初始值和规则，不保存这些当前值。

## 示例：Iron Vanguard

第一版样例角色 `Iron Vanguard` 应覆盖完整链路。

包内资源：

- body model：`resources/models/iron_vanguard.glb`
- sword model：`resources/models/iron_sword.glb`
- shield model：`resources/models/kite_shield.glb`
- locomotion animations：idle、walk、run
- combat animations：sword_attack、shield_guard、unarmed_attack
- preview thumbnail

authoring 数据：

- 身高 `1.86m`
- 默认 capsule：height `1.86`，radius `0.34`，center `(0, 0.93, 0)`
- body parts：head、torso、left_hand、right_hand、left_leg、right_leg
- colliders：head sphere、torso capsule、hands sphere
- sockets：mainHand、offHand、back
- weapon attachments：mainHand sword、offHand shield
- traces：sword blade start/end/radius

loadout：

- unarmed
- single sword
- sword shield

预期结果：

- 外部编辑器可切换三种 loadout 预览。
- `EquipmentStateResolver` 能得到唯一 active state。
- `AbilityGrantResolver` 能显示当前有效能力。
- `ResourceDependencyResolver` 能列出模型、武器、动画、预览资源。
- Unity 导入后 Runtime 能生成玩家剑盾角色，也能生成同角色配置的敌方实例。

## 非人形适配

方案必须同时支持非人形。

示例一：Drake

- body parts：head、neck、body、left_wing、right_wing、tail、front_claw、back_claw。
- sockets：mouthVfx、tailTip、backMount。
- colliders：body capsule、wing boxes、tail capsules。
- hit zone：head 弱点，tail 可断尾，wing 命中影响飞行或姿态压力。

示例二：Slime

- body kind：primitive 或 compound。
- body parts：core、shell、front_face。
- colliders：一个 sphere 或多个 overlapping sphere。
- sockets：topVfx、frontAttack。
- weapon state：可以是 innate_weapon / body_weapon，不要求实体武器模型。

这说明角色系统不能假设人形骨骼，也不能把武器写死为“手上拿的一把剑”。

C0 已用同一套 DTO 表达 `Training Slime`：`Primitive` body kind、`core/shell/front_face` body parts、sphere colliders、`topVfx/frontAttack` sockets，并保留 innate weapon / body weapon 的后续编译空间。

## 分阶段实施

当前建议按以下 Issue 顺序推进：

| 阶段 | Issue | 交付 |
| --- | --- | --- |
| C0 | #221 | Character Resource Package 与 3D Authoring 数据契约。 |
| C0.5 | #223 | 包内资源管线、package-local resource catalog、hash、dependency graph、import hints。 |
| C0.6 | #224 | Authoring Compiler、gate policy、import/write plan。 |
| C1 | #217 | 外部 Character Resource Package 装配工作台 MVP。 |
| C2 | #222 | Unity Character Resource Package Importer Bridge。 |
| D | #218 | Runtime Spawn 垂直切片。 |

不能跳过 C0 / C0.5 / C0.6 直接做 UI。否则外部编辑器会变成看起来能调、实际导入和生成无法稳定复现的空壳。

## 需要优先补齐的前置功能

这套方案依赖以下能力稳定：

- Character Application 12 张配置表和 typed id。
- 纯 resolver：equipment、ability、combat action、body part hit zone、resource dependency、spawn、save state binding。
- Resource Catalog 的包级挂载、variant、hash、导入映射。
- Config patch / package cache 的写入目标。
- noEngine Authoring Core / CLI 的基础校验入口。
- Unity Editor 侧 importer command 或菜单入口。

已完成的 #215 / #216 覆盖了角色配置表和纯 resolver。下一步应优先做 #221，把角色资源包和 3D authoring 数据契约固定下来。

## 主要风险

| 风险 | 处理方式 |
| --- | --- |
| 外部编辑器和 Unity 坐标不一致 | C0 固定坐标、单位、local pose 父空间和 roundtrip 误差。 |
| 资源包和 Unity 项目 ResourceCatalog 映射混乱 | C0.5 固定 package-local catalog 和 import mapping。 |
| 编辑器、Importer、Runtime 各自解释数据 | C0.6 固定 Authoring Compiler 作为唯一转换权威。 |
| UI 先行导致好看但不可用 | C1 只能在 C0/C0.5/C0.6 后做，并必须调用 compiler 预检。 |
| 非人形角色后补会推翻结构 | C0 必须用 Drake / Slime 作为契约样例校验。 |
| 运行时状态污染配置 | 所有当前值只进入 runtime state / SaveState，不进入 config。 |
| 过早支持复杂碰撞和动画 | v1 只支持 capsule / box / sphere，不做完整动画状态机编辑。 |
| Unity glTF/GLB 导入能力不确定 | C0.5 只声明源格式契约；#222 / #224 必须确认 Unity importer package、转换器或 placeholder 策略。 |

## Done Definition

这条主线完成时，应满足：

- `Iron Vanguard` 角色资源包可以被外部编辑器打开、编辑、保存。
- 用户能在 3D 视口调整 body collider、socket、weapon trace。
- 用户能切换 unarmed、single sword、sword shield 三种 loadout，并看到 resolver 输出。
- Compiler 能输出 generated config patch、geometry binding、resource mapping、gate report。
- Unity Importer 能导入包内资源并生成 ResourceCatalog 映射。
- Runtime Spawn 能生成可活动角色实例，并显示 resolved profile、runtime ids、equipment state、abilities、resource issues。
- SaveState roundtrip 能恢复角色 config binding，并重新解析 active equipment state。

到这里，角色不再只是配置表，也不是 Unity 场景里的临时 prefab，而是框架可复用的角色资源包 authoring 到 runtime 的完整闭环。
