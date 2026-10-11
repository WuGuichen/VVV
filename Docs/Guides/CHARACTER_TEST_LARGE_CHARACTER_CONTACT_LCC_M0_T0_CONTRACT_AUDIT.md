# LCC-M0-T0：现状审计与最小合同基线

> Status: Guide
>
> 工作包：LCC-M0-T0（只读审计）
>
> 状态权威：[LCC-M0 第 4 节](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M0_CONTRACT_FOUNDATION.md)
>
> 审计基线：`main@8a9dd405b`，2026-08-24
>
> 结论：审计基线已冻结，可派发 T1 与 T3；这只证明实施边界明确，不证明功能已经存在。

## 1. 结果与范围

本任务确认 M0 的现有代码入口、最小公共合同、文件所有权和实施顺序。M0 只建立 Body Presence 数据链与通用 separation 查询，不改变角色移动、伤害、网络状态或 Dragon14 的实际碰撞行为。

本基线刻意不做形式化证明系统，也不要求为罕见数值边界自研任意精度或固定 limb 算法。实现优先复用现有 fixed-point 类型和碰撞原语；输入超出现有安全范围时 fail closed，并由针对性测试决定是否需要后续增强。

## 2. 当前代码事实

| 链路 | 当前入口 | 审计结论 |
| --- | --- | --- |
| Authoring DTO 与可选文档 | `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/CharacterPackages/CharacterAuthoringGeometry.cs` | DamageZone 与 EngagementAnchor 已提供 Missing、Current、UnsupportedSchema、Invalid 和 raw 保留先例；目前没有 Body Presence DTO。 |
| package 读取、校验与保存 | `CharacterPackageCommands.cs`、`CharacterPackageValidation.cs`、`EditorServer.cs` | 可以复用 package 路径解析、revision 和原子文件替换设施；Body Presence 需要独立单文件保存入口，不能让整包保存静默覆盖 future/invalid 原文。 |
| 编译 | `CharacterAuthoringCompiler.cs` | 当前输出 geometry binding v1～v3；只有 Current Body Presence 才升级到 v4，Missing 必须保持旧输出。 |
| RuntimeSpawn | `Packages/com.vincent.wgameframework/Character.RuntimeSpawn/CharacterImportedPackageJson.cs`、`CharacterRuntimeSpawnContracts.cs` | 当前只接受 v1～v3；v4 需新增严格解析，同时保持旧版本兼容。 |
| Combat physics | `Packages/com.vincent.wgameframework/Combat/Physics/CombatPhysicsShape.cs`、`CombatPhysicsWorld.*.cs`、`CombatPhysicsObbMath.cs` | 已有 Sphere、Capsule、AABB、OBB、compound leaf 与稳定身份；缺少统一返回法线、接触点和 penetration 的 separation API。 |
| Dragon14 | `Authoring/CharacterPackages/character-000003/geometry/body_geometry.json` 及生成包 | root capsule 作者中心含非零 Z；Runtime 当前只消费 Y。M0 只产生显式诊断，不改变 motor 中心语义。 |
| CharacterStudio | `Tools/MxFramework.CharacterStudio/web/src/api.js`、`inspector.js`、`scripts/smoke.mjs` | 已有可选文档状态与只读显示先例；M0 验收只要求最小读写保护闭环，不扩建完整几何编辑器。 |

## 3. 最小冻结合同

### 3.1 `geometry/body_presence.json` v1

- 文档是可选项。状态沿用 `Missing`、`Current`、`UnsupportedSchema`、`Invalid`；UI 可把 `UnsupportedSchema` 显示为 Future/ReadOnly。
- Missing 不自动创建、不标脏、不改变旧 binding。Future/Invalid 保留原始内容并禁止覆盖。
- Current 文档包含稳定 `volumeId`、`ModelRoot` reference frame、`Sphere|Capsule|Box`、local position/rotation、shape 尺寸、`skinMillimeters`、`pressureScaleMilli`、`enabled`；最多 4 个 volume，禁用项仍占容量。
- 数值必须 finite 且落在项目约定的角色局部范围内；旋转必须是有效单位四元数，scale 首版固定为 1。非法数据返回 typed diagnostic，不做静默修复。
- canonical 输出按 `volumeId` ordinal 排序，字段与数字格式稳定；content hash 只由 canonical Current 内容生成。
- 保存使用独立 Body Presence operation、expected package revision 和现有安全 package 解析；一次只原子替换 `body_presence.json`。冲突、校验失败或 IO 失败不得改变原文件；不要求重做整个 package 的事务系统。
- `body_geometry.json` 的 center X/Z 非零时给出 typed diagnostic，明确“当前 motor 仅消费 Y”；该诊断不偷改运行时行为。

### 3.2 Geometry binding v4

- Missing Presence 继续生成原有 v1/v2/v3，保持 canonical/hash 与 Runtime 行为不变。
- Current Presence（包括显式空 volumes）生成 v4；v4 增加 Presence 内容 hash 和编译后的稳定 volume 列表。
- 编译器记录 source mapping，RuntimeSpawn 严格读取 v4；未知版本、非法 shape、非有限值、重复 ID 或越界容量 fail closed。
- v1～v3 继续兼容读取。M0 不增加 snapshot 或网络 schema，因为 Presence 在 M0 尚未参与 authority 行为。

### 3.3 通用 separation API

