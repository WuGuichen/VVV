# Authoring Contract Ability 01：Ability Authoring Contract

> **状态**：Done（2026-05-09）
> **优先级**：P0
> **所属 Goal**：`PHASE11_RUNTIME_GAMEPLAY_GOAL.md` / M5
> **目标模块**：`MxFramework.Config.Runtime` + `MxFramework.Gameplay`

## Goal

在真正制作 Ability 可视化编辑器、AI 生成器或 WGame Ability JSON 映射前，先固定一份运行时无关的 Ability Authoring Contract。

完成后，上层工具应能通过同一份 contract 表达一个最小 Ability，并经过校验、错误码输出和映射，稳定生成当前运行时可执行的 `BasicAbilityConfig`。

## 背景

Phase 11 已完成：

- M1：`MxFramework.Gameplay` Runtime API。
- M2：`BasicAbilityConfig -> ConfigAbilityFactory -> SimpleAbility`。
- M3：Gameplay Diagnostic Snapshot。
- M4：Runtime Config Change Handling。

下一步如果直接做编辑器或 AI 生成，会让字段、错误码、中文说明和运行时映射分散在不同工具里。这个任务先把“工具写什么、运行时读什么、错误怎么说”固定下来。

## Scope

### 做

- 新增 Ability authoring contract 类型，建议放在 `Assets/Scripts/MxFramework/Config.Runtime/`。
- Contract 必须是纯数据结构，适合 JSON / 外部工具 / AI 生成，不依赖 `UnityEditor`，也不携带 WGame 业务类型。
- 字段至少覆盖当前 `BasicAbilityConfig` 能表达的最小能力：
  - Ability ID。
  - 显示名 / 说明。
  - 目标选择：`Self` / `SingleEnemy`。
  - 效果列表：`DamageByAttackDefense` / `ApplyBuff`。
  - Damage 参数：attack attribute id、defense attribute id、hp attribute id。
  - Buff 参数：buff id。
- 提供字段中文名和说明，供 AI 上下文、未来编辑器表单和校验提示复用。
- 提供稳定校验错误码，例如：
  - `MissingAbilityId`
  - `InvalidAbilityId`
  - `MissingDisplayName`
  - `UnknownTargetSelector`
  - `MissingEffect`
  - `UnknownEffectKind`
  - `MissingEffectParameter`
  - `InvalidAttributeId`
  - `InvalidBuffId`
  - `UnsupportedContractVersion`
- 提供 validator，返回结构化 issue / report，不只返回字符串。
- 提供 mapper，把合法 contract 转为 `BasicAbilityConfig` / `AbilityEffectConfig`。
- 提供 AI context / schema summary 的纯 C# 入口，能列出字段、中文名、类型、说明、允许值和错误码。
- 增加 EditMode 测试，覆盖：
  - 合法 contract 可映射为 `BasicAbilityConfig`。
  - 映射后的 Ability 可通过 `ConfigAbilityFactory` 创建并完成一次 cast。
  - 常见非法输入返回稳定错误码。
  - AI context / schema summary 包含核心字段和错误码。
- 更新文档：
  - `Docs/Interfaces/Gameplay.md`
  - `Docs/USAGE.md`
  - `Docs/CAPABILITIES.md`
  - `Docs/Tasks/PHASE11_RUNTIME_GAMEPLAY_GOAL.md`

### 不做

- 不做 Unity Editor 界面。
- 不做外部编辑器产品化。
- 不导入 WGame 真实 Ability JSON。
- 不接入 Luban / Entitas / WGame 私有运行时。
- 不做复杂公式 DSL。
- 不做 cooldown、cost、cast time、range、projectile、physics、navigation、animation、input。
- 不把 Demo 类型作为框架 API。

## Contract 设计建议

命名可按现有代码微调，但建议保持职责清晰：

```text
AbilityAuthoringContract
AbilityAuthoringEffectContract
AbilityAuthoringTargetSelectorKind
AbilityAuthoringEffectKind
AbilityAuthoringValidationCode
AbilityAuthoringValidationIssue
AbilityAuthoringValidationReport
AbilityAuthoringContractValidator
AbilityAuthoringContractMapper
AbilityAuthoringSchema
AbilityAuthoringFieldDescriptor
```

Authoring contract 是“工具输入层”，`BasicAbilityConfig` 是“运行时配置层”。两者不要混成一个类型：

```text
AI / Editor / JSON
  -> AbilityAuthoringContract
  -> Validate
  -> Map
  -> BasicAbilityConfig
  -> ConfigAbilityFactory
  -> SimpleAbility
```

## Public API 约束

