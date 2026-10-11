# CD-M2-T0：DamageZone 合同与现状审计

> Status: Guide
>
> 工作包：CD-M2-T0
>
> 工作包状态权威：[CD-M2 总控第 3 节](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M2_DAMAGE_ZONE.md)
>
> 里程碑状态权威：[实施总台账第 4 节](CHARACTER_TEST_CHARACTER_DISTANCE_DEVELOPMENT.md)
>
> 前置：CD-M1 `Verified`
>
> 本文是只读审计与合同冻结任务，不授权实现生产源码、生成资产或 CharacterStudio UI。清单和候选类型名不证明实现存在。

## 1. 任务结果

在任何 CD-M2 生产实现开始前，形成一份无 `TBD` 的字段级 DamageZone 合同、当前数据链路地图、精确影响文件清单和可执行测试矩阵，使后续任务不需要边写代码边猜 schema、兼容或 revision 语义。

## 2. 最小上下文

- [CD-M2 总控](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M2_DAMAGE_ZONE.md) 的公共合同、任务状态和依赖。
- [实施总台账](CHARACTER_TEST_CHARACTER_DISTANCE_DEVELOPMENT.md) 第 2、3、5、6 节。
- [CD-M1](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M1_ATTACK_OPPORTUNITY.md) 的实际交付记录和 HitZone 证据边界。
- [父设计](CHARACTER_TEST_CHARACTER_DISTANCE_CALCULATION.md) 第 7.6 节。
- [CharacterApplication](../Interfaces/CharacterApplication.md)、[AI](../Interfaces/AI.md)、[Combat](../Interfaces/Combat.md) 当前命中章节。
- `Docs/API_STANDARDS.md`、`Docs/QUALITY_GATE.md` 与 `Docs/CHARACTER_SDK_AUTHORING_PIPELINE_PLAN.md` 的 schema、Authoring 和验证边界。

不需要加载 CD-M3、CD-M4 的完整执行文档。

## 3. 审计范围

### 3.1 当前链路

- [x] 从 `geometry/body_colliders.json::hitZoneId` 追踪到 Authoring DTO、校验、Compiler、`geometry_binding.json`、Unity Runtime binding、Hurtbox authority 和 CD-M1 Attack Opportunity 输出。
- [x] 区分现有 `HitZoneBindings` 与新增 `DamageZone`：记录二者的身份、基数、所有权和兼容关系，禁止把名称相近当成同一概念。
- [x] 确认 `geometry_binding.json` 当前 format/schema、canonical hash、source mapping、Unity import 和 Runtime 解析入口。
- [x] 确认 Hurtbox/HitZone 的创建、禁用、销毁、bounds 更新和 revision 推进位置。
- [x] 确认 Planner authority state、network snapshot、full/owned/non-owned hash、rollback 和 replay 的现有扩展入口。
- [x] 核对 CharacterStudio 当前 package state、保存事务、Undo/Redo、普通刷新、显式丢弃重载和浏览器草稿恢复；不得根据旧记录推断当前实现。

### 3.2 必须冻结的合同

- [x] `damage_zones.json` 的 format、schemaVersion、根对象和确定性序列化顺序。
- [x] Zone 的稳定身份、显示字段、作者优先级，以及 `Hittable`、`Aimable`、`Approachable` 三个正交属性的默认值与必填规则。
- [x] HitZone 与 DamageZone 的作者映射方向、基数和重复归属诊断。
- [x] 缺失文件、空文件、已知 schema 非法内容、未知 schema 四种状态的读取、编辑、保存和编译行为。
- [x] 是否需要真实迁移源版本；若首版没有历史格式，不得伪造迁移成功，只保留未来迁移入口和当前版本 round-trip。
- [x] 缺少作者文件时旧 Hurtbox 的命中/瞄准兼容，以及不得生成长期 `Approachable` Zone 的边界。
- [x] `geometry_binding.json` 的版本策略：兼容追加还是 format/schema 升级；缺少可选文件时输出/hash 是否保持不变。
- [x] Runtime Zone aggregate 的坐标空间、更新阶段、空集行为、排序和 revision 推进条件。
- [x] Zone 选择的初选、保持、失效、机会等级提升、同等级至少改善 2 tick，以及完整稳定决胜顺序。
- [x] Zone lock 的身份字段、revision witness、snapshot 生命周期、旧 schema 归一化和 stale 行为。
- [x] `Approachable` 在 M2 只被保存/编译还是有 Runtime 消费；不得把 CD-M3 的 Anchor、CandidatePose 或路径成本带入 M2。
- [x] 每种失败的 typed diagnostic code、责任层和是否阻断整个文件、单个 Zone 或单次查询。

