# LCC-M0：合同、Authoring 与分离查询基础

> Status: Guide
>
> 里程碑：LCC-M0
>
> 里程碑状态权威：[LCC 实施总台账第 6 节](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_DEVELOPMENT.md)
>
> 工作包状态权威：本文第 4 节
>
> 当前状态：`Verified`
>
> 执行包准备度：`Production-Frozen`（T0～T5 已全部 Verified）
>
> 本文定义 M0 的生产实施边界；候选字段、类型名和文件清单在实际落地并通过 Gate 前不代表公共 API 或 Runtime 能力。

## 1. 目标与完成链

M0 建立后续大型角色接触功能所需的唯一 Authoring/Runtime 几何合同和通用 fixed-point separation 基础，但不改变角色移动或接触行为。

```text
geometry/body_presence.json
  -> Authoring Core DTO / validation / raw preservation
  -> canonical content hash
  -> geometry_binding.json v4
  -> Character.RuntimeSpawn binding

Combat query shape + target collider/compound leaf
  -> deterministic separation narrow phase
  -> normal + penetration + contact point + stable identity
```

M0 完成后允许 M1 使用正式编译结果和正式 separation API；M0 本身不得让 Dragon14 获得新的推挤、伤害、部位状态或地形 IK 行为。

## 2. 冻结合同

### 2.1 `body_presence.json` v1

计划格式：

- `format = "mx.characterBodyPresence.v1"`
- `schemaVersion = "1.0"`
- `revision >= 1`
- `volumes[]` 按 `volumeId` ordinal 稳定排序进入 canonical hash 和 binding

每个 volume 最少包含：

| 字段 | 合同 |
| --- | --- |
| `volumeId` | 包内唯一、非空、稳定身份；重命名视为删除并新增 |
| `referenceFrame` | v1 只允许 `ModelRoot` |
| `shape` | `Sphere`、`Capsule`、`Box`；未知值 fail closed |
| `localPose` | ModelRoot local position/rotation；scale 必须为单位或在编译时显式烘焙，禁止 Runtime 读取 Transform scale |
| `radius` / `height` / `size` | 由 shape 决定，有限、正值且满足现有 Combat physics 形状约束 |
| `skinMillimeters` | 非负、有上限；只扩大软接触范围，不改变 Hard Root |
| `pressureScaleMilli` | 有界 fixed-point 比例；0 表示禁用压力，不表示删除身份 |
| `enabled` | 显式作者开关；禁用项保留身份但不进入 Runtime compound |

约束：

- 每包 `volumes[]` 总数最多 4 个，启用项是其子集；禁用项同样占作者容量并接受完整字段校验。超限是 typed validation error，不在编译时截断。
- volume 不携带 HitZone、伤害倍率、寻路成本或动作攻击语义。
- 缺失文件是兼容状态：不生成默认文件、不标脏，Runtime 只使用现有 Hard Root。
- 未知 schema 保存原 payload 并进入只读阻断；CharacterStudio、CLI 或服务不得用空/default 文档覆盖。
- 已知 schema 中的非法 shape、重复 ID、非 ModelRoot、非有限数值和超限 fail closed，禁止生成 v4 binding。

### 2.2 Geometry binding v4

- `mx.characterGeometryBinding.v1`～v3 保持兼容读；只有 Current Presence 文档进入编译时输出 v4。
- v4 在现有 `CharacterImportedGeometryBinding` 上增加 `BodyPresenceVolumes`、revision、content hash 和明确 mode；Runtime 仍只消费生成 binding，不读取作者源文件。
- v4 canonical text 必须覆盖 format/schema/revision、稳定排序后的全部字段和禁用状态；不得依赖 JSON 属性顺序或集合枚举顺序。
- 生成 source mapping 必须能从 binding volume 回溯到 `body_presence.json` 对象路径。
- package/source revision 必须覆盖 Presence 内容，使网络双方配置不一致能够被现有 source identity 门禁发现。

### 2.3 通用 separation API

公共或模块 API 使用通用 Combat 名称，候选合同为 `CombatShapeSeparationResult`；禁止出现 Monster/Dragon/CharacterTest 业务词。

结果最少包含：

- source query identity；
- target entity/body/collider 和 compound leaf identity；
- normalized fixed-point separation normal；
- non-negative penetration；
- contact point；
- deterministic comparer 所需字段；
- 明确的 `IsOverlapping` / failure reason。

至少覆盖 query capsule 对 AABB、sphere、capsule 和 OBB。完全重合或最短轴不唯一时使用冻结的稳定轴/identity 决胜，不允许使用浮点随机方向或集合顺序。

