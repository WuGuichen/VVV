# WGame 数据源审计

> Status: Frozen for Phase 9 closeout input
> Owner: Framework Producer / Codex Review
> Last Verified: 2026-05-09
>
> Fact Conclusions: 本文冻结的数据源分类、规模摘要和优先迁移对象，仅基于文内已记录的 WGame 审计证据；可作为 Phase 9 Schema 种子和引用规则输入。
>
> Pending Confirmation: 本文不确认 WGame 项目层业务取舍，不迁移真实配置，不把 BaseDataJson、bytes 或 SaveData 视为新权威源；这些边界由后续迁移试点逐项确认。

> 目标：先逐步汇总 WGame 当前数据来源，再分析整理新结构。本文只记录现状、证据和初步分类，不直接执行迁移。

## 1. 当前结论

WGame 当前配置数据不是单一来源，至少包含：

- Luban Excel 表。
- Luban 生成或导出的 BaseData JSON。
- Ability / AI / Buff 等拆分 JSON。
- bytes 运行时或资源产物。
- SaveData JSON/bytes 存档数据。

这些来源混合了"权威编辑源""运行时导出产物""存档数据"和"工具缓存"。下一步不能直接选择一种格式覆盖全部数据，必须先区分数据职责。

> **本文涉及的 AI 概念属于 AIAction Config 域**（旧 WGame AI 行为配置数据的审计），部分涉及 Runtime AI Planner（WGame GOAP 行为树的来源分析）。详见 `Docs/INTERFACES.md` 的 AI Terminology 章节。

下一轮关系审计见 `Docs/WGAME_DATA_RELATION_AUDIT.md`。当前最关键的结论是：WGame 的真实配置结构依赖跨源引用链路，尤其是 `TbAIAction -> SplitAIActionData -> SplitAbilityData`、`TbCharacterAI -> SplitAIConfigData -> SplitAIActionData`、`TbBuff -> SplitBuffData` 和 `TbTalentTree.param1/param2` 多态引用。

Split Graph 结构细节审计见 `Docs/WGAME_SPLIT_GRAPH_AUDIT.md`。当前已补齐 `SplitAIActionData`、`SplitAIConfigData`、`SplitBuffData` 的主要字段位序、类型分布和迁移影响。

表字段索引见 `Docs/WGAME_TABLE_FIELD_INDEX.md`，枚举映射索引见 `Docs/WGAME_ENUM_MAPPING_AUDIT.md`。当前已把 Luban/BaseData 的主表字段、引用列、多语言列和常见枚举域汇总成 Schema 设计入口。

## 2. 数据来源总览

| 类别 | 路径 | 数量/规模 | 当前形态 | 初步职责判断 |
|------|------|-----------|----------|--------------|
| Luban 表格 | `WGame/Client/Luban/Configs/Datas/*.xlsx` | 约 39 个 xlsx，含临时 `~$settingData.xlsx` | Excel | 表格型权威源或历史权威源 |
| Luban 类型定义 | `WGame/Client/Luban/Configs/Defines/builtin.xml` | 1 个 | XML | 历史 Schema/类型定义 |
| BaseDataJson | `Assets/Res/BaseDataJson/{locale}/*.json` | 11 个语言目录，每个 33 个 JSON，共 363 个 | JSON 数组表 | 多语言运行时导出产物或热更产物 |
| Ability 拆分数据 | `Assets/Res/SplitAbilityData/**/*.json` | 1282 个 | 压缩结构 JSON | 结构型能力数据，当前更像运行时/编辑器混合产物 |
| AIAction 拆分数据 | `Assets/Res/SplitAIActionData/*.json` | 501 个 | 压缩结构 JSON | 结构型动作数据 |
| AIConfig 拆分数据 | `Assets/Res/SplitAIConfigData/**/*.json` | 151 个 | 压缩结构 JSON | 结构型 AI 配置 |
| Buff 拆分数据 | `Assets/Res/SplitBuffData/*.json` | 179 个 | 压缩结构 JSON | Buff 基础字段与复杂效果混合 |
| SaveData | `Assets/Res/SaveData/`、`StreamingAssets/SaveData/` | 若干 JSON/bytes | JSON/bytes | 存档数据，不应归入配置权威源 |
| Runtime bytes | `*.bytes` | Assets 下约数百个 | bytes | 运行时或资源产物，不应人工编辑 |