## 4. 合同冻结记录

执行本任务时填写下表；任一“未核对”都阻止 T0 进入 `Verified`。

| 决策 | 当前记录 | 证据位置 |
| --- | --- | --- |
| 作者 schema 与字段 | `format="mx.characterDamageZones.v1"`、`schemaVersion="1.0"`、根 `zones[]`。Zone 必填 `zoneId`、非空 `hitZoneIds[]`、`hittable/aimable/approachable`；可选 `displayName`（默认 `zoneId`），`authoredPriority` 默认 0、范围 -100000～100000。保存按 `zoneId` ordinal、成员按 HitZone ordinal；三属性互不推导。 | `CharacterAuthoringGeometry.cs` 的现有几何 DTO 约定；`CharacterPackageCommands.ReadGeometry` 的组件文件入口；本节冻结新增合同 |
| HitZone → DamageZone 基数 | 作者方向为 DamageZone 拥有 `hitZoneIds[]`；一个 HitZone 至多归属一个 Zone。现有 `HitZoneBindings` 继续表示 HitZone→BodyPart/伤害参数，不改名、不复用身份；DamageZone 只做多个 HitZone/Hurtbox 的稳定选择聚合。 | `CharacterAuthoringCompiler.cs::BuildGeometryBinding` 当前 `HitZoneBindings`；`CharacterTestDamageGeometrySnapshot` 当前 collider/HitZone 证据 |
| 缺失/非法/未知 schema 行为 | 缺失：`DocumentState=Missing`，可命中/瞄准旧 Hurtbox，不标脏、不写文件、不产生持久 Zone/Approachable。当前 schema 空 `zones`：有效显式空文档。已知 schema 非法：保留 typed DTO/诊断，阻断该包 DamageZone 保存与编译且不写部分产物。未知 format/schema：保留原始 UTF-8 payload，`ReadOnly/WriteEligible=false`，所有保存入口拒绝覆盖。首版无真实迁移源，只接受 1.0 round-trip；其它版本不得伪迁移。 | `ReadOptional<T>` 与 EditorServer 当前保存入口；`sessionState.js` 当前草稿恢复；冻结兼容合同 |
| Compiler format/hash 策略 | 文件缺失时继续输出现有 `mx.characterGeometryBinding.v1`/`1.0`，canonical text、source package hash 和输出字节不新增占位字段。文件存在且有效（包括显式空）时输出 `mx.characterGeometryBinding.v2`/`2.0` 并追加排序后的 `damageZones`；source mapping 指向 `geometry/damage_zones.json`。Runtime reader同时接受 v1（无 Zone）与 v2。 | `CharacterAuthoringCompilerFormats.GeometryBinding`、`BuildGeometryBindingCanonicalText`、`BuildSourcePackageCanonicalText`、CLI write plan |
| Runtime aggregate/revision | 使用当前帧完成并 sealed 的世界空间 Fix64 Hurtbox AABB；成员按 `LogicalColliderId` ordinal，Zone 按 `ZoneId` ordinal。空/全部失效 Zone 不可选。`BindingRevision` 仅在编译 binding 身份/内容变化时推进；`HurtboxRevision` 沿用现有注册、禁用、销毁和成员变化；`ZoneRevision` 是 binding+h​​urtbox authority witness，姿态仅通过同帧 `WorldRevision`/bounds 失效，不伪造身份变化。旧 snapshot 任一 witness 不匹配即 stale。 | `CharacterTestDamageGeometrySnapshot`、`CharacterTestAttackEvaluationSnapshot`、Hurtbox system 的 commit/seal 入口 |
| 选择、迟滞和决胜 | 合法候选顺序：机会等级降序、earliest tick 升序、`authoredPriority` 降序、ZoneId ordinal、HurtboxId ordinal。无旧锁按全链初选；旧锁有效默认保持。仅旧锁失效、候选机会等级严格更高，或同等级 earliest tick 至少减少 2（恰好 2 允许）时切换；通过门槛后仍用完整稳定链。Unavailable/Unsupported/Stale 不参与。 | CD-M1 opportunity level/earliest tick 合同；父设计 7.6；本节冻结边界 |
| Zone lock authority shape | `HasLock`、target Gameplay/Combat entity、resolved action stable id+revision、ZoneId、HitZoneId、logical HurtboxId、opportunity level、earliest tick、binding/hurtbox/world/zone revisions、selected frame、switch reason。每次 Planner authority snapshot 随帧捕获；旧 network schema 归一为 `HasLock=false`；任一身份/revision 不匹配先失效再重选，不沿用 stale witness。 | 当前 Planner state、session authority participant、character snapshot/full-owned-non-owned hash、rollback/replay 扩展入口 |
| CharacterStudio 状态保护 | DamageZone 复用 package state、统一保存事务和现有 session snapshot。普通“刷新状态”必须保留 package/global/import 三类草稿、Undo/Redo、selection/tabs/folding/camera/scroll；仅“放弃改动并重载”清除当前 package 草稿，且保留 UI 布局。未知 schema 显示只读原文和诊断，不能被 UI 默认值覆盖。 | `web/app.js` refresh/discard 分离；`web/src/api.js`；`web/src/sessionState.js` beforeunload/sessionStorage/localStorage |
| Diagnostic code 清单 | Authoring 阻断文件/包编译：`CHARDZ001_INVALID_FORMAT`、`002_UNSUPPORTED_SCHEMA`、`003_DUPLICATE_ZONE_ID`、`004_EMPTY_ZONE_ID`、`005_EMPTY_MEMBERS`、`006_DUPLICATE_HIT_ZONE_MEMBERSHIP`、`007_UNKNOWN_HIT_ZONE`、`008_DISABLED_BODY_PART`、`009_INVALID_PRIORITY`、`010_INVALID_ATTRIBUTES`、`011_READ_ONLY_DOCUMENT`。Runtime/query fail closed：`CHARDZ101_INVALID_BINDING`、`102_STALE_SNAPSHOT`、`103_NO_VALID_ZONE`、`104_LOCK_INVALIDATED`、`105_UNSUPPORTED_OPPORTUNITY`。非法作者文档不做逐 Zone 部分编译；Runtime 单次查询失败不破坏其它实体。 | 现有 `CharacterAuthoringValidationCodes`/report 模式；CD-M1 typed status 模式；本节冻结代码 |

