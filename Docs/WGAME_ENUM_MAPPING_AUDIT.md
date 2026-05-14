# WGame 枚举映射审计

> **本文涉及的 AI 概念属于 AIAction Config 域**（GOAPWorldKey/GOAPCompareType/GOAPEffectType 等 AI 行为配置枚举的审计）。详见 `Docs/INTERFACES.md` 的 AI Terminology 章节。

> Status: Frozen for Phase 9 closeout input
> Owner: Framework Producer / Codex Review
> Last Verified: 2026-05-09
>
> Fact Conclusions: 本文冻结 Luban 枚举、C# 常量域和字段到枚举域的初步绑定；可作为 Schema 字段 `enumId` 和编辑器控件选择的输入。
>
> Pending Confirmation: `auto` 值展开、`1 << n` flags 计算、重复枚举值策略、组合 flags 解析报告和 UI 枚举域 JSON 仍需后续生成器或专项审计确认。

> 目标：汇总 WGame 配置中常见枚举、flags、常量映射来源，避免后续可视化编辑器继续展示裸整数。本文不迁移真实数据。

## 1. 结论

WGame 的枚举来源分两类：

- Luban 枚举：`Luban/Configs/Datas/__enums__.xlsx`，主要服务 BaseData 表。
- C# 常量/枚举：`Assets/Scripts/Ability/Data/**`，主要服务 Ability、AI、Buff、Map 等 Split Graph。

新配置系统需要统一枚举注册表：

```text
enumId
  source
  isFlags
  values
  displayName
  aliases
```

否则 UI Toolkit 编辑器和 AI Agent 都只能看到 `9`、`8192`、`12583055` 这类裸值。

## 2. Luban 枚举

| Enum | Flags | Items |
|---|---:|---|
| `item.EQuality` | False | WHITE=1(白); BLUE=2(蓝); PURPLE=3(紫); RED=4(红) |
| `test.AccessFlag` | True | WRITE=1; READ=2; TRUNCATE=4; NEW=8; READ_WRITE=WRITE\|READ |
| `Character.Job` | False | Swordman=1(剑士); Civilian=2(平民); Tower=3(塔); Archer=4(弓兵); Mage=5(法师); Assassin=6(刺客); Lancer=7(枪兵); Warrior=auto(战士) |
| `Character.Gender` | False | None=0(无性别); Male=1(男/雄); Female=2(女/雌) |
| `Character.Race` | False | Skeleton=1(骷髅); Artificial=2(人造物); Human=3(人类); Wyvern=4(飞龙); Goblin=5(地精); Soul=6(灵); Werewolf=7(狼人); OakTree=2(树人); Undead=2(不死族); Mantis=3(机械族); Wolf=2(野兽); Models=3(恶魔); Minotaur=4(牛头人) |
| `Character.Camp` | False | None=0(无阵营); White=1(阵营1); Red=2(阵营2); NPC=3(npc) |
| `Res.PoolType` | False | Character=0(角色); Effect=auto(特效); Weapon=auto(武器); Item=auto(物品) |
| `Setting.PageType` | False | Input=0(输入); Graphic=1(图像); Other=2(其他) |
| `Setting.EditType` | False | ComboBox=0(下拉框); Toggle=1(选中框); Slider=2(滑动条) |
| `weapon.HandleType` | True | RightHand=1(右手); LeftHand=2(左手); Dual=4(双持); All=7(全部) |
| `weapon.Type` | True | FistAndLeg=1(空手); LongSword=2(长剑); Gloves=4(手套); Staff=8(手杖); LongBow=16(长弓); Katana=32(太刀); Dual=64(双刀); Knife=128(短刀); Shield=256(盾牌); Spear=512(长矛); GreatSword=1024(大剑); GreateAxe=2048(大斧); GreatHammer=4096(大锤); Special=8192(特殊); Mix=16384(混合); SwordShield=32768(剑盾); GreateWeapon=7168(大型武器); All=-1(全部); None=0(无) |
| `AI.ActionType` | True | Fight=1(战斗); React=2(应对) |
| `AI.UniqueType` | True | None=0(无); All=-1(全部); Parry=1(弹反) |
| `Character.FootStep` | False | None=0(无); Human=1(基础人形); HumanArmor=2(铠甲人形); Beast=3(大型怪物) |
| `Setting.MatType` | False | Meat=0(肉); Metal=1(铁); Rock=2(石头); Wooden=3(木头); Rubber=4(橡胶) |
| `AI.FunctionTag` | True | Summon=1(召唤); HPRecover=2(回血); Block=4(防反); EnergyRecover=8(回气); Damage=16(增伤); Element=32(元素); Control=64(控制); HitFly=128(击飞); Frenzy=256(狂暴); Swiftness=512(迅捷); FakeImage=1024(残影); BackStab=2048(背刺); ReduceDamage=4096(减伤); MoveSpeed=8192(移速); Evade=16384(躲避); ApproveDamge=32768(易伤); AntiElement=65536(元抗); AddPoise=131072(强韧); Bleed=262144(流血); Impact=524288(冲击) ... |

