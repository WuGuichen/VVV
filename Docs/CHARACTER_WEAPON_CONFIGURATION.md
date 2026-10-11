# CharacterStudio 武器与动作配置 8.0 开发基准

> Status: Current
>
> Authority: Normative Development Baseline
>
> Implementation: Character Application 8.0、Global Weapon Library 6.0、CharacterAction Source、Authoring Compiler、事务迁移与 CharacterStudio 均以本契约为准。

> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/Editor/CharacterTestActionBrowserCatalogExporter.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems/Character/CharacterTestActionUseAuthority.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems/Character/CharacterTestActionUseFailure.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestActionBrowserCatalog.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/TacticalCard/CharacterTestTacticalExecutionAdapter.Published.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/TacticalCard/TacticalCardEnums.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/UI/FairyGui/CharacterTestFairyGuiTacticalActionPicker.Classification.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/UI/FairyGui/CharacterTestFairyGuiTacticalActionPicker.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/UI/TacticalCardHud.cs`
> Code-Doc: `Packages/com.vincent.wgameframework/Character.Application/CharacterApplicationConfigSchemas.cs`
> Code-Doc: `Packages/com.vincent.wgameframework/Character.Application/CharacterApplicationConfigTypes.cs`
> Code-Doc: `Packages/com.vincent.wgameframework/Character.RuntimeSpawn/CharacterImportedPackageJson.cs`
> Code-Doc: `Packages/com.vincent.wgameframework/CharacterAction/CharacterActionConfig.cs`
> Code-Doc: `Tools/Blender/WGameWeaponTools/export_contract.py`
> Code-Doc: `Tools/Blender/WGameWeaponTools/test_export_contract.py`
> Code-Doc: `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/CharacterPackages/CharacterAuthoringCompiler.cs`
> Code-Doc: `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/CharacterPackages/CharacterWeaponConfigurationValidator.cs`
> Code-Doc: `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/CharacterPackages/GlobalWeaponLibrary.cs`
> Code-Doc: `Tools/MxFramework.CharacterStudio/web/src/singleWeaponWorkbench.js`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems/Character/CharacterTestWeaponTypeAdmission.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestWeaponTypePublication.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Game/CharacterTest/CharacterTestWeaponTypePolicyTests.cs`
> Code-Doc: `Packages/com.vincent.wgameframework/Character.Application/WeaponCategory.cs`
> Code-Doc: `Packages/com.vincent.wgameframework/Character.Application/WeaponTypeCatalog.cs`

## 1. 核心结论

组合武器配置是项目公共配置，不属于某个角色。角色只保存公共组合 ID、默认选择和确有差异的行为覆盖：

```text
Global Weapon Library 6.0
└─ 公共组合武器配置
   ├─ 主手 / 副手单一武器配置
   ├─ 公共动画默认
   ├─ 公共能力默认（当前只读兼容）
   ├─ 公共受击默认
   └─ 公共 CharacterAction / ActionSet 默认

Character Application 8.0
└─ 公共组合引用
   └─ 可选角色行为覆盖
      ├─ 动画
      ├─ 能力（数据契约保留，普通 UI 冻结）
      ├─ 受击
      └─ CharacterAction / ActionSet
```

解析顺序固定为：

`角色非空覆盖 > 公共默认`。

2026-10-11 当前源码：CharacterTest 的能力动作通过实体当前装备的 ActionSet 解析 `AbilityBindings`；未声明角色专属绑定时回退到共享 Ability 动作，同一 Ability 出现重复绑定时返回无效动作。动作准入、卡牌费用查询和实际提交按这一有效动作检查能力归属，共享 Ability 继续拥有冷却与资源成本。角色 000010 的法杖技能配置和既有原生验证范围见[角色技能说明](Guides/CHARACTER_000010_STAFF_ABILITIES.md)；本段源码复核不增加运行验收结论。

没有覆盖时必须自动继承公共默认，不复制数据，不创建隐式角色私有配置。主副手槽位永远来自公共组合，角色不得覆盖。

- Resource Manager 管理模型、动画 Clip、贴图、材质、Prefab 和 ResourceKey 生命周期。
- `WeaponSource/global_weapon_library.json` 管理单一武器、公共组合及可复用行为配置。
- `CharacterActionSource/Libraries/Common/character_actions.json` 是公共动作正文的编辑权威。
- `<角色包>/config/character_actions.json` 是角色专属动作正文的编辑权威。
- `config/character_application.json` 只管理当前角色引用、默认项和纯动作引用覆盖。
- `Assets/MxFrameworkGenerated/CharacterActions` 只保存编译投影，不是编辑入口。
- Runtime 不读取全局 Authoring 库；Compiler 为每个角色投影解析后的有效配置。
- 禁止根据名称、类别、标签或 `sword/shield/all_weapons` 等字符串猜测组合或行为。

