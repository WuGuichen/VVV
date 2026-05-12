# Ability JSON 审计报告

> Status: Frozen for Phase 9 closeout input
> Owner: Framework Producer / Codex Review
> Last Verified: 2026-05-09
>
> Fact Conclusions: 本文冻结 Ability JSON 的 `B/P/E` 顶层结构、事件公共字段、事件类型分布和 `D` 字段结构摘要；可作为 `AbilityIndex` 与 `AbilityGraph` Schema 种子输入。
>
> Pending Confirmation: 本文只审计 JSON 侧结构事实，不确认通用框架语义归属，不迁移真实 Ability 数据，不把 WGame 领域特化 property key 直接写入通用契约。

> **状态**: ✅ 已完成（详情见 `Docs/CAPABILITIES.md`）

> 目标：完成对 WGame Ability JSON 存储格式的全面审计，为 Ability Graph 新结构提供数据支撑。
>
> 本报告只审计 JSON 侧事实，不讨论框架语义。语义归属请参考 `Docs/WGAME_DATA_AUDIT.md`。
>
> 统计脚本：本地辅助脚本 `Tools/analyze_ability_json.py`（可复现，不抽取具体值，只分析结构）。这些脚本只用于生成本报告，不属于框架交付内容。
>
> 审计日期：2026-05-06

---

## 1. 数据范围

### 1.1 目录概况

| 目录 | JSON 文件数 | `_index.json` | 用途 |
|------|------------|--------------|------|
| **TestGroup** | 993 | ✅ | 角色动作/技能 Ability 配置文件 |
| **SkillData** | 51 | ✅ | 技能 Ability 配置（法球、陨石等） |
| **MapTriggerData** | 43 | ✅ | 地图触发器 Ability |
| **MapSettingData** | 190 | ✅ | 地图设定 Ability（环境、角色生成） |
| **WyvernData** | 0 | ✅ | 空目录，仅索引无数据 |

**事实（FACT）**: WGame 中 `WyvernData` 目前无任何 JSON 数据文件，但保留索引结构供后续使用。其余四个目录总计 1277 个有效 JSON 文件。

### 1.2 `_index.json` 结构

所有分组共享统一索引格式：

```json
{
  "GroupName": "TestGroup",
  "StorageMode": "Split",
  "LastUpdate": "2026-02-07 16:10:22",
  "TotalFiles": 993,
  "Files": [ ... ]
}
```

- `StorageMode` 始终为 `"Split"`（文件按 AbilityID 分拆存储）
- `Files` 数组包含每个文件的信息条目（结构未深入分析，但不影响 JSON 审计）

---

## 2. Ability 顶层结构

所有文件统一为三层结构：

```json
{
  "B": [ID, Name, TotalTime],
  "P": [ ... ],
  "E": [ ... ]
}
```

**事实（FACT）**: 三个顶层 key `B`、`P`、`E` 在全部 1277 个文件中一致出现，无例外。

### 2.1 `B`（Base）字段 — 位序确认

**来源**: `AbilityData.cs` 第 50-55 行 (`Deserialize` 方法)

```csharp
var cfg = jd["B"];
ID = JsonHelper.ReadInt(cfg[BASE_ID]);         // BASE_ID = 0
Name = JsonHelper.ReadString(cfg[BASE_NAME]);  // BASE_NAME = 1
TotalTime = JsonHelper.ReadInt(cfg[BASE_TOTALTIME]); // BASE_TOTALTIME = 2
```

**事实（FACT）**: B 字段始终为定长 3 元素数组，位序如下：

| 索引 | 字段名 | 类型 | 含义 | 源码常量 | 100%一致性 |
|------|-------|------|------|---------|----------|
| 0 | `ID` | `int` | Ability 唯一标识 | `BASE_ID = 0` | ✅ 1277/1277 |
| 1 | `Name` | `string` | Ability 名称（调试/显示用） | `BASE_NAME = 1` | ✅ 1277/1277 |
| 2 | `TotalTime` | `int` | 总时长（毫秒） | `BASE_TOTALTIME = 2` | ✅ 1277/1277 |

