# WGame 表字段索引

> Status: Archive
> Archived state: Frozen for Phase 9 closeout input
> Owner: Framework Producer / Codex Review
> Last Verified: 2026-05-09
>
> Fact Conclusions: 本文冻结 Luban Excel 与 `BaseDataJson/zh` 的表字段索引、多语言列、显式引用字段和优先 Schema 顺序；可作为 `Table` 与 `Localization` 种子输入。
>
> Pending Confirmation: Excel 与 BaseDataJson 行数差异、注释/禁用行处理、复合引用细节和全量表迁移顺序仍需后续迁移器验证；本文不把 BaseDataJson 本地化展开文本作为新权威编辑源。

> 目标：汇总 WGame Luban Excel 与 BaseDataJson 的表、字段、行数、引用、多语言列和枚举列，为后续 Schema 设计提供全局索引。本文不迁移真实数据。

## 1. 结论

WGame 表格层有两个事实来源：

- `Luban/Configs/Datas/*.xlsx` 是历史编辑源。
- `Assets/Res/BaseDataJson/zh/*.json` 是当前中文运行时导出产物之一。

注意：

- `__tables__.xlsx` 只显式登记了部分非 `#` 表；大量 `#*.xlsx` 仍导出为 `BaseDataJson`。
- Excel 行数和 JSON 行数不完全一致，通常因为 Excel 内有注释行、空行、禁用行或临时行。
- `BaseDataJson/{locale}` 已经把 text key 展开成当前语言文本，不应作为新的权威编辑源。
- 新 Schema 应以 Excel 字段语义为起点，以 BaseDataJson 实际导出字段校验运行时产物。

## 2. 表索引