- source 首期为 capsule；target 覆盖 AABB、Sphere、Capsule、OBB 和 compound leaf。
- 结果至少包含 typed status、source query identity、target body/collider/leaf identity、从 target 指向 source 的 normalized normal、非负 penetration、target surface contact point。
- `Separated`、`Touching`、`Overlapping`、`UnsupportedShape`、`InvalidShape` 必须可区分；失败不返回伪造接触。
- 查询使用现有 fixed-point 类型和 shape 定义，不引入 UnityEngine、业务角色类型或第二套浮点碰撞系统。
- 相切、零长度 capsule、同中心、平行轴、内含和等距候选使用固定 fallback 与完整稳定排序；compound 结果不依赖 BVH 或容器枚举顺序。
- OBB 只接受现有合同下有效的正交基。无法安全计算或发现中间量溢出时返回 `InvalidShape`，不在 M0 自研通用大整数内核。
- 单查询和 caller-owned bulk 查询 warmed path 为 0 B GC；不使用 LINQ、字符串格式化或每次调用新建集合。
- 量化误差由覆盖典型尺度与边界的测试约束。只有真实失败用例证明现有 fixed-point 原语不足时，才单独设计数值增强任务。

### 3.4 Dragon14 fixture 与调试

- T4 从正式作者包拟合 3～4 个稳定 Presence volume，覆盖躯干、颈/头与尾部主要体积；翅膀仍由主动 Trace/Hurtbox 表达，不做连续硬阻挡。
- volume 身份、启用状态和 separation 结果可投影到现有 debug snapshot/overlay；调试显示只读 authority 数据。
- M0 fixture 只证明编译、读取、查询和可视化，不对 motor 施加推挤，也不产生伤害。

## 4. 文件所有权与交接

| 工作包 | 唯一生产职责 | 主要写入面 | 只读边界 |
| --- | --- | --- | --- |
| T1 | Authoring DTO、状态、校验、canonical、独立保存、center X/Z 诊断 | Authoring Core/CLI 的 Body Presence 专属文件与 focused tests；共享入口只做最小 hook | compiler、RuntimeSpawn、Combat、Dragon14、CharacterStudio web |
| T2 | compiler、binding v4、RuntimeSpawn v1～v4 | `CharacterAuthoringCompiler.cs`、RuntimeSpawn DTO/parser、对应 tests | T1 保存实现、Combat、Dragon14 |
| T3 | separation result、narrow phase、stable bulk/comparer、NoAlloc tests | Combat Physics 专属 separation 文件和 tests；现有 shared 文件只做必要接线 | Authoring、RuntimeSpawn、CharacterTest、Dragon14 |
| T4 | Dragon14 正式 fixture、生成结果、最小 debug projection 与 Game View 证据 | Dragon14 作者源/正式生成资产、CharacterTest debug adapter/tests | T1～T3 核心实现 |
| T5 | 合并回归、CharacterStudio 最小读写保护、文档与最终验收 | CharacterStudio 必要接线/tests、`Docs/CAPABILITIES.md` 与精简验收证据 | 不首次重写 T1～T4 合同 |

共享文件冲突由主代理串行集成。子代理不得修改父里程碑状态表或总台账。

## 5. 实施顺序

```text
T1 ─> T2 ─┐
           ├─> T4 ─> T5
T3 ────────┘
```

- T1 与 T3 可并行。
- T2 在 T1 通过 focused tests 后开始。
- T4 等待 T2 与 T3 的公共合同稳定。
- T5 只做集成与验收，不扩大 M0 功能范围。

## 6. 验收门槛

T0 自身完成条件：

- [x] 当前 DTO、compiler、RuntimeSpawn、Combat、Dragon14 和 CharacterStudio 入口已定位。
- [x] T1～T5 的写入 owner、依赖和非目标已明确。
- [x] Missing/future 兼容、v4 升级条件、normal 方向、OBB fail-closed 与 M0 无行为变化已冻结。
- [x] 文档相对链接和基础格式通过本地检查。

后续生产任务统一使用以下证据，不建立额外取证框架：

- focused automated tests 的原始通过/失败结果；
- 普通 Unity C# 改动的一次脚本验证：refresh、compile、domain reload、Editor ready、Console error 0；
- T3 warmed NoAlloc 测量；
- T4 Dragon14 实际 Game View 与正式生成结果；
- T5 一次独立集成审查，重点看玩法回归、兼容、确定性和未修改范围。

P2 建议不阻塞实施；只有会造成数据破坏、确定性分叉、错误角色行为或无法回滚的 P0/P1 才阻塞当前任务。

## 7. 停止条件

- 需要改变 root capsule、root-distance、DamageZone/EngagementAnchor 或现有网络 schema 时，停止并由主代理重新定界。
- 需要引入新公共 shape、突破 4 volume 上限或支持非单位 scale 时，单独形成后续任务，不在 M0 顺手扩张。
- 现有 fixed-point 数值范围出现可复现错误时，先保存最小失败用例，再决定修复；不得为了理论完备预先实现通用大整数系统。
- 任何任务触碰其它代理或用户既有修改时，停止该文件写入并交由主代理集成。

## 8. 实际执行记录

| 项目 | 记录 |
| --- | --- |
| 审计结果 | 完成；形成最小可实现基线 |
| 生产修改 | 无 |
| 自动化 / Unity | T0 未运行；由 T1～T5 按实际改动验证 |
| 保留风险 | OBB 极端 fixed-point 范围、Dragon14 volume 参数和真实接触感必须由实现测试与 Game View 决定 |
| 流程调整 | 废弃过度展开的形式化规格草案；不再以多轮全量评审、定制 HMAC 证据系统或固定 limb 数学作为生产前置 |