**异常**: 无。全部 1277 文件 B 长度均为 3，类型匹配预期。

### 2.2 `P`（Properties）字段 — K/V 类型分布

**来源**: `AbilityData.cs` 第 56-63 行

```csharp
var pList = jd["P"];
for (int i = 0; i < pList.Count; ++i) {
    var property = new CustomProperty();
    property.Deserialize(pList[i]);
    Properties.Add(property);
    Context.AddProperty(property.Name, property.Value.Value);
}
```

**事实（FACT）**: P 字段是 K/V 对象的数组。

```json
{
  "K": "speed",          // string, 属性键名
  "V": {
    "T": "Float",        // 类型标识: "Int" | "Float" | "String"
    "V": 1.0             // 实际值
  }
}
```

| 指标 | 值 |
|------|-----|
| 总 property 数 | 1449 |
| `P.K` 类型 | 100% `string` |
| `P.V.T` 分布 | `"Int"`=1108 (76.5%), `"Float"`=262 (18.1%), `"String"`=3 (0.2%) |
| 有 P 的文件 | 1041 (81.5%) |
| 无 P 的文件 | 236 (18.5%) |

**常见 P.K 键名（Top 10）**:

| 键名 | 出现次数 | 推断含义 |
|------|---------|---------|
| `T` | 993 | Hit pattern 数量（Type mask） |
| `speed` | 259 | 动作速率 |
| `WH` | 62 | 武器命中（Weapon Hit）次数 |
| `cool` | 19 | 冷却时间 |
| `OH` | 15 | 法球命中（Orb Hit）次数 |
| `triggerAll` | 14 | 全部触发标记 |
| `SH` | 13 | Sphere Hit 次数 |
| `roundTime` | 12 | 回合时间 |
| `chess` | 10 | 棋盘/棋局相关 |
| `teamLimit` | 9 | 队伍限制 |

**事实（FACT）**: P 字段的实际语义由 WGame 运行时通过字符串键名从 `TypeIDExtension` 常量查找。这些键名是领域特化的，不应照搬进通用框架。

### 2.3 `E`（Events）字段 — 概览

**来源**: `AbilityData.cs` 第 143-168 行（`DeserializeEvent`），`DataEvent.cs` 第 331-344 行（`Deserialize`）

**事实（FACT）**: 总计 12983 个事件分布在 1277 个 JSON 文件中。格式统一为：

```json
{
  "C": [TrackName, TrackIndex, TriggerType, TriggerTime, IsEnabled, Duration],
  "T": 0,
  "D": { ... }
}
```

---

## 3. `C` 字段 — Event Common（事件公共字段）

**来源**: `DataEvent.cs` 第 324-329 行（常量定义）、第 331-344 行（反序列化）

```csharp
private const int TRACK_NAME = 0;
private const int TRACK_INDEX = 1;
private const int TRIGGER_TYPE = 2;
private const int TRIGGER_TIME = 3;
private const int IS_NABLE = 4;
private const int DURATION = 5;
```

**事实（FACT）**: C 字段为定长 6 元素数组，12983/12983 事件全部一致。

| 索引 | 字段名 | C# 字段 | 类型 | 含义 | 100%一致性 |
|------|-------|---------|------|------|----------|
| 0 | `TrackName` | `_trackName` | `string` | 轨道名称（显示用，如"Animation 0"、"Break 0"） | ✅ |
| 1 | `TrackIndex` | `_trackIndex` | `int` | 轨道索引（排序关键词） | ✅ |
| 2 | `TriggerType` | `_triggerType` | `string` 枚举 | `"Signal"` 或 `"Duration"` | ✅ |
| 3 | `TriggerTime` | `_triggerTime` | `int` | 触发时间（毫秒，从 Ability 开始） | ✅ |
| 4 | `IsEnabled` | `IsEnable` | `bool` | 是否启用 | ✅ |
| 5 | `Duration` | `_duration` | `int` | 持续时间（毫秒，Signal 事件为 0） | ✅ |

