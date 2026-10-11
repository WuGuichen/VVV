# Character Authoring SDK 与 CharacterStudio 开发基准

> Status: Design
>
> Decision: Accepted
>
> Authority: Normative SDK Boundary
>
> Version: 1.0
>
> Updated: 2026-07-13
>
> Current implementation baseline: Character Application 7.0 / Global Weapon Library 5.0

## 1. 结论

角色创作能力必须按“无界面 SDK + 接入层 + 第一方编辑器 + Unity 桥 + Runtime 消费端”分层：

```text
Character Authoring SDK
├─ DTO / Schema
├─ Validation / Diagnostics
├─ Compiler / Deterministic Hash
├─ Migration / Conflict Detection
└─ Import / Resource Write Plan

Authoring CLI / Local Service
└─ 将 SDK 暴露为命令行和低频 HTTP API

CharacterStudio
└─ 使用上述 API 的第一方可视化角色编辑器

Unity Import Bridge
└─ 消费 Write Plan，在 Unity Editor 内构建资产并回写验收状态

Runtime
└─ 只消费已编译、已导入且通过门禁的运行时产物
```

**CharacterStudio 不是 SDK。** 它是 SDK 的第一方参考客户端。第三方编辑器、批处理工具、Development Agent 自动化和 Unity Bridge 必须复用同一套 SDK 契约，不得各自实现 Schema、校验、编译或资源推导。

当前仓库已经具备项目内 SDK 的主要能力，承载于 `MxFramework.Authoring.Core`、CLI、本地 Authoring Service 和相关结果 DTO；但尚未完成独立分发的公共 SDK 产品。没有完成本文第 13 节之前，不得宣称已交付对外稳定的 Character Authoring SDK。

## 2. 文档权威范围

本文只负责以下规范：

- SDK、CharacterStudio、Authoring Service、Unity Import Bridge 和 Runtime 的职责边界。
- SDK 应公开的能力类别、兼容策略、事务语义和结果契约。
- 从角色源数据到 Runtime Ready 的阶段门禁。
- 第三方创作工具接入 SDK 时必须遵守的规则。

本文不复制具体角色或武器 Schema。专项事实源为：

- 单一武器、公共组合、动画、受击、冻结能力兼容、角色槽位及角色组合覆盖：[`CHARACTER_WEAPON_CONFIGURATION.md`](CHARACTER_WEAPON_CONFIGURATION.md)。
- Character Application、Compiler、Resolver、Unity Bridge 和 Runtime 接口：[`Interfaces/CharacterApplication.md`](Interfaces/CharacterApplication.md)。
- 角色资源包所有权与内容结构：[`CHARACTER_RESOURCE_PACKAGE_AUTHORING.md`](CHARACTER_RESOURCE_PACKAGE_AUTHORING.md)。
- 外部主创工具总体原则：[`AUTHORING_EDITOR_PROGRAM.md`](AUTHORING_EDITOR_PROGRAM.md)。

发生冲突时，具体数据字段以对应 Schema 和接口文档为准；SDK 分层、依赖方向和就绪定义以本文为准。历史任务文档和生成产物不得反向覆盖这些基准。

## 3. 术语与当前成熟度

| 名称 | 定义 | 当前状态 |
| --- | --- | --- |
| Character Authoring SDK | 无界面的角色创作核心能力和稳定结果契约 | 项目内可用；尚未形成独立分发包 |
| Authoring Core | 当前 SDK 的 C# 实现载体，不引用 Unity API | 已实现 |
| Authoring CLI | SDK 的命令行适配器 | 已实现 |
| Authoring Service | CharacterStudio 等本地客户端使用的低频 HTTP 适配器 | 已实现；仅面向本地创作流程 |
| CharacterStudio | 第一方浏览器角色编辑器和 SDK 参考客户端 | 已实现并持续迭代 |
| Resource Manager | 公共资源生命周期、ResourceKey、导入修正和资源选择工具 | 已实现；不是角色规则编辑器 |
| Animation Editor | 全局动画配置的专用编辑器 | 已接入 CharacterStudio |
| Unity Import Bridge | 唯一允许在 Unity 项目内执行角色资产写入和构建的适配层 | 已实现 |
| Runtime | 消费生成配置、ResourceKey、Prefab、CharacterAction 和 Trace Bake 的运行端 | 已接入；不得读取 Authoring 源文件 |
| Public SDK Distribution | 可供仓库外消费者安装并获得兼容承诺的 SDK 产品 | 未完成 |