问题：

- `Character.Race` 存在重复值，说明它可能不是严格唯一枚举，迁移时不能假设 value 唯一。
- `weapon.Type` 和 `AI.FunctionTag` 是 flags，编辑器应使用多选，而不是普通下拉框。
- `auto` 值必须在迁移器中显式展开，否则不同工具可能计算不一致。

## 3. Ability / AIAction Config / Buff 常量域

| Domain | Kind | Source | Items |
|---|---|---|---|
| `GOAPCompareType` | const | `Assets/Scripts/Ability/Data/AI/GOAPCompareType.cs` | SmallerThan=0; SmallerThanOrEqual=1; GreaterThan=2; GreaterThanOrEqual=3 |
| `GOAPEffectType` | const | `Assets/Scripts/Ability/Data/AI/GOAPEffectType.cs` | Decrease=0; Increase=1 |
| `GOAPWorldKey` | const | `Assets/Scripts/Ability/Data/AI/GOAPWorldKey.cs` | IsHateRank=0; IsHPPercentTarget=1; IsGuardPoint=2; IsKeepAway=3; IsEnergySelf=4; IsEnergyTarget=5; IsPoiseSelf=6; IsPoiseTarget=7; IsHeavyAtk=8; IsNearTarget=9; IsHitTargetBefore=10; IsHPPercentSelf=11; IsHPPercentChangeSelf2Sec=12; IsElementPyroPoint=13; IsElementLuminoPoint=14; IsElementElectroPoint=15 ... |
| `BuffAddType` | enum | `Assets/Scripts/Ability/Data/Buff/BuffAddType.cs` | AddNone=0; ReplaceFist; RefreshAllTime; UseSingleTime; RefreshAllTimeAndAdd; ReplaceMaxTime |
| `BuffTargetType` | enum | `Assets/Scripts/Ability/Data/Buff/BuffTargetType.cs` | None=0; Self; Target |
| `BuffType` | enum | `Assets/Scripts/Ability/Data/Buff/BuffType.cs` | None=0; Numerical; Condition; ChangeAttr; DamageByAttr; CastOrbBezier; CastOrbTrack; CastOrbLinear; Positive; Status; Max |
| `CompareType` | enum | `Assets/Scripts/Ability/Data/Buff/Condition/CompareType.cs` | Equal=0; UnEqual; Greater; Less; GreaterEqual; LessEqual |
| `ConditionType` | enum | `Assets/Scripts/Ability/Data/Buff/Condition/ConditionType.cs` | None=0; CheckHP; OnBeHit; CheckStatus; MAX |
| `AIActionEquipType` | flags const | `Assets/Scripts/Ability/Data/Define/AIActionEquipType.cs` | Fight=1; Reaction=1 << 1 |
| `AStateType` | flags const | `Assets/Scripts/Ability/Data/Define/AStateType.cs` | EnableWeaponRight=1; Unbalance=1<<1; LocalMotion=1<<2; IsOnGround=1<<3; UnHittable=1<<4; Invincible=1<<5; RotateToFocus=1<<6; KeepDistance=1<<7; IsRunning=1<<8; MoveToFocus=1<<9; DisableInput=1<<10; EnableWeaponLeft=1<<11 ... |
| `AbilityMotionType` | flags const | `Assets/Scripts/Ability/Data/Define/AbilityMotionType.cs` | LocalMotion=1; PhysicalAttack=1<<1; CastMagic=1<<2; Evade=1<<3; Hit=1<<4; Jump=1<<5; Death=1<<6; Defense=1<<7; GetUp=1<<8; Land=1<<9; ParryBack=1<<10; Final=1<<11 ... |
| `AbilityTargetType` | flags const | `Assets/Scripts/Ability/Data/Define/AbilityTargetType.cs` | Self=1; Enemy=1<<1; FocusTarget=1<<2; HateTarget=1<<3; Forward=1<<4; NPC=1<<5; FocusTargetAndForward=1<<6; OnlyGround=1<<7 |
| `AbilityType` | const | `Assets/Scripts/Ability/Data/Define/AbilityType.cs` | NormalAttack=0; SpecialAttack=1; TransitionAttack=2; PowerfulAttack=3; ComboAttack=4; Evade=5; ImpactAttack=6; HeavyAttack=7 |
| `AudioPartType` | const | `Assets/Scripts/Ability/Data/Define/AudioPartType.cs` | Root=0; Head=1; LeftHand=2; RightHand=3; LeftFoot=4; RightFoot=5; Tail=6; LastEffect=7 |
| `DirectionType` | const | `Assets/Scripts/Ability/Data/Define/DirectionType.cs` | ToTarget=1; ToSelf=2; Forward=3; Backward=4; Left=5; Right=6; ToTargetLeft=7; ToTargetRight=8 |
| `EmitNumberType` | const | `Assets/Scripts/Ability/Data/Define/EmitNumberType.cs` | Slashing=0; Bludgeoning=1; Piercing=2; True=3; Psychic=4; Holy=5; Dark=6; Fire=7; Lightning=8; Toxic=9; Water=10; Earth=11; Wind=12; Ice=13; Normal=14; HealHP=15 ... |
| `HitPerformType` | flags const | `Assets/Scripts/Ability/Data/Define/HitPerformType.cs` | Effect=1; Voice=1<<1; Impact=1<<2; Damage=1<<3; Defensed=1<<4; Parry=1<<5; Poise=1<<6; Anim=1<<7; NotShieldDefense=1<<8 |
| `InputType` | flags const | `Assets/Scripts/Ability/Data/Define/InputType.cs` | Attack=1; Defense=1<<1; HoldAttack=1<<2; JumpAttack=1<<3; Jump=1<<4; LocalMotion=1<<5; Step=1<<6; Special=1<<7; Run=1<<8; DefenseClick=1<<9; GuardPointClick=1<<10; Grounded=1<<11 |
| `MapTriggerActionType` | const | `Assets/Scripts/Ability/Data/Define/MapTriggerActionType.cs` | None=0; CloseView=1; PlayAmbient=2; PlaySound=3; End=4; AddTriggerEvent=5; EnterScene=6; PlayEffect=7; OpenView=8; RemoveTriggerEvent=9; EnterArena=10; LoadArchive=11; SetPortalPoint=12; StopEffect=13; StopDialog=14; SetSoulState=15 ... |
| `MoveDirectionType` | flags const | `Assets/Scripts/Ability/Data/Define/MoveDirectionType.cs` | Down=1; Left=1<<1; Right=1<<2; Up=1<<3; Forward=1<<4; Backward=1<<5; FocusTargetFwd=1<<6; FocusTargetBwd=1<<7; LeftForward=1<<8; LeftBackward=1<<9; RightForward=1<<10; RightBackward=1<<11 |
| `ReactionType` | const | `Assets/Scripts/Ability/Data/Define/ReactionType.cs` | Hit=0; BeHit=1; Injury=2; BeInjury=3; BreakPoise=4; BeBreakPoise=5; NoInjury=6; Heal=7; Kill=8; AbilityUse=9; WillHit=10 |
| `StatusBuffType` | const | `Assets/Scripts/Ability/Data/Define/StatusBuffType.cs` | Stun=0; WeaponIce1=1; Paralysis=2; SuperArmor=3; Dazzel=4; CrimsonNight=5; PowerImpact=6; IronCurtain=7; SelfSacrifice=8; PeakWeight=9; Equilibrium=10; Unbreakable=11; IronWall=12; EnhancedShield=13; Focus=14; FireEnchant=15 ... |
| `TriggerEventType` | flags const | `Assets/Scripts/Ability/Data/Define/TriggerEventType.cs` | HitTarget=1; BeHit=1<<1; JumpLand=1<<2; JumpLand_Heavy=1<<3; ChangeAttr=1<<4; Count=5 |
| `WAttrType` | const | `Assets/Scripts/Ability/Data/Define/WAttrType.cs` | MaxHP=0; CurHP=1; MaxMP=2; CurMP=3; ATK=4; DEF=5; ImpactVec=6; MoveSpeed=7; RotateSpeed=8; DmgRate=9; BasePoise=10; RunSpeedRate=11; PoiseRate=12; DmgPoise=13; MotionSpeedRate=14; AbilityCooldownRate=15 ... |