## 2. 所有权

| 数据 | DTO / 文件 | 所有权 |
| --- | --- | --- |
| 单一武器 | `SingleWeaponConfiguration` | 项目公共 |
| 组合武器 | `CombinedWeaponConfigurationAuthoringSummary` | 项目公共 |
| 动画 | `GlobalAnimationConfiguration` | 项目公共 |
| 能力 | `GlobalAbilityConfiguration` | 项目公共；Authoring 当前冻结 |
| 受击 | `GlobalReactionConfiguration` | 项目公共 |
| 公共动作正文 | `CharacterActionSource/Libraries/Common/character_actions.json` | 项目公共；角色上下文只读 |
| 角色专属动作正文 | `<角色包>/config/character_actions.json` | 角色本地 |
| 组合引用与覆盖 | `CharacterCombinedWeaponConfigurationBinding` | 角色本地 |
| 角色武器槽位 | `geometry/weapon_slots.json` | 角色本地；Socket 与槽位姿态 |
| 公共资产 | `AuthoringResourceSelectionRef` / `ResourceKey` | Resource Manager |

公共配置删除前必须扫描全部角色引用。仍被引用时硬阻止，不级联删除或改写角色。

## 3. Global Weapon Library 6.0

权威文件固定为 `WeaponSource/global_weapon_library.json`：

```json
{
  "schemaVersion": "6.0",
  "libraryId": "mx.global.single-weapons",
  "weaponTypes": [],
  "singleWeaponConfigurations": [],
  "combinedWeaponConfigurations": [],
  "animationConfigurations": [],
  "abilityConfigurations": [],
  "reactionConfigurations": []
}
```

各类型 ID 在本类型内唯一、长期稳定、区分大小写。公共组合 ID 必须具备项目级唯一性；角色包迁移使用 `<packageId>.<localCombinationId>` 避免碰撞。

保存使用统一 library revision 与 `expectedRevision`。单条 CRUD 也推进同一 revision。

### 3.1 单一武器

单一武器拥有模型选择、自身姿态、完整武器局部 Trace、表现、战斗参数、资源策略和基础 Modifier，不拥有动画、能力、受击或 Action。

- `weapon.unarmed` 为不可删除的内置配置，不生成武器实例。
- 普通单手武器 `occupiesSlots=[]`；双手武器显式占用主副手。
- 同一单一武器可用于主副手；左右手差异来自角色槽位，Compiler 不隐式镜像。

### 3.2 公共组合武器

```json
{
  "combinedWeaponConfigurationId": "combat_girl_sword.sword_empty",
  "displayName": "剑 / 空手",
  "slots": [
    { "slotId": "mainHand", "singleWeaponConfigurationId": "combat_girl_sword.sword" },
    { "slotId": "offHand", "singleWeaponConfigurationId": "weapon.unarmed" }
  ],
  "animationConfigurationId": "animation.combat_girl_sword.profile.sword",
  "abilityConfigurationId": "",
  "reactionConfigurationId": "reaction.combat_girl_sword.default",
  "actionConfiguration": {
    "actions": [
      { "actionRole": "primary", "actionId": "light_attack" },
      { "actionRole": "primary.combo2", "actionId": "light_attack_2" },
      { "actionRole": "primary.combo3", "actionId": "light_attack_3" }
    ],
    "commandSequences": [
      {
        "intentId": "LightAttack",
        "actionRoles": ["primary", "primary.combo2", "primary.combo3"],
        "wrapAfterLast": true,
        "priority": 10,
        "allowQueue": false,
        "queueWindowFrames": 0
      }
    ],
    "abilityActionBindings": [],
    "reactionActionBindings": []
  }
}
```

规则：