## 3. Luban 表格清单

当前 `Luban/Configs/Datas` 下的表格包括：

- `#AIAction.xlsx`
- `#AbilityModifier.xlsx`
- `#Acheivement.xlsx`
- `#AnimAttack.xlsx`
- `#AnimBase.xlsx`
- `#AnimDefense.xlsx`
- `#Blessing.xlsx`
- `#Buff.xlsx`
- `#CharacterAI.xlsx`
- `#CharacterAttribute.xlsx`
- `#CharacterAudio.xlsx`
- `#CharacterItem.xlsx`
- `#CharacterSkill.xlsx`
- `#ChessLoot.xlsx`
- `#Dialog.xlsx`
- `#Entry.xlsx`
- `#MapData.xlsx`
- `#Skill.xlsx`
- `#Story.xlsx`
- `#TalentAttribute.xlsx`
- `#TalentEffect.xlsx`
- `#TalentTree.xlsx`
- `#WeaponAudio.xlsx`
- `#WeaponItems.xlsx`
- `#lang.xlsx`
- `__beans__.xlsx`
- `__enums__.xlsx`
- `__tables__.xlsx`
- `animationClips.xlsx`
- `characterInfoData.xlsx`
- `characters.xlsx`
- `charactersAnimation.xlsx`
- `guideInput.xlsx`
- `hitAnimData.xlsx`
- `item.xlsx`
- `objectPool.xlsx`
- `settingData.xlsx`
- `weapons.xlsx`

初步判断：

- `#Buff`、`#AbilityModifier`、`#Entry`、`#AIAction`、`#CharacterSkill`、`#Talent*` 是战斗和成长系统优先审计对象。
- `__beans__`、`__enums__`、`__tables__` 是历史类型定义来源，应转入 Schema 审计。
- `#lang` 和 `BaseDataJson/{locale}` 需要一起判断多语言权威源。

## 4. BaseDataJson 汇总

`Assets/Res/BaseDataJson` 目前按语言拆分：

| 语言目录 | JSON 数量 |
|----------|-----------|
| `de` | 33 |
| `en` | 33 |
| `es` | 33 |
| `fr` | 33 |
| `it` | 33 |
| `jp` | 33 |
| `kr` | 33 |
| `pt_br` | 33 |
| `ru` | 33 |
| `zh` | 33 |
| `zh_hant` | 33 |

代表结构：

- `tbbuff.json`：数组表，字段包括 `id/name/desc/icon/effectAddr/effectPartType/effectScale`。
- `tbcharacterskill.json`：数组表，字段包括 `id/name/desc/icon/quality/cooldown` 等。
- `weapon_tbweapon.json`：数组表，字段包含 `id/name/desc/AI/ATK/animAttack/animBase/animDefense` 等。

初步判断：

- BaseDataJson 已经更接近运行时表数据。
- 它不适合作为最终权威编辑源，因为同一表被多语言目录复制，容易产生结构和翻译同步问题。
- 新结构应把多语言拆到独立 Localization 表，业务表只引用 text key。

## 5. AbilityData 语义入口与 SplitAbilityData 汇总

数量：

| 子目录 | JSON 数量 |
|--------|-----------|
| `TestGroup` | 994 |
| `MapSettingData` | 191 |
| `SkillData` | 52 |
| `MapTriggerData` | 44 |
| `WyvernData` | 1 |

语义入口：