| Excel | BaseDataJson | JSON rows | 字段数 | 主字段 | 引用 | 多语言列 | 枚举列 |
|---|---:|---:|---:|---|---|---|---|
| `#AIAction.xlsx` | `tbaiaction.json` | 499 | 16 | id:int, name:text, desc:text, icon:string, editName:string, canGet:bool, quality:int, cost:int, type:AI.ActionType, weaponType:weapon.Type ... | - | name, desc | type:AI.ActionType; weaponType:weapon.Type; handleType:weapon.HandleType |
| `#AbilityModifier.xlsx` | `tbabilitymodifier.json` | 6 | 7 | id:int, modType:int, name:text?, param1:int, param2:int, param3:int, canGet:bool | - | name | - |
| `#Acheivement.xlsx` | `tbacheivement.json` | 2 | 4 | id:int, name:text, desc:text, point:int | - | name, desc | - |
| `#AnimAttack.xlsx` | `tbanimattack.json` | 55 | 9 | id:string, attack1:string?, attack2:string?, attack3:string?, SpecialAttack:string?, SpecialAttack2:string?, RunAttack:string?, distance:int, energyCost:int | - | - | - |
| `#AnimBase.xlsx` | `tbanimbase.json` | 55 | 13 | id:string, localMotion:string?, deadR1:string?, jump1:string?, falling:string?, jumpLand1:string?, Spare1:string?, Step1:string?, Step2:string?, stun:string ... | - | - | - |
| `#AnimDefense.xlsx` | `tbanimdefense.json` | 13 | 11 | id:string, block1:string?, ParryAtk1:string, ParryAtk2:string, ParryAtk3:string, ParryLeft:string, ParryRight:string, BounceLeft:string, BounceRight:string, Impact:string ... | - | - | - |
| `#Blessing.xlsx` | `tbblessing.json` | 14 | 5 | id:int, name:text, icon:string, listEntry:list,entryList#sep=|, canGet:bool | - | name | - |
| `#Buff.xlsx` | `tbbuff.json` | 60 | 7 | id:int, name:text, desc:text, icon:string?, effectAddr:string?, effectScale:float, effectPartType:int | - | name, desc | - |
| `#CharacterAI.xlsx` | `tbcharacterai.json` | 133 | 4 | id:string, fightAI:string, reactAI:string, goal:string | - | - | - |
| `#CharacterAttribute.xlsx` | `tbcharacterattribute.json` | 15 | 30 | id:int, name:string, moveSpeed:int, rotationSpeed:int, runSpeedMul:int, maxHP:int, curHP:int, maxMP:int, curMP:int, ATK:int ... | - | - | - |
| `#CharacterAudio.xlsx` | `tbcharacteraudio.json` | 43 | 10 | id:string, footVolumeFactor:float, footPitchFactor:float, footParticleSizeRate:float, footParticleType:int, sensorMaterialType:list,int#sep=|, sensorMatSizeType:list,int#sep=|, objectID:int, rotateOffset:int, PoiseOffset:int | - | - | - |
| `#CharacterItem.xlsx` | `tbcharacteritem.json` | 2 | 2 | id:int, item list | - | - | - |
| `#CharacterSkill.xlsx` | `tbcharacterskill.json` | 48 | 10 | id:int, name:text?, desc:text?, operateType:int, icon:string, quality:int, cooldown:float, cooldownInit:float, freezeTime:float, canGet:bool | - | name, desc | - |
| `#ChessLoot.xlsx` | `tbchessloot.json` | 48 | 4 | id:int, name:text, quality:int, icon:string | - | name | - |
| `#Dialog.xlsx` | `tbdialog.json` | 54 | 3 | id:int, value:text, formatParam:(array#sep=|),int | - | value | - |
| `#Entry.xlsx` | `tbentry.json` | 103 | 10 | id:int, name:text?, desc:text?, param1:(array#sep=|),int, param2:(array#sep=|),int, param3:(array#sep=|),int, levelCount:int, canGet:bool, quality:int, tagFunction:AI.FunctionTag | - | name, desc | tagFunction:AI.FunctionTag |
| `#MapData.xlsx` | `tbmapdata.json` | 5 | 3 | id:int, name:string, editName:string | - | - | - |
| `#Skill.xlsx` | `tbskill.json` | 13 | 9 | id:int, name:text, desc:text, icon:string, quality:int, cost:int, damage:int, dmgPoise:int, impact:int | - | name, desc | - |
| `#Story.xlsx` | `tbstory.json` | 8 | 4 | id:int, storyID:int, name:text?, desc:text? | - | name, desc | - |
| `#TalentAttribute.xlsx` | `tbtalentattribute.json` | 12 | 6 | id:int, name:text, icon:string?, attrId:int, attrAdd:int, attrMul:int | - | name | - |
| `#TalentEffect.xlsx` | `tbtalenteffect.json` | 2 | 5 | id:int, name:text, icon:string?, typeId:int, value:int | - | name | - |
| `#TalentTree.xlsx` | `tbtalenttree.json` | 117 | 5 | id:int, maxNum:int, parentID:array#sep=|,int, param1:int, param2:int | - | - | - |
| `#WeaponAudio.xlsx` | `tbweaponaudio.json` | 21 | 7 | id:string, audio:list,audioParam#sep=|, hit:list,int#sep=|, sensorMatBaseType:int, materialType:int, isBaseMat:bool, groundSound:list,int#sep=| | - | - | - |
| `#WeaponItems.xlsx` | `tbweaponitems.json` | 22 | 10 | id:int, name:text, desc:text, icon:string, canGet:bool, quality:int, weaponType:weapon.Type, rightID:int#ref=id@weapon.TbWeapon, leftID:int#ref=subId@weapon.TbWeapon, cost:int | rightID->id@weapon.TbWeapon; leftID->subId@weapon.TbWeapon | name, desc | weaponType:weapon.Type |
| `#lang.xlsx` | `tblang.json` | 680 | 2 | id:int, value:text | - | value | - |
| `animationClips.xlsx` | - | - | 2 | id:int, path:string | - | - | - |
| `characterInfoData.xlsx` | `tbcharacterinfo.json` | 148 | 29 | id:int, name:string, preset:string?, weapon:int#ref=id@weapon.TbWeapon, weaponSub:int#ref=subId@weapon.TbWeapon, AIIndex:int, job:Character.Job, gender:Character.Gender, race:Character.Race, camp:Character.Camp ... | weapon->id@weapon.TbWeapon; weaponSub->subId@weapon.TbWeapon; attribute->TbCharacterAttribute; entries->TbEntry; audio->TbCharacterAudio | showName | job:Character.Job; gender:Character.Gender; race:Character.Race; camp:Character.Camp |
| `characters.xlsx` | `tbcharacter.json` | 13 | 18 | id:int, name:string, showName:text, desc:text, objectId:int#ref=TbObjectData, infoId:int#ref=TbCharacterInfo, deadZoom:bool, showStateToScreen:bool, arenaEnterEff:string, icon:string ... | objectId->TbObjectData; infoId->TbCharacterInfo; defaultWeapon->TbWeaponItems; item->TbCharacterItem | showName, desc | weaponType:weapon.Type |
| `charactersAnimation.xlsx` | `tbcharanim.json` | 61 | 43 | id:int, name:string, idle:string?, isHumanoid:bool, walk_F:string?, run_F:string?, walk_B:string?, run_B:string?, walk_L:string?, run_L:string? ... | - | - | - |
| `guideInput.xlsx` | `tbguideinput.json` | 2 | 6 | id:int, name:string, desc:string, objectId:int, infoId:int, AI:string | - | - | - |
| `hitAnimData.xlsx` | - | - | 3 | hitWeapon:int, tarWeapon:int, motions:string | - | - | - |
| `item.xlsx` | `item_tbitem.json` | 1 | 3 | id:int, name:text, desc:text | - | name, desc | - |
| `objectPool.xlsx` | `tbobjectdata.json` | 139 | 6 | id:int, name:string, path:string, needCache:bool, poolType:Res.PoolType, param:int | - | - | poolType:Res.PoolType |
| `settingData.xlsx` | `tbsettingdata.json` | 21 | 10 | id:int, sort:int, pageType:Setting.PageType, name:text, editType:Setting.EditType, editDataString:(array#sep=|),text, editDataInt:(array#sep=|),int, defaultValue:int, show:bool, refreshOnQualityChanged:bool | - | name, editDataString | pageType:Setting.PageType; editType:Setting.EditType |
| `weapons.xlsx` | `weapon_tbweapon.json` | 123 | 19 | id:int, subId:int, name:string, desc:string, quality:int, weaponType:weapon.Type, soundCfg:string#ref=TbWeaponAudio, objectId:int#ref=TbObjectData, AI:(array#sep=|),string#ref=TbCharacterAI, animBase:string#ref=TbAnimBase ... | soundCfg->TbWeaponAudio; objectId->TbObjectData; AI->TbCharacterAI; animBase->TbAnimBase; animAttack->TbAnimAttack; animDefense->TbAnimDefense | - | weaponType:weapon.Type |

## 3. 字段分类

### 3.1 多语言字段

当前明确使用 `text` / `text?` 的表：

- `#AIAction`: `name`, `desc`
- `#Buff`: `name`, `desc`
- `#Entry`: `name`, `desc`
- `#CharacterSkill`: `name`, `desc`
- `#Skill`: `name`, `desc`
- `#Story`: `name`, `desc`
- `#Dialog`: `value`
- `#lang`: `value`
- `#AbilityModifier`: `name`
- `#Acheivement`: `name`, `desc`
- `#Blessing`: `name`
- `#ChessLoot`: `name`
- `#TalentAttribute`: `name`
- `#TalentEffect`: `name`
- `#WeaponItems`: `name`, `desc`
- `characters`: `showName`, `desc`
- `characterInfoData`: `showName`
- `item`: `name`, `desc`
- `settingData`: `name`, `editDataString`

新结构建议：

- 表格源保留 `nameKey` / `descKey`。
- `Localization.tsv` 统一保存 key 和多语言文本。
- BaseDataJson 的本地化文本只作为旧导出产物。

### 3.2 显式引用字段

当前 Excel 中有 `#ref` 标记的字段集中在角色、武器和对象链路：

- `characters.objectId -> TbObjectData`
- `characters.infoId -> TbCharacterInfo`
- `characters.defaultWeapon -> TbWeaponItems`
- `characters.item -> TbCharacterItem`
- `characterInfoData.weapon + weaponSub -> weapon.TbWeapon`
- `characterInfoData.attribute -> TbCharacterAttribute`
- `characterInfoData.entries -> TbEntry`
- `characterInfoData.audio -> TbCharacterAudio`
- `weapons.soundCfg -> TbWeaponAudio`
- `weapons.objectId -> TbObjectData`
- `weapons.AI -> TbCharacterAI`
- `weapons.animBase -> TbAnimBase`
- `weapons.animAttack -> TbAnimAttack`
- `weapons.animDefense -> TbAnimDefense`
- `#WeaponItems.rightID + leftID -> weapon.TbWeapon`

注意：

- `#AIAction -> SplitAIActionData -> SplitAbilityData`、`#Buff -> SplitBuffData`、`#CharacterAI -> SplitAIConfigData` 这些关键关系不在 Excel `#ref` 中，需要由新 Schema 显式补上。
- `#TalentTree.param1/param2` 是多态引用，Excel 没有表达目标表，需要新 Schema 补充。

## 4. 对 Schema 设计的影响

优先补 Schema 的表：

1. `#AIAction`：连接 BaseData、AIAction Graph、AbilityGraph。
2. `#Buff`：连接可见 Buff 元数据和 SplitBuffData。
3. `#CharacterAI` / `weapons`：连接武器、角色 AI 和 AIConfig Graph。
4. `#TalentTree`：多态引用需要显式建模。
5. `#lang`：拆成独立 Localization 权威源。

不建议先全量迁移所有表。更合理的顺序是：

- 先为高关系密度表建立 Schema。
- 再用 Schema 反推低风险纯表格表。
- 最后处理历史导出产物和运行时 bytes。
