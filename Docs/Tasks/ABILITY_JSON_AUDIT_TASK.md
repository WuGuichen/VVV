# Ability JSON 审计任务

> **状态**: ✅ 已完成（详情见 `Docs/CAPABILITIES.md`）

> 目标：单独分析 WGame 现有 Ability JSON 的存储细节，为后续 Ability Graph 新结构提供证据。这个任务不直接修改框架代码。

## 1. 背景

主审计文档 `Docs/WGAME_DATA_AUDIT.md` 已确认：

- Ability 语义入口是 `WGame/Client/Assets/Scripts/ECS/Ability/AbilityData.cs`。
- 事件时间轴入口是 `WGame/Client/Assets/Scripts/ECS/Ability/DataEvent.cs`。
- Ability JSON 只是旧存储格式证据，不应反过来决定新框架语义。

本任务只负责补齐 JSON 侧事实。

## 2. 输入范围

分析以下目录：

- `WGame/Client/Assets/Res/SplitAbilityData/TestGroup/`
- `WGame/Client/Assets/Res/SplitAbilityData/SkillData/`
- `WGame/Client/Assets/Res/SplitAbilityData/MapTriggerData/`
- `WGame/Client/Assets/Res/SplitAbilityData/MapSettingData/`
- `WGame/Client/Assets/Res/SplitAbilityData/WyvernData/`

优先参考代码：

- `WGame/Client/Assets/Scripts/ECS/Ability/AbilityData.cs`
- `WGame/Client/Assets/Scripts/ECS/Ability/DataEvent.cs`
- `WGame/Client/Assets/Scripts/ECS/Ability/AbilityEvent/*.cs`
- `WGame/Client/Assets/Scripts/ECS/Ability/MapEvent/*.cs`

## 3. 需要产出

请补充一份独立报告，建议命名为：

- `Docs/Tasks/ABILITY_JSON_AUDIT_RESULT.md`

报告至少包含：

- 各子目录文件数和 `_index.json` 信息。
- Ability 基础字段 `B` 的位序确认。
- 属性字段 `P.K/P.V` 的类型分布。
- 事件字段 `E.C/T/D` 的位序和含义确认。
- 事件类型 `T` 的分布统计。
- 每个高频事件类型对应的 `IEventData` 类名。
- 每个高频事件类型的 `D` 字段结构。
- 明显异常样本：空字段、未知事件类型、字段长度不一致、引用缺失。
- 对新 Ability Graph JSON 的字段命名建议。

## 4. 验收标准

- 不要求手工阅读全部 JSON，但统计脚本必须可复现。
- 报告应区分事实、推断和建议。
- 每个字段含义必须能追溯到 `AbilityData.cs`、`DataEvent.cs` 或具体 `Event*` 类型。
- 不把旧字段名 `B/E/P/C/T/D` 作为新权威源字段名。
- 不在 WGameFramework 中提交 WGame 的真实 Ability 数据。

## 5. 后续衔接

完成后再回到框架设计：

- 更新 `Docs/WGAME_DATA_AUDIT.md` 的 Ability 部分结论。
- 更新 `Docs/CONFIG_FORMAT_STRATEGY.md` 的 Ability Graph 结构建议。
- 决定是否启动 `AbilityIndex.tsv` 和 `Graphs/Ability/*.json` 的试点 Schema。