API 负责 narrow-phase 几何事实，不施加位移、速度、伤害或 Gameplay 状态。

### 2.4 Dragon14 fixture 与中心偏移诊断

- Dragon14 首个 Presence fixture 只拟合胸腹核心、骨盆/尾根和肩颈根部，启用 volume 不超过 4 个。
- 翼膜、尾尖、头尖不进入 v1 fixture；自然攻击继续使用现有 BakedSocket Trace。
- 精确位置和尺寸必须通过 CharacterStudio/Unity 模型叠加拟合并记录截图，不在计划文档中预填猜测数字。
- `defaultCapsule.center.x/z != 0` 而 Runtime 只消费 Y 时，Authoring/导入诊断必须明确报告“当前运动合同忽略 X/Z”；M0 不改变 motor 行为。

### 2.5 调试投影合同

M0 只建立 debug data/overlay 身份和显示约定，不用 Gizmo 反向驱动 authority：

- Hard Root：固定颜色 A；
- Body Presence：固定颜色 B，并显示 volume id、enabled、skin、pressure scale；
- Animated Hurtbox：固定颜色 C；
- Active Trace：固定颜色 D。

颜色具体值由实现时沿用现有 CharacterTest overlay palette；不得仅凭 Scene Gizmo 存在宣称 Runtime binding 已消费。

## 3. 非目标

- 不创建 CharacterTest Body Presence layer/body 或每 tick compound。
- 不修改 `MotionObstacleLayerMask`、contact velocity、角色位置或导航结果。
- 不实现伤口、Broken、Severed、Focus target、动态 Zone priority。
- 不实现 CharacterStudio 完整编辑器；M0 只要求 package state、读写保护和最小可验证入口，正式编辑闭环可在本阶段完成基础后交由对应工作包收口。
- 不实现地形探针、IK target、动画骨骼覆盖或 Hurtbox 姿态改写。

## 4. 工作包状态与实施顺序

| 工作包 | 状态 | 交付物 | 前置 |
| --- | --- | --- | --- |
| [LCC-M0-T0](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M0_T0_CONTRACT_AUDIT.md) | `Verified` | 审计现有 Character Authoring、geometry binding v1～v3、Combat narrow phase、Dragon14 数据与测试 seam；冻结最小可实现合同 | 总计划接受 |
| [LCC-M0-T1](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M0_T1_AUTHORING_CORE.md) | `Verified` | Authoring DTO、document state、load/save/raw preservation、validation、canonical hash、CLI/service 与 center X/Z typed diagnostic | T0 Verified |
| [LCC-M0-T2](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M0_T2_COMPILER_RUNTIME_BINDING.md) | `Verified` | Compiler/source mapping、geometry binding v4、RuntimeSpawn DTO/import v1～v4 兼容 | T1 Verified |
| [LCC-M0-T3](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M0_T3_SEPARATION_API.md) | `Verified` | 通用 separation result/API、全部目标形状 narrow phase、稳定 comparer 和 NoAlloc tests | T0 Verified |
| [LCC-M0-T4](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M0_T4_DRAGON_FIXTURE_DIAGNOSTICS.md) | `Verified` | Dragon14 `body_presence.json` 正式 fixture、中心 X/Z 诊断实证、最小 overlay/debug projection | T2、T3 Verified |
| [LCC-M0-T5](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M0_T5_ACCEPTANCE.md) | `Verified` | 合并回归、Unity 验证、CharacterStudio/CLI 读写保护、文档和独立终审 | T4 Verified |

工作包状态只在本表维护。主代理是本表唯一写入者，子代理不得修改本表或总台账。T0 已把最小合同、owner 与验证边界回写到 T1～T5，M0 进入 `Production-Frozen`。T1 与 T3 可并行；T2 在 T1 focused tests 通过后开始；同一文件只允许一个 owner；T4 必须同时消费正式 binding 和 separation 合同。普通任务由实现者自测后交主代理集成，高风险任务最多安排一次独立只读审查，不以 P2 建议阻塞实施。

## 5. 预计影响面

实际 T0 必须重新定位并登记精确文件；当前候选包括：

- `Tools/MxFramework.Authoring/.../CharacterAuthoringGeometry.cs`
- Character package load/save/service/CLI 与 CharacterStudio package-state 相关文件
- `Tools/MxFramework.Authoring/.../CharacterAuthoringCompiler.cs`
- `Packages/com.vincent.wgameframework/Character.RuntimeSpawn/CharacterRuntimeSpawnContracts.cs`
- `Packages/com.vincent.wgameframework/Character.RuntimeSpawn/CharacterImportedPackageJson.cs`
- `Packages/com.vincent.wgameframework/Combat/Physics/CombatPhysicsWorld*.cs`
- `Authoring/CharacterPackages/character-000003/geometry/body_presence.json`
- CharacterStudio geometry editor、CharacterTest debug overlay 与相关测试

