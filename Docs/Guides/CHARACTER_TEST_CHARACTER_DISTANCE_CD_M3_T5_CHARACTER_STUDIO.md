# CD-M3-T5：CharacterStudio Anchor 编辑闭环

> Status: Guide
>
> 状态权威：[CD-M3 总控页第 3 节](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M3_ENGAGEMENT_ANCHOR.md)
>
> 前置：CD-M3-T1 `Verified`

## 1. 可交付结果

在 CharacterStudio 中完成 Anchor 列表与字段编辑、Zone 绑定、预览、Undo/Redo、保存重开、刷新恢复和 unknown schema 只读阻断，同时保护未保存草稿与 workbench UI 状态。

## 2. 实现清单

- [x] Anchor 新增/复制/删除/排序、stable ID、Zone、ModelRoot-local mm、yaw millidegree、capacity、priority/phase 字段编辑。
- [x] 编辑事务接入现有 package state、dirty、Undo/Redo 与统一保存；缺失文件首次真实编辑时才创建。
- [x] unknown schema 显示原始 payload/诊断并禁止任何覆盖入口；显式 discard 与 reload 分离。
- [x] 普通刷新保留 dirty draft、选择、tab/folding、camera/workbench；browser reload 提供临时恢复，不把刷新当 reset。
- [x] 预览展示 ModelRoot reference、Zone/Anchor、capacity 和 CandidatePose 辅助，不把骨骼/Unity transform 变成 runtime authority。
- [x] 标准与窄视口操作可达、无水平溢出或隐藏保存按钮。

## 3. 自动化与直接 UI 证据

- [x] editor-server transaction/smoke：edit、Undo、Redo、save/reopen、missing/unknown、explicit discard。
- [x] 实际 CharacterStudio 直接操作并保存截图：标准/窄视口、dirty refresh recovery、unknown read-only、save/reopen。
- [x] Compiler 输出与 Studio 保存内容 hash 一致。

## 4. 完成 Gate

- automated smoke 与直接 UI 流程全部通过，证据路径记录。
- 不破坏 DamageZone 编辑、已有 draft/reload 语义和其他 package workbench 状态。
- 受影响文件、viewport 下界、已知 UI 限制写入实际记录。

## 5. 实际记录

| 项目 | 当前记录 |
| --- | --- |
| 状态 | 见总控页；`Verified` |
| 实际分支 / 提交 | `main`；Anchor inspector、技术树、3D marker/yaw、draft/Undo/保存、复制与顺序编辑均已实现 |
| automated smoke | `CharacterStudio smoke ok`；CRUD/order、unknown lossless、3D、draft/Undo 断言通过 |
| 直接 UI / 截图 | Browser 直接验证 Dragon14 6 anchors；标准与 900px 窄视口、3D marker、完整 Inspector 均取证；浏览器 Console 0 error |
| 保存重开 / 恢复 | `authoredPriority 100 -> 101` 后刷新保持草稿/选择/Undo，Undo 回 100 后保存、浏览器重开仍为 6 anchors/priority 100；Git 保持无资产差异 |
| unknown / lossless | training_slime 临时 future schema `9.0` 显示原始 `futurePayload.preserveMe=true`、Anchor 编辑控件数为 0；临时文件随后删除 |
| copy / order | `anchor.head.front` 复制为稳定唯一 `anchor.head.front.copy`，计数 6→7；上移后顺序稳定；两次 Undo 恢复 6 个及原选择 |
| 已知限制 | 900px full-page capture 对 WebGL/sticky 区域有浏览器拼接重复伪影；可见视口和 DOM 操作可达性正常，不影响产品状态 |