- 必须显式且仅包含一个 `mainHand` 与一个 `offHand`。
- 空手必须写为 `weapon.unarmed`，不得用缺失槽位表达。
- 槽位只保存 `slotId + singleWeaponConfigurationId`，不得保存姿态、Trace 或 attachment ID。
- 动画与受击缺失时允许保存公共草稿，但阻止引用角色 Compile / Unity Import。
- `abilityConfigurationId` 可空；当前普通 CharacterStudio 流程不创建、修改、复制或绑定能力。
- `actionConfiguration` 是公共默认。角色只有显式动作覆盖时才保存自己的完整替代值。
- `actions[]` 只允许 `{ actionRole, actionId }`；旧内嵌 `action` / `timeline` 仅供迁移读取，不再写出。
- 同一个 intent 只能由普通 `commandBindings` 或结构化 `commandSequences` 之一拥有。
- 公共组合、单一武器和行为配置均参与引用保护与稳定 hash。

### 3.3 动画、能力与受击

- 动画配置必须自包含完整 Set、Profile、Layer、Group、Clip、Blend、Timeline 与 locomotion。
- 能力配置当前为 Frozen / Experimental Compatibility；历史数据只在高级兼容诊断中只读显示，不作为普通完成条件。
- 受击配置保存触发语义、冲击区间、资源和 `actionRole`，不得保存组合专属 `actionId`。
- 受击 `actionRole` 由有效组合动作配置唯一解析。

### 3.4 实体武器类型与动作准入

武器类型以 `WeaponSource/global_weapon_library.json.weaponTypes[]` 为唯一制作权威。字段为 `typeId`、`displayName`、`equipmentCategory`、`iconResourceKey`；具体武器的 `weaponTypeId` 精确引用目录。当前只声明 `sword`（单手剑，`OneHandMelee`）和 `staff`（法杖，`Catalyst`）。`WeaponCategory` 现有九项枚举用于宽泛装备分类，成员及编号保持原样；它不代替实体类型目录。`weapon.unarmed` 的类型为空，不是实体类型。数字武器 catalog 只保存身份和出口投影，必须与该目录及单一武器一致。

`WeaponTypeCatalog` 构造时校验非空目录、稳定 ID/显示名、唯一性、合法且精确的 Category 名称，拒绝数值字符串、`Unarmed` 映射和 ID 中空白，并复制定义。`ValidateWeapon`、`ValidateRequirement`、`Matches` 分别验证武器类型/Category、正数槽位和实际装备匹配；`Contains`、`DisplayName`、`IconResourceKey` 提供只读查询。构造为低频分配，正常成功查询不创建定义或加载资源。

Compiler 将单一武器类型投影到 `WeaponConfig.WeaponTypeId` 和 schema 同名字符串字段；Runtime Spawn JSON reader 同时读取类型、既有拖尾资源、残留时间与 `BladeTrailStyleId`。两个现有构造调用方式仍可使用，类型参数追加在末尾；省略时类型为空、拖尾样式为 0。目录、映射和被引用武器的类型均参与 source hash，不能从名称、tags、手槽、动画文件名或数字编号推断类型。

动作 `requirements[]` 增加 `WeaponTypeRequired=11` 和 `WeaponTypeIndependent=12`。前者必须为 `{ kind: "WeaponTypeRequired", stableId: "sword" | "staff", value: <正数装备槽>, referenceId: 0 }`；后者必须为 `{ kind: "WeaponTypeIndependent", stableId: "", value: 0, referenceId: 0 }`。两种声明不能并存。前者表达实际装备约束，后者表达已核对的身体动作/无武器依赖动作；`action_classification.json` 只保存 Body/通用用途等浏览元数据，不授予学习、持有或执行权限。

当前声明仅覆盖已核对动作：301/324/325、455–458、460–463 使用主手单手剑；000010 的 1090000–1090003 使用主手法杖；000003 的 940000–940004 为身体动作。Mage 四个动作仍为 `animation_only`，这些声明不添加伤害、Projectile、技能事件或命中轨。410、950000–950002 保持未核对的 BasicAttack，准入明确拒绝；这是当前发布策略，不是证明无伤害或动画动作非法。新增未声明 BasicAttack 也遵循该策略。

CharacterTest 在可用性查询、费用/预留检查与最终动作提交边界复核同一准入。实体必须启用实际装备来源 `UseDefaultLoadout`；关闭时即便有合法非空 state ID 也返回 `WeaponTypeEquipmentSourceUnavailable`。包 owner 与实际 trace/VFX 路径一致：实体 GeneratedPackageRoot 非空时使用 `LoadPackageForEntity`，否则使用 `LoadDefaultCharacterPackage`。非空装备状态精确且唯一解析 loadout；只有来源启用且状态为空才读取默认 loadout。未知、歧义、空槽或错类型均拒绝，不回退到其他包/槽位。拒绝不得提前提交 Ability、支付费用或发布行动 Stage；原有卡片实例与账本事务保持现有协议。