README 中的“.NET 9+ SDK”只是运行 CLI/服务所需的开发环境，不等同于本文定义的 Character Authoring SDK。

## 4. 强制依赖方向

```text
CharacterStudio / Third-party Editor / Automation
                         │
                         ▼
              Authoring Service / CLI
                         │
                         ▼
              Character Authoring SDK
                  │              │
                  ▼              ▼
       Character Source     Global Libraries
                  │              │
                  └──────┬───────┘
                         ▼
               Deterministic Outputs
                         │
                         ▼
               Unity Import Bridge
                         │
                         ▼
               Generated Unity Assets
                         │
                         ▼
                       Runtime
```

必须遵守：

- Authoring Core 不引用 `UnityEngine`、`UnityEditor` 或具体游戏业务程序集。
- CharacterStudio 和第三方编辑器不得复制 Compiler、Resolver、迁移或诊断规则。
- Authoring Service 只做协议、文件事务和进程协调，不创建第二套业务事实。
- Unity 资产写入、Avatar、Prefab、CharacterAction Registry 和 Weapon Trace Bake 只能由 Unity Import Bridge 完成。
- Runtime 不读取 `ArtSource`、CharacterStudio 草稿、Global Weapon Library 或 Authoring API。
- Resource Manager 管资源生命周期；CharacterStudio 管角色装配和角色配置，二者不得互相接管所有权。

## 5. SDK 能力边界

Character Authoring SDK 至少包含以下能力类别：

### 5.1 数据与 Schema

- 角色资源包、角色配置、几何、资源引用和编译结果 DTO。
- Schema 版本识别、序列化 round-trip 和规范化排序。
- 稳定 ID、路径、枚举和跨文档引用校验。

### 5.2 加载与校验

- 从显式工作区和包路径加载数据，不依赖当前进程目录猜测。
- 返回结构化诊断：稳定 code、severity、source、field、message 和 suggested fix。
- 区分可保存草稿、Compile 阻断、Unity Import 阻断与 Runtime Spawn 阻断。

当前实现入口包括 `CharacterResourcePackage`、`CharacterResourcePackageValidator`、`CharacterWeaponConfigurationValidator` 和 `CharacterAuthoringValidationReport`。这些类型是当前实现证据，不自动等于已经冻结的仓库外公共 API。

### 5.3 编译与计划

- 通过 `CharacterAuthoringCompiler` 生成确定性的配置 patch、geometry binding、resource mapping 和 Unity write plan。
- source hash 必须覆盖实际参与编译的角色数据和全局配置。
- 相同规范化输入必须生成稳定排序、稳定 ID、稳定 hash 和等价诊断。
- Compiler 不直接写 Unity 资产，也不读取 Unity Editor 状态。

### 5.4 迁移与冲突

- Migrator 只读取明确支持的旧版本并输出目标版本草稿。
- 涉及角色包和全局库时执行双 `expectedRevision` 校验、事务备份和完整回滚。
- 不根据名称、标签、武器类别或资源文件名猜测配置关系。
- 无法无损迁移时保留可编辑草稿并产生稳定阻断诊断，不静默丢弃或覆盖数据。

### 5.5 导入与验收结果

- 输出 Unity Import Write Plan，而不是从 SDK 内调用 Unity API。
- Authoring 写入报告与 Unity Editor 执行状态必须分开建模。
- `Skipped` 只表示目标内容 hash 已匹配，不能证明 Prefab、Avatar、Registry、Trace Bake 或 Spawn Ready。
- `Temp/MxFrameworkAuthoring/CharacterImport/status` 只承载当前 Unity Editor 会话的 pending/running/failed/completed IPC，不进入版本管理，也不是跨会话 Ready 的唯一证据。
- Unity Editor 只有在 Avatar、Prefab、CharacterAction Registry、默认武器、Weapon Trace/Socket Bake 和 Spawn 门禁全部通过后，才能原子写入 `Assets/MxFrameworkGenerated/CharacterPackages/<packageId>/package_cache/unity_editor_import_receipt.json`。
- `unity_editor_import_receipt.json` 使用独立 Receipt Schema 1.0，只保存稳定的成功验收摘要和当前 Compiler/Unity artifact hash；不得保存 request ID、时间、消息、绝对路径或机器信息。相同输入重复导入必须产生相同内容。
- 当前 hash 没有会话证据或有效 receipt 时，状态是 `Deferred / 待执行 Unity 导入`，不是角色内容错误；当前匹配会话明确失败时仍是 `Blocked`，不得被旧 receipt 掩盖。

