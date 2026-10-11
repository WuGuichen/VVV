# LCC-M5-T0：动画附肢软接触运行合同审计

> Status: Guide
>
> 工作包：LCC-M5-T0（只读审计）
>
> 状态权威：[LCC-M5 第 3 节](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M5_ANIMATED_APPENDAGE_SOFT_CONTACT.md)
>
> 执行文档状态：`Verified`（主代理已复核 tick、identity、schema、owner/filter 与 stop gate）
>
> 前置：M4 `Verified`，M5 执行包 `Audit-Frozen`。

## 1. 任务结果与非目标

冻结当前 Hurtbox sync、DamageGeometry snapshot、motion/contact pressure、Body Presence lifecycle、Severed topology、snapshot/rollback 和正式 Dragon14 数据的精确生产链，并向 T1～T3交付文件级 owner、字段/容量、tick 顺序、schema 决策、focused filter 和证据文件名。

本任务不改 Runtime、Authoring、CharacterStudio、正式资产或测试，不运行 Unity，不顺手实现接触，不建立公共 Animated Physics/Pose Graph 或新的碰撞数学。

## 2. 前置与并行

M4 必须保持 `Verified`，M5 必须为 `Audit-Frozen`。T0 是唯一可先派发任务；T1～T3 全部等待。允许其它代理只读调查，但不得并行写本任务文档或 M5 生产候选文件。

## 3. 必读输入

- [LCC 总台账第 1～8 节](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_DEVELOPMENT.md)
- [LCC-M5](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M5_ANIMATED_APPENDAGE_SOFT_CONTACT.md)
- `CharacterTestCharacterRuntimeModule.Tick`
- `CharacterTestCharacterMotionSystem.Tick/SyncBodyPartDerivedTopology`
- `CharacterTestCharacterHurtboxSystem.SyncAll`
- `CharacterTestDamageGeometrySnapshot`
- `CharacterTestCharacterContactResolver` 的 Presence constraint/pressure 路径
- Body Presence Authoring/validation/compiler/RuntimeSpawn v1～v6 compatibility
- Dragon14 `body_presence.json`、`body_colliders.json`、`body_part_damage.json` 和 Hurtbox bake index
- Network snapshot、rollback/replay 与 M1/M3/M4 focused tests

## 4. 写入所有权

- 只允许更新本任务文档的审计表和 `Docs/Evidence/LCC/LCC-M5-T0/AUDIT_2026-08-26.md`。
- 其它 Docs、源码、测试、Authoring、Generated、Scene、Prefab 和 `.meta` 全部只读。
- 发现共享 dirty hunk 时记录 owner，不做格式化或清理。

## 5. 必须冻结的合同

1. first/second Hurtbox `SyncAll` 的现有 frame/revision、motion 前后位置和 M4 authority-pose关系；不得凭印象增加第三次 sync。
2. `CharacterHurtboxLayer` query 与 `MotionObstacleLayerMask` 隔离；选定叶子过滤使用 logical collider identity 和同帧 metadata。
3. previous/current 动画表面 witness 的来源、spawn/restore/teleport/revision failure、是否纯派生以及 schema 34 stop gate。
4. Body Presence v2 `animatedHurtboxSources` 字段、最大数量候选、v1 raw preservation、geometry binding v7 与 v1～v6 compatibility。
5. M1 pressure solver 可复用入口、质量/PushScale/CanBePushed、三轮迭代、skin 与 clamp 是否需要窄适配。
6. Severed tail 同帧/下一帧边界、topology revision 和 contact-phase Ended 的派生规则。
7. T1～T3 精确文件/partial owner、合并顺序、focused tests、PlayMode 场景、8-Dragon budget 和 Evidence 文件名。

## 6. 审计实施清单