**ETriggerType 枚举**（来源: `DataMgr.cs` 第 10-14 行）:

```csharp
public enum ETriggerType {
    Signal = 0,    // 瞬间触发，Duration=0
    Duration,      // 持续区间，TriggerTime~TriggerTime+Duration
}
```

**TriggerType 分布**:

| 触发类型 | 事件数 | 占比 |
|---------|-------|------|
| `Duration` | 6868 | 52.9% |
| `Signal` | 6115 | 47.1% |

**推理（INFERENCE）**: `Duration` 事件在 `TriggerTime` 触发开始，持续 `Duration` 毫秒后结束。`Signal` 事件在 `TriggerTime` 瞬间执行一次即完成。

---

## 4. `T` 字段 — Event Type（事件类型）

**来源**: `DataEvent.cs` 第 341 行

```csharp
EventType = (EventDataType)JsonHelper.ReadInt(jd["T"]);
```

**事实（FACT）**: T 为整数，映射到 `EventDataType` 枚举（定义于 `EventDataType.cs`，共 67 个枚举值，0-66）。

### 4.1 事件类型分布（全部）

| T | 枚举名 | 事件数 | 占比 | 领域含义 |
|---|--------|-------|------|---------|
| 28 | `SetMoveParam` | 3047 | 23.5% | 设置移动参数（移动模式、速度等） |
| 29 | `Interrupt` | 2514 | 19.4% | 可被打断标记（Break 轨道） |
| 0 | `PlayAnim` | 1064 | 8.2% | 播放动画 |
| 32 | `MapAddCharacter` | 836 | 6.4% | 地图添加角色 |
| 26 | `CostMP` | 553 | 4.3% | 消耗精力值 |
| 13 | `FocusDoFaceTo` | 497 | 3.8% | 锁定目标/面向目标 |
| 25 | `ActionPoise` | 493 | 3.8% | 设置出手韧性 |
| 3 | `PlayAudio` | 450 | 3.5% | 播放音频 |
| 59 | `PlayAudioPreset` | 445 | 3.4% | 播放预设音频 |
| 24 | `OpenWeapon` | 432 | 3.3% | 开启武器命中框 |
| 10 | `FocusKeepDist` | 384 | 3.0% | 锁定目标/保持距离 |
| 23 | `SetState` | 306 | 2.4% | 设置角色状态 |
| 61 | `SetGroundHitEffect` | 294 | 2.3% | 设置地面碰撞特效 |
| 19 | `AddHitTriggerSphere` | 250 | 1.9% | 添加球形攻击检测 |
| 12 | `FocusDoApproach` | 246 | 1.9% | 锁定目标/靠近目标 |
| 7 | `DoAction` | 161 | 1.2% | 行为动作（复合操作） |
| 6 | `AddBuff` | 114 | 0.9% | 添加 Buff |
| 16 | `TriggerStateToMotion` | 108 | 0.8% | 状态切换 Motion |
| 64 | `EventAddSpineRotation` | 102 | 0.8% | 添加脊椎旋转 |
| 1 | `PlayEffectCharacter` | 80 | 0.6% | 播放角色特效 |
| ... | 其余 27 种类型 | 557 | 4.3% | 低频事件类型（<0.5% 每种） |

**事件类型完整列表（全 67 种枚举）** 见附录 A。

### 4.2 未知事件类型

**事实（FACT）**: 实际数据中未发现无法映射到 `EventDataType` 枚举的 T 值。

---

## 5. `D` 字段 — Event Data（事件数据）

**来源**: 每个 `EventDataType` 枚举值对应一个 `IEventData` 实现类。反序列化入口在 `DataEvent.cs` 第 68-273 行（`GetEventData` 工厂方法）和第 343 行。