## 6. 数据所有权

| 数据 | 权威所有者 | 允许编辑入口 | Runtime 是否直接读取 |
| --- | --- | --- | --- |
| 角色主体 FBX、可选贴图及角色自有源文件 | `ArtSource/Characters/<characterId>` | CharacterStudio / Resource Manager 委托流程 | 否 |
| 公共模型、动画 Clip、材质、贴图、Prefab 和 ResourceKey | Resource Manager 与公共资源清单 | Resource Manager | 否 |
| 单一武器、公共组合、全局动画、全局受击和冻结能力兼容数据 | Global Weapon Library | CharacterStudio / Animation Editor | 否 |
| 角色主副手槽位、公共组合 bindings 和可选行为覆盖 | Character Resource Package | CharacterStudio | 否 |
| 公共组合动作默认 | Global Weapon Library | CharacterStudio 的组合工作台 | 否 |
| 角色动作覆盖 | Character Resource Package | CharacterStudio 的组合工作台 | 否 |
| 编译 patch、resource plan 和 write plan | Character Authoring SDK | 不允许手工编辑 | 否 |
| Unity Prefab、Avatar、Registry 和 Trace Bake | Unity Import Bridge | Unity Editor 桥接流程 | 是 |
| Unity 会话状态和成功验收 receipt | Unity Import Bridge | Unity Editor 桥接流程 | 否；只作为 Authoring/Unity 验收证据 |
| Runtime EquipmentState 和可生成角色目录 | 编译/导入投影产物 | 不允许手工编辑 | 是 |

Humanoid 主体以 FBX 为唯一权威源；GLB/glTF 只用于非 Humanoid、静态模型或自动生成的预览产物。公共武器是独立公共资源，不复制进角色源清单。具体武器姿态、Trace 和角色槽位所有权以武器配置基准为准。

## 7. 版本与兼容策略

以下版本必须独立演进，禁止用一个数字代替全部兼容性：

| 版本域 | 当前基线 | 说明 |
| --- | --- | --- |
| Character Application Schema | 7.0 | 角色公共组合 bindings、行为覆盖和槽位引用 |
| Global Weapon Library Schema | 5.0 | 单一武器、公共组合、行为默认和冻结能力兼容数据 |
| Character Authoring SDK | 项目内版本，尚未对外冻结 | C# API 与结果契约版本 |
| Authoring HTTP API | 本地服务兼容接口 | CharacterStudio/工具适配层版本 |
| Generated Output Format | 由各结果 DTO 的 format/schema 标识 | Compiler 与 Unity Bridge 的交接版本 |
| Unity Editor Import Receipt Schema | 1.0 | 成功验收证据；不改变 Character Application 或 Global Weapon Library Schema |

兼容规则：

- Reader 可以为 Migrator 读取受支持旧版本；Compiler 只接受当前可发布版本。
- 新增可选字段且旧消费者可安全忽略时可视为向后兼容；删除、改名、改变默认语义或 stable identity 均为破坏性变更。
- 稳定诊断 code、结果状态和 source hash 语义属于兼容面，不能随意修改。
- SDK、CLI、服务和 CharacterStudio 必须在启动或请求时报告其支持的 Schema/format 范围；不兼容时明确拒绝，不做最佳猜测。
- 正式对外 SDK 发布后采用独立语义版本，并维护支持矩阵和弃用周期。

## 8. 保存、事务与并发

