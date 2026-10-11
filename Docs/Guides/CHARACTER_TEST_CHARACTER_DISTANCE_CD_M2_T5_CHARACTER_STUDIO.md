# CD-M2-T5：CharacterStudio DamageZone 编辑闭环

> Status: Guide
>
> 工作包：CD-M2-T5
>
> 工作包状态权威：[CD-M2 总控第 3 节](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M2_DAMAGE_ZONE.md)
>
> 里程碑状态权威：[实施总台账第 4 节](CHARACTER_TEST_CHARACTER_DISTANCE_DEVELOPMENT.md)
>
> 前置：CD-M2-T1 `Verified`；可与 T2～T4 顺序之外独立实施，但 T6 同时要求 T4、T5 `Verified`
>
> 本文只实现 CharacterStudio 作者体验与真实保存闭环；不复制 Core 校验、不直接读取 Runtime 状态、不实现 Planner 行为。

## 1. 任务结果

让作者能在 CharacterStudio 中显式创建、编辑、校验、撤销、重做、保存并重开 DamageZone；缺失文件保持无损，未知 schema 保持只读，普通刷新和浏览器恢复不会销毁未保存草稿或工作台状态。

## 2. 最小上下文与前置检查

- [CD-M2 总控](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M2_DAMAGE_ZONE.md)、[T0 冻结记录](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M2_T0_CONTRACT_AUDIT.md) 和 [T1 实际交付](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M2_T1_AUTHORING_COMPILER.md)。
- `Docs/CHARACTER_SDK_AUTHORING_PIPELINE_PLAN.md`、`Docs/QUALITY_GATE.md`。
- CharacterStudio 当前 `web/app.js`、`web/src/api.js`、`state.js`、`sessionState.js`、`contextPanels.js`、viewport/inspector 相关模块和 `scripts/smoke.mjs`。
- T1 提供的 package state、typed diagnostics、write eligibility、保存事务和 Compiler 入口。

开工前必须实际核对当前普通刷新、显式丢弃重载、session draft 恢复和 dirty domains；历史诊断或按钮文案不能替代当前 smoke 证据。

## 3. 交互与状态合同

- 浏览缺失的可选 DamageZone 文件不创建文档、不标脏；只有显式“创建 DamageZone 配置”事务才产生草稿。
- 每个新增、删除、复制、重命名、属性切换、优先级修改和 HitZone 归属变化都是可撤销文档事务。
- Unknown schema 显示原 format/schema、阻断原因和恢复建议；整个 DamageZone 面板只读，保存/编译不能覆盖原 payload。
- 普通“刷新状态”只更新 readiness、资源计划和外部状态，必须保留 package 草稿、DamageZone 草稿、Undo/Redo、当前选择、tab/folding、viewport/camera。
- “丢弃并重载”必须显式触发并清楚提示会丢弃哪些 dirty domains。
- 浏览器 reload/crash 恢复应复用现有 session draft 机制，包含 DamageZone 数据和必要 UI 定位状态；不得建立第二套互相覆盖的 localStorage 真源。
- 任何 UI 校验仅投影 T1 Core diagnostics；前端不能自行发明不同的合法性规则。

## 4. 实现顺序

- [ ] 将 T1 的 DamageZone 文档状态、write eligibility 和 diagnostics 接入现有 package state。
- [ ] 增加列表/详情编辑面，至少覆盖稳定 Zone ID、显示字段、三个正交属性、作者优先级和 HitZone 成员关系。
- [ ] 成员选择只展示当前 package 中可引用 HitZone，并清楚区分已禁用、已占用和非法遗留引用。
- [ ] 为全部编辑操作接入现有 package history/Undo/Redo；选择、折叠、筛选和预览不标脏。
- [ ] 接入现有统一保存事务；验证/Compiler 失败时不写部分文件、不清空 dirty/undo。
- [ ] 首次显式创建、保存、重新加载后保持稳定 ID、顺序和属性。
- [ ] 未知 schema 只读阻断；普通刷新不覆盖 raw payload。
- [ ] 将 DamageZone 草稿纳入普通刷新保护、显式丢弃提示和浏览器 session recovery。
- [ ] 增加定位 diagnostics 的交互，使作者能从问题跳到对应 Zone/HitZone 字段。

## 5. 自动化与 Smoke