冻结结果应写回本节；若改变总台账第 2 节的公共合同，必须先走设计/合同评审并同步父设计，不能在后续实现中暗改。

## 5. 输出物

- [x] 上述冻结表全部有明确结论和源码/文档位置。
- [x] 按 T1～T6 分组的精确候选影响文件清单，区分“预计修改”和“只读核对”。
- [x] 测试矩阵列出测试入口、fixture、断言、执行环境和归属工作包。
- [x] 记录可复用的现有实现和必须删除或避免的第二套真源风险。
- [x] 记录阻断项、负责人依赖和解除条件；不能用“实现时再看”代替结论。

### 5.1 精确影响面

| 工作包 | 预计修改 | 只读核对 / 禁止复制 |
| --- | --- | --- |
| T1 | Core 的 `CharacterAuthoringGeometry.cs`、`CharacterResourcePackage.cs`、`CharacterPackageValidation.cs`、`CharacterAuthoringCompiler.cs`；CLI 的 `CharacterPackageCommands.cs`、`EditorServer.cs`；Authoring focused tests | 复用现有 validation report、package save transaction、geometry binding write plan；不新增旁路 Zone 文件输出 |
| T2 | RuntimeSpawn 的 imported geometry binding/JSON parser；CharacterTest damage geometry/zone snapshot 与 focused tests | 复用 sealed `CharacterTestDamageGeometrySnapshot` 和 Fix64 world AABB；不直读作者 JSON |
| T3 | CharacterTest Attack Opportunity 邻接的纯选择器、Planner 接入与 focused tests | 不改变 `target.distance`/`target.inAttackRange`，不持久化 authority |
| T4 | Planner authority state、network character/session snapshot、hash、rollback/replay 与 focused tests | 复用现有 participant 和 schema normalization；不建第二份网络状态 |
| T5 | CharacterStudio `state.js`、`api.js`、`sessionState.js`、`inspector.js`/context panel、`app.js`、HTML/CSS、smoke | 复用现有 package/global/import 草稿和刷新/显式丢弃分流 |
| T6 | 正式 fixture、CharacterTest 可观察面、接口文档和里程碑台账 | 不引入 CD-M3 Anchor/CandidatePose/pathfinding |