- 角色包和全局库分别拥有 revision；跨文件操作必须同时携带预期 revision。
- 单配置 API 可以推进全局库统一 revision，但不能伪装成每条记录独立 revision。
- `409` 冲突必须要求刷新后重放修改；默认禁止强制覆盖和字段级自动合并。
- 保存前校验只决定草稿能否写入；Compile、Unity Import 和 Spawn 各自拥有更严格门禁。
- 事务失败必须完整回滚内容、revision 和备份状态。
- 删除全局配置前必须完成跨角色引用扫描；存在引用或引用状态未知时硬阻止。

## 9. CharacterStudio 产品边界

CharacterStudio 是 SDK 的第一方参考实现，负责把结构化能力呈现为可理解的创作流程，但不拥有新的数据语义。

当前一级导航为：

```text
绑定 / 单一武器 / 组合武器 / 原材料 / 运行验证
```

- “绑定”编辑角色骨骼、Socket 和主副手槽位姿态。
- “单一武器”编辑全局单一武器、武器自身变换、Trace 和武器属性。
- “组合武器”编辑公共主副手组合与行为默认，并维护当前角色 binding、默认选择和可选覆盖。
- 深度动画编辑委托 Animation Editor；公共资源生命周期委托 Resource Manager。
- 能力 Authoring 当前为 `Frozen / Experimental Compatibility`，不进入普通导航、完成条件或下一步；仅在高级兼容诊断中只读显示历史数据。
- CharacterStudio 可以保留未完成草稿，但必须使用 SDK 诊断明确阻止无效 Compile、Unity Import 或 Spawn。
- 原始 JSON 只能作为只读审计或明确标注的高级兼容入口，不能成为普通结构化编辑的替代品。

第三方编辑器不要求复制 CharacterStudio UI，但必须产生相同的规范化数据、诊断、编译结果和门禁判断。

## 10. 端到端阶段与门禁

正式流程固定为：

```text
1. Load Source and Revisions
2. Validate Draft
3. Compile Character Package
4. Build Resource / Unity Write Plan
5. Write Authoring Outputs
6. Unity AssetDatabase Import
7. Avatar and Prefab Build
8. CharacterAction Library Merge and Registry Build
9. Weapon Trace and Socket Frame Bake
10. Runtime Spawn Preflight
11. Ready
```

阶段结果不得合并成一个含糊的 `success`。至少区分：

- Draft Saved：源数据已保存，但可能尚不可编译。
- Compile Ready：Schema、引用和 Compiler 门禁通过。
- Authoring Written：生成文件已写入或 hash 已匹配。
- Waiting for Unity Editor：已发出任务，但 Unity 尚未完成消费。
- Unity Import Deferred：当前 `packageId + 五类 Compiler hash` 身份尚无会话验收或有效 receipt，允许重新执行 Unity 导入，不计为内容阻断。
- Unity Import Ready：资源、Avatar、Prefab、CharacterAction 和 Bake 均通过。
- Spawn Ready：默认组合和 Runtime 依赖可实际生成。
- Failed：存在明确失败阶段与稳定诊断。

## 11. Ready 的严格定义

只有同时满足以下条件，CharacterStudio、SDK 报告和 Unity 状态才能把角色标记为可运行：

- 当前角色包和全局库 revision 已保存，`packageId` 与五类 Compiler hash 均与导入任务一致。
- Schema、唯一 ID、主副手槽位、默认组合、动画、受击和必需 ActionRole 引用有效。
- 公共资源可解析，ResourceKey、provider 和 Unity imported asset 状态一致。
- Humanoid Avatar valid，Prefab rebuild 和 runtime composition smoke 成功。
- Profile 指定的角色 CharacterAction Library 与 `Common` Library 已合并并成功建立 Registry。
- 默认组合的 CharacterAction Profile、ActionSet、命令绑定和受击动作全部可发现。
- 每个攻击有效帧都存在对应 Clip、武器 Trace、Locator 和装备槽位 Socket frame。
- Weapon Trace Bake 已完成，禁止用静态胶囊 fallback 掩盖缺帧。
- 默认武器资源与组合引用一致，Runtime Spawn Preflight 通过。
- 当前 `packageId + 五类 Compiler hash` 身份拥有匹配的 Unity Editor 会话成功状态或持久成功 receipt；当前会话失败优先于旧 receipt，旧身份状态不得误报 Ready。
- receipt 声明 Ready 时，其 Prefab、Runtime Prefab、CharacterAction 和 Weapon Trace Bake 等关键路径必须仍位于项目范围内且真实存在。