实现后至少执行并记录：

```powershell
npm --prefix Tools/MxFramework.CharacterStudio run smoke
dotnet run --no-build --project Tools/MxFramework.Authoring/tests/MxFramework.Authoring.Tests/MxFramework.Authoring.Tests.csproj -- character-damage-zones
```

前端 smoke 必须新增 DamageZone 断言；仅运行尚未覆盖 DamageZone 的旧 smoke 不能关闭本任务 Gate。

- [ ] 缺失文件打开、切换面板、普通刷新和关闭工作台均不产生文件或 dirty。
- [ ] 显式创建后新增/编辑/删除/复制/排序/归属操作逐项 Undo/Redo 精确恢复。
- [ ] 保存失败保持草稿、dirty 和 Undo/Redo；保存成功只清理对应 package dirty domain。
- [ ] 保存并重开后 schema、Zone、属性、顺序和成员关系一致。
- [ ] 未知 schema 全面只读，refresh/save/compile/discard 之外的入口不能覆盖 raw payload。
- [ ] 普通刷新保留 DamageZone 草稿、当前 Zone、tab/folding、camera/viewport 和 Undo/Redo。
- [ ] 显式丢弃重载清楚确认并只在确认后恢复磁盘状态。
- [ ] 浏览器 reload 恢复 DamageZone 草稿并提示恢复来源；用户可显式丢弃恢复草稿。
- [ ] CharacterStudio smoke 覆盖 package state、编辑、保存、Compiler 触发和 diagnostics 定位。

## 6. 人工界面证据

- 在标准窗口和窄窗口分别检查列表、字段、成员选择、diagnostics 和按钮可用性；无文字遮挡、水平溢出或不可达操作。
- 保存直接界面证据：缺失文件、正常编辑、Undo/Redo、未知 schema 只读、普通刷新保护、显式丢弃和重开结果。
- 自动化 DOM/几何测试不能替代实际浏览器界面检查。
- 证据必须记录当前 commit/worktree、浏览器环境和文件路径。

## 7. 完成 Gate

- Core、UI 和磁盘 round-trip 使用同一 schema/diagnostics，未知 schema 无覆盖路径。
- 所有文档事务可 Undo/Redo，保存失败不丢草稿。
- 普通刷新、显式丢弃和浏览器恢复的 DamageZone 行为均有 smoke 与实际界面证据。
- CharacterStudio 标准/窄窗口实际检查通过。
- 实际交付记录完整后 T5 才能进入 `Verified`；这不单独证明 Runtime/Planner 已完成。

## 8. 实际提交与验证结果

| 项目 | 当前记录 |
| --- | --- |
| 工作包状态 | 见[CD-M2 总控第 3 节](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M2_DAMAGE_ZONE.md) |
| 实际分支 / 提交 | `main` 脏工作区，基线 `84d3d084d`，尚未提交 |
| 实际修改文件 | `web/src/inspector.js`、`scripts/smoke.mjs`；复用 T1 editor-server/package state/save transaction |
| CharacterStudio automated/smoke | bundled Node 执行 `scripts/smoke.mjs`：`CharacterStudio smoke ok`；覆盖 UI 入口、未知 schema 写保护、session draft/Undo 保存 |
| Undo / Redo / save / reopen | 隔离副本实际修改 `zone.head` 显示名、保存、重开并读回；恢复原值后保存；browser reload 草稿恢复后 Undo 回到 clean |
| Refresh / discard / recovery | 实际 reload 恢复 `Browser Recovery Draft` 并显示恢复来源；Undo 恢复原值；`Disposable Draft` 通过既有确认路径显式丢弃并回到 clean，未触碰正式包 |
| 标准 / 窄窗口证据 | 标准窗口确认 Dragon14 5 Zone、成员、正交属性和优先级可达；内置浏览器实际最小 1280×720 下无水平溢出，字段、成员、优先级和保存操作可达 |
| Unknown schema | 隔离副本临时改为 `9.9`，实际页面显示只读与“不支持的 schema”，保留 raw `9.9`，新增/保存入口不可用；随后恢复 `1.0` |
| 已知限制 / 阻断 | 无 T5 验收阻断；浏览器后端不能将实际宽度设置到 1280 px 以下，本次以 1280×720 作为窄视口下界 |