### 5.2 测试矩阵

| 包 | 入口 / 环境 | fixture 与核心断言 |
| --- | --- | --- |
| T1 | Authoring tests `character-damage-zones` + CLI validate/compile | 临时最小角色包；round-trip、顺序扰动、typed errors、missing 不补写、unknown 不覆盖、v1/v2 hash 字节稳定 |
| T2 | Unity EditMode `CharacterTestCharacterDistanceM2RuntimeTests` | v2 compiler binding；单/多 Zone 聚合、八种属性组合、revision/stale、顺序扰动、warmed 0 B |
| T3 | Unity EditMode `CharacterTestCharacterDistanceM2SelectionTests` | 0/1/2 tick 边界、等级提升、失效、稳定 ID 链、长序列抖动、NoAlloc |
| T4 | Unity EditMode/PlayMode network focused | formal snapshot、full/owned/non-owned hash、旧 schema no-lock、rollback/replay 下一 tick command parity；snapshot capture/apply 按现有 `LowFreqAlloc` 合同，不纳入逐帧 0 B 窗口 |
| T5 | CharacterStudio node smoke + 实际浏览器 | create/edit/delete、Undo/Redo、save/reopen、普通刷新保草稿/UI、显式丢弃、unknown read-only、标准/窄宽度 |
| T6 | 合并 focused + Unity PlayMode/Game View | 正式包全链、可观察 lock/reason、Console 0、逐帧 aggregate/selector warmed 0 B、低频 snapshot `LowFreqAlloc`、接口回写 |

可复用权威为现有 `geometry_binding.json`、sealed Hurtbox snapshot、CD-M1 evaluation snapshot、Planner/network snapshot participant 和 CharacterStudio session snapshot。必须避免的第二真源包括 Runtime 直读 `damage_zones.json`、UI 自行校验 schema、按 broadphase 返回顺序选 Zone、以及 UI/Planner 私有 lock 副本。当前无外部阻断；T1～T6 的解除条件仅为前置工作包按本总控进入 `Verified`。

## 6. 验证与完成 Gate

- 审计覆盖 Authoring → Compiler → generated binding → Runtime → Planner → Network → CharacterStudio 全链，但没有修改生产代码或资产。
- 所有冻结项无 `TBD`、无互相矛盾的 fail-closed/legacy fallback 表述。
- 每个后续工作包都有明确输入、输出、测试入口和禁止越界项。
- 独立复核能仅凭冻结表回答：某个非法 Zone 会在哪里被拒绝、缺失文件如何兼容、哪个 revision 使旧 lock 失效。
- T0 实际交付记录完整后，才把 T0 改为 `Verified` 并允许 T1 开工。

## 7. 停止条件

遇到以下任一情况时停止，不进入实现：

- 新合同需要新增框架级公共 API，而总台账锁定为 CharacterTest 内部实现。
- 现有 `HitZoneBindings` 与 DamageZone 的身份/基数无法无歧义共存。
- `geometry_binding.json` 版本策略会破坏旧 Player/包，但尚无迁移或显式兼容方案。
- CharacterStudio 无法无损保留未知 schema 或未保存草稿。
- Zone lock 需要 CD-M3 路径/Anchor 权威才能定义。

## 8. 实际提交与验证结果

| 项目 | 当前记录 |
| --- | --- |
| 工作包状态 | 见[CD-M2 总控第 3 节](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M2_DAMAGE_ZONE.md) |
| 实际分支 / 提交 | `main` 脏工作区；T0 只改文档，未提交 |
| 审计基线 | 2026-08-18，`84d3d084d`；按当前源码重新核对，未把旧诊断当作当前事实 |
| 冻结合同 | 第 4 节九项均已冻结，无 TBD |
| 影响文件清单 | 第 5.1 节，按 T1～T6 分组 |
| 测试矩阵 | 第 5.2 节；T0 为只读审计，未运行生产测试 |
| 阻断 / 限制 | 无外部阻断；T0 不声明任何生产实现存在 |