- `WGame/Client/Assets/Scripts/ECS/Ability/AbilityData.cs` 是 Ability 数据语义入口。
- `AbilityData` 的基础字段是 `ID`、`Name`、`TotalTime`。
- `AbilityData.Properties` 是自定义变量列表，运行时进入 `PropertyContext` 按变量名访问。
- `AbilityData.EventList` 是事件时间轴，事件由 `DataEvent` 表达。
- `DataEvent` 的公共事件信息包含 `TrackName`、`TrackIndex`、`TriggerType`、`TriggerTime`、`IsEnable`、`Duration`。
- `DataEvent.EventType` 决定具体 `IEventData` 类型，具体事件数据由各 `Event*` 类型负责。

存储形态现状：

- 1277 个 Ability JSON 文件是 `dict:B,E,P`。
- 5 个 `_index.json` 是 `dict:Files,GroupName,LastUpdate,StorageMode,TotalFiles`。
- `B` 对应 Ability 基础字段，当前位序为 `ID/Name/TotalTime`。
- `P` 对应变量列表，当前字段为 `K/V`。
- `E` 对应事件列表，事件基础字段在 `C` 数组，事件类型在 `T`，事件数据在 `D`。

边界说明：

- Ability JSON 细节分析已由独立任务完成：`Docs/Tasks/ABILITY_JSON_AUDIT_RESULT.md`。
- 后续新结构设计必须以 `AbilityData.cs`、`DataEvent.cs` 和具体 `IEventData` 类型为准，JSON 只作为旧存储格式证据。

任务结果摘要：

- 全部 1277 个有效 Ability JSON 均包含 `B/P/E` 三个顶层字段。
- `B` 全部为定长 3 元素数组，对应 `ID/Name/TotalTime`。
- `P` 总计 1449 个属性，`P.K` 全部为字符串，`P.V` 使用 `{T,V}` 多态值结构。
- `E` 总计 12983 个事件，事件公共字段 `C` 全部为定长 6 元素数组。
- `T` 均能映射到 `EventDataType`，实际使用 41 种事件类型，未发现未知类型。
- `D` 全部为对象，同一 `T` 下结构一致；但内部混合了具名字段和数组位序。
- 明显异常较少：3 个空事件列表文件、236 个无 `P` 文件、`WyvernData` 仅有索引无数据。

初步判断：

- `B/E/P` 这类短字段名和数组位序对运行时读取友好，但对人和 AI 不友好。
- Ability 更适合拆成：
  - `AbilityIndex.tsv`：ID、名称、类型、资源引用、入口 Graph。
  - `Graphs/Ability/*.json`：保留事件序列、触发器、参数和时间轴。
- Graph JSON 应使用语义字段名，不继续使用 `B/E/P/C/T/D` 作为权威源。
- `properties` 可以保留 `{type,value}` 多态值思想，但字段名必须语义化。
- `eventData` 必须逐事件类型定义 Schema，尤其要把旧数组位序字段转成具名字段。

## 6. SplitAIActionData 汇总

数量：

- 501 个 JSON。
- 500 个是动作数据。
- 1 个 `_index.json`。

结构签名：

- 500 个文件是 `dict:B`。
- `_index.json` 是 `dict:DataType,Files,LastUpdate,StorageMode,TotalFiles`。

代表片段：

```json
{
  "B": [
    459,
    "GS_LeapHeavyChop",
    1,
    0,
    941,
    7168,
    43,
    12000,
    8000,
    1,
    0,
    0,
    12583055
  ]
}
```

初步判断：

- AIAction 当前字段全靠数组位置表达，缺少自描述性。
- 基础字段应拆成 `AIActionIndex.tsv`。
- 复杂动作参数、条件和效果应进入 `Graphs/AIAction/*.json`。
- 详细位序和条件/效果结构见 `Docs/WGAME_SPLIT_GRAPH_AUDIT.md`。

## 7. SplitAIConfigData 汇总

数量：

| 子目录 | JSON 数量 |
|--------|-----------|
| `AIConfig` | 135 |
| `AIConfig_Defense` | 14 |
| `AIGoals` | 2 |

结构签名：

- 148 个文件是 `dict:B`。
- 3 个 `_index.json` 是 `dict:ConfigMode,DataType,Files,LastUpdate,StorageMode,TotalFiles`。

代表片段：