```csharp
(_eventData as IData).Deserialize(jd["D"]);
```

**事实（FACT）**: D 字段始终为对象（`dict`），内部 key 和结构完全由 `IEventData` 实现类决定。

### 5.1 D 字段结构速查表（Top 20 高频事件）

| T | 事件类 | D Key | D 结构（数组/对象） | 元素数 | 来源文件 |
|---|-------|-------|-------------------|--------|---------|
| 28 | `EventSetMoveParam` | `"Data"` | `array[int, int]` | 2 | — |
| 29 | `EventInterrupt` | `"Type"` | `int` | 1 scalar | `EventInterrupt.cs` |
| 0 | `EventPlayAnim` | `"AnimCfg"` | `array[str, int×3, bool, int×3]` | 8 | `EventPlayAnim.cs` Deserialize |
| 32 | `MapEventAddCharacter` | `"M"` | `array[dict, float×4, int×3, bool×2]` | 10 | — |
| 26 | `EventCostMP` | `"V"` | `int` | 1 scalar | `EventCostMP.cs` Deserialize |
| 13 | `EventFocusDoFaceTo` | `"V"` | `array[int×2, bool, float, bool]` | 5 | — |
| 25 | `EventSetActionPoise` | `"D"` | `int` | 1 scalar | — |
| 3 | `EventPlayAudio` | `"C"` | `array[int×5]` | 5 | — |
| 59 | `EventPlayAudioPreset` | `"T"` + `"P"` | `int` + `int` | 2 scalars | — |
| 24 | `EventOpenWeapon` | `"D"` | `array[int×12]` | 12 | `EventOpenWeapon.cs` Deserialize |
| 10 | `EventFocusKeepDist` | `"Offset"` | `int` | 1 scalar | — |
| 23 | `EventSetState` | `"States"` | `int` | 1 scalar (bitmask) | — |

**关键发现**: D 字段内部使用两种模式：

1. **标量/对象模式**（如 `Interrupt.Type`、`CostMP.V`、`DoAction.Type+Param`）：使用具名字段，反序列化为显式属性。
2. **数组模式**（如 `PlayAnim.AnimCfg`、`OpenWeapon.D`、`PlayEffectCharacter.Data`）：使用固定位置数组，反序列化通过索引 `data[0]`, `data[1]` 等。这是旧格式的历史遗留。

**建议（RECOMMENDATION）**: 在 Ability Graph 新结构中，放弃数组模式，全部使用具命名字段。

### 5.2 D 字段结构一致性

**事实（FACT）**: 每个 `T` 值的 D 字段结构完全一致。在 41 个实际使用的类型中，没有发现同一类型下的结构变体。未发现非 dict 类型的 D 字段。

---

## 6. 明显异常样本

### 6.1 整体一致性结论

| 检查项 | 结论 |
|-------|------|
| B 字段长度 | 全部 len=3，0 异常 |
| C 字段长度 | 全部 len=6，0 异常 |
| D 字段类型 | 全部为 dict，0 异常 |
| T 值映射 | 全部可映射到 EventDataType 枚举，0 异常 |
| P.K 类型 | 全部为 string，0 异常 |
| P.V 结构 | 全部为 `{T, V}` 格式，0 异常 |

### 6.2 统计异常

| 异常 | 数量 | 详情 |
|------|------|------|
| **空事件列表** | 3 文件 | `0045_ANewAbility.json`（测试占位）、`0043_教程_棋盘操作.json`（教程触发器）、`0032_Empty.json`（空地图设定） |
| **无 P 属性** | 236 文件 | 18.5% 的 Ability 没有自定义属性，通常为简单 Action 或纯事件链 |
| **无文件** | WyvernData | 保持索引但无实际数据 |

### 6.3 数据质量评价