1. 画出真实 tick 顺序并登记每次 Hurtbox sync 的输入/输出 revision。
2. 追踪 head/tail leaf 从 Authoring、bake、Runtime binding、DamageGeometry 到 separation identity。
3. 追踪 M1 pressure 的 constraint、surface witness、clamp、contact state 与下一 tick motor消费。
4. 审计 M3 Severed removal 与 restore/replay topology 重建。
5. 冻结 v2/v7 字段容量、schema 34 stop gate、T1～T3 owner/filter/evidence。
6. 检查 dirty worktree 冲突并只记录，不修复。

## 7. 验证与停止

本任务只做源码、文档、正式数据和现有测试的可复核引用；不把测试存在写成测试通过。若发现必须改变 Hard Root 唯一硬阻挡、Hurtbox damage职责、root-distance、M3 Severed时序或 M4 resolved-pose 真源，停止并回报主代理修改父合同。

Evidence 必须包含：current tick 流程图、数据/身份表、容量表、schema 决策、文件 owner 表、测试 filter 表、已知 dirty 冲突和 T1 stop gates。

## 8. 派发、回滚与回报

执行者严格只读。完成后报告：精确源码入口、字段/容量、时序、schema、owner/filter/evidence 表；列出无法确认项和停止条件。不得宣布 M5 实现存在或自行把 T1 标为可执行。回滚仅删除/恢复本任务 Evidence 和本任务文档审计段，不触碰任何生产文件或其它 dirty 修改。

## 9. 实际执行记录

| 项目 | 当前记录 |
| --- | --- |
| 状态权威 | M5 工作包表 |
| 执行者 / 审查者 | `lcc_m5_t0_audit` / 主代理复核完成 |
| 实际修改文件 | 本文审计记录；`Docs/Evidence/LCC/LCC-M5-T0/AUDIT_2026-08-26.md` |
| 审计证据 | [AUDIT_2026-08-26](../Evidence/LCC/LCC-M5-T0/AUDIT_2026-08-26.md)：tick、身份、witness/schema、容量、pressure、Severed、owner/filter/evidence 与 dirty/stop gate 已冻结 |
| 测试 / Unity | 未执行 |
| 交接 | `Verified`；精确 owner/filter/evidence 已回写 T1～T3，父 M5 可提升 Production-Frozen |

## 10. 审计冻结摘要

- 保留现有两次 Hurtbox `SyncAll`：第一次位于 motion 尾部、已完成根运动但早于 M4 frame-N resolved pose；第二次位于 `TickAuthorityPose` 后，是 M5 可消费的最终同帧 DamageGeometry。禁止第三次 sync。
- previous witness 从本 tick motor 前的上一完成/restore-rebuilt snapshot 固定容量复制，current witness 从第二次 sync 后复制；两者只在同 tick 内使用。现有 contact velocity/count/displacement 已进 snapshot/hash，因此当前结论是不启用 schema 34。
- Body Presence v2 固定追加最多 4 个 `animatedHurtboxSources`，字段仅 `sourceId/colliderId/skinMillimeters/pressureScaleMilli/enabled`；geometry binding v7 additive，v1 与 binding v1～v6 保持旧语义。
- M5 固定容量为 8 个 authored source owners、每只 4 叶、32 witness、8 Dragon + 1 player roster、最多 256 directed constraints；任何超限在 contact-state mutation 前失败关闭。
- 精确 authored leaf 由 `DamageGeometrySnapshot` 的 logical/combat collider/PartId/layer/shape identity 解析，并调用既有 `CombatPhysicsWorld.CalculateCapsuleSeparation`；不扫描或提升整个 Hurtbox layer，不改变 `MotionObstacleLayerMask`。
- M1 复用三轮 pressure、mass/PushScale/CanBePushed、profile clamp、contact state 与下一 tick motor/static sweep；禁止 direct displacement。
- frame N combat 接受 Severed 后，尾叶在 frame N+1 的现有 topology sync 边界移除；M5 在该最终 snapshot 上只派生一次 `Ended`，restore/source reset 使用 silent baseline。
- 精确 owner、focused filter、Evidence 名、dirty 冲突和 T1/T2 stop gate 见审计证据第 7～9 节。唯一未确认项是 8-Dragon 实测 timing threshold，留 T3 真实测量，不能在 T0 臆定。
