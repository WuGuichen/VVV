# Ability Runtime Graph 01D：Config Mapping

> 状态：Completed / Verified 2026-05-10
> 日期：2026-05-10
> 优先级：P1
> 父任务：`ABILITY_RUNTIME_GRAPH_01_V0_FOUNDATION.md`

## 目标

在 `MxFramework.Config.Runtime` 中提供 synthetic Ability Graph config DTO 和 mapper，把配置侧节点 / 边 / payload 转换为 01A 的 runtime graph definition。该任务是后续 JSON schema、Authoring Editor、WGame 迁移的种子，不接真实 WGame 数据。

## 建议写入范围

- `Assets/Scripts/MxFramework/Config.Runtime/AbilityGraph*.cs`
- `Assets/Scripts/MxFramework/Tests/Config/AbilityGraphConfigMappingTests.cs`
- 对应 `.meta`

不要修改 WGame 目录，不读取项目私有 JSON，不把 demo fixture 写死进 runtime mapper。

## 建议模型

```text
AbilityGraphConfig
  - id / version
  - entry node id
  - node configs
  - edge configs

AbilityGraphNodeConfig
  - node id
  - node kind
  - typed config fields for v0 payloads

AbilityGraphConfigMapper
  - map config DTO to AbilityGraphDefinition
  - run graph validation
  - return mapping result with config path errors
```

## 规则

- config mapper 输出必须带 source path / field path，方便后续 authoring UI 定位错误。
- mapper 不吞掉 01A validation error，应把 runtime validation 错误转换为 config diagnostics。
- 不使用反射猜字段，不依赖字段声明顺序。
- 不把本批之外的 cooldown、cost、formula DSL 偷偷塞进 DTO。

## 测试

至少覆盖：

- valid synthetic config 映射成 runtime graph。
- 节点和边顺序稳定。
- unresolved node id 输出带 config path 的错误。
- invalid payload 输出带 config path 的错误。
- mapper 不改变输入 config 集合。

## 验收

- 后续可以基于该 DTO 生成 JSON schema seed。
- Config.Runtime 继续保持可测试、无 UnityEditor、无 WGame 依赖。
- 不破坏现有 `ConfigAbilityFactoryTests` 和 runtime config change tests。

## 2026-05-10 实现记录

- 新增 synthetic `AbilityGraphConfig`、`AbilityGraphNodeConfig`、`AbilityGraphEdgeConfig` 和 typed payload config。
- 新增 `AbilityGraphConfigMapper` / mapping result / mapping diagnostic。
- mapper 显式转换到 `AbilityGraphDefinition`，运行 01A validation，并把 runtime error 回映射到 config `SourcePath` / `FieldPath`。
- 覆盖 valid config、稳定排序、unresolved node id、invalid payload、输入 config 不被修改。