**推理（INFERENCE）**: 整体数据质量很高。基本字段高度规范化，没有发现字段长度不一致、类型错误、未知枚举值等常见数据问题。说明 WGame 的序列化/反序列化代码是严格的双向对称（bidirectional）的。

---

## 7. 对新 Ability Graph JSON 的字段命名建议

基于 JSON 审计结果，不建议沿用旧单字母命名方式（`B`, `E`, `C`, `T`, `D`, `P`, `K`, `V`）。以下是对应关系和建议命名：

### 7.1 Ability 顶层

| 旧字段 | 建议命名 | 理由 |
|--------|---------|------|
| `B` | `base` / `baseFields` | 基础字段组，具名对象比数组更可读 |
| `P` | `properties` | 自定义属性列表 |
| `E` | `events` / `timeline` | 事件时间线（"timeline" 更语义化） |

### 7.2 B 字段展开

| 旧索引 | 建议命名 | 预期类型 |
|--------|---------|---------|
| B[0] | `id` | `int` |
| B[1] | `name` | `string` |
| B[2] | `totalTimeMs` | `int` |

### 7.3 事件公共字段（旧 C 数组 → 具名对象）

| 旧索引 | 建议命名 | 旧名 | 预期类型 |
|--------|---------|------|---------|
| C[0] | `trackName` | `C[0]` | `string` |
| C[1] | `trackIndex` | `C[1]` | `int` |
| C[2] | `triggerType` | `C[2]` | `enum: "Signal" | "Duration"` |
| C[3] | `triggerTimeMs` | `C[3]` | `int` |
| C[4] | `enabled` | `C[4]` | `bool` |
| C[5] | `durationMs` | `C[5]` | `int` |

### 7.4 属性字段（旧 P）

| 旧字段 | 建议命名 | 旧名 |
|--------|---------|------|
| `P[].K` | `name` | `K` |
| `P[].V.T` | `type` | `V.T` |
| `P[].V.V` | `value` | `V.V` |

### 7.5 事件类型和数据（旧 T/D）

| 旧字段 | 建议命名 | 备注 |
|--------|---------|------|
| `T` | `eventType` | 保留数值枚举或改为字符串枚举名 |
| `D` | `eventData` | 内层结构由具体类型定义 |

**推理（INFERENCE）**: 旧 JSON 使用单字母字段名是 LitJson 序列化/编辑器直出的历史产物，便于最小化存储体积。新 Ability Graph JSON 是只读配置，不需要兼容不人性化的序列化格式，应当优先可读性。

---

## 8. 后续建议

1. **继续保留 P 的双层结构**（`{type, value}`）—— 这是合理的多态值设计，适合直接进入新框架，只需改名。
2. **事件 D 字段需要统一模式**—— 当前混合使用数组索引和具名字段。新结构应全部使用具名字段，为每种事件类型定义明确的 Schema。
3. **数组模式的事件 D 字段**需要逐个转换为具名结构。总共约 41 种事件类型需要定义 Schema，但其中数组模式的类型不到 20 种（主要是高频类型）。
4. 本审计与 `Docs/WGAME_DATA_AUDIT.md` 的 Ability 交叉引用后，可以在 `Docs/CONFIG_FORMAT_STRATEGY.md` 中补充 Ability Graph 结构草案。

---

## 附录 A: EventDataType 枚举完整列表

来源: `EventDataType.cs`（`WGame/Client/Assets/Scripts/Ability/Data/Event/EventDataType.cs`）