- Runtime 程序集不得引用 `UnityEditor`。
- `MxFramework.Config.Runtime` 可以依赖 `MxFramework.Config`、`MxFramework.Gameplay`、`MxFramework.Attributes` / `Buffs` 的公共契约，但不能依赖 Demo。
- 错误码必须是稳定枚举或等价的稳定常量，测试应断言具体 code，不只断言 message。
- message 可以是中文，但 code 不应本地化。
- 映射失败必须可诊断，不允许静默丢弃 unknown target selector / unknown effect。
- 不要为了本任务扩大 `SimpleAbility`、`RuntimeEntity` 或 Combat API。

## Acceptance

完成时必须满足：

- 新增 contract / validator / mapper / schema summary。
- 至少一个合法 authoring contract 能生成可 cast 的 runtime ability。
- 至少 4 类非法输入返回稳定错误码。
- `Docs/Interfaces/Gameplay.md` 记录 authoring contract 与 `BasicAbilityConfig` 的关系。
- `Docs/USAGE.md` 给出最小 authoring contract -> runtime ability 示例。
- `Docs/CAPABILITIES.md` 和本 Goal 状态同步。
- 不引入 WGame 业务类型。
- 不引入 Unity Editor runtime 依赖。

## Suggested Verification

```bash
dotnet build WGameFramework.sln --no-restore -v minimal
```

Unity EditMode 测试建议至少跑：

```text
MxFramework.Tests.Ability
MxFramework.Tests.Config
```

如果测试 filter 名称不同，以实际新增测试 namespace / class 为准；最终在 closeout 里写清楚实际命令和结果。

影响面检查：

```bash
Tools/GitNexus/gitnexus.sh detect-changes
```

## Dispatch Prompt

请实现 `Docs/Tasks/AUTHORING_CONTRACT_ABILITY_01.md`。

你不是唯一在代码库里工作的 agent；不要 revert 其他人的改动，不要清理无关 Library / ProjectSettings / UserSettings 噪音。先读根目录 `AGENTS.md`、`Assets/AGENTS.md`、`Assets/Scripts/MxFramework/AGENTS.md`，再按任务文档实现。

主要负责范围：

- `Assets/Scripts/MxFramework/Config.Runtime/` 下新增 Ability authoring contract、validator、mapper、schema summary。
- `Assets/Scripts/MxFramework/Tests/` 下新增或扩展 Ability / Config Runtime EditMode 测试。
- 更新本任务列出的文档，并在任务文档末尾补充 closeout 记录。

实现时保持 contract 层和 runtime config 层分离：authoring contract 面向 AI / 编辑器 / JSON；`BasicAbilityConfig` 仍是运行时配置入口。不要做 UI，不要做 WGame JSON 导入，不要扩大玩法功能范围。

完成后请报告：

- 修改过的文件。
- 新增公共 API。
- 测试命令和结果。
- 未完成项或剩余风险。

## Closeout（2026-05-09）

实现内容：

- 在 `MxFramework.Config.Runtime` 新增 `AbilityAuthoringContract`、`AbilityAuthoringEffectContract`、工具层 selector/effect 枚举、稳定 `AbilityAuthoringValidationCode`、结构化 issue/report、validator、mapper 和 `AbilityAuthoringSchema` summary。
- mapper 只接受 validator 通过的 contract，并映射为当前运行时入口 `BasicAbilityConfig` / `AbilityEffectConfig`。
- 新增 `AbilityAuthoringContractTests`，覆盖合法映射、映射后通过 `ConfigAbilityFactory` 创建并 cast、非法输入稳定错误码、schema summary 核心字段和错误码。
- 更新 `Docs/Interfaces/Gameplay.md`、`Docs/USAGE.md`、`Docs/CAPABILITIES.md`、`Docs/Tasks/PHASE11_RUNTIME_GAMEPLAY_GOAL.md`。

验证记录：

- `dotnet build WGameFramework.sln -v minimal`：通过，11 个既有 warning，0 error。
- `dotnet build WGameFramework.sln --no-restore -v minimal`：通过，0 warning，0 error。
- Unity MCP EditMode `MxFramework.Tests.Config.AbilityAuthoringContractTests`：7/7 通过。
- Unity MCP EditMode `MxFramework.Tests.Config`：99/99 通过。
- Unity MCP EditMode `MxFramework.Tests.Ability`：25/25 通过。
- Unity EditMode batchmode：子代理首次尝试时因已有 Unity 实例打开同一个 WGameFramework 工程，batchmode 报错 `Multiple Unity instances cannot open the same project`；随后已改用当前 Unity MCP 实例完成测试。
- `Tools/GitNexus/gitnexus.sh detect-changes`：通过，Risk level low。

剩余风险：

- `DisplayName` / `Description` 当前直接映射为 `LocalizedTextKey` value；后续真实本地化导入器可在 mapper 外层改为项目自己的 key 生成策略。