Schema 6.0 存在实体 single 时，缺失、null 或空 `weaponTypes` 产生 `WeaponTypeCatalogMissing`，Severity=Error、Gate=ExportBlocked、SourcePath=`WeaponSource/global_weapon_library.json`、Field=`weaponTypes`。仅无实体 single 的 unarmed-only Authoring fixture 可省略目录；自带目录仍须合法。项目 Publication 继续要求完整有效目录。

原生 `MxFramework/CharacterTest/Rebuild Action Browser Catalog` 在写文件前核对目录、单一武器、数字身份及动作 requirement。发布 `mx.weapon-types.v1` 到 Editor 的 `Assets/MxFrameworkGenerated/CharacterActions/weapon_type_catalog.json` 与 Player 的 `Assets/StreamingAssets/MxFramework/CharacterTest/weapon_type_catalog.json`。字段包括 `sourcePath`、源字节 `sourceHash`、`actionCatalogFingerprint`、目录、`unclassifiedBasicAttackPolicy="reject"`、已独立动作 ID 与未核对 BasicAttack ID。Editor/Player 投影是发布产物，不能手工维护第二套目录。

CharacterStudio 单一武器编辑与动作选择器使用同一目录，类型选择同步其 Category；新武器必须显式选类型，不能自由写 Category 制造第二套映射。动作选择器入口显示“武器 / 用途”，把实体武器与身体/通用用途分组。当前 `iconResourceKey` 为空，界面保留文字；专用剑与法杖 PNG 尚未获得合法本机来源，不得声称已集成或填入不存在的资源路径。

集成状态（2026-10-09，main `cbc1409f`）：000001–000004、000008–000010 的官方 Unity 导入均为 ready；本轮重导入 000002、000003、000004、000008、000009、000010 全部成功。官方目录发布已完成，武器类型与动作浏览目录的 Editor/Player 两份投影字节一致，sourceHash 为 `116d0f581a5ab1a5cdf8f15ecd89eb7f3383b7772b0e3fdeb0350c0a8f7fc188`。最后原生编译 job `ab4782f0433240c38c693e807d638835` 通过，编译时 Console error 为 0。首次 340 项原生测试为 324 通过、16 失败；三处夹具修正后，定向 37 项全通过，覆盖全部 16 个失败及到达、恢复和后继动作三个主线修复案例；没有删除断言或放宽生产门禁。实际导入角色装备探测 36/36 通过。真实 FairyGUI 已显示五张正式准备卡与法杖分类的 9 个动作。InteractionPreparation 专用说明分支已集成，新增 16 项原生说明测试与 13 项发布测试全部通过；真实五卡卡面已显示对应效果，生产 DTO 的有效期详情包含 F+600，截图已实际查看。AI Lab 试验界面现有 Tooltip 仅显示实例/区域和选牌提示，本轮没有捕获到可见悬浮窗口，不将其宣称为效果详情 Tooltip 验收。扩展运行中旧动作卡夹具仍有 4 项失败，额外夹具迁移已撤回，保留原候选测试文件。最终恢复候选后的原生编译已结束、重载已观察、Editor ready 且 Console error 为 0；验证工具 job 882aece28d804cc9a62aded6aab96f39 报超时，原始状态已留档。StreamingAssets 的角色动作镜像仍含旧 requirement，必须通过正式 `CharacterTestCharacterActionPlayerArtifacts.Refresh()` 与全局资源构建更新，尚未执行 Player 构建验收。专用剑/法杖图标仍未取得可用 Library 材料，保留文字回退。原用户场景、Play 状态和 562 个受保护持久化文件已恢复；本轮证据位于本机 task-11/Weapon_Types_CBC1409f。

## 4. Character Application 8.0

角色文件不再序列化 `combinedWeaponConfigurations[]`：

```json
{
  "schemaVersion": "8.0",
  "defaultCombinedWeaponConfigurationId": "combat_girl_sword.sword_empty",
  "combinedWeaponConfigurationBindings": [
    {
      "combinedWeaponConfigurationId": "combat_girl_sword.sword_empty",
      "overrides": {
        "animationConfigurationId": "",
        "abilityConfigurationId": "",
        "reactionConfigurationId": "",
        "actionConfiguration": null
      }
    }
  ]
}
```

### 4.1 引用与覆盖规则