```json
{
  "B": [
    111,
    "MountainDragon_3",
    8,
    364,
    365,
    243,
    236,
    237,
    238,
    371,
    420,
    140,
    220
  ]
}
```

初步判断：

- AIConfig 是 AI 组合关系，天然偏结构型。
- 可拆为：
  - `AIConfigIndex.tsv`：ID、名称、模式、默认组。
  - `Graphs/AIConfig/*.json`：动作列表、目标、策略、权重。
- `AIConfig`、`AIConfig_Defense`、`AIGoals` 三个分组的 `B` 位序不同，必须拆成三个 Schema。

## 8. SplitBuffData 汇总

数量：

- 179 个 JSON。
- 178 个 Buff 文件。
- 1 个 `_index.json`。

结构签名：

- 178 个文件是 `dict:Buff,Type`。
- `_index.json` 是 `dict:DataType,Files,LastUpdate,StorageMode,TotalFiles`。

代表片段：

```json
{
  "Type": "Numerical",
  "Buff": {
    "Data": [
      70,
      "Stagger",
      null,
      "None",
      "RefreshAllTimeAndAdd",
      1,
      6000,
      200,
      true
    ],
    "N": [
      7,
      0,
      -50000,
      "",
      0,
      false,
      -1,
      false,
      1.0,
      false
    ]
  }
}
```

初步判断：

- Buff 已有 `Type`，比 Ability/AIAction 更容易分类。
- `Buff.Data` 的基础字段适合进 `Buff.tsv`。
- `Buff.N` 等类型特化参数应进入：
  - 类型化参数列，或
  - `Graphs/Buff/*.json`。
- 不建议继续让数组位序成为权威源。
- Buff 新结构必须用 `type` 区分不同 `data` Schema，不能做成一张超宽表。

## 9. 代码使用证据

当前运行时代码大量通过 `GameData.Tables` 读取表：

- `GameData.Tables.TbBuff`
- `GameData.Tables.TbAIAction`
- `GameData.Tables.TbCharacter`
- `GameData.Tables.TbTalentTree`
- `GameData.Tables.TbBlessing`
- `GameData.Tables.TbAbilityModifier`
- `GameData.Tables.TbSettingData`

代表调用：

- `BuffStatus.TryGetBuffCfg` 通过 `GameData.Tables.TbBuff.DataMap` 查 Buff。
- `ObjectPool` 读取 `TbObjectData`、`TbWeaponItems`、`TbAIAction`、`TbCharacter`。
- `TreeSlotData` 读取 `TbTalentTree` 后再跨表查 `TbBlessing`、`TbTalentEffect`、`TbAIAction`、`TbCharacterSkill`、`TbTalentAttribute`、`TbCharacter`。
- `TypeIDExtension` 集中封装了大量配置 ID 到描述、图标、AIAction、AbilityData 的转换。

初步判断：

- 表格型数据已经在运行时表现为 `Tb*` 表和 `DataMap` 查询。
- 新运行时统一格式应继续满足快速按 ID 查表。
- 结构型 Ability/AI/Buff Graph 需要通过 ID 和表格索引连接，不应让业务代码直接读文件路径。

## 10. 待继续汇总

下一轮审计建议继续补充：

- Luban/BaseData 主表字段索引已完成，剩余是逐表完整字段注释和低频表验证。
- BaseDataJson 33 张表名和主字段已完成索引，剩余是多语言差异比对。
- Ability JSON 已完成独立审计，下一步是把结果转成 Ability Graph Schema 草案。
- SplitBuffData 的 `Type` 分布已完成主干审计，剩余 CastOrb/条件细节待补。
- SplitAIActionData 的数组长度分布和字段位序含义已完成主干审计，剩余枚举名映射待补。
- 枚举映射主索引已完成，剩余是 flags 组合值解析和 UI 枚举域 JSON。
- 现有 bytes 的路径分类和是否为配置产物。
- `DataMgr`、`WAbilityMgr`、`GameData` 初始化链路。

只有完成这些汇总后，才进入正式的新结构设计。
