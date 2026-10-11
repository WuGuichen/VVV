# CD-M3-T1：Anchor Authoring、Compiler 与 Runtime Binding

> Status: Guide
>
> 状态权威：[CD-M3 总控页第 3 节](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M3_ENGAGEMENT_ANCHOR.md)
>
> 前置：CD-M3-T0 `Verified`

## 1. 可交付结果

建立 `geometry/engagement_anchors.json` 从 Authoring Core 到现有 `geometry_binding.json` 的唯一可追踪链，并让 Unity Runtime 只读消费编译结果。

## 2. 实现清单

- [x] DTO/schema、严格 validator、canonical sort/hash、round-trip、缺失/unknown/invalid save protection。
- [x] 仅接受 T0 冻结的 ModelRoot-local mm/millidegree contract；World、bone、leaf-follow typed reject。
- [x] Zone 引用、stable ID、capacity/priority、revision/phase fields 严格校验。
- [x] 复用现有 Character package service、CLI/editor-server 事务和 Compiler，不建立第二份 runtime artifact。
- [x] geometry binding schema/version 按兼容规则升级；旧包缺失 Anchor 进入显式 compatibility，不标脏、不自动补文件。
- [x] Runtime binding 以 immutable/canonical 结构暴露 Anchor，并计算独立 Anchor content revision/hash witness。

## 3. 自动化测试

- [x] schema round-trip、canonical hash、输入顺序扰动、compile determinism。
- [x] duplicate/invalid Zone/coordinate/yaw/capacity/reference frame 全部精确失败。
- [x] missing file 不写默认；unknown schema 原 payload 保留且所有写入口阻断。
- [x] Compiler 输出被 Runtime 正式 binding 消费；旧 binding compatibility 明确。

## 4. 完成 Gate

- Core/CLI/editor-server、Compiler、Runtime binding focused tests 全通过。
- Unity refresh/compile/domain reload、Console 0 error 通过。
- 实际记录包含 schema version、fixture、hash、迁移/无迁移边界和限制。

## 5. 实际记录

| 项目 | 当前记录 |
| --- | --- |
| 状态 | 见总控页；2026-08-18 `Verified` |
| 实际分支 / 提交 | `main`；CD-M3 里程碑提交待 T6 一并收口 |
| 受影响文件 | Authoring Geometry/Validation/Compiler、CLI package load/editor-server save、RuntimeSpawn contracts/parser、Dragon14 source/generated binding、Authoring 与 Unity focused tests |
| Authoring / Compiler tests | `character-engagement-anchors` 5 条合同路径通过；Dragon14 CLI validate `Ready`；两次 compile 的 `geometry_binding.json` SHA256 均为 `7AA71F78BB4160E2CB7ABD9DF73875DCB1DD46CE86D8838992EE5A9D7EB7F2E2` |
| Unity / Runtime binding | `mx.characterGeometryBinding.v3` / schema `3.0`；Dragon14 revision `1`、Anchor content hash `sha256:31d8e9c1c12ccf7a1e8271e420be84fdd5d021355e852fb812dea7361d8fc5f7`、6 anchors；Unity compile/domain reload/Console 0（job `ba9b9ba88d574e2284fcfb3c040a02bf`）；Runtime binding 3/3（job `c1bf3e81d89a4bd2985a509463e587e7`） |
| 兼容 / 迁移边界 | v1/v2 明确进入 `CompatibilityRootApproach`；缺文件不补写、不带空字段；unknown/invalid 原 payload 只读并阻断保存/编译；不存在历史 EngagementAnchor schema，未伪造迁移 |
| 已知限制 | T2 前不生成 CandidatePose；正式 phase controller 尚不存在，fixture 使用 `phase.default` |