## 4. 字段到枚举域的初步绑定

| 数据 | 字段 | 枚举域 |
|------|------|--------|
| `#AIAction` | `type` | `AI.ActionType` |
| `#AIAction` | `weaponType` | `weapon.Type` |
| `#AIAction` | `handleType` | `weapon.HandleType` |
| `#Entry` | `tagFunction` | `AI.FunctionTag` |
| `#WeaponItems` | `weaponType` | `weapon.Type` |
| `characterInfoData` | `job` | `Character.Job` |
| `characterInfoData` | `gender` | `Character.Gender` |
| `characterInfoData` | `race` | `Character.Race` |
| `characterInfoData` | `camp` | `Character.Camp` |
| `objectPool` | `poolType` | `Res.PoolType` |
| `settingData` | `pageType` | `Setting.PageType` |
| `settingData` | `editType` | `Setting.EditType` |
| `SplitAIActionData` | `EquipType` | `AIActionEquipType` |
| `SplitAIActionData` | `AbilityWeaponType` | `weapon.Type` 或 Ability 侧 weapon type mask |
| `SplitAIActionData` | `ReactType` | `ReactionType` |
| `SplitAIActionData` | `ConditionList.K` | `GOAPWorldKey` |
| `SplitAIActionData` | `ConditionList.T` | `GOAPCompareType` |
| `SplitAIActionData` | `EffectList.K` | `GOAPWorldKey` |
| `SplitAIActionData` | `EffectList.T` | `GOAPEffectType` |
| `SplitBuffData` | `Type` | `BuffType` |
| `SplitBuffData.Buff.Data[3]` | `Target` | `BuffTargetType` |
| `SplitBuffData.Buff.Data[4]` | `AddType` | `BuffAddType` |
| `SplitBuffData.N[0]` | `AttrID` | `WAttrType` |
| `SplitBuffData.Condition.ConditionType` | `ConditionType` |
| `SplitBuffData.Status.D[0]` | `StatusType` | `StatusBuffType` |

## 5. 对可视化编辑器的影响

编辑器控件建议：

- flags 使用多选掩码控件。
- 普通 enum 使用下拉框。
- const domain 也应包装成 enum-like 选择器。
- 允许显示 `中文名 + 英文名 + 数值`，例如 `长剑 LongSword (2)`。
- AI Agent 上下文必须导出枚举域，否则自动修改配置时容易误写裸整数。

后续需要补：

- 把 `auto` 枚举值完全展开。
- 把 `1 << n` flags 计算成数值。
- 为 `BreakType=12583055` 这类组合值提供解析报告。
- 为 UI Toolkit 编辑器生成枚举域 JSON。