| T | 枚举名 | 中文名 | 发现次数 |
|---|--------|--------|---------|
| 0 | `PlayAnim` | 播放动画 | 1064 |
| 1 | `PlayEffectCharacter` | 播放角色特效 | 80 |
| 2 | `PlayEffectDuration` | 持续特效 | 51 |
| 3 | `PlayAudio` | 播放音频 | 450 |
| 4 | `NoticeMessage` | 发送通知 | 0 |
| 5 | `AddMessageReceiver` | 接收通知 | 67 |
| 6 | `AddBuff` | 添加 Buff | 114 |
| 7 | `DoAction` | 行为动作 | 161 |
| 8 | `LockTick` | 锁定时间状态(已废弃) | 6 |
| 9 | `SetTimeScale` | 时间倍率(编辑器有效) | 0 |
| 10 | `FocusKeepDist` | 对锁定目标/保持距离 | 384 |
| 11 | `FocusDoForce` | 对锁定目标/给目标施加力 | 0 |
| 12 | `FocusDoApproach` | 对锁定目标/靠近目标 | 246 |
| 13 | `FocusDoFaceTo` | 对锁定目标/面向目标 | 497 |
| 14 | `FinishTargetHit` | 对终结技目标/造成伤害 | 2 |
| 15 | `TriggerInputToMotion` | 输入切换 Motion | 60 |
| 16 | `TriggerStateToMotion` | 状态切换 Motion | 108 |
| 17 | `TriggerInputToAbility` | 输入切换 Ability | 12 |
| 18 | `TriggerStateToAbility` | 状态切换 Ability | 2 |
| 19 | `AddHitTriggerSphere` | 球形攻击检测 | 250 |
| 20 | `AddHitTriggerBox` | 盒型攻击检测 | 0 |
| 21 | `SetOwnerProperty` | 设置角色参数 | 0 |
| 22 | `SetOwnerAttr` | 设置角色属性 | 6 |
| 23 | `SetState` | 设置状态 | 306 |
| 24 | `OpenWeapon` | 开启武器命中 | 432 |
| 25 | `ActionPoise` | 出手韧性 | 493 |
| 26 | `CostMP` | 消耗精力 | 553 |
| 27 | `SetTimeArea` | 时间区域 | 1 |
| 28 | `SetMoveParam` | 移动参数 | 3047 |
| 29 | `Interrupt` | 打断 | 2514 |
| 30 | `MoveToPoint` | 移动到目标点 | 78 |
| 31 | `MoveToSpecialPoint` | 移动到特定目标点 | 8 |
| 32 | `MapAddCharacter` | 地图添加角色 | 836 |
| 33 | `MapTriggerInputToAction` | 地图/输入监听 | 2 |
| 34 | `MapTriggerSpherePortal` | 地图/球形传送 | 4 |
| 35 | `MapTriggerDoAction` | 地图/单个行为 | 67 |
| 36 | `MapTriggerDoActionCondition` | 地图/条件行为-单个 | 14 |
| 37 | `MapTriggerDoMultiAction` | 地图/多个行为 | 6 |
| 38 | `MapTriggerDoMultiActionCondition` | 地图/条件行为-多个 | 8 |
| 39 | `MapTriggerEvent` | 地图/事件 | 0 |
| 40 | `MapTriggerNPC` | 地图/NPC | 3 |
| 41 | `MapTriggerDialog` | 地图/对话 | 29 |
| 42 | `SetWeaponPosition` | 武器位置 | 0* |
| 43 | `SetWeaponRotation` | 武器旋转 | 0* |
| 44 | `EventAddRayHitTrigger` | 射线攻击检测 | 0 |
| 45 | `EventAddSectorHitTrigger` | 扇形攻击检测 | 12 |
| 46 | `EventCastSkill` | 释放技能 | 8 |
| 47 | `MapTriggerDialogDecorate` | 地图/对话修饰 | 16 |
| 48 | `MapTriggerDurationAction` | 地图/持续检测 | 1 |
| 49 | `MapTriggerEventAction` | 地图/事件监听 | 0 |
| 50 | `MapTriggerMotionToAction` | 地图/动作监听 | 0 |
| 51 | `MapTriggerItem` | 地图/物品 | 2 |
| 52 | `MapTriggerMoveNPC` | 地图/NPC 移动 | 6 |
| 53 | `EventPlayEffectSkill` | 播放技能特效 | 22 |
| 54 | `EventSetFakeShadow` | 设置幻影/嘲讽 | 6 |
| 55 | `SetStatusBuff` | 设置状态 Buff | 11 |
| 56 | `EventOpenShield` | 开启盾牌 | 45 |
| 57 | `TargetStateToAbility` | 目标状态切换 Ability | 15 |
| 58 | `EventSummon` | 召唤 | 1 |
| 59 | `PlayAudioPreset` | 播放音频预设 | 445 |
| 60 | `SetHitEffectOpen` | 碰撞效果开启 | 0 |
| 61 | `SetGroundHitEffect` | 地面碰撞特效 | 294 |
| 62 | `EventGroundSkill` | 地面技能 | 25 |
| 63 | `FocusDoAttach` | 对锁定目标/吸附目标 | 6 |
| 64 | `EventAddSpineRotation` | 脊椎旋转 | 102 |
| 65 | `EventSetAttackShadow` | 设置幻影/攻击 | 2 |
| 66 | `TriggerToAbility` | 检测触发切换到能力 | 3 |