公共 API 或 schema 实际发生变化时，按落地结果回写 `Docs/Interfaces/Combat.md`、Character Resource Package/Authoring 文档和 `Docs/Interfaces/Network.md` 的 source identity 边界；计划文字不得提前写成 Current API。

## 6. 验证矩阵

| 类别 | 必须覆盖 |
| --- | --- |
| Authoring | 缺失文件、Current round-trip、重复 ID、超 4、非法 shape/数值/reference frame、future schema 原文保持、禁止覆盖 |
| Compiler | v1～v3 输入兼容、v4 输出条件、稳定排序/hash、source mapping、重复编译字节/语义一致 |
| Runtime import | v1～v4 读取、缺失 Presence 兼容、未知/非法 v4 fail closed、content hash/revision 投影 |
| Separation | capsule 对 AABB/sphere/capsule/OBB；相离、相切、浅/深穿透、完全重合、旋转 OBB、顺序扰动 |
| NoAlloc | 预热后 caller-owned buffer/query path 0 B；overflow/容量显式诊断 |
| Dragon14 | 正式 package validate/compile、最多 4 个核心 volume、翼/尾尖/头尖不在 fixture、中心 X/Z 警告可见 |
| Unity/工具 | refresh/compile/domain reload/Console 0；CharacterStudio load/save/reopen/unknown schema 只读；Game/Scene overlay 直接截图 |

## 7. M0 完成 Gate

- T0～T5 全部 `Verified`，实际交付记录填写完整。
- `body_presence.json` 只有一套 Authoring 真源，编译到现有 geometry binding v4，Runtime 不读取源 JSON。
- 缺失文件兼容、非法 Current fail closed、future schema 原文保护和禁止覆盖均有自动化与实际工具证据。
- separation API 对四类目标形状结果稳定，完全重合和顺序扰动可重演，预热后 0 B GC。
- Dragon14 fixture 经模型叠加拟合，有直接图像证据；非零 capsule center X/Z 不再静默。
- 没有任何角色移动、接触、伤害或姿态行为变化；若发生行为变化必须退回修正范围。
- Unity 完整脚本验证、Console 0 error、受影响 Authoring/Runtime tests 通过，独立审查 P0/P1 为 0。
- 接口文档按实际 API 回写；`Docs/CAPABILITIES.md` 不新增可玩能力，因为 M0 只提供基础合同。

## 8. 实际交付记录

| 项目 | 当前记录 |
| --- | --- |
| 工作包进度 | 6/6 Verified；M0 `Verified` |
| 实际分支 / 提交 | `main@8a9dd405b` 工作区内实施，保留既有非 LCC 修改 |
| 实际文件 | T0 精简审计基线；T1 Body Presence Authoring Core；T2 binding v4/RuntimeSpawn；T3 separation result/API/tests；T4 Dragon14 作者/生成资产与 debug；T5 CharacterStudio/service 集成和实际文档 |
| Authoring / Compiler tests | T1 build/focused/CLI 通过；T2 生产项目 build 0 warning/0 error、Authoring focused 与 RuntimeSpawn 3/3 通过；T5 Node smoke、`lcc-m0-integration`、`character-body-presence` 通过 |
| Combat separation tests | T3 + 旧 World/OBB 61/61；主代理最终 focused 8/8，job `576cbf1039bf40a78968af43224b2a02` |
| Unity / CharacterStudio | T5 focused EditMode 13/13，脚本验证 job `a42e98f09d6d4fde824db8eec286bb88` 完整通过、Console 0；CharacterStudio Current 独立保存与 Missing/Future/Invalid 保护通过 API/Node smoke。in-app browser 不可用，唯一终审判定不构成 P0/P1 |
| Dragon14 直接证据 | Game View Dragon14 与 Scene View 三层 overlay 已取得；路径登记在 T4 执行记录 |
| NoAlloc | T3 caller-owned bulk 预热后 4096 次 0 B；完整 8 角色接触路径留待 M1 |
| 接口回写 | Combat、Character Application、Character Resource Package、CharacterStudio README 与能力表均按实际 M0 API/边界回写 |
| 限制 / 阻断 | 无 M0 阻断；M0 仍无可玩接触行为，完整 8 角色接触、全姿态 skin/pressure 调参与 Runtime 推挤属于 M1 |

