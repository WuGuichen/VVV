# LCC-M1-T2：Presence 候选、约束与 Pressure 接入

> Status: Guide
>
> 工作包：LCC-M1-T2（生产实现）
>
> 状态权威：[LCC-M1 第 5 节](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M1_BODY_PRESENCE.md)
>
> 执行文档状态：`Production-Frozen`（M1-T0/T1 已 Verified；owner、容量与失败边界已解除）
>
> 工作包派发状态：`Verified`
>
> 派发、集成与回滚规则：[LCC 单任务执行文档模板](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_TASK_EXECUTION_TEMPLATE.md)
>
> 并行规则：除本文明确写明“可并行”外，不与同里程碑其它生产写入并行；跨里程碑只允许只读调查。
>
> 必读基线：总台账第 1～8 节、父里程碑冻结合同、本任务全部章节、路径上的 AGENTS.md、Docs/API_STANDARDS.md 和 Docs/QUALITY_GATE.md；再按本文输入/所有权加载专项接口与源码。
>
> 子代理派发合同：本文是唯一任务边界；只写 Owner 范围，保留工作区其它修改，不改父状态。完成回报逐项列出实际文件、合同/兼容/失败行为、测试原始结果、未执行验证、风险冲突、集成与回滚建议。实现者不得担任 schema、network、碰撞数学、拓扑或正式资产任务的最终独立审查者。
>
> 两级派发门禁：只有 T0 只读审计可在全路线 Audit-Frozen 后派发。T0 只写自身冻结表并结构化回报精确文件/partial owner、测试 filter、Docs/Evidence/LCC/<TASK-ID>/ 证据文件名和交接点；T1+ 必须等待主代理把这些结论回写到生产任务，并将本里程碑标记为 Production-Frozen。
>
> 前置：T1 `Verified`。

## 1. 任务结果与非目标

每个角色 Hard Root capsule 可查询其它角色 Presence，生成去重、稳定、有界的 separation constraints，并把共享 pressure 作为接触速度输入交给既有 motion chain。

非目标：新增的 Presence-originated path 不直接改 transform/position；本任务不处理静态 sweep 安全细节（T3），不删除或改写既有 Hard Root compatibility `ApplyContactDisplacement`，不改变 damage/Hurtbox/Attack Opportunity。

## 2. 写入所有权

- Owner：`CharacterTestCharacterContactResolver.cs` 的 Presence candidate buffer、规范 key/去重、root+Presence stable constraint order、既有三轮 pressure solver 接入与 overflow diagnostic；`CharacterTestCharacterMotionSystem.cs` 仅拥有 post-motor contact 调用序列；新增 `CharacterTestLargeCharacterPresenceContactTests.cs`。
- `CharacterTestCharacterPhysicsRuntime.cs` 只消费 T1 API，不反向修改；`CharacterTestCharacterMotionSystem.cs` 除本文 post-motor 调用序列外的 Motion partial 由 T3 写。
- 禁止修改 Network/Debug（T4）、Dragon14 数据、M0 Combat API、父状态。

## 3. 实施清单

- [x] 用角色 Hard Root capsule 对其它 Presence layer 查询；排除 self、disabled/dead/不合法 entity 和非目标 layer。
- [x] 通过 M0 separation 获取几何事实；normal 方向统一为“目标 Presence 推离 query character”。
- [x] 以 entity pair + volume/leaf identity 去重；完整 comparator 不依赖 broadphase/字典顺序。
- [x] fixed-capacity buffer：8 角色时每 target 最多 28 raw candidate、全帧 28 unordered pair、224 directed constraint；任一超限在 mutation 前拒绝整个 Presence 子步并清零该子步 contribution，Hard Root 旧路径继续。
- [x] 相互角色只结算一次共享约束；不同所有权/质量/不可推动状态按合同分配 pressure。
- [x] 只写 contact velocity/pressure accumulator；保留既有输入和其它 contact 来源的组合顺序。
- [x] 同中心、相切、多个 volume、A/B 枚举反转和跨帧稳定有测试。

## 4. 验证与证据

- focused tests：单/多 volume、双向 query 去重、三角色链、同中心、顺序扰动、overflow、dead/unpushable、自身排除。
- 断言 Presence-originated constraint 不调用 resolver 的直接 transform displacement；既有 Hard Root compatibility path 仍有独立回归，且 Presence 不产生 damage/trace/obstacle hit。
- warmed candidate+constraint+pressure loop 0 B；记录容量水位和最坏样本。
- Unity 脚本验证通过；T3 前只证明 pressure 输出，不证明墙边 Playable 安全。

## 5. 停止条件与派发

若 Presence 无法通过现有 contact velocity 接口表达压力，或只能新增直接位移，停止并上报；不得绕过 motor，也不得为满足本任务删除既有 Hard Root compatibility 位移。回报 comparator、容量、pressure 输出、测试和未验证安全场景。

## 6. 实际执行记录

| 项目 | 当前记录 |
| --- | --- |
| 实际分支 / 提交 | 未执行 |
| 实际修改文件 | `CharacterTestCharacterContactResolver.cs` Presence partial；`CharacterTestCharacterMotionSystem.cs` post-motor 调用序列；新增 `CharacterTestLargeCharacterPresenceContactTests.cs` 与 `.meta` |
| 测试与原始证据 | 初始 EditMode job `2a8b72fa219a46e4b7aa7664b164499b`：6/6 passed。T5 Playable 捕获到非阻挡训练目标被 Presence 误纳入后，最小回修的新增单项 `b1e81b8dcf624a04b0c56c3cd4f85732` 1/1、整类 PresenceContact `5739c6bf19344398b532a9f1f68d17ed` 7/7、MotionSafety `d3a2a02b14014ccfae0270df5a3c09c0` 6/6，均 0 failed/skipped。 |
| 集成 / 回滚结果 | 主代理检查 owner diff、失败边界与禁止直接位移门禁后接受；T5 回修只在 Presence eligibility 复用现有 `ColliderPosture.BlocksCharacters`：`false` 不建 pair/constraint，`BlocksCharacters=true + CanBePushed=false` 仍记录约束但目标速度为 0。可按 resolver 单行与对应 focused test 回滚，T1 Presence runtime 不受影响。 |
| 状态历史 | 2026-08-24：`Planned` → `In Progress` → `Verified`；T1 Gate 后派发，源码、focused、NoAlloc 与 Unity 证据闭合。T5 发现并按原 owner 边界回修非阻挡目标语义，未改变 T2 容量、算法或状态模型。 |
| 执行者 / 审查者 | 子代理实现；主代理集成验收；按用户要求不增加逐任务独立评审，M1 只在 T5 保留一次 P0/P1 终审 |
| candidate / constraint | 每 target 28 raw、全帧 28 unordered pair、224 directed constraint；规范 key/排序、相同结果去重、冲突/身份/状态异常整子步 fail-closed |
| pressure / stable order | M0 normal 以 Presence → Hard Root 统一；全量查询/身份/去重/排序成功后才进入既有三轮 pressure，overflow 时旧 Hard Root pressure 继续 |
| NoAlloc / Unity | 8×4、224 constraints warmed 128 帧 0 B；初始脚本验证 job `9d7cea237d1349dda1741a5957aa7c39` 通过；回修后最终 validator `0e71867fd4d642a9a7ec4b280fe58b0f` 再次确认 refresh/compile/domain reload/Editor ready，Console 0 error。 |
| 限制 | 仅证明约束与 pressure 输出；root 实际平移/Yaw 表面速度、clamp、墙边/下一 tick motor 安全和 Playable 属于 T3～T5 |