- `defaultCombinedWeaponConfigurationId` 必须精确引用当前角色 bindings 中的一项。
- binding 的公共 ID 必须存在且不可重复。
- 空字符串表示继承对应公共默认。
- `actionConfiguration` 缺失或 `null` 表示继承公共动作；非空时完整替代公共动作，不做深层合并。
- 第一次修改任一角色动作槽时，CharacterStudio 先复制完整公共映射，再只替换目标槽；这仍是显式完整替代，不是隐式深合并。
- 角色覆盖不得包含 `slots`；武器组合身份始终由公共配置决定。
- 有效配置仅在读取、校验、预览和编译阶段解析，使用 `[JsonIgnore]` 临时投影，不能写回角色文件。
- 角色覆盖不影响其他角色；公共默认修改会自动影响所有未覆盖该字段的角色。

### 4.2 角色槽位

`geometry/weapon_slots.json` 只拥有语义槽位到 Socket 的绑定与角色局部姿态。最终变换顺序：

`角色骨骼 × 角色武器槽位 × 单一武器 modelPose × Resource Manager 导入修正`。

## 5. CharacterStudio

组合武器工作台必须清晰区分两层：

- Profile 固定显示为 `<package>.default`，ActionSet 固定显示为 `<package>.<weaponCombination>`；二者由 Compiler 生成，不提供 ID 输入框。
- 动作区按普通攻击 1/2/3、重击、闪避及当前能力/受击配置要求动态显示动作槽，不显示可编辑的大段 ActionSet JSON。
- 每个动作槽使用“角色专属 / 公共动作”分组选择，并显示动画、时长、命中轨、来源及原生/Humanoid 重定向/不兼容状态。
- Action Library schema 仍为 `0.2.0`，但库根可声明 `animationSources[]`：以资源 key 前缀记录源 `rigType`、Skeleton/Avatar identity 与 `allowRetargeting`。Compiler 和 CharacterStudio 对动作正文实际引用的每个动画资源执行最长前缀匹配；缺少声明、声明冲突或非 Humanoid 跨骨架引用均判为不兼容，不允许只根据目标角色类型猜测。
- 公共组合默认只可引用 Common；角色覆盖可引用 Common 或当前角色库。一个有效 Profile 的库集合内动作 ID 必须唯一。
- Common 动作在角色上下文只读；“在 Unity 查看”使用只读模式，“复制为角色专属并编辑”原子复制正文并切换当前槽引用。
- 原始 JSON 仅作为折叠只读审计信息。
- 当前角色状态：显示“未引用 / 已引用 / 角色默认”，允许加入或移除引用、设为默认，并编辑可选行为覆盖。
- 覆盖选择器第一项固定为“继承公共默认（当前 ID）”。清除覆盖立即恢复继承。
- 动作覆盖必须显式启用；关闭后删除角色动作副本并恢复公共动作。
- 不提供角色槽位覆盖 UI。
- 保存顺序为公共库后角色包；任一 revision 冲突均停止并保留内存草稿。
- 列表、总览、3D 预览和运行快照使用同一有效解析结果。
- 删除公共组合时先读引用；存在任意角色 binding 时阻止。

能力 Authoring 的冻结边界保持不变。8.0 数据模型预留能力覆盖字段是为了无损兼容，不代表普通 UI 已开放能力编辑。

## 6. API 与编译

- `GET /api/weapon-library/state`：返回 Global Weapon Library 6.0 与 revision。
- `POST /api/weapon-library/save`：保存完整公共库并保护被引用的删除。
- `GET /api/weapon-library/references?configurationType=combinedWeapon&configurationId=<id>`：返回角色 bindings。
- `GET /api/weapon-library/reference-index?configurationType=combinedWeapon`：批量返回公共组合引用索引。
- `GET /api/weapon-library/configurations?configurationType=combinedWeapon`：返回公共组合列表。
- 单条 validate/save/delete API 必须接受 `configurationType=combinedWeapon`。
- `GET /api/character/weapon-configurations`：返回当前角色解析后的有效组合、生成状态和诊断；不是持久化 DTO。
- CharacterAction catalog API 返回固定 Profile、ActionSet、分组动作库、动作元数据和源文件 hash；复制 API 使用 package revision 与源 hash 执行双文件原子事务。
- Unity 打开请求必须携带 Profile、ActionSet、Library、Action ID、读写模式与源 hash；打开、保存、冲突、失败和超时均写回可查询状态。

Compiler 规则：