## 9. 状态历史

| 日期 | 工作包 | 原状态 | 新状态 | 证据摘要 | 限制或阻断 |
| --- | --- | --- | --- | --- | --- |
| 2026-08-24 | T0～T5 | 初始化 | `Planned` | 草拟 M0 目标、字段候选、兼容边界、工作包和完成 Gate | 计划落盘，执行包仍待审查，不代表 schema/API 已实现 |
| 2026-08-24 | T0～T5 | `Drafting` | `Audit-Frozen` | 6/6 执行文档经结构、DAG、写入边界和验证合同独立审查，P0/P1 为 0 | 仅可派发 T0；无 schema/API/Runtime/资产实现 |
| 2026-08-24 | T0 | `Planned` | `In Progress` | 派发字段级现状审计，要求精确文件/partial owner、测试 filter、证据文件名和串行交接点 | 只写 T0 自身冻结表；T1～T5 仍等待 `Production-Frozen` |
| 2026-08-24 | T0 | `In Progress` | `Verified` | 将审计结果收敛为 127 行最小合同基线；定位当前 Authoring、compiler、RuntimeSpawn、Combat、Dragon14 与 CharacterStudio 入口，冻结兼容、owner、DAG 与实际验证门槛 | 仅为源码审计结论，无生产能力或运行证据 |
| 2026-08-24 | T1、T3 | `Planned` | `In Progress` | M0 进入 `Production-Frozen`，按 DAG 并行派发 Authoring Core 与 separation API | 候选实现仍需 focused tests、主代理集成；T3 另需一次独立高风险审查 |
| 2026-08-24 | T1 | `In Progress` | `Verified` | Body Presence Authoring Core、typed validation、canonical/hash、独立 revision/atomic save 与 center X/Z warning 落地；build/focused/CLI 通过，主代理数据安全复核完成 | 真实 HTTP/CharacterStudio 留给 T5；全套 Authoring 被既有 ResourceMapping/UPM 断言阻断，不归因于 T1 |
| 2026-08-24 | T2 | `Planned` | `In Progress` | T1 Gate 解除，按 DAG 派发 compiler、binding v4 与 RuntimeSpawn compatibility | 尚无 T2 候选或 Unity 证据 |
| 2026-08-24 | T3 | `In Progress` | `Verified` | capsule 对四类目标/compound leaf 的确定性 separation 落地；实现者 61/61、主代理 focused 8/8、4096 warmed 0 B；唯一一次独立审查的近平行与共线两个 P1 均定向修复并复算关闭 | 只返回几何事实，不改变移动/伤害；完整 8 角色接触性能留待 M1 |
| 2026-08-24 | T2 | `In Progress` | `Verified` | geometry binding v4、source mapping、RuntimeSpawn v1～v4 与 center X/Z 诊断投影落地；主代理 Authoring focused 与 Unity 3/3 通过，唯一 schema 审查 P0/P1 为 0 | 未迁移正式资产或接 Gameplay；实际 Dragon14 生成留给 T4 |
| 2026-08-24 | T4 | `Planned` | `In Progress` | T2、T3 Gate 解除，派发 Dragon14 正式 Presence fixture、生成链和最小诊断投影 | 不得接 motor/contact 或改 Gameplay |
| 2026-08-24 | T4 | `In Progress` | `Verified` | Dragon14 三胶囊 fixture、v4 正式生成、center X/Z typed diagnostic 与三层 debug 投影落地；主代理 focused 3/3、脚本验证/Console 0、Game/Scene 直接图像通过 | Game capture 不合成 Gizmo，分层以 Scene View 单独取证；接触与全姿态调参留给 M1 |
| 2026-08-24 | T5 | `Planned` | `In Progress` | T4 Gate 解除，派发 M0 CharacterStudio/CLI 合并验收与一次独立终审 | 不得首次重写 T1～T4 核心合同 |
| 2026-08-24 | T5 | `In Progress` | `Verified` | CharacterStudio 四态展示与 Current 独立 revision 保存闭环；Node/API/Authoring/Unity focused 通过；默认脚本验证 Console 0；唯一终审 P0=0、P1=0 | in-app browser 不可用，保留 API/Node 与正式 Dragon Playable 画面证据；未追加无关 PlayMode 套件 |
| 2026-08-24 | M0 | `In Progress` | `Verified` | T0～T5 全部 Verified；Authoring、binding v4、separation、Dragon14 正式 fixture/debug、CharacterStudio 保护和文档 Gate 全部闭合 | M0 只提供基础合同与诊断，不声明 Runtime 身体推挤；M1-T0 前置解除 |
