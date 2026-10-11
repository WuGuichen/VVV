# LCC-M0-T4：Dragon14 Presence fixture 与诊断投影

> Status: Guide
>
> 工作包：LCC-M0-T4（正式数据与最小诊断）
>
> 状态权威：[LCC-M0 第 4 节](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M0_CONTRACT_FOUNDATION.md)
>
> 执行文档状态：`Production-Frozen`
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
> 前置：T2、T3 `Verified`。

## 1. 任务结果与非目标

Dragon14 作者包具有最多 4 个 ModelRoot Presence volume，经正式生成链得到 v4 binding；最小调试投影能分别观察 Hard Root、Presence 与中心 X/Z 合同债务，但不改变 Gameplay。

非目标：不启用软压力、不把翼膜/尾尖/头尖变成常驻体积、不静默修正 motor center、不手工编辑生成 binding/prefab。

## 2. 写入所有权

- Owner：`Authoring/CharacterPackages/character-000003/geometry/body_presence.json`、正式生成器产生的 Dragon14 versioned outputs、CharacterTest 专属 Presence debug snapshot/projection 与对应 tests。`ArtSource/Characters/000003/**` 只保存美术源，不得建立第二份几何作者真源。
- 正式资产只通过 Authoring generator/Unity Editor/Unity MCP 写入；生成结果与 `.meta` 按 write plan 管理。
- 只读：Combat separation、RuntimeSpawn parser、CharacterTest motion/contact、CharacterStudio web。
- debug 共享文件若已由其它改动占用，新增专属 partial/source，禁止覆盖现有用户修改。

## 3. 实施清单

- [x] 以胸腹核心、骨盆/尾根、肩颈根部拟合 3 个稳定 capsule；明确 local pose、skin、pressureScale 和 ID。
- [x] 明确排除翼膜、尾尖、头尖与攻击窗口 Trace；作者 fixture 与 focused test 锁定边界。
- [x] 通过正式 validate/compile/import 生成 v4 binding 和 source mapping，未手改 generated JSON；重复导入 `updated=0 / skipped=49 / conflicts=0 / errors=0`。
- [x] Dragon14 实际触发 `CHARPRES013_ROOT_CAPSULE_CENTER_XZ_NOT_CONSUMED`；debug 投影显示 source `(0, 0.5)` 与 runtime-consumed `(0, 0)`，不改变 root capsule。
- [x] 最小 overlay/debug snapshot 分层标识 Hard Root、Body Presence、Animated Hurtbox；Active Trace 沿用现有可观察面。
- [x] fixture/binding/debug tests 验证 volume 数、稳定 ID、层级分离、natural attack Trace 分离和 Gameplay 无行为变化。

## 4. 验证与证据

- Authoring validate/compile 的原始报告、生成 hash/source mapping 和 git diff；重复生成零非确定性差异。
- Unity refresh/compile/Console 0；打开正式 Dragon14 Playable/preview，保存 Game View/overlay 证据，且角色接触行为与 M0 前一致。
- 核对生成包没有手写漂移，作者源可重建；中心 X/Z 诊断文本准确说明“未消费”而非“已修复”。
- 测试 Dragon14 natural attacks 保持既有 Trace 行为。

## 5. 停止条件与派发

若正式作者真源不唯一、生成器会覆盖未知数据、需要修改 motor/contact 或需要超过 4 个 volume，停止交主代理；不得临时绕过生成链。回报作者源、生成命令、输出、画面证据和限制。

## 6. 实际执行记录

| 项目 | 当前记录 |
| --- | --- |
| 实际分支 / 提交 | `main@8a9dd405b` 工作区候选；未提交，保留既有非 LCC 修改 |
| 实际修改文件 | `geometry/body_presence.json`；12 个 Dragon14 正式生成输出；`CharacterTestLargeCharacterPresenceDebug.cs`、两个最小接入点、focused tests 与 `.meta` |
| 测试与原始证据 | 实现者与主代理最终均为 3/3；主代理 job `57c26645ba5144d0a531f91b911317ba`；正式 validate/双 compile/import 通过，7 个 compile 输出重复 SHA256 一致 |
| 集成 / 回滚结果 | 作者 JSON 是唯一真源，generated 只由正式链重建；删除 fixture/专属 debug 文件并重跑 import 即可回退 v1～v3，无 Gameplay 状态迁移 |
| 状态历史 | 2026-08-24：`Planned` → `In Progress` → `Verified` |
| 执行者 / 审查者 | fixture/debug 由实现代理完成；另一代理只读提供 Hurtbox/Prefab 测量依据；主代理复核代码、生成 provenance、图像与测试 |
| Dragon14 作者 / 生成资产 | 3 个 ModelRoot capsule：neck、tail_root、torso；v4/revision 1，Presence hash `sha256:0673cd534daef52c6f5679e707259796f4218e43ecfaf11e365fbc9f4fd20938`，4 条 source mapping |
| 诊断 / overlay | `TryCapture=true / Authored / volumes=3 / centerDebt=true`；amber Hard Root、cyan Presence、magenta Animated Hurtbox；`CHARPRES013` 在 validate/compile/import 可见 |
| Unity / Game View | 最终脚本验证 job `e1b8b3f39aad4ea8bbdf2bcee5974b8b` 完整通过、Console 0；Game View `Temp/LCC-M0-T4/000003_gameview_direct_focus.png`，Scene overlay `000003_sceneview_presence_core.png` |
| 限制 | Unity camera capture 不合成 Gizmo，故 Game View 角色实景与 Scene View 分层分别取证；Animated Hurtbox 较密；skin/pressure 是中性初值，idle/turn/attack 全姿态拟合和接触行为属于 M1 |