能力配置为空是合法状态；冻结能力兼容诊断和可选 Gameplay Runtime 集成不得影响普通角色 Ready。

## 12. 当前维护样例

`Tools/MxFramework.Authoring/samples/character-codex-flow-test` / `codex_flow_test` 是当前唯一维护的迁移样例和回归样本：

- Character Application 7.0。
- Global Weapon Library 5.0。
- 只保留 `sword_empty`（剑 / 空手，默认）和 `empty_empty`（空手 / 空手）两个角色组合。
- 默认角色配置使用 `sword_empty`。
- `katana_atk1` / `katana_atk2` 的攻击有效帧必须同时具备武器 Trace frame 和 `mainHand` Socket frame。

样例是验收证据，不是 SDK 的隐式默认值；SDK 不得根据 `codex_flow_test` 的 ID、目录或资源命名推导通用规则。

## 13. 对外 SDK Definition of Done

满足以下全部条件后，项目才能宣称提供正式 Character Authoring SDK：

- 有独立、可安装、带语义版本的 SDK 分发产物，不要求复制整个仓库。
- 明确列出受支持的 .NET/平台版本、Schema/format 支持矩阵和生命周期。
- 冻结并记录公共 API；内部 DTO 和服务实现不因 `public` 修饰符自动成为承诺 API。
- 提供 API reference、最小第三方客户端、无 UI 编译示例和 Unity Bridge 接入示例。
- 提供加载、校验、编译、迁移、冲突、确定性 hash 和错误路径的兼容测试。
- 提供稳定诊断目录和破坏性变更/弃用政策。
- 文件访问限制在显式 workspace/package 根目录；路径穿越、符号链接、恶意包和超大输入有安全策略。
- SDK 不依赖 Unity Editor、CharacterStudio 浏览器状态或本地服务进程才能执行纯校验和编译。
- CI 能验证 SDK 包安装、示例编译、跨版本读取和当前 CharacterStudio/Unity Bridge 集成。

在此之前，应使用“项目内 Character Authoring Core/SDK 能力”描述当前状态，避免对仓库外兼容性作出错误承诺。

## 14. 后续开发规则

- 新增 Character Authoring 规则时先判断它属于 SDK、服务适配、编辑器表现、Unity Bridge 还是 Runtime，不得跨层放置。
- 新增公共 SDK API 前必须同步 `API_STANDARDS.md`、相关接口文档、分配行为和兼容影响。
- 新增 CharacterStudio 功能必须先确认 SDK 已提供对应结构化能力；不得先在浏览器中实现临时推导再补 Core。
- 新增 Unity 导入步骤必须提供 editor status、source hash、稳定诊断、成功 receipt 和 Spawn 门禁，不能只写文件。
- Schema、迁移或事务变化至少覆盖 round-trip、旧版本读取、冲突拒绝、失败回滚和确定性输出。
- 本文只维护 SDK 边界；字段级规则只在专项规范中维护并从本文引用。

## 15. 验收

本文后续实现或修改至少验证：

- Authoring Core 可在无 Unity 环境加载、校验和编译维护样例。
- CLI、本地服务和 CharacterStudio 对同一输入返回一致的稳定诊断和状态。
- CharacterStudio 不包含与 Core 重复的 Schema/Compiler 推导。
- Unity Import 的 Authoring 写入、会话级 Editor 状态和持久成功 receipt 可独立观察；清空 Temp 后当前 receipt 仍能证明 Ready，删除或过期 receipt 后则进入可重新导入的 Deferred，旧 hash 状态不会误报 Ready。
- 维护样例能完成 Compile、Unity Import、CharacterAction Registry、Weapon Trace Bake 和 Runtime Spawn Preflight。
- 文档健康检查和定向搜索确认没有仍把 CharacterStudio 本身称为 SDK、仍声明 Character Application 6.0 / Global Library 4.0 为当前状态，或把冻结能力列为必经步骤的规范性表述。