注: 标记 `*` 的事件类型可能在 Editor 编辑中用到但未出现在已导出的 JSON 数据中。

---

## 附录 B: D 字段结构详细参考

（提取自实际 JSON 数据的 Top 20 类型样本，每个字段对应 C# 类的序列化结构）

### T=28 (SetMoveParam)
```json
D: { "Data": [moveMode(int), speed(int)] }
```
- Data[0]: 移动模式（enum int）
- Data[1]: 速度百分比

### T=29 (Interrupt)
```json
D: { "Type": interruptType(int) }
```
Type 为位掩码，表示允许被打断的类型

### T=0 (PlayAnim)
```json
D: { "AnimCfg": [animName(str), offsetStart(int), offsetEnd(int), transDuration(int), layerType(int), resetLayer(bool), speedRate(int), bone(int)] }
```
来源: `EventPlayAnim.cs` Deserialize (第 48-60 行)

### T=32 (MapAddCharacter)
```json
D: { "M": [posDict, rotW, rotX, rotY, rotZ, charID, aiID, groupID, isBoss, isElite] }
```

### T=26 (CostMP)
```json
D: { "V": cost(int) }
```
来源: `EventCostMP.cs` Deserialize

### T=13 (FocusDoFaceTo)
```json
D: { "V": [mode(int), speed(int), stopOnFinish(bool), angularSpeed(float), keepFace(bool)] }
```

### T=25 (ActionPoise)
```json
D: { "D": actionPoise(int) }
```

### T=3 (PlayAudio)
```json
D: { "C": [audioID(int), audIdx1, audIdx2, audIdx3, audIdx4] }
```
五个 int 构成音频预设参数

### T=24 (OpenWeapon)
```json
D: { "D": [dmgRate(int), impact(int), dmgPoise(int), shieldBreak(int), part(int), updateSpan(int), dmgType(int), buffCount(int), buffIDs..., eleType(int), eleValue(int), isAddBuffByHitCount(bool)] }
```
来源: `EventOpenWeapon.cs` Deserialize (第 61-81 行)

---

## 附录 C: 本地辅助脚本清单

以下脚本只用于生成本报告，不属于框架运行时、编辑器功能或正式配置工具链。

| 文件 | 用途 |
|------|------|
| `Tools/analyze_ability_json.py` | 主统计：文件计数、字段分布、事件类型分布、异常检测 |
| `Tools/analyze_extended.py` | 扩展分析：`_index.json` 结构、`P.V` 类型系统、`D` 样本收集 |
| `Tools/deep_dive_d.py` | 深度 D 字段：每种事件类型的 D 结构样本和 C 样本 |
| `Tools/analyze_misc.py` | 杂项：MapSettingData 结构验证、TriggerType 分布、空事件文件 |
| `Tools/debug_sample.py` | 调试：单文件样本验证 |

---

*报告完毕。本报告仅审计 JSON 存储格式，不讨论语义模型。*