- 先解析角色 binding 与覆盖，再执行全部既有组合校验。
- 精确解析 ID，不做名称、标签或类别推导。
- source hash 包含角色 bindings、覆盖以及实际引用的公共配置规范化内容。
- source hash 同时包含 Common 与角色动作源；动作正文变化后旧 Unity import receipt 必须立即判为过期。
- 只投影当前角色 bindings 指向的 Weapon、Animation、Ability、Reaction、CharacterAction、EquipmentLoadout 与 EquipmentState。
- Runtime 与 Unity Importer 不直接读取 `WeaponSource`。
- 动作库正文保持 schema `0.2.0`；生成的 Profile、ActionSet、Reaction/Landing 投影使用 schema `0.3.0`。
- Runtime 以实体 `characterStableId + equipmentStateStableId` 精确选择 ActionSet；缺失时报告完整上下文，不回退到其他角色或 Codex Flow。

## 7. 迁移

Character Application 7.0 → 8.0 与 Global Weapon Library 5.0 → 6.0 在迁移动作权威时作为同一批次处理：

1. 同时校验角色包和全局库两个 `expectedRevision`。
2. 每个角色本地组合按 `<packageId>.<localId>` 提升为公共组合。
3. 同 ID 同内容复用；同 ID 不同内容以 `WEAPON_SCHEMA_MIGRATION_GLOBAL_CONFLICT` 阻止。
4. 角色本地组合替换为 bindings，默认 ID 同步改为提升后的公共 ID。
5. 将生成目录中的 Common 动作正文迁入正式公共源，将组合内嵌动作抽取到对应角色动作源，并把 `actions[]` 收敛为纯引用。
6. 为普通攻击生成 `LightAttack` 结构化连段；Profile、ActionSet 与库投影按语义 hash 比较成功后才切换权威。
7. 任一步失败同时回滚角色包、全局库与动作源。

仓库维护者可在仓库根目录执行确定性迁移脚本：

```powershell
pwsh -File Tools/MxFramework.Authoring/scripts/migrate-character-actions-v8.ps1
```

脚本会同时迁移 Common、角色动作源和组合引用，输出 Profile / ActionSet 语义 hash；重复执行必须得到相同文件与相同 hash。

更早版本仍先按各自旧迁移规则完成单一武器、行为和槽位提升，再进入 8.0；旧结构不是新开发扩展入口。

## 8. 稳定诊断

新增或继续使用的关键诊断包括：

- `GLOBAL_COMBINED_WEAPON_CONFIGURATION_ID_DUPLICATE`
- `GLOBAL_COMBINED_WEAPON_CONFIGURATION_NOT_FOUND`
- `GLOBAL_WEAPON_CONFIGURATION_IN_USE`
- `COMBINED_WEAPON_CONFIGURATION_MISSING`
- `COMBINED_WEAPON_CONFIGURATION_DEFAULT_INVALID`
- `COMBINED_WEAPON_SLOT_MISSING`
- `COMBINED_WEAPON_ATTACHMENT_REQUIRED`
- `COMBINED_WEAPON_ACTION_SET_MISSING`
- `COMBINED_WEAPON_ACTION_ROLE_MISSING`
- `COMBINED_WEAPON_ACTION_ID_SHARED`
- `COMBINED_WEAPON_COMMAND_INTENT_CONFLICT`
- `WEAPON_SCHEMA_MIGRATION_GLOBAL_CONFLICT`

## 9. Definition of Done

- 公共组合能 CRUD、复制、共享编辑、引用扫描、冲突拒绝与受保护删除。
- 角色只序列化 bindings 与覆盖；有效组合不回写角色文件。
- 无覆盖时逐字段继承公共默认；动作覆盖为显式完整替代；槽位不可覆盖。
- Combat Girl 与示例角色使用不同的命名空间公共组合，并解析各自正确单一武器。
- Compiler、快照、3D 预览与 Runtime 投影使用同一有效组合。
- CharacterStudio 逐槽选择、继承恢复、Common 只读/复制和 Unity 状态回传有 smoke 覆盖。
- Runtime 普通攻击只请求 `LightAttack`，由每个实体自己的 ActionSet 连段依次解析真实动作并继续服从动作切换窗口。
- 7.0 / 5.0 → 8.0 / 6.0 的源抽取、语义比较、双 revision、冲突检测和完整回滚有自动化覆盖。
- Authoring 定向测试、CharacterStudio smoke、代码规范和 `git diff --check` 通过。
